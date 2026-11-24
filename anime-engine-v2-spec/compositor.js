/* =============================================================================
 * compositor.js — Canvas2D 레이어 합성
 * buildRenderLayers() 결과를 앞에서부터 그린다. 슬롯/규칙/색 계산은 하지 않는다.
 * 제작: 포시즌jo
 * ========================================================================== */

const MASTER_W = 1024;
const MASTER_H = 1280;
const EXPORT_SCALE = 2;
const BLEND_PX = 8;

const LAYER_PLACEHOLDER_COLORS = {
  background: '#2a2d3a',
  aura_back: '#3a2a4a',
  hair_back: '#4a3a2a',
  body_base: '#c8a898',
  ears_back: '#c8a898',
  face_base: '#e0c0a8',
  cheek_fx: '#e08090',
  eyes: '#5a8ac0',
  nose: '#a08070',
  mouth: '#c06070',
  eyebrow: '#3a2a22',
  outfit: '#4a5a7a',
  hair_side: '#4a3a2a',
  ears_front: '#c8a898',
  hair_front: '#4a3a2a',
  ahoge: '#4a3a2a',
  glasses: '#808890',
  head_acc: '#c0a050',
  aura_front: '#6a4a8a',
  watermark: '#e4b968',
};

function hexToRgba(hex, alpha) {
  let h = String(hex || '#888').replace('#', '').trim();
  if (h.length === 3) h = h.split('').map((c) => c + c).join('');
  if (!/^[0-9a-f]{6}$/i.test(h)) h = '888888';
  const r = parseInt(h.slice(0, 2), 16);
  const g = parseInt(h.slice(2, 4), 16);
  const b = parseInt(h.slice(4, 6), 16);
  return `rgba(${r},${g},${b},${alpha})`;
}

/**
 * 눈 흰자(고명도) 픽셀을 multiply 틴트 이전 원본으로 되돌린다.
 * 무채색 eyes 파츠에서 L > threshold 인 영역은 홍채만 물들이고 흰자는 순백에 가깝게 유지.
 *
 * @param {Uint8ClampedArray|number[]} dest 틴트된 RGBA (in-place)
 * @param {Uint8ClampedArray|number[]} src  원본 파츠 RGBA
 * @param {{ threshold?: number, fullAt?: number }} [opts]
 * @returns {Uint8ClampedArray|number[]}
 */
export function protectScleraPixels(dest, src, opts = {}) {
  const threshold = opts.threshold == null ? 225 : opts.threshold;
  const fullAt = opts.fullAt == null ? 245 : opts.fullAt;
  const span = Math.max(1, fullAt - threshold);
  const n = Math.min(dest.length, src.length);
  for (let i = 0; i < n; i += 4) {
    if (src[i + 3] < 8) continue;
    const sr = src[i];
    const sg = src[i + 1];
    const sb = src[i + 2];
    // Rec.709 luma, 0–255
    const L = 0.2126 * sr + 0.7152 * sg + 0.0722 * sb;
    if (L <= threshold) continue;
    const t = Math.min(1, (L - threshold) / span);
    dest[i] = Math.round(dest[i] + (sr - dest[i]) * t);
    dest[i + 1] = Math.round(dest[i + 1] + (sg - dest[i + 1]) * t);
    dest[i + 2] = Math.round(dest[i + 2] + (sb - dest[i + 2]) * t);
  }
  return dest;
}

function makeCanvas(w, h) {
  const c = document.createElement('canvas');
  c.width = w;
  c.height = h;
  return c;
}

export class Compositor {
  /**
   * @param {HTMLCanvasElement} canvas
   * @param {object} [manifest]
   */
  constructor(canvas, manifest = null) {
    this.canvas = canvas;
    this.ctx = canvas.getContext('2d', { alpha: true });
    this.manifest = manifest;
    this.imageCache = new Map();
    this.missingWarned = new Set();
    this.onReady = null;
    this._lastLayers = null;
    this._lastOpts = null;
    this._tintTmp = null;   // colored silhouette
    this._partBuf = null;   // fully tinted part (isolated)
    this._bandBuf = null;   // band-scaled output
    this._displayW = null;
    this._displayH = null;
    this._setupCanvas(1);
  }

