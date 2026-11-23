/* ============================================================
   draw-body.js — 키/가슴/엉덩이/덩치 · 포즈 · 의상 · 손
   모든 좌표는 헤드 스페이스 (1두신 = 376, 정수리 y=212)
   ============================================================ */
(function () {
  const AE = (window.AE = window.AE || {});
  const CX = 512, TOP = 212, HH = 376;
  const yF = (f) => TOP + f * HH;

  const HEIGHT = {
    petite: { ratio: 6.5, bustF: 1.92, waistF: 2.92, hipF: 3.45, kneeF: 4.70, ankleF: 6.18, label: '아담 152cm · 6.5등신' },
    standard: { ratio: 7.2, bustF: 2.00, waistF: 3.00, hipF: 3.60, kneeF: 5.25, ankleF: 6.90, label: '표준 164cm · 7.2등신' },
    tall: { ratio: 8.0, bustF: 2.02, waistF: 3.05, hipF: 3.75, kneeF: 5.85, ankleF: 7.70, label: '장신 176cm · 8등신' },
  };
  const BUILD = {
    slender: { sh: 196, rib: 114, waist: 94, limb: 25, neck: 24, label: '여리여리' },
    standard: { sh: 222, rib: 134, waist: 112, limb: 31, neck: 27, label: '표준' },
    athletic: { sh: 248, rib: 150, waist: 122, limb: 36, neck: 31, label: '탄탄 스포티' },
    wide: { sh: 266, rib: 166, waist: 136, limb: 41, neck: 34, label: '듬직 와이드' },
  };
  const BUST = { flat: { add: 0, proj: 0.25 }, medium: { add: 9, proj: 0.55 }, large: { add: 20, proj: 0.85 }, glamour: { add: 32, proj: 1.1 } };
  const HIP = { slim: 126, normal: 152, wide: 176, full: 200 };

  const POSE = {
    standing: { headTilt: 0, turn: 0, arms: 'down' },
    tilt: { headTilt: -9, turn: 0.1, arms: 'down' },
    threequarter: { headTilt: 2, turn: 0.62, arms: 'down', bodyShift: 26 },
    v_sign: { headTilt: 4, turn: 0.1, arms: 'v' },
    wave: { headTilt: -4, turn: -0.06, arms: 'wave' },
    crossed: { headTilt: 0, turn: 0, arms: 'cross' },
    hip: { headTilt: -3, turn: 0.12, arms: 'hip' },
    chin: { headTilt: 5, turn: 0.2, arms: 'chin' },
  };

  AE.HEIGHT = HEIGHT; AE.BUILD = BUILD; AE.BUST = BUST; AE.HIP = HIP; AE.POSE = POSE;

  AE.figure = function (ch) {
    const h = HEIGHT[ch.parts.height] || HEIGHT.standard;
    const b = BUILD[ch.parts.build] || BUILD.standard;
    const bu = BUST[ch.parts.bust] || BUST.medium;
    const hipW = (HIP[ch.parts.hip] || HIP.normal) * (0.94 + (b.sh / 186) * 0.12);
    return {
      h, b, bu,
      ratio: h.ratio,
      chinY: 588,
      shoulderY: yF(1.22),
      armpitY: yF(1.66),
      bustY: yF(h.bustF),
      waistY: yF(h.waistF),
      hipY: yF(h.hipF),
      crotchY: yF(h.hipF + 0.12),
      kneeY: yF(h.kneeF),
      ankleY: yF(h.ankleF),
      soleY: yF(h.ratio),
      shW: b.sh,
      ribW: b.rib + bu.add,
      waistW: Math.max(b.waist, hipW * 0.63) + bu.add * 0.18,
      hipW,
      limb: b.limb,
      neckW: b.neck,
      bustProj: bu.proj,
      bustAdd: bu.add,
    };
  };

  /* ---------------- 몸통 실루엣 ---------------- */
  AE.torsoPath = function (fig) {
    const p = new Path2D();
    const { chinY, shoulderY, bustY, waistY, hipY, crotchY, shW, ribW, waistW, hipW, neckW } = fig;
    const shoulder = (s) => {
      p.bezierCurveTo(CX + s * neckW * 2.4, chinY + 122, CX + s * shW * 0.86, shoulderY + 26, CX + s * shW * 0.982, shoulderY + 58);
      p.quadraticCurveTo(CX + s * shW * 1.035, shoulderY + 106, CX + s * shW * 0.982, shoulderY + 156);
      p.bezierCurveTo(CX + s * shW * 0.95, shoulderY + 220, CX + s * ribW * 1.0, bustY - 120, CX + s * ribW, bustY - 30);
      p.bezierCurveTo(CX + s * ribW, bustY + 70, CX + s * waistW, waistY - 40, CX + s * waistW, waistY);
      p.bezierCurveTo(CX + s * waistW, waistY + 54, CX + s * hipW * 0.99, hipY - 60, CX + s * hipW, hipY);
    };
    p.moveTo(CX - neckW * 1.55, chinY + 74);
    shoulder(-1);
    p.bezierCurveTo(CX - hipW * 0.98, hipY + 54, CX - hipW * 0.7, crotchY + 20, CX - hipW * 0.30, crotchY + 34);
    p.lineTo(CX + hipW * 0.30, crotchY + 34);
    p.bezierCurveTo(CX + hipW * 0.7, crotchY + 20, CX + hipW * 0.98, hipY + 54, CX + hipW, hipY);
    shoulder(1);
    p.bezierCurveTo(CX + shW * 0.86, shoulderY + 26, CX + neckW * 2.4, chinY + 122, CX + neckW * 1.55, chinY + 74);
    p.closePath();
    return p;
  };

  /* ---------------- 다리 / 발 ---------------- */
  function legPath(fig, s) {
    const { hipY, kneeY, ankleY, hipW, limb } = fig;
    const hipX = CX + s * hipW * 0.46;
    const kneeX = CX + s * hipW * 0.40;
    const ankleX = CX + s * hipW * 0.36;
    const thW = hipW * 0.31;
    const knW = limb * 1.26;
    const caW = limb * 1.62;
    const anW = limb * 0.86;
    const p = new Path2D();
    p.moveTo(hipX - s * thW, hipY - 20);
    p.bezierCurveTo(hipX - s * thW * 1.02, kneeY - 190, kneeX - s * knW * 1.02, kneeY - 90, kneeX - s * knW, kneeY);
    p.bezierCurveTo(kneeX - s * caW, kneeY + 120, ankleX - s * anW * 1.15, ankleY - 130, ankleX - s * anW, ankleY);
    p.bezierCurveTo(ankleX - s * anW * 0.3, ankleY + 14, ankleX + s * anW * 1.0, ankleY + 14, ankleX + s * anW * 1.06, ankleY - 6);
    p.bezierCurveTo(ankleX + s * anW * 1.5, ankleY - 140, kneeX + s * caW * 0.95, kneeY + 100, kneeX + s * knW * 0.95, kneeY);
    p.bezierCurveTo(kneeX + s * knW * 0.95, kneeY - 90, hipX + s * thW * 1.02, kneeY - 200, hipX + s * thW * 1.05, hipY - 20);
    p.closePath();
    return p;
  }
  function footPath(fig, s) {
    const { ankleY, soleY, hipW, limb } = fig;
    const ax = CX + s * hipW * 0.36;
    const p = new Path2D();
    p.moveTo(ax - s * limb * 0.85, ankleY - 18);
    p.bezierCurveTo(ax - s * limb * 1.0, soleY - 46, ax - s * limb * 1.0, soleY - 4, ax - s * limb * 0.35, soleY);
    p.bezierCurveTo(ax + s * limb * 1.4, soleY + 3, ax + s * limb * 2.2, soleY - 8, ax + s * limb * 2.2, soleY - 28);
    p.bezierCurveTo(ax + s * limb * 2.1, soleY - 48, ax + s * limb * 1.4, ankleY - 4, ax + s * limb * 0.75, ankleY - 24);
    p.closePath();
    return p;
  }

  /* ---------------- 팔 좌표 ---------------- */
  function armPoints(fig, s, mode) {
    const { shoulderY, bustY, waistY, hipY, chinY, shW, ribW, hipW, limb } = fig;
    const sx = CX + s * shW * 0.74, sy = shoulderY + 80;
    let ex, ey, wx, wy;
    if (mode === 'v') {
      ex = CX + s * (ribW + 108); ey = bustY + 120;
      wx = CX + s * (ribW * 0.36 + 46); wy = chinY - 10;
    } else if (mode === 'wave') {
      ex = CX + s * (shW + 44); ey = bustY - 30;
      wx = CX + s * (shW - 22); wy = chinY - 150;
    } else if (mode === 'cross') {
      ex = CX + s * (ribW + 74); ey = bustY + 120;
      wx = CX - s * (ribW * 0.52); wy = bustY + 168;
    } else if (mode === 'hip') {
      ex = CX + s * (shW + 62); ey = waistY - 20;
      wx = CX + s * (hipW * 0.90); wy = waistY + 70;
    } else if (mode === 'chin') {
      ex = CX + s * (ribW + 86); ey = bustY - 6;
      wx = CX + s * 52; wy = chinY + 44;
    } else {
      ex = CX + s * ribW * 1.02; ey = waistY - 30;
      wx = CX + s * hipW * 0.94; wy = hipY + 40;
    }
    return { sx, sy, ex, ey, wx, wy };
  }
  AE.armPoints = armPoints;

  function limbPath(pts) {
    return pts;
  }

  function strokeLimb(pts, width, color) {
    const ctx = AE.G.ctx;
    ctx.save();
    ctx.strokeStyle = color;
    ctx.lineCap = 'round';
    ctx.lineJoin = 'round';
    ctx.beginPath();
    ctx.moveTo(pts.sx, pts.sy);
    ctx.quadraticCurveTo(pts.ex, pts.ey, pts.wx, pts.wy);
    ctx.lineWidth = width;
    ctx.stroke();
    ctx.beginPath();
    const mx = (pts.ex + pts.wx) / 2, my = (pts.ey + pts.wy) / 2;
    ctx.moveTo(mx, my);
    ctx.quadraticCurveTo((mx + pts.wx) / 2, (my + pts.wy) / 2, pts.wx, pts.wy);
    ctx.lineWidth = width * 0.84;
    ctx.stroke();
    ctx.restore();
  }

  /* ---------------- 손 ---------------- */
  function capsule(p, x1, y1, x2, y2, r) {
    const a = Math.atan2(y2 - y1, x2 - x1);
    p.moveTo(x1 + Math.cos(a - Math.PI / 2) * r, y1 + Math.sin(a - Math.PI / 2) * r);
    p.arc(x1, y1, r, a - Math.PI / 2, a + Math.PI / 2, true);
    p.lineTo(x2 + Math.cos(a + Math.PI / 2) * r, y2 + Math.sin(a + Math.PI / 2) * r);
    p.arc(x2, y2, r, a + Math.PI / 2, a - Math.PI / 2, true);
    p.closePath();
  }

  /* 팔다리 실루엣 폴리곤 */
  function limbPoly(pts, wS, wE, wW) {
    const { sx, sy, ex, ey, wx, wy } = pts;
    const d1 = Math.atan2(ey - sy, ex - sx) + Math.PI / 2;
    const d2 = Math.atan2(wy - ey, wx - ex) + Math.PI / 2;
    const p = new Path2D();
    p.moveTo(sx + Math.cos(d1) * wS * 0.5, sy + Math.sin(d1) * wS * 0.5);
    p.lineTo(ex + Math.cos((d1 + d2) / 2) * wE * 0.5, ey + Math.sin((d1 + d2) / 2) * wE * 0.5);
    p.lineTo(wx + Math.cos(d2) * wW * 0.5, wy + Math.sin(d2) * wW * 0.5);
    p.quadraticCurveTo(wx + Math.cos(d2 + 1.2) * wW * 0.52, wy + Math.sin(d2 + 1.2) * wW * 0.52,
      wx - Math.cos(d2) * wW * 0.5, wy - Math.sin(d2) * wW * 0.5);
    p.lineTo(ex - Math.cos((d1 + d2) / 2) * wE * 0.5, ey - Math.sin((d1 + d2) / 2) * wE * 0.5);
    p.lineTo(sx - Math.cos(d1) * wS * 0.5, sy - Math.sin(d1) * wS * 0.5);
    p.quadraticCurveTo(sx, sy - wS * 0.06, sx + Math.cos(d1) * wS * 0.5, sy + Math.sin(d1) * wS * 0.5);
    p.closePath();
    return p;
  }

  AE.drawHand = function (x, y, ang, size, type) {
    const ctx = AE.G.ctx, P = AE.G.pal;
    ctx.save();
    ctx.translate(x, y);
    ctx.rotate(ang);
    ctx.scale(size, size);
    const skin = P.skin, sh = P.skinShade, deep = P.skinDeep;
    function part(fn) {
      const p = new Path2D();
      fn(p);
      AE.fill(p, skin);
      AE.innerShadow(p, sh, 3, 5, 0.5);
      AE.stroke(p, AE.alpha(deep, 0.5), 1.8, 0.6);
    }
    if (type === 'v') {
      part((p) => { p.roundRect(-27, -8, 54, 56, 21); });
      part((p) => capsule(p, -12, 2, -19, -78, 10));
      part((p) => capsule(p, 15, 2, 26, -72, 10));
      part((p) => capsule(p, 27, 14, 35, 42, 11));
      part((p) => capsule(p, -25, 18, -45, 0, 10));
      ctx.beginPath();
      ctx.moveTo(-8, 18);
      ctx.quadraticCurveTo(6, 25, 17, 16);
      ctx.strokeStyle = AE.alpha(deep, 0.3);
      ctx.lineWidth = 2;
      ctx.stroke();
    } else if (type === 'open') {
      part((p) => { p.roundRect(-29, -8, 58, 54, 19); });
      part((p) => capsule(p, -21, 2, -33, -64, 9));
      part((p) => capsule(p, -7, 0, -11, -80, 9.5));
      part((p) => capsule(p, 8, 0, 13, -84, 9.5));
      part((p) => capsule(p, 23, 2, 31, -72, 9));
      part((p) => capsule(p, -27, 20, -47, 4, 10));
    } else if (type === 'fist') {
      part((p) => { p.roundRect(-29, -16, 58, 62, 23); });
      for (let i = 0; i < 3; i++) part((p) => capsule(p, -18 + i * 18, -12, -20 + i * 18, -30, 10.5));
      part((p) => capsule(p, -27, 16, -43, 4, 10));
    } else {
      part((p) => { p.roundRect(-26, -6, 52, 54, 18); });
      for (let i = 0; i < 4; i++) part((p) => capsule(p, -18 + i * 12, 0, -20 + i * 13, -32 - (i === 1 || i === 2 ? 6 : 0), 8.5));
    }
    ctx.restore();
  };

  /* ---------------- 신체 ---------------- */
  AE.drawBodySkin = function (fig) {
    const ctx = AE.G.ctx, P = AE.G.pal;

    // 다리
    for (const s of [-1, 1]) {
      const lp = legPath(fig, s);
      AE.fill(lp, P.skin);
      AE.innerShadow(lp, P.skinShade, -s * 28, 0, 0.5);
      AE.innerShadow(lp, P.skinDeep, 0, -70, 0.22);
      ctx.save();
      ctx.clip(lp);
      AE.softBlob(CX + s * fig.hipW * 0.46, fig.kneeY + 30, 22, 90, P.skinHi, 0.32);
      ctx.restore();
      AE.stroke(lp, AE.alpha(P.skinDeep, 0.35), 2.2, 0.45);
    }

    // 몸통
    const tp = AE.torsoPath(fig);
    AE.fill(tp, P.skin);
    ctx.save();
    ctx.clip(tp);
    const by = fig.bustY;
    for (const s of [-1, 1]) {
      AE.softBlob(CX + s * fig.ribW * 0.42, by + 4, fig.ribW * 0.36, 22 * fig.bustProj + 18, P.skinShade, 0.3);
      AE.softBlob(CX + s * fig.ribW * 0.34, by + 40, fig.ribW * 0.24, 16, P.skinDeep, 0.18);
    }
    for (const s of [-1, 1]) {
      const g = ctx.createLinearGradient(CX + s * fig.ribW * 0.5, 0, CX + s * fig.hipW, 0);
      g.addColorStop(0, AE.alpha(P.skinShade, 0));
      g.addColorStop(1, AE.alpha(P.skinShade, 0.45));
      ctx.fillStyle = g;
      ctx.fillRect(s < 0 ? CX - fig.hipW : CX + fig.ribW * 0.5, fig.shoulderY, fig.hipW, fig.crotchY - fig.shoulderY);
    }
    AE.softBlob(CX, fig.chinY + 96, 84, 38, P.skinShade, 0.35);
    if (AE.G.ch.parts.outfit === 'resort') {
      ctx.beginPath();
      ctx.ellipse(CX, fig.waistY + 46, 5, 7, 0, 0, Math.PI * 2);
      ctx.fillStyle = AE.alpha(P.skinDeep, 0.45);
      ctx.fill();
    }
    ctx.restore();

    // 쇄골
    ctx.save();
    for (const s of [-1, 1]) {
      ctx.beginPath();
      ctx.moveTo(CX + s * fig.neckW * 0.5, fig.chinY + 86);
      ctx.quadraticCurveTo(CX + s * fig.neckW * 2.0, fig.chinY + 94, CX + s * fig.neckW * 3.0, fig.chinY + 74);
      ctx.strokeStyle = AE.alpha(P.skinShade, 0.65);
      ctx.lineWidth = 3.2;
      ctx.lineCap = 'round';
      ctx.stroke();
      ctx.beginPath();
      ctx.moveTo(CX + s * fig.neckW * 0.6, fig.chinY + 98);
      ctx.quadraticCurveTo(CX + s * fig.neckW * 2.0, fig.chinY + 106, CX + s * fig.neckW * 3.0, fig.chinY + 86);
      ctx.strokeStyle = AE.alpha(P.skinHi, 0.5);
      ctx.lineWidth = 2.4;
      ctx.stroke();
    }
    ctx.restore();
  };

  /* 팔 — phase: 'limb' | 'hand' */
  AE.drawArms = function (fig, arms, phase, which) {
    const ctx = AE.G.ctx, P = AE.G.pal;
    for (const a of arms) {
      if (which && a.which !== which) continue;
      const pts = armPoints(fig, a.s, a.mode);
      const w = fig.limb * 1.28;
      if (phase === 'limb') {
        const poly = limbPoly(pts, w * 1.02, w * 0.86, w * 0.72);
        AE.fill(poly, P.skin);
        AE.innerShadow(poly, P.skinShade, -a.s * 12, 0, 0.55);
        AE.stroke(poly, AE.alpha(P.skinDeep, 0.35), 2.2, 0.5);
      } else {
        const ang = Math.atan2(pts.wy - pts.ey, pts.wx - pts.ex) - Math.PI / 2;
        const t = a.mode === 'v' ? 'v' : a.mode === 'wave' ? 'open' : a.mode === 'cross' ? 'fist' : a.mode === 'chin' ? 'fist' : a.mode === 'hip' ? 'fist' : 'flat';
        AE.drawHand(pts.wx, pts.wy, a.mode === 'cross' ? ang + 1.15 * a.s : ang, (fig.limb / 30) * 1.2, t);
      }
    }
  };

  AE.buildArms = function (pose) {
    if (pose.arms === 'cross') return [{ s: -1, mode: 'cross', which: 'fore' }, { s: 1, mode: 'cross', which: 'fore' }];
    if (pose.arms === 'v') return [{ s: -1, mode: 'down', which: 'back' }, { s: 1, mode: 'v', which: 'fore' }];
    if (pose.arms === 'wave') return [{ s: 1, mode: 'down', which: 'back' }, { s: -1, mode: 'wave', which: 'fore' }];
    if (pose.arms === 'hip') return [{ s: -1, mode: 'down', which: 'back' }, { s: 1, mode: 'hip', which: 'fore' }];
    if (pose.arms === 'chin') return [{ s: 1, mode: 'down', which: 'back' }, { s: -1, mode: 'chin', which: 'fore' }];
    return [{ s: -1, mode: 'down', which: 'back' }, { s: 1, mode: 'down', which: 'back' }];
  };

  /* ---------------- 의상 헬퍼 ---------------- */
  function skirtPath(fig, hemY, hemW, pleats) {
    const { waistY } = fig;
    const p = new Path2D();
    const topW = fig.waistW * 1.03;
    p.moveTo(CX - topW, waistY - 14);
    p.bezierCurveTo(CX - topW * 1.08, waistY + 80, CX - hemW * 0.88, hemY - 130, CX - hemW, hemY);
    const n = pleats || 7;
    for (let i = 0; i < n; i++) {
      const x0 = CX - hemW + (2 * hemW * (i + 0.5)) / n;
      const x1 = CX - hemW + (2 * hemW * (i + 1)) / n;
      p.quadraticCurveTo(x0, hemY + 22, x1, hemY - (i % 2 ? 16 : 0));
    }
    p.bezierCurveTo(CX + hemW * 0.88, hemY - 130, CX + topW * 1.08, waistY + 80, CX + topW, waistY - 14);
    p.closePath();
    return p;
  }

  function pantsPath(fig, hemY) {
    const { waistY, hipW, limb, crotchY } = fig;
    const p = new Path2D();
    const w = fig.waistW * 1.03;
    p.moveTo(CX - w, waistY - 14);
    p.bezierCurveTo(CX - hipW * 1.02, waistY + 80, CX - hipW * 1.0, crotchY, CX - hipW * 0.58, crotchY + 26);
    p.bezierCurveTo(CX - hipW * 0.54, hemY - 220, CX - limb * 1.55, hemY - 70, CX - limb * 1.4, hemY);
    p.lineTo(CX - limb * 0.18, hemY);
    p.bezierCurveTo(CX - limb * 0.3, crotchY + 130, CX - limb * 0.3, crotchY + 70, CX, crotchY + 44);
    p.bezierCurveTo(CX + limb * 0.3, crotchY + 70, CX + limb * 0.3, crotchY + 130, CX + limb * 0.18, hemY);
    p.lineTo(CX + limb * 1.4, hemY);
    p.bezierCurveTo(CX + limb * 1.55, hemY - 70, CX + hipW * 0.54, hemY - 220, CX + hipW * 0.58, crotchY + 26);
    p.bezierCurveTo(CX + hipW * 1.0, crotchY, CX + hipW * 1.02, waistY + 80, CX + w, waistY - 14);
    p.closePath();
    return p;
  }

  function shadeFabric(path, pal, opt) {
    opt = opt || {};
    const ctx = AE.G.ctx;
    AE.innerShadow(path, pal.outfitShade, opt.dx == null ? 26 : opt.dx, opt.dy == null ? 0 : opt.dy, opt.a == null ? 0.8 : opt.a);
    AE.innerShadow(path, pal.outfitDeep, 0, 46, 0.32);
    ctx.save();
    ctx.clip(path);
    const rng = AE.subRng(AE.G.ch.seed, 'fold' + (opt.salt || ''));
    for (let i = 0; i < (opt.folds || 5); i++) {
      const x0 = CX + (rng() - 0.5) * (opt.span || 220);
      const y0 = (opt.y0 || 700) + rng() * 50;
      const len = (opt.foldLen || 160) * (0.55 + rng() * 0.9);
      ctx.beginPath();
      ctx.moveTo(x0, y0);
      ctx.quadraticCurveTo(x0 + (rng() - 0.5) * 44, y0 + len * 0.5, x0 + (rng() - 0.5) * 66, y0 + len);
      ctx.strokeStyle = AE.alpha(pal.outfitDeep, 0.32);
      ctx.lineWidth = 2.6;
      ctx.stroke();
    }
    ctx.restore();
  }

  function sleeve(fig, a, short, pal) {
    const ctx = AE.G.ctx;
    const pts = armPoints(fig, a.s, a.mode);
    const wS = fig.limb * 1.95, wE = fig.limb * 1.62, wW = fig.limb * 1.30;
    const poly = short
      ? (() => {
        const mx = pts.sx + (pts.ex - pts.sx) * 0.42, my = pts.sy + (pts.ey - pts.sy) * 0.42;
        return limbPoly({ sx: pts.sx, sy: pts.sy, ex: mx, ey: my, wx: mx, wy: my }, wS, wS * 1.02, wS * 0.98);
      })()
      : limbPoly(pts, wS, wE, wW);
    AE.fill(poly, pal.outfit);
    AE.innerShadow(poly, pal.outfitShade, -a.s * 14, 0, 0.6);
    AE.innerShadow(poly, pal.outfitDeep, 0, 22, 0.25);
    AE.stroke(poly, AE.alpha(pal.outfitDeep, 0.45), 2.6, 0.5);
    // 접힘선
    ctx.save();
    ctx.clip(poly);
    ctx.beginPath();
    ctx.moveTo(pts.sx + (pts.ex - pts.sx) * 0.55 - a.s * wE * 0.2, pts.sy + (pts.ey - pts.sy) * 0.55);
    ctx.quadraticCurveTo(pts.ex + a.s * wE * 0.18, pts.ey + 10, pts.wx + a.s * wW * 0.12, pts.wy - 10);
    ctx.strokeStyle = AE.alpha(pal.outfitDeep, 0.3);
    ctx.lineWidth = 3;
    ctx.stroke();
    ctx.restore();
    if (short) {
      const mx = pts.sx + (pts.ex - pts.sx) * 0.42, my = pts.sy + (pts.ey - pts.sy) * 0.42;
      ctx.save();
      ctx.beginPath();
      ctx.ellipse(mx, my, wS * 0.5, wS * 0.2, Math.atan2(pts.ey - pts.sy, pts.ex - pts.sx), 0, Math.PI * 2);
      ctx.fillStyle = pal.outfitHi;
      ctx.fill();
      ctx.strokeStyle = AE.alpha(pal.outfitDeep, 0.45);
      ctx.lineWidth = 2;
      ctx.stroke();
      ctx.restore();
    } else {
      const ang = Math.atan2(pts.wy - pts.ey, pts.wx - pts.ex);
      ctx.save();
      ctx.beginPath();
      ctx.ellipse(pts.wx - Math.cos(ang) * 16, pts.wy - Math.sin(ang) * 16, wW * 0.5, wW * 0.22, ang, 0, Math.PI * 2);
      ctx.fillStyle = pal.outfitHi;
      ctx.fill();
      ctx.strokeStyle = AE.alpha(pal.outfitDeep, 0.5);
      ctx.lineWidth = 2;
      ctx.stroke();
      ctx.restore();
    }
  }

  /* ---------------- 의상 ---------------- */
  AE.drawOutfit = function (ch, fig, arms) {
    const ctx = AE.G.ctx, P = AE.G.pal;
    const id = ch.parts.outfit;

    /* 하의 */
    if (id === 'gothic') {
      const sk = skirtPath(fig, fig.ankleY + 34, fig.hipW * 1.8, 9);
      AE.fill(sk, P.outfit);
      shadeFabric(sk, P, { folds: 9, span: fig.hipW * 2.4, y0: fig.waistY + 70, foldLen: 440, salt: 'g' });
      ctx.save();
      ctx.clip(sk);
      for (let i = 0; i < 26; i++) {
        const x = CX - fig.hipW * 1.8 + (fig.hipW * 3.6 * i) / 25;
        ctx.beginPath();
        ctx.arc(x, fig.ankleY + 30, 11, Math.PI, Math.PI * 2);
        ctx.fillStyle = AE.alpha(P.accent, 0.45);
        ctx.fill();
      }
      ctx.restore();
    } else if (id === 'cyber') {
      const pn = pantsPath(fig, fig.ankleY - 14);
      AE.fill(pn, AE.shade(P.outfit, -7, 0));
      shadeFabric(pn, P, { folds: 7, span: fig.hipW * 1.5, y0: fig.waistY + 40, foldLen: 260, salt: 'p' });
      ctx.save();
      ctx.clip(pn);
      ctx.strokeStyle = AE.alpha(P.accent, 0.9);
      ctx.lineWidth = 4;
      for (const s of [-1, 1]) {
        ctx.beginPath();
        ctx.moveTo(CX + s * fig.hipW * 0.5, fig.waistY + 40);
        ctx.lineTo(CX + s * fig.limb * 0.95, fig.ankleY - 50);
        ctx.stroke();
      }
      ctx.restore();
    } else if (id === 'hoodie') {
      const pn = pantsPath(fig, fig.hipY + (fig.kneeY - fig.hipY) * 0.34);
      AE.fill(pn, AE.shade(P.outfit, -14, -8, 10));
      shadeFabric(pn, P, { folds: 6, span: fig.hipW * 1.4, y0: fig.hipY, foldLen: 200, salt: 'h' });
    } else if (id === 'resort') {
      const sk = skirtPath(fig, fig.hipY + (fig.kneeY - fig.hipY) * 0.46, fig.hipW * 1.30, 5);
      AE.fill(sk, AE.alpha(AE.shade(P.accent, 6, -6), 0.98));
      shadeFabric(sk, P, { folds: 6, span: fig.hipW * 1.8, y0: fig.waistY, foldLen: 240, salt: 'r' });
    } else {
      const sk = skirtPath(fig, fig.hipY + (fig.kneeY - fig.hipY) * 0.52, fig.hipW * 1.46, 8);
      AE.fill(sk, id === 'sailor' ? AE.shade(P.outfit, 3, 0) : AE.shade(P.outfit, 0, -3));
      shadeFabric(sk, P, { folds: 8, span: fig.hipW * 2, y0: fig.waistY + 40, foldLen: 300, salt: 's' });
      ctx.save();
      ctx.clip(sk);
      for (let i = 0; i < 9; i++) {
        const t = (i + 0.5) / 9;
        const x0 = CX - fig.waistW + 2 * fig.waistW * t;
        const x1 = CX - fig.hipW * 1.46 + fig.hipW * 2.92 * t;
        ctx.beginPath();
        ctx.moveTo(x0, fig.waistY);
        ctx.quadraticCurveTo((x0 + x1) / 2, fig.hipY + 110, x1, fig.hipY + (fig.kneeY - fig.hipY) * 0.52);
        ctx.strokeStyle = i % 2 ? AE.alpha(P.outfitDeep, 0.5) : AE.alpha(P.outfitHi, 0.45);
        ctx.lineWidth = 10;
        ctx.stroke();
      }
      ctx.restore();
    }

    /* 소매 (상의보다 먼저 — 어깨는 상의가 덮음) */
    const shortSleeve = { sailor: true, resort: true }[id] ? true : false;
    if (shortSleeve) { for (const a of arms) sleeve(fig, a, true, P); }
    else { for (const a of arms) sleeve(fig, a, false, P); }

    /* 상의 — 어깨~힙 */
    const tp = AE.torsoPath(fig);
    AE.fill(tp, P.outfit);
    shadeFabric(tp, P, { folds: 6, span: fig.shW * 1.6, y0: fig.bustY + 40, foldLen: 200, salt: 'top', a: 0.75 });
    AE.stroke(tp, AE.alpha(P.outfitDeep, 0.5), 3.4, 0.5);

    if (id === 'sailor') {
      const col = new Path2D();
      col.moveTo(CX - fig.neckW * 1.55, fig.chinY + 80);
      col.bezierCurveTo(CX - fig.shW * 0.72, fig.shoulderY + 4, CX - fig.shW * 0.95, fig.shoulderY + 40, CX - fig.shW * 0.88, fig.shoulderY + 130);
      col.lineTo(CX - fig.ribW * 0.30, fig.bustY - 60);
      col.lineTo(CX, fig.bustY + 46);
      col.lineTo(CX + fig.ribW * 0.30, fig.bustY - 60);
      col.lineTo(CX + fig.shW * 0.88, fig.shoulderY + 130);
      col.bezierCurveTo(CX + fig.shW * 0.95, fig.shoulderY + 40, CX + fig.shW * 0.72, fig.shoulderY + 4, CX + fig.neckW * 1.55, fig.chinY + 80);
      col.closePath();
      AE.fill(col, AE.shade(P.outfit, AE.lum(P.outfit) > 60 ? -10 : 12, 0));
      AE.innerShadow(col, P.outfitDeep, 0, 28, 0.35);
      ctx.save();
      ctx.strokeStyle = AE.alpha('#ffffff', 0.92);
      ctx.lineWidth = 5;
      ctx.beginPath();
      ctx.moveTo(CX - fig.shW * 0.82, fig.shoulderY + 118);
      ctx.lineTo(CX - fig.ribW * 0.32, fig.bustY - 54);
      ctx.lineTo(CX, fig.bustY + 32);
      ctx.lineTo(CX + fig.ribW * 0.32, fig.bustY - 54);
      ctx.lineTo(CX + fig.shW * 0.82, fig.shoulderY + 118);
      ctx.stroke();
      ctx.restore();
      const kn = new Path2D();
      kn.roundRect(CX - 20, fig.bustY - 26, 40, 40, 10);
      AE.fill(kn, P.accentDeep);
      const nt = new Path2D();
      nt.moveTo(CX - 24, fig.bustY + 8);
      nt.lineTo(CX + 24, fig.bustY + 8);
      nt.lineTo(CX + 8, fig.bustY + 118);
      nt.lineTo(CX - 8, fig.bustY + 118);
      nt.closePath();
      AE.fill(nt, P.accent);
      AE.innerShadow(nt, P.accentDeep, 12, 0, 0.45);
    } else if (id === 'blazer') {
      const sh = new Path2D();
      sh.moveTo(CX - fig.neckW * 1.15, fig.chinY + 76);
      sh.lineTo(CX, fig.bustY + 10);
      sh.lineTo(CX + fig.neckW * 1.15, fig.chinY + 76);
      sh.closePath();
      AE.fill(sh, '#f8f5ef');
      const kn = new Path2D();
      kn.roundRect(CX - 15, fig.bustY - 62, 30, 28, 7);
      AE.fill(kn, P.accentDeep);
      const nt = new Path2D();
      nt.moveTo(CX - 17, fig.bustY - 36);
      nt.lineTo(CX + 17, fig.bustY - 36);
      nt.lineTo(CX + 6, fig.bustY + 54);
      nt.lineTo(CX - 6, fig.bustY + 54);
      nt.closePath();
      AE.fill(nt, P.accent);
      for (const s of [-1, 1]) {
        const lp = new Path2D();
        const x0 = CX + s * fig.neckW * 1.18;
        lp.moveTo(x0, fig.chinY + 74);
        lp.lineTo(x0 + s * fig.neckW * 0.95, fig.chinY + 150);
        lp.lineTo(CX + s * fig.neckW * 0.55, fig.bustY + 40);
        lp.lineTo(x0, fig.chinY + 120);
        lp.closePath();
        AE.fill(lp, AE.shade(P.outfit, 10, -2));
        AE.innerShadow(lp, P.outfitShade, -s * 10, 0, 0.6);
        AE.stroke(lp, AE.alpha(P.outfitDeep, 0.6), 2.6, 0.6);
        // 칼라 밴드
        const cb = new Path2D();
        cb.moveTo(CX + s * fig.neckW * 1.1, fig.chinY + 66);
        cb.lineTo(CX + s * fig.neckW * 2.3, fig.chinY + 96);
        cb.lineTo(CX + s * fig.neckW * 1.1, fig.chinY + 128);
        cb.closePath();
        AE.fill(cb, AE.shade(P.outfit, 5, -1));
        AE.stroke(cb, AE.alpha(P.outfitDeep, 0.5), 2.2, 0.5);
      }
      for (let i = 0; i < 2; i++) {
        ctx.beginPath();
        ctx.arc(CX + 8, fig.bustY + 200 + i * 56, 7, 0, Math.PI * 2);
        ctx.fillStyle = P.accentDeep;
        ctx.fill();
      }
    } else if (id === 'hoodie') {
      if (ch.parts.hoodUp) {
        const hd = new Path2D();
        hd.moveTo(CX - 196, fig.chinY + 150);
        hd.bezierCurveTo(CX - 214, fig.chinY - 70, CX + 214, fig.chinY - 70, CX + 196, fig.chinY + 150);
        hd.bezierCurveTo(CX + 120, fig.chinY + 210, CX - 120, fig.chinY + 210, CX - 196, fig.chinY + 150);
        hd.closePath();
        AE.fill(hd, AE.shade(P.outfit, -10, 0));
        AE.innerShadow(hd, P.outfitDeep, 0, 34, 0.5);
        AE.stroke(hd, AE.alpha(P.outfitDeep, 0.6), 3, 0.6);
      } else {
        const hd = new Path2D();
        hd.moveTo(CX - 156, fig.chinY + 120);
        hd.bezierCurveTo(CX - 150, fig.chinY + 240, CX + 150, fig.chinY + 240, CX + 156, fig.chinY + 120);
        hd.closePath();
        AE.fill(hd, AE.shade(P.outfit, -8, 0));
        AE.innerShadow(hd, P.outfitDeep, 0, 26, 0.45);
      }
      const nk = new Path2D();
      nk.ellipse(CX, fig.chinY + 146, fig.neckW * 2.1, 48, 0, 0, Math.PI * 2);
      AE.fill(nk, AE.shade(P.outfit, -12, 0));
      ctx.save();
      ctx.strokeStyle = AE.shade(P.outfit, 26, -12);
      ctx.lineWidth = 5;
      ctx.lineCap = 'round';
      for (const s of [-1, 1]) {
        ctx.beginPath();
        ctx.moveTo(CX + s * 22, fig.chinY + 160);
        ctx.quadraticCurveTo(CX + s * 34, fig.bustY, CX + s * 20, fig.bustY + 80);
        ctx.stroke();
      }
      ctx.restore();
      const pk = new Path2D();
      pk.moveTo(CX - 116, fig.waistY - 70);
      pk.lineTo(CX + 116, fig.waistY - 70);
      pk.lineTo(CX + 100, fig.waistY + 40);
      pk.lineTo(CX - 100, fig.waistY + 40);
      pk.closePath();
      AE.stroke(pk, AE.alpha(P.outfitDeep, 0.6), 3, 0.75);
    } else if (id === 'cyber') {
      const hl = new Path2D();
      hl.moveTo(CX - fig.neckW * 2.2, fig.chinY + 104);
      hl.lineTo(CX - fig.neckW * 1.95, fig.chinY + 6);
      hl.lineTo(CX + fig.neckW * 1.95, fig.chinY + 6);
      hl.lineTo(CX + fig.neckW * 2.2, fig.chinY + 104);
      hl.closePath();
      AE.fill(hl, AE.shade(P.outfit, -12, 0));
      AE.stroke(hl, AE.alpha(P.accent, 0.85), 3, 0.85);
      ctx.save();
      ctx.strokeStyle = AE.alpha(P.accent, 0.8);
      ctx.lineWidth = 3.4;
      ctx.beginPath();
      ctx.moveTo(CX, fig.chinY + 116);
      ctx.lineTo(CX + 8, fig.hipY - 20);
      ctx.stroke();
      for (const s of [-1, 1]) {
        ctx.strokeStyle = AE.alpha(P.accent, 0.9);
        ctx.lineWidth = 7;
        ctx.beginPath();
        ctx.moveTo(CX + s * fig.neckW * 1.7, fig.shoulderY + 4);
        ctx.quadraticCurveTo(CX + s * fig.shW * 0.8, fig.shoulderY + 18, CX + s * fig.shW * 0.98, fig.shoulderY + 62);
        ctx.stroke();
      }
      ctx.restore();
    } else if (id === 'gothic') {
      const hn = new Path2D();
      hn.moveTo(CX - fig.neckW * 1.75, fig.chinY + 16);
      hn.lineTo(CX + fig.neckW * 1.75, fig.chinY + 16);
      hn.lineTo(CX + fig.neckW * 1.85, fig.chinY + 116);
      hn.lineTo(CX - fig.neckW * 1.85, fig.chinY + 116);
      hn.closePath();
      AE.fill(hn, AE.shade(P.outfit, 5, 0));
      ctx.save();
      ctx.clip(hn);
      for (let i = 0; i < 9; i++) {
        ctx.beginPath();
        ctx.arc(CX - fig.neckW * 1.6 + i * (fig.neckW * 3.2 / 8), fig.chinY + 20, 8, 0, Math.PI * 2);
        ctx.fillStyle = AE.alpha(P.accent, 0.5);
        ctx.fill();
      }
      ctx.restore();
      for (const s of [-1, 1]) {
        const pf = new Path2D();
        pf.ellipse(CX + s * fig.shW * 0.76, fig.shoulderY + 66, fig.shW * 0.30, 88, s * 0.2, 0, Math.PI * 2);
        AE.fill(pf, AE.shade(P.outfit, 11, 0));
        AE.innerShadow(pf, P.outfitDeep, s * 22, 22, 0.45);
      }
      ctx.save();
      for (let i = 0; i < 6; i++) {
        const yy = fig.waistY - 150 + i * 50;
        ctx.strokeStyle = AE.alpha(P.accent, 0.45);
        ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(CX - fig.waistW * 0.52, yy);
        ctx.lineTo(CX + fig.waistW * 0.52, yy + 14);
        ctx.stroke();
        ctx.beginPath();
        ctx.moveTo(CX + fig.waistW * 0.52, yy);
        ctx.lineTo(CX - fig.waistW * 0.52, yy + 14);
        ctx.stroke();
      }
      ctx.restore();
    } else if (id === 'resort') {
      ctx.save();
      ctx.beginPath();
      ctx.moveTo(CX - fig.ribW * 1.02, fig.chinY + 152);
      ctx.bezierCurveTo(CX - fig.ribW * 0.92, fig.bustY - 40, CX + fig.ribW * 0.92, fig.bustY - 40, CX + fig.ribW * 1.02, fig.chinY + 152);
      ctx.lineTo(CX + fig.ribW * 0.92, fig.bustY + 70);
      ctx.lineTo(CX - fig.ribW * 0.92, fig.bustY + 70);
      ctx.closePath();
      const g = ctx.createLinearGradient(0, fig.chinY + 120, 0, fig.bustY + 70);
      g.addColorStop(0, P.outfit);
      g.addColorStop(1, AE.shade(P.outfit, -10, 2));
      ctx.fillStyle = g;
      ctx.fill();
      ctx.restore();
      for (const s of [-1, 1]) {
        AE.softBlob(CX + s * fig.ribW * 0.40, fig.bustY + 20, fig.ribW * 0.36, 20 + 22 * fig.bustProj, P.outfitDeep, 0.35);
      }
      ctx.save();
      ctx.strokeStyle = AE.shade(P.outfit, 14, -6);
      ctx.lineWidth = 7;
      for (const s of [-1, 1]) {
        ctx.beginPath();
        ctx.moveTo(CX + s * fig.ribW * 0.72, fig.chinY + 162);
        ctx.lineTo(CX + s * fig.shW * 0.52, fig.shoulderY + 14);
        ctx.stroke();
      }
      ctx.restore();
    }

    /* 신발 */
    for (const s of [-1, 1]) {
      const f = footPath(fig, s);
      const shoeCol = id === 'gothic' || id === 'cyber' ? AE.shade(P.outfit, -16, 0) : '#2b2c34';
      AE.fill(f, shoeCol);
      AE.innerShadow(f, '#0d0e13', 0, 20, 0.5);
      ctx.save();
      ctx.clip(f);
      ctx.fillStyle = AE.alpha('#ffffff', 0.16);
      ctx.beginPath();
      ctx.ellipse(CX + s * fig.hipW * 0.36 - s * 8, fig.soleY - 30, 42, 12, 0, 0, Math.PI * 2);
      ctx.fill();
      ctx.restore();
      if (id === 'sailor' || id === 'blazer') {
        ctx.save();
        ctx.fillStyle = '#f8f6f3';
        ctx.beginPath();
        ctx.ellipse(CX + s * fig.hipW * 0.36, fig.ankleY - 16, fig.limb * 0.94, 28, 0, 0, Math.PI * 2);
        ctx.fill();
        ctx.restore();
      }
    }
  };
})();
