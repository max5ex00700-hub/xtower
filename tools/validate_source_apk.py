#!/usr/bin/env python3
"""Check the source APK ZIP, image decoding and reviewed floor 6 identities.

Reports missing artwork without substituting other character images or awarding
unearned codex discoveries. Requires Pillow. This is not a Unity/Android build.
"""
import io
import hashlib
import json
from pathlib import Path
import sys
import zipfile
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
STREAM = ROOT / 'unity/XTapUnity/Assets/StreamingAssets'


def main():
    full = STREAM / 'xtop_source.apk'
    data = full.read_bytes() if full.exists() else b''.join(
        (STREAM / ('xtop_source.part' + str(i))).read_bytes() for i in (1, 2))
    failures = []
    checked = 0
    required = 0
    review = json.loads((ROOT / 'tools/character_art_review.json').read_text())
    supported = 0
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        corrupt = archive.testzip()
        if corrupt:
            failures.append((corrupt, 'ZIP CRC failed'))
        names = set(archive.namelist())
        codes = [p + str(i).zfill(2) for p in ('p', 'k', 'b', 'd') for i in range(10)] + ['cap']
        for character in range(1, 11):
            for code in codes:
                # User-approved 40-image floor 2 codex; k09 is absent in the source.
                if character == 2 and code == 'k09':
                    continue
                required += 1
                if character != 6 or code in review['verified_floor_6']:
                    supported += 1
                name = f'assets/f{character}_{code}.jpg'
                if name not in names:
                    failures.append((name, 'missing required codex image'))
                    continue
                try:
                    image = Image.open(io.BytesIO(archive.read(name)))
                    image.load()
                    checked += 1
                except Exception as error:
                    failures.append((name, str(error)))
        # Pin the visual identity review to the actual bytes. Filename/CRC/decode
        # checks alone cannot detect a valid image of the wrong character.
        for section in ('verified_floor_6', 'rejected_floor_6'):
            for code, expected_hash in review[section].items():
                name = f'assets/f6_{code}.jpg'
                if name in names and hashlib.sha256(archive.read(name)).hexdigest() != expected_hash:
                    failures.append((name, 'bytes changed since character identity review; review the image again'))
    for name, error in failures:
        print(f'FAIL {name}: {error}', file=sys.stderr)
    print(f'Source APK: {checked}/{required} source images decoded; {supported} supported codex images; '
          f'{len(review["rejected_floor_6"])} mixed floor 6 images excluded; {len(failures)} failures')
    return 1 if failures else 0


if __name__ == '__main__':
    sys.exit(main())
