/* ============================================================
   draw-core.js — 팔레트 · 얼굴 · 눈/눈썹/코/입 · 볼FX
   헤드 스페이스: 정수리 y=212, 턱 y≈588, 얼굴 중심 x=512
   ============================================================ */
(function () {
  const AE = (window.AE = window.AE || {});
  const CX = 512;

  const G = (AE.G = { ctx: null, ch: null, pal: null, seed: 1 });
  AE.setScene = function (ctx, ch) {
    G.ctx = ctx;
    G.ch = ch;
    G.pal = buildPalette(ch);
    G.rng = AE.subRng(ch.seed, 'scene');
    G.seed = ch.seed;
    return G.pal;
  };
  const ctx = () => G.ctx;

  /* ---------------- 팔레트 ---------------- */
  function buildPalette(ch) {
    const p = ch.parts;
    const skin = AE.byId('skinTone', p.skinTone).color;
    const hair = p.hairColor;
    const hairL = AE.lum(hair);
    const iris = p.iris;
    const outfit = p.outfitColor;
    return {
      skin,
      skinMid: AE.shade(skin, -4, 2, -2),
      skinShade: AE.shade(skin, -11, 2, -3),
      skinDeep: AE.shade(skin, -21, -1, -5),
      skinHi: AE.shade(skin, 5, -6, 1),
      blush: AE.shade(AE.mix(skin, '#ff7089', 0.62), -2, 8),
      lip: AE.shade(AE.mix(skin, '#c34a58', 0.68), -8, 4),
      hair,
      hairShade: AE.shade(hair, hairL > 55 ? -18 : -9, 4, -4),
      hairDeep: AE.shade(hair, hairL > 55 ? -33 : -19, 2, -6),
      hairHi: AE.shade(hair, hairL > 72 ? 12 : 22, -6, 2),
      hairInner: p.hairInner,
      hairLine: AE.shade(hair, hairL > 60 ? -25 : -11, 0, 0),
      iris,
      irisB: p.irisB,
      irisDark: AE.shade(iris, -26, 6),
      irisLight: AE.shade(iris, 24, -10),
      irisBDark: AE.shade(p.irisB, -26, 6),
      irisBLight: AE.shade(p.irisB, 24, -10),
      outfit,
      outfitShade: AE.shade(outfit, AE.lum(outfit) > 60 ? -13 : -8, 4),
      outfitDeep: AE.shade(outfit, AE.lum(outfit) > 60 ? -26 : -16, 2),
      outfitHi: AE.shade(outfit, AE.lum(outfit) > 60 ? 7 : 13, -5),
      accent: p.outfitAccent,
      accentDeep: AE.shade(p.outfitAccent, -20, 4),
      white: '#ffffff',
      lash: AE.shade(AE.mix(hair, '#0d0810', 0.62), -28, -8),
      line: AE.shade(AE.mix(hair, '#140d16', 0.6), -26, -20),
    };
  }
  AE.buildPalette = buildPalette;

  /* ---------------- 헬퍼 ---------------- */
  AE.fill = function (path, style, alpha) {
    const c = ctx();
    c.save();
    if (alpha != null) c.globalAlpha = alpha;
    c.fillStyle = style;
    c.fill(path);
    c.restore();
  };
  AE.stroke = function (path, style, lw, alpha, cap) {
    const c = ctx();
    c.save();
    if (alpha != null) c.globalAlpha = alpha;
    c.strokeStyle = style;
    c.lineWidth = lw;
    c.lineCap = cap || 'round';
    c.lineJoin = 'round';
    c.stroke(path);
    c.restore();
  };
  AE.innerShadow = function (path, color, dx, dy, alpha) {
    const c = ctx();
    const region = new Path2D();
    region.rect(-9000, -9000, 20000, 20000);
    try {
      region.addPath(path, new DOMMatrix([1, 0, 0, 1, -dx, -dy]));
    } catch (e) {
      return;
    }
    c.save();
    c.clip(path);
    c.globalAlpha = alpha == null ? 0.7 : alpha;
    c.fillStyle = color;
    c.fill(region, 'evenodd');
    c.restore();
  };
  AE.softBlob = function (x, y, rx, ry, color, alpha, rot) {
    const c = ctx();
    c.save();
    c.translate(x, y);
    if (rot) c.rotate(rot);
    c.scale(rx, ry);
    const g = c.createRadialGradient(0, 0, 0, 0, 0, 1);
    g.addColorStop(0, AE.alpha(color, alpha));
    g.addColorStop(0.55, AE.alpha(color, alpha * 0.5));
    g.addColorStop(1, AE.alpha(color, 0));
    c.fillStyle = g;
    c.beginPath();
    c.arc(0, 0, 1, 0, Math.PI * 2);
    c.fill();
    c.restore();
  };
  AE.linear = function (x0, y0, x1, y1, stops) {
    const g = ctx().createLinearGradient(x0, y0, x1, y1);
    for (const s of stops) g.addColorStop(s[0], s[1]);
    return g;
  };

  /* ---------------- 얼굴형 ---------------- */
  const HS = {
    oval: { hw: 150, chinY: 588, jaw: 0.70, chinW: 0.30, topY: 212, eyeY: 428, eyeDx: 0.560 },
    vline: { hw: 144, chinY: 602, jaw: 0.58, chinW: 0.21, topY: 210, eyeY: 426, eyeDx: 0.566 },
    baby: { hw: 158, chinY: 572, jaw: 0.80, chinW: 0.36, topY: 218, eyeY: 430, eyeDx: 0.552 },
    cool: { hw: 146, chinY: 598, jaw: 0.62, chinW: 0.24, topY: 208, eyeY: 424, eyeDx: 0.564 },
  };
  AE.headShape = function () {
    return HS[G.ch.parts.faceShape] || HS.oval;
  };

  AE.facePath = function () {
    const f = AE.headShape();
    const y0 = f.topY, chinY = f.chinY, hw = f.hw, Hh = chinY - y0;
    const Y = (t) => y0 + Hh * t;
    const p = new Path2D();
    p.moveTo(CX, y0 + 4);
    p.bezierCurveTo(CX + hw * 0.50, y0 - 2, CX + hw * 0.90, Y(0.06), CX + hw * 0.975, Y(0.17));
    p.bezierCurveTo(CX + hw * 1.005, Y(0.30), CX + hw * 0.998, Y(0.45), CX + hw * 0.955, Y(0.585));
    p.bezierCurveTo(CX + hw * 0.915, Y(0.70), CX + hw * 0.855, Y(0.80), CX + hw * f.jaw, Y(0.885));
    p.bezierCurveTo(CX + hw * f.jaw * 0.72, Y(0.955), CX + hw * f.chinW, Y(0.995), CX, chinY);
    p.bezierCurveTo(CX - hw * f.chinW, Y(0.995), CX - hw * f.jaw * 0.72, Y(0.955), CX - hw * f.jaw, Y(0.885));
    p.bezierCurveTo(CX - hw * 0.855, Y(0.80), CX - hw * 0.915, Y(0.70), CX - hw * 0.955, Y(0.585));
    p.bezierCurveTo(CX - hw * 0.998, Y(0.45), CX - hw * 1.005, Y(0.30), CX - hw * 0.975, Y(0.17));
    p.bezierCurveTo(CX - hw * 0.90, Y(0.06), CX - hw * 0.50, y0 - 2, CX, y0 + 4);
    p.closePath();
    return p;
  };

  /* ---------------- 눈 ---------------- */
  const EYES = {
    round: { w: 94, h: 78, innerY: 0.18, outerY: 0.02, apexX: -0.16, apexY: 0.60, up1: 0.76, up2: 0.92, low1: 0.34, low2: 0.50, lash: 1.0 },
    wide: { w: 98, h: 84, innerY: 0.19, outerY: 0.00, apexX: -0.10, apexY: 0.62, up1: 0.78, up2: 0.96, low1: 0.38, low2: 0.54, lash: 1.0 },
    sharp: { w: 100, h: 62, innerY: 0.24, outerY: -0.14, apexX: -0.22, apexY: 0.50, up1: 0.60, up2: 0.82, low1: 0.28, low2: 0.40, lash: 1.12 },
    droopy: { w: 94, h: 72, innerY: 0.10, outerY: 0.14, apexX: -0.26, apexY: 0.52, up1: 0.66, up2: 0.70, low1: 0.32, low2: 0.46, lash: 1.0 },
    cat: { w: 96, h: 66, innerY: 0.22, outerY: -0.20, apexX: -0.30, apexY: 0.54, up1: 0.64, up2: 0.96, low1: 0.26, low2: 0.36, lash: 1.15 },
    sleepy: { w: 94, h: 50, innerY: 0.24, outerY: -0.06, apexX: -0.14, apexY: 0.34, up1: 0.38, up2: 0.50, low1: 0.26, low2: 0.38, lash: 1.05 },
  };

  function eyePath(o, hs) {
    const hw = o.w / 2, hh = (o.h * hs) / 2;
    const p = new Path2D();
    p.moveTo(-hw, hh * o.innerY);
    p.bezierCurveTo(-hw * 0.58, -hh * o.up1, hw * 0.40, -hh * o.up2, hw, -hh * o.outerY);
    p.bezierCurveTo(hw * 0.72, hh * o.low1, hw * 0.12, hh * o.low2, -hw * 0.52, hh * (o.low2 - 0.06));
    p.bezierCurveTo(-hw * 0.78, hh * (o.low2 - 0.14), -hw * 0.93, hh * (o.innerY - 0.02), -hw, hh * o.innerY);
    p.closePath();
    return p;
  }

  function star(cx, cy, r, n, color, inner) {
    const c = ctx();
    c.beginPath();
    for (let i = 0; i < n * 2; i++) {
      const a = (i / (n * 2)) * Math.PI * 2 - Math.PI / 2;
      const rr = i % 2 ? r * (inner || 0.42) : r;
      i ? c.lineTo(cx + Math.cos(a) * rr, cy + Math.sin(a) * rr) : c.moveTo(cx + Math.cos(a) * rr, cy + Math.sin(a) * rr);
    }
    c.closePath();
    c.fillStyle = color;
    c.fill();
  }
  function heart(cx, cy, r, color) {
    const c = ctx();
    c.beginPath();
    c.moveTo(cx, cy + r * 0.8);
    c.bezierCurveTo(cx - r * 1.7, cy - r * 0.35, cx - r * 0.45, cy - r * 1.3, cx, cy - r * 0.35);
    c.bezierCurveTo(cx + r * 0.45, cy - r * 1.3, cx + r * 1.7, cy - r * 0.35, cx, cy + r * 0.8);
    c.closePath();
    c.fillStyle = color;
    c.fill();
  }

  AE.drawClosedEye = function (cx, cy, s) {
    const c = ctx(), P = G.pal;
    const o = EYES[G.ch.parts.eyeShape] || EYES.round;
    const w = o.w;
    c.save();
    c.translate(cx, cy);
    c.scale(s, 1);
    const p = new Path2D();
    p.moveTo(-w * 0.52, 10);
    p.quadraticCurveTo(0, -24, w * 0.52, 5);
    c.save();
    c.strokeStyle = P.lash;
    c.lineWidth = 9 * o.lash;
    c.lineCap = 'round';
    c.stroke(p);
    c.restore();
    for (let i = 0; i < 2; i++) {
      c.beginPath();
      c.moveTo(w * (0.34 + i * 0.12), -8 - i * 2);
      c.lineTo(w * (0.44 + i * 0.12), -24 - i * 4);
      c.strokeStyle = P.lash;
      c.lineWidth = 4;
      c.lineCap = 'round';
      c.stroke();
    }
    c.beginPath();
    c.moveTo(-w * 0.38, 15);
    c.quadraticCurveTo(0, 24, w * 0.32, 16);
    c.strokeStyle = AE.alpha(P.lash, 0.35);
    c.lineWidth = 2.8;
    c.stroke();
    c.restore();
  };

  AE.drawEye = function (cx, cy, s, irisColor, irisLight, irisDark, idx, openness, xscale) {
    const c = ctx(), P = G.pal, p = G.ch.parts;
    const o = EYES[p.eyeShape] || EYES.round;
    const hs = openness == null ? 1 : openness;
    const w = o.w, h = o.h * hs;
    const rng = AE.subRng(G.ch.seed, 'eye' + idx);

    c.save();
    c.translate(cx, cy);
    c.scale(s * (xscale || 1), 1);

    const lid = eyePath(o, hs);
    const iy = h * 0.02;
    const irx = w * 0.355, iry = h * 0.52;
    const icx = w * 0.02;

    AE.fill(lid, '#fffdfb');
    c.save();
    c.clip(lid);
    const sg = c.createLinearGradient(0, -h * 0.55, 0, h * 0.06);
    sg.addColorStop(0, 'rgba(96,96,122,0.5)');
    sg.addColorStop(0.55, 'rgba(120,120,150,0.12)');
    sg.addColorStop(1, 'rgba(150,150,175,0)');
    c.fillStyle = sg;
    c.fillRect(-w * 0.7, -h * 0.8, w * 1.4, h);
    c.restore();

    const irisG = c.createLinearGradient(0, iy - iry, 0, iy + iry);
    irisG.addColorStop(0, irisDark);
    irisG.addColorStop(0.45, irisColor);
    irisG.addColorStop(1, irisLight);
    c.save();
    c.clip(lid);
    c.beginPath();
    c.ellipse(icx, iy, irx, iry, 0, 0, Math.PI * 2);
    c.fillStyle = irisG;
    c.fill();

    c.save();
    c.clip();
    const n = 12;
    for (let i = 0; i < n; i++) {
      const a = (i / n) * Math.PI * 2 + rng() * 0.2;
      c.beginPath();
      c.moveTo(icx + Math.cos(a) * irx * 0.34, iy + Math.sin(a) * iry * 0.34);
      c.lineTo(icx + Math.cos(a) * irx * 1.04, iy + Math.sin(a) * iry * 1.04);
      c.strokeStyle = i % 2 ? AE.alpha(irisDark, 0.26) : AE.alpha('#ffffff', 0.13);
      c.lineWidth = 1.8;
      c.stroke();
    }
    c.restore();

    const bg2 = c.createRadialGradient(icx, iy + iry * 0.5, 1, icx, iy + iry * 0.5, irx * 1.5);
    bg2.addColorStop(0, AE.alpha(irisLight, 0.9));
    bg2.addColorStop(1, AE.alpha(irisLight, 0));
    c.fillStyle = bg2;
    c.beginPath();
    c.ellipse(icx, iy + iry * 0.46, irx, iry * 0.8, 0, 0, Math.PI * 2);
    c.fill();

    const tg = c.createLinearGradient(0, iy - iry, 0, iy - iry * 0.12);
    tg.addColorStop(0, AE.alpha(irisDark, 0.9));
    tg.addColorStop(1, AE.alpha(irisDark, 0));
    c.fillStyle = tg;
    c.fillRect(icx - irx, iy - iry, irx * 2, iry);

    c.beginPath();
    c.ellipse(icx, iy, irx, iry, 0, 0, Math.PI * 2);
    c.strokeStyle = AE.alpha(irisDark, 0.92);
    c.lineWidth = 2.8;
    c.stroke();

    const fx = p.irisFx;
    c.save();
    c.beginPath();
    c.ellipse(icx, iy, irx * 0.97, iry * 0.97, 0, 0, Math.PI * 2);
    c.clip();
    if (fx === 'star') {
      star(icx, iy - iry * 0.04, irx * 0.62, 5, AE.alpha('#ffffff', 0.5), 2);
      star(icx, iy - iry * 0.04, irx * 0.26, 5, AE.alpha('#ffffff', 0.68), 2);
    } else if (fx === 'heart') {
      heart(icx, iy - iry * 0.06, irx * 0.55, AE.alpha('#ffffff', 0.4));
    } else if (fx === 'ring') {
      c.beginPath();
      c.ellipse(icx, iy, irx * 0.68, iry * 0.72, 0, 0, Math.PI * 2);
      c.strokeStyle = AE.alpha('#ffffff', 0.3);
      c.lineWidth = 2.2;
      c.stroke();
      c.beginPath();
      c.ellipse(icx, iy, irx * 0.40, iry * 0.46, 0, 0, Math.PI * 2);
      c.strokeStyle = AE.alpha(irisLight, 0.5);
      c.lineWidth = 1.6;
      c.stroke();
    } else if (fx === 'gradient') {
      const gg = c.createLinearGradient(0, iy - iry, 0, iy + iry);
      gg.addColorStop(0, AE.alpha(irisDark, 0.5));
      gg.addColorStop(0.5, AE.alpha(irisLight, 0.1));
      gg.addColorStop(1, AE.alpha(irisLight, 0.55));
      c.fillStyle = gg;
      c.fillRect(icx - irx, iy - iry, irx * 2, iry * 2);
    } else if (fx === 'sparkle') {
      for (let i = 0; i < 5; i++) {
        const a = (i / 5) * Math.PI * 2 + 0.4;
        star(icx + Math.cos(a) * irx * 0.52, iy + Math.sin(a) * iry * 0.6, irx * 0.15, 4, AE.alpha('#ffffff', 0.45), 2);
      }
    }
    c.restore();

    c.beginPath();
    c.ellipse(icx, iy + h * 0.005, irx * 0.44, iry * 0.50, 0, 0, Math.PI * 2);
    const pg = c.createRadialGradient(icx, iy - iry * 0.2, 1, icx, iy, irx * 0.6);
    pg.addColorStop(0, '#2a1f2e');
    pg.addColorStop(1, '#0c080e');
    c.fillStyle = pg;
    c.fill();

    c.save();
    c.translate(icx - irx * 0.34, iy - iry * 0.40);
    c.rotate(-0.35);
    c.beginPath();
    c.ellipse(0, 0, irx * 0.40, iry * 0.28, 0, 0, Math.PI * 2);
    c.fillStyle = 'rgba(255,255,255,0.96)';
    c.fill();
    c.restore();
    c.beginPath();
    c.ellipse(icx + irx * 0.42, iy + iry * 0.40, irx * 0.16, iry * 0.14, 0, 0, Math.PI * 2);
    c.fillStyle = 'rgba(255,255,255,0.8)';
    c.fill();
    c.beginPath();
    c.ellipse(icx - irx * 0.50, iy + iry * 0.48, irx * 0.09, iry * 0.11, 0, 0, Math.PI * 2);
    c.fillStyle = 'rgba(255,255,255,0.5)';
    c.fill();

    c.restore(); // lid clip

    // 아래 눈꺼풀
    c.save();
    c.beginPath();
    c.moveTo(-w * 0.44, h * (o.low2 - 0.10));
    c.bezierCurveTo(-w * 0.10, h * (o.low2 + 0.02), w * 0.40, h * (o.low1 + 0.02), w * 0.94, -h * o.outerY + h * 0.08);
    c.strokeStyle = AE.alpha(P.lash, 0.26);
    c.lineWidth = 2.6;
    c.stroke();
    c.beginPath();
    c.moveTo(-w * 0.34, h * (o.low2 - 0.02));
    c.bezierCurveTo(0, h * (o.low2 + 0.08), w * 0.36, h * (o.low1 + 0.08), w * 0.78, -h * o.outerY + h * 0.18);
    c.strokeStyle = 'rgba(255,255,255,0.7)';
    c.lineWidth = 1.8;
    c.stroke();
    c.restore();

    /* 위 래시 */
    const lashW = 6.6 * o.lash * Math.max(0.55, hs);
    const lp = new Path2D();
    lp.moveTo(-w * 0.5, h * o.innerY);
    lp.bezierCurveTo(-w * 0.30, -h * o.up1, w * 0.22, -h * o.up2, w * 0.545, -h * o.outerY - h * 0.02);
    lp.lineTo(w * 0.60, -h * o.outerY - h * 0.09);
    lp.bezierCurveTo(w * 0.30, -h * o.up2 + lashW, -w * 0.22, -h * o.up1 + lashW * 1.05, -w * 0.44, h * o.innerY + lashW * 0.40);
    lp.closePath();
    c.save();
    c.translate(0, -1.5);
    c.fillStyle = P.lash;
    c.fill(lp);
    c.restore();

    // 바깥 래시 2가닥
    for (let i = 0; i < 2; i++) {
      const bx = w * (0.40 + i * 0.13), by = -h * (o.up2 - i * 0.12) - 2;
      const sp = new Path2D();
      sp.moveTo(bx - 4, by + 3);
      sp.lineTo(bx + 12 + i * 4, by - 20 - i * 6);
      sp.lineTo(bx + 3, by + 7);
      sp.closePath();
      c.fillStyle = P.lash;
      c.fill(sp);
    }
    c.save();
    c.beginPath();
    c.moveTo(w * 0.50, -h * o.outerY - h * 0.02);
    c.quadraticCurveTo(w * 0.58, -h * o.outerY - h * 0.07, w * 0.64, -h * o.outerY - h * 0.12);
    c.strokeStyle = P.lash;
    c.lineWidth = 4.4 * o.lash;
    c.lineCap = 'round';
    c.stroke();
    c.restore();

    // 쌍꺼풀
    c.save();
    c.beginPath();
    c.moveTo(-w * 0.32, -h * (o.up1 - 0.02));
    c.bezierCurveTo(-w * 0.08, -h * (o.up2 + 0.14), w * 0.24, -h * (o.up2 + 0.11), w * 0.50, -h * (o.outerY + 0.18));
    c.strokeStyle = AE.alpha(P.skinDeep, 0.45);
    c.lineWidth = 2.4;
    c.stroke();
    c.restore();

    // 캐런큘러
    c.beginPath();
    c.ellipse(-w * 0.44, h * (o.innerY + 0.02), 5, 3.6, 0.3, 0, Math.PI * 2);
    c.fillStyle = AE.alpha(P.blush, 0.65);
    c.fill();

    c.restore();
  };

  /* ---------------- 눈썹 ---------------- */
  AE.drawBrow = function (cx, cy, s) {
    const c = ctx(), P = G.pal;
    const style = G.ch.parts.eyebrow;
    const f = AE.headShape();
    const len = f.hw * 0.44;
    c.save();
    c.translate(cx, cy);
    c.scale(s, 1);
    const p = new Path2D();
    if (style === 'straight') {
      p.moveTo(-len * 0.92, -1);
      p.bezierCurveTo(-len * 0.3, -9, len * 0.35, -10, len * 0.96, -1);
      p.bezierCurveTo(len * 0.35, -2, -len * 0.3, -1, -len * 0.92, 7);
    } else if (style === 'arch') {
      p.moveTo(-len * 0.94, 5);
      p.bezierCurveTo(-len * 0.35, -16, len * 0.30, -19, len * 0.98, -2);
      p.bezierCurveTo(len * 0.30, -8, -len * 0.35, -6, -len * 0.94, 12);
    } else if (style === 'cool') {
      p.moveTo(-len * 0.94, 8);
      p.bezierCurveTo(-len * 0.4, -8, len * 0.35, -18, len * 1.0, -5);
      p.bezierCurveTo(len * 0.35, -5, -len * 0.4, 0, -len * 0.94, 15);
    } else if (style === 'gentle') {
      p.moveTo(-len * 0.92, -7);
      p.bezierCurveTo(-len * 0.3, -12, len * 0.35, -3, len * 0.96, 9);
      p.bezierCurveTo(len * 0.35, 3, -len * 0.3, -1, -len * 0.92, 2);
    } else {
      p.moveTo(-len * 0.96, -3);
      p.bezierCurveTo(-len * 0.25, -20, len * 0.45, -20, len * 1.0, 0);
      p.bezierCurveTo(len * 0.4, -3, -len * 0.25, -5, -len * 0.96, 15);
    }
    p.closePath();
    AE.fill(p, AE.shade(P.hairLine, -3, 0, 0), 0.88);
    c.restore();
  };

  /* ---------------- 코 ---------------- */
  AE.drawNose = function () {
    const c = ctx(), P = G.pal, f = AE.headShape();
    const kind = G.ch.parts.nose;
    const y = f.chinY - (f.chinY - f.topY) * 0.185;
    c.save();
    if (kind === 'dot') {
      c.beginPath();
      c.ellipse(CX + 5, y + 4, 4.6, 3.6, 0, 0, Math.PI * 2);
      c.fillStyle = AE.alpha(P.skinDeep, 0.68);
      c.fill();
    } else if (kind === 'line') {
      c.beginPath();
      c.moveTo(CX + 3, y - 12);
      c.quadraticCurveTo(CX + 8, y + 2, CX + 4, y + 8);
      c.strokeStyle = AE.alpha(P.skinDeep, 0.75);
      c.lineWidth = 2.8;
      c.lineCap = 'round';
      c.stroke();
    } else if (kind === 'shade') {
      const g = c.createLinearGradient(CX + 2, y - 40, CX + 18, y + 8);
      g.addColorStop(0, AE.alpha(P.skinShade, 0));
      g.addColorStop(1, AE.alpha(P.skinShade, 0.5));
      c.fillStyle = g;
      c.beginPath();
      c.moveTo(CX - 2, y - 44);
      c.quadraticCurveTo(CX + 22, y - 6, CX + 10, y + 10);
      c.quadraticCurveTo(CX + 2, y + 4, CX - 2, y - 44);
      c.fill();
      c.beginPath();
      c.moveTo(CX - 7, y + 6);
      c.quadraticCurveTo(CX + 4, y + 12, CX + 14, y + 3);
      c.strokeStyle = AE.alpha(P.skinDeep, 0.45);
      c.lineWidth = 2.2;
      c.stroke();
    }
    c.restore();
  };

  /* ---------------- 입 ---------------- */
  AE.drawMouth = function () {
    const c = ctx(), P = G.pal, f = AE.headShape();
    const y = f.chinY - (f.chinY - f.topY) * 0.098;
    const type = G.ch.parts.mouth;
    const lip = P.lip;
    c.save();
    if (type === 'cat') {
      c.beginPath();
      c.moveTo(CX - 17, y - 3);
      c.quadraticCurveTo(CX - 8, y + 8, CX, y - 1);
      c.quadraticCurveTo(CX + 8, y + 8, CX + 17, y - 3);
      c.strokeStyle = lip;
      c.lineWidth = 3.2;
      c.lineCap = 'round';
      c.stroke();
    } else if (type === 'open') {
      const w = 15, h = 13;
      const p = new Path2D();
      p.moveTo(CX - w, y - 4);
      p.quadraticCurveTo(CX, y - 8, CX + w, y - 4);
      p.quadraticCurveTo(CX + w * 0.6, y + h, CX, y + h);
      p.quadraticCurveTo(CX - w * 0.6, y + h, CX - w, y - 4);
      p.closePath();
      AE.fill(p, '#5b2230');
      c.save();
      c.clip(p);
      c.beginPath();
      c.ellipse(CX, y + h * 0.8, w * 0.62, h * 0.5, 0, 0, Math.PI * 2);
      c.fillStyle = '#c9646f';
      c.fill();
      c.beginPath();
      c.rect(CX - w, y - 6, w * 2, 4);
      c.fillStyle = 'rgba(255,255,255,0.85)';
      c.fill();
      c.restore();
      AE.stroke(p, AE.shade(lip, -12), 1.8, 0.8);
    } else if (type === 'smirk') {
      c.beginPath();
      c.moveTo(CX - 15, y + 3);
      c.quadraticCurveTo(CX + 2, y + 8, CX + 19, y - 6);
      c.strokeStyle = lip;
      c.lineWidth = 3.4;
      c.lineCap = 'round';
      c.stroke();
    } else if (type === 'pout') {
      c.beginPath();
      c.moveTo(CX - 9, y - 2);
      c.quadraticCurveTo(CX, y + 6, CX + 9, y - 2);
      c.quadraticCurveTo(CX, y - 7, CX - 9, y - 2);
      c.fillStyle = AE.shade(lip, 4);
      c.fill();
    } else if (type === 'flat') {
      c.beginPath();
      c.moveTo(CX - 12, y);
      c.lineTo(CX + 12, y);
      c.strokeStyle = lip;
      c.lineWidth = 3;
      c.lineCap = 'round';
      c.stroke();
    } else {
      c.beginPath();
      c.moveTo(CX - 13, y - 2);
      c.quadraticCurveTo(CX, y + 6, CX + 13, y - 2);
      c.strokeStyle = lip;
      c.lineWidth = 3;
      c.lineCap = 'round';
      c.stroke();
      c.beginPath();
      c.moveTo(CX - 4, y + 7);
      c.quadraticCurveTo(CX, y + 9, CX + 4, y + 6);
      c.strokeStyle = AE.alpha(P.skinShade, 0.4);
      c.lineWidth = 1.8;
      c.stroke();
    }
    c.restore();
  };

  /* ---------------- 볼 ---------------- */
  AE.drawCheeks = function () {
    const c = ctx(), P = G.pal, f = AE.headShape(), p = G.ch.parts;
    const y = f.chinY - (f.chinY - f.topY) * 0.30;
    const strong = p.expression === 'shy' || p.expression === 'grin' || p.expression === 'surprise';
    for (const s of [-1, 1]) {
      c.save();
      c.translate(CX + s * f.hw * 0.62, y);
      c.rotate(s * 0.08);
      c.scale(1, 0.6);
      const g = c.createRadialGradient(0, 0, 2, 0, 0, 46);
      g.addColorStop(0, AE.alpha(P.blush, strong ? 0.7 : 0.5));
      g.addColorStop(0.6, AE.alpha(P.blush, strong ? 0.34 : 0.22));
      g.addColorStop(1, AE.alpha(P.blush, 0));
      c.fillStyle = g;
      c.beginPath();
      c.arc(0, 0, 46, 0, Math.PI * 2);
      c.fill();
      c.restore();
    }
    if (p.cheekFx === 'hatch') {
      c.save();
      c.strokeStyle = AE.alpha(AE.shade(P.blush, -18, 10), 0.42);
      c.lineWidth = 2.2;
      c.lineCap = 'round';
      for (const s of [-1, 1]) {
        for (let i = 0; i < 3; i++) {
          const x = CX + s * (f.hw * 0.62 - 14 + i * 14);
          c.beginPath();
          c.moveTo(x - 4, y + 12);
          c.lineTo(x + 4, y - 12);
          c.stroke();
        }
      }
      c.restore();
    }
    if (p.cheekFx === 'mole') {
      c.beginPath();
      c.ellipse(CX - f.hw * 0.42, y + 18, 3, 3, 0, 0, Math.PI * 2);
      c.fillStyle = AE.shade(P.skinDeep, -22, -10);
      c.fill();
    }
    if (p.cheekFx === 'sweat') {
      const sx = CX + f.hw * 0.80, sy = y - 62;
      const g = c.createLinearGradient(sx, sy - 16, sx + 8, sy + 20);
      g.addColorStop(0, 'rgba(255,255,255,0.94)');
      g.addColorStop(1, 'rgba(168,214,255,0.9)');
      c.beginPath();
      c.moveTo(sx, sy - 19);
      c.bezierCurveTo(sx + 16, sy + 2, sx + 11, sy + 20, sx, sy + 20);
      c.bezierCurveTo(sx - 11, sy + 20, sx - 16, sy + 2, sx, sy - 19);
      c.closePath();
      c.fillStyle = g;
      c.fill();
      c.strokeStyle = 'rgba(120,170,220,0.45)';
      c.lineWidth = 1.4;
      c.stroke();
      c.beginPath();
      c.ellipse(sx - 4, sy + 7, 2.6, 4.4, -0.3, 0, Math.PI * 2);
      c.fillStyle = 'rgba(255,255,255,0.9)';
      c.fill();
    }
    if (p.cheekFx === 'band') {
      c.save();
      c.translate(CX + f.hw * 0.58, y + 12);
      c.rotate(-0.22);
      const bw = 54, bh = 19;
      c.fillStyle = '#fdf3e6';
      c.beginPath();
      c.roundRect(-bw / 2, -bh / 2, bw, bh, 7);
      c.fill();
      c.strokeStyle = 'rgba(190,170,150,0.8)';
      c.lineWidth = 1.6;
      c.stroke();
      c.fillStyle = 'rgba(232,218,202,0.9)';
      c.beginPath();
      c.roundRect(-bw * 0.16, -bh / 2, bw * 0.32, bh, 3);
      c.fill();
      c.restore();
    }
  };

  /* ---------------- 얼굴 ---------------- */
  AE.drawFace = function () {
    const c = ctx(), P = G.pal, f = AE.headShape();
    const face = AE.facePath();

    if (G.ch.parts.ears === 'human') {
      for (const s of [-1, 1]) {
        c.save();
        c.translate(CX + s * f.hw * 0.95, f.chinY - (f.chinY - f.topY) * 0.30);
        c.rotate(s * 0.14);
        c.beginPath();
        c.ellipse(0, 0, 14, 26, 0, 0, Math.PI * 2);
        c.fillStyle = P.skinShade;
        c.fill();
        c.beginPath();
        c.ellipse(s * 2, 2, 6.5, 14, 0, 0, Math.PI * 2);
        c.fillStyle = AE.alpha(P.skinDeep, 0.55);
        c.fill();
        c.restore();
      }
    }

    const fg = c.createLinearGradient(0, f.topY, 0, f.chinY);
    fg.addColorStop(0, P.skinMid);
    fg.addColorStop(0.45, P.skin);
    fg.addColorStop(1, P.skinMid);
    AE.fill(face, fg);

    c.save();
    c.clip(face);
    // 이마 그늘
    const hg = c.createLinearGradient(0, f.topY + 40, 0, f.topY + 170);
    hg.addColorStop(0, AE.alpha(P.skinShade, 0.75));
    hg.addColorStop(0.55, AE.alpha(P.skinShade, 0.24));
    hg.addColorStop(1, AE.alpha(P.skinShade, 0));
    c.fillStyle = hg;
    c.fillRect(CX - f.hw, f.topY, f.hw * 2, 180);
    // 옆면 그늘
    for (const s of [-1, 1]) {
      const g = c.createLinearGradient(CX + s * f.hw * 0.66, 0, CX + s * f.hw * 1.03, 0);
      g.addColorStop(0, AE.alpha(P.skinShade, 0));
      g.addColorStop(1, AE.alpha(P.skinShade, 0.45));
      c.fillStyle = g;
      c.fillRect(s < 0 ? CX - f.hw : CX + f.hw * 0.66, f.topY, f.hw * 0.4, f.chinY - f.topY);
    }
    // 턱 그늘 · 광대 하이라이트
    AE.softBlob(CX, f.chinY - 16, f.hw * 0.72, 24, P.skinShade, 0.35);
    AE.softBlob(CX - f.hw * 0.46, f.chinY - (f.chinY - f.topY) * 0.36, 40, 22, P.skinHi, 0.45);
    AE.softBlob(CX + f.hw * 0.50, f.chinY - (f.chinY - f.topY) * 0.34, 34, 20, P.skinHi, 0.3);
    c.restore();
    AE.stroke(face, AE.alpha(P.line, 0.55), 3.6, 0.55);
  };

  AE.drawFeatures = function () {
    const f = AE.headShape(), P = G.pal, p = G.ch.parts;
    const eyeY = f.eyeY;
    const dx = f.hw * f.eyeDx;
    const expr = p.expression;
    let open = 1, closedL = false;
    if (expr === 'smile') open = 0.93;
    if (expr === 'grin') open = 0.8;
    if (expr === 'cool') open = 0.78;
    if (expr === 'shy') open = 0.9;
    if (expr === 'surprise') open = 1.1;
    if (expr === 'wink') closedL = true;
    if (p.eyeShape === 'sleepy') open *= 0.92;

    const turn = AE.G.turn || 0;
    for (let i = 0; i < 2; i++) {
      const s = [-1, 1][i];
      const isB = i === 1 && p.oddEye;
      const iris = isB ? P.irisB : P.iris;
      const light = isB ? P.irisBLight : P.irisLight;
      const dark = isB ? P.irisBDark : P.irisDark;
      const far = (turn > 0 && s < 0) || (turn < 0 && s > 0);
      const xs = far ? Math.max(0.62, 1 - Math.abs(turn) * 0.5) : 1;
      if (i === 0 && closedL) AE.drawClosedEye(CX + s * dx, eyeY, s);
      else AE.drawEye(CX + s * dx, eyeY, s, iris, light, dark, i, open, xs);
    }
    let browDy = 0;
    if (expr === 'surprise') browDy = -9;
    if (expr === 'cool') browDy = -3;
    if (expr === 'shy') browDy = 4;
    for (let i = 0; i < 2; i++) {
      const s = [-1, 1][i];
      AE.drawBrow(CX + s * dx, eyeY - 62 + browDy, s);
    }
    AE.drawNose();
    AE.drawMouth();
    AE.drawCheeks();
  };

  /* ---------------- 목 ---------------- */
  AE.drawNeck = function (fig) {
    const c = ctx(), P = G.pal, f = AE.headShape();
    const chinY = f.chinY;
    const nw = fig.neckW;
    const shY = fig.shoulderY;
    const p = new Path2D();
    p.moveTo(CX - nw, chinY - 52);
    p.bezierCurveTo(CX - nw * 1.04, chinY + 30, CX - nw * 1.6, shY - 60, CX - nw * 2.6, shY - 8);
    p.lineTo(CX + nw * 2.6, shY - 8);
    p.bezierCurveTo(CX + nw * 1.6, shY - 60, CX + nw * 1.04, chinY + 30, CX + nw, chinY - 52);
    p.closePath();
    AE.fill(p, P.skinMid);
    AE.innerShadow(p, P.skinDeep, 0, 22, 0.6);
    const sh = new Path2D();
    sh.moveTo(CX - nw, chinY - 40);
    sh.bezierCurveTo(CX - nw * 0.5, chinY + 18, CX + nw * 0.5, chinY + 18, CX + nw, chinY - 40);
    sh.lineTo(CX + nw, chinY - 64);
    sh.lineTo(CX - nw, chinY - 64);
    sh.closePath();
    AE.fill(sh, P.skinShade, 0.9);
    c.save();
    c.clip(p);
    AE.softBlob(CX - 10, chinY + 62, 50, 22, P.skinHi, 0.35);
    c.restore();
  };
})();
