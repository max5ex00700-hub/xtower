#include <jni.h>
#include <android/log.h>
#include <oboe/Oboe.h>
#include <algorithm>
#include <array>
#include <atomic>
#include <cmath>
#include <cstdint>
#include <memory>
#include <mutex>
#include <string>
#include <vector>
#include "miniaudio.h"

#define LOGI(...) __android_log_print(ANDROID_LOG_INFO, "ThumbRhythmDrum", __VA_ARGS__)
#define LOGE(...) __android_log_print(ANDROID_LOG_ERROR, "ThumbRhythmDrum", __VA_ARGS__)

namespace {
constexpr int kRate = 48000;
constexpr int kChannels = 2;
constexpr int kVoices = 32;
constexpr int kQueue = 64;

struct Sample {
    std::vector<float> pcm;
    int64_t frames = 0;
};

struct Cmd {
    int sample = 0;
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
        std::lock_guard<std::mutex> lock(mutex_);
        if (stream_) return true;

        oboe::AudioStreamBuilder b;
        b.setDirection(oboe::Direction::Output)
         .setPerformanceMode(oboe::PerformanceMode::LowLatency)
         .setSharingMode(oboe::SharingMode::Exclusive)
         .setFormat(oboe::AudioFormat::Float)
         .setChannelCount(kChannels)
         .setSampleRate(kRate)
         .setUsage(oboe::Usage::Game)
         .setContentType(oboe::ContentType::Music)
         .setDataCallback(this)
         .setErrorCallback(this);

        oboe::Result r = b.openStream(stream_);
        if (r != oboe::Result::OK) {
            b.setSharingMode(oboe::SharingMode::Shared);
            r = b.openStream(stream_);
        }
        if (r != oboe::Result::OK || !stream_) {
            LOGE("openStream failed: %s", oboe::convertToText(r));
            stream_.reset();
            return false;
        }
        int32_t burst = stream_->getFramesPerBurst();
        if (burst > 0) stream_->setBufferSizeInFrames(burst * 2);
        r = stream_->requestStart();
        LOGI("start %s burst=%d rate=%d", oboe::convertToText(r), burst, stream_->getSampleRate());
        return r == oboe::Result::OK;
    }

    void stop() {
        std::lock_guard<std::mutex> lock(mutex_);
        if (!stream_) return;
        stream_->requestStop();
        stream_->close();
        stream_.reset();
    }

    bool load(int index, const std::string& path) {
        if (index < 0 || index > 1) return false;
        ma_decoder_config cfg = ma_decoder_config_init(ma_format_f32, kChannels, kRate);
        ma_decoder dec{};
        ma_result r = ma_decoder_init_file(path.c_str(), &cfg, &dec);
        if (r != MA_SUCCESS) {
            LOGE("decoder init failed %d", r);
            return false;
        }
        ma_uint64 total = 0;
        if (ma_decoder_get_length_in_pcm_frames(&dec, &total) != MA_SUCCESS || total == 0) {
            ma_decoder_uninit(&dec);
            return false;
        }
        Sample s;
        s.pcm.resize(static_cast<size_t>(total) * kChannels);
        ma_uint64 got = 0;
        ma_decoder_read_pcm_frames(&dec, s.pcm.data(), total, &got);
        ma_decoder_uninit(&dec);
        if (got == 0) return false;
        s.frames = static_cast<int64_t>(got);
        s.pcm.resize(static_cast<size_t>(got) * kChannels);
        samples_[index] = std::move(s);
        LOGI("sample %d loaded frames=%lld", index, (long long)samples_[index].frames);
        return start();
    }

    void hit(int mode, float gain) {
        gain = std::clamp(gain, 0.1f, 2.5f);
        if (mode == 2) {
            enqueue(0, gain);
            enqueue(1, gain * 0.42f);
        } else {
            enqueue(mode == 1 ? 1 : 0, gain);
        }
    }

