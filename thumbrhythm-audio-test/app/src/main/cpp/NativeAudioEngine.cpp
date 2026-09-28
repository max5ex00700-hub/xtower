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
constexpr int kMaxVoices = 32;
constexpr int kCommandCount = 128;

struct DrumSample {
    std::shared_ptr<std::vector<float>> pcm;
    float normalize = 1.0f;
};

struct HitCommand {
    int sample = 0; // 0=stick, 1=hat
    float gain = 1.0f;
};

struct Voice {
    bool active = false;
    int sample = 0;
    int64_t cursor = 0;
    float gain = 1.0f;
};

class DrumEngine final : public oboe::AudioStreamDataCallback,
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
        b.setContentType(oboe::ContentType::Sonification);
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
        const int32_t burst = stream_->getFramesPerBurst();
        if (burst > 0) stream_->setBufferSizeInFrames(burst * 2);
        r = stream_->requestStart();
        LOGI("Oboe started: %s rate=%d burst=%d", oboe::convertToText(r),
             stream_->getSampleRate(), burst);
        return r == oboe::Result::OK;
    }

    void stop() {
        std::lock_guard<std::mutex> guard(streamMutex_);
        if (!stream_) return;
        stream_->requestStop();
        stream_->close();
        stream_.reset();
    }

    bool loadDrums(const std::string& stickPath, const std::string& hatPath) {
        DrumSample stick, hat;
        if (!decode(stickPath, stick)) return false;
        if (!decode(hatPath, hat)) return false;
        samples_[0] = std::move(stick);
        samples_[1] = std::move(hat);
        loaded_.store(true, std::memory_order_release);
        return start();
    }

    void hit(int mode, float userGain) {
        if (!loaded_.load(std::memory_order_acquire)) return;
        userGain = std::clamp(userGain, 0.20f, 2.50f);
        if (mode == 0) {
            enqueue(0, userGain);
        } else if (mode == 1) {
            enqueue(1, userGain);
        } else {
            // "착" mix: firm stick body + a light closed-hat edge.
            enqueue(0, userGain * 0.92f);
            enqueue(1, userGain * 0.34f);
        }
    }

    oboe::DataCallbackResult onAudioReady(
        oboe::AudioStream*, void* audioData, int32_t numFrames) override {
        float* out = static_cast<float*>(audioData);
        std::fill(out, out + static_cast<size_t>(numFrames) * kChannels, 0.0f);
        drainCommands();

        for (int f = 0; f < numFrames; ++f) {
            float l = 0.0f, r = 0.0f;
            for (auto& v : voices_) {
                if (!v.active) continue;
                const auto pcm = samples_[v.sample].pcm;
                if (!pcm || pcm->empty()) {
                    v.active = false;
                    continue;
                }
                const int64_t totalFrames = static_cast<int64_t>(pcm->size() / kChannels);
                if (v.cursor >= totalFrames) {
                    v.active = false;
                    continue;
                }

                float env = 1.0f;
                const int64_t fadeFrames = std::min<int64_t>(160, totalFrames / 4);
                if (fadeFrames > 0 && v.cursor > totalFrames - fadeFrames) {
                    env = std::max(0.0f, (totalFrames - v.cursor) / float(fadeFrames));
                }

                const size_t p = static_cast<size_t>(v.cursor) * kChannels;
                const float g = v.gain * samples_[v.sample].normalize * env;
                l += (*pcm)[p] * g;
                r += (*pcm)[p + 1] * g;
                ++v.cursor;
            }

            // Transparent-ish safety limiter for stacked rapid hits.
            out[f * 2] = std::tanh(l * 0.92f);
            out[f * 2 + 1] = std::tanh(r * 0.92f);
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
    bool decode(const std::string& path, DrumSample& out) {
        ma_decoder_config cfg = ma_decoder_config_init(ma_format_f32, kChannels, kSampleRate);
        ma_decoder decoder{};
        ma_result result = ma_decoder_init_file(path.c_str(), &cfg, &decoder);
        if (result != MA_SUCCESS) {
            LOGE("decode init failed %s: %d", path.c_str(), result);
            return false;
        }

        ma_uint64 totalFrames = 0;
        if (ma_decoder_get_length_in_pcm_frames(&decoder, &totalFrames) != MA_SUCCESS || totalFrames == 0) {
            ma_decoder_uninit(&decoder);
            return false;
        }

        auto pcm = std::make_shared<std::vector<float>>();
        pcm->resize(static_cast<size_t>(totalFrames) * kChannels);
        ma_uint64 framesRead = 0;
        result = ma_decoder_read_pcm_frames(&decoder, pcm->data(), totalFrames, &framesRead);
        ma_decoder_uninit(&decoder);
        if (framesRead == 0) return false;
        pcm->resize(static_cast<size_t>(framesRead) * kChannels);

        float peak = 0.0f;
        for (float s : *pcm) peak = std::max(peak, std::abs(s));
        out.pcm = std::move(pcm);
        out.normalize = peak > 0.0001f ? std::min(2.5f, 0.92f / peak) : 1.0f;
        LOGI("Loaded drum %s: frames=%llu peak=%.3f norm=%.3f",
             path.c_str(), (unsigned long long)framesRead, peak, out.normalize);
        return true;
    }

    void enqueue(int sample, float gain) {
        uint32_t w = write_.load(std::memory_order_relaxed);
        uint32_t next = (w + 1) % kCommandCount;
        uint32_t r = read_.load(std::memory_order_acquire);
        if (next == r) {
            read_.store((r + 1) % kCommandCount, std::memory_order_release);
        }
        commands_[w] = {sample, gain};
        write_.store(next, std::memory_order_release);
    }

    void drainCommands() {
        uint32_t r = read_.load(std::memory_order_relaxed);
        const uint32_t w = write_.load(std::memory_order_acquire);
        while (r != w) {
            const HitCommand cmd = commands_[r];
            Voice* v = nullptr;
            for (auto& candidate : voices_) {
                if (!candidate.active) { v = &candidate; break; }
            }
            if (!v) v = &voices_[steal_++ % voices_.size()];
            *v = {true, cmd.sample, 0, cmd.gain};
            r = (r + 1) % kCommandCount;
        }
        read_.store(r, std::memory_order_release);
    }

    std::shared_ptr<oboe::AudioStream> stream_;
    std::mutex streamMutex_;
    std::array<DrumSample, 2> samples_{};
    std::atomic<bool> loaded_{false};
    std::array<Voice, kMaxVoices> voices_{};
    std::array<HitCommand, kCommandCount> commands_{};
    std::atomic<uint32_t> write_{0};
    std::atomic<uint32_t> read_{0};
    size_t steal_ = 0;
};

DrumEngine engine;
}

extern "C" JNIEXPORT jboolean JNICALL
Java_com_thumb_rhythm_audiotest_NativeAudio_nativeLoadDrums(
    JNIEnv* env, jclass, jstring stickPath, jstring hatPath) {
    const char* s = env->GetStringUTFChars(stickPath, nullptr);
    const char* h = env->GetStringUTFChars(hatPath, nullptr);
    bool ok = s && h && engine.loadDrums(s, h);
    if (s) env->ReleaseStringUTFChars(stickPath, s);
    if (h) env->ReleaseStringUTFChars(hatPath, h);
    return ok ? JNI_TRUE : JNI_FALSE;
}

extern "C" JNIEXPORT void JNICALL
Java_com_thumb_rhythm_audiotest_NativeAudio_nativeDrumHit(
    JNIEnv*, jclass, jint mode, jfloat gain) {
    engine.hit(mode, gain);
}

extern "C" JNIEXPORT void JNICALL
Java_com_thumb_rhythm_audiotest_NativeAudio_nativeStart(JNIEnv*, jclass) {
    engine.start();
}

extern "C" JNIEXPORT void JNICALL
Java_com_thumb_rhythm_audiotest_NativeAudio_nativeStop(JNIEnv*, jclass) {
    engine.stop();
}
