# Recorded forge anvil source

The forge sounds from 11.67 use real recorded hammer strikes, replacing the
oscillator/noise synthesis from 11.66.

- Title: **Anvil #1**, sound 3589
- Author: **Pablo BERGEL**
- Publisher: BigSoundBank / LaSonotheque
- Source page: https://bigsoundbank.com/anvil-1-s3589.html
- License page: https://bigsoundbank.com/licenses.html
- License listed on the source page: **CC0 (public domain)**. The page explicitly
  permits editing, redistribution, use in games/apps and commercial use without
  attribution. Source and license checked 2026-09-28.
- The publisher describes an omnidirectional microphone recording of a hammer
  repeatedly striking a blacksmith's anvil.
- Download: source page's public WAV form (`download.php`, then its download form).
- Original WAV: 4,727,472 bytes; mono, 48,000 Hz, 24-bit PCM; 1,575,476 frames.
- Original SHA-256: `ebc51af2da517a3b3a2e21a7f7be1ce0c5942f3583a0a8702506af87d4ca10c0`.

`bigsoundbank_3589_excerpt.wav` preserves the first 168,000 original PCM frames
(0.000–3.500 seconds), with a standard WAV header and without the original
container's extra metadata chunks. No sample processing was applied to this
source excerpt. It stays outside Unity Assets so it does not enter the APK.

Excerpt SHA-256:
`dee234e7e07b8c583ef49c0434247ee8155ca44f9a587993b7693ebcb540b140`

Run `python3 tools/build_forge_sfx.py` to rebuild the three game sounds. The script
selects three separate impacts, removes leading silence, removes DC offset,
resamples to mono 44.1 kHz / 16-bit PCM, scales volume and fades the end. It does
not synthesize resonances, transpose the recording, add reverb or change speed.
Existing Unity .meta GUIDs and contact-time playback are retained.

| Game sound | Source interval (seconds) | Length | Peak |
|---|---|---:|---:|
| Angel | 0.136771–0.716771 | 0.58 s | 0.82 |
| Demon | 1.349479–1.969479 | 0.62 s | 0.82 |
| Finisher | 2.424375–3.424375 | 1.00 s | 0.88 |