    oboe::DataCallbackResult onAudioReady(oboe::AudioStream*, void* data, int32_t frames) override {
        float* out = static_cast<float*>(data);
        std::fill(out, out + static_cast<size_t>(frames) * kChannels, 0.0f);
        drain();

        for (int32_t f = 0; f < frames; ++f) {
            float l = 0.0f, r = 0.0f;
            for (auto& v : voices_) {
                if (!v.active) continue;
                const Sample& s = samples_[v.sample];
                if (v.cursor >= s.frames || s.frames <= 0) {
                    v.active = false;
                    continue;
                }
                const int64_t attack = std::max<int64_t>(1, kRate / 1000); // 1 ms
                const int64_t release = std::max<int64_t>(1, kRate / 200); // 5 ms
                float env = 1.0f;
                if (v.cursor < attack) env = v.cursor / float(attack);
                int64_t remain = s.frames - v.cursor;
                if (remain < release) env *= remain / float(release);

                size_t p = static_cast<size_t>(v.cursor) * kChannels;
                l += s.pcm[p] * v.gain * env;
                r += s.pcm[p + 1] * v.gain * env;
                ++v.cursor;
            }
            // Gentle saturation preserves attack while preventing ugly clipping on repeated hits.
            out[f * 2] = std::tanh(l * 0.95f);
            out[f * 2 + 1] = std::tanh(r * 0.95f);
        }
        return oboe::DataCallbackResult::Continue;
    }

    void onErrorAfterClose(oboe::AudioStream*, oboe::Result error) override {
        LOGE("stream closed: %s", oboe::convertToText(error));
        {
            std::lock_guard<std::mutex> lock(mutex_);
            stream_.reset();
        }
        start();
    }

private:
    void enqueue(int sample, float gain) {
        if (sample < 0 || sample > 1 || samples_[sample].frames <= 0) return;
        uint32_t w = write_.load(std::memory_order_relaxed);
        uint32_t n = (w + 1) % kQueue;
        uint32_t r = read_.load(std::memory_order_acquire);
        if (n == r) read_.store((r + 1) % kQueue, std::memory_order_release);
        queue_[w] = {sample, gain};
        write_.store(n, std::memory_order_release);
    }

    void drain() {
        uint32_t r = read_.load(std::memory_order_relaxed);
        uint32_t w = write_.load(std::memory_order_acquire);
        while (r != w) {
            const Cmd c = queue_[r];
            Voice* v = nullptr;
            for (auto& x : voices_) {
                if (!x.active) { v = &x; break; }
            }
            if (!v) v = &voices_[steal_++ % voices_.size()];
            *v = {true, c.sample, 0, c.gain};
            r = (r + 1) % kQueue;
        }
        read_.store(r, std::memory_order_release);
    }

    std::array<Sample, 2> samples_{};
    std::array<Voice, kVoices> voices_{};
    std::array<Cmd, kQueue> queue_{};
    std::atomic<uint32_t> write_{0}, read_{0};
    size_t steal_ = 0;
    std::shared_ptr<oboe::AudioStream> stream_;
    std::mutex mutex_;
};

DrumEngine drum;
}

extern "C" JNIEXPORT jboolean JNICALL
Java_com_thumb_rhythm_audiotest_NativeAudio_nativeLoadDrum(
    JNIEnv* env, jclass, jint index, jstring path) {
    const char* p = env->GetStringUTFChars(path, nullptr);
    bool ok = p && drum.load(index, p);
    if (p) env->ReleaseStringUTFChars(path, p);
    return ok ? JNI_TRUE : JNI_FALSE;
}

extern "C" JNIEXPORT void JNICALL
Java_com_thumb_rhythm_audiotest_NativeAudio_nativeDrumHit(
    JNIEnv*, jclass, jint mode, jfloat gain) {
    drum.hit(mode, gain);
}

extern "C" JNIEXPORT void JNICALL
Java_com_thumb_rhythm_audiotest_NativeAudio_nativeDrumStop(
    JNIEnv*, jclass) {
    drum.stop();
}
