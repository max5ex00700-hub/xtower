/* Depth3D — 화면 조작 (슬라이더·토글·사진 선택·몰입 모드) */
(function (global) {
  'use strict';

  var $ = function (id) { return document.getElementById(id); };
  var STORE_KEY = 'depth3d.settings.v1';

  var S = {
    depth: 0.62, smooth: 0.48, invert: false,
    lightAngle: -32, lightPower: 0.78,
    sway: false, gyro: false, center: false
  };

  var immersive = false;
  var collapsed = false;
  var hintTimer = null;
  var toastTimer = null;
  var smoothTimer = null;
  var samples = global.DEPTH3D_SAMPLES || [];

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
      S.invert = !!o.invert;
      S.sway = !!o.sway;
      S.center = !!o.center;
      S.gyro = false; // 센서는 켜진 채로 시작하지 않는다
    } catch (e) {}
  }

  /* -------------------------------------------------------------- 화면 영역 */

  function viewportArea() {
    var h = global.innerHeight || document.documentElement.clientHeight;
    if (immersive) return { top: 0, bottom: 0 };
    var topEl = $('top');
    var sheet = $('sheet');
    var top = topEl ? topEl.getBoundingClientRect().bottom + 4 : 64;
    var sheetTop = sheet ? sheet.getBoundingClientRect().top : h - 380;
    return {
      top: Math.max(0, top),
      bottom: Math.max(0, h - sheetTop)
    };
  }

  function syncSheetHeight() {
    var sheet = $('sheet');
    if (!sheet) return;
    var h = Math.round(sheet.getBoundingClientRect().height);
    document.documentElement.style.setProperty('--sheet-h', h + 'px');
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

  /* -------------------------------------------------------------- 슬라이더 */

  function fill(el) {
    var min = parseFloat(el.min), max = parseFloat(el.max), v = parseFloat(el.value);
    var p = max > min ? (v - min) / (max - min) * 100 : 0;
    el.style.setProperty('--p', p.toFixed(1) + '%');
  }

  function syncSliders() {
    $('s-depth').value = Math.round(S.depth * 100);
    $('s-smooth').value = Math.round(S.smooth * 100);
    $('s-light').value = Math.round(S.lightAngle);
    $('s-power').value = Math.round(S.lightPower * 100);
    $('v-depth').textContent = Math.round(S.depth * 100) + '%';
    $('v-smooth').textContent = Math.round(S.smooth * 100) + '%';
    $('v-light').textContent = Math.round(S.lightAngle) + '°';
    $('v-power').textContent = Math.round(S.lightPower * 100) + '%';
    ['s-depth', 's-smooth', 's-light', 's-power'].forEach(function (id) { fill($(id)); });
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
    $('t-center').classList.toggle('on', S.center);
    $('t-invert').classList.toggle('on', S.invert);
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

    $('t-center').addEventListener('click', function () {
      S.center = !S.center;
      D3D.setParam('center', S.center);
      syncToggles(); save();
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
    if (!meta) return;
    if (d && d.ready) {
      meta.textContent = '깊이 ' + d.depthMap[0] + '×' + d.depthMap[1] + ' · 변위 메쉬 ' + d.segments[0] + '×' + d.segments[1];
    }
  }

  function onPhotoLoaded(label, w, h) {
    $('photo-name').textContent = label || '내 사진';
    updateMeta();
    var thumb = $('depth-thumb');
    if (thumb && D3D.getDepthPreview) D3D.getDepthPreview(thumb);
    hideLoading();
    hideHint();
    D3D.fit();
  }

  function loadSample(i) {
    var s = samples[i];
    if (!s) return;
    D3D.loadPhoto(s.src, s.name);
  }

  function wireSources() {
    var openBtn = $('btn-open');
    openBtn.addEventListener('click', function () {
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

  /* -------------------------------------------------------------- 몰입·접기 */

  function setImmersive(on, fromNative) {
    immersive = !!on;
    document.body.classList.toggle('immersive', immersive);
    if (global.Android && Android.setImmersive) {
      Android.setImmersive(immersive);
    } else {
      // 일반 브라우저에서는 전체화면으로 대신한다
      try {
        if (immersive && document.documentElement.requestFullscreen) {
          document.documentElement.requestFullscreen();
        } else if (!immersive && document.fullscreenElement && document.exitFullscreen) {
          document.exitFullscreen();
        }
      } catch (e) {}
    }
    if (immersive) hideHint();
    syncSheetHeight();
    setTimeout(function () { D3D.resize(); }, 420);
  }

  function wireImmersive() {
    $('btn-immersive').addEventListener('click', function () { setImmersive(!immersive, false); });
    $('btn-restore').addEventListener('click', function () { setImmersive(false, false); });

    // 몰입 상태에서 화면을 톡 치면 컨트롤이 돌아온다
    var down = null;
    var c = $('gl');
    c.addEventListener('pointerdown', function (e) { down = { x: e.clientX, y: e.clientY, t: performance.now() }; });
    c.addEventListener('pointerup', function (e) {
      if (!down) return;
      var dist = Math.abs(e.clientX - down.x) + Math.abs(e.clientY - down.y);
      var dt = performance.now() - down.t;
      down = null;
      if (immersive && dist < 10 && dt < 320) setImmersive(false, false);
    });
  }

  function wireCollapse() {
    var btn = $('btn-collapse');
    btn.addEventListener('click', function () {
      collapsed = !collapsed;
      document.body.classList.toggle('collapsed', collapsed);
      btn.textContent = collapsed ? '펼치기' : '접기';
      syncSheetHeight();
      setTimeout(function () { D3D.resize(); }, 400);
    });
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
    syncToggles();
    syncSheetHeight();
    wireSliders();
    wireToggles();
    wireSources();
    wireImmersive();
    wireCollapse();

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
    D3D.setSway(S.sway);

    if (samples.length) {
      setTimeout(function () { loadSample(0); }, 80);
    }

    hintTimer = setTimeout(hideHint, 7000);
    global.addEventListener('resize', function () { syncSheetHeight(); });
    if (global.Android && Android.onReady) Android.onReady();
  }

  /* -------------------------------------------------------------- 공개 */

  global.D3DUI = {
    viewportArea: viewportArea,
    showLoading: showLoading,
    hideLoading: hideLoading,
    loadingProgress: loadingProgress,
    toast: toast,
    onPhotoLoaded: onPhotoLoaded,
    setImmersive: setImmersive,
    syncToggles: syncToggles,
    onInteract: hideHint,
    onBackPressed: function () {
      if (immersive) { setImmersive(false, false); return; }
      if (collapsed) { $('btn-collapse').click(); }
    },
    state: S
  };

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', boot);
  } else {
    boot();
  }
})(window);
