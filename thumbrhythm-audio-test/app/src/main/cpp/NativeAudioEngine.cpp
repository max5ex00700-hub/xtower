#include <jni.h>
#include <android/log.h>
#include <oboe/Oboe.h>
#include <atomic>
#include <algorithm>
#include <array>
#include <cmath>
#include <cstdint>
#include <memory>
#include <mutex>
#include <string>
#include <vector>

#define MINIAUDIO_IMPLEMENTATION
#define MA_NO_DEVICE_IO
#include "miniaudio.h"

#define LOGI(...) __android_log_print(ANDROID_LOG_INFO, "ThumbRhythmNative", __VA_ARGS__)
#define LOGE(...) __android_log_print(ANDROID_LOG_ERROR, "ThumbRhythmNative", __VA_ARGS__)

namespace {
constexpr int kSampleRate = 48000;
constexpr int kChannels = 2;
constexpr int kMaxVoices = 24;
constexpr int kCommandCount = 64;
constexpr float kPreRollSec = 0.018f;
constexpr float kHitLengthSec = 0.220f;

struct HitCommand {
    int64_t start = 0;
    int64_t end = 0;
    float gain = 1.0f;
};

struct Voice {
    bool active = false;
    int64_t cursor = 0;
    int64_t start = 0;
    int64_t end = 0;
    float gain = 1.0f;
};

class KeySoundEngine final : public oboe::AudioStreamDataCallback,
                             public oboe::AudioStreamErrorCallback {
public:
    bool start() {
        std::lock_guard<std::mutex> guard(streamMutex_);
        if (stream_) return true;

        oboe::AudioStreamBuilder b;
        b.setDirection(oboe::Direction::Output);
        b.setPerformanceMode(oboe::PerformanceMode::LowLatency);
        b.setSharingMode(oboe::SharingMode::Exclusive);
        b.setFormat(oboe::AudioFormat::Float);
        b.setChannelCount(kChannels);
        b.setSampleRate(kSampleRate);
        b.setUsage(oboe::Usage::Game);
        b.setContentType(oboe::ContentType::Music);
        b.setDataCallback(this);
        b.setErrorCallback(this);

        oboe::Result r = b.openStream(stream_);
        if (r != oboe::Result::OK) {
            LOGI("Exclusive failed: %s. Retrying Shared.", oboe::convertToText(r));
            b.setSharingMode(oboe::SharingMode::Shared);
            r = b.openStream(stream_);
        }
        if (r != oboe::Result::OK || !stream_) {
            LOGE("openStream failed: %s", oboe::convertToText(r));
            stream_.reset();
            return false;
        }

        auto burst = stream_->getFramesPerBurst();
        if (burst > 0) stream_->setBufferSizeInFrames(burst * 2);
        r = stream_->requestStart();
        LOGI("start=%s rate=%d burst=%d", oboe::convertToText(r),
             stream_->getSampleRate(), stream_->getFramesPerBurst());
        return r == oboe::Result::OK;
    }

    void stop() {
        std::lock_guard<std::mutex> guard(streamMutex_);
        if (!stream_) return;
        stream_->requestStop();
        stream_->close();
        stream_.reset();
    }

    bool loadSong(const std::string& path) {
        ma_decoder_config cfg = ma_decoder_config_init(ma_format_f32, kChannels, kSampleRate);
        ma_decoder decoder{};
        ma_result result = ma_decoder_init_file(path.c_str(), &cfg, &decoder);
        if (result != MA_SUCCESS) {
            LOGE("ma_decoder_init_file failed: %d", result);
            return false;
        }

        ma_uint64 totalFrames = 0;
        result = ma_decoder_get_length_in_pcm_frames(&decoder, &totalFrames);
        if (result != MA_SUCCESS || totalFrames == 0) {
            ma_decoder_uninit(&decoder);
            LOGE("Could not get decoded length: %d", result);
            return false;
        }

        auto decoded = std::make_shared<std::vector<float>>();
        try {
            decoded->resize(static_cast<size_t>(totalFrames) * kChannels);
        } catch (...) {
            ma_decoder_uninit(&decoder);
            return false;
        }

        ma_uint64 framesRead = 0;
        result = ma_decoder_read_pcm_frames(&decoder, decoded->data(), totalFrames, &framesRead);
        ma_decoder_uninit(&decoder);
        if (framesRead == 0) {
            LOGE("No PCM decoded: %d", result);
            return false;
        }
        decoded->resize(static_cast<size_t>(framesRead) * kChannels);

        std::atomic_store_explicit(&song_, decoded, std::memory_order_release);
        songFrames_.store(static_cast<int64_t>(framesRead), std::memory_order_release);
        LOGI("Loaded %.2f seconds of PCM", framesRead / double(kSampleRate));
        return start();
    }

    void hit(double positionSec, float userGain) {
        const int64_t total = songFrames_.load(std::memory_order_acquire);
        if (total <= 0 || !std::isfinite(positionSec)) return;

        int64_t attack = static_cast<int64_t>(positionSec * kSampleRate);
        int64_t startFrame = std::max<int64_t>(
            0, attack - static_cast<int64_t>(kPreRollSec * kSampleRate));
        int64_t endFrame = std::min<int64_t>(
            total, startFrame + static_cast<int64_t>(kHitLengthSec * kSampleRate));
        if (endFrame - startFrame < 128) return;

        float rms = rmsAt(startFrame,
                          std::min<int64_t>(endFrame, startFrame + kSampleRate / 8));
        float autoGain = rms > 0.001f ? std::clamp(0.18f / rms, 0.75f, 3.0f) : 1.0f;
        float gain = std::clamp(userGain * autoGain, 0.35f, 2.6f);

        uint32_t w = write_.load(std::memory_order_relaxed);
        uint32_t next = (w + 1) % kCommandCount;
        uint32_t r = read_.load(std::memory_order_acquire);
        if (next == r) {
            read_.store((r + 1) % kCommandCount, std::memory_order_release);
        }
        commands_[w] = {startFrame, endFrame, gain};
        write_.store(next, std::memory_order_release);
    }

    oboe::DataCallbackResult onAudioReady(
        oboe::AudioStream*, void* audioData, int32_t numFrames) override {
        float* out = static_cast<float*>(audioData);
        std::fill(out, out + static_cast<size_t>(numFrames) * kChannels, 0.0f);
        drainCommands();

        auto song = std::atomic_load_explicit(&song_, std::memory_order_acquire);
        const int64_t total = songFrames_.load(std::memory_order_acquire);
        if (!song || song->empty() || total <= 0)
            return oboe::DataCallbackResult::Continue;

        const float* pcm = song->data();

        for (int f = 0; f < numFrames; ++f) {
            float left = 0.0f, right = 0.0f;
            for (auto& v : voices_) {
                if (!v.active) continue;
                if (v.cursor >= v.end || v.cursor >= total) {
                    v.active = false;
                    continue;
                }

                const int64_t rel = v.cursor - v.start;
                const int64_t len = std::max<int64_t>(1, v.end - v.start);
                const int64_t attackFrames = static_cast<int64_t>(0.006 * kSampleRate);
                float env = 1.0f;
                if (rel < attackFrames) {
                    env = rel / float(std::max<int64_t>(1, attackFrames));
                } else {
                    float x = (rel - attackFrames) /
                              float(std::max<int64_t>(1, len - attackFrames));
                    env = std::pow(std::max(0.0f, 1.0f - x), 1.25f);
                }

                size_t p = static_cast<size_t>(v.cursor) * kChannels;
                left += pcm[p] * v.gain * env;
                right += pcm[p + 1] * v.gain * env;
                ++v.cursor;
            }

            // Soft saturation only on the keysound bus.
            out[f * 2] = std::tanh(left * 0.88f);
            out[f * 2 + 1] = std::tanh(right * 0.88f);
        }

        return oboe::DataCallbackResult::Continue;
    }

    void onErrorAfterClose(oboe::AudioStream*, oboe::Result error) override {
        LOGE("Oboe error after close: %s", oboe::convertToText(error));
        {
            std::lock_guard<std::mutex> guard(streamMutex_);
            stream_.reset();
        }
        start();
    }

private:
    float rmsAt(int64_t startFrame, int64_t endFrame) {
        auto song = std::atomic_load_explicit(&song_, std::memory_order_acquire);
        if (!song || song->empty()) return 0.0f;
        int64_t total = static_cast<int64_t>(song->size() / kChannels);
        endFrame = std::min(endFrame, total);
        if (endFrame <= startFrame) return 0.0f;

        double sum = 0.0;
        int64_t count = 0;
        for (int64_t i = startFrame; i < endFrame; i += 4) {
            size_t p = static_cast<size_t>(i) * kChannels;
            float mono = ((*song)[p] + (*song)[p + 1]) * 0.5f;
            sum += double(mono) * mono;
            ++count;
        }
        return count ? static_cast<float>(std::sqrt(sum / count)) : 0.0f;
    }

    void drainCommands() {
        uint32_t r = read_.load(std::memory_order_relaxed);
        uint32_t w = write_.load(std::memory_order_acquire);
        while (r != w) {
            const HitCommand cmd = commands_[r];
            Voice* v = nullptr;
            for (auto& candidate : voices_) {
                if (!candidate.active) { v = &candidate; break; }
            }
            if (!v) v = &voices_[steal_++ % voices_.size()];
            *v = {true, cmd.start, cmd.start, cmd.end, cmd.gain};
            r = (r + 1) % kCommandCount;
        }
        read_.store(r, std::memory_order_release);
    }

    std::shared_ptr<oboe::AudioStream> stream_;
    std::mutex streamMutex_;
    std::shared_ptr<std::vector<float>> song_;
    std::atomic<int64_t> songFrames_{0};

    std::array<Voice, kMaxVoices> voices_{};
    std::array<HitCommand, kCommandCount> commands_{};
    std::atomic<uint32_t> write_{0};
    std::atomic<uint32_t> read_{0};
    size_t steal_ = 0;
};

KeySoundEngine engine;
}

extern "C" JNIEXPORT jboolean JNICALL
Java_com_thumb_rhythm_audiotest_NativeAudio_nativeLoadSong(
    JNIEnv* env, jclass, jstring path) {
    const char* chars = env->GetStringUTFChars(path, nullptr);
    bool ok = chars && engine.loadSong(chars);
    if (chars) env->ReleaseStringUTFChars(path, chars);
    return ok ? JNI_TRUE : JNI_FALSE;
}

extern "C" JNIEXPORT void JNICALL
Java_com_thumb_rhythm_audiotest_NativeAudio_nativeHit(
    JNIEnv*, jclass, jdouble positionSec, jfloat gain) {
    engine.hit(positionSec, gain);
}

extern "C" JNIEXPORT void JNICALL
Java_com_thumb_rhythm_audiotest_NativeAudio_nativeStart(
    JNIEnv*, jclass) {
    engine.start();
}

extern "C" JNIEXPORT void JNICALL
Java_com_thumb_rhythm_audiotest_NativeAudio_nativeStop(
    JNIEnv*, jclass) {
    engine.stop();
}