  setManifest(manifest) {
    this.manifest = manifest;
  }

  _dpr() {
    return (typeof window !== 'undefined' && window.devicePixelRatio) || 1;
  }

  _setupCanvas(scale = 1) {
    const dpr = this._dpr();
    this.canvas.width = Math.round(MASTER_W * scale * dpr);
    this.canvas.height = Math.round(MASTER_H * scale * dpr);
    // CSS가 표시 크기를 담당한다. setDisplaySize()를 쓴 경우에만 인라인 지정.
    if (this._displayW != null && this._displayH != null) {
      this.canvas.style.width = `${this._displayW}px`;
      this.canvas.style.height = `${this._displayH}px`;
    }
    this.ctx.setTransform(dpr * scale, 0, 0, dpr * scale, 0, 0);
  }

  setDisplaySize(cssW, cssH) {
    this._displayW = cssW;
    this._displayH = cssH;
    if (this.canvas) {
      this.canvas.style.width = `${cssW}px`;
      this.canvas.style.height = `${cssH}px`;
    }
  }

  _buf(key) {
    if (!this[key]) this[key] = makeCanvas(MASTER_W, MASTER_H);
    return this[key];
  }

  getImage(src) {
    if (!src) return null;
    const cached = this.imageCache.get(src);
    if (cached === null) return null;
    if (cached && cached.complete && cached.naturalWidth > 0) return cached;
    return null;
  }

  preload(srcs) {
    const unique = [...new Set((srcs || []).filter(Boolean))];
    return Promise.all(unique.map((src) => this._loadOne(src))).then(() => undefined);
  }

  _loadOne(src) {
    if (!src) return Promise.resolve(null);
    if (this.imageCache.has(src)) {
      const img = this.imageCache.get(src);
      if (img === null) return Promise.resolve(null);
      if (img.complete && img.naturalWidth > 0) return Promise.resolve(img);
      return new Promise((resolve) => {
        const done = () => resolve(this.imageCache.get(src));
        img.addEventListener('load', done, { once: true });
        img.addEventListener('error', done, { once: true });
      });
    }
    return new Promise((resolve) => {
      const img = new Image();
      img.decoding = 'async';
      img.onload = () => {
        this.imageCache.set(src, img);
        if (typeof this.onReady === 'function') this.onReady(src);
        resolve(img);
      };
      img.onerror = () => {
        this.imageCache.set(src, null);
        if (!this.missingWarned.has(src)) {
          this.missingWarned.add(src);
          console.warn('[Compositor] 이미지 없음 → 자리표시자:', src);
        }
        if (typeof this.onReady === 'function') this.onReady(src);
        resolve(null);
      };
      this.imageCache.set(src, img);
      img.src = src;
    });
  }

  /**
   * @param {object[]} layers
   * @param {object} [opts]
   */
  render(layers, opts = {}) {
    this._lastLayers = layers;
    this._lastOpts = opts;
    const scale = opts.scale || 1;
    this._setupCanvas(scale);

    const ctx = this.ctx;
    const dpr = this._dpr();
    ctx.setTransform(dpr * scale, 0, 0, dpr * scale, 0, 0);
    ctx.clearRect(0, 0, MASTER_W, MASTER_H);

    if (!opts.transparent) {
      ctx.fillStyle = '#0A0B11';
      ctx.fillRect(0, 0, MASTER_W, MASTER_H);
    }

    const toPreload = [];
    for (const layer of layers || []) {
      if (layer && layer.src) toPreload.push(layer.src);
    }
    this.preload(toPreload);

    for (const layer of layers || []) {
      if (!layer) continue;
      if (opts.transparent && layer.layer === 'background') continue;
      if (layer.visible === false && !opts.debugLayers) continue;
      this._drawLayer(ctx, layer, opts);
    }
  }

