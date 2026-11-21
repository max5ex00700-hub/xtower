/*
 * Depth3D — 기기 안에서만 도는 깊이 맵 생성기.
 * 서버·API·ML 런타임 없이, 사진 한 장에서 2.5D 변위용 깊이 맵을 만든다.
 *
 *   1) 휘도(L)·채도(S)·국소 대비(E, 초점 선명도 대용)를 뽑는다.
 *   2) 전경 가중치(F) = 선명도 + 채도 → 인물/사물이 앞으로 나오게.
 *   3) 다중 스케일 guided filter(에지 보존 블러)로 형태를 정리한다.
 *   4) 백분위 대비 보정으로 0~1 깊이를 고르게 펼친다.
 */
(function (global) {
  'use strict';

  var WORK_MAX = 512;

  var work = document.createElement('canvas');
  var wctx = work.getContext('2d', { willReadFrequently: true });

  /* ---------------------------------------------------------------- 기초 필터 */

  function boxH(src, dst, w, h, r) {
    var inv = 1 / (2 * r + 1);
    for (var y = 0; y < h; y++) {
      var row = y * w, sum = 0, i, x;
      for (i = -r; i <= r; i++) sum += src[row + Math.min(w - 1, Math.max(0, i))];
      for (x = 0; x < w; x++) {
        dst[row + x] = sum * inv;
        var add = x + r + 1; if (add > w - 1) add = w - 1;
        var sub = x - r; if (sub < 0) sub = 0;
        sum += src[row + add] - src[row + sub];
      }
    }
  }

  function boxV(src, dst, w, h, r) {
    var inv = 1 / (2 * r + 1);
    var tmp = new Float32Array(h);
    for (var x = 0; x < w; x++) {
      var sum = 0, i, y;
      for (i = -r; i <= r; i++) sum += src[Math.min(h - 1, Math.max(0, i)) * w + x];
      for (y = 0; y < h; y++) {
        dst[y * w + x] = sum * inv;
        var add = y + r + 1; if (add > h - 1) add = h - 1;
        var sub = y - r; if (sub < 0) sub = 0;
        sum += src[add * w + x] - src[sub * w + x];
      }
    }
    return tmp;
  }

  function blur(src, w, h, r) {
    if (r < 1) return src.slice();
    var t = new Float32Array(w * h);
    var o = new Float32Array(w * h);
    boxH(src, t, w, h, r);
    boxV(t, o, w, h, r);
    return o;
  }

  /* 에지 보존 평활: 자기 유도(guided) 필터 */
  function guided(p, guide, w, h, r, eps) {
    var n = w * h, i;
    var mI = blur(guide, w, h, r);
    var mP = blur(p, w, h, r);
    var Ip = new Float32Array(n), II = new Float32Array(n);
    for (i = 0; i < n; i++) {
      Ip[i] = guide[i] * p[i];
      II[i] = guide[i] * guide[i];
    }
    var mIp = blur(Ip, w, h, r);
    var mII = blur(II, w, h, r);
    var a = new Float32Array(n), b = new Float32Array(n);
    for (i = 0; i < n; i++) {
      var varI = mII[i] - mI[i] * mI[i];
      var cov = mIp[i] - mI[i] * mP[i];
      a[i] = cov / (varI + eps);
      b[i] = mP[i] - a[i] * mI[i];
    }
    var ma = blur(a, w, h, r);
    var mb = blur(b, w, h, r);
    var q = new Float32Array(n);
    for (i = 0; i < n; i++) q[i] = ma[i] * guide[i] + mb[i];
    return q;
  }

  /* 백분위(기본 2~98%) 스트레치 */
  function stretch(src, loPct, hiPct) {
    var n = src.length, i, min = Infinity, max = -Infinity;
    for (i = 0; i < n; i++) {
      var v = src[i];
      if (v < min) min = v;
      if (v > max) max = v;
    }
    var out = new Float32Array(n);
    if (!(max > min)) { out.fill(0.5); return out; }
    var bins = 512, hist = new Uint32Array(bins), scale = (bins - 1) / (max - min);
    for (i = 0; i < n; i++) hist[((src[i] - min) * scale) | 0]++;
    var loTarget = n * loPct, hiTarget = n * hiPct, acc = 0, lo = min, hi = max, found = false;
    for (i = 0; i < bins; i++) {
      acc += hist[i];
      if (!found && acc >= loTarget) { lo = min + i / scale; found = true; }
      if (acc >= hiTarget) { hi = min + i / scale; break; }
    }
    if (!(hi > lo)) { lo = min; hi = max; }
    var span = 1 / (hi - lo);
    for (i = 0; i < n; i++) {
      var t = (src[i] - lo) * span;
      out[i] = t < 0 ? 0 : (t > 1 ? 1 : t);
    }
    return out;
  }

  /* ---------------------------------------------------------------- 본 계산 */

  var cache = null;

  function prepare(img) {
    var iw = img.naturalWidth || img.width;
    var ih = img.naturalHeight || img.height;
    if (!iw || !ih) return null;

    var w, h;
    if (iw >= ih) { w = WORK_MAX; h = Math.max(8, Math.round(WORK_MAX * ih / iw)); }
    else { h = WORK_MAX; w = Math.max(8, Math.round(WORK_MAX * iw / ih)); }

    work.width = w;
    work.height = h;
    wctx.fillStyle = '#000';
    wctx.fillRect(0, 0, w, h);
    wctx.imageSmoothingEnabled = true;
    wctx.imageSmoothingQuality = 'high';
    wctx.drawImage(img, 0, 0, w, h);

    var data = wctx.getImageData(0, 0, w, h).data;
    var n = w * h, i, x, y;

    var L = new Float32Array(n);
    var sat = new Float32Array(n);
    for (i = 0; i < n; i++) {
      var o = i * 4;
      var r = data[o] / 255, g = data[o + 1] / 255, b = data[o + 2] / 255;
      L[i] = 0.2126 * r + 0.7152 * g + 0.0722 * b;
      var mx = r > g ? (r > b ? r : b) : (g > b ? g : b);
      var mn = r < g ? (r < b ? r : b) : (g < b ? g : b);
      sat[i] = mx > 0.001 ? (mx - mn) / mx : 0;
    }

    // 초점(선명도) 단서: 아주 큰 스케일에서만 본다 — 머리카락 결 같은
    // 미세한 잡음이 깊이 요철로 새어 나오지 않게 넓게 번지게 한다.
    var Lmid = blur(L, w, h, 5);
    var edge = new Float32Array(n);
    for (i = 0; i < n; i++) edge[i] = Math.abs(L[i] - Lmid[i]);
    var sharp = blur(blur(edge, w, h, 4), w, h, 8);

    // 큰 형태 위주의 휘도(얼굴·사물 덩어리) — 픽셀 단위 요철을 피한다.
    var Lbig = blur(blur(L, w, h, 7), w, h, 7);
    var Ln = stretch(Lbig, 0.03, 0.97);
    var Ld = stretch(L, 0.02, 0.98);
    var Sn = stretch(sat, 0.05, 0.95);
    var En = stretch(sharp, 0.05, 0.95);

    // 전경 가중치: 선명하고 채도가 높은 곳 = 앞쪽.
    var fg = new Float32Array(n);
    for (i = 0; i < n; i++) {
      var f = 0.62 * En[i] + 0.38 * Sn[i];
      fg[i] = f < 0 ? 0 : (f > 1 ? 1 : f);
    }
    fg = guided(fg, L, w, h, 10, 0.006);

    var base = new Float32Array(n);
    var cx = (w - 1) * 0.5, cy = (h - 1) * 0.5;
    var rx = w * 0.58, ry = h * 0.58;
    for (y = 0; y < h; y++) {
      for (x = 0; x < w; x++) {
        i = y * w + x;
        var dx = (x - cx) / rx, dy = (y - cy) / ry;
        var c = Math.exp(-(dx * dx + dy * dy) * 0.85); // 중앙 부드러운 가중
        base[i] = 0.54 * Ln[i] + 0.14 * fg[i] + 0.10 * En[i] + 0.10 * Ld[i] + 0.12 * c;
      }
    }

    cache = { w: w, h: h, base: base, guide: L, L: new Float32Array(L), iw: iw, ih: ih };
    return cache;
  }

  function refine(smooth) {
    if (!cache) return null;
    smooth = Math.max(0, Math.min(1, smooth || 0));
    var w = cache.w, h = cache.h, base = cache.base, guide = cache.guide, n = w * h;

    var r1 = 4 + Math.round(8 * smooth);
    var r2 = 14 + Math.round(26 * smooth);
    var q1 = guided(base, guide, w, h, r1, 0.0025);
    var q2 = guided(base, guide, w, h, r2, 0.0060);

    var wSmooth = 0.30 + 0.50 * smooth;
    var q = new Float32Array(n);
    var i;
    var detail = blur(guide, w, h, 2);
    var detailAmt = 0.045 * (1 - 0.7 * smooth);
    for (i = 0; i < n; i++) {
      var v = q1[i] * (1 - wSmooth) + q2[i] * wSmooth;
      v += (guide[i] - detail[i]) * detailAmt; // 미세 결 유지
      q[i] = v;
    }

    var d = stretch(q, 0.01, 0.99);
    for (i = 0; i < n; i++) {
      var t = d[i];
      // 어두운 배경이 완전히 0으로 붙지 않게, 살짝 눌러 곡선을 부드럽게.
      t = t * t * (3 - 2 * t) * 0.35 + t * 0.65;
      d[i] = 0.055 + 0.945 * (t < 0 ? 0 : (t > 1 ? 1 : t));
    }

    // 조명용 완만한 기복(relief): 깊이 자체를 자기 유도로 한 번 더 다듬는다.
    // 변위는 d(디테일 유지), 표면 법선은 relief(부드러움)를 쓴다.
    var relief = guided(guided(d, d, w, h, 4 + Math.round(8 * smooth), 0.008), d, w, h, 3, 0.004);
    relief = stretch(relief, 0.02, 0.98);
    for (i = 0; i < n; i++) relief[i] = 0.055 + 0.945 * relief[i];

    return { depth: d, relief: relief, w: w, h: h };
  }

  function build(img, smooth) {
    if (!prepare(img)) return null;
    return refine(smooth);
  }

  function sampleBilinear(map, w, h, u, v) {
    // u,v in 0..1, v=0 = top of image
    var x = u * (w - 1), y = v * (h - 1);
    var x0 = Math.floor(x), y0 = Math.floor(y);
    var x1 = Math.min(w - 1, x0 + 1), y1 = Math.min(h - 1, y0 + 1);
    var fx = x - x0, fy = y - y0;
    var a = map[y0 * w + x0], b = map[y0 * w + x1];
    var c = map[y1 * w + x0], e = map[y1 * w + x1];
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + e * fx) * fy;
  }

  global.DepthEngine = {
    build: build,
    refine: refine,
    sample: sampleBilinear,
    hasImage: function () { return !!cache; },
    size: function () { return cache ? { w: cache.w, h: cache.h } : null; }
  };
})(window);
