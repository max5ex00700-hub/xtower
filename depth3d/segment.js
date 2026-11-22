/*
 * Depth3D — 피사체 분리 엔진 (v1.1.0)
 *
 *  [1] 고속 배경 고정 : 기존 깊이 맵에서 전경 컷오프 + 에지 확장으로 마스크를 만든다.
 *  [2] AI 피사체 분리 : u2netp(4.4MB, Apache-2.0) ONNX 모델을 onnxruntime-web(WASM)으로
 *      기기 안에서만 돌려 살리언시 마스크를 만든다. 네트워크·CDN 없음.
 *
 * 두 엔진 모두 같은 마스크 형식(Float32Array 0~1, 깊이 맵과 같은 해상도)을 낸다.
 * 그 위에서 컷오프·페더링을 적용해 알파를 만들고, 실루엣 윤곽선(contour)과
 * 배경 인페인팅 캔버스를 뽑아 3D 뷰어에 넘긴다.
 */
(function (global) {
  'use strict';

  var MODEL = {
    name: 'U2Net-p (u2netp)',
    file: 'u2netp.onnx',
    bytes: 4574861,
    license: 'Apache-2.0',
    input: 320
  };

  var AI = {
    status: 'idle',        // idle | loading | ready | error
    session: null,
    pending: null,
    err: null,
    lastMs: 0,
    wasmBytes: 0
  };

  /* ------------------------------------------------------------- 로딩 도우미 */

  function loadScript(src) {
    return new Promise(function (res, rej) {
      var s = document.createElement('script');
      s.src = src;
      s.async = true;
      s.onload = function () { res(); };
      s.onerror = function () { rej(new Error('스크립트를 읽지 못했습니다: ' + src)); };
      document.head.appendChild(s);
    });
  }

  function b64ToU8(b64) {
    var bin = atob(b64), n = bin.length, out = new Uint8Array(n);
    for (var i = 0; i < n; i++) out[i] = bin.charCodeAt(i);
    return out;
  }

  function gunzip(u8) {
    if (typeof DecompressionStream === 'undefined') {
      return Promise.reject(new Error('이 기기의 WebView 는 gzip 해제를 지원하지 않습니다'));
    }
    var st = new Blob([u8]).stream().pipeThrough(new DecompressionStream('gzip'));
    return new Response(st).arrayBuffer().then(function (b) { return new Uint8Array(b); });
  }

  /* ONNX Runtime 은 .wasm 을 fetch 로 읽는다. file:// 에서는 fetch 가 막히므로
     압축 해제해 둔 바이트를 그대로 돌려주도록 fetch 를 한 번만 감싼다. */
  function installFetch(bytes) {
    global.__D3D_WASM_BYTES = bytes;
    if (global.__D3D_FETCH_PATCHED) return;
    global.__D3D_FETCH_PATCHED = true;
    var orig = global.fetch ? global.fetch.bind(global) : null;
    global.fetch = function (input, init) {
      var url = (typeof input === 'string') ? input : ((input && input.url) || '');
      if (/\.wasm(\?|$)/.test(url) && global.__D3D_WASM_BYTES) {
        return Promise.resolve(new Response(global.__D3D_WASM_BYTES, {
          status: 200,
          headers: { 'Content-Type': 'application/wasm', 'Content-Length': String(global.__D3D_WASM_BYTES.length) }
        }));
      }
      if (orig) return orig(input, init);
      return Promise.reject(new Error('fetch 를 쓸 수 없습니다'));
    };
  }

  function ensureRuntime(onStatus) {
    if (AI.session) return Promise.resolve(AI.session);
    if (AI.pending) return AI.pending;
    AI.status = 'loading';
    AI.pending = Promise.resolve()
      .then(function () { if (!global.ort) return loadScript('ort.wasm.min.js'); })
      .then(function () {
        if (onStatus) onStatus('AI 엔진 불러오는 중');
        return loadScript('ai-wasm.js');
      })
      .then(function () { return gunzip(b64ToU8(global.__D3D_WASM_GZ)); })
      .then(function (wasm) {
        global.__D3D_WASM_GZ = null;
        AI.wasmBytes = wasm.length;
        installFetch(wasm);
        var ort = global.ort;
        ort.env.wasm.numThreads = 1;      // SharedArrayBuffer 없이(파일 로컬) 단일 스레드
        ort.env.wasm.proxy = false;
        ort.env.wasm.wasmPaths = 'https://depth3d.local/';  // 위 fetch 가 가로챈다
        ort.env.logLevel = 'error';
        if (onStatus) onStatus('AI 모델 불러오는 중');
        return loadScript('ai-model.js');
      })
      .then(function () { return gunzip(b64ToU8(global.__D3D_MODEL_GZ)); })
      .then(function (model) {
        global.__D3D_MODEL_GZ = null;
        if (onStatus) onStatus('AI 모델 준비 중');
        return global.ort.InferenceSession.create(model.buffer, {
          executionProviders: ['wasm'],
          graphOptimizationLevel: 'all'
        });
      })
      .then(function (s) {
        AI.session = s;
        AI.status = 'ready';
        AI.pending = null;
        return s;
      })
      .catch(function (e) {
        AI.status = 'error';
        AI.err = String((e && e.message) || e);
        AI.pending = null;
        throw e;
      });
    return AI.pending;
  }

  /* ------------------------------------------------------------- 기본 수학 */

  function clamp01(v) { return v < 0 ? 0 : (v > 1 ? 1 : v); }

  function smoothstep(e0, e1, x) {
    var t = (x - e0) / (e1 - e0 || 1e-6);
    t = t < 0 ? 0 : (t > 1 ? 1 : t);
    return t * t * (3 - 2 * t);
  }

  function workCanvas(w, h) {
    var c = document.createElement('canvas');
    c.width = w; c.height = h;
    return c;
  }

  function resample(src, sw, sh, dw, dh) {
    var out = new Float32Array(dw * dh);
    var sx = sw / dw, sy = sh / dh;
    for (var y = 0; y < dh; y++) {
      var fy = (y + 0.5) * sy - 0.5;
      var y0 = Math.floor(fy); var ty = fy - y0;
      if (y0 < 0) { y0 = 0; ty = 0; }
      var y1 = Math.min(sh - 1, y0 + 1);
      for (var x = 0; x < dw; x++) {
        var fx = (x + 0.5) * sx - 0.5;
        var x0 = Math.floor(fx); var tx = fx - x0;
        if (x0 < 0) { x0 = 0; tx = 0; }
        var x1 = Math.min(sw - 1, x0 + 1);
        var a = src[y0 * sw + x0], b = src[y0 * sw + x1];
        var c = src[y1 * sw + x0], d = src[y1 * sw + x1];
        out[y * dw + x] = (a * (1 - tx) + b * tx) * (1 - ty) + (c * (1 - tx) + d * tx) * ty;
      }
    }
    return out;
  }

  /* ------------------------------------------------------------- [2] AI 마스크 */

  function aiMask(img, dw, dh, onStatus) {
    return ensureRuntime(onStatus).then(function (session) {
      var S = MODEL.input;
      var cv = workCanvas(S, S);
      var g = cv.getContext('2d', { willReadFrequently: true });
      g.fillStyle = '#000';
      g.fillRect(0, 0, S, S);
      g.imageSmoothingEnabled = true;
      g.imageSmoothingQuality = 'high';
      g.drawImage(img, 0, 0, S, S); // rembg 와 동일하게 320 정사각으로 늘려 넣는다

      var px = g.getImageData(0, 0, S, S).data;
      var data = new Float32Array(3 * S * S);
      var mean = [0.485, 0.456, 0.406], std = [0.229, 0.224, 0.225];
      var i, c;
      for (i = 0; i < S * S; i++) {
        for (c = 0; c < 3; c++) data[c * S * S + i] = (px[i * 4 + c] / 255 - mean[c]) / std[c];
      }

      var feed = {};
      feed[session.inputNames[0]] = new global.ort.Tensor('float32', data, [1, 3, S, S]);
      var t0 = (global.performance || Date).now();
      return session.run(feed).then(function (out) {
        AI.lastMs = Math.round(((global.performance || Date).now()) - t0);
        var o = out[session.outputNames[0]];   // d0 (가장 정밀한 살리언시 맵)
        var od = o.data;
        var mn = Infinity, mx = -Infinity, k, v;
        for (k = 0; k < od.length; k++) { v = od[k]; if (v < mn) mn = v; if (v > mx) mx = v; }
        var span = (mx - mn) || 1;
        var small = new Float32Array(S * S);
        for (k = 0; k < S * S; k++) small[k] = (od[k] - mn) / span;
        return { w: dw, h: dh, mask: resample(small, S, S, dw, dh), engine: 'ai', ms: AI.lastMs };
      });
    });
  }

  /* ------------------------------------------------------- [1] 깊이 기반 마스크 */

  function dilate(src, w, h, r) {
    if (r < 1) return src;
    var tmp = new Float32Array(w * h), out = new Float32Array(w * h);
    var x, y, i, j, m;
    for (y = 0; y < h; y++) {
      for (x = 0; x < w; x++) {
        m = 0;
        for (j = -r; j <= r; j++) {
          var xx = x + j; if (xx < 0) xx = 0; else if (xx >= w) xx = w - 1;
          var v = src[y * w + xx]; if (v > m) m = v;
        }
        tmp[y * w + x] = m;
      }
    }
    for (x = 0; x < w; x++) {
      for (y = 0; y < h; y++) {
        m = 0;
        for (j = -r; j <= r; j++) {
          var yy = y + j; if (yy < 0) yy = 0; else if (yy >= h) yy = h - 1;
          var v2 = tmp[yy * w + x]; if (v2 > m) m = v2;
        }
        out[y * w + x] = m;
      }
    }
    return out;
  }

  /* 가장 큰 연결 덩어리만 남기고, 안쪽 구멍은 메운다 (깊이 마스크의 잔티끌 제거) */
  function largestBlob(bin, w, h) {
    var n = w * h;
    var lab = new Int32Array(n);
    var stack = new Int32Array(n);
    var best = 0, bestCount = 0, cur = 0, i;
    var counts = [];
    for (i = 0; i < n; i++) {
      if (!bin[i] || lab[i]) continue;
      cur++;
      var sp = 0, count = 0;
      stack[sp++] = i;
      lab[i] = cur;
      while (sp > 0) {
        var p = stack[--sp];
        count++;
        var x = p % w, y = (p / w) | 0;
        if (x > 0 && bin[p - 1] && !lab[p - 1]) { lab[p - 1] = cur; stack[sp++] = p - 1; }
        if (x < w - 1 && bin[p + 1] && !lab[p + 1]) { lab[p + 1] = cur; stack[sp++] = p + 1; }
        if (y > 0 && bin[p - w] && !lab[p - w]) { lab[p - w] = cur; stack[sp++] = p - w; }
        if (y < h - 1 && bin[p + w] && !lab[p + w]) { lab[p + w] = cur; stack[sp++] = p + w; }
      }
      counts[cur] = count;
      if (count > bestCount) { bestCount = count; best = cur; }
    }
    if (!best) return bin;
    var out = new Uint8Array(n);
    for (i = 0; i < n; i++) out[i] = (lab[i] === best) ? 1 : 0;

    // 구멍 메우기: 테두리에서 닿지 않는 배경은 피사체 내부로 본다
    var outside = new Uint8Array(n);
    var sp2 = 0;
    for (i = 0; i < n; i++) {
      var xx = i % w, yy = (i / w) | 0;
      if (out[i]) continue;
      if (xx === 0 || yy === 0 || xx === w - 1 || yy === h - 1) { outside[i] = 1; stack[sp2++] = i; }
    }
    while (sp2 > 0) {
      var q = stack[--sp2];
      var qx = q % w, qy = (q / w) | 0;
      if (qx > 0 && !out[q - 1] && !outside[q - 1]) { outside[q - 1] = 1; stack[sp2++] = q - 1; }
      if (qx < w - 1 && !out[q + 1] && !outside[q + 1]) { outside[q + 1] = 1; stack[sp2++] = q + 1; }
      if (qy > 0 && !out[q - w] && !outside[q - w]) { outside[q - w] = 1; stack[sp2++] = q - w; }
      if (qy < h - 1 && !out[q + w] && !outside[q + w]) { outside[q + w] = 1; stack[sp2++] = q + w; }
    }
    for (i = 0; i < n; i++) if (!outside[i]) out[i] = 1;
    return out;
  }

  function depthMask(depth, cutoff) {
    var w = depth.w, h = depth.h, n = w * h;
    var src = depth.relief || depth.depth;
    var soft = global.DepthEngine.blur(src, w, h, 2);
    var lo = cutoff - 0.12, hi = cutoff + 0.06;
    var bin = new Uint8Array(n);
    var thr = 0.5 * (lo + hi) - 0.06;   // 살짝 넉넉하게 잡아 어깨·몸통까지 포함
    for (var i = 0; i < n; i++) bin[i] = soft[i] > thr ? 1 : 0;

    // 사진 가장자리에 닿은 큰 덩어리(인물/사물)만 남기고 잔티끌은 버린다
    bin = largestBlob(bin, w, h);

    var out = new Float32Array(n);
    for (i = 0; i < n; i++) out[i] = bin[i];
    out = dilate(out, w, h, 2);                       // 에지 확장: 테두리까지 포함
    out = global.DepthEngine.blur(out, w, h, 2);
    return { w: w, h: h, mask: out, engine: 'fast', ms: 0 };
  }

  /* 알파: 컷오프(문턱) + 페더링(경계 부드러움) */
  function buildAlpha(raw, cutoff, feather) {
    var w = raw.w, h = raw.h, n = w * h;
    var px = Math.round(feather * 6.5);
    var m = px > 0 ? global.DepthEngine.blur(raw.mask, w, h, px) : raw.mask;
    var band = 0.06 + 0.16 * feather;
    var lo = cutoff - band, hi = cutoff + band;
    var a = new Float32Array(n);
    for (var i = 0; i < n; i++) a[i] = smoothstep(lo, hi, m[i]);
    return a;
  }

  /* ------------------------------------------------ 실루엣 윤곽선 (marching squares) */

  function traceContour(alpha, w, h, thr) {
    /* 테두리에 1픽셀 여백을 두고 계산한다 — 피사체가 사진 가장자리에 닿아도
       윤곽선이 닫히도록(외곽은 항상 배경=0). */
    var PW = w + 2, PH = h + 2;
    function at(px_, py_) {
      var sx = px_ - 1, sy = py_ - 1;
      if (sx < 0 || sy < 0 || sx >= w || sy >= h) return 0;
      return alpha[sy * w + sx];
    }
    var segsA = [], segsB = [];
    var px = new Float32Array(PW * PH * 2);
    var seen = new Uint8Array(PW * PH * 2);
    var x0, y0;

    function hEdge(cx, cy, va, vb) {
      var id = (cy * PW + cx) * 2;
      if (!seen[id]) {
        var t = (thr - va) / ((vb - va) || 1e-6);
        px[id * 2] = cx - 1 + t;
        px[id * 2 + 1] = cy - 1;
        seen[id] = 1;
      }
      return id;
    }
    function vEdge(cx, cy, va, vb) {
      var id = (cy * PW + cx) * 2 + 1;
      if (!seen[id]) {
        var t = (thr - va) / ((vb - va) || 1e-6);
        px[id * 2] = cx - 1;
        px[id * 2 + 1] = cy - 1 + t;
        seen[id] = 1;
      }
      return id;
    }

    for (y0 = 0; y0 < PH - 1; y0++) {
      for (x0 = 0; x0 < PW - 1; x0++) {
        var a = at(x0, y0), b = at(x0 + 1, y0), c = at(x0 + 1, y0 + 1), d = at(x0, y0 + 1);
        var idx = (a > thr ? 1 : 0) | (b > thr ? 2 : 0) | (c > thr ? 4 : 0) | (d > thr ? 8 : 0);
        if (idx === 0 || idx === 15) continue;
        var eT, eR, eB, eL;
        switch (idx) {
          case 1: segsA.push(hEdge(x0, y0, a, b)); segsB.push(vEdge(x0, y0, a, d)); break;
          case 2: segsA.push(hEdge(x0, y0, a, b)); segsB.push(vEdge(x0 + 1, y0, b, c)); break;
          case 3: segsA.push(vEdge(x0, y0, a, d)); segsB.push(vEdge(x0 + 1, y0, b, c)); break;
          case 4: segsA.push(vEdge(x0 + 1, y0, b, c)); segsB.push(hEdge(x0, y0 + 1, d, c)); break;
          case 5:
            segsA.push(vEdge(x0, y0, a, d)); segsB.push(hEdge(x0, y0, a, b));
            segsA.push(vEdge(x0 + 1, y0, b, c)); segsB.push(hEdge(x0, y0 + 1, d, c));
            break;
          case 6: segsA.push(hEdge(x0, y0, a, b)); segsB.push(hEdge(x0, y0 + 1, d, c)); break;
          case 7: segsA.push(vEdge(x0, y0, a, d)); segsB.push(hEdge(x0, y0 + 1, d, c)); break;
          case 8: segsA.push(hEdge(x0, y0 + 1, d, c)); segsB.push(vEdge(x0, y0, a, d)); break;
          case 9: segsA.push(hEdge(x0, y0, a, b)); segsB.push(hEdge(x0, y0 + 1, d, c)); break;
          case 10:
            segsA.push(hEdge(x0, y0, a, b)); segsB.push(vEdge(x0 + 1, y0, b, c));
            segsA.push(hEdge(x0, y0 + 1, d, c)); segsB.push(vEdge(x0, y0, a, d));
            break;
          case 11: segsA.push(vEdge(x0 + 1, y0, b, c)); segsB.push(hEdge(x0, y0 + 1, d, c)); break;
          case 12: segsA.push(vEdge(x0, y0, a, d)); segsB.push(vEdge(x0 + 1, y0, b, c)); break;
          case 13: segsA.push(hEdge(x0, y0, a, b)); segsB.push(vEdge(x0 + 1, y0, b, c)); break;
          case 14: segsA.push(vEdge(x0, y0, a, d)); segsB.push(hEdge(x0, y0, a, b)); break;
        }
      }
    }
    var segCount = segsA.length;
    if (!segCount) return [];

    var adj = {};
    for (var sI = 0; sI < segCount; sI++) {
      var pA = segsA[sI], pB = segsB[sI];
      (adj[pA] || (adj[pA] = [])).push(sI);
      (adj[pB] || (adj[pB] = [])).push(sI);
    }
    var used = new Uint8Array(segCount);
    var loops = [];
    for (var st = 0; st < segCount; st++) {
      if (used[st]) continue;
      var cur = st, curId = segsA[st];
      var chain = [];
      var guard = 0;
      while (guard++ < 400000) {
        used[cur] = 1;
        var aId = segsA[cur], bId = segsB[cur];
        var exitId = (curId === aId) ? bId : aId;
        chain.push(curId);
        chain.push(exitId);
        var nextId = exitId;
        var cand = adj[nextId] || [];
        var nxt = -1;
        for (var k = 0; k < cand.length; k++) {
          if (!used[cand[k]]) { nxt = cand[k]; break; }
        }
        if (nxt < 0) break;
        cur = nxt; curId = nextId;
      }
      var pts = [];
      for (var m = 0; m < chain.length; m += 2) pts.push([px[chain[m] * 2], px[chain[m] * 2 + 1]]);
      if (pts.length > 12) loops.push(pts);
    }
    if (!loops.length) return [];

    loops.sort(function (p1, p2) { return p2.length - p1.length; });
    var best = simplify(loops[0], 0.6);
    best = smoothLoop(best, 2);
    var out = new Array(best.length);
    for (var i2 = 0; i2 < best.length; i2++) out[i2] = { x: best[i2][0], y: best[i2][1] };
    return out;
  }

  function simplify(pts, eps) {
    var n = pts.length;
    if (n < 8) return pts;
    var keep = new Uint8Array(n);
    keep[0] = 1; keep[n - 1] = 1;
    var stack = [[0, n - 1]];
    while (stack.length) {
      var seg = stack.pop(), i0 = seg[0], i1 = seg[1];
      if (i1 - i0 < 2) continue;
      var ax = pts[i0][0], ay = pts[i0][1], bx = pts[i1][0], by = pts[i1][1];
      var dx = bx - ax, dy = by - ay;
      var len = Math.sqrt(dx * dx + dy * dy) || 1e-6;
      var bestI = -1, bestD = eps;
      for (var i = i0 + 1; i < i1; i++) {
        var p = pts[i];
        var d = Math.abs((p[0] - ax) * dy - (p[1] - ay) * dx) / len;
        if (d > bestD) { bestD = d; bestI = i; }
      }
      if (bestI > 0) { keep[bestI] = 1; stack.push([i0, bestI]); stack.push([bestI, i1]); }
    }
    var out = [];
    for (var k = 0; k < n; k++) if (keep[k]) out.push(pts[k]);
    return out;
  }

  function smoothLoop(pts, iter) {
    var n = pts.length;
    var cur = pts;
    for (var it = 0; it < iter; it++) {
      var nxt = new Array(n);
      for (var i = 0; i < n; i++) {
        var p0 = cur[(i - 1 + n) % n], p1 = cur[i], p2 = cur[(i + 1) % n];
        nxt[i] = [p1[0] * 0.5 + (p0[0] + p2[0]) * 0.25, p1[1] * 0.5 + (p0[1] + p2[1]) * 0.25];
      }
      cur = nxt;
    }
    return cur;
  }

  /* ------------------------------------------------------------- 배경 인페인팅 */

  /* 푸시-풀(push-pull) 다중 해상도 채움: 피사체 자리를 주변 배경색으로 메운다. */
  function fillLayer(img, alpha, w, h) {
    var cv = workCanvas(w, h);
    var g = cv.getContext('2d', { willReadFrequently: true });
    g.imageSmoothingEnabled = true;
    g.imageSmoothingQuality = 'high';
    g.drawImage(img, 0, 0, w, h);
    var px = g.getImageData(0, 0, w, h).data;
    var n = w * h, i;
    var R = new Float32Array(n), G = new Float32Array(n), B = new Float32Array(n), W = new Float32Array(n);
    for (i = 0; i < n; i++) {
      var wt = 1 - smoothstep(0.10, 0.38, alpha[i]);   // 피사체 몸통만 결측 처리
      R[i] = px[i * 4] * wt;
      G[i] = px[i * 4 + 1] * wt;
      B[i] = px[i * 4 + 2] * wt;
      W[i] = wt;
    }

    var levels = [{ w: w, h: h, R: R, G: G, B: B, W: W }];
    while (levels[levels.length - 1].w > 6 && levels[levels.length - 1].h > 6) {
      var c = levels[levels.length - 1];
      var nw = Math.max(3, c.w >> 1), nh = Math.max(3, c.h >> 1);
      var nR = new Float32Array(nw * nh), nG = new Float32Array(nw * nh),
        nB = new Float32Array(nw * nh), nW = new Float32Array(nw * nh);
      for (var y = 0; y < nh; y++) {
        for (var x = 0; x < nw; x++) {
          var sumR = 0, sumG = 0, sumB = 0, sumW = 0;
          for (var dy = 0; dy < 2; dy++) {
            for (var dx = 0; dx < 2; dx++) {
              var sx = Math.min(c.w - 1, x * 2 + dx), sy = Math.min(c.h - 1, y * 2 + dy);
              var si = sy * c.w + sx;
              sumR += c.R[si]; sumG += c.G[si]; sumB += c.B[si]; sumW += c.W[si];
            }
          }
          var o = y * nw + x;
          nR[o] = sumR / 4; nG[o] = sumG / 4; nB[o] = sumB / 4; nW[o] = sumW / 4;
        }
      }
      levels.push({ w: nw, h: nh, R: nR, G: nG, B: nB, W: nW });
    }

    // 거친 층에서부터 위로 올라오며 빈 곳을 채운다
    for (var L = levels.length - 2; L >= 0; L--) {
      var fine = levels[L], up = levels[L + 1];
      for (var y2 = 0; y2 < fine.h; y2++) {
        for (var x2 = 0; x2 < fine.w; x2++) {
          var fi = y2 * fine.w + x2;
          if (fine.W[fi] >= 0.999) continue;
          var ux = Math.min(up.w - 1, x2 * 0.5), uy = Math.min(up.h - 1, y2 * 0.5);
          var ux0 = Math.floor(ux), uy0 = Math.floor(uy);
          var ux1 = Math.min(up.w - 1, ux0 + 1), uy1 = Math.min(up.h - 1, uy0 + 1);
          var tx = ux - ux0, ty = uy - uy0;
          var w00 = (1 - tx) * (1 - ty), w10 = tx * (1 - ty), w01 = (1 - tx) * ty, w11 = tx * ty;
          var i00 = uy0 * up.w + ux0, i10 = uy0 * up.w + ux1, i01 = uy1 * up.w + ux0, i11 = uy1 * up.w + ux1;
          var wsum = up.W[i00] * w00 + up.W[i10] * w10 + up.W[i01] * w01 + up.W[i11] * w11;
          if (wsum < 1e-5) continue;
          var cr = (up.R[i00] * w00 + up.R[i10] * w10 + up.R[i01] * w01 + up.R[i11] * w11) / wsum;
          var cg = (up.G[i00] * w00 + up.G[i10] * w10 + up.G[i01] * w01 + up.G[i11] * w11) / wsum;
          var cb = (up.B[i00] * w00 + up.B[i10] * w10 + up.B[i01] * w01 + up.B[i11] * w11) / wsum;
          var miss = 1 - fine.W[fi];
          fine.R[fi] += cr * miss;
          fine.G[fi] += cg * miss;
          fine.B[fi] += cb * miss;
          fine.W[fi] = 1;
        }
      }
    }

    var base = levels[0];
    // 사진 전체를 크게 번진 색과 섞는다 — 채운 자리가 주변 톤과 이어지도록.
    var br = Math.max(3, Math.round(Math.min(w, h) * 0.055));
    var bR = global.DepthEngine.blur(base.R, w, h, br);
    var bG = global.DepthEngine.blur(base.G, w, h, br);
    var bB = global.DepthEngine.blur(base.B, w, h, br);
    var bW = global.DepthEngine.blur(base.W, w, h, br);
    var out = new Float32Array(n * 3);
    for (i = 0; i < n; i++) {
      var ww = base.W[i] || 1;
      var fr = base.R[i] / ww, fg2 = base.G[i] / ww, fb = base.B[i] / ww;
      var bw = bW[i] || 1;
      var gr = bR[i] / bw, gg = bG[i] / bw, gb = bB[i] / bw;
      out[i * 3] = fr * 0.62 + gr * 0.38;
      out[i * 3 + 1] = fg2 * 0.62 + gg * 0.38;
      out[i * 3 + 2] = fb * 0.62 + gb * 0.38;
    }
    return { w: w, h: h, rgb: out };
  }

  /* 배경 캔버스 만들기: 원본 + (피사체 자리를 메운 저해상도 채움) 을 부드러운 마스크로 합성 */
  function background(img, alpha, aw, ah, outW, outH) {
    var t0 = (global.performance || Date).now();
    var fill = fillLayer(img, alpha, aw, ah);
    var softA = global.DepthEngine.blur(alpha, aw, ah, Math.max(2, Math.round(aw * 0.030)));

    var cv = workCanvas(outW, outH);
    var g = cv.getContext('2d', { willReadFrequently: true });
    g.imageSmoothingEnabled = true;
    g.imageSmoothingQuality = 'high';
    g.drawImage(img, 0, 0, outW, outH);
    var im = g.getImageData(0, 0, outW, outH);
    var d = im.data;
    var sx = aw / outW, sy = ah / outH;
    var i, u, v;
    for (var y = 0; y < outH; y++) {
      v = (y + 0.5) * sy - 0.5;
      var y0 = Math.max(0, Math.min(ah - 1, Math.floor(v)));
      var y1 = Math.min(ah - 1, y0 + 1);
      var ty = v - Math.floor(v);
      for (var x = 0; x < outW; x++) {
        u = (x + 0.5) * sx - 0.5;
        var x0 = Math.max(0, Math.min(aw - 1, Math.floor(u)));
        var x1 = Math.min(aw - 1, x0 + 1);
        var tx = u - Math.floor(u);
        var a0 = softA[y0 * aw + x0] * (1 - tx) + softA[y0 * aw + x1] * tx;
        var a1 = softA[y1 * aw + x0] * (1 - tx) + softA[y1 * aw + x1] * tx;
        var A = a0 * (1 - ty) + a1 * ty;
        if (A <= 0.002) continue;
        if (A > 0.985) A = 0.985;
        var f0 = ((y0 * fill.w + x0) * 3);
        var fr = fill.rgb[f0], fg = fill.rgb[f0 + 1], fb = fill.rgb[f0 + 2];
        i = (y * outW + x) * 4;
        d[i] = d[i] * (1 - A) + fr * A;
        d[i + 1] = d[i + 1] * (1 - A) + fg * A;
        d[i + 2] = d[i + 2] * (1 - A) + fb * A;
      }
    }
    g.putImageData(im, 0, 0);

    // 배경 전체를 아주 살짝 번지게 한다 — 채운 자리와 주변이 자연스럽게 이어진다.
    var half = workCanvas(Math.max(8, outW >> 1), Math.max(8, outH >> 1));
    var hg = half.getContext('2d');
    hg.imageSmoothingEnabled = true;
    hg.imageSmoothingQuality = 'high';
    hg.drawImage(cv, 0, 0, half.width, half.height);
    g.imageSmoothingEnabled = true;
    g.imageSmoothingQuality = 'high';
    g.clearRect(0, 0, outW, outH);
    g.drawImage(half, 0, 0, outW, outH);

    // 뒤에 깔릴 부드러운 그림자(피사체 실루엣) — 채운 자리의 흔적을 자연스럽게 감춘다
    var shW = aw, shH = ah;
    var shadow = workCanvas(shW, shH);
    var sg = shadow.getContext('2d');
    var sIm = sg.createImageData(shW, shH);
    var softSh = global.DepthEngine.blur(alpha, aw, ah, Math.max(3, Math.round(aw * 0.028)));
    for (var q = 0; q < aw * ah; q++) {
      var av = softSh[q];
      if (av < 0.02) continue;
      var qo = q * 4;
      sIm.data[qo] = 6; sIm.data[qo + 1] = 8; sIm.data[qo + 2] = 12;
      sIm.data[qo + 3] = Math.round(Math.min(1, av * 1.05) * 150);
    }
    sg.putImageData(sIm, 0, 0);

    return { canvas: cv, shadow: shadow, ms: Math.round(((global.performance || Date).now()) - t0) };
  }

  /* ------------------------------------------------------------- 공개 API */

  var API = {
    MODEL: MODEL,
    aiStatus: function () { return { status: AI.status, err: AI.err, lastMs: AI.lastMs, wasmBytes: AI.wasmBytes }; },
    warmup: function (onStatus) { return ensureRuntime(onStatus); },
    aiMask: aiMask,
    depthMask: depthMask,
    buildAlpha: buildAlpha,
    traceContour: traceContour,
    background: background,
    smoothstep: smoothstep
  };

  global.SegEngine = API;
})(window);