  _drawLayer(ctx, layer, opts) {
    const alphaMul = (layer.visible === false && opts.debugLayers) ? 0.2 : 1;
    const opacity = (layer.opacity == null ? 1 : layer.opacity) * alphaMul;

    if (layer.kind === 'text') {
      this._drawWatermark(ctx, layer, opacity);
      return;
    }

    ctx.save();
    ctx.globalAlpha = opacity;

    if (layer.clip === 'right_eye' || layer.clip === 'left_eye') {
      this._applyEyeClip(ctx, layer.clip);
    }

    const tf = layer.transform || {};
    const px = (tf.pivot && tf.pivot[0]) || 0;
    const py = (tf.pivot && tf.pivot[1]) || 0;
    const x = tf.x || 0;
    const y = tf.y || 0;
    const rot = ((tf.rot || 0) * Math.PI) / 180;
    const sx = tf.scaleX == null ? 1 : tf.scaleX;
    const sy = tf.scaleY == null ? 1 : tf.scaleY;

    // pivot 기준 translate → rotate → scale (SPEC 4.1)
    ctx.translate(px + x, py + y);
    ctx.rotate(rot);
    ctx.scale(sx, sy);
    ctx.translate(-px, -py);

    const img = this.getImage(layer.src);
    if (!img) {
      if (layer.src && !this.imageCache.has(layer.src)) this._loadOne(layer.src);
      this._drawPlaceholder(ctx, layer);
    } else if (tf.bands && (layer.layer === 'body_base' || layer.layer === 'outfit')) {
      this._drawBanded(ctx, img, layer);
    } else {
      this._paintPart(ctx, img, layer);
    }

    ctx.restore();
  }

  _applyEyeClip(ctx, clip) {
    const rig = (this.manifest && this.manifest.rig) || {};
    const hc = rig.head_center || [512, 320];
    const midX = hc[0];
    ctx.beginPath();
    if (clip === 'right_eye') ctx.rect(midX, 0, MASTER_W - midX, MASTER_H);
    else ctx.rect(0, 0, midX, MASTER_H);
    ctx.clip();
  }

  _drawWatermark(ctx, layer, opacity) {
    const text = layer.text || '제작: 포시즌jo';
    ctx.save();
    ctx.globalAlpha = opacity == null ? 0.55 : opacity;
    ctx.fillStyle = 'rgba(232,234,240,0.9)';
    ctx.font = '14px "Noto Sans KR", sans-serif';
    ctx.textAlign = 'right';
    ctx.textBaseline = 'bottom';
    ctx.fillText(text, MASTER_W - 24, MASTER_H - 20);
    ctx.restore();
  }

  _drawPlaceholder(ctx, layer) {
    const tint = layer.tint || LAYER_PLACEHOLDER_COLORS[layer.layer] || '#888888';
    let x = 200; let y = 200; let w = 624; let h = 400;
    const name = layer.layer || '';
    if (name === 'background') { x = 0; y = 0; w = MASTER_W; h = MASTER_H; }
    else if (name.startsWith('aura')) { x = 80; y = 80; w = 864; h = 1040; }
    else if (name === 'body_base' || name === 'outfit') { x = 280; y = 480; w = 464; h = 640; }
    else if (['face_base', 'cheek_fx', 'eyes', 'nose', 'mouth', 'eyebrow', 'glasses'].includes(name)) {
      x = 352; y = 180; w = 320; h = 360;
    } else if (name.startsWith('hair') || name === 'ahoge' || name === 'head_acc' || name.startsWith('ears')) {
      x = 280; y = 80; w = 464; h = 520;
    }

    ctx.save();
    ctx.fillStyle = hexToRgba(tint, 0.35);
    ctx.strokeStyle = hexToRgba(tint, 0.75);
    ctx.lineWidth = 2;
    const r = 12;
    ctx.beginPath();
    ctx.moveTo(x + r, y);
    ctx.arcTo(x + w, y, x + w, y + h, r);
    ctx.arcTo(x + w, y + h, x, y + h, r);
    ctx.arcTo(x, y + h, x, y, r);
    ctx.arcTo(x, y, x + w, y, r);
    ctx.closePath();
    ctx.fill();
    ctx.stroke();

    ctx.fillStyle = 'rgba(232,234,240,0.9)';
    ctx.font = '600 18px "Noto Sans KR", sans-serif';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText(String(layer.slot || name), x + w / 2, y + h / 2 - 12);
    ctx.font = '500 13px "Noto Sans KR", sans-serif';
    ctx.fillStyle = 'rgba(138,144,162,0.95)';
    ctx.fillText(name + (layer.id ? ` · ${layer.id}` : ''), x + w / 2, y + h / 2 + 14);
    ctx.restore();
  }

