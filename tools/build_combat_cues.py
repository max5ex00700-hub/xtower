#!/usr/bin/env python3
"""Build original vector combat cues and transparent Unity PNGs (CairoSVG/Pillow).

No character artwork is generated or modified. SVG sources remain editable.
The review board is a design preview, not a Unity screenshot.
"""
from pathlib import Path
import hashlib
import io
import cairosvg
import fitz
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
VECTORS = ROOT / 'tools/ui_sources/combat_cues'
DEST = ROOT / 'unity/XTapUnity/Assets/Resources/XTapCombatUI'
REVIEW = ROOT / 'unity/combat-cues-review-11.68'


def meta(path, folder=False):
    target = Path(str(path) + '.meta')
    if target.exists():
        return
    guid = hashlib.sha256(str(path.relative_to(ROOT)).encode()).hexdigest()[:32]
    target.write_text('fileFormatVersion: 2\nguid: ' + guid + '\n' +
        ('folderAsset: yes\nDefaultImporter:\n' if folder else 'TextScriptImporter:\n') +
        '  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n')


def shell(body, accent, hot):
    return f'''<svg xmlns="http://www.w3.org/2000/svg" width="512" height="512" viewBox="0 0 512 512">
<defs>
 <linearGradient id="metal" x1="0" y1="0" x2=".85" y2="1">
  <stop stop-color="#FFF3C1"/><stop offset=".24" stop-color="#DAA54D"/>
  <stop offset=".46" stop-color="#6C421D"/><stop offset=".55" stop-color="#FFE8A0"/>
  <stop offset=".78" stop-color="#A66A24"/><stop offset="1" stop-color="#FFE9AF"/>
 </linearGradient>
 <linearGradient id="edge" x1="0" y1="0" x2="1" y2="1">
  <stop stop-color="#FFFFFF"/><stop offset=".3" stop-color="{accent}"/>
  <stop offset=".7" stop-color="{hot}"/><stop offset="1" stop-color="#FFF1C5"/>
 </linearGradient>
 <linearGradient id="glass" x1="0" y1="0" x2=".85" y2="1">
  <stop stop-color="{accent}"/><stop offset=".48" stop-color="{hot}"/>
  <stop offset="1" stop-color="#061321"/>
 </linearGradient>
 <radialGradient id="aura">
  <stop offset=".45" stop-color="{hot}" stop-opacity=".10"/>
  <stop offset=".72" stop-color="{accent}" stop-opacity=".18"/>
  <stop offset="1" stop-color="{accent}" stop-opacity="0"/>
 </radialGradient>
</defs>
<circle cx="256" cy="256" r="249" fill="url(#aura)"/>
{body}
</svg>'''


