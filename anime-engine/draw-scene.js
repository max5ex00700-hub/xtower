/* ============================================================
   draw-scene.js — 배경·오라·오버레이·렌더 오케스트레이션
   ============================================================ */
(function () {
  const AE = (window.AE = window.AE || {});
  const W = 1024, H = 1280, CX = 512;
  AE.W = W; AE.H = H;

  /* ---------------- 배경 ---------------- */
  function bgBase(ch, pal) {
    const ctx = AE.G.ctx;
    ctx.fillStyle = '#0b0d14';
    ctx.fillRect(0, 0, W, H);
  }

  const BGS = {
    classroom(ctx, pal, ch) {
      let g = ctx.createLinearGradient(0, 0, 0, H);
      g.addColorStop(0, '#dfe8f2');
      g.addColorStop(0.45, '#f2ead9');
      g.addColorStop(1, '#cbb9a6');
      ctx.fillStyle = g;
      ctx.fillRect(0, 0, W, H);
      // 창문
      ctx.save();
      ctx.fillStyle = 'rgba(190,224,248,0.95)';
      ctx.fillRect(80, 90, 420, 720);
      const sky = ctx.createLinearGradient(0, 90, 0, 810);
      sky.addColorStop(0, '#bfe4ff');
      sky.addColorStop(0.6, '#e8f4ff');
      sky.addColorStop(1, '#fdf3df');
      ctx.fillStyle = sky;
      ctx.fillRect(88, 98, 404, 704);
      // 창틀
      ctx.strokeStyle = 'rgba(255,255,255,0.9)';
      ctx.lineWidth = 14;
      ctx.strokeRect(88, 98, 404, 704);
      ctx.lineWidth = 10;
      ctx.beginPath();
      ctx.moveTo(290, 98);
      ctx.lineTo(290, 802);
      ctx.moveTo(88, 380);
      ctx.lineTo(492, 380);
      ctx.stroke();
      // 빛줄기
      ctx.globalCompositeOperation = 'screen';
      for (let i = 0; i < 4; i++) {
        const lg = ctx.createLinearGradient(120 + i * 90, 120, 420 + i * 120, 1180);
        lg.addColorStop(0, 'rgba(255,245,210,0.5)');
        lg.addColorStop(1, 'rgba(255,245,210,0)');
        ctx.fillStyle = lg;
        ctx.beginPath();
        ctx.moveTo(120 + i * 90, 120);
        ctx.lineTo(200 + i * 90, 120);
        ctx.lineTo(620 + i * 130, 1180);
        ctx.lineTo(500 + i * 130, 1180);
        ctx.closePath();
        ctx.fill();
      }
      ctx.restore();
      // 칠판 / 벽 소품
      ctx.fillStyle = 'rgba(46,72,64,0.18)';
      ctx.fillRect(620, 140, 340, 420);
      ctx.fillStyle = 'rgba(255,255,255,0.25)';
      ctx.fillRect(620, 140, 340, 8);
      // 바닥
      ctx.fillStyle = 'rgba(120,96,74,0.28)';
      ctx.fillRect(0, 1010, W, 270);
      bokeh(ctx, pal, ch, 16, 0.35, 0.9);
    },
    forest(ctx, pal, ch) {
      let g = ctx.createLinearGradient(0, 0, 0, H);
      g.addColorStop(0, '#1d3b32');
      g.addColorStop(0.5, '#28503f');
      g.addColorStop(1, '#0e1c18');
      ctx.fillStyle = g;
      ctx.fillRect(0, 0, W, H);
      // 나무 기둥
      for (let i = 0; i < 7; i++) {
        const x = 40 + i * 155 + (i % 2) * 40;
        const w = 26 + (i % 3) * 16;
        ctx.fillStyle = `rgba(10,24,20,${0.35 + (i % 3) * 0.12})`;
        ctx.beginPath();
        ctx.moveTo(x, 0);
        ctx.lineTo(x + w, 0);
        ctx.lineTo(x + w * 0.7, H);
        ctx.lineTo(x + w * 0.1, H);
        ctx.closePath();
        ctx.fill();
      }
      // 빛줄기
      ctx.globalCompositeOperation = 'screen';
      for (let i = 0; i < 6; i++) {
        const lg = ctx.createLinearGradient(200 + i * 110, 0, 380 + i * 120, H);
        lg.addColorStop(0, 'rgba(190,255,200,0.28)');
        lg.addColorStop(1, 'rgba(190,255,200,0)');
        ctx.fillStyle = lg;
        ctx.beginPath();
        ctx.moveTo(180 + i * 110, 0);
        ctx.lineTo(260 + i * 110, 0);
        ctx.lineTo(560 + i * 130, H);
        ctx.lineTo(440 + i * 130, H);
        ctx.closePath();
        ctx.fill();
      }
      ctx.globalCompositeOperation = 'source-over';
      bokeh(ctx, pal, ch, 26, 0.4, 1.1, '#b6ffcf');
      // 안개
      const fg = ctx.createLinearGradient(0, H * 0.6, 0, H);
      fg.addColorStop(0, 'rgba(210,255,225,0)');
      fg.addColorStop(1, 'rgba(210,255,225,0.22)');
      ctx.fillStyle = fg;
      ctx.fillRect(0, H * 0.6, W, H * 0.4);
    },
    neon_city(ctx, pal, ch) {
      let g = ctx.createLinearGradient(0, 0, 0, H);
      g.addColorStop(0, '#120a24');
      g.addColorStop(0.55, '#2a1140');
      g.addColorStop(1, '#08060f');
      ctx.fillStyle = g;
      ctx.fillRect(0, 0, W, H);
      // 빌딩
      const rng = AE.subRng(ch.seed, 'city');
      for (let i = 0; i < 14; i++) {
        const bw = 60 + rng() * 120;
        const bx = rng() * (W + 100) - 50;
        const bh = 220 + rng() * 620;
        ctx.fillStyle = `rgba(10,8,24,${0.5 + rng() * 0.4})`;
        ctx.fillRect(bx, H - bh - 60, bw, bh);
        // 창문
        for (let j = 0; j < 12; j++) {
          if (rng() > 0.55) continue;
          ctx.fillStyle = rng() > 0.5 ? 'rgba(120,230,255,0.35)' : 'rgba(255,120,200,0.30)';
          ctx.fillRect(bx + 8 + (j % 3) * (bw / 3.4), H - bh - 40 + Math.floor(j / 3) * 34, 10, 14);
        }
      }
      // 네온 사인
      ctx.globalCompositeOperation = 'screen';
      for (let i = 0; i < 5; i++) {
        const hue = [300, 190, 330, 265, 210][i];
        const x = 60 + i * 200 + (i % 2) * 40;
        const y = 220 + (i % 3) * 180;
        const gg = ctx.createRadialGradient(x, y, 4, x, y, 130);
        gg.addColorStop(0, `hsla(${hue},100%,70%,0.65)`);
        gg.addColorStop(1, `hsla(${hue},100%,60%,0)`);
        ctx.fillStyle = gg;
        ctx.fillRect(x - 140, y - 140, 280, 280);
        ctx.fillStyle = `hsla(${hue},100%,78%,0.85)`;
        ctx.fillRect(x - 6, y - 60, 12, 120);
      }
      ctx.globalCompositeOperation = 'source-over';
      // 바닥 그리드
      ctx.strokeStyle = 'rgba(120,240,255,0.30)';
      ctx.lineWidth = 2;
      for (let i = 0; i < 12; i++) {
        const y = 980 + i * i * 3.2;
        ctx.beginPath();
        ctx.moveTo(0, y);
        ctx.lineTo(W, y);
        ctx.stroke();
      }
      for (let i = -8; i <= 8; i++) {
        ctx.beginPath();
        ctx.moveTo(CX + i * 40, 980);
        ctx.lineTo(CX + i * 220, H);
        ctx.stroke();
      }
      bokeh(ctx, pal, ch, 14, 0.3, 0.7, '#9ef0ff');
    },
    sunset_sea(ctx, pal, ch) {
      const g = ctx.createLinearGradient(0, 0, 0, H);
      g.addColorStop(0, '#3b2b63');
      g.addColorStop(0.28, '#8e4a6a');
      g.addColorStop(0.52, '#e88a6a');
      g.addColorStop(0.62, '#ffc98c');
      g.addColorStop(0.64, '#f0a878');
      g.addColorStop(1, '#5b3358');
      ctx.fillStyle = g;
      ctx.fillRect(0, 0, W, H);
      // 해
      const sx = 700, sy = 640;
      const sg = ctx.createRadialGradient(sx, sy, 10, sx, sy, 260);
      sg.addColorStop(0, 'rgba(255,246,210,1)');
      sg.addColorStop(0.35, 'rgba(255,214,150,0.75)');
      sg.addColorStop(1, 'rgba(255,190,140,0)');
      ctx.fillStyle = sg;
      ctx.beginPath();
      ctx.arc(sx, sy, 260, 0, Math.PI * 2);
      ctx.fill();
      // 수평선 반짝임
      ctx.globalCompositeOperation = 'screen';
      for (let i = 0; i < 26; i++) {
        const y = 826 + i * 16;
        const w = 260 - i * 7 + (i % 3) * 40;
        ctx.fillStyle = `rgba(255,225,180,${0.45 - i * 0.014})`;
        ctx.fillRect(sx - w / 2, y, w, 5);
      }
      ctx.globalCompositeOperation = 'source-over';
      // 구름
      for (let i = 0; i < 5; i++) {
        ctx.fillStyle = `rgba(255,200,190,${0.10 + i * 0.02})`;
        ctx.beginPath();
        ctx.ellipse(120 + i * 220, 250 + (i % 3) * 90, 200, 26, 0, 0, Math.PI * 2);
        ctx.fill();
      }
      bokeh(ctx, pal, ch, 18, 0.3, 1.0, '#ffe0b0');
    },
    gothic_hall(ctx, pal, ch) {
      const g = ctx.createLinearGradient(0, 0, 0, H);
      g.addColorStop(0, '#160d1c');
      g.addColorStop(0.5, '#2a1226');
      g.addColorStop(1, '#0a060d');
      ctx.fillStyle = g;
      ctx.fillRect(0, 0, W, H);
      // 고딕 아치 창
      ctx.save();
      ctx.translate(CX, 0);
      const arch = new Path2D();
      arch.moveTo(-210, 900);
      arch.lineTo(-210, 360);
      arch.bezierCurveTo(-210, 120, 210, 120, 210, 360);
      arch.lineTo(210, 900);
      arch.closePath();
      ctx.fillStyle = 'rgba(120,90,160,0.20)';
      ctx.fill(arch);
      ctx.strokeStyle = 'rgba(190,150,220,0.35)';
      ctx.lineWidth = 8;
      ctx.stroke(arch);
      // 장미창
      ctx.beginPath();
      ctx.arc(0, 430, 120, 0, Math.PI * 2);
      ctx.strokeStyle = 'rgba(214,170,240,0.30)';
      ctx.lineWidth = 4;
      ctx.stroke();
      for (let i = 0; i < 12; i++) {
        ctx.beginPath();
        ctx.moveTo(0, 430);
        ctx.lineTo(Math.cos((i / 12) * Math.PI * 2) * 120, 430 + Math.sin((i / 12) * Math.PI * 2) * 120);
        ctx.stroke();
      }
      ctx.restore();
      // 촛불
      ctx.globalCompositeOperation = 'screen';
      for (let i = 0; i < 4; i++) {
        const x = 110 + i * 270, y = 830 + (i % 2) * 60;
        const cg = ctx.createRadialGradient(x, y, 4, x, y, 160);
        cg.addColorStop(0, 'rgba(255,196,120,0.55)');
        cg.addColorStop(1, 'rgba(255,150,80,0)');
        ctx.fillStyle = cg;
        ctx.fillRect(x - 160, y - 160, 320, 320);
      }
      ctx.globalCompositeOperation = 'source-over';
      // 바닥
      ctx.fillStyle = 'rgba(0,0,0,0.45)';
      ctx.fillRect(0, 1020, W, 260);
      bokeh(ctx, pal, ch, 12, 0.22, 0.8, '#e8c8ff');
    },
    bokeh_soft(ctx, pal, ch) {
      const h1 = AE.hue(pal.outfit), h2 = AE.hue(pal.hair);
      const g = ctx.createLinearGradient(0, 0, W, H);
      g.addColorStop(0, `hsl(${h1}, 42%, 22%)`);
      g.addColorStop(0.55, `hsl(${(h1 + 30) % 360}, 34%, 34%)`);
      g.addColorStop(1, `hsl(${h2}, 38%, 16%)`);
      ctx.fillStyle = g;
      ctx.fillRect(0, 0, W, H);
      bokeh(ctx, pal, ch, 30, 0.35, 1.5);
    },
  };

  function bokeh(ctx, pal, ch, n, alpha, scale, tint) {
    const rng = AE.subRng(ch.seed, 'bokeh' + n);
    const colors = [pal.outfitHi, pal.accent, '#ffffff', tint || pal.hairHi];
    ctx.save();
    ctx.globalCompositeOperation = 'screen';
    for (let i = 0; i < n; i++) {
      const x = rng() * W;
      const y = rng() * H;
      const r = (10 + rng() * 46) * scale;
      const col = colors[Math.floor(rng() * colors.length)];
      const gg = ctx.createRadialGradient(x, y, r * 0.2, x, y, r);
      gg.addColorStop(0, AE.alpha(col, alpha * 0.7));
      gg.addColorStop(0.7, AE.alpha(col, alpha * 0.25));
      gg.addColorStop(1, AE.alpha(col, 0));
      ctx.fillStyle = gg;
      ctx.beginPath();
      ctx.arc(x, y, r, 0, Math.PI * 2);
      ctx.fill();
    }
    ctx.restore();
  }

  /* ---------------- 오라 ---------------- */
  function auraBack(ch, pal, fig, k, topPx) {
    const ctx = AE.G.ctx;
    const id = ch.parts.aura;
    if (id === 'none') return;
    const rng = AE.subRng(ch.seed, 'aura');
    ctx.save();
    if (id === 'bokeh') {
      ctx.globalCompositeOperation = 'screen';
      for (let i = 0; i < 26; i++) {
        const x = rng() * W, y = 120 + rng() * 1000, r = 8 + rng() * 40;
        const gg = ctx.createRadialGradient(x, y, 1, x, y, r);
        gg.addColorStop(0, AE.alpha(pal.accent, 0.5));
        gg.addColorStop(1, AE.alpha(pal.accent, 0));
        ctx.fillStyle = gg;
        ctx.beginPath();
        ctx.arc(x, y, r, 0, Math.PI * 2);
        ctx.fill();
      }
    } else if (id === 'sakura') {
      for (let i = 0; i < 26; i++) {
        const x = rng() * W, y = rng() * H, s = 8 + rng() * 16, a = rng() * 6.28;
        ctx.save();
        ctx.translate(x, y);
        ctx.rotate(a);
        ctx.fillStyle = AE.alpha(AE.mix('#ffc3d8', pal.accent, 0.25), 0.55 + rng() * 0.3);
        for (let p = 0; p < 5; p++) {
          ctx.beginPath();
          ctx.ellipse(Math.cos((p / 5) * 6.283) * s * 0.52, Math.sin((p / 5) * 6.283) * s * 0.52, s * 0.5, s * 0.34, (p / 5) * 6.283, 0, Math.PI * 2);
          ctx.fill();
        }
        ctx.restore();
      }
    } else if (id === 'neon') {
      ctx.globalCompositeOperation = 'screen';
      for (let i = 0; i < 18; i++) {
        const y = 80 + (i / 18) * H;
        ctx.fillStyle = AE.alpha(pal.accent, 0.05 + (i % 3) * 0.02);
        ctx.fillRect(0, y, W, 3);
      }
    } else if (id === 'magic') {
      const cy = topPx + 470 * k;
      ctx.translate(CX, cy);
      ctx.globalCompositeOperation = 'screen';
      const R = 300 * k + 120;
      for (let r = 0; r < 3; r++) {
        ctx.beginPath();
        ctx.arc(0, 0, R * (0.55 + r * 0.22), 0, Math.PI * 2);
        ctx.strokeStyle = AE.alpha(pal.accent, 0.45 - r * 0.1);
        ctx.lineWidth = 4 - r;
        ctx.stroke();
      }
      ctx.beginPath();
      for (let i = 0; i < 5; i++) {
        const a = (i / 5) * Math.PI * 2 - Math.PI / 2;
        const a2 = ((i + 2) / 5) * Math.PI * 2 - Math.PI / 2;
        ctx.moveTo(Math.cos(a) * R * 0.77, Math.sin(a) * R * 0.77);
        ctx.lineTo(Math.cos(a2) * R * 0.77, Math.sin(a2) * R * 0.77);
      }
      ctx.strokeStyle = AE.alpha(pal.accent, 0.3);
      ctx.lineWidth = 2;
      ctx.stroke();
      for (let i = 0; i < 36; i++) {
        const a = (i / 36) * Math.PI * 2;
        ctx.beginPath();
        ctx.moveTo(Math.cos(a) * R * 0.99, Math.sin(a) * R * 0.99);
        ctx.lineTo(Math.cos(a) * R * 1.05, Math.sin(a) * R * 1.05);
        ctx.strokeStyle = AE.alpha(pal.accent, 0.35);
        ctx.lineWidth = 3;
        ctx.stroke();
      }
    } else if (id === 'speed') {
      ctx.globalCompositeOperation = 'screen';
      for (let i = 0; i < 90; i++) {
        const a = rng() * Math.PI * 2;
        const r0 = 300 + rng() * 200, r1 = r0 + 260 + rng() * 420;
        ctx.beginPath();
        ctx.moveTo(CX + Math.cos(a) * r0, 600 + Math.sin(a) * r0);
        ctx.lineTo(CX + Math.cos(a) * r1, 600 + Math.sin(a) * r1);
        ctx.strokeStyle = AE.alpha('#ffffff', 0.05 + rng() * 0.12);
        ctx.lineWidth = 2 + rng() * 6;
        ctx.stroke();
      }
    }
    ctx.restore();
  }

  /* ---------------- 오버레이 ---------------- */
  function overlay(ch, pal) {
    const ctx = AE.G.ctx;
    // 림 라이트 (좌상단)
    ctx.save();
    ctx.globalCompositeOperation = 'screen';
    const g = ctx.createLinearGradient(0, 0, W * 0.7, H);
    g.addColorStop(0, 'rgba(255,250,235,0.10)');
    g.addColorStop(0.5, 'rgba(255,250,235,0.02)');
    g.addColorStop(1, 'rgba(0,0,0,0)');
    ctx.fillStyle = g;
    ctx.fillRect(0, 0, W, H);
    ctx.restore();
    // 비네트
    const v = ctx.createRadialGradient(CX, H * 0.44, H * 0.28, CX, H * 0.44, H * 0.92);
    v.addColorStop(0, 'rgba(0,0,0,0)');
    v.addColorStop(0.72, 'rgba(6,6,12,0.16)');
    v.addColorStop(1, 'rgba(4,4,10,0.5)');
    ctx.fillStyle = v;
    ctx.fillRect(0, 0, W, H);
  }

  /* ---------------- 스케일 ---------------- */
  function viewFor(zoom, fig) {
    const top = zoom === 'bust' ? 60 : 56;
    let bottom;
    if (zoom === 'bust') bottom = 212 + 2.62 * 376;
    else if (zoom === 'full') bottom = 212 + (fig.ratio + 0.06) * 376;
    else bottom = 212 + (fig.h.kneeF + 0.18) * 376;
    return { top, bottom, k: 1160 / (bottom - top) };
  }
  function scaleFor(zoom, fig) { return viewFor(zoom, fig).k; }
  AE.scaleFor = scaleFor;

  /* ---------------- 렌더 ---------------- */
  let OFF = null;
  function getOff() {
    if (!OFF) {
      OFF = document.createElement('canvas');
      OFF.width = W;
      OFF.height = H;
    }
    return OFF;
  }

  AE.render = function (ctx, ch, opt) {
    opt = opt || {};
    const zoom = opt.zoom || 'knee';
    const showBg = opt.bg !== false;
    ctx.setTransform(1, 0, 0, 1, 0, 0);
    ctx.clearRect(0, 0, W, H);

    const off = getOff();
    const octx = off.getContext('2d');
    octx.setTransform(1, 0, 0, 1, 0, 0);
    octx.clearRect(0, 0, W, H);

    const pal = AE.setScene(octx, ch);
    const fig = AE.figure(ch);
    const pose = AE.POSE[ch.parts.pose] || AE.POSE.standing;

    if (showBg) {
      (BGS[ch.parts.bg] || BGS.bokeh_soft)(ctx, pal, ch);
      // 캐릭터와 배경 명도가 붙으면 배경을 밀어 대비 확보
      const BG_L = { classroom: 78, forest: 30, neon_city: 22, sunset_sea: 62, gothic_hall: 20, bokeh_soft: 34 };
      const bgL = BG_L[ch.parts.bg] == null ? 50 : BG_L[ch.parts.bg];
      const oL = AE.lum(pal.outfit);
      if (Math.abs(bgL - oL) < 28) {
        ctx.save();
        if (oL >= bgL) {
          ctx.fillStyle = 'rgba(6,7,14,0.30)';
          ctx.fillRect(0, 0, W, H);
          const g2 = ctx.createRadialGradient(CX, H * 0.42, 60, CX, H * 0.42, H * 0.8);
          g2.addColorStop(0, 'rgba(255,255,255,0.10)');
          g2.addColorStop(1, 'rgba(255,255,255,0)');
          ctx.fillStyle = g2;
          ctx.fillRect(0, 0, W, H);
        } else {
          ctx.fillStyle = 'rgba(235,240,255,0.12)';
          ctx.fillRect(0, 0, W, H);
        }
        ctx.restore();
      }
    }

    const view = viewFor(zoom, fig);
    const k = view.k;
    const topPx = 66 + (212 - view.top) * k;

    auraBack(ch, pal, fig, k, topPx);

    /* 캐릭터 좌표계 (오프스크린) */
    octx.save();
    octx.translate(CX, topPx);
    octx.scale(k, k);
    octx.translate(-CX, -212);

    const arms = AE.buildArms(pose);

    const headT = () => {
      octx.save();
      const px = CX, py = 588;
      const shiftX = (pose.turn || 0) * 22;
      octx.translate(px + shiftX, py);
      octx.scale(1.12, 1.12);
      octx.rotate(((pose.headTilt || 0) * Math.PI) / 180);
      octx.translate(-px, -py);
    };

    // 1. 뒤머리
    headT();
    AE.drawHairBack();
    octx.restore();

    // 2. 몸통/다리/의상
    octx.save();
    if (pose.bodyShift) octx.translate(pose.bodyShift, 0);
    AE.drawBodySkin(fig);
    AE.G.fshift = (pose.turn || 0) * 16;
    AE.drawNeck(fig);
    AE.drawArms(fig, arms, 'limb', 'back');
    AE.drawOutfit(ch, fig, arms);
    AE.drawArms(fig, arms, 'hand', 'back');
    octx.restore();

    // 3. 머리
    headT();
    AE.G.turn = pose.turn || 0;
    AE.drawFace();
    const fshift = (pose.turn || 0) * 16;
    AE.G.fshift = fshift;
    octx.save();
    octx.translate(fshift, 0);
    AE.drawFeatures();
    octx.restore();
    AE.drawHairCap();
    AE.drawElfEars();
    AE.drawAnimalEars();
    AE.drawHairSide();
    AE.drawHairFront();
    AE.drawAhoge();
    AE.drawHeadAcc();
    AE.drawGlasses();
    octx.restore();

    // 4. 앞쪽 팔
    octx.save();
    if (pose.bodyShift) octx.translate(pose.bodyShift, 0);
    AE.drawArms(fig, arms, 'hand', 'fore');
    octx.restore();

    octx.restore(); // 캐릭터 좌표계

    /* 라이팅 패스 — 캐릭터에만 적용 */
    octx.save();
    octx.globalCompositeOperation = 'source-atop';
    const lg = octx.createLinearGradient(W * 0.08, 0, W * 0.96, H);
    lg.addColorStop(0, 'rgba(255,247,228,0.15)');
    lg.addColorStop(0.40, 'rgba(255,255,255,0)');
    lg.addColorStop(0.70, 'rgba(56,42,92,0.12)');
    lg.addColorStop(1, 'rgba(20,12,36,0.30)');
    octx.fillStyle = lg;
    octx.fillRect(0, 0, W, H);
    octx.restore();

    ctx.drawImage(off, 0, 0);

    if (showBg) overlay(ch, pal);
  };

  /* ---------------- 헤어 악세 / 안경 ---------------- */
  AE.drawHeadAcc = function () {
    const ch = AE.G.ch, pal = AE.G.pal, ctx = AE.G.ctx;
    const id = ch.parts.headAcc;
    if (id === 'none') return;
    const rng = AE.subRng(ch.seed, 'acc');
    ctx.save();
    if (id === 'ribbon') {
      const x = CX + 108, y = 262, s = 1;
      ctx.save();
      ctx.translate(x, y);
      ctx.rotate(-0.25);
      for (const d of [-1, 1]) {
        const p = new Path2D();
        p.moveTo(0, 0);
        p.bezierCurveTo(d * 46, -44, d * 96, -20, d * 74, 22);
        p.bezierCurveTo(d * 62, 48, d * 24, 30, 0, 8);
        p.closePath();
        AE.fill(p, pal.accent);
        AE.innerShadow(p, pal.accentDeep, d * 8, 6, 0.5);
        AE.stroke(p, AE.alpha(pal.accentDeep, 0.8), 2.4, 0.7);
      }
      const kn = new Path2D();
      kn.ellipse(0, 4, 16, 20, 0, 0, Math.PI * 2);
      AE.fill(kn, pal.accentDeep);
      ctx.restore();
    } else if (id === 'headband') {
      const p = new Path2D();
      p.moveTo(CX - 168, 330);
      p.bezierCurveTo(CX - 140, 214, CX + 140, 214, CX + 168, 330);
      p.lineTo(CX + 158, 348);
      p.bezierCurveTo(CX + 120, 246, CX - 120, 246, CX - 158, 348);
      p.closePath();
      AE.fill(p, pal.accent);
      AE.innerShadow(p, pal.accentDeep, 0, 8, 0.4);
    } else if (id === 'headphones') {
      const p = new Path2D();
      p.moveTo(CX - 172, 400);
      p.bezierCurveTo(CX - 186, 190, CX + 186, 190, CX + 172, 400);
      ctx.strokeStyle = AE.shade(pal.outfitDeep, 0, 0);
      ctx.lineWidth = 20;
      ctx.lineCap = 'round';
      ctx.stroke(p);
      ctx.strokeStyle = AE.alpha(pal.accent, 0.85);
      ctx.lineWidth = 8;
      ctx.stroke(p);
      for (const s of [-1, 1]) {
        const cup = new Path2D();
        cup.roundRect(CX + s * 172 - 34, 360, 68, 112, 30);
        AE.fill(cup, AE.shade(pal.outfitDeep, 6, 0));
        AE.innerShadow(cup, '#0b0b10', -s * 8, 0, 0.5);
        const led = new Path2D();
        led.ellipse(CX + s * 172, 416, 12, 12, 0, 0, Math.PI * 2);
        AE.fill(led, pal.accent);
      }
    } else if (id === 'beret') {
      ctx.save();
      ctx.translate(CX + 26, 236);
      ctx.rotate(0.16);
      const p = new Path2D();
      p.ellipse(0, 0, 186, 92, 0, 0, Math.PI * 2);
      AE.fill(p, pal.outfit);
      AE.innerShadow(p, pal.outfitDeep, 0, 26, 0.45);
      const rim = new Path2D();
      rim.ellipse(0, 42, 150, 40, 0, 0, Math.PI);
      AE.stroke(rim, AE.alpha(pal.outfitDeep, 0.6), 5, 0.6);
      const tip = new Path2D();
      tip.ellipse(-20, -78, 16, 14, 0, 0, Math.PI * 2);
      AE.fill(tip, AE.shade(pal.outfit, 12, 0));
      ctx.restore();
    } else if (id === 'hairpin') {
      const x = CX + 128, y = 330;
      for (let i = 0; i < 2; i++) {
        ctx.save();
        ctx.translate(x + i * 34, y + i * 14);
        ctx.rotate(i * 0.4);
        starPath(0, 0, 15, 5, AE.mix('#fff3c0', pal.accent, 0.5));
        ctx.restore();
      }
    } else if (id === 'crown') {
      const p = new Path2D();
      const y = 226, w = 118;
      p.moveTo(CX - w, y);
      p.lineTo(CX - w * 0.7, y - 62);
      p.lineTo(CX - w * 0.38, y - 18);
      p.lineTo(CX, y - 78);
      p.lineTo(CX + w * 0.38, y - 18);
      p.lineTo(CX + w * 0.7, y - 62);
      p.lineTo(CX + w, y);
      p.closePath();
      AE.fill(p, AE.mix('#ffd977', pal.accent, 0.35));
      AE.innerShadow(p, '#a8762a', 0, 14, 0.4);
      AE.stroke(p, AE.alpha('#8a5d1e', 0.8), 2.4, 0.6);
      for (let i = -1; i <= 1; i++) {
        const g = new Path2D();
        g.ellipse(CX + i * w * 0.5, y - 22, 8, 10, 0, 0, Math.PI * 2);
        AE.fill(g, i === 0 ? '#ff5d8f' : '#7ee6ff');
      }
    }
    ctx.restore();
  };

  function starPath(x, y, r, n, color) {
    const ctx = AE.G.ctx;
    ctx.beginPath();
    for (let i = 0; i < n * 2; i++) {
      const a = (i / (n * 2)) * Math.PI * 2 - Math.PI / 2;
      const rr = i % 2 ? r * 0.45 : r;
      i ? ctx.lineTo(x + Math.cos(a) * rr, y + Math.sin(a) * rr) : ctx.moveTo(x + Math.cos(a) * rr, y + Math.sin(a) * rr);
    }
    ctx.closePath();
    ctx.fillStyle = color;
    ctx.fill();
    ctx.strokeStyle = 'rgba(255,255,255,0.7)';
    ctx.lineWidth = 2;
    ctx.stroke();
  }

  AE.drawGlasses = function () {
    const ch = AE.G.ch, pal = AE.G.pal, ctx = AE.G.ctx;
    const id = ch.parts.glasses;
    if (id === 'none') return;
    const f = AE.headShape();
    const eyeY = f.eyeY;
    const dx = f.hw * f.eyeDx;
    const fs = AE.G.fshift || 0;
    ctx.save();
    if (id === 'visor') {
      const p = new Path2D();
      p.roundRect(CX - f.hw * 1.05 + fs, eyeY - 54, f.hw * 2.1, 96, 26);
      const g = ctx.createLinearGradient(CX, eyeY - 54, CX, eyeY + 42);
      g.addColorStop(0, AE.alpha(pal.accent, 0.55));
      g.addColorStop(0.5, AE.alpha(pal.accent, 0.18));
      g.addColorStop(1, AE.alpha(pal.accent, 0.45));
      ctx.fillStyle = g;
      ctx.fill(p);
      ctx.strokeStyle = AE.alpha('#ffffff', 0.55);
      ctx.lineWidth = 3;
      ctx.stroke(p);
      ctx.beginPath();
      ctx.roundRect(CX - f.hw * 1.05 + fs, eyeY - 54, f.hw * 2.1, 18, 9);
      ctx.fillStyle = AE.alpha('#ffffff', 0.22);
      ctx.fill();
      for (let i = 0; i < 5; i++) {
        ctx.beginPath();
        ctx.moveTo(CX - f.hw * 0.9 + fs + i * 62, eyeY - 30);
        ctx.lineTo(CX - f.hw * 0.9 + fs + i * 62 + 30, eyeY + 20);
        ctx.strokeStyle = AE.alpha('#ffffff', 0.25);
        ctx.lineWidth = 3;
        ctx.stroke();
      }
    } else {
      for (const s of [-1, 1]) {
        const cx = CX + s * dx + fs;
        const p = new Path2D();
        if (id === 'round') {
          p.ellipse(cx, eyeY - 6, 40, 36, 0, 0, Math.PI * 2);
        } else {
          p.ellipse(cx, eyeY - 4, 40, 36, 0, Math.PI * 0.08, Math.PI * 0.92);
        }
        ctx.fillStyle = 'rgba(200,230,255,0.10)';
        ctx.fill(p);
        ctx.strokeStyle = 'rgba(40,38,52,0.8)';
        ctx.lineWidth = 2.6;
        ctx.lineCap = 'round';
        ctx.stroke(p);
        // 렌즈 반사
        ctx.beginPath();
        ctx.moveTo(cx - 22, eyeY - 22);
        ctx.lineTo(cx - 2, eyeY - 34);
        ctx.strokeStyle = 'rgba(255,255,255,0.45)';
        ctx.lineWidth = 4;
        ctx.stroke();
        // 안경 다리
        ctx.beginPath();
        ctx.moveTo(cx + s * 38, eyeY - 12);
        ctx.lineTo(cx + s * 74, eyeY - 26);
        ctx.strokeStyle = 'rgba(40,38,52,0.7)';
        ctx.lineWidth = 4;
        ctx.stroke();
      }
      // 브릿지
      ctx.beginPath();
      ctx.moveTo(CX - dx + 38 + fs, eyeY - 18);
      ctx.quadraticCurveTo(CX + fs, eyeY - 30, CX + dx - 38 + fs, eyeY - 18);
      ctx.strokeStyle = 'rgba(40,38,52,0.8)';
      ctx.lineWidth = 4;
      ctx.stroke();
    }
    ctx.restore();
  };
})();
