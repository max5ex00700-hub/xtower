#!/usr/bin/env python3
"""Validate tracked Unity image bytes before pushing (requires Pillow).

    python3 tools/validate_unity_images.py
    python3 tools/validate_unity_images.py --build-inputs
    python3 tools/validate_unity_images.py --ref <commit>

The second form audits the exact Git objects, without checking out that commit.
This is an asset check, not a Unity compile or Android build.
"""

import argparse
import base64
import io
import json
from pathlib import Path
import struct
import subprocess
import sys
import zlib

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
RESOURCES = "unity/XTapUnity/Assets/Resources/"
PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"
BUILD_INPUTS = {
    "XTapBlacksmithUI/forge_duel_background.bytes",
    "XTapBlacksmithUI/forge_duel_atlas.bytes",
    "XTapGachaUI/block_gear_machine.bytes",
    "XTapGachaUI/block_gear_wheel.bytes",
    "XTapSigilBeat/visual_pack.bytes",
} | {"XTapMainUI/ref_" + name + "_runtime.bytes" for name in (
    "fight", "challenge", "option", "codex", "tab", "nav_prev", "nav_bag",
    "nav_jail", "nav_forge", "nav_next",
)}


def git(*args):
    return subprocess.check_output(["git", *args], cwd=ROOT)


def validate_png(data):
    position = 8
    chunks = []
    image_data = []
    while position < len(data):
        if position + 12 > len(data):
            raise ValueError(f"truncated chunk header at {position}")
        length = struct.unpack_from(">I", data, position)[0]
        kind = data[position + 4:position + 8]
        end = position + 12 + length
        if end > len(data):
            raise ValueError(f"truncated {kind!r}: declares {length} bytes")
        stored = struct.unpack_from(">I", data, end - 4)[0]
        actual = zlib.crc32(data[position + 4:end - 4]) & 0xFFFFFFFF
        if actual != stored:
            raise ValueError(
                f"{kind.decode('ascii', errors='replace')} CRC mismatch at {position}: "
                f"stored={stored:08X}, actual={actual:08X}"
            )
        chunks.append(kind)
        if kind == b"IHDR" and (position != 8 or length != 13):
            raise ValueError("invalid IHDR position or length")
        if kind == b"IDAT":
            image_data.append(data[position + 8:end - 4])
        position = end
        if kind == b"IEND":
            if length:
                raise ValueError("nonempty IEND")
            break
    if not chunks or chunks[0] != b"IHDR" or not image_data or chunks[-1] != b"IEND":
        raise ValueError("missing IHDR, IDAT or IEND")
    if position != len(data):
        raise ValueError("trailing data after IEND")
    # Checks the compressed stream and its independent Adler-32 checksum.
    decoder = zlib.decompressobj()
    decoder.decompress(b"".join(image_data))
    decoder.flush()
    if not decoder.eof or decoder.unused_data or decoder.unconsumed_tail:
        raise ValueError("incomplete or trailing zlib stream")


def validate_image(data):
    if data.startswith(PNG_SIGNATURE):
        validate_png(data)
    elif not (data.startswith(b"\xff\xd8") and data.endswith(b"\xff\xd9")):
        raise ValueError("expected a complete PNG or JPEG")
    with Image.open(io.BytesIO(data)) as image:
        image.verify()
    with Image.open(io.BytesIO(data)) as image:
        image.load()
        return image.format, image.size


def unpack_images(path, data):
    if path.endswith("/XTapSigilBeat/visual_pack.bytes"):
        pack = json.loads(data)
        required = {
            "sigil_background", "thumb_left", "thumb_right", "timing_ring",
            "note_down", "note_right", "note_left", "note_up", "note_tap",
            "slash_left", "slash_right", "judgement_frame", "title_plate",
        }
        if set(pack) != required:
            raise ValueError("visual pack image keys do not match the 13 required images")
        for key, encoded in sorted(pack.items()):
            yield f"{path}:{key}", base64.b64decode(encoded, validate=True)
    elif path.endswith(("/XTapBlacksmithUI/atlas.bytes", "/XTapMainUI/main_atlas_runtime.bytes")):
        yield path, base64.b64decode(data.strip(), validate=True)
    else:
        yield path, data


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--ref", help="Audit this Git ref instead of working tree files")
    parser.add_argument("--build-inputs", action="store_true",
                        help="Check only the 27 images required by XTapBuildConfig")
    args = parser.parse_args()
    if args.ref:
        # Resolve the ref once so all reads use the same immutable revision.
        revision = git("rev-parse", "--verify", args.ref + "^{commit}").decode().strip()
        paths = git("ls-tree", "-r", "--name-only", revision, "--", RESOURCES)
    else:
        revision = None
        paths = git("ls-files", "--", RESOURCES)
    failures = []
    checked = 0
    tracked_paths = paths.decode().splitlines()
    if args.build_inputs:
        tracked = set(tracked_paths)
        for relative in sorted(BUILD_INPUTS):
            if RESOURCES + relative not in tracked:
                failures.append((RESOURCES + relative, "required file is not tracked"))
    for path in tracked_paths:
        if args.build_inputs and path[len(RESOURCES):] not in BUILD_INPUTS:
            continue
        if not path.endswith((".png", ".jpg", ".jpeg", ".bytes")):
            continue
        try:
            data = git("show", revision + ":" + path) if revision else (ROOT / path).read_bytes()
            for label, image_data in unpack_images(path, data):
                checked += 1
                try:
                    validate_image(image_data)
                except Exception as error:
                    failures.append((label, str(error)))
        except Exception as error:
            failures.append((path, str(error)))
    for label, error in failures:
        print(f"FAIL {label}: {error}", file=sys.stderr)
    scope = "build inputs" if args.build_inputs else "all resource images"
    print(f"{revision or 'working tree'} ({scope}): {checked} images checked; {len(failures)} failures")
    if not checked:
        print("No tracked images found", file=sys.stderr)
        return 1
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