def hit_svg(followup=False):
    accent, hot = ('#FFCA58', '#E9481C') if followup else ('#FFE795', '#E9A127')
    parts = ['<circle cx="256" cy="256" r="172" fill="#081017" fill-opacity=".90" stroke="#05080D" stroke-width="18"/>',
             f'<circle cx="256" cy="256" r="171" fill="none" stroke="{accent}" stroke-opacity=".12" stroke-width="24"/>',
             '<circle cx="256" cy="256" r="170" fill="none" stroke="url(#metal)" stroke-width="13"/>',
             '<circle cx="256" cy="256" r="156" fill="none" stroke="#FEDE92" stroke-opacity=".55" stroke-width="3"/>']
    # Four blades give the weak point an aggressive, unmistakable target silhouette.
    for angle in (0, 90, 180, 270):
        parts.append(f'''<g transform="rotate({angle} 256 256)">
 <path d="M256 28 L276 74 L269 98 L288 115 L269 130 L256 166 L243 130 L224 115 L243 98 L236 74 Z" fill="#080E16" stroke="#04070B" stroke-width="9"/>
 <path d="M256 33 L271 76 L264 100 L279 114 L263 126 L256 153 L249 126 L233 114 L248 100 L241 76 Z" fill="url(#metal)"/>
 <path d="M256 49 L260 103 L270 114 L256 140 L244 114 L252 103 Z" fill="url(#glass)" stroke="{accent}" stroke-width="2"/>
 <path d="M252 57 L250 88" fill="none" stroke="#FFF9DC" stroke-width="3"/>
 </g>''')
    # Interrupted outer brackets / rune ticks keep detail clear at thumb size.
    for angle in (45, 135, 225, 315):
        parts.append(f'''<g transform="rotate({angle} 256 256)">
 <path d="M220 57 L237 49 L275 49 L292 57" fill="none" stroke="{accent}" stroke-width="6"/>
 <path d="M237 68 L256 60 L275 68" fill="none" stroke="url(#metal)" stroke-width="4"/>
 <path d="M256 81 L263 92 L256 104 L249 92 Z" fill="{accent}"/>
 </g>''')
    parts.append(f'''<path d="M256 134 L378 256 L256 378 L134 256 Z" fill="#080C13" stroke="#A97027" stroke-width="7"/>
 <path d="M256 145 L362 256 L256 367 L150 256 Z" fill="url(#glass)" stroke="url(#edge)" stroke-width="5"/>
 <path d="M256 147 L240 218 L155 255 Z" fill="#FFF3BF" fill-opacity=".58"/>
 <path d="M256 147 L361 255 L283 225 Z" fill="{hot}"/>
 <path d="M155 257 L235 286 L256 365 Z" fill="#5E240D" fill-opacity=".64"/>
 <path d="M361 257 L283 279 L256 365 Z" fill="{accent}" fill-opacity=".48"/>
 <path d="M273 162 L258 191 L274 214 M184 223 L207 236 L197 256 M319 277 L294 290 L302 312 M258 302 L243 327 L250 347" fill="none" stroke="#FFF0B2" stroke-width="5" stroke-linejoin="miter"/>
 <path d="M148 221 L175 204 L337 204 L364 221 L364 291 L337 308 L175 308 L148 291 Z" fill="#081018" fill-opacity=".95" stroke="url(#metal)" stroke-width="4"/>
 <path d="M180 216 L230 216 M282 296 L332 296" fill="none" stroke="{accent}" stroke-width="3"/>
 <path d="M117 246 L131 256 L117 266 M395 246 L381 256 L395 266" fill="none" stroke="#FFEBAF" stroke-width="5"/>
 ''')
    if followup:
        parts.append('<path d="M84 206 L65 256 L84 306 M428 206 L447 256 L428 306" fill="none" stroke="#FF752A" stroke-width="6"/>')
    return shell(''.join(parts), accent, hot)


def shield_svg():
    shape = 'M256 75 L402 137 L389 282 Q369 366 256 448 Q143 366 123 282 L110 137 Z'
    return shell(f'''<path d="{shape}" fill="none" stroke="#50D7FF" stroke-opacity=".10" stroke-width="36"/>
 <path d="{shape}" fill="#050C19" stroke="#04070D" stroke-width="18"/>
 <path d="{shape}" fill="url(#metal)" stroke="#FFF0BE" stroke-width="3"/>
 <path d="M256 96 L382 151 L369 278 Q351 349 256 421 Q161 349 143 278 L130 151 Z" fill="#132A3C" stroke="#6F502C" stroke-width="5"/>
 <path d="M256 108 L370 160 L356 275 Q341 337 256 404 Q171 337 156 275 L142 160 Z" fill="url(#glass)" stroke="#BDF6FF" stroke-width="4"/>
 <path d="M256 110 L251 239 L145 163 Z" fill="#C8F6FF" fill-opacity=".68"/>
 <path d="M256 110 L368 161 L267 239 Z" fill="#0876D2"/>
 <path d="M146 166 L247 253 L170 302 L157 274 Z" fill="#239FEB" fill-opacity=".7"/>
 <path d="M369 167 L265 253 L340 304 L355 274 Z" fill="#08477F"/>
 <path d="M169 306 L247 260 L256 401 Q203 359 169 306 Z" fill="#08244B"/>
 <path d="M266 262 L340 307 Q305 362 256 401 Z" fill="#259EEB" fill-opacity=".65"/>
 <path d="M256 127 L256 393 M160 176 L248 250 L185 318 M352 177 L265 249 L326 320" fill="none" stroke="#95E7FF" stroke-opacity=".36" stroke-width="3"/>
 <path d="M255 33 L277 73 L309 62 L296 100 L256 119 L216 100 L203 62 L235 73 Z" fill="#061425" stroke="url(#metal)" stroke-width="8"/>
 <path d="M256 48 L266 81 L256 102 L246 81 Z" fill="#C9F6FF" stroke="#36BDF7" stroke-width="3"/>
 <path d="M104 144 L72 161 L108 209 L118 244 L130 194 Z M408 144 L440 161 L404 209 L394 244 L382 194 Z" fill="url(#metal)" stroke="#151016" stroke-width="4"/>
 <path d="M88 166 L108 184 L108 164 Z M424 166 L404 184 L404 164 Z" fill="#EAFBFF"/>
 <path d="M256 173 L283 216 L271 239 L297 252 L271 266 L276 303 L256 335 L236 303 L241 266 L215 252 L241 239 L229 216 Z" fill="#03182E" stroke="#051526" stroke-width="12"/>
 <path d="M256 177 L277 216 L264 242 L287 252 L264 261 L270 301 L256 325 L242 301 L248 261 L225 252 L248 242 L235 216 Z" fill="#CBF6FF" stroke="#76E0FF" stroke-width="3"/>
 <path d="M256 194 L256 315 L250 301 L255 254 L242 252 L255 247 L247 218 Z" fill="#FFFFFF"/>
 <path d="M231 430 L256 470 L281 430 L256 440 Z" fill="url(#metal)" stroke="#091021" stroke-width="4"/>
 <path d="M154 182 L165 249 M344 288 Q324 330 290 354" fill="none" stroke="#F3FDFF" stroke-width="5" stroke-linecap="round"/>
 ''', '#A4F1FF', '#087FE1')


