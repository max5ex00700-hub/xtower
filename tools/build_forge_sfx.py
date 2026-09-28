#!/usr/bin/env python3
"""Deterministic, original PCM anvil hits: immediate attack + inharmonic metal ring."""
from pathlib import Path
import hashlib
import wave
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / 'unity/XTapUnity/Assets/Resources/XTapBlacksmithUI'
RATE = 44100


def make_hit(name, duration, base, body, seed):
    t = np.arange(round(RATE * duration)) / RATE
    rng = np.random.default_rng(seed)
    modes = ((1, 1, .105), (2.41, .69, .17), (3.89, .43, .10),
             (5.43, .25, .07), (7.13, .12, .035))
    samples = sum(amp * np.sin(2*np.pi*base*ratio*t) * np.exp(-t/decay)
                  for ratio, amp, decay in modes)
    noise = rng.normal(0, 1, len(t))
    noise = noise - np.concatenate(([0], noise[:-1])) * .82
    samples += noise * .48 * np.exp(-t/.007)
    samples += body * np.sin(2*np.pi*176*t) * np.exp(-t/.045)
    samples *= np.minimum(1, t/.00065)  # avoid a digital click, <1 ms attack
    samples *= np.minimum(1, (duration-t)/.045)
    samples *= .88 / np.max(np.abs(samples))
    pcm = np.rint(samples * 32767).astype('<i2')
    path = DEST / (name + '.wav')
    with wave.open(str(path), 'wb') as f:
        f.setparams((1, 2, RATE, len(pcm), 'NONE', 'not compressed'))
        f.writeframes(pcm.tobytes())
    meta = path.with_suffix('.wav.meta')
    if not meta.exists():
        guid = hashlib.sha256(name.encode()).hexdigest()[:32]
        meta.write_text(f'''fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 7
  defaultSettings:
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 0
    quality: 1
    conversionMode: 0
  platformSettingOverrides: {{}}
  forceToMono: 1
  normalize: 0
  preloadAudioData: 1
  loadInBackground: 0
  ambisonic: 0
  3D: 0
  userData:
  assetBundleName:
  assetBundleVariant:
''')
    # Read actual written data: detect truncation, clipping, leading silence and DC.
    with wave.open(str(path), 'rb') as f:
        decoded = np.frombuffer(f.readframes(f.getnframes()), dtype='<i2') / 32768
        assert f.getframerate() == RATE and len(decoded) == len(t)
    onset = np.flatnonzero(np.abs(decoded) > .01)[0] / RATE
    assert onset < .003 and np.max(np.abs(decoded)) < .99
    assert abs(np.mean(decoded)) < .01 and np.sqrt(np.mean(decoded**2)) > .035
    print(f'{path.name}: {len(decoded)/RATE:.2f}s, onset {onset*1000:.2f}ms, peak {np.max(np.abs(decoded)):.3f}')


if __name__ == '__main__':
    make_hit('forge_hammer_angel', .38, 760, .28, 116601)
    make_hit('forge_hammer_demon', .42, 570, .58, 116602)
    make_hit('forge_hammer_final', .62, 650, 1.10, 116603)
