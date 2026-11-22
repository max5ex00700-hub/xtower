/*
 * Depth3D — 순수 JS GIF89a 인코더 (v1.3.0)
 *
 * 외부 CDN·라이브러리 없이 동작한다(인터넷 권한 0개 정책 유지).
 *   - median-cut 256색 양자화 (프레임에서 뽑은 표본으로 전역 팔레트 1개)
 *   - 8비트 LZW 압축 + GIF 서브블록(255바이트) 출력
 *   - NETSCAPE2.0 무한 루프 확장
 *
 * 사용법
 *   var enc = GifEnc.create(w, h);
 *   enc.setPalette(samples);           // Uint8Array(RGBA) 여러 장을 넘겨도 된다
 *   enc.addFrame(rgba, delayCs);       // Uint8Array(w*h*4), delay 1/100초
 *   var bytes = enc.finish();          // Uint8Array
 */
(function (global) {
  'use strict';

  /* ------------------------------------------------------------ 바이트 버퍼 */

  function ByteBuf(cap) {
    this.buf = new Uint8Array(Math.max(1024, cap || 65536));
    this.len = 0;
  }
  ByteBuf.prototype.ensure = function (n) {
    if (this.len + n <= this.buf.length) return;
    var cap = this.buf.length;
    while (cap < this.len + n) cap <<= 1;
    var nb = new Uint8Array(cap);
    nb.set(this.buf.subarray(0, this.len));
    this.buf = nb;
  };
  ByteBuf.prototype.u8 = function (v) {
    this.ensure(1);
    this.buf[this.len++] = v & 255;
  };
  ByteBuf.prototype.u16 = function (v) {
    this.ensure(2);
    this.buf[this.len++] = v & 255;
    this.buf[this.len++] = (v >> 8) & 255;
  };
  ByteBuf.prototype.raw = function (arr) {
    this.ensure(arr.length);
    this.buf.set(arr, this.len);
    this.len += arr.length;
  };
  ByteBuf.prototype.ascii = function (s) {
    this.ensure(s.length);
    for (var i = 0; i < s.length; i++) this.buf[this.len++] = s.charCodeAt(i) & 255;
  };
  ByteBuf.prototype.result = function () {
    return this.buf.subarray(0, this.len);
  };

  /* ------------------------------------------------------------ median cut */

  function boxBounds(idx, rgb, start, end) {
    var rmin = 255, rmax = 0, gmin = 255, gmax = 0, bmin = 255, bmax = 0;
    for (var i = start; i < end; i++) {
      var o = idx[i] * 3;
      var r = rgb[o], g = rgb[o + 1], b = rgb[o + 2];
      if (r < rmin) rmin = r;
      if (r > rmax) rmax = r;
      if (g < gmin) gmin = g;
      if (g > gmax) gmax = g;
      if (b < bmin) bmin = b;
      if (b > bmax) bmax = b;
    }
    return { start: start, end: end, rmin: rmin, rmax: rmax, gmin: gmin, gmax: gmax, bmin: bmin, bmax: bmax };
  }

  /* 표본 RGB(3바이트/픽셀) → 팔레트(최대 maxColors). 반환 {pal:Uint8Array(n*3), size:n} */
  function medianCut(rgb, count, maxColors) {
    if (!count) {
      return { pal: new Uint8Array([11, 15, 23]), size: 1 };
    }
    var idx = new Array(count);
    for (var i = 0; i < count; i++) idx[i] = i;
    var boxes = [boxBounds(idx, rgb, 0, count)];
    var ch, sub, cmp;

    function channel(b) {
      var dr = b.rmax - b.rmin, dg = b.gmax - b.gmin, db = b.bmax - b.bmin;
      if (dr >= dg && dr >= db) return 0;
      return dg >= db ? 1 : 2;
    }

    while (boxes.length < maxColors) {
      var bi = -1, bestArea = -1;
      for (var k = 0; k < boxes.length; k++) {
        var b = boxes[k];
        var n = b.end - b.start;
        if (n < 2) continue;
        var area = Math.max(b.rmax - b.rmin, b.gmax - b.gmin, b.bmax - b.bmin);
        if (area <= 0) continue;
        var score = area * Math.sqrt(n);          // 넓고 픽셀 많은 상자부터 쪼갠다
        if (score > bestArea) { bestArea = score; bi = k; }
      }
      if (bi < 0) break;

      var bx = boxes[bi];
      ch = channel(bx);
      cmp = (function (c) {
        return function (a, b2) { return rgb[a * 3 + c] - rgb[b2 * 3 + c]; };
      })(ch);
      sub = idx.slice(bx.start, bx.end);
      sub.sort(cmp);
      for (var q = 0; q < sub.length; q++) idx[bx.start + q] = sub[q];

      var mid = bx.start + ((bx.end - bx.start) >> 1);
      // 같은 값이 몰려 있으면 한쪽이 비지 않게 밀어 준다
      var lo = mid - 1, hi = mid;
      while (lo > bx.start && rgb[idx[lo] * 3 + ch] === rgb[idx[mid] * 3 + ch]) lo--;
      while (hi < bx.end - 1 && rgb[idx[hi] * 3 + ch] === rgb[idx[mid] * 3 + ch]) hi++;
      if (lo === bx.start && hi < bx.end - 1) mid = hi + 1;
      if (mid <= bx.start) mid = bx.start + 1;
      if (mid >= bx.end) mid = bx.end - 1;

      boxes.splice(bi, 1, boxBounds(idx, rgb, bx.start, mid), boxBounds(idx, rgb, mid, bx.end));
    }

    var size = boxes.length;
    var pal = new Uint8Array(size * 3);
    for (var j = 0; j < size; j++) {
      var bj = boxes[j], sr = 0, sg = 0, sb = 0, cnt = 0;
      for (var t = bj.start; t < bj.end; t++) {
        var o = idx[t] * 3;
        sr += rgb[o]; sg += rgb[o + 1]; sb += rgb[o + 2]; cnt++;
      }
      cnt = Math.max(1, cnt);
      pal[j * 3] = Math.round(sr / cnt);
      pal[j * 3 + 1] = Math.round(sg / cnt);
      pal[j * 3 + 2] = Math.round(sb / cnt);
    }
    return { pal: pal, size: size };
  }

  /* ------------------------------------------------------------ 팔레트 매핑 */

  function Mapper(pal, size) {
    this.pal = pal;
    this.size = size;
    this.cache = new Int16Array(32768);
    for (var i = 0; i < 32768; i++) this.cache[i] = -1;
  }

  Mapper.prototype.nearest = function (r, g, b) {
    var key = ((r >> 3) << 10) | ((g >> 3) << 5) | (b >> 3);
    var hit = this.cache[key];
    if (hit >= 0) return hit;
    var pal = this.pal, n = this.size;
    var best = 0, bestD = 1e12;
    for (var i = 0; i < n; i++) {
      var o = i * 3;
      var dr = r - pal[o], dg = g - pal[o + 1], db = b - pal[o + 2];
      var d = dr * dr * 0.34 + dg * dg * 0.5 + db * db * 0.16;
      if (d < bestD) { bestD = d; best = i; }
    }
    this.cache[key] = best;
    return best;
  };

  /* ------------------------------------------------------------ LZW */

  var BAYER4 = [
    0, 8, 2, 10,
    12, 4, 14, 6,
    3, 11, 1, 9,
    15, 7, 13, 5
  ];

  function LzwOut(out) {
    this.out = out;
    this.block = new Uint8Array(255);
    this.blockLen = 0;
    this.bits = 0;
    this.nbits = 0;
  }
  LzwOut.prototype.flushBlock = function () {
    if (!this.blockLen) return;
    this.out.u8(this.blockLen);
    this.out.raw(this.block.subarray(0, this.blockLen));
    this.blockLen = 0;
  };
  LzwOut.prototype.code = function (code, size) {
    this.bits |= code << this.nbits;
    this.nbits += size;
    while (this.nbits >= 8) {
      this.block[this.blockLen++] = this.bits & 255;
      this.bits >>>= 8;
      this.nbits -= 8;
      if (this.blockLen === 255) this.flushBlock();
    }
  };
  LzwOut.prototype.flush = function () {
    if (this.nbits > 0) {
      this.block[this.blockLen++] = this.bits & 255;
      this.bits = 0;
      this.nbits = 0;
      if (this.blockLen === 255) this.flushBlock();
    }
    this.flushBlock();
    this.out.u8(0);          // 서브블록 종료
  };

  var DICT = new Int32Array(4096 * 256);

  function compress(indices, out) {
    var clearCode = 256, endCode = 257, first = 258;
    var codeSize = 9, next = first;
    var w = new LzwOut(out);
    DICT.fill(0);
    w.code(clearCode, codeSize);

    var n = indices.length;
    if (!n) { w.code(endCode, codeSize); w.flush(); return; }
    var cur = indices[0];
    for (var i = 1; i < n; i++) {
      var k = indices[i];
      var key = (cur << 8) | k;
      var found = DICT[key];
      if (found) { cur = found; continue; }
      w.code(cur, codeSize);
      // 코드 길이 갱신은 방출 '직후·사전 추가 직전' — GIF 표준 인코더(GIFCOMPR)와 같은 순서.
      if (next > (1 << codeSize) - 1 && codeSize < 12) codeSize++;
      if (next < 4096) {
        DICT[key] = next++;
      } else {
        w.code(clearCode, codeSize);
        DICT.fill(0);
        next = first;
        codeSize = 9;
      }
      cur = k;
    }
    w.code(cur, codeSize);
    w.code(endCode, codeSize);
    w.flush();
  }

  /* ------------------------------------------------------------ 인코더 */

  function Encoder(w, h, opts) {
    opts = opts || {};
    this.w = w | 0;
    this.h = h | 0;
    this.out = new ByteBuf(this.w * this.h);
    this.pal = null;
    this.palSize = 0;
    this.mapper = null;
    this.frames = 0;
    this.started = false;
    this.bg = opts.background || [11, 15, 23];
    this.dither = opts.dither === undefined ? 10 : opts.dither;
    this.loop = opts.loop === undefined ? 0 : opts.loop;   // 0 = 무한
    this.indexBuf = new Uint8Array(this.w * this.h);
  }

  Encoder.prototype.setPalette = function (samples, maxColors) {
    var rgb;
    if (samples instanceof Uint8Array) {
      var pixels = (samples.length / 4) | 0;
      var stride = Math.max(1, Math.floor(pixels / 40000));
      var cnt = Math.floor(pixels / stride);
      rgb = new Uint8Array(cnt * 3);
      var c = 0;
      for (var i = 0; i < pixels; i += stride) {
        var o = i * 4;
        if (samples[o + 3] < 24) continue;              // 빈 곳은 팔레트 표본에서 뺀다
        rgb[c * 3] = samples[o];
        rgb[c * 3 + 1] = samples[o + 1];
        rgb[c * 3 + 2] = samples[o + 2];
        c++;
      }
      rgb = rgb.subarray(0, c * 3);
    } else if (Array.isArray(samples)) {
      // [[...RGBA], [...RGBA]] 형태
      var total = 0;
      for (var f = 0; f < samples.length; f++) total += samples[f].length >> 2;
      var stride2 = Math.max(1, Math.floor(total / 40000));
      rgb = new Uint8Array(Math.ceil(total / stride2) * 3 + 3);
      var q = 0, seen = 0;
      for (var f2 = 0; f2 < samples.length; f2++) {
        var buf = samples[f2], px = buf.length >> 2;
        for (var p = 0; p < px; p++) {
          if (seen++ % stride2) continue;
          var oo = p * 4;
          if (buf[oo + 3] < 24) continue;
          rgb[q * 3] = buf[oo];
          rgb[q * 3 + 1] = buf[oo + 1];
          rgb[q * 3 + 2] = buf[oo + 2];
          q++;
        }
      }
      rgb = rgb.subarray(0, q * 3);
    } else {
      rgb = new Uint8Array(0);
    }

    var res = medianCut(rgb, (rgb.length / 3) | 0, Math.min(256, maxColors || 256));
    this.pal = res.pal;
    this.palSize = res.size;
    this.mapper = new Mapper(this.pal, res.size);
    this.started = false;
    return { colors: res.size, samples: (rgb.length / 3) | 0 };
  };

  Encoder.prototype.start = function () {
    var o = this.out;
    o.ascii('GIF89a');
    o.u16(this.w);
    o.u16(this.h);
    var bits = 1;
    while ((1 << bits) < this.palSize) bits++;
    if (bits < 1) bits = 1;
    var sizeCode = Math.max(0, Math.min(7, bits - 1));
    o.u8(0x80 | 0x70 | sizeCode);        // 전역 색표 있음 · 8비트 색해상도
    o.u8(0);                             // 배경 색 인덱스
    o.u8(0);                             // 픽셀 종횡비
    var table = (1 << (sizeCode + 1)) * 3;
    var gct = new Uint8Array(table);
    gct.set(this.pal.subarray(0, Math.min(this.pal.length, table)));
    o.raw(gct);
    // NETSCAPE2.0 무한 루프
    o.u8(0x21); o.u8(0xFF); o.u8(0x0B);
    o.ascii('NETSCAPE2.0');
    o.u8(0x03); o.u8(0x01); o.u16(this.loop); o.u8(0);
    this.started = true;
  };

  /* rgba: Uint8Array/Uint8ClampedArray(w*h*4), delayCs: 1/100초 */
  Encoder.prototype.addFrame = function (rgba, delayCs) {
    if (!this.started) this.start();
    var w = this.w, h = this.h, n = w * h;
    var pal = this.pal, mapper = this.mapper;
    var idx = this.indexBuf;
    var bg = this.bg;
    var amp = this.dither;
    var br = bg[0], bgg = bg[1], bb = bg[2];

    for (var y = 0; y < h; y++) {
      var row = y * w;
      var by = (y & 3) << 2;
      for (var x = 0; x < w; x++) {
        var p = (row + x) * 4;
        var r = rgba[p], g = rgba[p + 1], b = rgba[p + 2], a = rgba[p + 3];
        if (a < 250) {
          var k = a / 255;
          r = r * k + br * (1 - k);
          g = g * k + bgg * (1 - k);
          b = b * k + bb * (1 - k);
        }
        if (amp) {
          var d = (BAYER4[by + (x & 3)] - 7.5) / 7.5 * amp;
          r += d; g += d; b += d;
          r = r < 0 ? 0 : (r > 255 ? 255 : r);
          g = g < 0 ? 0 : (g > 255 ? 255 : g);
          b = b < 0 ? 0 : (b > 255 ? 255 : b);
        }
        idx[row + x] = mapper.nearest(r | 0, g | 0, b | 0);
      }
    }

    var o = this.out;
    var delay = Math.max(2, Math.round(delayCs || 6));
    o.u8(0x21); o.u8(0xF9); o.u8(0x04);
    o.u8(0x04);                 // disposal 1 (다음 프레임 위에 겹침) · 투명색 없음
    o.u16(delay);
    o.u8(0);                    // 투명 색 인덱스(안 씀)
    o.u8(0);
    o.u8(0x2C);                 // 이미지 디스크립터
    o.u16(0); o.u16(0);
    o.u16(w); o.u16(h);
    o.u8(0);                    // 지역 색표 없음 · 인터레이스 없음
    o.u8(8);                    // LZW 최소 코드 크기
    compress(idx.subarray(0, n), o);
    this.frames++;
    return this.frames;
  };

  Encoder.prototype.finish = function () {
    if (!this.started) this.start();
    this.out.u8(0x3B);
    return this.out.result();
  };

  global.GifEnc = {
    create: function (w, h, opts) { return new Encoder(w, h, opts); },
    medianCut: medianCut
  };
})(window);