def build():
    VECTORS.mkdir(parents=True, exist_ok=True)
    DEST.mkdir(parents=True, exist_ok=True)
    REVIEW.mkdir(parents=True, exist_ok=True)
    meta(DEST, True)
    for name, svg in [('hit_gold', hit_svg()), ('hit_followup', hit_svg(True)), ('shield_crystal', shield_svg())]:
        svg = '\n'.join(line.rstrip() for line in svg.splitlines()) + '\n'
        (VECTORS / (name + '.svg')).write_text(svg)
        png = cairosvg.svg2png(bytestring=svg.encode(), output_width=512, output_height=512)
        path = DEST / (name + '.bytes')
        path.write_bytes(png)
        meta(path)
        with Image.open(path) as image:
            assert image.size == (512, 512) and image.mode == 'RGBA'
            image.load()
        print(name, len(png), hashlib.sha256(png).hexdigest())


def preview():
    board = Image.new('RGBA', (1200, 950), '#080C14')
    draw = ImageDraw.Draw(board)
    font_data = fitz.Font('korea').buffer
    def label(text, xy, size, color='#F7EBCF'):
        draw.text(xy, text, font=ImageFont.truetype(io.BytesIO(font_data), size), fill=color, anchor='mm')
    label('X탑 · 전투 표적 디자인', (600, 58), 40)
    label('금빛 약점  /  붉은 금빛 추가타  /  푸른 수정 실드', (600, 112), 24, '#AEBBCC')
    for x, name, title, word in [(210,'hit_gold','약점 HIT','HIT'),(600,'hit_followup','추가 HIT','HIT!!'),(990,'shield_crystal','수정 실드','')]:
        icon = Image.open(DEST / (name + '.bytes')).convert('RGBA')
        icon = icon.resize((340, 340), Image.Resampling.LANCZOS)
        board.alpha_composite(icon, (x-170, 170))
        if word:
            draw.text((x,340),word,font=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf',round(340*(.18 if word=='HIT' else .145))),fill='#FFF6DB',anchor='mm',stroke_width=1,stroke_fill='#060B13')
        label(title, (x, 558), 29)
        label('휴대폰 표시 크기 예시', (x, 630), 20, '#AEBBCC')
        for xx, back in [(x-78,'#CFAD88'),(x+78,'#0C2337')]:
            draw.rounded_rectangle((xx-70,676,xx+70,832),radius=16,fill=back)
            small=Image.open(DEST / (name+'.bytes')).convert('RGBA').resize((116,116),Image.Resampling.LANCZOS)
            board.alpha_composite(small,(xx-58,696))
            if word:
                draw.text((xx,754),word,font=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf',round(116*(.18 if word=='HIT' else .145))),fill='#FFF6DB',anchor='mm',stroke_width=0)
    label('설계 미리보기 · Unity 실행 화면 아님', (600, 905), 23, '#8D9BAF')
    board.convert('RGB').save(REVIEW/'combat-cues-design.jpg',quality=94)


if __name__ == '__main__':
    build()
    preview()
