/*
 * Depth3D — 입체 뷰어 (three.js 로컬 번들)
 *
 *  mode = 'full'   : 사진 전체를 카드처럼 2.5D 변위 (v1.0.0 동작)
 *  mode = 'fixed'  : 배경은 평면 고정 + 피사체만 3D 변위·회전 (v1.1.0 신규)
 *                    · 피사체 알파(마스크)로 잘라내고 실루엣에 두께 벽을 세운다
 *                    · 배경은 피사체 자리를 인페인팅한 평면 텍스처
 */
(function (global) {
  'use strict';

  var canvas = document.getElementById('gl');

  var state = {
    depth: 0.62, smooth: 0.48, invert: false,
    lightAngle: -32, lightPower: 0.78,
    sway: false, gyro: false, center: false,
    mode: 'fixed',          // 'full' | 'fixed'
    engine: 'ai',           // 'ai' | 'fast'  (배경 고정 모드의 분리 엔진)
    bgLock: 1.0,            // 배경 고정 강도 1 = 완전 고정
    feather: 0.42,          // 경계 부드러움
    cutoff: 0.50            // 전경 컷오프
  };

  /* 회전(yaw/pitch) + 확대(zoom 0.5~3.5) + 이동(pan, 화면 폭·높이 대비 비율) */
  var ZOOM_MIN = 0.5, ZOOM_MAX = 3.5, PAN_LIMIT = 0.95;
  var view = {
    yaw: 0, pitch: 0, tYaw: 0, tPitch: 0, vYaw: 0, vPitch: 0,
    zoom: 1, tZoom: 1, panX: 0, panY: 0, tPanX: 0, tPanY: 0
  };
  var fitScale = 1, fitY = 0, bgCover = 1;
  var sway = { amp: 0, t: 0 };
  var gyro = { on: false, calYaw: null, calPitch: null, yaw: 0, pitch: 0, tYaw: 0, tPitch: 0, ready: false };

  var renderer, scene, camera, group, subjectRoot, bgRoot;
  var frontMesh = null, wallMesh = null, backMesh = null, bgMesh = null, shadowMesh = null, glow = null;
  var materials = [];
  var photoTex = null, depthTex = null, depthArray = null, normalTex = null, maskTex = null, bgTex = null, shadowTex = null;
  var dispScale = 1;
  var depthInfo = null;
  var planeW = 1, planeH = 1;
  var segX = 0, segY = 0;
  var clock = null;
  var maxAngleY = 0.70, maxAngleX = 0.44;
  var disposeList = [];

  var doc = { img: null, label: '', depth: null, rawMask: null, maskEngine: null, bg: null, shadow: null, gen: 0, aiBusy: false };

  var chunk = null;

  /* ------------------------------------------------------------ shaders */

  var VERT_SUBJECT = [
    'uniform float uStrength;',
    'uniform float uThick;',
    'uniform float uBack;',
    'attribute float aDepth;',
    'attribute float aSide;',
    'varying vec2 vUv;',
    'varying float vDepth;',
    'varying vec3 vViewDir;',
    'void main() {',
    '  vUv = uv;',
    '  vDepth = aDepth;',
    '  vec3 p = position;',
    '  p.z += aDepth * uStrength;',
    '  p.z -= aSide * uThick;',
    '  p.z -= uBack * uThick;',
    '  vec4 mv = modelViewMatrix * vec4(p, 1.0);',
    '  vViewDir = normalize(-mv.xyz);',
    '  gl_Position = projectionMatrix * mv;',
    '}'
  ].join('\n');

  var FRAG_SUBJECT = [
    'uniform sampler2D uPhoto;',
    'uniform sampler2D uNormalTex;',
    'uniform sampler2D uMaskTex;',
    'uniform float uCutout;',
    'uniform float uBack;',
    'uniform float uStrength;',
    'uniform float uTilt;',
    'uniform float uLightAngle;',
    'uniform float uLightPower;',
    'uniform mat3 uNormalMat;',
    'varying vec2 vUv;',
    'varying float vDepth;',
    'varying vec3 vViewDir;',

    'void main() {',
    '  float alpha = 1.0;',
    '  if (uCutout > 0.5) {',
    '    alpha = texture2D(uMaskTex, vUv).a;',
    '    if (alpha < 0.012) discard;',
    '  }',
    '  vec3 albedo = texture2D(uPhoto, vUv).rgb;',
    '  vec3 baked = texture2D(uNormalTex, vUv).xyz * 2.0 - 1.0;',
    '  vec3 nObj = normalize(vec3(baked.x * uTilt, baked.y * uTilt, baked.z));',
    '  vec3 N = normalize(uNormalMat * nObj);',
    '  vec3 V = normalize(vViewDir);',
    '  float az = uLightAngle;',
    '  vec3 L1 = normalize(vec3(sin(az) * 0.90, 0.36, cos(az) * 0.58 + 0.34));',
    '  vec3 L2 = normalize(vec3(-sin(az) * 0.70, -0.28, cos(az) * 0.30 + 0.44));',
    '  float relief = dot(N.xy, L1.xy) + 0.35 * dot(N.xy, L2.xy);',
    '  float pw = uLightPower;',
    '  float shade = 1.0 + relief * 0.74 * pw;',
    '  shade *= mix(0.90, 1.0, smoothstep(0.0, 0.72, vDepth));',
    '  shade = clamp(shade, 0.55, 1.35);',
    '  vec3 H = normalize(L1 + V);',
    '  float spec = max(pow(max(dot(N, H), 0.0), 16.0) - pow(H.z, 16.0), 0.0);',
    '  float fres = pow(1.0 - max(dot(N, V), 0.0), 4.0);',
    '  vec3 key = vec3(1.0, 0.96, 0.88);',
    '  vec3 rim = vec3(0.93, 0.79, 0.46);',
    '  float cliff = smoothstep(0.28, 0.80, length(baked.xy));',
    '  albedo *= mix(1.0, 0.64, cliff);',
    '  vec3 col = albedo * shade;',
    '  col += key * spec * 0.10 * pw;',
    '  col += rim * fres * 0.09 * pw * vDepth;',
    '  vec2 q = vUv - 0.5;',
    '  col *= 1.0 - dot(q, q) * 0.22;',
    '  // 컷아웃 가장자리: 살짝 어둡게 + 금빛 림 (배경과 분리돼 보이게)',
    '  float edge = 1.0 - smoothstep(0.0, 0.55, alpha);',
    '  if (uCutout > 0.5) {',
    '    col *= mix(1.0, 0.86, edge);',
    '    col += vec3(0.93, 0.80, 0.50) * pow(edge, 1.6) * 0.16 * pw;',
    '  }',
    '  col = mix(col, col * 0.34, uBack);',
    '  gl_FragColor = vec4(col, alpha);',
    '}'
  ].join('\n');

  var FRAG_SUBJECT_BACK = FRAG_SUBJECT;

  var VERT_WALL = [
    'uniform float uStrength;',
    'uniform float uThick;',
    'attribute float aDepth;',
    'attribute float aSide;',
    'attribute vec3 aNormal;',
    'varying vec2 vUv;',
    'varying float vDepth;',
    'varying vec3 vN;',
    'varying vec3 vViewDir;',
    'void main() {',
    '  vUv = uv;',
    '  vDepth = aDepth;',
    '  vN = normalize(normalMatrix * aNormal);',
    '  vec3 p = position;',
    '  p.z += aDepth * uStrength - aSide * uThick;',
    '  vec4 mv = modelViewMatrix * vec4(p, 1.0);',
    '  vViewDir = normalize(-mv.xyz);',
    '  gl_Position = projectionMatrix * mv;',
    '}'
  ].join('\n');

  var FRAG_WALL = [
    'uniform sampler2D uPhoto;',
    'uniform float uLightAngle;',
    'uniform float uLightPower;',
    'varying vec2 vUv;',
    'varying float vDepth;',
    'varying vec3 vN;',
    'varying vec3 vViewDir;',
    'void main() {',
    '  vec3 albedo = texture2D(uPhoto, vUv).rgb;',
    '  vec3 N = normalize(gl_FrontFacing ? vN : -vN);',
    '  vec3 V = normalize(vViewDir);',
    '  float az = uLightAngle;',
    '  vec3 L1 = normalize(vec3(sin(az) * 0.88, 0.40, cos(az) * 0.82));',
    '  float ndl = max(dot(N, L1), 0.0);',
    '  float fres = pow(1.0 - max(dot(N, V), 0.0), 2.0);',
    '  vec3 tint = mix(albedo * 0.55, vec3(0.14, 0.16, 0.20), 0.30);',
    '  vec3 col = tint * (0.55 + ndl * 1.15 * uLightPower);',
    '  col += vec3(0.93, 0.79, 0.47) * fres * 0.55 * uLightPower;',
    '  col *= mix(0.60, 1.0, vDepth);',
    '  gl_FragColor = vec4(col, 0.55);',
    '}'
  ].join('\n');

  var VERT_BG = [
    'varying vec2 vUv;',
    'void main() {',
    '  vUv = uv;',
    '  gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);',
    '}'
  ].join('\n');

  var FRAG_BG = [
    'uniform sampler2D uBg;',
    'uniform float uVig;',
    'varying vec2 vUv;',
    'void main() {',
    '  vec2 uv = clamp(vUv, vec2(0.0005), vec2(0.9995));',
    '  vec3 c = texture2D(uBg, uv).rgb;',
    '  vec2 q = vUv - 0.5;',
    '  c *= 1.0 - dot(q, q) * uVig;',
    '  float t = max(abs(q.x), abs(q.y)) * 2.0;',   // 0 가운데 ~ 1 테두리
    '  float a = 1.0 - smoothstep(0.88, 1.0, t);',
    '  if (a < 0.004) discard;',
    '  gl_FragColor = vec4(c, a);',
    '}'
  ].join('\n');

  /* ------------------------------------------------------------ setup */

  function makeGlow() {
    var c = document.createElement('canvas');
    c.width = c.height = 256;
    var g = c.getContext('2d');
    var grd = g.createRadialGradient(128, 128, 10, 128, 128, 128);
    grd.addColorStop(0, 'rgba(233,196,106,0.55)');
    grd.addColorStop(0.45, 'rgba(78,214,184,0.22)');
    grd.addColorStop(1, 'rgba(0,0,0,0)');
    g.fillStyle = grd;
    g.fillRect(0, 0, 256, 256);
    var tex = new THREE.CanvasTexture(c);
    var mat = new THREE.SpriteMaterial({
      map: tex, transparent: true, depthWrite: false, depthTest: false,
      blending: THREE.AdditiveBlending, opacity: 0.30
    });
    var sp = new THREE.Sprite(mat);
    sp.position.set(0, 0, -1.4);
    sp.scale.set(3.4, 3.4, 1);
    sp.renderOrder = -20;
    return sp;
  }

  function init() {
    renderer = new THREE.WebGLRenderer({
      canvas: canvas, antialias: true, alpha: true, powerPreference: 'high-performance',
      preserveDrawingBuffer: true   // 📸 저장: 마지막 렌더를 그대로 PNG 로 뽑기 위해
    });
    renderer.setClearColor(0x000000, 0);
    renderer.setPixelRatio(Math.min(global.devicePixelRatio || 1, 2));

    scene = new THREE.Scene();
    camera = new THREE.PerspectiveCamera(34, 1, 0.05, 100);
    camera.position.set(0, 0, 4.4);

    group = new THREE.Group();
    scene.add(group);

    subjectRoot = new THREE.Group();
    bgRoot = new THREE.Group();
    group.add(bgRoot);
    group.add(subjectRoot);

    glow = makeGlow();
    group.add(glow);

    clock = new THREE.Clock();
    bindPointer();
    resize();
    animate();
  }

  function clearScene() {
    for (var i = 0; i < disposeList.length; i++) {
      var obj = disposeList[i];
      if (!obj) continue;
      if (obj.parent) obj.parent.remove(obj);
      if (obj.geometry) obj.geometry.dispose();
      if (obj.material) obj.material.dispose();
    }
    disposeList.length = 0;
    materials.length = 0;
    frontMesh = wallMesh = backMesh = bgMesh = shadowMesh = null;
  }

  /* ------------------------------------------------------------ textures */

  function makePhotoTexture(img) {
    if (photoTex) photoTex.dispose();
    photoTex = new THREE.Texture(img);
    photoTex.needsUpdate = true;
    photoTex.minFilter = THREE.LinearFilter;
    photoTex.magFilter = THREE.LinearFilter;
    photoTex.generateMipmaps = false;
    photoTex.wrapS = photoTex.wrapT = THREE.ClampToEdgeWrapping;
    return photoTex;
  }

  function buildDepthTexture(depth) {
    var n = depth.w * depth.h;
    if (!depthArray || depthArray.length !== n * 4) depthArray = new Uint8Array(n * 4);
    var d = depth.relief || depth.depth;
    for (var i = 0; i < n; i++) {
      var v = Math.round(d[i] * 255);
      if (v < 0) v = 0; else if (v > 255) v = 255;
      var o = i * 4;
      depthArray[o] = v;
      depthArray[o + 1] = v;
      depthArray[o + 2] = v;
      depthArray[o + 3] = 255;
    }
    if (depthTex) depthTex.dispose();
    depthTex = new THREE.DataTexture(depthArray, depth.w, depth.h, THREE.RGBAFormat, THREE.UnsignedByteType);
    depthTex.minFilter = THREE.LinearFilter;
    depthTex.magFilter = THREE.LinearFilter;
    depthTex.generateMipmaps = false;
    depthTex.needsUpdate = true;
  }

  /* 마스크 배열(행 0 = 사진 위쪽)을 텍스처(행 0 = UV v=0 = 평면 아래쪽)에 맞춰 뒤집어 넣는다. */
  function writeMaskData(data, alpha, w, h) {
    for (var y = 0; y < h; y++) {
      var srcRow = y * w;
      var dstRow = (h - 1 - y) * w;
      for (var x = 0; x < w; x++) {
        var v = Math.round(alpha[srcRow + x] * 255);
        if (v < 0) v = 0; else if (v > 255) v = 255;
        var o = (dstRow + x) * 4;
        data[o] = v; data[o + 1] = v; data[o + 2] = v; data[o + 3] = v;
      }
    }
    return data;
  }

  function buildMaskTexture(alpha, w, h) {
    var n = w * h;
    var data = writeMaskData(new Uint8Array(n * 4), alpha, w, h);
    if (maskTex) maskTex.dispose();
    maskTex = new THREE.DataTexture(data, w, h, THREE.RGBAFormat, THREE.UnsignedByteType);
    maskTex.minFilter = THREE.LinearFilter;
    maskTex.magFilter = THREE.LinearFilter;
    maskTex.generateMipmaps = false;
    maskTex.needsUpdate = true;
    return maskTex;
  }

  function buildNormalTexture(depth) {
    if (!segX) return;
    var step = 2;
    var nx = Math.floor(segX / step) + 1;
    var ny = Math.floor(segY / step) + 1;
    var data = new Uint8Array(nx * ny * 4);
    var rel = depth.relief || depth.depth;
    var refStrength = 0.22 * 0.55;
    var du = step / segX, dv = step / segY;
    var i, j, k = 0;
    var slopes = new Float32Array(nx * ny * 2);
    var sp = 0;
    for (j = 0; j < ny; j++) {
      var v = Math.min(1, j * dv);
      for (i = 0; i < nx; i++) {
        var u = Math.min(1, i * du);
        var x0 = Math.max(0, u - du), x1 = Math.min(1, u + du);
        var y0 = Math.max(0, v - dv), y1 = Math.min(1, v + dv);
        var hL = DepthEngine.sample(rel, depth.w, depth.h, x0, 1 - v);
        var hR = DepthEngine.sample(rel, depth.w, depth.h, x1, 1 - v);
        var hD = DepthEngine.sample(rel, depth.w, depth.h, u, 1 - y0);
        var hU = DepthEngine.sample(rel, depth.w, depth.h, u, 1 - y1);
        var dzdx = (hR - hL) / ((x1 - x0) * planeW) * refStrength;
        var dzdy = (hU - hD) / ((y1 - y0) * planeH) * refStrength;
        slopes[sp++] = Math.abs(dzdx);
        slopes[sp++] = Math.abs(dzdy);
        dzdx = dzdx / (1 + Math.abs(dzdx) * 1.4);
        dzdy = dzdy / (1 + Math.abs(dzdy) * 1.4);
        var len = Math.sqrt(dzdx * dzdx + dzdy * dzdy + 1);
        data[k] = Math.round((-dzdx / len * 0.5 + 0.5) * 255);
        data[k + 1] = Math.round((-dzdy / len * 0.5 + 0.5) * 255);
        data[k + 2] = Math.round((1 / len * 0.5 + 0.5) * 255);
        data[k + 3] = 255;
        k += 4;
      }
    }
    var sorted = Array.prototype.slice.call(slopes);
    sorted.sort(function (a, b) { return a - b; });
    var p96 = sorted[Math.floor(sorted.length * 0.96)] || 0.6;
    dispScale = Math.max(0.50, Math.min(1.40, 0.80 / Math.max(0.05, p96)));

    if (normalTex) normalTex.dispose();
    normalTex = new THREE.DataTexture(data, nx, ny, THREE.RGBAFormat, THREE.UnsignedByteType);
    normalTex.minFilter = THREE.LinearFilter;
    normalTex.magFilter = THREE.LinearFilter;
    normalTex.generateMipmaps = false;
    normalTex.needsUpdate = true;
    return normalTex;
  }

  /* ------------------------------------------------------------ uniforms */

  function makeUniforms(back) {
    var strength = 0.22 * dispScale * state.depth * (state.invert ? -1 : 1);
    return {
      uPhoto: { value: photoTex },
      uDepthTex: { value: depthTex },
      uNormalTex: { value: normalTex },
      uMaskTex: { value: maskTex },
      uTexel: { value: new THREE.Vector2(1 / (depthInfo ? depthInfo.w : 512), 1 / (depthInfo ? depthInfo.h : 512)) },
      uSize: { value: new THREE.Vector2(planeW, planeH) },
      uStrength: { value: strength },
      uTilt: { value: state.depth * (state.invert ? -1 : 1) },
      uThick: { value: 0.014 + 0.024 * state.depth },
      uLightAngle: { value: state.lightAngle * Math.PI / 180 },
      uLightPower: { value: 0.25 + 1.15 * state.lightPower },
      uNormalMat: { value: new THREE.Matrix3() },
      uCutout: { value: maskTex ? 1 : 0 },
      uBack: { value: back ? 1 : 0 },
      uBg: { value: bgTex },
      uVig: { value: 0.34 }
    };
  }

  function addMaterial(mat) { materials.push(mat); return mat; }

  /* ------------------------------------------------------------ grid */

  function buildGrid(depth) {
    var nx = segX + 1, ny = segY + 1, count = nx * ny;
    var pos = new Float32Array(count * 3);
    var uvs = new Float32Array(count * 2);
    var ad = new Float32Array(count);
    var side = new Float32Array(count);
    var i, j, k = 0, t = 0;
    var disp = depth.relief || depth.depth;

    for (j = 0; j < ny; j++) {
      var v = j / segY;
      for (i = 0; i < nx; i++) {
        var u = i / segX;
        pos[k] = (u - 0.5) * planeW;
        pos[k + 1] = (v - 0.5) * planeH;
        pos[k + 2] = 0;
        k += 3;
        uvs[t] = u;
        uvs[t + 1] = v;
        t += 2;
        ad[j * nx + i] = DepthEngine.sample(disp, depth.w, depth.h, u, 0.5 + (0.5 - v));
      }
    }

    var indices = new Uint32Array(segX * segY * 6);
    var p = 0;
    for (j = 0; j < segY; j++) {
      for (i = 0; i < segX; i++) {
        var a = j * nx + i, b = a + 1, c = a + nx, d2 = c + 1;
        indices[p++] = a; indices[p++] = b; indices[p++] = c;
        indices[p++] = b; indices[p++] = d2; indices[p++] = c;
      }
    }

    var geo = new THREE.BufferGeometry();
    geo.setAttribute('position', new THREE.BufferAttribute(pos, 3));
    geo.setAttribute('uv', new THREE.BufferAttribute(uvs, 2));
    geo.setAttribute('aDepth', new THREE.BufferAttribute(ad, 1));
    geo.setAttribute('aSide', new THREE.BufferAttribute(side, 1));
    geo.setIndex(new THREE.BufferAttribute(indices, 1));
    geo.computeBoundingSphere();
    return geo;
  }

  function borderPath() {
    var border = [];
    var i, j;
    for (i = 0; i < segX; i++) border.push([i, 0]);
    for (j = 0; j < segY; j++) border.push([segX, j]);
    for (i = segX; i > 0; i--) border.push([i, segY]);
    for (j = segY; j > 0; j--) border.push([0, j]);
    return border;
  }

  /* 사진 테두리를 따라 세운 카드 두께(전체 2.5D 모드) */
  function buildBorderSkirt(depth) {
    var border = borderPath();
    var m = border.length - 1;
    var i;
    var pts = [];
    for (i = 0; i <= m; i++) {
      var gi = border[i][0], gj = border[i][1];
      var u = gi / segX, v = gj / segY;
      pts.push({
        x: (u - 0.5) * planeW,
        y: (v - 0.5) * planeH,
        u: u, v: v,
        d: DepthEngine.sample(depth.relief || depth.depth, depth.w, depth.h, u, 0.5 + (0.5 - v))
      });
    }
    return wallGeometryFromPath(pts, false);
  }

  /* 실루엣(마스크 윤곽)을 따라 세운 피사체 두께 벽 */
  function buildSilhouetteWall(contour, depth, mw, mh) {
    if (!contour || contour.length < 8) return null;
    var n = contour.length;
    var pts = [];
    for (var i = 0; i < n; i++) {
      var u = (contour[i].x + 0.5) / mw;
      var v = 1 - (contour[i].y + 0.5) / mh;
      pts.push({
        x: (u - 0.5) * planeW,
        y: (v - 0.5) * planeH,
        u: u, v: v,
        d: DepthEngine.sample(depth.relief || depth.depth, depth.w, depth.h, u, 1 - v)
      });
    }
    // 감김 방향 정리 (바깥 법선이 일관되게)
    var area = 0;
    for (i = 0; i < n; i++) {
      var p0 = pts[i], p1 = pts[(i + 1) % n];
      area += p0.u * p1.v - p1.u * p0.v;
    }
    if (area < 0) pts.reverse();
    return wallGeometryFromPath(pts, true);
  }

  function wallGeometryFromPath(pts, outward) {
    var m = pts.length;
    var closed = outward;
    var count = closed ? m : m - 1;
    var vpos = new Float32Array(count * 4 * 3);
    var vuv = new Float32Array(count * 4 * 2);
    var vad = new Float32Array(count * 4);
    var vside = new Float32Array(count * 4);
    var vnrm = new Float32Array(count * 4 * 3);
    var t = 0;

    for (var i = 0; i < count; i++) {
      var a = pts[i], b = pts[(i + 1) % m];
      var ex = b.x - a.x, ey = b.y - a.y;
      var el = Math.sqrt(ex * ex + ey * ey) || 1e-5;
      var bnx = ey / el, bny = -ex / el;
      if (outward) {
        // 중심에서 바깥으로 향하는 쪽으로 법선 정렬
        var cxp = (a.x + b.x) * 0.5, cyp = (a.y + b.y) * 0.5;
        if (bnx * cxp + bny * cyp < 0) { bnx = -bnx; bny = -bny; }
      }
      var quad = [[a, 0], [b, 0], [b, 1], [a, 1]];
      for (var q = 0; q < 4; q++) {
        vpos[t * 3] = quad[q][0].x;
        vpos[t * 3 + 1] = quad[q][0].y;
        vpos[t * 3 + 2] = 0;
        vuv[t * 2] = quad[q][0].u;
        vuv[t * 2 + 1] = quad[q][0].v;
        vad[t] = quad[q][0].d;
        vside[t] = quad[q][1];
        vnrm[t * 3] = bnx;
        vnrm[t * 3 + 1] = bny;
        vnrm[t * 3 + 2] = 0.35;
        t++;
      }
    }

    var idxArr = new Uint32Array(count * 6);
    var w = 0;
    for (var j = 0; j < count; j++) {
      var base = j * 4;
      idxArr[w++] = base; idxArr[w++] = base + 1; idxArr[w++] = base + 2;
      idxArr[w++] = base; idxArr[w++] = base + 2; idxArr[w++] = base + 3;
    }

    var geo = new THREE.BufferGeometry();
    geo.setAttribute('position', new THREE.BufferAttribute(vpos, 3));
    geo.setAttribute('uv', new THREE.BufferAttribute(vuv, 2));
    geo.setAttribute('aDepth', new THREE.BufferAttribute(vad, 1));
    geo.setAttribute('aSide', new THREE.BufferAttribute(vside, 1));
    geo.setAttribute('aNormal', new THREE.BufferAttribute(vnrm, 3));
    geo.setIndex(new THREE.BufferAttribute(idxArr, 1));
    geo.computeBoundingSphere();
    return geo;
  }

  /* ------------------------------------------------------------ scene build */

  function buildScene(img, depth, mask) {
    clearScene();

    var iw = img.naturalWidth || img.width;
    var ih = img.naturalHeight || img.height;
    planeW = 1;
    planeH = ih / iw;

    segX = state.mode === 'fixed' ? 240 : 208;
    segY = Math.max(80, Math.min(340, Math.round(segX * planeH)));

    makePhotoTexture(img);
    buildDepthTexture(depth);
    buildNormalTexture(depth);

    if (mask) buildMaskTexture(mask.alpha, mask.w, mask.h);
    else if (maskTex) { maskTex.dispose(); maskTex = null; }

    var grid = buildGrid(depth);

    if (state.mode === 'fixed' && mask) {
      buildFixedScene(depth, mask, grid);
    } else {
      buildFullScene(depth, grid);
    }

    glow.position.set(0, 0, -1.2);
    glow.scale.set(planeW * 3.6, planeH * 2.2, 1);

    depthInfo = depth;
    applyUniforms();
    fit();
  }

  function buildFullScene(depth, grid) {
    var U = makeUniforms(false);
    var frontMat = addMaterial(new THREE.ShaderMaterial({
      uniforms: U, vertexShader: VERT_SUBJECT, fragmentShader: FRAG_SUBJECT, side: THREE.FrontSide
    }));
    frontMesh = new THREE.Mesh(grid, frontMat);
    frontMesh.renderOrder = 3;
    subjectRoot.add(frontMesh);
    disposeList.push(frontMesh);

    var skirtGeo = buildBorderSkirt(depth);
    var skirtMat = addMaterial(new THREE.ShaderMaterial({
      uniforms: U, vertexShader: VERT_WALL, fragmentShader: FRAG_WALL, side: THREE.DoubleSide
    }));
    wallMesh = new THREE.Mesh(skirtGeo, skirtMat);
    wallMesh.renderOrder = 2;
    subjectRoot.add(wallMesh);
    disposeList.push(wallMesh);

    var UB = makeUniforms(true);
    var backMat = addMaterial(new THREE.ShaderMaterial({
      uniforms: UB, vertexShader: VERT_SUBJECT, fragmentShader: FRAG_SUBJECT, side: THREE.BackSide
    }));
    backMesh = new THREE.Mesh(grid, backMat);
    backMesh.renderOrder = 1;
    subjectRoot.add(backMesh);
    disposeList.push(backMesh);

    if (bgMesh) { bgMesh.visible = false; }
  }

  function buildFixedScene(depth, mask, grid) {
    // --- 배경 평면 (고정)
    if (mask.bgCanvas) {
      if (bgTex) bgTex.dispose();
      bgTex = new THREE.CanvasTexture(mask.bgCanvas);
      bgTex.minFilter = THREE.LinearFilter;
      bgTex.magFilter = THREE.LinearFilter;
      bgTex.generateMipmaps = false;
      bgTex.needsUpdate = true;
      var bgMat = addMaterial(new THREE.ShaderMaterial({
        uniforms: { uBg: { value: bgTex }, uVig: { value: 0.34 } },
        vertexShader: VERT_BG, fragmentShader: FRAG_BG,
        transparent: true, depthTest: false, depthWrite: false, side: THREE.FrontSide
      }));
      bgMesh = new THREE.Mesh(new THREE.PlaneGeometry(planeW, planeH), bgMat);
      bgMesh.position.z = -0.002;
      bgMesh.renderOrder = -10;
      bgRoot.add(bgMesh);
      disposeList.push(bgMesh);
    }

    // 피사체 뒤 그림자 (배경에 고정)
    if (mask.shadowCanvas) {
      if (shadowTex) shadowTex.dispose();
      shadowTex = new THREE.CanvasTexture(mask.shadowCanvas);
      shadowTex.minFilter = THREE.LinearFilter;
      shadowTex.magFilter = THREE.LinearFilter;
      shadowTex.generateMipmaps = false;
      shadowTex.needsUpdate = true;
      var shMat = new THREE.MeshBasicMaterial({
        map: shadowTex, transparent: true, depthWrite: false, depthTest: true
      });
      shadowMesh = new THREE.Mesh(new THREE.PlaneGeometry(planeW * 1.02, planeH * 1.02), shMat);
      shadowMesh.position.set(0, -0.012, -0.006);
      shadowMesh.renderOrder = 0;
      bgRoot.add(shadowMesh);
      disposeList.push(shadowMesh);
    }

    var U = makeUniforms(false);
    var frontMat = addMaterial(new THREE.ShaderMaterial({
      uniforms: U, vertexShader: VERT_SUBJECT, fragmentShader: FRAG_SUBJECT,
      side: THREE.FrontSide, transparent: true, depthWrite: true
    }));
    frontMesh = new THREE.Mesh(grid, frontMat);
    frontMesh.renderOrder = 3;
    subjectRoot.add(frontMesh);
    disposeList.push(frontMesh);

    // --- 실루엣 두께 벽
    var wallGeo = buildSilhouetteWall(mask.contour, depth, mask.w, mask.h);
    if (wallGeo) {
      var wallMat = addMaterial(new THREE.ShaderMaterial({
        uniforms: U, vertexShader: VERT_WALL, fragmentShader: FRAG_WALL,
        side: THREE.DoubleSide, transparent: true, depthWrite: false
      }));
      wallMesh = new THREE.Mesh(wallGeo, wallMat);
      wallMesh.renderOrder = 2;
      subjectRoot.add(wallMesh);
      disposeList.push(wallMesh);
    }

    // --- 뒷면 판 (돌렸을 때 뒤에서도 막혀 보이게)
    var UB = makeUniforms(true);
    var backMat = addMaterial(new THREE.ShaderMaterial({
      uniforms: UB, vertexShader: VERT_SUBJECT, fragmentShader: FRAG_SUBJECT_BACK,
      side: THREE.BackSide, transparent: true, depthWrite: true
    }));
    backMesh = new THREE.Mesh(grid, backMat);
    backMesh.renderOrder = 2;
    subjectRoot.add(backMesh);
    disposeList.push(backMesh);
  }

  /* ------------------------------------------------------------ mask/rebuild */

  function maskAlpha() {
    var raw = doc.rawMask;
    if (!raw) return null;
    var alpha = SegEngine.buildAlpha(raw, state.cutoff, state.feather);
    return { w: raw.w, h: raw.h, alpha: alpha, engine: raw.engine };
  }

  var bgMs = 0;

  /* ------------------------------------------------------------ depth refresh */

  function refreshDepth(smooth) {
    if (!DepthEngine.hasImage()) return;
    var d = DepthEngine.refine(smooth);
    if (!d) return;
    depthInfo = d;
    var n = d.w * d.h;
    if (!depthArray || depthArray.length !== n * 4) depthArray = new Uint8Array(n * 4);
    var rel = d.relief || d.depth;
    for (var i = 0; i < n; i++) {
      var v = Math.round(rel[i] * 255);
      var o = i * 4;
      depthArray[o] = depthArray[o + 1] = depthArray[o + 2] = v;
      depthArray[o + 3] = 255;
    }
    if (depthTex && depthTex.image.width === d.w && depthTex.image.height === d.h) {
      depthTex.image.data = depthArray;
      depthTex.needsUpdate = true;
    } else {
      buildDepthTexture(d);
    }
    buildNormalTexture(d);
    doc.depth = d;
    applyUniforms();

    var k;
    if (frontMesh) {
      var geo = frontMesh.geometry;
      var attr = geo.getAttribute('aDepth');
      var uv = geo.getAttribute('uv');
      for (k = 0; k < attr.count; k++) {
        attr.array[k] = DepthEngine.sample(d.relief || d.depth, d.w, d.h, uv.array[k * 2], 0.5 + (0.5 - uv.array[k * 2 + 1]));
      }
      attr.needsUpdate = true;
      if (backMesh && backMesh.geometry === geo) { /* 같은 지오메트리 공유 */ }
    }
    if (wallMesh) {
      var g2 = wallMesh.geometry;
      var a2 = g2.getAttribute('aDepth');
      var u2 = g2.getAttribute('uv');
      for (k = 0; k < a2.count; k++) {
        a2.array[k] = DepthEngine.sample(d.relief || d.depth, d.w, d.h, u2.array[k * 2], 1 - u2.array[k * 2 + 1]);
      }
      a2.needsUpdate = true;
    }
  }

  function applyUniforms() {
    var strength = 0.22 * dispScale * state.depth * (state.invert ? -1 : 1);
    var thick = 0.014 + 0.024 * state.depth;
    var lightAngle = state.lightAngle * Math.PI / 180;
    var lightPower = 0.22 + 1.18 * state.lightPower;
    for (var i = 0; i < materials.length; i++) {
      var u = materials[i].uniforms;
      if (!u) continue;
      if (u.uStrength) u.uStrength.value = strength;
      if (u.uTilt) u.uTilt.value = state.depth * (state.invert ? -1 : 1);
      if (u.uThick) u.uThick.value = thick * (state.mode === 'fixed' ? 0.62 : 1.0);
      if (u.uLightAngle) u.uLightAngle.value = lightAngle;
      if (u.uLightPower) u.uLightPower.value = lightPower;
      if (u.uMaskTex) u.uMaskTex.value = maskTex;
      if (u.uCutout) u.uCutout.value = maskTex ? 1 : 0;
      if (u.uBg) u.uBg.value = bgTex;
    }
    if (bgMesh) bgMesh.position.z = -0.002;
  }

  /* ------------------------------------------------------------ layout */

  function resize() {
    if (!renderer) return;
    var w = canvas.clientWidth || global.innerWidth;
    var h = canvas.clientHeight || global.innerHeight;
    renderer.setSize(w, h, false);
    camera.aspect = w / h;
    camera.updateProjectionMatrix();
    fit();
  }

  function area() {
    if (global.D3DUI && global.D3DUI.viewportArea) return global.D3DUI.viewportArea();
    return { top: 0, bottom: 0 };
  }

  function fit() {
    if (!camera || !group) return;
    var w = canvas.clientWidth || global.innerWidth;
    var h = canvas.clientHeight || global.innerHeight;
    var a = area();
    var viewH = 2 * Math.tan(camera.fov * Math.PI / 360) * camera.position.z;
    var viewW = viewH * camera.aspect;

    var availH = Math.max(150, h - a.top - a.bottom);
    // 폭·높이를 모두 채우는 배율 — 3D 화면이 상하좌우 빈틈 없이 꽉 찬다.
    var s = Math.min((viewW * 0.98) / planeW, (availH / h) * viewH * 0.94 / planeH);
    fitScale = s;

    var centerPx = a.top + availH * 0.5;
    var ndc = 1 - 2 * (centerPx / h);
    fitY = ndc * viewH * 0.5;
    applyView();
  }

  /* 줌·패닝을 반영한 장면 배치 */
  function applyView() {
    if (!camera || !group) return;
    var viewH = 2 * Math.tan(camera.fov * Math.PI / 360) * camera.position.z;
    var viewW = viewH * camera.aspect;
    var s = Math.max(0.0001, fitScale * view.zoom);
    group.scale.setScalar(s);
    group.position.set(view.panX * viewW, fitY + view.panY * viewH, 0);
    /* 배경 고정 모드: 인페인팅한 배경 평면을 화면 밖까지 늘여 깔아
       사진 사각형 테두리가 보이지 않게 한다 (20:9 풀블리드). */
    /* 셰이더가 평면 테두리에서 투명해지므로 1.2배 더 늘려 페이드 구간을 화면 밖으로 밀어낸다. */
    bgCover = (state.mode === 'fixed')
      ? 1.2 * Math.max(viewW / (planeW * s), viewH / (planeH * s))
      : 1;
  }

  /* ------------------------------------------------------------ interaction */

  var drag = { active: false, id: null, x: 0, y: 0, t: 0, moved: false };
  var pointers = {};
  var pinch = null;

  function clamp(v, a, b) { return v < a ? a : (v > b ? b : v); }

  function pointerIds() {
    var out = [];
    for (var k in pointers) if (pointers.hasOwnProperty(k)) out.push(k);
    return out;
  }

  function pinchStart() {
    var ids = pointerIds();
    if (ids.length < 2) return null;
    var a = pointers[ids[0]], b = pointers[ids[1]];
    var dx = b.x - a.x, dy = b.y - a.y;
    return {
      dist: Math.max(1, Math.sqrt(dx * dx + dy * dy)),
      mx: (a.x + b.x) * 0.5,
      my: (a.y + b.y) * 0.5,
      zoom: view.tZoom, panX: view.tPanX, panY: view.tPanY
    };
  }

  /* 두 손가락: 핀치 확대/축소 + 두 손가락 드래그 이동 */
  function pinchMove() {
    var ids = pointerIds();
    if (!pinch || ids.length < 2) return;
    var a = pointers[ids[0]], b = pointers[ids[1]];
    var dx = b.x - a.x, dy = b.y - a.y;
    var dist = Math.max(1, Math.sqrt(dx * dx + dy * dy));
    var mx = (a.x + b.x) * 0.5, my = (a.y + b.y) * 0.5;
    var w = canvas.clientWidth || global.innerWidth || 1;
    var h = canvas.clientHeight || global.innerHeight || 1;
    view.tZoom = clamp(pinch.zoom * (dist / pinch.dist), ZOOM_MIN, ZOOM_MAX);
    view.tPanX = clamp(pinch.panX + (mx - pinch.mx) / w, -PAN_LIMIT, PAN_LIMIT);
    view.tPanY = clamp(pinch.panY - (my - pinch.my) / h, -PAN_LIMIT, PAN_LIMIT);
    if (global.D3DUI && global.D3DUI.onZoom) global.D3DUI.onZoom(view.tZoom);
  }

  function bindPointer() {
    canvas.addEventListener('pointerdown', function (e) {
      if (e.pointerType === 'mouse' && e.button !== undefined && e.button !== 0) return;
      if (e.pointerType === 'mouse') { pointers = {}; pinch = null; }  // 남아 있는 터치 상태 정리
      pointers[e.pointerId] = { x: e.clientX, y: e.clientY };
      try { canvas.setPointerCapture(e.pointerId); } catch (err) {}
      var ids = pointerIds();
      if (ids.length === 1) {
        drag.active = true;
        drag.id = e.pointerId;
        drag.x = e.clientX;
        drag.y = e.clientY;
        drag.t = performance.now();
        drag.moved = false;
        view.vYaw = 0; view.vPitch = 0;
      } else if (ids.length === 2) {
        drag.active = false;
        pinch = pinchStart();
      }
      if (global.D3DUI) global.D3DUI.onInteract();
    });

    canvas.addEventListener('pointermove', function (e) {
      if (e.pointerType === 'mouse' && pinch) { pinch = null; }   // 터치 캡처가 남아 있으면 정리
      if (!pointers[e.pointerId]) return;
      pointers[e.pointerId] = { x: e.clientX, y: e.clientY };
      if (pinch && e.pointerType !== 'mouse') { pinchMove(); return; }
      if (!drag.active || e.pointerId !== drag.id) return;
      var now = performance.now();
      var dt = Math.max(8, now - drag.t) / 1000;
      var dx = e.clientX - drag.x;
      var dy = e.clientY - drag.y;
      drag.x = e.clientX;
      drag.y = e.clientY;
      drag.t = now;
      if (Math.abs(dx) + Math.abs(dy) > 1.2) drag.moved = true;

      var kY = 0.0036, kX = 0.0028;
      view.tYaw = clamp(view.tYaw + dx * kY, -maxAngleY, maxAngleY);
      view.tPitch = clamp(view.tPitch - dy * kX, -maxAngleX, maxAngleX);
      view.vYaw = dx * kY / dt;
      view.vPitch = -dy * kX / dt;
    });

    function end(e) {
      delete pointers[e.pointerId];
      try { canvas.releasePointerCapture(e.pointerId); } catch (err) {}
      if (e.type === 'pointercancel') { pointers = {}; pinch = null; drag.active = false; return; }
      var ids = pointerIds();
      if (ids.length >= 2) { pinch = pinchStart(); return; }
      pinch = null;
      if (ids.length === 1) {
        // 핀치에서 손가락 하나를 뗐다 → 남은 손가락으로 바로 회전 이어가기
        var p = pointers[ids[0]];
        drag.active = true;
        drag.id = +ids[0];
        drag.x = p.x; drag.y = p.y; drag.t = performance.now();
        return;
      }
      if (!drag.active) return;
      drag.active = false;
      if (Math.abs(view.vYaw) < 0.03) view.vYaw = 0;
      if (Math.abs(view.vPitch) < 0.03) view.vPitch = 0;
      view.vYaw = clamp(view.vYaw, -2.2, 2.2);
      view.vPitch = clamp(view.vPitch, -1.6, 1.6);
    }
    canvas.addEventListener('pointerup', end);
    canvas.addEventListener('pointercancel', end);
    canvas.addEventListener('pointerleave', end);

    /* 데스크톱: 마우스 휠 확대·축소 (가운데 기준) */
    canvas.addEventListener('wheel', function (e) {
      e.preventDefault();
      var f = Math.exp(-(e.deltaY || 0) * 0.0016);
      view.tZoom = clamp(view.tZoom * f, ZOOM_MIN, ZOOM_MAX);
      if (global.D3DUI && global.D3DUI.onZoom) global.D3DUI.onZoom(view.tZoom);
      if (global.D3DUI) global.D3DUI.onInteract();
    }, { passive: false });

    global.addEventListener('keydown', function (e) {
      if (!frontMesh) return;
      var step = 0.10;
      if (e.key === 'ArrowLeft') view.tYaw = clamp(view.tYaw - step, -maxAngleY, maxAngleY);
      else if (e.key === 'ArrowRight') view.tYaw = clamp(view.tYaw + step, -maxAngleY, maxAngleY);
      else if (e.key === 'ArrowUp') view.tPitch = clamp(view.tPitch + step, -maxAngleX, maxAngleX);
      else if (e.key === 'ArrowDown') view.tPitch = clamp(view.tPitch - step, -maxAngleX, maxAngleX);
      else return;
      e.preventDefault();
    });

    global.addEventListener('resize', function () { resize(); });
    if (global.visualViewport) global.visualViewport.addEventListener('resize', function () { resize(); });
  }

  /* ------------------------------------------------------------ loop */

  var lastTime = 0;
  var mvMat = new THREE.Matrix4();

  function animate() {
    requestAnimationFrame(animate);
    var now = performance.now();
    var dt = lastTime ? Math.min(0.05, (now - lastTime) / 1000) : 0.016;
    lastTime = now;

    if (!drag.active) {
      view.tYaw += view.vYaw * dt;
      view.tPitch += view.vPitch * dt;
      var decay = Math.pow(0.06, dt);
      view.vYaw *= decay;
      view.vPitch *= decay;
      if (state.center) {
        var k = Math.min(1, dt * 5.2);
        view.tYaw += (0 - view.tYaw) * k;
        view.tPitch += (0 - view.tPitch) * k;
        view.vYaw *= Math.pow(0.02, dt);
        view.vPitch *= Math.pow(0.02, dt);
      }
      view.tYaw = clamp(view.tYaw, -maxAngleY, maxAngleY);
      view.tPitch = clamp(view.tPitch, -maxAngleX, maxAngleX);
    }

    var follow = Math.min(1, dt * 16);
    view.yaw += (view.tYaw - view.yaw) * follow;
    view.pitch += (view.tPitch - view.pitch) * follow;

    // 확대·이동은 조금 더 빠르게 따라가 손가락에 붙는다.
    var followZ = Math.min(1, dt * 22);
    view.zoom += (view.tZoom - view.zoom) * followZ;
    view.panX += (view.tPanX - view.panX) * followZ;
    view.panY += (view.tPanY - view.panY) * followZ;
    applyView();

    var targetAmp = state.sway ? 1 : 0;
    sway.amp += (targetAmp - sway.amp) * Math.min(1, dt * 2.2);
    if (state.sway) sway.t += dt;
    var swayYaw = Math.sin(sway.t * 0.58) * 0.20 + Math.sin(sway.t * 1.31 + 0.7) * 0.035;
    var swayPitch = Math.sin(sway.t * 0.42 + 1.15) * 0.105 + Math.sin(sway.t * 1.07) * 0.02;

    if (state.gyro) {
      var gk = Math.min(1, dt * 7);
      gyro.yaw += (gyro.tYaw - gyro.yaw) * gk;
      gyro.pitch += (gyro.tPitch - gyro.pitch) * gk;
    } else {
      gyro.yaw *= (1 - Math.min(1, dt * 8));
      gyro.pitch *= (1 - Math.min(1, dt * 8));
    }

    var ry = view.yaw + swayYaw * sway.amp + gyro.yaw;
    var rx = view.pitch + swayPitch * sway.amp - gyro.pitch;

    if (subjectRoot) {
      subjectRoot.rotation.y = ry;
      subjectRoot.rotation.x = rx;
    }
    if (bgRoot) {
      var drift = (1 - state.bgLock) * 0.55;
      bgRoot.rotation.y = -ry * drift;
      bgRoot.rotation.x = -rx * drift;
      var zoom = (1 + (1 - state.bgLock) * 0.06) * bgCover;
      if (bgMesh) {
        bgMesh.scale.set(zoom, zoom, 1);
        bgMesh.position.x = -Math.sin(ry) * drift * 0.10;
        bgMesh.position.y = Math.sin(rx) * drift * 0.10;
      }
    }
    if (frontMesh) {
      var u = frontMesh.material.uniforms;
      var ldrift = Math.sin(now * 0.00021) * 0.035 + Math.sin(now * 0.00057) * 0.012;
      u.uLightAngle.value = state.lightAngle * Math.PI / 180 + ldrift;
      camera.updateMatrixWorld();
      camera.matrixWorldInverse.copy(camera.matrixWorld).invert();
      frontMesh.updateMatrixWorld();
      mvMat.multiplyMatrices(camera.matrixWorldInverse, frontMesh.matrixWorld);
      var nm = new THREE.Matrix3().getNormalMatrix(mvMat);
      for (var i = 0; i < materials.length; i++) {
        var uu = materials[i].uniforms;
        if (uu && uu.uNormalMat) uu.uNormalMat.value.copy(nm);
      }
    }
    if (glow && group) {
      glow.position.x = -ry * 0.22;
      glow.position.y = -rx * 0.22;
    }

    if (renderer && scene && camera) renderer.render(scene, camera);
  }

  /* ------------------------------------------------------------ photos */

  function showLoading(text) {
    if (global.D3DUI) global.D3DUI.showLoading(text);
  }
  function hideLoading() {
    if (global.D3DUI) global.D3DUI.hideLoading();
  }

  function loadPhoto(src, label) {
    showLoading('사진 준비 중');
    var img = new Image();
    img.onload = function () {
      showLoading('깊이 맵 계산 중');
      setTimeout(function () {
        try {
          var depth = DepthEngine.build(img, state.smooth);
          if (!depth) throw new Error('depth failed');
          showLoading('입체 메쉬 생성 중');
          setTimeout(function () {
            try {
              doc.img = img;
              doc.label = label || '내 사진';
              doc.depth = depth;
              doc.rawMask = null;
              doc.bg = null;
              doc.gen++;
              if (state.mode === 'fixed') {
                doc.rawMask = SegEngine.depthMask(depth, state.cutoff);
                doc.maskEngine = 'fast';
              }
              buildScene(img, depth, state.mode === 'fixed' ? fixedPack(true) : null);
              if (global.D3DUI) global.D3DUI.onPhotoLoaded(label, img.naturalWidth, img.naturalHeight);
              hideLoading();
              maybeRunAI();
            } catch (e) {
              console.error(e);
              hideLoading();
              if (global.D3DUI) global.D3DUI.toast('입체 변환에 실패했습니다');
            }
          }, 16);
        } catch (e) {
          console.error(e);
          hideLoading();
          if (global.D3DUI) global.D3DUI.toast('깊이 맵을 만들지 못했습니다');
        }
      }, 32);
    };
    img.onerror = function () {
      hideLoading();
      if (global.D3DUI) global.D3DUI.toast('이미지를 열 수 없습니다');
    };
    img.src = src;
  }

  function fixedPack(forceBg) {
    if (!doc.rawMask) return null;
    var alpha = SegEngine.buildAlpha(doc.rawMask, state.cutoff, state.feather);
    var contour = SegEngine.traceContour(alpha, doc.rawMask.w, doc.rawMask.h, 0.5);
    if (!doc.bg || forceBg) {
      var outW = Math.min(1024, Math.max(512, Math.round((doc.img.naturalWidth || 1024) * 0.72)));
      var outH = Math.max(8, Math.round(outW * (doc.img.naturalHeight || 1) / (doc.img.naturalWidth || 1)));
      var bgRes = SegEngine.background(doc.img, alpha, doc.rawMask.w, doc.rawMask.h, outW, outH);
      bgMs = bgRes.ms;
      doc.bg = bgRes.canvas;
      doc.shadow = bgRes.shadow;
    }
    doc.contourCount = contour.length;
    return {
      w: doc.rawMask.w, h: doc.rawMask.h, alpha: alpha,
      contour: contour, bgCanvas: doc.bg, shadowCanvas: doc.shadow, engine: doc.rawMask.engine
    };
  }

  /* ------------------------------------------------------------ AI 분리 */

  function maybeRunAI() {
    if (state.mode !== 'fixed' || state.engine !== 'ai' || doc.aiBusy) return;
    if (doc.maskEngine === 'ai') return;
    runAI();
  }

  function runAI() {
    if (!doc.img || !doc.depth) return;
    if (doc.aiBusy) return;
    if (SegEngine.aiStatus().status === 'error') {
      if (global.D3DUI) global.D3DUI.toast('AI 엔진을 쓸 수 없어 고속 모드로 둡니다');
      return;
    }
    doc.aiBusy = true;
    var gen = doc.gen;
    showLoading('AI 피사체 분리 중');
    if (global.D3DUI) global.D3DUI.aiStatus('loading', 'AI 모델 준비 중');
    setTimeout(function () {
      var t0 = performance.now();
      SegEngine.aiMask(doc.img, doc.depth.w, doc.depth.h, function (msg) {
        if (global.D3DUI) global.D3DUI.aiStatus('loading', msg);
      }).then(function (m) {
        doc.aiBusy = false;
        if (!doc.img || doc.gen !== gen) { maybeRunAI(); return; }   // 사진이 바뀌었으면 새 사진으로 다시
        if (state.engine !== 'ai' || state.mode !== 'fixed') {       // 그 사이 사용자가 고속/전체로 바꿨다
          hideLoading();
          if (global.D3DUI) global.D3DUI.aiStatus('idle', 'AI 분리 결과를 보류했습니다');
          return;
        }
        doc.rawMask = m;
        doc.maskEngine = 'ai';
        var pack = fixedPack(true);
        if (!pack) return;
        buildScene(doc.img, doc.depth, pack);
        hideLoading();
        if (global.D3DUI && global.D3DUI.onRebuilt) global.D3DUI.onRebuilt('fixed', 'ai');
        var total = Math.round(performance.now() - t0);
        if (global.D3DUI) {
          global.D3DUI.aiStatus('ready', 'AI 분리 완료 · ' + (m.ms / 1000).toFixed(1) + 's (모델 ' + total + 'ms)');
          global.D3DUI.syncEngine();
        }
      }).catch(function (e) {
        doc.aiBusy = false;
        hideLoading();
        console.error(e);
        var stale = (doc.gen !== gen);
        doc.maskEngine = 'fast';
        if (global.D3DUI) {
          global.D3DUI.aiStatus('error', 'AI 분리 실패 — 고속 모드 유지');
          global.D3DUI.toast('AI 분리를 하지 못해 고속 마스크를 씁니다');
          global.D3DUI.syncEngine();
        }
        if (stale) { maybeRunAI(); return; }
        if (state.mode === 'fixed') {
          doc.rawMask = SegEngine.depthMask(doc.depth, state.cutoff);
          var pack2 = fixedPack(true);
          if (pack2) buildScene(doc.img, doc.depth, pack2);
        }
      });
    }, 40);
  }

  /* ------------------------------------------------------------ 확대·이동·각도 저장 */

  function resetView() {
    view.tYaw = 0; view.tPitch = 0;
    view.vYaw = 0; view.vPitch = 0;
    view.tZoom = 1; view.tPanX = 0; view.tPanY = 0;
    if (global.D3DUI && global.D3DUI.onZoom) global.D3DUI.onZoom(1);
    return { yaw: 0, pitch: 0, zoom: 1, panX: 0, panY: 0 };
  }

  /* 지금 화면에 보이는 그대로를 고화질 PNG 로 뽑는다(3D 장면만, HUD 제외). */
  function capture() {
    if (!renderer || !scene || !camera) throw new Error('3D 화면이 준비되지 않았습니다');
    var w = canvas.clientWidth || global.innerWidth;
    var h = canvas.clientHeight || global.innerHeight;
    var longEdge = 2160;
    var k = longEdge / Math.max(1, Math.max(w, h));
    if (!isFinite(k) || k <= 0) k = 2;
    if (k < 1) k = 1;
    if (k > 3.2) k = 3.2;
    var cw = Math.max(64, Math.round(w * k));
    var ch = Math.max(64, Math.round(h * k));

    var oldPR = renderer.getPixelRatio();
    var oldAspect = camera.aspect;
    try {
      // ① 고해상도로 한 번 더 렌더
      renderer.setPixelRatio(1);
      renderer.setSize(cw, ch, false);
      camera.aspect = cw / ch;
      camera.updateProjectionMatrix();
      camera.updateMatrixWorld();
      renderer.render(scene, camera);

      // ② 앱 배경색 위에 합성 — 투명 구멍 없이 갤러리에서 그대로 보이게
      var out = document.createElement('canvas');
      out.width = cw; out.height = ch;
      var g = out.getContext('2d');
      g.fillStyle = '#0B0F17';
      g.fillRect(0, 0, cw, ch);
      var rad = g.createRadialGradient(cw * 0.5, -ch * 0.08, Math.min(cw, ch) * 0.1,
        cw * 0.5, ch * 0.42, Math.max(cw, ch) * 0.78);
      rad.addColorStop(0, '#17212f');
      rad.addColorStop(0.46, '#0d131e');
      rad.addColorStop(1, '#070a10');
      g.fillStyle = rad;
      g.fillRect(0, 0, cw, ch);
      g.drawImage(canvas, 0, 0, cw, ch);

      var url = out.toDataURL('image/png');
      return {
        dataUrl: url, width: cw, height: ch,
        bytes: Math.max(0, Math.round((url.length - url.indexOf(',') - 1) * 3 / 4)),
        view: { yaw: view.yaw * 180 / Math.PI, pitch: view.pitch * 180 / Math.PI, zoom: view.zoom }
      };
    } finally {
      // ③ 화면 크기·비율 복구
      renderer.setPixelRatio(oldPR);
      renderer.setSize(w, h, false);
      camera.aspect = oldAspect;
      camera.updateProjectionMatrix();
      fit();
      renderer.render(scene, camera);
    }
  }

  /* ------------------------------------------------------------ 브러시 보정 */

  var brush = { on: false, w: 0, h: 0, base: null, layer: null, swayWas: false, gyroWas: false, tint: null };

  function composeBrush() {
    var n = brush.w * brush.h;
    var out = new Float32Array(n);
    var B = brush.base, L = brush.layer;
    for (var i = 0; i < n; i++) {
      var l = L[i];
      var b = B[i];
      out[i] = l > 0 ? Math.max(b, l) : (l < 0 ? Math.min(b, 1 + l) : b);
    }
    return out;
  }

  function brushAlpha() {
    if (!doc.rawMask) return null;
    var mask = brush.on ? composeBrush() : doc.rawMask.mask;
    return { alpha: SegEngine.buildAlpha({
      w: brush.w || doc.rawMask.w, h: brush.h || doc.rawMask.h,
      mask: mask, refined: !!doc.rawMask.refined
    }, state.cutoff, state.feather), w: brush.w || doc.rawMask.w, h: brush.h || doc.rawMask.h };
  }

  /* 3D 평면이 화면에서 차지하는 사각형(브러시 캔버스를 겹칠 위치) */
  function photoRect() {
    var w = canvas.clientWidth || global.innerWidth;
    var h = canvas.clientHeight || global.innerHeight;
    var viewH = 2 * Math.tan(camera.fov * Math.PI / 360) * camera.position.z;
    var viewW = viewH * camera.aspect;
    var s = fitScale * view.zoom;
    var pw = planeW * s, ph = planeH * s;
    var cx = group.position.x, cy = group.position.y;
    var x0 = w * 0.5 + ((cx - pw * 0.5) / viewW) * w;
    var x1 = w * 0.5 + ((cx + pw * 0.5) / viewW) * w;
    var y0 = h * 0.5 - ((cy + ph * 0.5) / viewH) * h;
    var y1 = h * 0.5 - ((cy - ph * 0.5) / viewH) * h;
    return { x: x0, y: y0, w: x1 - x0, h: y1 - y0 };
  }

  function beginBrush() {
    if (!doc.rawMask || !doc.img) return null;
    if (state.mode !== 'fixed') return null;
    brush.on = true;
    brush.w = doc.rawMask.w;
    brush.h = doc.rawMask.h;
    brush.base = new Float32Array(doc.rawMask.mask);
    brush.layer = new Float32Array(brush.w * brush.h);
    brush.tint = null;
    // 회전·줌을 초기화해야 사진이 화면 사각형과 정확히 겹친다.
    view.tYaw = view.yaw = 0; view.tPitch = view.pitch = 0;
    view.vYaw = view.vPitch = 0;
    view.tZoom = view.zoom = 1;
    view.tPanX = view.panX = view.tPanY = view.panY = 0;
    brush.swayWas = state.sway; brush.gyroWas = state.gyro;
    state.sway = false; state.gyro = false;
    if (global.Android && Android.stopSensors) Android.stopSensors();
    fit();
    renderer.render(scene, camera);
    return { w: brush.w, h: brush.h, rect: photoRect() };
  }

  /* u,v(0~1, 사진 좌표) 위를 반지름 r(마스크 픽셀) 로 칠한다 */
  function brushPaint(u0, v0, u1, v1, r, add) {
    if (!brush.on || !brush.layer) return 0;
    var w = brush.w, h = brush.h;
    var x0 = u0 * (w - 1), y0 = v0 * (h - 1);
    var x1 = u1 * (w - 1), y1 = v1 * (h - 1);
    var dx = x1 - x0, dy = y1 - y0;
    var len2 = dx * dx + dy * dy;
    var rr = Math.max(1.2, r);
    var minX = Math.max(0, Math.floor(Math.min(x0, x1) - rr));
    var maxX = Math.min(w - 1, Math.ceil(Math.max(x0, x1) + rr));
    var minY = Math.max(0, Math.floor(Math.min(y0, y1) - rr));
    var maxY = Math.min(h - 1, Math.ceil(Math.max(y0, y1) + rr));
    var painted = 0;
    for (var y = minY; y <= maxY; y++) {
      for (var x = minX; x <= maxX; x++) {
        var t = len2 > 0.0001 ? ((x - x0) * dx + (y - y0) * dy) / len2 : 0;
        t = t < 0 ? 0 : (t > 1 ? 1 : t);
        var px = x0 + dx * t, py = y0 + dy * t;
        var ddx = x - px, ddy = y - py;
        var d = Math.sqrt(ddx * ddx + ddy * ddy);
        if (d > rr) continue;
        var wgt = 1 - SegEngine.smoothstep(rr * 0.62, rr, d);
        if (wgt <= 0.001) continue;
        var i = y * w + x;
        if (add) {
          if (wgt > brush.layer[i]) brush.layer[i] = wgt;
        } else {
          if (-wgt < brush.layer[i]) brush.layer[i] = -wgt;
        }
        painted++;
      }
    }
    if (painted) brush.tint = null;
    return painted;
  }

  /* 브러시 화면용: 사진 + 현재 마스크(민트색) */
  function brushOverlay(cv) {
    if (!brush.on || !doc.img || !cv || !cv.getContext) return false;
    var W = cv.width, H = cv.height;
    var g = cv.getContext('2d');
    g.clearRect(0, 0, W, H);
    g.fillStyle = '#05070b';
    g.fillRect(0, 0, W, H);
    g.imageSmoothingEnabled = true;
    g.imageSmoothingQuality = 'high';
    g.drawImage(doc.img, 0, 0, W, H);

    var a = brushAlpha();
    if (!a) return true;
    if (!brush.tint || brush.tint.width !== a.w || brush.tint.height !== a.h) {
      brush.tint = document.createElement('canvas');
      brush.tint.width = a.w;
      brush.tint.height = a.h;
    }
    var tc = brush.tint.getContext('2d');
    var im = tc.createImageData(a.w, a.h);
    var d = im.data;
    for (var i = 0; i < a.w * a.h; i++) {
      var al = a.alpha[i];
      var o = i * 4;
      if (al <= 0.02) { d[o + 3] = 0; continue; }
      // 피사체 = 민트 반투명, 경계 = 밝은 금빛 선
      var edge = al > 0.15 && al < 0.85 ? (1 - Math.abs(al - 0.5) * 2) : 0;
      d[o] = Math.round(78 * (1 - edge) + 233 * edge);
      d[o + 1] = Math.round(214 * (1 - edge) + 196 * edge);
      d[o + 2] = Math.round(184 * (1 - edge) + 106 * edge);
      d[o + 3] = Math.round(Math.min(1, al * 0.42 + edge * 0.5) * 255);
    }
    tc.putImageData(im, 0, 0);
    g.drawImage(brush.tint, 0, 0, W, H);
    return true;
  }

  /* 완료: 브러시 결과를 피사체·배경 인페인팅에 반영 */
  function applyBrush() {
    if (!brush.on) return false;
    var composed = composeBrush();
    brush.on = false;
    doc.rawMask = { w: brush.w, h: brush.h, mask: composed, engine: doc.maskEngine, refined: !!doc.rawMask.refined };
    doc.bg = null;
    doc.shadow = null;
    if (doc.img) {
      var pack = fixedPack(true);
      if (pack) buildScene(doc.img, doc.depth, pack);
    }
    state.sway = brush.swayWas;
    state.gyro = brush.gyroWas;
    brush.base = brush.layer = brush.tint = null;
    if (global.D3DUI) global.D3DUI.onRebuilt(state.mode, doc.maskEngine);
    return true;
  }

  function cancelBrush() {
    if (!brush.on) return false;
    brush.on = false;
    brush.base = brush.layer = brush.tint = null;
    state.sway = brush.swayWas;
    state.gyro = brush.gyroWas;
    fit();
    if (global.D3DUI) global.D3DUI.onRebuilt(state.mode, doc.maskEngine);
    return true;
  }

  /* ------------------------------------------------------------ public */

  var D3D = {
    init: init,
    resize: resize,
    fit: fit,
    loadPhoto: loadPhoto,
    resetView: resetView,
    capture: capture,
    beginBrush: beginBrush,
    brushPaint: brushPaint,
    brushOverlay: brushOverlay,
    applyBrush: applyBrush,
    cancelBrush: cancelBrush,
    photoRect: photoRect,
    brushDebug: function () {
      if (!brush.on || !brush.layer) return null;
      var minX = 1e9, minY = 1e9, maxX = -1, maxY = -1, ones = 0, n = brush.w * brush.h;
      for (var y = 0; y < brush.h; y++) {
        for (var x = 0; x < brush.w; x++) {
          var v = brush.layer[y * brush.w + x];
          if (v !== 0) {
            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
            if (v >= 0.99 || v <= -0.99) ones++;
          }
        }
      }
      return { minX: minX, minY: minY, maxX: maxX, maxY: maxY, full: ones, w: brush.w, h: brush.h };
    },
    brushState: function () {
      return {
        on: brush.on, w: brush.w, h: brush.h,
        painted: brush.on ? (function () {
          var c = 0; for (var i = 0; i < brush.layer.length; i++) if (brush.layer[i] !== 0) c++;
          return c;
        })() : 0,
        maskCoverage: doc.rawMask ? (function () {
          var m = doc.rawMask.mask, s = 0, n = m.length;
          for (var i = 0; i < n; i++) s += m[i] > 0.5 ? 1 : 0;
          return s / n;
        })() : 0,
        composedCoverage: (function () {
          if (!brush.on || !brush.layer) return -1;
          var m = composeBrush(), s = 0, n = m.length;
          for (var i = 0; i < n; i++) s += m[i] > 0.5 ? 1 : 0;
          return s / n;
        })()
      };
    },

    getDepthPreview: function (cv) {
      if (!depthInfo || !cv || !cv.getContext) return false;
      var w = cv.width, h = cv.height;
      var c = cv.getContext('2d');
      var img = c.createImageData(w, h);
      var d = depthInfo;
      for (var y = 0; y < h; y++) {
        for (var x = 0; x < w; x++) {
          var v = DepthEngine.sample(d.depth, d.w, d.h, x / Math.max(1, w - 1), y / Math.max(1, h - 1));
          var r, g, b;
          if (v < 0.5) {
            var t = v * 2;
            r = 9 + (46 - 9) * t; g = 24 + (146 - 24) * t; b = 34 + (128 - 34) * t;
          } else {
            var t2 = (v - 0.5) * 2;
            r = 46 + (233 - 46) * t2; g = 146 + (196 - 146) * t2; b = 128 + (106 - 128) * t2;
          }
          var o = (y * w + x) * 4;
          img.data[o] = r; img.data[o + 1] = g; img.data[o + 2] = b; img.data[o + 3] = 255;
        }
      }
      c.putImageData(img, 0, 0);
      return true;
    },

    getMaskPreview: function (cv) {
      if (!doc.rawMask || !cv || !cv.getContext) return false;
      var c = cv.getContext('2d');
      var w = cv.width, h = cv.height;
      var im = c.createImageData(w, h);
      var alpha = SegEngine.buildAlpha(doc.rawMask, state.cutoff, state.feather);
      for (var y = 0; y < h; y++) {
        for (var x = 0; x < w; x++) {
          var u = x / Math.max(1, w - 1), v = y / Math.max(1, h - 1);
          var a = DepthEngine.sample(alpha, doc.rawMask.w, doc.rawMask.h, u, v);
          var o = (y * w + x) * 4;
          im.data[o] = 233 * a; im.data[o + 1] = 196 * a + 40 * (1 - a); im.data[o + 2] = 106 * a + 90 * (1 - a);
          im.data[o + 3] = 255;
        }
      }
      c.putImageData(im, 0, 0);
      return true;
    },

    setParam: function (key, value) {
      if (key === 'mode') { this.setMode(value); return; }
      if (key === 'engine') { this.setEngine(value); return; }
      if (key === 'cutoff' || key === 'feather') {
        state[key] = value;
        if (state.mode === 'fixed' && doc.rawMask) softRebuild();
        return;
      }
      if (!(key in state)) return;
      state[key] = value;
      if (key === 'depth' || key === 'invert') applyUniforms();
      if (key === 'light' || key === 'lightAngle' || key === 'lightPower') applyUniforms();
      if (key === 'smooth' && DepthEngine.hasImage()) refreshDepth(value);
      if (key === 'bgLock') { /* animate 에서 반영 */ }
      if (key === 'center' && value) { view.vYaw = 0; view.vPitch = 0; }
    },

    setMode: function (mode) {
      state.mode = (mode === 'full') ? 'full' : 'fixed';
      if (!doc.img) return;
      doc.gen++;
      if (state.mode === 'fixed') {
        if (!doc.rawMask) doc.rawMask = SegEngine.depthMask(doc.depth, state.cutoff);
        var pack = fixedPack(false);
        if (pack) buildScene(doc.img, doc.depth, pack);
        maybeRunAI();
      } else {
        buildScene(doc.img, doc.depth, null);
      }
      if (global.D3DUI) global.D3DUI.onModeChanged(state.mode);
    },

    setEngine: function (engine) {
      state.engine = (engine === 'fast') ? 'fast' : 'ai';
      if (state.mode !== 'fixed') return;
      if (state.engine === 'fast') {
        doc.rawMask = SegEngine.depthMask(doc.depth, state.cutoff);
        doc.maskEngine = 'fast';
        var pack = fixedPack(true);
        if (pack && doc.img) buildScene(doc.img, doc.depth, pack);
        if (global.D3DUI) global.D3DUI.aiStatus('idle', '고속 마스크 · 즉시');
      } else {
        runAI();
      }
    },

    reapplyMask: function () { if (state.mode === 'fixed') softRebuild(); },
    runAI: runAI,

    getState: function () {
      return {
        depth: state.depth, smooth: state.smooth, invert: state.invert,
        lightAngle: state.lightAngle, lightPower: state.lightPower,
        sway: state.sway, gyro: state.gyro, center: state.center,
        mode: state.mode, engine: state.engine,
        bgLock: state.bgLock, feather: state.feather, cutoff: state.cutoff
      };
    },

    setView: function (yawDeg, pitchDeg) {
      view.tYaw = clamp((yawDeg || 0) * Math.PI / 180, -maxAngleY, maxAngleY);
      view.tPitch = clamp((pitchDeg || 0) * Math.PI / 180, -maxAngleX, maxAngleX);
      view.yaw = view.tYaw;
      view.pitch = view.tPitch;
      view.vYaw = view.vPitch = 0;
    },

    setZoom: function (z, panX, panY) {
      view.tZoom = clamp(z || 1, ZOOM_MIN, ZOOM_MAX);
      if (typeof panX === 'number') view.tPanX = clamp(panX, -PAN_LIMIT, PAN_LIMIT);
      if (typeof panY === 'number') view.tPanY = clamp(panY, -PAN_LIMIT, PAN_LIMIT);
    },

    getView: function () {
      return {
        yaw: view.yaw * 180 / Math.PI, pitch: view.pitch * 180 / Math.PI,
        zoom: view.zoom, panX: view.panX, panY: view.panY
      };
    },

    setSway: function (on) { state.sway = !!on; },
    setGyro: function (on) {
      state.gyro = !!on;
      gyro.calYaw = null;
      gyro.calPitch = null;
      gyro.ready = false;

      function giveUp(msg) {
        state.gyro = false;
        if (global.D3DUI) {
          global.D3DUI.toast(msg || '이 환경에서는 기울기 센서를 쓸 수 없습니다');
          global.D3DUI.syncToggles();
        }
      }

      function waitForEvent() {
        setTimeout(function () {
          if (state.gyro && !gyro.ready) giveUp('이 브라우저에서는 기울기 센서를 쓸 수 없습니다');
        }, 1600);
      }

      if (!on) {
        if (global.Android && Android.stopSensors) Android.stopSensors();
        return;
      }
      if (global.Android && Android.startSensors) {
        Android.startSensors();
        return;
      }
      if (typeof global.DeviceOrientationEvent === 'undefined') { giveUp(); return; }
      if (typeof global.DeviceOrientationEvent.requestPermission === 'function') {
        global.DeviceOrientationEvent.requestPermission().then(function (r) {
          if (r === 'granted') waitForEvent();
          else giveUp('센서 권한이 허용되지 않았습니다');
        }).catch(function () { giveUp('센서 권한을 요청하지 못했습니다'); });
      } else {
        waitForEvent();
      }
    },
    setImmersiveUI: function (on) {
      if (global.D3DUI) global.D3DUI.setImmersive(!!on, true);
    },

    onSensor: function (yaw, pitch, roll) {
      if (!state.gyro) return;
      if (gyro.calYaw === null) {
        gyro.calYaw = yaw;
        gyro.calPitch = pitch;
      }
      var dy = yaw - gyro.calYaw;
      while (dy > 180) dy -= 360;
      while (dy < -180) dy += 360;
      var dp = pitch - gyro.calPitch;
      gyro.tYaw = clamp(dy * 0.011, -maxAngleY, maxAngleY);
      gyro.tPitch = clamp(dp * 0.011, -maxAngleX, maxAngleX);
      gyro.ready = true;
    },

    onSensorUnavailable: function () {
      state.gyro = false;
      if (global.D3DUI) global.D3DUI.toast('이 기기에서 자이로 센서를 찾지 못했습니다');
      if (global.D3DUI) global.D3DUI.syncToggles();
    },

    onPhotoLoading: function () { showLoading('사진 불러오는 중'); },
    beginPhoto: function (label, total) {
      chunk = { label: label, total: total, parts: [], got: 0 };
      showLoading('사진 불러오는 중');
    },
    pushPhoto: function (part) {
      if (!chunk) return;
      chunk.parts.push(part);
      chunk.got += part.length;
      if (global.D3DUI && chunk.total) {
        global.D3DUI.loadingProgress(Math.min(1, chunk.got / chunk.total));
      }
    },
    endPhoto: function () {
      if (!chunk) return;
      var b64 = chunk.parts.join('');
      var label = chunk.label;
      chunk = null;
      loadPhoto('data:image/jpeg;base64,' + b64, label);
    },
    onPhotoError: function (msg) {
      hideLoading();
      if (global.D3DUI) global.D3DUI.toast(msg || '사진을 불러오지 못했습니다');
    },

    onAppPause: function () { if (state.gyro && global.Android) Android.stopSensors(); },
    onAppResume: function () { if (state.gyro && global.Android) Android.startSensors(); },
    onBackPressed: function () {
      if (global.D3DUI) global.D3DUI.onBackPressed();
    },

    _meshes: function () { return { front: frontMesh, wall: wallMesh, back: backMesh, bg: bgMesh, group: group, subject: subjectRoot, photo: photoTex, depth: depthTex, mask: maskTex }; },

    debug: function () {
      var geoInfo = null;
      if (frontMesh) {
        var pa = frontMesh.geometry.getAttribute('position').array;
        var da = frontMesh.geometry.getAttribute('aDepth').array;
        var pmin = [1e9, 1e9, 1e9], pmax = [-1e9, -1e9, -1e9], nan = 0, i, c;
        for (i = 0; i < pa.length; i += 3) {
          for (c = 0; c < 3; c++) {
            var v = pa[i + c];
            if (!isFinite(v)) nan++;
            if (v < pmin[c]) pmin[c] = v;
            if (v > pmax[c]) pmax[c] = v;
          }
        }
        var dmin = 1e9, dmax = -1e9;
        for (i = 0; i < da.length; i++) {
          var dv = da[i];
          if (!isFinite(dv)) nan++;
          if (dv < dmin) dmin = dv;
          if (dv > dmax) dmax = dv;
        }
        geoInfo = { pmin: pmin, pmax: pmax, dmin: dmin, dmax: dmax, nan: nan };
      }
      return {
        ready: !!frontMesh,
        mode: state.mode,
        maskEngine: doc.maskEngine,
        aiStatus: SegEngine ? SegEngine.aiStatus() : null,
        model: SegEngine ? SegEngine.MODEL : null,
        contourPoints: doc.contourCount || 0,
        hasMask: !!maskTex,
        hasBg: !!(bgMesh && bgMesh.visible),
        bgMs: bgMs,
        vertices: frontMesh ? frontMesh.geometry.getAttribute('position').count : 0,
        wallVertices: wallMesh ? wallMesh.geometry.getAttribute('position').count : 0,
        segments: [segX, segY],
        depthMap: depthInfo ? [depthInfo.w, depthInfo.h] : null,
        renderer: renderer ? renderer.info.render.triangles : 0,
        three: THREE.REVISION,
        geo: geoInfo,
        camera: camera ? { fov: camera.fov, z: camera.position.z, aspect: camera.aspect } : null,
        scale: group ? group.scale.x : 0,
        dispScale: dispScale,
        plane: [planeW, planeH],
        view: {
          yaw: view.yaw * 180 / Math.PI, pitch: view.pitch * 180 / Math.PI,
          zoom: view.zoom, panX: view.panX, panY: view.panY,
          fitScale: fitScale, fitY: fitY, bgCover: bgCover
        }
      };
    },

    /* 검증용: 화면 픽셀을 읽어 배경이 실제로 고정인지 비교할 수 있게 */
    _state: function () { return state; },
    _camera: function () { return camera; }
  };

  function softRebuild() {
    if (!doc.img || !doc.depth || !doc.rawMask) return;
    var alpha = SegEngine.buildAlpha(doc.rawMask, state.cutoff, state.feather);
    if (!maskTex) buildMaskTexture(alpha, doc.rawMask.w, doc.rawMask.h);
    else {
      writeMaskData(maskTex.image.data, alpha, doc.rawMask.w, doc.rawMask.h);
      maskTex.needsUpdate = true;
    }
    // 실루엣 벽만 새로 만든다
    var contour = SegEngine.traceContour(alpha, doc.rawMask.w, doc.rawMask.h, 0.5);
    doc.contourCount = contour.length;
    if (wallMesh) {
      var geo = buildSilhouetteWall(contour, doc.depth, doc.rawMask.w, doc.rawMask.h);
      if (geo) {
        var old = wallMesh.geometry;
        wallMesh.geometry = geo;
        old.dispose();
      }
    }
    if (global.D3DUI) global.D3DUI.onRebuilt(state.mode, doc.maskEngine);
  }

  global.D3D = D3D;

  global.addEventListener('deviceorientation', function (e) {
    if (!state.gyro) return;
    if (e.alpha === null && e.beta === null) return;
    D3D.onSensor(e.alpha || 0, e.beta || 0, e.gamma || 0);
  });
})(window);
