#!/usr/bin/env python3
"""Static design review, NOT a Unity render or an Android screenshot.

Reproduce the 11.63 layout geometry and assets at four portrait aspect ratios.
Requires Pillow, numpy and PyMuPDF. Writes previews outside Assets.
"""
import argparse
import io
import math
from pathlib import Path
import re
import subprocess

import fitz
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageOps

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / 'unity/XTapUnity/Assets'
CODE = (ASSETS / 'Scripts/XTapGachaMachine.cs').read_text()
VALUES = list(map(int, re.search(r'corrections = \{([^}]+)', CODE)[1].split(',')))
FONT_BYTES = fitz.Font('korea').buffer


def font(size, latin=False):
    if latin:
        return ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf', size)
    return ImageFont.truetype(io.BytesIO(FONT_BYTES), size)


def label(image, value, xy, size, color='#FFF7E6', latin=False, anchor='mm'):
    ImageDraw.Draw(image).text(xy, value, font=font(size, latin), fill=color,
                               anchor=anchor, stroke_width=1, stroke_fill='#080A12')


def wheel_art(angle=8 * 360 / 11, layers=False):
    y, x = np.mgrid[-500:500, -500:500]
    radius = np.hypot(x, y)
    sector = (np.floor((np.arctan2(x, -y) % (2 * np.pi)) / (2 * np.pi / 11) + .5) % 11).astype(int)
    rgba = np.zeros((1000, 1000, 4), dtype=np.uint8)
    rgba[radius <= 110] = [3, 6, 13, 255]
    for i in range(11):
        inner, outer = ((.012, .035, .085), (.045, .23, .40))
        if 4 <= i <= 8:
            inner, outer = ((.075, .036, .012), (.40, .22, .065))
        elif i == 3:
            inner, outer = ((.065, .022, .10), (.30, .13, .43))
        elif i == 9:
            inner, outer = ((.075, .014, .025), (.36, .075, .10))
        mask = (sector == i) & (radius >= 110) & (radius <= 440)
        t = np.sin(np.clip((radius[mask] - 110) / 330, 0, 1) * np.pi * .82)
        rgba[mask, :3] = ((np.array(inner)[None, :] * (1 - t[:, None]) + np.array(outer)[None, :] * t[:, None]) * 255).astype(np.uint8)
        rgba[mask, 3] = 255
    image = Image.fromarray(rgba)
    draw = ImageDraw.Draw(image)
    for i in range(11):
        divider_angle = math.radians((i + .5) * 360 / 11)
        draw.line([(500 + math.sin(divider_angle) * r, 500 - math.cos(divider_angle) * r) for r in (110, 440)], fill='#B88038', width=3)
    for radius in (110, 440):
        draw.ellipse((500-radius, 500-radius, 500+radius, 500+radius), outline='#B88038', width=3)
    ornament = Image.open(ASSETS / 'Resources/XTapGachaUI/block_gear_wheel.bytes').convert('RGBA')
    assert .25 < np.mean(np.asarray(ornament)[:, :, 3] < 16) < .9
    for i, value in enumerate(VALUES):
        label_angle = i * 360 / 11
        text = '0%' if value == 0 else f'{value:+d}'
        tile = Image.new('RGBA', (220, 120))
        label(tile, text, (110, 60), 54, latin=True)
        tile = tile.rotate(-label_angle, Image.Resampling.BICUBIC, expand=True)
        px = 500 + math.sin(math.radians(label_angle)) * 324
        py = 500 - math.cos(math.radians(label_angle)) * 324
        image.alpha_composite(tile, (round(px-tile.width/2), round(py-tile.height/2)))
        # Unity rotates the wheel counter-clockwise by this amount to select i.
        assert abs(((-label_angle + i * 360 / 11 + 180) % 360) - 180) < 1e-8
    # Only the precise face/values rotate. Painted casing and hub stay still.
    ornament = ornament.resize((1000, 1000), Image.Resampling.LANCZOS)
    if layers:
        return image, ornament
    image = image.rotate(angle, Image.Resampling.BICUBIC)
    image.alpha_composite(ornament)
    return image


def spin_review(output):
    """Design animation only: constant center/radius and stationary casing."""
    face, casing = wheel_art(layers=True)
    face = face.resize((540, 540), Image.Resampling.LANCZOS)
    casing = casing.resize((540, 540), Image.Resampling.LANCZOS)
    process = subprocess.Popen([
        'ffmpeg', '-hide_banner', '-loglevel', 'error', '-y',
        '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-s', '540x540', '-r', '30',
        '-i', '-', '-an', '-c:v', 'libx264', '-crf', '21', '-pix_fmt', 'yuv420p',
        '-movflags', '+faststart', str(output),
    ], stdin=subprocess.PIPE)
    # Full 360-degree review; not a reproduction of Unity's spin timing.
    for frame in range(90):
        image = Image.new('RGBA', (540, 540), '#080A10')
        image.alpha_composite(face.rotate(-frame * 4, Image.Resampling.BICUBIC))
        image.alpha_composite(casing)
        process.stdin.write(image.convert('RGB').tobytes())
    process.stdin.close()
    assert process.wait() == 0


