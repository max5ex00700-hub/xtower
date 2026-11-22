/* Depth3D — 화면 조작 (플로팅 바·패널·분리 엔진·확대/이동·이미지 저장·브러시 보정) */
(function (global) {
  'use strict';

  var $ = function (id) { return document.getElementById(id); };
  var STORE_KEY = 'depth3d.settings.v2';

  var S = {
    depth: 0.62, smooth: 0.48, invert: false,
    lightAngle: -32, lightPower: 0.78,
    sway: false, gyro: false, center: false,
    mode: 'fixed', engine: 'ai',
    bgLock: 1.0, feather: 0.42, cutoff: 0.50
  };

  var immersive = false;
  var panelOpen = false;
  var hintTimer = null;
  var toastTimer = null;
  var zoomTimer = null;
  var smoothTimer = null;
  var cutoffTimer = null;
  var samples = global.DEPTH3D_SAMPLES || [];
  var rebuilding = false;

  /* -------------------------------------------------------------- 상태 저장 */

  function save() {
    try { localStorage.setItem(STORE_KEY, JSON.stringify(S)); } catch (e) {}
  }

  function load() {
    try {
      var raw = localStorage.getItem(STORE_KEY);
      if (!raw) return;
      var o = JSON.parse(raw);
      if (typeof o.depth === 'number') S.depth = o.depth;
      if (typeof o.smooth === 'number') S.smooth = o.smooth;
      if (typeof o.lightAngle === 'number') S.lightAngle = o.lightAngle;
      if (typeof o.lightPower === 'number') S.lightPower = o.lightPower;
      if (typeof o.bgLock === 'number') S.bgLock = o.bgLock;
      if (typeof o.feather === 'number') S.feather = o.feather;
      if (typeof o.cutoff === 'number') S.cutoff = o.cutoff;
      if (o.mode === 'full' || o.mode === 'fixed') S.mode = o.mode;
      if (o.engine === 'fast' || o.engine === 'ai') S.engine = o.engine;
      S.invert = !!o.invert;
      S.sway = !!o.sway;
      S.center = !!o.center;
      S.gyro = false;
      if (typeof o.brushSize === 'number') brushUI.size = o.brushSize;
    } catch (e) {}
  }

  /* -------------------------------------------------------------- 화면 영역 */

  function viewportArea() {
    var h = global.innerHeight || document.documentElement.clientHeight;
    if (immersive) return { top: 0, bottom: 0 };
    var topEl = $('top');
    var botEl = $('bottom');
    var top = topEl ? topEl.getBoundingClientRect().bottom : 52;
    var botTop = botEl ? botEl.getBoundingClientRect().top : h - 54;
    return {
      top: Math.max(0, Math.round(top)),
      bottom: Math.max(0, Math.round(h - botTop))
    };
  }

  /* -------------------------------------------------------------- 로더·토스트 */

  function showLoading(text) {
    var el = $('loader');
    if (!el) return;
    $('loader-text').textContent = text || '처리 중';
    $('loader-fill').style.width = '0%';
    el.classList.add('on');
  }

  function loadingProgress(p) {
    var f = $('loader-fill');
    if (f) f.style.width = Math.round(Math.max(0, Math.min(1, p)) * 100) + '%';
  }

  function hideLoading() {
    var el = $('loader');
    if (el) el.classList.remove('on');
  }

  function toast(msg) {
    var el = $('toast');
    if (!el) return;
    el.textContent = msg;
    el.classList.add('on');
    clearTimeout(toastTimer);
    toastTimer = setTimeout(function () { el.classList.remove('on'); }, 2400);
  }

  /* -------------------------------------------------------------- 패널 */

  function setPanel(open) {
    panelOpen = !!open;
    document.body.classList.toggle('panel-open', panelOpen);
    var p = $('panel');
    if (p) p.setAttribute('aria-hidden', panelOpen ? 'false' : 'true');
    var b = $('btn-panel');
    if (b) b.classList.toggle('on', panelOpen);
    if (panelOpen) hideHint();
  }

  function closePanel() { if (panelOpen) setPanel(false); }

  /* -------------------------------------------------------------- 슬라이더 */

  function fill(el) {
    if (!el) return;
    var min = parseFloat(el.min), max = parseFloat(el.max), v = parseFloat(el.value);
    var p = max > min ? (v - min) / (max - min) * 100 : 0;
    el.style.setProperty('--p', p.toFixed(1) + '%');
  }

  var RANGES = ['s-depth', 's-smooth', 's-light', 's-power', 's-lock', 's-feather', 's-cutoff'];

  function syncSliders() {
    $('s-depth').value = Math.round(S.depth * 100);
    $('s-smooth').value = Math.round(S.smooth * 100);
    $('s-light').value = Math.round(S.lightAngle);
    $('s-power').value = Math.round(S.lightPower * 100);
    $('s-lock').value = Math.round(S.bgLock * 100);
    $('s-feather').value = Math.round(S.feather * 100);
    $('s-cutoff').value = Math.round(S.cutoff * 100);
    $('v-depth').textContent = Math.round(S.depth * 100) + '%';
    $('v-smooth').textContent = Math.round(S.smooth * 100) + '%';
    $('v-light').textContent = Math.round(S.lightAngle) + '°';
    $('v-power').textContent = Math.round(S.lightPower * 100) + '%';
    $('v-lock').textContent = Math.round(S.bgLock * 100) + '%';
    $('v-feather').textContent = Math.round(S.feather * 100) + '%';
    $('v-cutoff').textContent = Math.round(S.cutoff * 100) + '%';
    RANGES.forEach(function (id) { fill($(id)); });
  }

  function wireSliders() {
    $('s-depth').addEventListener('input', function () {
      var v = +this.value;
      S.depth = v / 100;
      $('v-depth').textContent = v + '%';
      fill(this);
      D3D.setParam('depth', S.depth);
      save();
    });

    $('s-smooth').addEventListener('input', function () {
      var v = +this.value;
      S.smooth = v / 100;
      $('v-smooth').textContent = v + '%';
      fill(this);
      clearTimeout(smoothTimer);
      smoothTimer = setTimeout(function () {
        D3D.setParam('smooth', S.smooth);
        updateMeta();
        save();
      }, 90);
    });

    $('s-light').addEventListener('input', function () {
      var v = +this.value;
      S.lightAngle = v;
      $('v-light').textContent = v + '°';
      fill(this);
      D3D.setParam('lightAngle', v);
      save();
    });

    $('s-power').addEventListener('input', function () {
      var v = +this.value;
      S.lightPower = v / 100;
      $('v-power').textContent = v + '%';
      fill(this);
      D3D.setParam('lightPower', S.lightPower);
      save();
    });

    $('s-lock').addEventListener('input', function () {
      var v = +this.value;
      S.bgLock = v / 100;
      $('v-lock').textContent = v + '%';
      fill(this);
      D3D.setParam('bgLock', S.bgLock);
      save();
    });

    $('s-feather').addEventListener('input', function () {
      var v = +this.value;
      S.feather = v / 100;
      $('v-feather').textContent = v + '%';
      fill(this);
      clearTimeout(cutoffTimer);
      cutoffTimer = setTimeout(function () {
        D3D.setParam('feather', S.feather);
        updateMeta();
        save();
      }, 110);
    });

    $('s-cutoff').addEventListener('input', function () {
      var v = +this.value;
      S.cutoff = v / 100;
      $('v-cutoff').textContent = v + '%';
      fill(this);
      clearTimeout(cutoffTimer);
      cutoffTimer = setTimeout(function () {
        D3D.setParam('cutoff', S.cutoff);
        updateMeta();
        save();
      }, 110);
    });
  }

  /* -------------------------------------------------------------- 모드·엔진 */

  function syncMode(mode) {
    S.mode = mode;
    document.body.classList.toggle('mode-fixed', mode === 'fixed');
    document.body.classList.toggle('mode-full', mode === 'full');
    var btns = document.querySelectorAll('.mode-btn');
    for (var i = 0; i < btns.length; i++) {
      btns[i].classList.toggle('on', btns[i].getAttribute('data-mode') === mode);
    }
    var pm = $('p-mode');
    if (pm) pm.querySelector('b').textContent = (mode === 'fixed') ? '배경 고정' : '전체 2.5D';
    var hint = $('hint');
    if (hint) {
      hint.innerHTML = (mode === 'fixed')
        ? '<span class="hint-dot"></span>두 손가락 확대 · 끌어서 회전'
        : '<span class="hint-dot"></span>두 손가락 확대 · 끌어서 기울이기';
    }
    setTimeout(function () { D3D.resize(); }, 60);
  }

  function syncEngine() {
    var live = (global.D3D && D3D.getState) ? D3D.getState() : S;
    S.engine = live.engine || S.engine;
    var btns = document.querySelectorAll('.engine-btn');
    for (var i = 0; i < btns.length; i++) {
      btns[i].classList.toggle('on', btns[i].getAttribute('data-engine') === S.engine);
    }
    var pe = $('p-engine');
    if (pe) {
      pe.querySelector('b').textContent = (S.engine === 'ai') ? 'AI 분리' : '고속 분리';
      pe.classList.toggle('mint', true);
      pe.classList.toggle('on', S.engine === 'ai');
    }
  }

  function aiStatus(kind, msg) {
    var el = $('eng-status');
    if (!el) return;
    el.textContent = msg || '';
    el.classList.remove('busy', 'err');
    if (kind === 'loading') el.classList.add('busy');
    if (kind === 'error') el.classList.add('err');
  }

  function wireModes() {
    var btns = document.querySelectorAll('.mode-btn');
    for (var i = 0; i < btns.length; i++) {
      (function (btn) {
        btn.addEventListener('click', function () {
          var m = btn.getAttribute('data-mode');
          if (m === S.mode) return;
          syncMode(m);
          D3D.setMode(m);
          save();
          hideHint();
          toast(m === 'fixed' ? '배경을 고정하고 피사체만 3D로 띄웁니다' : '사진 전체를 2.5D 카드로 띄웁니다');
        });
      })(btns[i]);
    }

    var ebtns = document.querySelectorAll('.engine-btn');
    for (var k = 0; k < ebtns.length; k++) {
      (function (btn) {
        btn.addEventListener('click', function () {
          var e = btn.getAttribute('data-engine');
          if (e === S.engine) return;
          S.engine = e;
          D3D.setEngine(e);      // 먼저 뷰어 상태를 바꾸고
          syncEngine();          // 그 다음 화면을 맞춘다 (반대 순서면 상태가 되돌아간다)
          save();
          if (e === 'fast') aiStatus('idle', '고속 마스크 · 즉시 분리');
          hideHint();
        });
      })(ebtns[k]);
    }

    $('p-mode').addEventListener('click', function () {
      var m = (S.mode === 'fixed') ? 'full' : 'fixed';
      syncMode(m);
      D3D.setMode(m);
      save();
      hideHint();
      toast(m === 'fixed' ? '배경 고정 · 피사체만 3D' : '전체 2.5D');
    });

    $('p-engine').addEventListener('click', function () {
      var live = (global.D3D && D3D.getState) ? D3D.getState() : S;
      var e = (live.engine === 'ai') ? 'fast' : 'ai';
      S.engine = e;
      D3D.setEngine(e);
      syncEngine();
      save();
      hideHint();
      toast(e === 'ai' ? 'AI 정밀 분리 (u2netp)' : '고속 분리 · 깊이 컷오프');
    });

    $('p-center').addEventListener('click', function () {
      D3D.resetView();
      hideHint();
      toast('회전·확대·이동을 처음 상태로');
    });

    $('p-brush').addEventListener('click', function () { openBrush(); });
    $('t-brush').addEventListener('click', function () { openBrush(); });
  }

  /* -------------------------------------------------------------- 토글 */

  function syncToggles() {
    var live = (global.D3D && D3D.getState) ? D3D.getState() : S;
    S.sway = !!live.sway;
    S.gyro = !!live.gyro;
    S.center = !!live.center;
    S.invert = !!live.invert;
    $('t-sway').classList.toggle('on', S.sway);
    $('t-gyro').classList.toggle('on', S.gyro);
    $('t-invert').classList.toggle('on', S.invert);
    var tb = $('t-brush');
    if (tb) tb.classList.toggle('on', brushUI.on);
  }

  function wireToggles() {
    $('t-sway').addEventListener('click', function () {
      S.sway = !S.sway;
      D3D.setSway(S.sway);
      syncToggles(); save(); hideHint();
    });

    $('t-gyro').addEventListener('click', function () {
      S.gyro = !S.gyro;
      D3D.setGyro(S.gyro);
      syncToggles(); save(); hideHint();
      toast(S.gyro ? '폰을 기울여 감상하세요' : '기울기 센서를 껐습니다');
    });

    $('t-invert').addEventListener('click', function () {
      S.invert = !S.invert;
      D3D.setParam('invert', S.invert);
      syncToggles(); save();
    });
  }

  /* -------------------------------------------------------------- 사진 */

  function updateMeta() {
    var d = D3D.debug ? D3D.debug() : null;
    var meta = $('photo-meta');
    if (!meta || !d) return;
    if (!d.ready) { meta.textContent = '기기 안에서 깊이를 계산합니다'; return; }
    if (d.mode === 'fixed') {
      var eng = d.maskEngine === 'ai' ? 'AI 피사체 분리' : '고속 분리';
      var extra = [];
      if (d.contourPoints) extra.push('실루엣 ' + d.contourPoints.toLocaleString('ko-KR') + '점');
      if (d.bgMs) extra.push('배경 채움 ' + d.bgMs + 'ms');
      if (d.aiStatus && d.aiStatus.refineMs) extra.push('경계 보정 ' + d.aiStatus.refineMs + 'ms');
      meta.textContent = eng + ' · ' + (d.aiStatus && d.aiStatus.lastMs ? (d.aiStatus.lastMs / 1000).toFixed(1) + 's · ' : '') +
        (extra.length ? extra.join(' · ') : '배경 고정');
    } else {
      meta.textContent = '깊이 ' + d.depthMap[0] + '×' + d.depthMap[1] + ' · 변위 메쉬 ' + d.segments[0] + '×' + d.segments[1];
    }
  }

  function updateThumb() {
    var thumb = $('depth-thumb');
    if (!thumb) return;
    var d = D3D.debug ? D3D.debug() : null;
    if (d && d.mode === 'fixed' && d.hasMask && D3D.getMaskPreview) D3D.getMaskPreview(thumb);
    else if (D3D.getDepthPreview) D3D.getDepthPreview(thumb);
  }

  function onPhotoLoaded(label, w, h) {
    $('photo-name').textContent = label || '내 사진';
    updateMeta();
    updateThumb();
    hideLoading();
    hideHint();
    D3D.fit();
  }

  function onRebuilt() {
    updateMeta();
    updateThumb();
  }

  function onModeChanged(mode) {
    syncMode(mode);
    updateMeta();
    updateThumb();
  }

  function loadSample(i) {
    var s = samples[i];
    if (!s) return;
    closePanel();
    D3D.loadPhoto(s.src, s.name);
  }

  function wireSources() {
    $('btn-open').addEventListener('click', function () {
      if (global.Android && Android.pickImage) {
        Android.pickImage();
      } else {
        $('file').click();
      }
    });

    $('file').addEventListener('change', function () {
      var f = this.files && this.files[0];
      if (!f) return;
      var reader = new FileReader();
      reader.onload = function () { D3D.loadPhoto(reader.result, f.name.replace(/\.[^.]+$/, '')); };
      reader.readAsDataURL(f);
      this.value = '';
    });

    var btns = document.querySelectorAll('.sample');
    for (var i = 0; i < btns.length; i++) {
      (function (btn) {
        btn.addEventListener('click', function () { loadSample(+btn.getAttribute('data-sample')); });
      })(btns[i]);
    }

    for (var k = 0; k < Math.min(2, samples.length); k++) {
      var t = $('thumb-' + k);
      if (t && samples[k].thumb) t.style.backgroundImage = 'url("' + samples[k].thumb + '")';
    }
  }

  /* -------------------------------------------------------------- 확대 배지 */

  function onZoom(z) {
    var el = $('zoom-badge');
    if (!el) return;
    el.textContent = (Math.round((z || 1) * 10) / 10).toFixed(1) + '×';
    el.classList.add('on');
    clearTimeout(zoomTimer);
    zoomTimer = setTimeout(function () { el.classList.remove('on'); }, 850);
  }

  /* -------------------------------------------------------------- 📸 이미지 저장 */

  function stamp() {
    var d = new Date();
    var p = function (n) { return (n < 10 ? '0' : '') + n; };
    return 'Depth3D_' + d.getFullYear() + p(d.getMonth() + 1) + p(d.getDate()) + '_' +
      p(d.getHours()) + p(d.getMinutes()) + p(d.getSeconds());
  }

  function humanBytes(n) {
    if (!n) return '0 KB';
    if (n > 1024 * 1024) return (n / 1024 / 1024).toFixed(1) + ' MB';
    return Math.round(n / 1024) + ' KB';
  }

  function saveImage(silent) {
    if (!global.D3D || !D3D.capture) { toast('3D 화면이 아직 준비되지 않았습니다'); return null; }
    var res;
    try {
      res = D3D.capture();
    } catch (e) {
      console.error(e);
      toast('화면을 저장하지 못했습니다');
      return null;
    }
    if (!res || !res.dataUrl || res.dataUrl.indexOf('data:image/png') !== 0 || res.bytes < 2048) {
      toast('PNG 를 만들지 못했습니다');
      return null;
    }
    var name = stamp();

    /* APK: MediaStore(Pictures/Depth3D) 로 바로 저장 — base64 를 조각으로 넘겨 브리지 부담을 줄인다 */
    if (global.Android && Android.savePngBegin) {
      try {
        Android.savePngBegin(name);
        var CHUNK = 192 * 1024;
        for (var i = 0; i < res.dataUrl.length; i += CHUNK) {
          Android.savePngPart(res.dataUrl.substr(i, CHUNK));
        }
        var r = String(Android.savePngEnd());
        if (r.indexOf('ok') === 0) {
          if (!silent) toast('갤러리에 저장했습니다 · Pictures/Depth3D (' + humanBytes(res.bytes) + ')');
          return { ok: true, name: name, width: res.width, height: res.height, bytes: res.bytes, target: r };
        }
        if (!silent) toast('저장 실패: ' + r);
        return { ok: false, error: r };
      } catch (e) {
        console.error(e);
        if (!silent) toast('저장 중 오류가 났습니다');
        return { ok: false, error: String(e) };
      }
    }

    /* 웹: <a download> 로 PNG 내려받기 */
    var a = document.createElement('a');
    a.href = res.dataUrl;
    a.download = name + '.png';
    a.rel = 'noopener';
    document.body.appendChild(a);
    a.click();
    setTimeout(function () { if (a.parentNode) a.parentNode.removeChild(a); }, 400);
    if (!silent) toast('PNG 저장 · ' + res.width + '×' + res.height + ' (' + humanBytes(res.bytes) + ')');
    return { ok: true, name: name, width: res.width, height: res.height, bytes: res.bytes, url: res.dataUrl };
  }

  function wireSave() {
    $('btn-save').addEventListener('click', function () { saveImage(false); });
    var b = $('btn-save-panel');
    if (b) b.addEventListener('click', function () { saveImage(false); });
  }

  /* -------------------------------------------------------------- 브러시 보정 */

  var brushUI = {
    on: false, mode: 'add', size: 26,
    painting: false, last: null, maskW: 0, maskH: 0,
    rect: null, scale: 1
  };
  var brushFrame, brushPhoto, brushStroke, brushPhotoCtx, brushStrokeCtx;

  function layoutBrush(rect) {
    brushUI.rect = rect;
    var dpr = Math.min(global.devicePixelRatio || 1, 2);
    brushFrame.style.left = Math.round(rect.x) + 'px';
    brushFrame.style.top = Math.round(rect.y) + 'px';
    brushFrame.style.width = Math.round(rect.w) + 'px';
    brushFrame.style.height = Math.round(rect.h) + 'px';
    var pw = Math.max(64, Math.round(rect.w * dpr));
    var ph = Math.max(64, Math.round(rect.h * dpr));
    brushPhoto.width = pw; brushPhoto.height = ph;
    brushStroke.width = pw; brushStroke.height = ph;
    brushUI.scale = pw / Math.max(1, brushUI.maskW);
    drawBrushPhoto();
    clearStroke();
  }

  function drawBrushPhoto() {
    if (!brushPhotoCtx) return false;
    return D3D.brushOverlay(brushPhoto);
  }

  function clearStroke() {
    if (!brushStrokeCtx) return;
    brushStrokeCtx.clearRect(0, 0, brushStroke.width, brushStroke.height);
  }

  function openBrush() {
    if (brushUI.on) return;
    if (!global.D3D || !D3D.beginBrush) return;
    var info = D3D.beginBrush();
    if (!info) {
      if (S.mode !== 'fixed') {
        syncMode('fixed');
        D3D.setMode('fixed');
        setTimeout(function () {
          var again = D3D.beginBrush();
          if (again) mountBrush(again);
          else toast('피사체 분리가 끝난 뒤 다시 시도해 주세요');
        }, 420);
        return;
      }
      toast('AI 분리가 끝난 뒤 브러시를 쓸 수 있습니다');
      return;
    }
    mountBrush(info);
  }

  function mountBrush(info) {
    brushUI.on = true;
    brushUI.maskW = info.w;
    brushUI.maskH = info.h;
    brushUI.painting = false;
    brushUI.last = null;
    closePanel();
    var layer = $('brush-layer');
    layer.classList.add('on');
    layer.setAttribute('aria-hidden', 'false');
    layoutBrush(info.rect);
    syncToggles();
    $('brush-hint').textContent = '＋ 살리기 / － 지우기 — 사진 위를 문지르세요';
    toast('브러시로 빠진 부분을 살리고 배경을 지우세요');
  }

  function closeBrushLayer() {
    brushUI.on = false;
    brushUI.painting = false;
    brushUI.last = null;
    var layer = $('brush-layer');
    layer.classList.remove('on');
    layer.setAttribute('aria-hidden', 'true');
    clearStroke();
    syncToggles();
  }

  function finishBrush(apply) {
    if (!brushUI.on) return;
    showLoading(apply ? '브러시 보정 반영 중' : '되돌리는 중');
    setTimeout(function () {
      try {
        if (apply) D3D.applyBrush(); else D3D.cancelBrush();
      } catch (e) {
        console.error(e);
      }
      hideLoading();
      closeBrushLayer();
      updateMeta();
      updateThumb();
      toast(apply ? '브러시 보정을 3D 피사체·배경에 반영했습니다' : '브러시 보정을 취소했습니다');
    }, 60);
  }

  function brushUV(e) {
    var r = brushFrame.getBoundingClientRect();
    var u = (e.clientX - r.left) / Math.max(1, r.width);
    var v = (e.clientY - r.top) / Math.max(1, r.height);
    return { u: Math.max(0, Math.min(1, u)), v: Math.max(0, Math.min(1, v)), raw_u: u, raw_v: v };
  }

  function paintAt(p) {
    var r = brushUI.rect;
    var radiusDisplay = brushUI.size;
    var rMask = radiusDisplay / Math.max(1, r.w) * brushUI.maskW;
    var last = brushUI.last || p;
    D3D.brushPaint(last.u, last.v, p.u, p.v, rMask, brushUI.mode === 'add');

    // 실제 마스크(민트색)를 바로 갱신 — 칠한 자리가 그대로 보인다.
    var now = performance.now();
    if (!brushUI.lastTint || now - brushUI.lastTint > 80) {
      brushUI.lastTint = now;
      drawBrushPhoto();
    }

    // 손가락 밑에 브러시 원을 표시(누적되지 않게 매번 새로 그린다)
    var dpr = brushStroke.width / Math.max(1, r.w);
    var ctx = brushStrokeCtx;
    ctx.clearRect(0, 0, brushStroke.width, brushStroke.height);
    ctx.save();
    ctx.lineWidth = Math.max(1.5, 2 * dpr);
    ctx.strokeStyle = brushUI.mode === 'add' ? 'rgba(78,214,184,0.95)' : 'rgba(232,120,110,0.95)';
    ctx.beginPath();
    ctx.arc(p.u * r.w * dpr, p.v * r.h * dpr, radiusDisplay * dpr, 0, Math.PI * 2);
    ctx.stroke();
    ctx.restore();

    brushUI.last = p;
    brushUI.dirty = true;
  }

  function wireBrush() {
    brushFrame = $('brush-frame');
    brushPhoto = $('brush-photo');
    brushStroke = $('brush-stroke');
    brushPhotoCtx = brushPhoto.getContext('2d');
    brushStrokeCtx = brushStroke.getContext('2d');

    var frame = brushFrame;

    frame.addEventListener('pointerdown', function (e) {
      if (!brushUI.on) return;
      e.preventDefault();
      try { frame.setPointerCapture(e.pointerId); } catch (err) {}
      brushUI.painting = true;
      brushUI.last = null;
      paintAt(brushUV(e));
    });
    frame.addEventListener('pointermove', function (e) {
      if (!brushUI.on || !brushUI.painting) return;
      e.preventDefault();
      paintAt(brushUV(e));
    });
    function endPaint(e) {
      if (!brushUI.painting) return;
      brushUI.painting = false;
      brushUI.last = null;
      brushUI.lastTint = 0;
      drawBrushPhoto();
      clearStroke();
      try { frame.releasePointerCapture(e.pointerId); } catch (err) {}
    }
    frame.addEventListener('pointerup', endPaint);
    frame.addEventListener('pointercancel', endPaint);
    frame.addEventListener('pointerleave', endPaint);

    $('b-add').addEventListener('click', function () {
      brushUI.mode = 'add';
      $('b-add').classList.add('on');
      $('b-erase').classList.remove('on');
    });
    $('b-erase').addEventListener('click', function () {
      brushUI.mode = 'erase';
      $('b-erase').classList.add('on');
      $('b-add').classList.remove('on');
    });
    $('b-size').addEventListener('input', function () {
      brushUI.size = +this.value;
      $('b-size-val').textContent = brushUI.size;
      fill(this);
      S.brushSize = brushUI.size;
      save();
    });
    $('b-done').addEventListener('click', function () { finishBrush(true); });
    $('b-cancel').addEventListener('click', function () { finishBrush(false); });
  }

  /* -------------------------------------------------------------- 몰입·패널 */

  function setImmersive(on) {
    immersive = !!on;
    document.body.classList.toggle('immersive', immersive);
    if (global.Android && Android.setImmersive) {
      Android.setImmersive(immersive);
    } else {
      try {
        if (immersive && document.documentElement.requestFullscreen) {
          document.documentElement.requestFullscreen();
        } else if (!immersive && document.fullscreenElement && document.exitFullscreen) {
          document.exitFullscreen();
        }
      } catch (e) {}
    }
    if (immersive) hideHint();
    setTimeout(function () { D3D.resize(); }, 420);
  }

  function wireImmersive() {
    $('btn-immersive').addEventListener('click', function () { setImmersive(!immersive); });
    $('btn-restore').addEventListener('click', function () { setImmersive(false); });

    var down = null;
    var c = $('gl');
    c.addEventListener('pointerdown', function (e) {
      down = { x: e.clientX, y: e.clientY, t: performance.now() };
      if (panelOpen) closePanel();
    });
    c.addEventListener('pointerup', function (e) {
      if (!down) return;
      var dist = Math.abs(e.clientX - down.x) + Math.abs(e.clientY - down.y);
      var dt = performance.now() - down.t;
      down = null;
      if (immersive && dist < 10 && dt < 320) setImmersive(false);
    });

    $('btn-panel').addEventListener('click', function () { setPanel(!panelOpen); });
    $('btn-panel-close').addEventListener('click', function () { setPanel(false); });
  }

  /* -------------------------------------------------------------- 힌트 */

  function hideHint() {
    var h = $('hint');
    if (h) h.classList.add('gone');
  }

  /* -------------------------------------------------------------- 부팅 */

  function boot() {
    load();
    syncSliders();
    syncMode(S.mode);
    syncToggles();
    syncEngine();
    wireSliders();
    wireModes();
    wireToggles();
    wireSources();
    wireImmersive();
    wireSave();
    wireBrush();
    $('b-size').value = brushUI.size;
    $('b-size-val').textContent = brushUI.size;
    fill($('b-size'));

    if (!global.THREE) {
      toast('3D 엔진을 읽지 못했습니다');
      return;
    }

    try {
      D3D.init();
    } catch (e) {
      console.error(e);
      toast('3D 화면을 열지 못했습니다');
      return;
    }

    D3D.setParam('depth', S.depth);
    D3D.setParam('smooth', S.smooth);
    D3D.setParam('lightAngle', S.lightAngle);
    D3D.setParam('lightPower', S.lightPower);
    D3D.setParam('invert', S.invert);
    D3D.setParam('bgLock', S.bgLock);
    D3D.setParam('feather', S.feather);
    D3D.setParam('cutoff', S.cutoff);
    D3D.setSway(S.sway);
    D3D.setParam('mode', S.mode);
    D3D.setParam('engine', S.engine);

    if (samples.length) {
      setTimeout(function () { loadSample(0); }, 80);
    }

    hintTimer = setTimeout(hideHint, 8000);
    global.addEventListener('resize', function () {
      D3D.resize();
      if (brushUI.on) layoutBrush(D3D.photoRect());
    });
    if (global.visualViewport) {
      global.visualViewport.addEventListener('resize', function () {
        D3D.resize();
        if (brushUI.on) layoutBrush(D3D.photoRect());
      });
    }
    if (global.Android && Android.onReady) Android.onReady();

    /* APK 안드로이드 쪽에서 상태 바 인셋을 넣어 주면 플로팅 바가 그 아래로 내려간다 */
    global.addEventListener('d3d-insets', function (e) {
      var d = e.detail || {};
      if (typeof d.top === 'number') document.documentElement.style.setProperty('--safe-top', d.top + 'px');
      if (typeof d.bottom === 'number') document.documentElement.style.setProperty('--safe-bottom', d.bottom + 'px');
      setTimeout(function () { D3D.resize(); }, 30);
    });
  }

  /* -------------------------------------------------------------- 공개 */

  global.D3DUI = {
    viewportArea: viewportArea,
    showLoading: showLoading,
    hideLoading: hideLoading,
    loadingProgress: loadingProgress,
    toast: toast,
    onPhotoLoaded: onPhotoLoaded,
    onRebuilt: onRebuilt,
    onModeChanged: onModeChanged,
    aiStatus: aiStatus,
    syncEngine: syncEngine,
    syncToggles: syncToggles,
    setImmersive: setImmersive,
    onZoom: onZoom,
    onInteract: hideHint,
    save: saveImage,
    openBrush: openBrush,
    onBackPressed: function () {
      if (brushUI.on) { finishBrush(false); return true; }
      if (immersive) { setImmersive(false); return true; }
      if (panelOpen) { setPanel(false); return true; }
      return false;
    },
    brush: brushUI,
    state: S
  };

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', boot);
  } else {
    boot();
  }
})(window);
