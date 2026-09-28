#!/usr/bin/env python3
"""Replace undecodable legacy UI panels with reproducible SVG-authored atlases.

Requires CairoSVG. Preserves every existing sprite rectangle and 9-slice border.
No character, roulette, or forge duel artwork is changed.
"""
from pathlib import Path
import base64
import hashlib
import re
import cairosvg

ROOT = Path(__file__).resolve().parents[1]
RES = ROOT / 'unity/XTapUnity/Assets/Resources'
SOURCES = ROOT / 'tools/ui_sources/panels'


def panel(x, y, w, h, selected=False):
    edge = '#eac77c' if selected else '#997247'
    return f'''<g transform="translate({x} {y})">
<rect width="{w}" height="{h}" fill="#0a0c13"/>
<rect x="2" y="2" width="{w-4}" height="{h-4}" fill="url(#plate)" stroke="{edge}" stroke-width="2"/>
<rect x="6" y="6" width="{w-12}" height="{h-12}" fill="none" stroke="#54442f"/>
<path d="M10 20 V10 H28 M{w-28} 10 H{w-10} V20 M10 {h-20} V{h-10} H28 M{w-28} {h-10} H{w-10} V{h-20}" fill="none" stroke="{edge}" stroke-width="2"/>
<path d="M14 3 L18 7 L14 11 L10 7 Z M{w-14} {h-3} L{w-18} {h-7} L{w-14} {h-11} L{w-10} {h-7} Z" fill="{edge}"/>
</g>'''


def svg(w, h, body):
    return f'''<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="0 0 {w} {h}">
<defs><linearGradient id="plate" x1="0" y1="0" x2="0" y2="1">
<stop stop-color="#20202a"/><stop offset=".30" stop-color="#11131c"/><stop offset="1" stop-color="#090b12"/>
</linearGradient></defs><rect width="{w}" height="{h}" fill="#090b12"/>{body}</svg>'''


def meta(path):
    target = Path(str(path) + '.meta')
    if target.exists():
        return
    guid = hashlib.sha256(str(path.relative_to(ROOT)).encode()).hexdigest()[:32]
    target.write_text('fileFormatVersion: 2\nguid: ' + guid + '\nTextScriptImporter:\n'
                      '  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n')


def main():
    SOURCES.mkdir(parents=True, exist_ok=True)
    # Common/forge atlas uses top-left coordinates.
    common = panel(0, 0, 144, 256) + panel(144, 0, 112, 84) + panel(144, 84, 112, 112)
    for y in (256, 299):
        common += panel(0, y, 128, 43) + panel(128, y, 128, 43, True)
    for y in (342, 406):
        common += panel(0, y, 256, 64)
    # Main atlas rectangles use Unity bottom-left coordinates; convert to SVG top-left.
    main_panels = panel(0, 0, 256, 83) + panel(256, 83, 256, 83) + panel(0, 166, 128, 192)
    rendered = {}
    for name, width, height, body in [('common', 256, 512, common), ('main', 512, 512, main_panels)]:
        source = svg(width, height, body)
        (SOURCES / (name + '.svg')).write_text(source)
        rendered[name] = cairosvg.svg2png(bytestring=source.encode())
    common_file = RES / 'XTapBlacksmithUI/atlas.bytes'
    common_file.write_bytes(base64.b64encode(rendered['common']) + b'\n')
    main_file = RES / 'XTapMainUI/main_atlas_runtime.bytes'
    main_file.write_bytes(base64.b64encode(rendered['main']) + b'\n')
    (RES / 'XTapMainUI/main_atlas.png').write_bytes(rendered['main'])
    for p in (common_file, main_file):
        meta(p)
    # Keep the existing missing-Resources fallback byte-identical to the validated PNG.
    cs = ROOT / 'unity/XTapUnity/Assets/Scripts/XTapBlacksmith.cs'
    code, count = re.subn(r'static readonly string EmbeddedBlacksmithAtlas(?:Jpeg|Png)Base64 = "[^"]*";',
        'static readonly string EmbeddedBlacksmithAtlasPngBase64 = "' + base64.b64encode(rendered['common']).decode() + '";', cs.read_text())
    assert count == 1
    cs.write_text(code.replace('EmbeddedBlacksmithAtlasJpegBase64', 'EmbeddedBlacksmithAtlasPngBase64'))
    for name, data in rendered.items():
        print(name, len(data), hashlib.sha256(data).hexdigest())


if __name__ == '__main__':
    main()