def layout(height, top_inset, bottom_inset):
    safe_top = max(24, top_inset + 16)
    safe_bottom = max(24, bottom_inset + 16)
    side, width = 42, 996
    footer = height - safe_bottom - 88
    bg = Image.open(ASSETS / 'Resources/XTapGachaUI/block_gear_machine.bytes')
    bg_height = max(height, 1080 * bg.height / bg.width)
    wheel_top = max(bg_height * .30 - (bg_height-height)*.5, safe_top+312)
    diameter = max(120, min(980, width, footer-16-440-24-wheel_top))
    card = wheel_top + diameter + 24
    card_height = footer - 16 - card
    assert wheel_top - 64 >= safe_top + 228
    assert card_height >= 440 and side <= (1080 - diameter) / 2
    assert card + card_height < footer and footer + 88 <= height - bottom_inset
    # The stat caption and value have separate, adequately tall rectangles.
    assert card_height * .4 * .36 >= 32 * 1.3
    assert card_height * .4 * .64 >= 66 * 1.3
    for columns, rows in ((1, 1), (6, 1), (1, 6), (4, 3), (3, 4)):
        cell = min(72, (width * .265 - 16) / columns, (card_height * .84 - 16) / rows)
        assert cell * columns < width * .265 and cell * rows < card_height * .84
    return safe_top, side, width, footer, card, card_height, wheel_top, diameter


def render(height, top, bottom, wheel, output):
    safe_top, side, width, footer, card, card_height, wheel_top, diameter = layout(height, top, bottom)
    bg = Image.open(ASSETS / 'Resources/XTapGachaUI/block_gear_machine.bytes').convert('RGB')
    image = ImageOps.fit(bg, (1080, height), Image.Resampling.LANCZOS).convert('RGBA')
    shade = np.zeros((height, 1080, 4), dtype=np.uint8)
    shade[:, :, 3] = (np.interp(1 - np.arange(height) / height, [0, .25, .6, .82, 1], [.82, .55, .06, 0, .32]) * 255).astype(np.uint8)[:, None]
    image.alpha_composite(Image.fromarray(shade))
    label(image, 'BLOCK GEAR', (540, safe_top + 80), 112, '#FFE0A3', latin=True)
    label(image, '보정  +50%', (540, wheel_top - 36), 42, '#FFE099')
    diameter = round(diameter)
    image.alpha_composite(wheel.resize((diameter, diameter), Image.Resampling.LANCZOS), ((1080-diameter)//2, round(wheel_top)))
    # Stationary pointer, same 70x96 local design coordinates as the mesh.
    draw = ImageDraw.Draw(image)
    scale = diameter / 1000
    cy = wheel_top + diameter/2 - 444 * scale
    def points(coords):
        return [(540+x*scale, cy-y*scale) for x, y in coords]
    draw.polygon(points([(0, 43), (19, 16), (0, -11), (-19, 16)]), fill='#F0C268')
    draw.polygon(points([(0, 35), (13, 16), (0, -3), (-13, 16)]), fill='#144D8A')
    draw.polygon(points([(0, -45), (-13, -3), (13, -3)]), fill='#F0C268')
    draw.rectangle((side, card, side+width, card+card_height), fill='#050609', outline='#B8853D', width=2)
    divider = side + width * .305
    draw.line((divider, card+card_height*.12, divider, card+card_height*.88), fill='#584023', width=2)
    cell = min(72, (width*.265 - 16)/4)
    for y in range(3):
        for x in range(4):
            xx = side + width*.1575 + (x-2)*cell + 2
            yy = card + card_height/2 + (y-1.5)*cell + 2
            draw.rectangle((xx, yy, xx+cell-3, yy+cell-3), fill='#F0A624')
            draw.rectangle((xx+7, yy+7, xx+cell-10, yy+cell-10), fill='#1A1B1F')
    left = side + width * .33
    label(image, '획득 보상', (left, card+card_height*.075), 32, '#E0BD7D', anchor='lm')
    label(image, '활  ·  12칸', (left, card+card_height*.235), 60, anchor='lm')
    for offset, title, value, color in [(0, '공격', '91', '#FFBF70'), (.34, '방어', '89', '#76CFFF'), (.68, '체력', '134', '#FF8585')]:
        x = left + width*.64*offset
        label(image, title, (x, card+card_height*.422), 32, anchor='lm')
        label(image, value, (x, card+card_height*.622), 66, color, anchor='lm')
    draw.rectangle((side, footer, side+width, footer+88), fill='#03050A', outline='#584023', width=2)
    label(image, '터치하여 보상을 바닥에 놓기', (540, footer+44), 38, '#FFEBC2')
    image.convert('RGB').resize((540, height//2), Image.Resampling.LANCZOS).save(output, quality=93)
    print(f'{output.name}: 1080x{height}; safe insets {top}/{bottom}; wheel top {wheel_top:.1f}px, diameter {diameter}px; card {card_height:.1f}px; geometry passed')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--spin-video', action='store_true')
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    assert VALUES == [-30, -20, -10, 0, 10, 20, 30, 40, 50, -50, -40]
    wheel = wheel_art()
    for height, top, bottom in [(1920, 0, 0), (2340, 90, 60), (2400, 100, 70), (2520, 110, 80)]:
        render(height, top, bottom, wheel, args.output / f'block-gear-{height}-design.jpg')
    if args.spin_video:
        spin_review(args.output / 'block-gear-spin-design.mp4')