  /** Build tinted part into offscreen buffer, return the canvas. */
  _buildTinted(img, layer) {
    const mode = layer.tintMode || 'multiply';
    const strength = layer.tintStrength == null ? 1 : layer.tintStrength;
    const out = this._buf('_partBuf');
    const o = out.getContext('2d');
    o.setTransform(1, 0, 0, 1, 0, 0);
    o.clearRect(0, 0, MASTER_W, MASTER_H);

    if (mode === 'none' || !layer.tint) {
      o.drawImage(img, 0, 0, MASTER_W, MASTER_H);
      return out;
    }

    const tintTmp = this._buf('_tintTmp');
    const t = tintTmp.getContext('2d');
    t.setTransform(1, 0, 0, 1, 0, 0);
    t.clearRect(0, 0, MASTER_W, MASTER_H);
    t.globalCompositeOperation = 'source-over';
    t.globalAlpha = 1;
    t.drawImage(img, 0, 0, MASTER_W, MASTER_H);
    t.globalCompositeOperation = 'source-in';
    t.fillStyle = layer.tint;
    t.fillRect(0, 0, MASTER_W, MASTER_H);

    if (mode === 'screen' || mode === 'soft-light') {
      o.drawImage(img, 0, 0, MASTER_W, MASTER_H);
      o.globalCompositeOperation = mode;
      o.globalAlpha = strength;
      o.drawImage(tintTmp, 0, 0, MASTER_W, MASTER_H);
      o.globalCompositeOperation = 'source-over';
      o.globalAlpha = 1;
      return out;
    }

    // multiply + highlight pass (SPEC 4.2)
    o.globalCompositeOperation = 'source-over';
    o.globalAlpha = 1;
    o.drawImage(img, 0, 0, MASTER_W, MASTER_H);
    o.globalCompositeOperation = 'multiply';
    o.globalAlpha = strength;
    o.drawImage(tintTmp, 0, 0, MASTER_W, MASTER_H);
    o.globalCompositeOperation = 'lighter';
    o.globalAlpha = 0.18 * strength;
    o.drawImage(img, 0, 0, MASTER_W, MASTER_H);
    o.globalCompositeOperation = 'source-over';
    o.globalAlpha = 1;

    // eyes: 고명도 흰자는 multiply에 물들지 않도록 원본으로 복원
    if (layer.layer === 'eyes') {
      this._protectScleraOnCanvas(o, img);
    }
    return out;
  }

  /**
   * 오프스크린 컨텍스트의 eyes 레이어에 흰자 보호를 적용한다.
   * @param {CanvasRenderingContext2D} o
   * @param {CanvasImageSource} img
   */
  _protectScleraOnCanvas(o, img) {
    let tinted;
    let source;
    try {
      tinted = o.getImageData(0, 0, MASTER_W, MASTER_H);
    } catch (_) {
      return; // tainted canvas 등 — 보호 생략
    }
    const srcBuf = this._buf('_scleraSrc');
    const s = srcBuf.getContext('2d');
    s.setTransform(1, 0, 0, 1, 0, 0);
    s.clearRect(0, 0, MASTER_W, MASTER_H);
    s.drawImage(img, 0, 0, MASTER_W, MASTER_H);
    try {
      source = s.getImageData(0, 0, MASTER_W, MASTER_H);
    } catch (_) {
      return;
    }
    protectScleraPixels(tinted.data, source.data);
    o.putImageData(tinted, 0, 0);
  }

  _paintPart(ctx, img, layer) {
    const buf = this._buildTinted(img, layer);
    ctx.drawImage(buf, 0, 0, MASTER_W, MASTER_H);
  }

