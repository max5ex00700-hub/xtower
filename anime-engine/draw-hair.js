/* ============================================================
   draw-hair.js — 4파트 헤어 (뒤/옆/앞/삐침) + 투톤 + 수인귀
   헤드 스페이스: 정수리 y=190, 턱 y≈580, 얼굴 중심 x=512
   ============================================================ */
(function () {
  const AE = (window.AE = window.AE || {});
  const CX = 512;
  const P = () => new Path2D();

  function skull() { return { cx: CX, cy: 398, rx: 172, ry: 206 }; }
  AE.skull = skull;

  /* ------------ 헬퍼 ------------ */
  function lock(rootX, rootY, tipX, tipY, w, bowX, bowY) {
    const p = P();
    p.moveTo(rootX - w * 0.5, rootY);
    p.quadraticCurveTo(bowX - w * 0.30, bowY, tipX, tipY);
    p.quadraticCurveTo(bowX + w * 0.40, bowY, rootX + w * 0.5, rootY);
    p.closePath();
    return p;
  }
  AE.lock = lock;
  /** 사다리꼴 가닥: 뿌리는 넓고 끝은 좁은 짧은 변 — 이웃과 겹쳐 이어진다 */
  function lockQuad(rootX, rootY, tipX, tipY, w0, w1, bowX, bowY) {
    const p = P();
    p.moveTo(rootX - w0 * 0.5, rootY);
    p.quadraticCurveTo(bowX - w0 * 0.30, bowY, tipX - w1 * 0.5, tipY);
    p.quadraticCurveTo(tipX, tipY + w1 * 0.55, tipX + w1 * 0.5, tipY);
    p.quadraticCurveTo(bowX + w0 * 0.42, bowY, rootX + w0 * 0.5, rootY);
    p.closePath();
    return p;
  }


  function hairPaint(p, pal, o) {
    o = o || {};
    const ctx = AE.G.ctx;
    const g = AE.linear(0, o.top || 190, 0, o.bottom || 900, [
      [0, pal.hairHi],
      [o.mid == null ? 0.18 : o.mid, pal.hair],
      [1, o.inner || pal.hair],
    ]);
    AE.fill(p, g);
    AE.innerShadow(p, pal.hairShade, o.sx == null ? 8 : o.sx, o.sy == null ? 12 : o.sy, o.a == null ? 0.65 : o.a);
    if (o.edge !== false) AE.stroke(p, AE.alpha(pal.hairLine, 0.75), 3.2, 0.8);
  }

  function strandLines(p, pal, n, seedSalt, y0, y1, spread) {
    const ctx = AE.G.ctx;
    const rng = AE.subRng(AE.G.ch.seed, seedSalt);
    ctx.save();
    ctx.clip(p);
    for (let i = 0; i < n; i++) {
      const x0 = CX + (rng() - 0.5) * (spread || 260);
      const x1 = x0 + (rng() - 0.5) * 70;
      ctx.beginPath();
      ctx.moveTo(x0, y0 + rng() * 60);
      ctx.quadraticCurveTo(x0 + (rng() - 0.5) * 50, (y0 + y1) / 2, x1, y1 - rng() * 120);
      ctx.strokeStyle = AE.alpha(pal.hairLine, 0.18 + rng() * 0.16);
      ctx.lineWidth = 2 + rng() * 3;
      ctx.stroke();
    }
    ctx.restore();
  }

  function gloss(p, pal, cy, rx, ry, w) {
    const ctx = AE.G.ctx;
    ctx.save();
    ctx.clip(p);
    const arc = P();
    arc.ellipse(CX, cy, rx, ry, 0, Math.PI * 1.08, Math.PI * 1.92);
    ctx.strokeStyle = AE.alpha(pal.hairHi, 0.45);
    ctx.lineWidth = w || 16;
    ctx.lineCap = 'round';
    ctx.stroke(arc);
    ctx.strokeStyle = AE.alpha('#ffffff', 0.18);
    ctx.lineWidth = (w || 16) * 0.34;
    ctx.stroke(arc);
    ctx.restore();
  }

  /* ============================================================
     뒤머리
     ============================================================ */
  AE.drawHairBack = function () {
    const ch = AE.G.ch, pal = AE.G.pal, ctx = AE.G.ctx;
    if (!ch.parts.hairVisible) return;
    const kind = ch.parts.hairBack;
    const two = ch.parts.twoTone;

    function massGrad(bottom) {
      const g = ctx.createLinearGradient(0, 210, 0, bottom);
      g.addColorStop(0, pal.hairHi);
      g.addColorStop(0.16, pal.hair);
      g.addColorStop(0.55, two ? pal.hair : AE.shade(pal.hair, -4, 0));
      g.addColorStop(0.72, two ? pal.hairInner : pal.hairShade);
      g.addColorStop(1, two ? AE.shade(pal.hairInner, -10, 2) : pal.hairShade);
      return g;
    }

    function paint(p, bottom, salt) {
      AE.fill(p, massGrad(bottom));
      // 좌우 끝단 어둡게
      AE.innerShadow(p, pal.hairShade, 34, 0, 0.55);
      AE.innerShadow(p, pal.hairShade, -34, 0, 0.4);
      AE.innerShadow(p, pal.hairHi, -22, 0, 0.30);
      AE.innerShadow(p, pal.hairDeep, 0, 60, 0.3);
      strandLines(p, pal, 13, 'hb' + (salt || 0), 300, bottom - 80, 300);
      gloss(p, pal, 292, 152, 62, 16);
      AE.innerShadow(p, pal.hairDeep, 0, 90, 0.35);
      AE.stroke(p, AE.alpha(pal.hairLine, 0.6), 3.4, 0.7);
    }

    /* 긴 실루엣: 옆선에 잔물결을 주고 끝단을 뾰족하게 */
    function longMass(bottomY, hTop, hMid, hBot, wob, tipN, tipDepth) {
      const p = P();
      const topY = 208;
      p.moveTo(CX - hTop, topY + 46);
      // 왼쪽 라인
      p.bezierCurveTo(CX - hTop * 1.16, topY + 180, CX - hMid, topY + 520, CX - hMid * 1.02, topY + 780);
      p.bezierCurveTo(CX - hMid * 1.04 - wob, topY + 980, CX - hBot * 0.98, bottomY - 320, CX - hBot, bottomY - 150);
      let x = CX - hBot, y = bottomY - 150;
      for (let i = 0; i < tipN; i++) {
        const tx = CX - hBot + (hBot * 2) * ((i + 0.5) / tipN);
        const ty = bottomY - (i % 2 ? tipDepth * 0.35 : tipDepth);
        p.quadraticCurveTo(x + (tx - x) * 0.35, y + 70, tx, ty);
        const px = CX - hBot + (hBot * 2) * ((i + 1) / tipN);
        p.quadraticCurveTo(tx + (px - tx) * 0.62, ty - 50, px, y + (i % 2 ? 24 : -12));
        x = px;
        y = bottomY - (i % 2 ? tipDepth * 0.35 : tipDepth);
      }
      p.lineTo(CX + hBot, bottomY - 150);
      p.bezierCurveTo(CX + hBot * 0.98, bottomY - 320, CX + hMid * 1.04 + wob, topY + 980, CX + hMid * 1.02, topY + 780);
      p.bezierCurveTo(CX + hMid, topY + 520, CX + hTop * 1.16, topY + 180, CX + hTop, topY + 46);
      p.bezierCurveTo(CX + hTop * 0.6, topY - 24, CX - hTop * 0.6, topY - 24, CX - hTop, topY + 46);
      p.closePath();
      return p;
    }

    ctx.save();
    if (kind === 'long_straight') {
      paint(longMass(1740, 148, 196, 212, 16, 5, 120), 1740, 1);
    } else if (kind === 'wavy') {
      const p = P();
      const topY = 208, bottom = 1660;
      p.moveTo(CX - 160, topY + 46);
      let prev = [CX - 160, topY + 46];
      for (let i = 0; i < 5; i++) {
        const t0 = i / 5, t1 = (i + 1) / 5;
        const y0 = topY + 46 + (bottom - topY) * t0, y1 = topY + 46 + (bottom - topY) * t1;
        const amp = 20 + 34 * t1;
        p.bezierCurveTo(CX - 200 - amp, y0 + (y1 - y0) * 0.3, CX - 196 + amp, y0 + (y1 - y0) * 0.7, CX - 206, y1);
        prev = [CX - 206, y1];
      }
      for (let i = 0; i < 5; i++) {
        const x0 = CX - 206 + (412 * (i + 0.5)) / 5;
        p.quadraticCurveTo(x0, bottom + 96, CX - 206 + (412 * (i + 1)) / 5, bottom - (i % 2 ? 30 : 0));
      }
      for (let i = 5; i > 0; i--) {
        const t0 = i / 5, t1 = (i - 1) / 5;
        const y0 = topY + 46 + (bottom - topY) * t0, y1 = topY + 46 + (bottom - topY) * t1;
        const amp = 20 + 34 * t1;
        p.bezierCurveTo(CX + 196 - amp, y0 - (y0 - y1) * 0.3, CX + 200 + amp, y0 - (y0 - y1) * 0.7, CX + 206, y1);
      }
      p.bezierCurveTo(CX + 160 * 0.6, topY - 24, CX - 160 * 0.6, topY - 24, CX - 160, topY + 46);
      p.closePath();
      paint(p, 1660, 2);
    } else if (kind === 'twin_tails') {
      paint(longMass(760, 158, 186, 196, 10, 4, 70), 760, 3);
      for (const s of [-1, 1]) {
        const p = P();
        const bx = CX + s * 152, by = 336;
        p.moveTo(bx - s * 10, by - 44);
        p.bezierCurveTo(CX + s * 268, by + 10, CX + s * 318, by + 240, CX + s * 306, by + 470);
        p.bezierCurveTo(CX + s * 298, by + 640, CX + s * 272, by + 790, CX + s * 244, by + 880);
        p.bezierCurveTo(CX + s * 232, by + 760, CX + s * 214, by + 650, CX + s * 206, by + 560);
        p.bezierCurveTo(CX + s * 196, by + 430, CX + s * 196, by + 230, CX + s * 128, by + 110);
        p.closePath();
        paint(p, by + 880, 4 + s);
        // 묶음
        const tie = P();
        tie.ellipse(bx + s * 6, by + 4, 20, 28, s * 0.3, 0, Math.PI * 2);
        AE.fill(tie, pal.hairDeep);
        AE.stroke(tie, AE.alpha(pal.hairLine, 0.7), 2.4, 0.6);
      }
    } else if (kind === 'ponytail') {
      paint(longMass(680, 158, 180, 190, 8, 4, 60), 680, 5);
      const p = P();
      p.moveTo(CX + 30, 244);
      p.bezierCurveTo(CX + 176, 226, CX + 282, 356, CX + 296, 540);
      p.bezierCurveTo(CX + 310, 736, CX + 282, 960, CX + 240, 1110);
      p.bezierCurveTo(CX + 254, 950, CX + 236, 780, CX + 222, 630);
      p.bezierCurveTo(CX + 206, 470, CX + 168, 366, CX + 50, 296);
      p.closePath();
      paint(p, 1110, 6);
      const tie = P();
      tie.ellipse(CX + 78, 288, 26, 30, -0.4, 0, Math.PI * 2);
      AE.fill(tie, pal.hairDeep);
    } else if (kind === 'bob') {
      const p = P();
      p.moveTo(CX - 158, 250);
      p.bezierCurveTo(CX - 190, 400, CX - 190, 540, CX - 180, 636);
      for (let i = 0; i < 5; i++) {
        const x0 = CX - 180 + (360 * (i + 0.5)) / 5;
        p.quadraticCurveTo(x0, 700 - i * 8, CX - 180 + (360 * (i + 1)) / 5, 636 - (i % 2 ? 26 : 0));
      }
      p.bezierCurveTo(CX + 190, 540, CX + 190, 400, CX + 158, 250);
      p.bezierCurveTo(CX + 96, 186, CX - 96, 186, CX - 158, 250);
      p.closePath();
      paint(p, 706, 7);
    } else if (kind === 'braid') {
      paint(longMass(720, 158, 180, 190, 8, 4, 60), 720, 8);
      const bx = CX + 168;
      for (let i = 0; i < 7; i++) {
        const y = 520 + i * 88;
        const w = 42 - i * 3;
        const seg = P();
        seg.ellipse(bx + Math.sin(i * 1.2) * 10, y, w, 44, 0.24, 0, Math.PI * 2);
        AE.fill(seg, i % 2 ? pal.hair : pal.hairShade);
        AE.innerShadow(seg, pal.hairDeep, 8, 6, 0.5);
        AE.stroke(seg, AE.alpha(pal.hairLine, 0.45), 2, 0.6);
      }
      const tip = P();
      tip.moveTo(bx - 18, 1120);
      tip.quadraticCurveTo(bx + 16, 1180, bx + 2, 1242);
      tip.quadraticCurveTo(bx - 10, 1180, bx - 18, 1120);
      AE.fill(tip, pal.hairShade);
    } else if (kind === 'short') {
      const p = P();
      p.moveTo(CX - 156, 262);
      p.bezierCurveTo(CX - 176, 372, CX - 172, 462, CX - 160, 512);
      for (let i = 0; i < 6; i++) {
        const x0 = CX - 160 + (320 * (i + 0.5)) / 6;
        p.quadraticCurveTo(x0, 546 + (i % 2 ? 22 : 0), CX - 160 + (320 * (i + 1)) / 6, 508 + (i % 2 ? 8 : 0));
      }
      p.bezierCurveTo(CX + 172, 462, CX + 176, 372, CX + 156, 262);
      p.bezierCurveTo(CX + 96, 194, CX - 96, 194, CX - 156, 262);
      p.closePath();
      paint(p, 560, 9);
    }
    ctx.restore();
  };

  /* ============================================================
     머리 덮개 (정수리)
     ============================================================ */
  AE.drawHairCap = function () {
    const pal = AE.G.pal, ctx = AE.G.ctx;
    const p = P();
    p.moveTo(CX - 170, 400);
    p.bezierCurveTo(CX - 180, 268, CX - 118, 190, CX, 184);
    p.bezierCurveTo(CX + 118, 190, CX + 180, 268, CX + 170, 400);
    p.lineTo(CX + 150, 300);
    p.bezierCurveTo(CX + 92, 246, CX - 92, 246, CX - 150, 300);
    p.closePath();
    const g = ctx.createLinearGradient(0, 184, 0, 410);
    g.addColorStop(0, pal.hairHi);
    g.addColorStop(0.35, pal.hair);
    g.addColorStop(1, pal.hairShade);
    AE.fill(p, g);
    gloss(p, pal, 318, 146, 76, 14);
  };

  /* ============================================================
     앞머리 — 덩어리 + 겹친 가닥
     ============================================================ */
  function bangsMass(kind) {
    const p = P();
    const top = 200;
    // 좌측 관자 → 정수리 → 우측
    p.moveTo(CX - 170, 372);
    p.bezierCurveTo(CX - 176, 266, CX - 118, top, CX, top - 6);
    p.bezierCurveTo(CX + 118, top, CX + 176, 266, CX + 170, 372);

    // 아래 끝단 (오른쪽 → 왼쪽)
    const tips = [];
    const N = 8;
    const baseY = { blunt: 352, hime: 356, parted: 340, side_swept: 330, choppy: 336, curtain: 322, see_through: 318 }[kind] || 340;
    for (let i = 0; i < N; i++) {
      const t = (i + 0.5) / N;
      let y = baseY + Math.sin(t * Math.PI) * 10 + (i % 2 ? 12 : 0);
      if (kind === 'parted') y += Math.abs(t - 0.42) * 44;
      if (kind === 'side_swept') y += t * 46;
      if (kind === 'curtain') y += Math.abs(t - 0.5) * 52;
      if (kind === 'choppy') y += (i * 29) % 40;
      tips.push({ x: CX + 170 - 340 * t, y });
    }
    // 오른쪽에서 왼쪽으로 훑기
    let prev = [CX + 170, 372];
    for (const tp of tips) {
      const mx = (prev[0] + tp.x) / 2;
      p.quadraticCurveTo(mx, (prev[1] + tp.y) / 2 - 26, tp.x, tp.y);
      prev = [tp.x, tp.y];
    }
    p.quadraticCurveTo((prev[0] + CX - 170) / 2, (prev[1] + 372) / 2 - 20, CX - 170, 372);
    p.closePath();
    return p;
  }

  AE.drawHairFront = function () {
    const ch = AE.G.ch, pal = AE.G.pal, ctx = AE.G.ctx;
    const kind = ch.parts.hairFront;
    const rng = AE.subRng(ch.seed, 'bangs');
    ctx.save();

    /* 1) 뿌리 덩어리 — 이마 위쪽만 덮음 */
    const mass = P();
    mass.moveTo(CX - 172, 344);
    mass.bezierCurveTo(CX - 178, 254, CX - 118, 196, CX, 190);
    mass.bezierCurveTo(CX + 118, 196, CX + 178, 254, CX + 172, 344);
    // 아래 가장자리: 완만한 곡선
    mass.quadraticCurveTo(CX + 90, 356, CX + 10, 330);
    mass.quadraticCurveTo(CX - 80, 306, CX - 172, 344);
    mass.closePath();
    const g = ctx.createLinearGradient(0, 186, 0, 360);
    g.addColorStop(0, pal.hairHi);
    g.addColorStop(0.30, pal.hair);
    g.addColorStop(1, AE.shade(pal.hair, -3, 0));
    AE.fill(mass, g);
    gloss(mass, pal, 296, 140, 70, 13);
    AE.innerShadow(mass, pal.hairShade, 0, 22, 0.3);

    /* 2) 앞머리 가닥 — 넓은 사다리꼴이 겹쳐 이어지는 형태 */
    const baseY = {
      blunt: 356, hime: 362, parted: 348, side_swept: 336,
      choppy: 344, curtain: 328, see_through: 320,
    }[kind] || 384;
    const N = { blunt: 7, hime: 7, parted: 7, side_swept: 6, choppy: 7, curtain: 7, see_through: 6 }[kind] || 7;

    for (let i = 0; i < N; i++) {
      const t = (i + 0.5) / N;
      const rr = AE.subRng(ch.seed, 'bang' + i)();
      let rx = CX + (t - 0.5) * 238;
      let tx = CX + (t - 0.5) * 322;
      let ty = baseY + (rr - 0.5) * 16;
      let w0 = 96, w1 = 26;
      let bx = (rx + tx) / 2, by = 276 + rr * 18;

      if (kind === 'parted') {
        const sgn = t < 0.34 ? -1 : 1;
        rx = CX - 26 + sgn * Math.abs(t - 0.34) * 210;
        tx = CX - 36 + sgn * Math.abs(t - 0.34) * 330;
        ty = baseY + Math.abs(t - 0.34) * 30;
        bx = rx + sgn * 26;
        w0 = 92; w1 = 24;
      } else if (kind === 'side_swept') {
        rx = CX - 120 + t * 230;
        tx = CX - 40 + t * 330;
        ty = baseY + t * 40;
        bx = rx + 40;
        w0 = 104; w1 = 28;
      } else if (kind === 'choppy') {
        ty = baseY + ((i * 31) % 48) - 12;
        w0 = 88 + rr * 20; w1 = 22;
      } else if (kind === 'curtain') {
        const sgn = t < 0.5 ? -1 : 1;
        rx = CX + sgn * Math.abs(t - 0.5) * 210;
        tx = CX + sgn * Math.abs(t - 0.5) * 340;
        ty = baseY + Math.abs(t - 0.5) * 52;
        bx = rx + sgn * 40;
        w0 = 96; w1 = 26;
      } else if (kind === 'hime') {
        ty = baseY + (rr - 0.5) * 8;
        w0 = 96; w1 = 30;
      } else if (kind === 'see_through') {
        ty = baseY + (i % 2 ? 40 : 0);
        w0 = 74; w1 = 18;
      }
      const p = lockQuad(rx, 214, tx, ty, w0, w1, bx, by);
      const gg = ctx.createLinearGradient(0, 200, 0, ty + 30);
      gg.addColorStop(0, pal.hairHi);
      gg.addColorStop(0.20, pal.hair);
      gg.addColorStop(1, AE.shade(pal.hair, -2, 0));
      AE.fill(p, gg);
      AE.innerShadow(p, pal.hairShade, 15, 12, 0.45);
      AE.stroke(p, AE.alpha(pal.hairLine, 0.55), 3, 0.75);
      // 가닥 하이라이트
      ctx.save();
      ctx.clip(p);
      ctx.beginPath();
      ctx.moveTo(rx - w0 * 0.16, 224);
      ctx.quadraticCurveTo(bx - w0 * 0.22, by + 32, tx - 4, ty - 10);
      ctx.strokeStyle = AE.alpha(pal.hairHi, 0.4);
      ctx.lineWidth = 4;
      ctx.stroke();
      ctx.restore();
    }
    ctx.restore();
  };

  /* ============================================================
     옆머리
     ============================================================ */
  AE.drawHairSide = function () {
    const ch = AE.G.ch, pal = AE.G.pal, ctx = AE.G.ctx;
    const kind = ch.parts.hairSide;
    const two = ch.parts.twoTone;
    ctx.save();
    function side(s) {
      const x = CX + s * 146;
      if (kind === 'short') {
        const p = lock(x, 268, x + s * 30, 512, 64, x + s * 46, 380);
        hairPaint(p, pal, { top: 240, bottom: 530 });
      } else if (kind === 'long') {
        const p = lock(x - s * 4, 262, x + s * 30, 860, 74, x + s * 44, 500);
        hairPaint(p, pal, { top: 240, bottom: 880, inner: two ? pal.hairInner : null });
        const p2 = lock(x - s * 26, 258, x - s * 2, 700, 48, x - s * 8, 440);
        hairPaint(p2, pal, { top: 236, bottom: 720 });
      } else if (kind === 'curl') {
        const p = P();
        p.moveTo(x, 264);
        p.bezierCurveTo(x + s * 74, 380, x + s * 98, 500, x + s * 62, 606);
        p.bezierCurveTo(x + s * 36, 688, x + s * 78, 726, x + s * 104, 682);
        p.bezierCurveTo(x + s * 48, 648, x + s * 30, 588, x + s * 46, 508);
        p.bezierCurveTo(x + s * 56, 412, x + s * 32, 340, x + s * 6, 272);
        p.closePath();
        hairPaint(p, pal, { top: 240, bottom: 740 });
      } else if (kind === 'braid') {
        const p = lock(x, 272, x + s * 18, 620, 52, x + s * 40, 420);
        hairPaint(p, pal, { top: 240, bottom: 640 });
        for (let i = 0; i < 5; i++) {
          const bp = P();
          bp.ellipse(x + s * 20, 468 + i * 44, 20 - i * 2, 25, s * 0.2, 0, Math.PI * 2);
          AE.fill(bp, i % 2 ? pal.hair : pal.hairShade);
          AE.stroke(bp, AE.alpha(pal.hairLine, 0.35), 2, 0.5);
        }
      } else if (kind === 'spiky') {
        for (let i = 0; i < 3; i++) {
          const p = lock(x - s * 8 + i * s * 12, 266 + i * 12, x + s * (50 + i * 24), 452 + i * 84, 46, x + s * 44, 366 + i * 56);
          hairPaint(p, pal, { top: 240, bottom: 500 + i * 84 });
        }
      }
    }
    side(-1);
    side(1);
    ctx.restore();
  };

  /* ============================================================
     삐침
     ============================================================ */
  AE.drawAhoge = function () {
    const ch = AE.G.ch, pal = AE.G.pal, ctx = AE.G.ctx;
    const kind = ch.parts.ahoge;
    if (kind === 'none') return;
    const rng = AE.subRng(ch.seed, 'ahoge');
    const dir = rng() > 0.5 ? 1 : -1;
    ctx.save();
    function spike(x0, y0, len, bend, w) {
      const p = P();
      p.moveTo(x0 - w, y0 + 8);
      p.bezierCurveTo(x0 + bend * 0.35, y0 - len * 0.66, x0 + bend * 0.9, y0 - len, x0 + bend * 1.16, y0 - len * 1.12);
      p.bezierCurveTo(x0 + bend * 0.55, y0 - len * 0.72, x0 + w * 0.45 + bend * 0.2, y0 - len * 0.36, x0 + w, y0 + 8);
      p.closePath();
      return p;
    }
    if (kind === 'single') {
      const p = spike(CX + dir * 8, 200, 116, dir * 54, 16);
      hairPaint(p, pal, { top: 90, bottom: 220, edge: false });
      AE.stroke(p, AE.alpha(pal.hairLine, 0.45), 2, 0.6);
    } else if (kind === 'double') {
      for (const s of [-1, 1]) {
        const p = spike(CX + s * 14, 200, 100 + (s > 0 ? 20 : 0), s * 62, 15);
        hairPaint(p, pal, { top: 90, bottom: 220, edge: false });
        AE.stroke(p, AE.alpha(pal.hairLine, 0.42), 2, 0.6);
      }
    } else if (kind === 'curl') {
      const p = P();
      p.moveTo(CX - 14, 206);
      p.bezierCurveTo(CX + 28, 142, CX + 96, 112, CX + 82, 162);
      p.bezierCurveTo(CX + 72, 196, CX + 36, 206, CX + 24, 228);
      p.bezierCurveTo(CX + 14, 214, CX, 206, CX - 14, 206);
      p.closePath();
      hairPaint(p, pal, { top: 90, bottom: 240, edge: false });
      AE.stroke(p, AE.alpha(pal.hairLine, 0.42), 2, 0.6);
    }
    ctx.restore();
  };

  /* ============================================================
     수인귀 (인간귀/엘프귀는 core에서)
     ============================================================ */
  AE.drawAnimalEars = function () {
    const ch = AE.G.ch, pal = AE.G.pal, ctx = AE.G.ctx;
    const kind = ch.parts.ears;
    if (kind === 'human' || kind === 'elf') return;
    const cfg = {
      cat: { w: 74, h: 118, tilt: 0.30, base: 236, off: 104, inner: 0.8 },
      fox: { w: 84, h: 164, tilt: 0.22, base: 232, off: 112, inner: 0.72 },
      rabbit: { w: 60, h: 210, tilt: 0.12, base: 240, off: 88, inner: 0.9 },
    }[kind];
    ctx.save();
    for (const s of [-1, 1]) {
      const bx = CX + s * cfg.off;
      const by = cfg.base;
      const w = cfg.w, h = cfg.h;
      ctx.save();
      ctx.translate(bx, by);
      ctx.rotate(s * cfg.tilt * 0.5);
      // 귀 실루엣: 밑동에서 안쪽으로 휘어 올라가는 뾰족한 형태
      const p = P();
      p.moveTo(-w * 0.52, 26);
      p.bezierCurveTo(-w * 0.46, -h * 0.42, -w * 0.20, -h * 0.92, s * h * 0.10, -h);
      p.bezierCurveTo(w * 0.22, -h * 0.86, w * 0.50, -h * 0.34, w * 0.52, 22);
      p.closePath();
      const g = ctx.createLinearGradient(-w * 0.4, -h, w * 0.5, 30);
      g.addColorStop(0, pal.hairHi);
      g.addColorStop(0.5, pal.hair);
      g.addColorStop(1, pal.hairShade);
      AE.fill(p, g);
      AE.innerShadow(p, pal.hairShade, 10, 10, 0.5);
      AE.stroke(p, AE.alpha(pal.hairLine, 0.5), 2.6, 0.75);
      // 안쪽 귀
      const inner = P();
      inner.moveTo(-w * 0.26, 16);
      inner.bezierCurveTo(-w * 0.24, -h * 0.38, -w * 0.08, -h * 0.78, s * h * 0.06, -h * 0.84);
      inner.bezierCurveTo(w * 0.16, -h * 0.72, w * 0.26, -h * 0.30, w * 0.26, 12);
      inner.closePath();
      AE.fill(inner, AE.alpha(AE.mix(pal.blush, '#ffffff', 0.55), cfg.inner * 0.85));
      // 솜털
      ctx.save();
      ctx.clip(inner);
      ctx.strokeStyle = AE.alpha('#ffffff', 0.22);
      ctx.lineWidth = 1.4;
      for (let i = 0; i < 4; i++) {
        ctx.beginPath();
        ctx.moveTo(-w * 0.08, 0 - i * 26);
        ctx.lineTo(w * 0.10, -14 - i * 26);
        ctx.stroke();
      }
      ctx.restore();
      ctx.restore();
    }
    ctx.restore();
  };

  AE.drawElfEars = function () {
    const ch = AE.G.ch, pal = AE.G.pal, ctx = AE.G.ctx;
    if (ch.parts.ears !== 'elf') return;
    ctx.save();
    for (const s of [-1, 1]) {
      const p = new Path2D();
      const x = CX + s * 146, y = 438;
      p.moveTo(x - s * 6, y - 26);
      p.bezierCurveTo(x + s * 52, y - 54, x + s * 100, y - 72, x + s * 112, y - 40);
      p.bezierCurveTo(x + s * 94, y - 8, x + s * 44, y + 12, x - s * 4, y + 24);
      p.closePath();
      AE.fill(p, pal.skinMid);
      AE.innerShadow(p, pal.skinDeep, -s * 10, 6, 0.5);
      const inner = new Path2D();
      inner.moveTo(x + s * 12, y - 12);
      inner.bezierCurveTo(x + s * 54, y - 36, x + s * 84, y - 46, x + s * 88, y - 32);
      inner.bezierCurveTo(x + s * 72, y - 8, x + s * 40, y, x + s * 14, y + 6);
      inner.closePath();
      AE.fill(inner, AE.alpha(pal.blush, 0.5));
      AE.stroke(p, AE.alpha(pal.skinDeep, 0.6), 2.4, 0.6);
    }
    ctx.restore();
  };
})();
