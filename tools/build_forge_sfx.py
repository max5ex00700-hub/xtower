#!/usr/bin/env python3
"""Prepare CC0 recorded anvil strikes, without synthesized tones or pitch shifts.

Source/license: tools/audio_sources/README.md. Requires numpy and scipy.
The lossless excerpt is in Git. Output uses the 44-byte PCM header Unity expects.
"""
from pathlib import Path
import hashlib
import wave
import numpy as np
from scipy.signal import resample_poly

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'tools/audio_sources/bigsoundbank_3589_excerpt.wav'
DEST = ROOT / 'unity/XTapUnity/Assets/Resources/XTapBlacksmithUI'
RATE = 44100


def read_recording():
    with wave.open(str(SOURCE), 'rb') as f:
        assert (f.getnchannels(), f.getsampwidth(), f.getframerate()) == (1, 3, 48000)
        count = f.getnframes()
        raw = f.readframes(count)
    assert count == 168000 and len(raw) == count * 3
    b = np.frombuffer(raw, dtype=np.uint8).reshape(-1, 3).astype(np.int32)
    pcm = b[:, 0] | (b[:, 1] << 8) | (b[:, 2] << 16)
    pcm = (pcm ^ 0x800000) - 0x800000
    return pcm.astype(np.float64) / 8388608


def prepare_hit(recording, name, search_start, duration, tail_fade, peak):
    lo = round(search_start * 48000)
    window = recording[lo:lo + 24000]
    contact = lo + int(np.flatnonzero(np.abs(window) > .08)[0])
    # Retain 1 ms before the impact to keep the real attack aligned to contact.
    start = contact - 48
    end = start + round(duration * 48000)
    assert 0 <= start < end <= len(recording)
    samples = recording[start:end].copy()
    samples -= np.mean(samples)
    samples = resample_poly(samples, 147, 160)  # 48 -> 44.1 kHz, original pitch
    samples = samples[:round(duration * RATE)]
    attack = max(2, round(.0002 * RATE))
    fade = round(tail_fade * RATE)
    samples[:attack] *= np.linspace(0, 1, attack)
    samples[-fade:] *= np.linspace(1, 0, fade)
    samples *= peak / np.max(np.abs(samples))
    pcm = np.rint(samples * 32767).astype('<i2')
    path = DEST / ('forge_hammer_' + name + '.wav')
    with wave.open(str(path), 'wb') as f:
        f.setparams((1, 2, RATE, len(pcm), 'NONE', 'not compressed'))
        f.writeframes(pcm.tobytes())
    # Preserve existing GUIDs and PCM import settings.
    assert path.with_suffix('.wav.meta').exists()
    with wave.open(str(path), 'rb') as f:
        assert (f.getnchannels(), f.getsampwidth(), f.getframerate()) == (1, 2, RATE)
        decoded = np.frombuffer(f.readframes(f.getnframes()), dtype='<i2') / 32768
    onset = np.flatnonzero(np.abs(decoded) > .05)[0] / RATE
    assert len(path.read_bytes()) == 44 + len(decoded) * 2
    assert len(decoded) == round(duration * RATE) and onset < .003
    assert np.max(np.abs(decoded)) < .90 and abs(np.mean(decoded)) < .01
    assert np.sqrt(np.mean(decoded**2)) > .025 and decoded[-1] == 0
    print(f'{path.name}: source {start/48000:.6f}..{end/48000:.6f}s; '
          f'{duration:.2f}s; onset {onset*1000:.2f}ms; '
          f'peak {np.max(np.abs(decoded)):.3f}; '
          f'sha256 {hashlib.sha256(path.read_bytes()).hexdigest()}')


if __name__ == '__main__':
    recording = read_recording()
    prepare_hit(recording, 'angel', 0, .58, .045, .82)
    prepare_hit(recording, 'demon', 1.2, .62, .050, .82)
    prepare_hit(recording, 'final', 2.3, 1.00, .090, .88)