  /**
   * 몸통 3밴드 가로 스케일 + 경계 8px 선형 블렌드
   */
  _drawBanded(ctx, img, layer) {
    const bands = layer.transform.bands || { bust: 1, waist: 1, hip: 1 };
    const rig = (this.manifest && this.manifest.rig && this.manifest.rig.bands) || {
      bust: [560, 700],
      waist: [700, 780],
      hip: [780, 920],
    };

    const src = this._buildTinted(img, layer);
    const bandCanvas = this._buf('_bandBuf');
    const bctx = bandCanvas.getContext('2d');
    bctx.setTransform(1, 0, 0, 1, 0, 0);
    bctx.clearRect(0, 0, MASTER_W, MASTER_H);

    const regions = [
      { y0: 0, y1: rig.bust[0], scale: 1 },
      { y0: rig.bust[0], y1: rig.bust[1], scale: bands.bust || 1 },
      { y0: rig.waist[0], y1: rig.waist[1], scale: bands.waist || 1 },
      { y0: rig.hip[0], y1: rig.hip[1], scale: bands.hip || 1 },
      { y0: rig.hip[1], y1: MASTER_H, scale: 1 },
    ];

    for (const reg of regions) {
      const h = reg.y1 - reg.y0;
      if (h <= 0) continue;
      const dw = MASTER_W * reg.scale;
      const dx = (MASTER_W - dw) / 2;
      bctx.drawImage(src, 0, reg.y0, MASTER_W, h, dx, reg.y0, dw, h);
    }

    const boundaries = [rig.bust[0], rig.bust[1], rig.waist[1], rig.hip[1]];
    for (const by of boundaries) {
      this._blendSeam(bctx, src, by, bands, rig);
    }

    ctx.drawImage(bandCanvas, 0, 0, MASTER_W, MASTER_H);
  }

  _blendSeam(bctx, srcCanvas, boundaryY, bands, rig) {
    const y0 = Math.max(0, boundaryY - BLEND_PX);
    const y1 = Math.min(MASTER_H, boundaryY + BLEND_PX);
    const h = y1 - y0;
    if (h <= 0) return;

    const scaleAbove = this._scaleAt(boundaryY - 1, bands, rig);
    const scaleBelow = this._scaleAt(boundaryY, bands, rig);

    for (let i = 0; i < h; i++) {
      const y = y0 + i;
      const t = (y - (boundaryY - BLEND_PX)) / (BLEND_PX * 2);
      const s = scaleAbove + (scaleBelow - scaleAbove) * Math.min(1, Math.max(0, t));
      const dw = MASTER_W * s;
      const dx = (MASTER_W - dw) / 2;
      bctx.clearRect(0, y, MASTER_W, 1);
      bctx.drawImage(srcCanvas, 0, y, MASTER_W, 1, dx, y, dw, 1);
    }
  }

  _scaleAt(y, bands, rig) {
    if (y < rig.bust[0]) return 1;
    if (y < rig.bust[1]) return bands.bust || 1;
    if (y < rig.waist[1]) return bands.waist || 1;
    if (y < rig.hip[1]) return bands.hip || 1;
    return 1;
  }

  /**
   * @param {object} [opts]
   * @returns {string}
   */
  toDataURL(opts = {}) {
    const scale = opts.scale == null ? EXPORT_SCALE : opts.scale;
    const off = makeCanvas(MASTER_W * scale, MASTER_H * scale);
    const octx = off.getContext('2d', { alpha: true });
    octx.setTransform(scale, 0, 0, scale, 0, 0);

    if (!opts.transparent) {
      octx.fillStyle = '#0A0B11';
      octx.fillRect(0, 0, MASTER_W, MASTER_H);
    }

    const layers = opts.layers || this._lastLayers || [];
    const renderOpts = { ...(this._lastOpts || {}), ...opts };

    for (const layer of layers) {
      if (!layer) continue;
      if (opts.transparent && layer.layer === 'background') continue;
      if (layer.visible === false && !opts.debugLayers) continue;
      this._drawLayer(octx, layer, renderOpts);
    }

    return off.toDataURL(opts.type || 'image/png');
  }

  refresh() {
    if (this._lastLayers) this.render(this._lastLayers, this._lastOpts || {});
  }
}

export default Compositor;
