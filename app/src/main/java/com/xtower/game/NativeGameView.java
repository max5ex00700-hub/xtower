package com.xtower.game;

import android.content.Context;
import android.content.SharedPreferences;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.Path;
import android.graphics.RectF;
import android.graphics.Typeface;
import android.media.AudioAttributes;
import android.media.SoundPool;
import android.os.Handler;
import android.os.Looper;
import android.view.MotionEvent;
import android.view.View;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.HashMap;
import java.util.HashSet;
import java.util.List;
import java.util.Locale;
import java.util.Random;
import java.util.UUID;

public final class NativeGameView extends View {
    private static final int BASE_ENEMY_HP = 60;
    private static final int STAGES_PER_FLOOR = 10;
    private static final int GRID_W = 8;
    private static final int BASE_GRID_CELLS = 24;

    private static final int LOC_BAG = 0;
    private static final int LOC_HELD = 1;
    private static final int LOC_GROUND = 2;

    private static final int BG = 0xff080708;
    private static final int PANEL = 0xff151216;
    private static final int TXT = 0xfff3eee6;
    private static final int MUTED = 0xffbcb2a7;
    private static final int GOLD = 0xffd4a648;
    private static final int RED = 0xff7a171c;

    private enum Mode { MAIN, COMBAT, GACHA, BAG, FORGE, JAIL }
    private enum ForgeMode { ENHANCE, SYNTHESIS, DISMANTLE }

    private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG | Paint.FILTER_BITMAP_FLAG);
    private final Random rng = new Random();
    private final Handler handler = new Handler(Looper.getMainLooper());
    private final SharedPreferences prefs;

    private Mode mode = Mode.MAIN;
    private long lastFrameMs;

    private final HashMap<Integer, Bitmap[]> floorStages = new HashMap<>();
    private final HashMap<String, Bitmap[]> actionBitmaps = new HashMap<>();
    private final HashMap<Integer, Bitmap> captureBitmaps = new HashMap<>();
    private Bitmap actionBitmap;
    private long actionUntil;

    private SoundPool soundPool;
    private final HashMap<String, Integer> sfx = new HashMap<>();
    private final HashMap<String, Integer> voices = new HashMap<>();

    private final RectF fightBtn = new RectF();
    private final RectF refightBtn = new RectF();
    private final RectF downBtn = new RectF();
    private final RectF upBtn = new RectF();
    private final RectF bagBtn = new RectF();
    private final RectF forgeBtn = new RectF();
    private final RectF jailBtn = new RectF();

    private int currentStep;
    private int maxUnlockedStep;
    private int enemyMaxHp = BASE_ENEMY_HP;
    private int enemyHp = BASE_ENEMY_HP;
    private int hitCount;
    private boolean won;
    private long inputLockUntil;

    private float downX, downY;
    private long downTime;
    private boolean movedSinceDown;

    private String bubbleText = "";
    private long bubbleStart;
    private long bubbleUntil;

    private long hitFxStart;
    private long hitFxUntil;
    private float hitFxX, hitFxY;
    private boolean hitFxHeavy;

    private boolean weakActive;
    private long weakUntil;
    private float weakX, weakY;
    private float weakVx, weakVy;

    private long jellyStart;
    private long jellyUntil;
    private float jellyX, jellyY;
    private float jellySwipeX, jellySwipeY;
    private boolean jellyHeavy;
    private boolean jellySwipe;
    private final int meshW = 10;
    private final int meshH = 16;
    private final float[] meshVerts = new float[(10 + 1) * (16 + 1) * 2];

    private long gachaStart;
    private final long gachaDuration = 1850L;
    private int gachaCorrectionIndex;
    private int gachaCorrection;
    private float gachaTotalSpin;
    private boolean gachaResolved;
    private boolean gachaReady;
    private GachaOutcome pendingOutcome;

    private final ArrayList<GearBlock> items = new ArrayList<>();

    private int activeBagOwner;
    private GearBlock selectedBlock;
    private GearBlock pointerBlock;
    private GearBlock draggingBlock;
    private int dragOffsetX, dragOffsetY;
    private int dragPreviewX = -99, dragPreviewY = -99;
    private int dragOriginalX, dragOriginalY, dragOriginalLocation, dragOriginalOwner;
    private final RectF bagGridRect = new RectF();
    private final RectF heldRect = new RectF();
    private final RectF groundRect = new RectF();
    private final RectF tidyBtn = new RectF();
    private final RectF closeBagBtn = new RectF();
    private final ArrayList<RowHit> heldHits = new ArrayList<>();
    private final ArrayList<RowHit> groundHits = new ArrayList<>();

    private ForgeMode forgeMode = ForgeMode.ENHANCE;
    private String forgeTargetId;
    private final ArrayList<String> forgeMaterialIds = new ArrayList<>();
    private final RectF forgeEnhanceTab = new RectF();
    private final RectF forgeSynthesisTab = new RectF();
    private final RectF forgeDismantleTab = new RectF();
    private final RectF forgeExecuteBtn = new RectF();
    private final RectF forgeClearBtn = new RectF();
    private final RectF forgeCloseBtn = new RectF();
    private final ArrayList<RowHit> forgeHits = new ArrayList<>();
    private String forgeMessage = "블록을 선택하세요.";

    private int jailSelectedCharacter;
    private final ArrayList<RowHit> jailHits = new ArrayList<>();
    private final RectF jailOpenBagBtn = new RectF();
    private final RectF jailCloseBtn = new RectF();

    private final int[] corrections = {-50, -40, -30, -20, -10, 0, 10, 20, 30, 40, 50};
    private final int[] allowedSizes = {1, 2, 3, 4, 5, 6, 9, 12};
    private final int[] baseBudgets = {10, 22, 35, 50, 66, 84, 135, 190};

    private final String[] prefixes = {
        "검은", "붉은", "은빛", "낡은", "잊힌", "차가운",
        "불완전한", "무거운", "날카로운", "고요한", "균열난", "빛나는"
    };
    private final String[] nouns = {
        "파편", "인장", "갑주", "장식", "핵", "조각",
        "결정", "부품", "흔적", "고리", "판금", "심장"
    };

    private final String[][] zoneTalk = {
        {"머리… 만지지 마.", "흩트리지 마.", "전투 중이잖아."},
        {"얼굴을 노리는 거야?", "가까이 오지 마.", "…시선이 거슬려."},
        {"윽… 거긴.", "정면으로 오는군.", "숨이 막혀."},
        {"거긴 안 돼!", "손 치워!", "잠깐…!"},
        {"다리를 노리는군.", "균형이…", "무릎이 풀려."},
        {"발끝까지 노려?", "거긴 빗나갔어.", "아래를 보는군."},
        {"허공이야.", "나는 이쪽이야.", "빗나갔어."}
    };
    private final String[] dodgeTalk = {"느려.", "피했어.", "거긴 아니야.", "다 보여."};
    private final String[] criticalTalk = {"윽… 거긴!", "잠깐…!", "그걸 찾았어?", "균형이… 깨졌어."};

    private final String[] normalHitVoices = {
        "female_moan1", "female_moan2", "female_moan3", "female_moan4",
        "female_exhale1", "female_breath1", "female_sigh1", "female_sigh2",
        "female_ooh1", "female_gasp1", "female_grunt1"
    };
    private final String[] swipeHitVoices = {
        "female_moan2", "female_moan3", "female_moan4", "female_moan4",
        "female_ooh1", "female_exhale1", "female_breath1",
        "female_gasp2", "female_grunt2"
    };
    private final String[] criticalHitVoices = {
        "female_moan3", "female_moan3", "female_moan4", "female_moan4",
        "female_ooh1", "female_agony1", "female_agony2",
        "female_agony2", "female_scream1"
    };
    private final String[] lowHpVoices = {
        "female_whimper1", "female_whimper1", "female_breath1",
        "female_sigh1", "female_sigh2", "female_moan4", "female_exhale1"
    };

    public NativeGameView(Context context) {
        super(context);
        setFocusable(true);
        setKeepScreenOn(true);

        prefs = context.getSharedPreferences("xtap_native_engine", Context.MODE_PRIVATE);
        currentStep = Math.max(0, prefs.getInt("progress_step", 0));
        maxUnlockedStep = Math.max(0, prefs.getInt("max_unlocked_step", 0));
        if (currentStep > maxUnlockedStep) currentStep = maxUnlockedStep;

        loadImages();
        loadAudio();
        loadItems();
        resetCombat();
        lastFrameMs = System.currentTimeMillis();
    }

    public void release() {
        handler.removeCallbacksAndMessages(null);
        if (soundPool != null) {
            soundPool.release();
            soundPool = null;
        }
    }

    private int towerFloor() { return currentStep / STAGES_PER_FLOOR + 1; }
    private int subStage() { return currentStep % STAGES_PER_FLOOR + 1; }
    private String stageLabel() { return towerFloor() + "-" + subStage(); }
    private float progressMultiplier() { return 1f + currentStep * .05f; }
    private int currentCharacterId() { return ((towerFloor() - 1) % 10) + 1; }
    private int currentVisualFloor() { return ((towerFloor() - 1) % 3) + 1; }
    private int currentEnemyMaxHp() { return Math.max(1, Math.round(BASE_ENEMY_HP * progressMultiplier())); }

    private void saveProgress() {
        prefs.edit()
            .putInt("progress_step", currentStep)
            .putInt("max_unlocked_step", maxUnlockedStep)
            .apply();
    }

    private void loadImages() {
        String[] stageSuffix = {"p00", "p02", "p04", "p06", "p08"};

        for (int floor = 1; floor <= 3; floor++) {
            Bitmap[] stages = new Bitmap[5];
            for (int i = 0; i < stageSuffix.length; i++) {
                stages[i] = bitmapByName("f" + floor + "_" + stageSuffix[i]);
                if (stages[i] == null && floor != 1)
                    stages[i] = bitmapByName("f1_" + stageSuffix[i]);
                if (stages[i] == null)
                    stages[i] = bitmapByName("s" + i);
            }
            floorStages.put(floor, stages);

            Bitmap cap = bitmapByName("f" + floor + "_cap");
            if (cap == null && floor != 1) cap = bitmapByName("f1_cap");
            if (cap == null) cap = stages[4];
            captureBitmaps.put(floor, cap);

            for (String group : new String[]{"p", "k", "b", "d"}) {
                Bitmap[] arr = new Bitmap[10];
                for (int i = 0; i < 10; i++) {
                    String suffix = String.format(Locale.US, "%02d", i);
                    arr[i] = bitmapByName("f" + floor + "_" + group + suffix);
                    if (arr[i] == null && floor != 1)
                        arr[i] = bitmapByName("f1_" + group + suffix);
                }
                actionBitmaps.put(floor + "_" + group, arr);
            }
        }
    }

    private Bitmap bitmapByName(String name) {
        int id = getResources().getIdentifier(name, "drawable", getContext().getPackageName());
        if (id == 0) return null;
        return BitmapFactory.decodeResource(getResources(), id);
    }

    private void loadAudio() {
        AudioAttributes attrs = new AudioAttributes.Builder()
            .setUsage(AudioAttributes.USAGE_GAME)
            .setContentType(AudioAttributes.CONTENT_TYPE_SONIFICATION)
            .build();

        soundPool = new SoundPool.Builder()
            .setAudioAttributes(attrs)
            .setMaxStreams(12)
            .build();

        for (String name : new String[]{
            "fight_punch_light", "fight_punch_medium", "fight_smash_heavy",
            "fight_swing_light", "fight_swing_heavy", "hit", "hurt", "win", "sparkle"
        }) loadSound(name, sfx);

        for (String name : new String[]{
            "female_grunt1", "female_grunt2", "female_gasp1", "female_gasp2",
            "female_agony1", "female_agony2", "female_scream1", "female_whimper1",
            "female_moan1", "female_moan2", "female_moan3", "female_moan4",
            "female_exhale1", "female_breath1", "female_sigh1", "female_sigh2",
            "female_ooh1"
        }) loadSound(name, voices);
    }

    private void loadSound(String name, HashMap<String, Integer> target) {
        int id = getResources().getIdentifier(name, "raw", getContext().getPackageName());
        if (id != 0) target.put(name, soundPool.load(getContext(), id, 1));
    }

    private void play(HashMap<String, Integer> source, String name, float volume) {
        if (soundPool == null) return;
        Integer id = source.get(name);
        if (id != null && id != 0) soundPool.play(id, volume, volume, 1, 0, 1f);
    }

    private void playSfx(String name, float volume) { play(sfx, name, volume); }
    private void playVoice(String name, float volume) { play(voices, name, volume); }

    private void playRandomVoice(String[] pool, float volume) {
        if (pool.length > 0) playVoice(pool[rng.nextInt(pool.length)], volume);
    }

    @Override protected void onDraw(Canvas canvas) {
        super.onDraw(canvas);

        long now = System.currentTimeMillis();
        float dt = Math.max(0f, Math.min(.05f, (now - lastFrameMs) / 1000f));
        lastFrameMs = now;

        updateWeakPoint(now, dt);

        switch (mode) {
            case MAIN: drawMain(canvas, now); break;
            case COMBAT: drawCombat(canvas, now); break;
            case GACHA: drawGacha(canvas, now); break;
            case BAG: drawBag(canvas, now); break;
            case FORGE: drawForge(canvas, now); break;
            case JAIL: drawJail(canvas, now); break;
        }

        if (needsAnimation(now)) postInvalidateOnAnimation();
    }

    private boolean needsAnimation(long now) {
        return (mode == Mode.GACHA && !gachaReady)
            || now < bubbleUntil || now < hitFxUntil || now < jellyUntil
            || now < actionUntil || weakActive;
    }

    private Bitmap[] currentStages() {
        Bitmap[] a = floorStages.get(currentVisualFloor());
        if (a == null) a = floorStages.get(1);
        return a;
    }

    private Bitmap currentCapture() {
        Bitmap b = captureBitmaps.get(currentVisualFloor());
        if (b == null) b = captureBitmaps.get(1);
        return b;
    }

    private void drawMain(Canvas c, long now) {
        int w = getWidth(), h = getHeight();
        c.drawColor(BG);

        Bitmap[] stages = currentStages();
        if (stages != null) drawBitmapCover(c, stages[0], new RectF(0, 0, w, h));

        paint.setColor(0x66000000);
        c.drawRect(0, 0, w, h * .30f, paint);
        paint.setColor(0xaa000000);
        c.drawRect(0, h * .70f, w, h, paint);

        drawText(c, "X탑", 24f, 62f, 46f, 0xfff1e5d2, true);
        drawText(c, "NATIVE 10.51", w - 24f, 34f, 13f, 0xffd9b65d, false, Paint.Align.RIGHT);
        drawText(c, "FLOOR", 24f, 105f, 15f, 0xffbdb0a0, false);
        drawText(c, stageLabel(), 24f, 154f, 46f, 0xffffffff, true);

        int growth = Math.round(progressMultiplier() * 100f);
        drawText(c,
            (currentStep < maxUnlockedStep ? "클리어 · ↑ 이동 가능" : "도전 중") +
            " · 성장 " + growth + "%" +
            " · 장비 공+" + equippedAtk(0) + " 방+" + equippedDef(0) + " 체+" + equippedHp(0),
            24f, 190f, 14f, 0xffe8dfd3, true);

        drawSpeechBubble(c, "...또 오는 거야?", w * .56f, 90f, w - 18f, 176f, 1f);

        float navH = Math.max(118f, h * .11f);
        float fightTop = h - navH - Math.max(142f, h * .13f);
        fightBtn.set(w * .205f, fightTop, w * .795f, fightTop + Math.max(82f, h * .075f));
        drawButton(c, fightBtn, "그녀를 베다", RED, 28f, true);

        RectF rail = new RectF(0, h - navH, w, h);
        paint.setColor(0xff111011);
        c.drawRect(rail, paint);
        strokeRect(c, rail, 0xff5e5140, 2f);

        RectF[] btns = {refightBtn, downBtn, upBtn, bagBtn, forgeBtn, jailBtn};
        String[] labels = {"다시", "↓", "↑", "배낭", "대장간", "감옥"};
        float gap = 6f, left = 8f;
        float bw = (w - left * 2f - gap * 5f) / 6f;

        for (int i = 0; i < btns.length; i++) {
            float x = left + i * (bw + gap);
            btns[i].set(x, h - navH + 12f, x + bw, h - 10f);
            drawButton(c, btns[i], labels[i], 0xff312a28, 14f, false);
        }

        int ch = currentCharacterId();
        drawText(c,
            isCaptured(ch) ? "캐릭터 " + ch + " 포획됨" : "캐릭터 " + ch + " 미포획",
            w - 20f, h - navH - 18f, 13f,
            isCaptured(ch) ? 0xffc990e8 : 0xffbeb4aa, true, Paint.Align.RIGHT);
    }

    private void drawCombat(Canvas c, long now) {
        int w = getWidth(), h = getHeight();
        c.drawColor(Color.BLACK);

        Bitmap current;
        Bitmap[] stages = currentStages();
        if (won && currentCapture() != null) current = currentCapture();
        else if (actionBitmap != null && now < actionUntil) current = actionBitmap;
        else current = stages == null ? null : stages[combatStage()];

        if (current != null) {
            if (now < jellyUntil) drawBitmapJelly(c, current, now);
            else drawBitmapCover(c, current, new RectF(0, 0, w, h));
        }

        paint.setColor(0x66000000);
        c.drawRect(0, 0, w, Math.max(70f, h * .075f), paint);
        drawText(c, stageLabel() + "   HP " + enemyHp + " / " + enemyMaxHp, 18f, 36f, 16f, Color.WHITE, true);

        float hpPct = enemyHp / (float)Math.max(1, enemyMaxHp);
        RectF bar = new RectF(18f, 46f, w - 18f, 58f);
        roundRect(c, bar, 6f, 0xff2b2425);
        roundRect(c, new RectF(bar.left, bar.top, bar.left + bar.width() * hpPct, bar.bottom), 6f, 0xffa01e25);

        if (weakActive) drawWeakPoint(c, now);
        if (now < hitFxUntil) drawHitFx(c, now);

        if (now < bubbleUntil) {
            float progress = Math.min(1f, (now - bubbleStart) / 110f);
            float pop = progress < .72f
                ? lerp(.86f, 1.045f, progress / .72f)
                : lerp(1.045f, 1f, (progress - .72f) / .28f);
            float bw = Math.min(w * .55f, Math.max(250f, 185f + bubbleText.length() * 18f));
            float right = w - 18f, top = Math.max(88f, h * .12f);
            drawSpeechBubble(c, bubbleText, right - bw, top, right, top + 116f, pop);
        }
    }

    private void drawBitmapJelly(Canvas c, Bitmap bm, long now) {
        if (bm == null) return;
        RectF dst = coverRect(bm, new RectF(0, 0, getWidth(), getHeight()));

        float duration = Math.max(1f, jellyUntil - jellyStart);
        float t = clamp01((now - jellyStart) / duration);
        float decay = 1f - t;
        float wave = (float)Math.cos(t * Math.PI * 5.0);
        float amp = (jellyHeavy ? 34f : (jellySwipe ? 25f : 19f)) * decay * wave;
        float radius = Math.max(150f, Math.min(getWidth(), getHeight()) * (jellyHeavy ? .34f : .28f));

        float swipeLen = (float)Math.hypot(jellySwipeX, jellySwipeY);
        float sx = swipeLen > .01f ? jellySwipeX / swipeLen : 0f;
        float sy = swipeLen > .01f ? jellySwipeY / swipeLen : 0f;

        int k = 0;
        for (int yy = 0; yy <= meshH; yy++) {
            float v = yy / (float)meshH;
            float baseY = dst.top + v * dst.height();

            for (int xx = 0; xx <= meshW; xx++) {
                float u = xx / (float)meshW;
                float baseX = dst.left + u * dst.width();

                float dx = baseX - jellyX, dy = baseY - jellyY;
                float dist = (float)Math.hypot(dx, dy);
                float falloff = clamp01(1f - dist / radius);
                falloff *= falloff;

                float nx = dist > .001f ? dx / dist : 0f;
                float ny = dist > .001f ? dy / dist : 0f;

                float px = baseX + nx * amp * falloff;
                float py = baseY + ny * amp * falloff;

                if (jellySwipe) {
                    px += sx * amp * .72f * falloff;
                    py += sy * amp * .72f * falloff;
                }

                meshVerts[k++] = px;
                meshVerts[k++] = py;
            }
        }

        int save = c.save();
        c.clipRect(0, 0, getWidth(), getHeight());
        c.drawBitmapMesh(bm, meshW, meshH, meshVerts, 0, null, 0, paint);
        c.restoreToCount(save);
    }

    private void drawWeakPoint(Canvas c, long now) {
        float px = weakX * getWidth(), py = weakY * getHeight();
        float pulse = .55f + .75f * ((float)Math.sin(now * .0085f) + 1f) * .5f;
        float r = Math.max(28f, getWidth() * .043f) * pulse;

        paint.setStyle(Paint.Style.STROKE);
        paint.setStrokeWidth(Math.max(4f, getWidth() * .006f));
        paint.setColor(0xffffd44f);
        c.drawCircle(px, py, r, paint);
        paint.setStrokeWidth(Math.max(2f, getWidth() * .003f));
        paint.setColor(0xffff6f42);
        c.drawCircle(px, py, r * .58f, paint);
        paint.setStyle(Paint.Style.FILL);
    }

    private void drawHitFx(Canvas c, long now) {
        float total = Math.max(1f, hitFxUntil - hitFxStart);
        float t = clamp01((now - hitFxStart) / total);
        float alpha = 1f - t;
        float r = (hitFxHeavy ? 60f : 36f) + t * (hitFxHeavy ? 130f : 90f);

        paint.setStyle(Paint.Style.STROKE);
        paint.setStrokeWidth(hitFxHeavy ? 9f : 6f);
        paint.setColor(withAlpha(0xffffe9ba, alpha));
        c.drawCircle(hitFxX, hitFxY, r, paint);
        if (hitFxHeavy) c.drawCircle(hitFxX, hitFxY, r * .55f, paint);
        paint.setStyle(Paint.Style.FILL);
    }

    private void drawGacha(Canvas c, long now) {
        int w = getWidth(), h = getHeight();
        c.drawColor(0xff07080b);

        RectF machine = new RectF(w * .06f, h * .04f, w * .94f, h * .96f);
        roundRect(c, machine, 26f, 0xff131820);
        strokeRect(c, machine, 0xffd2a236, 5f);

        drawText(c, "X-TOWER REWARD", w / 2f, machine.top + 52f, 26f, 0xffffd65f, true, Paint.Align.CENTER);
        drawText(c, "캐릭터 " + currentCharacterId(), w / 2f, machine.top + 84f, 17f, MUTED, true, Paint.Align.CENTER);

        float cx = w / 2f, cy = h * .36f;
        float wheelR = Math.min(w * .34f, h * .18f);
        float rotation;

        if (!gachaReady) {
            float p = clamp01((now - gachaStart) / (float)gachaDuration);
            float e = 1f - (float)Math.pow(1f - p, 4);
            rotation = -gachaTotalSpin * e;
            if (p >= 1f && !gachaResolved) resolveGacha();
        } else {
            rotation = -gachaTotalSpin;
        }

        drawRouletteWheel(c, cx, cy, wheelR, rotation);

        paint.setColor(0xffffd65f);
        Path pointer = new Path();
        pointer.moveTo(cx, cy - wheelR - 8f);
        pointer.lineTo(cx - 18f, cy - wheelR - 44f);
        pointer.lineTo(cx + 18f, cy - wheelR - 44f);
        pointer.close();
        c.drawPath(pointer, paint);

        if (!gachaReady) {
            drawText(c, "보정 룰렛 회전 중", w / 2f, h * .70f, 20f, 0xffb8bbc3, true, Paint.Align.CENTER);
            return;
        }

        String corr = gachaCorrection > 0 ? "+" + gachaCorrection + "%" : gachaCorrection + "%";
        drawText(c, "보정  " + corr, w / 2f, h * .67f, 25f, 0xffffcf50, true, Paint.Align.CENTER);

        if (pendingOutcome != null && pendingOutcome.captureAttempt) {
            drawCaptureBall(c, cx, h * .56f, pendingOutcome.captureSucceeded);
            drawText(c, pendingOutcome.captureSucceeded ? "포획 성공!" : "포획 실패",
                w / 2f, h * .77f, 27f,
                pendingOutcome.captureSucceeded ? 0xfff1c95d : 0xffd7d7d7,
                true, Paint.Align.CENTER);
            drawText(c,
                pendingOutcome.captureSucceeded
                    ? "감옥에 캐릭터 " + currentCharacterId() + " 등록 · 개인 가방 8×3 해금"
                    : "포획 확률 1%",
                w / 2f, h * .815f, 16f, MUTED, false, Paint.Align.CENTER);
        } else if (pendingOutcome != null && pendingOutcome.block != null) {
            GearBlock b = pendingOutcome.block;
            drawRewardShape(c, b, cx, h * .56f, Math.min(34f, w * .07f));
            drawText(c, b.name + " · " + b.cellCount + "칸",
                w / 2f, h * .755f, 22f, TXT, true, Paint.Align.CENTER);
            drawText(c, "공 +" + b.atk + "   방 +" + b.def + "   체 +" + b.hp,
                w / 2f, h * .80f, 18f, 0xffffce55, true, Paint.Align.CENTER);
        }

        drawText(c,
            pendingOutcome != null && pendingOutcome.block != null
                ? "터치하면 보상이 바닥으로 이동"
                : "화면을 터치해서 계속",
            w / 2f, h * .90f, 16f, MUTED, false, Paint.Align.CENTER);
    }

    private void drawRouletteWheel(Canvas c, float cx, float cy, float radius, float rotationDeg) {
        paint.setStyle(Paint.Style.STROKE);
        paint.setStrokeWidth(8f);
        paint.setColor(0xff464b56);
        c.drawCircle(cx, cy, radius, paint);
        paint.setStyle(Paint.Style.FILL);

        float seg = 360f / corrections.length;
        for (int i = 0; i < corrections.length; i++) {
            float a = (float)Math.toRadians(i * seg + rotationDeg - 90f);
            float x = cx + (float)Math.cos(a) * radius * .77f;
            float y = cy + (float)Math.sin(a) * radius * .77f;

            int corr = corrections[i];
            int color = corr == 0 ? 0xff8a348f : (corr > 0 ? 0xffb9791e : 0xff34485f);
            paint.setColor(color);
            c.drawCircle(x, y, radius * .12f, paint);

            drawText(c, corr > 0 ? "+" + corr : String.valueOf(corr),
                x, y + 5f, Math.max(11f, radius * .07f), Color.WHITE, true, Paint.Align.CENTER);
        }

        paint.setColor(0xff20252e);
        c.drawCircle(cx, cy, radius * .34f, paint);
        drawText(c, "X", cx, cy + radius * .11f, radius * .34f, 0xffffd65f, true, Paint.Align.CENTER);
    }

    private void drawCaptureBall(Canvas c, float cx, float cy, boolean success) {
        paint.setColor(success ? 0xffe4b144 : 0xff656a73);
        c.drawCircle(cx, cy, 58f, paint);
        paint.setColor(0xff17191d);
        c.drawCircle(cx, cy, 25f, paint);
        drawText(c, "X", cx, cy + 10f, 30f, 0xffffd761, true, Paint.Align.CENTER);
    }

    private void drawRewardShape(Canvas c, GearBlock b, float cx, float cy, float cell) {
        List<Cell> cells = rotatedCells(b, 0);
        int maxX = 0, maxY = 0;
        for (Cell q : cells) {
            maxX = Math.max(maxX, q.x);
            maxY = Math.max(maxY, q.y);
        }

        float width = (maxX + 1) * cell;
        float height = (maxY + 1) * cell;
        float ox = cx - width / 2f;
        float oy = cy - height / 2f;
        int color = blockColor(b);

        for (Cell q : cells) {
            RectF r = new RectF(ox + q.x * cell + 2f, oy + q.y * cell + 2f,
                ox + (q.x + 1) * cell - 2f, oy + (q.y + 1) * cell - 2f);
            roundRect(c, r, 5f, color);
            roundRect(c, new RectF(r.left + cell * .16f, r.top + cell * .16f,
                r.right - cell * .16f, r.bottom - cell * .16f), 3f, 0xdd15171b);
        }
    }

    private void drawBag(Canvas c, long now) {
        int w = getWidth(), h = getHeight();
        c.drawColor(0xff080708);

        RectF panel = new RectF(10f, 12f, w - 10f, h - 12f);
        roundRect(c, panel, 22f, 0xff151315);
        strokeRect(c, panel, 0xff6f5c42, 3f);

        String bagTitle = activeBagOwner == 0
            ? "플레이어 가방"
            : "캐릭터 " + activeBagOwner + " 가방";
        drawText(c, bagTitle + "   8 × " + activeGridRows(),
            26f, 58f, 29f, 0xfff2e5cf, true);

        int atk = equippedAtk(activeBagOwner);
        int def = equippedDef(activeBagOwner);
        int hp = equippedHp(activeBagOwner);
        int occupied = equippedCells(activeBagOwner);

        drawText(c, occupied + "/" + activeGridCapacity() + "칸", 26f, 90f, 16f, MUTED, false);
        drawText(c, "공 +" + atk + "  방 +" + def + "  체 +" + hp,
            w - 26f, 90f, 17f, 0xffffce56, true, Paint.Align.RIGHT);

        float cell = Math.min((w - 42f) / GRID_W, 92f);
        float gridW = cell * GRID_W;
        float gridH = cell * activeGridRows();
        float gridLeft = (w - gridW) * .5f;
        float gridTop = 116f;
        bagGridRect.set(gridLeft, gridTop, gridLeft + gridW, gridTop + gridH);

        roundRect(c, bagGridRect, 8f, 0xff0c0d0f);
        strokeRect(c, bagGridRect, 0xff5d5140, 2f);

        for (int index = 0; index < activeGridCapacity(); index++) {
            int x = index % GRID_W;
            int y = index / GRID_W;
            RectF r = bagCellRect(x, y, cell);
            int col = 0xff1a1a1f;
            if (draggingBlock != null && previewContains(draggingBlock, x, y)) {
                col = canPlace(draggingBlock, dragPreviewX, dragPreviewY,
                    draggingBlock.rotation, draggingBlock.id)
                    ? 0xff275b34 : 0xff702929;
            }
            roundRect(c, inset(r, 2f), 4f, col);
        }

        for (GearBlock b : items) {
            if (b.location == LOC_BAG && b.bagOwner == activeBagOwner)
                drawBagBlock(c, b, cell);
        }

        float y = bagGridRect.bottom + 25f;
        drawText(c, "소지품  " + countLocation(LOC_HELD) + "   ·   블록 탭=90° 회전", 24f, y + 22f, 17f, TXT, true);
        heldRect.set(18f, y + 34f, w - 18f, y + 165f);
        drawStorageRow(c, LOC_HELD, heldRect, heldHits);

        y = heldRect.bottom + 22f;
        drawText(c, "바닥  " + countLocation(LOC_GROUND) + "   ·   가챠 보상 도착", 24f, y + 22f, 17f, TXT, true);
        groundRect.set(18f, y + 34f, w - 18f, y + 165f);
        drawStorageRow(c, LOC_GROUND, groundRect, groundHits);

        float detailTop = groundRect.bottom + 25f;
        if (selectedBlock == null) {
            drawText(c, "탭=90° 회전 · 드래그=가방/소지품/바닥 이동",
                w / 2f, detailTop + 20f, 16f, 0xffd7cec0, false, Paint.Align.CENTER);
        } else {
            String ext = selectedBlock.exclusive
                ? " · 캐릭터 " + selectedBlock.characterId + " 전용"
                : "";
            drawText(c,
                selectedBlock.name + (selectedBlock.enhance > 0 ? " +" + selectedBlock.enhance : "") + ext,
                w / 2f, detailTop + 16f, 17f, TXT, true, Paint.Align.CENTER);
            drawText(c,
                "공 +" + selectedBlock.atk + "  방 +" + selectedBlock.def + "  체 +" + selectedBlock.hp,
                w / 2f, detailTop + 44f, 16f, 0xffffcf5a, true, Paint.Align.CENTER);
        }

        float buttonTop = h - 88f;
        tidyBtn.set(24f, buttonTop, w * .48f, h - 24f);
        closeBagBtn.set(w * .52f, buttonTop, w - 24f, h - 24f);
        drawButton(c, tidyBtn, "가방 자동 정리", 0xff44392e, 17f, true);
        drawButton(c, closeBagBtn, "닫기", 0xff512027, 17f, true);
    }

    private RectF bagCellRect(int x, int y, float cell) {
        return new RectF(
            bagGridRect.left + x * cell,
            bagGridRect.top + y * cell,
            bagGridRect.left + (x + 1) * cell,
            bagGridRect.top + (y + 1) * cell
        );
    }

    private void drawBagBlock(Canvas c, GearBlock b, float cell) {
        int ox = b.x, oy = b.y;
        if (draggingBlock == b && dragPreviewX > -50) {
            ox = dragPreviewX;
            oy = dragPreviewY;
        }

        for (Cell q : rotatedCells(b, b.rotation)) {
            int gx = ox + q.x, gy = oy + q.y;
            if (!isCellUnlocked(gx, gy)) continue;

            RectF r = inset(bagCellRect(gx, gy, cell), 4f);
            roundRect(c, r, 5f, blockColor(b));
            roundRect(c, new RectF(r.left + cell * .15f, r.top + cell * .15f,
                r.right - cell * .15f, r.bottom - cell * .15f), 3f, 0xdd15171b);
        }

        RectF badge = blockBounds(b, cell);
        if (badge != null) {
            float bh = Math.min(30f, cell * .32f);
            RectF rr = new RectF(badge.left, badge.bottom - bh, badge.right, badge.bottom);
            roundRect(c, rr, 4f, 0xcc111113);
            drawText(c,
                (b.enhance > 0 ? "+" + b.enhance + " " : "") +
                "공" + b.atk + " 방" + b.def + " 체" + b.hp,
                rr.centerX(), rr.centerY() + 5f, Math.min(14f, cell * .14f),
                0xffffdd78, true, Paint.Align.CENTER);
        }
    }

    private RectF blockBounds(GearBlock b, float cell) {
        List<Cell> cells = rotatedCells(b, b.rotation);
        if (cells.isEmpty()) return null;
        int maxX = 0, maxY = 0;
        for (Cell q : cells) {
            maxX = Math.max(maxX, q.x);
            maxY = Math.max(maxY, q.y);
        }
        return new RectF(
            bagGridRect.left + b.x * cell,
            bagGridRect.top + b.y * cell,
            bagGridRect.left + (b.x + maxX + 1) * cell,
            bagGridRect.top + (b.y + maxY + 1) * cell
        );
    }

    private void drawStorageRow(Canvas c, int location, RectF zone, ArrayList<RowHit> hits) {
        hits.clear();
        roundRect(c, zone, 8f, 0xff0e0d10);
        strokeRect(c, zone, 0xff4a4138, 2f);

        ArrayList<GearBlock> list = new ArrayList<>();
        for (GearBlock b : items) if (b.location == location) list.add(b);

        if (list.isEmpty()) {
            drawText(c, "(없음)", zone.left + 18f, zone.centerY() + 7f, 17f, 0xff706c69, false);
            return;
        }

        float x = zone.left + 8f;
        float cardW = Math.min(220f, zone.width() * .38f);
        for (GearBlock b : list) {
            if (x + cardW > zone.right - 4f) break;
            RectF card = new RectF(x, zone.top + 8f, x + cardW, zone.bottom - 8f);
            roundRect(c, card, 8f, selectedBlock == b ? 0xff392c24 : 0xff1d1b1d);
            strokeRect(c, card, b.exclusive ? 0xff9d4aba : 0xff5a4d42, 2f);

            drawText(c, b.name + (b.enhance > 0 ? " +" + b.enhance : ""),
                card.left + 10f, card.top + 27f, 14f, TXT, true);
            drawText(c, b.cellCount + "칸  공" + b.atk + " 방" + b.def + " 체" + b.hp,
                card.left + 10f, card.top + 55f, 13f, MUTED, false);
            drawText(c, "[탭=회전]", card.left + 10f, card.bottom - 12f, 12f, 0xffc8a66e, true);

            hits.add(new RowHit(card, b));
            x += cardW + 8f;
        }

        if (list.size() > hits.size())
            drawText(c, "+" + (list.size() - hits.size()) + "개", zone.right - 12f, zone.bottom - 12f, 13f, MUTED, true, Paint.Align.RIGHT);
    }

    private void drawForge(Canvas c, long now) {
        int w = getWidth(), h = getHeight();
        c.drawColor(0xff120b07);

        RectF panel = new RectF(10f, 12f, w - 10f, h - 12f);
        roundRect(c, panel, 22f, 0xff17100d);
        strokeRect(c, panel, 0xff9c5c24, 3f);

        drawText(c, "⚒  대 장 간", 24f, 58f, 32f, 0xffffb34c, true);
        drawText(c, "강화 · 합성 · 분해", 25f, 88f, 15f, 0xffb9a28d, false);

        float tabTop = 112f, tabH = 58f, gap = 8f;
        float tw = (w - 40f - gap * 2f) / 3f;
        forgeEnhanceTab.set(20f, tabTop, 20f + tw, tabTop + tabH);
        forgeSynthesisTab.set(20f + tw + gap, tabTop, 20f + tw * 2f + gap, tabTop + tabH);
        forgeDismantleTab.set(20f + tw * 2f + gap * 2f, tabTop, w - 20f, tabTop + tabH);

        drawButton(c, forgeEnhanceTab, "강화", forgeMode == ForgeMode.ENHANCE ? 0xff632c14 : 0xff2b1c17, 18f, true);
        drawButton(c, forgeSynthesisTab, "합성", forgeMode == ForgeMode.SYNTHESIS ? 0xff632c14 : 0xff2b1c17, 18f, true);
        drawButton(c, forgeDismantleTab, "분해", forgeMode == ForgeMode.DISMANTLE ? 0xff632c14 : 0xff2b1c17, 18f, true);

        String rule;
        if (forgeMode == ForgeMode.ENHANCE)
            rule = "재료 1개당 +10% · 성공: 공+1 체+5 · 짝수 강화마다 방+1 · 최대 +10";
        else if (forgeMode == ForgeMode.SYNTHESIS)
            rule = "대상 1개 + 재료 1개 · 성공률 1% · 성공 시 재료의 공/방/체 흡수";
        else
            rule = "재료 최대 10개 · 1개=10% … 10개=100% · 성공 시 플레이어 가방 +1칸";

        drawWrapped(c, rule, 24f, 202f, w - 48f, 15f, 0xffe3d0bd, false);

        forgeHits.clear();
        float listTop = 248f;
        float rowH = 80f;
        float y = listTop;
        int shown = 0;

        for (GearBlock b : items) {
            if (y + rowH > h - 255f) break;

            boolean target = b.id.equals(forgeTargetId);
            boolean material = forgeMaterialIds.contains(b.id);
            RectF row = new RectF(20f, y, w - 20f, y + rowH - 6f);
            roundRect(c, row, 8f, target ? 0xff5b2d10 : (material ? 0xff29351d : 0xff211a17));
            strokeRect(c, row, target ? 0xffffa13a : (material ? 0xff95b86a : 0xff51433a), 2f);

            String where = b.location == LOC_BAG
                ? (b.bagOwner == 0 ? "플레이어 가방" : "캐릭터 " + b.bagOwner + " 가방")
                : (b.location == LOC_HELD ? "소지" : "바닥");

            drawText(c,
                (target ? "[대상] " : material ? "[재료] " : "") +
                "[" + where + "] " + b.name + (b.enhance > 0 ? " +" + b.enhance : ""),
                row.left + 12f, row.top + 28f, 15f, TXT, true);
            drawText(c, b.cellCount + "칸 · 공 " + b.atk + " / 방 " + b.def + " / 체 " + b.hp,
                row.left + 12f, row.top + 55f, 14f, MUTED, false);

            forgeHits.add(new RowHit(row, b));
            y += rowH;
            shown++;
        }

        if (shown == 0)
            drawText(c, "사용할 블록이 없습니다.", w / 2f, listTop + 60f, 18f, MUTED, false, Paint.Align.CENTER);

        float infoTop = h - 235f;
        GearBlock target = findById(forgeTargetId);

        if (forgeMode == ForgeMode.ENHANCE) {
            int chance = Math.min(100, forgeMaterialIds.size() * 10);
            drawText(c, "대상: " + (target == null ? "없음" : target.name + " +" + target.enhance) +
                "   /   재료 " + forgeMaterialIds.size() + "개",
                24f, infoTop, 16f, TXT, true);
            drawText(c, "강화 성공률 " + chance + "%", w / 2f, infoTop + 38f, 23f, 0xffffa93a, true, Paint.Align.CENTER);
        } else if (forgeMode == ForgeMode.SYNTHESIS) {
            drawText(c, "대상: " + (target == null ? "없음" : target.name) +
                "   /   재료 " + (forgeMaterialIds.isEmpty() ? "없음" : nameOf(forgeMaterialIds.get(0))),
                24f, infoTop, 16f, TXT, true);
            drawText(c, "합성 성공률 1%", w / 2f, infoTop + 38f, 23f, 0xffffa93a, true, Paint.Align.CENTER);
        } else {
            int chance = Math.min(100, forgeMaterialIds.size() * 10);
            drawText(c, "분해 재료 " + forgeMaterialIds.size() + "/10 · 현재 플레이어 가방 " + playerGridCapacity() + "칸",
                24f, infoTop, 16f, TXT, true);
            drawText(c, "가방 확장 성공률 " + chance + "%", w / 2f, infoTop + 38f, 23f, 0xffffa93a, true, Paint.Align.CENTER);
        }

        drawText(c, forgeMessage, w / 2f, infoTop + 76f, 15f, 0xffdfd6cc, false, Paint.Align.CENTER);

        forgeClearBtn.set(22f, h - 104f, w * .39f, h - 28f);
        forgeExecuteBtn.set(w * .42f, h - 104f, w * .78f, h - 28f);
        forgeCloseBtn.set(w * .81f, h - 104f, w - 22f, h - 28f);
        drawButton(c, forgeClearBtn, "초기화", 0xff30241f, 15f, true);
        drawButton(c, forgeExecuteBtn, "작업 실행", 0xff6c2c12, 16f, true);
        drawButton(c, forgeCloseBtn, "닫기", 0xff30241f, 15f, true);
    }

    private void drawJail(Canvas c, long now) {
        int w = getWidth(), h = getHeight();
        c.drawColor(0xff080b12);

        RectF panel = new RectF(10f, 12f, w - 10f, h - 12f);
        roundRect(c, panel, 22f, 0xff111722);
        strokeRect(c, panel, 0xff536985, 3f);

        drawText(c, "▥  감 옥", 24f, 58f, 32f, 0xffdce8ff, true);
        drawText(c, "포획 캐릭터 · 캐릭터별 독립 8×3 장비 가방", 24f, 88f, 15f, 0xff8fa5c5, false);

        jailHits.clear();
        float top = 118f;
        float rowH = Math.min(82f, (h - 340f) / 10f);

        for (int charId = 1; charId <= 10; charId++) {
            boolean captured = isCaptured(charId);
            RectF row = new RectF(20f, top + (charId - 1) * rowH, w - 20f, top + charId * rowH - 5f);
            int col = !captured ? 0xff171a21 : (jailSelectedCharacter == charId ? 0xff243d61 : 0xff18283d);
            roundRect(c, row, 7f, col);
            strokeRect(c, row, captured ? 0xff526f98 : 0xff343943, 2f);

            String stat = captured
                ? "가방 " + equippedCells(charId) + "/24 · 공+" + equippedAtk(charId) +
                  " 방+" + equippedDef(charId) + " 체+" + equippedHp(charId)
                : "0% 룰렛 포획 성공 시 해금";

            drawText(c, "캐릭터 " + charId + "   [" + (captured ? "포획됨" : "미포획") + "]",
                row.left + 10f, row.top + row.height() * .40f, 14f,
                captured ? 0xffeef4ff : 0xff777c86, true);
            drawText(c, stat, row.left + 10f, row.top + row.height() * .73f, 12f,
                captured ? 0xffaebed6 : 0xff555962, false);

            jailHits.add(new RowHit(row, charId));
        }

        float infoTop = top + rowH * 10f + 18f;
        if (jailSelectedCharacter > 0 && isCaptured(jailSelectedCharacter)) {
            drawText(c, "캐릭터 " + jailSelectedCharacter + " 전용 가방  8×3 / 24칸",
                w / 2f, infoTop + 20f, 18f, TXT, true, Paint.Align.CENTER);
            drawText(c,
                "장착 합계  공+" + equippedAtk(jailSelectedCharacter) +
                "  방+" + equippedDef(jailSelectedCharacter) +
                "  체+" + equippedHp(jailSelectedCharacter),
                w / 2f, infoTop + 50f, 16f, 0xffcbd8ee, true, Paint.Align.CENTER);
        } else {
            drawText(c, "아직 포획된 캐릭터가 없습니다.",
                w / 2f, infoTop + 34f, 17f, MUTED, false, Paint.Align.CENTER);
        }

        jailOpenBagBtn.set(w * .12f, h - 118f, w * .72f, h - 34f);
        jailCloseBtn.set(w * .75f, h - 118f, w - 22f, h - 34f);
        drawButton(c, jailOpenBagBtn, "선택 캐릭터 가방 열기", 0xff1c3452, 16f, true);
        drawButton(c, jailCloseBtn, "닫기", 0xff202a38, 15f, true);
    }

    private void resetCombat() {
        enemyMaxHp = currentEnemyMaxHp();
        enemyHp = enemyMaxHp;
        hitCount = 0;
        won = false;
        weakActive = false;
        actionBitmap = null;
        actionUntil = 0;
        jellyUntil = 0;
        hitFxUntil = 0;
        bubbleUntil = 0;
        inputLockUntil = 0;
    }

    private int combatStage() {
        float q = enemyHp / (float)Math.max(1, enemyMaxHp);
        if (q <= 0f) return 4;
        if (q <= .25f) return 3;
        if (q <= .50f) return 2;
        if (q <= .75f) return 1;
        return 0;
    }

    private void beginCombat() {
        resetCombat();
        mode = Mode.COMBAT;
        invalidate();
    }

    private void processGesture(float sx, float sy, float ex, float ey, long durationMs) {
        long now = System.currentTimeMillis();
        if (won || now < inputLockUntil) return;
        inputLockUntil = now + 90L;

        float dx = ex - sx, dy = ey - sy;
        float dist = (float)Math.hypot(dx, dy);
        float swipeThreshold = Math.max(85f, getWidth() * .085f);
        boolean swipe = dist >= swipeThreshold && durationMs <= 650L;
        float impactX = swipe ? lerp(sx, ex, .55f) : ex;
        float impactY = swipe ? lerp(sy, ey, .55f) : ey;

        int zone = zoneOf(impactX, impactY);
        if (zone == 6) {
            showBubble(randomLine(zoneTalk[zone]), 1000L);
            playSfx("sparkle", .55f);
            startHitFx(impactX, impactY, false);
            invalidate();
            return;
        }

        float weakPx = weakX * getWidth(), weakPy = weakY * getHeight();
        boolean weakHit = weakActive &&
            Math.hypot(weakPx - impactX, weakPy - impactY) <= Math.max(58f, getWidth() * .06f);

        boolean dodged = !weakHit && rng.nextFloat() < .17f;
        if (dodged) {
            weakActive = false;
            setActionBitmap("d");
            showBubble(randomLine(dodgeTalk), 1000L);
            playSfx("fight_swing_light", .62f);
            if (rng.nextFloat() < .45f) playVoice("female_gasp1", .68f);
            startHitFx(impactX, impactY, false);
            invalidate();
            return;
        }

        String prefix = prefixFor(zone, swipe, dx, dy);
        setActionBitmap(prefix);

        int baseDamage = weakHit ? 18 : (swipe ? 8 : 5);
        int gearAttack = equippedAtk(0);
        int damage = baseDamage + Math.max(0, Math.round(gearAttack * (weakHit ? .20f : .08f)));
        enemyHp = Math.max(0, enemyHp - damage);
        hitCount++;

        startJelly(impactX, impactY, weakHit, swipe, dx, dy);
        startHitFx(impactX, impactY, weakHit);

        if (weakHit) {
            weakActive = false;
            showBubble(randomLine(criticalTalk), 1250L);
            playCombatImpact(prefix, swipe, true);
            playRandomVoice(criticalHitVoices, .84f);
        } else {
            showBubble(randomLine(zoneTalk[zone]), 1050L);
            playCombatImpact(prefix, swipe, false);
            if (enemyHp <= Math.round(enemyMaxHp * .25f) && rng.nextFloat() < .45f)
                playRandomVoice(lowHpVoices, .72f);
            else
                playRandomVoice(swipe ? swipeHitVoices : normalHitVoices, swipe ? .78f : .66f);
        }

        if (enemyHp <= 0) {
            won = true;
            weakActive = false;

            if (currentStep >= maxUnlockedStep) {
                maxUnlockedStep = currentStep + 1;
                saveProgress();
            }

            showBubble("…끝났어.", 30000L);
            playSfx("win", 1f);

            handler.postDelayed(new Runnable() {
                @Override public void run() {
                    if (won && mode == Mode.COMBAT) startGacha();
                }
            }, 450L);
        } else if (!weakActive && hitCount >= 4) {
            hitCount = 0;
            startWeakPoint();
        }

        invalidate();
    }

    private int zoneOf(float x, float y) {
        float nx = x / Math.max(1f, getWidth());
        float ny = y / Math.max(1f, getHeight());
        if (nx < .16f || nx > .84f) return 6;
        if (ny < .16f) return 0;
        if (ny < .30f) return 1;
        if (ny < .48f) return 2;
        if (ny < .59f && nx > .37f && nx < .63f) return 3;
        if (ny < .79f) return 4;
        return 5;
    }

    private String prefixFor(int zone, boolean swipe, float dx, float dy) {
        if (swipe) {
            if (Math.abs(dy) > Math.abs(dx)) return "b";
            return dy < 0 ? "p" : "k";
        }
        if (zone <= 1) return "p";
        if (zone <= 3) return "b";
        return "k";
    }

    private void setActionBitmap(String prefix) {
        Bitmap[] arr = actionBitmaps.get(currentVisualFloor() + "_" + prefix);
        if (arr == null || arr.length == 0) return;
        Bitmap chosen = arr[rng.nextInt(arr.length)];
        if (chosen != null) {
            actionBitmap = chosen;
            actionUntil = System.currentTimeMillis() + 220L;
        }
    }

    private void playCombatImpact(String prefix, boolean swipe, boolean critical) {
        if (critical) {
            playSfx("fight_swing_heavy", .78f);
            playSfx("fight_smash_heavy", 1f);
            return;
        }
        if (swipe) playSfx("k".equals(prefix) ? "fight_swing_heavy" : "fight_swing_light", .66f);
        if ("k".equals(prefix)) playSfx("fight_punch_medium", .95f);
        else if ("b".equals(prefix)) playSfx("fight_punch_medium", .88f);
        else playSfx(rng.nextFloat() < .55f ? "fight_punch_light" : "fight_punch_medium", .82f);
    }

    private void startJelly(float x, float y, boolean heavy, boolean swipe, float dx, float dy) {
        long now = System.currentTimeMillis();
        jellyStart = now;
        jellyUntil = now + (heavy ? 420L : (swipe ? 340L : 280L));
        jellyX = x;
        jellyY = y;
        jellyHeavy = heavy;
        jellySwipe = swipe;
        jellySwipeX = dx;
        jellySwipeY = dy;
    }

    private void startHitFx(float x, float y, boolean heavy) {
        long now = System.currentTimeMillis();
        hitFxStart = now;
        hitFxUntil = now + (heavy ? 260L : 200L);
        hitFxX = x;
        hitFxY = y;
        hitFxHeavy = heavy;
    }

    private void startWeakPoint() {
        weakActive = true;
        weakUntil = System.currentTimeMillis() + 1050L;
        weakX = .34f + rng.nextFloat() * .32f;
        weakY = .34f + rng.nextFloat() * .34f;

        float angle = rng.nextFloat() * (float)Math.PI * 2f;
        weakVx = (float)Math.cos(angle) * .34f;
        weakVy = (float)Math.sin(angle) * .34f;
    }

    private void updateWeakPoint(long now, float dt) {
        if (!weakActive || mode != Mode.COMBAT) return;
        if (now >= weakUntil) {
            weakActive = false;
            return;
        }

        weakX += weakVx * dt;
        weakY += weakVy * dt;

        if (weakX < .25f || weakX > .75f) {
            weakVx *= -1f;
            weakX = Math.max(.25f, Math.min(.75f, weakX));
        }
        if (weakY < .27f || weakY > .73f) {
            weakVy *= -1f;
            weakY = Math.max(.27f, Math.min(.73f, weakY));
        }
    }

    private void showBubble(String text, long duration) {
        bubbleText = text;
        bubbleStart = System.currentTimeMillis();
        bubbleUntil = bubbleStart + duration;
    }

    private String randomLine(String[] lines) { return lines[rng.nextInt(lines.length)]; }

    private void startGacha() {
        mode = Mode.GACHA;
        gachaCorrectionIndex = rng.nextInt(corrections.length);
        gachaCorrection = corrections[gachaCorrectionIndex];
        float segment = 360f / corrections.length;
        gachaTotalSpin = 360f * (4 + rng.nextInt(3)) + gachaCorrectionIndex * segment;
        gachaStart = System.currentTimeMillis();
        gachaResolved = false;
        gachaReady = false;
        pendingOutcome = null;
        invalidate();
    }

    private void resolveGacha() {
        if (gachaResolved) return;
        gachaResolved = true;

        pendingOutcome = new GachaOutcome();
        pendingOutcome.correction = gachaCorrection;

        int charId = currentCharacterId();

        if (gachaCorrection == 0 && !isCaptured(charId)) {
            pendingOutcome.captureAttempt = true;
            pendingOutcome.captureSucceeded = rng.nextFloat() < .01f;
            if (pendingOutcome.captureSucceeded)
                prefs.edit().putBoolean("captured_char_" + charId, true).apply();
        } else {
            pendingOutcome.block = rollBlock(gachaCorrection, gachaCorrection == 0 && isCaptured(charId));
        }

        gachaReady = true;
        invalidate();
    }

    private GearBlock rollBlock(int correction, boolean exclusive) {
        int sizeIndex = rng.nextInt(allowedSizes.length);
        int cells = allowedSizes[sizeIndex];
        int budget = baseBudgets[sizeIndex];
        int total = Math.max(3, Math.round(budget * (1f + correction / 100f)));

        float a = .15f + rng.nextFloat() * .55f;
        float d = .10f + rng.nextFloat() * .55f;
        float h = .10f + rng.nextFloat() * .55f;
        float sum = a + d + h;

        GearBlock b = new GearBlock();
        b.id = UUID.randomUUID().toString().replace("-", "");
        b.cellCount = cells;
        b.atk = Math.max(1, Math.round(total * a / sum));
        b.def = Math.max(1, Math.round(total * d / sum));
        b.hp = Math.max(1, total - b.atk - b.def);
        b.correction = correction;
        b.exclusive = exclusive;
        b.characterId = currentCharacterId();
        b.name = exclusive
            ? "캐릭터 " + b.characterId + " 전용 " + nouns[rng.nextInt(nouns.length)]
            : prefixes[rng.nextInt(prefixes.length)] + " " + nouns[rng.nextInt(nouns.length)];
        b.shape = encodeShape(shapeFor(cells));
        b.x = -1;
        b.y = -1;
        b.rotation = 0;
        b.location = LOC_GROUND;
        b.bagOwner = 0;
        return b;
    }

    private boolean isCaptured(int characterId) {
        return prefs.getBoolean("captured_char_" + characterId, false);
    }

    private void collectGacha() {
        if (!gachaReady || pendingOutcome == null) return;

        if (pendingOutcome.block != null) {
            pendingOutcome.block.location = LOC_GROUND;
            pendingOutcome.block.bagOwner = 0;
            pendingOutcome.block.x = -1;
            pendingOutcome.block.y = -1;
            items.add(pendingOutcome.block);
            saveItems();
        }

        pendingOutcome = null;
        resetCombat();
        mode = Mode.MAIN;
        invalidate();
    }

    private int playerGridCapacity() {
        return BASE_GRID_CELLS + Math.max(0, prefs.getInt("bag_extra_cells", 0));
    }

    private int activeGridCapacity() {
        return activeBagOwner == 0 ? playerGridCapacity() : BASE_GRID_CELLS;
    }

    private int activeGridRows() {
        return Math.max(3, (activeGridCapacity() + GRID_W - 1) / GRID_W);
    }

    private boolean isCellUnlocked(int x, int y) {
        if (x < 0 || x >= GRID_W || y < 0) return false;
        int index = y * GRID_W + x;
        return index >= 0 && index < activeGridCapacity();
    }

    private int equippedAtk(int owner) {
        int total = 0;
        for (GearBlock b : items)
            if (b.location == LOC_BAG && b.bagOwner == owner) total += Math.max(0, b.atk);
        return total;
    }

    private int equippedDef(int owner) {
        int total = 0;
        for (GearBlock b : items)
            if (b.location == LOC_BAG && b.bagOwner == owner) total += Math.max(0, b.def);
        return total;
    }

    private int equippedHp(int owner) {
        int total = 0;
        for (GearBlock b : items)
            if (b.location == LOC_BAG && b.bagOwner == owner) total += Math.max(0, b.hp);
        return total;
    }

    private int equippedCells(int owner) {
        int total = 0;
        for (GearBlock b : items)
            if (b.location == LOC_BAG && b.bagOwner == owner) total += Math.max(0, b.cellCount);
        return total;
    }

    private int countLocation(int location) {
        int n = 0;
        for (GearBlock b : items) if (b.location == location) n++;
        return n;
    }

    private List<Cell> shapeFor(int count) {
        ArrayList<List<Cell>> opts = new ArrayList<>();

        if (count == 1) {
            opts.add(cells(0,0));
        } else if (count == 2) {
            opts.add(cells(0,0,1,0));
        } else if (count == 3) {
            opts.add(cells(0,0,1,0,2,0));
            opts.add(cells(0,0,0,1,1,0));
        } else if (count == 4) {
            opts.add(cells(0,0,1,0,2,0,3,0));
            opts.add(cells(0,0,1,0,0,1,1,1));
            opts.add(cells(0,0,1,0,2,0,1,1));
            opts.add(cells(0,0,0,1,0,2,1,0));
            opts.add(cells(0,0,1,0,1,1,2,1));
        } else if (count == 5) {
            opts.add(cells(0,0,1,0,2,0,3,0,4,0));
            opts.add(cells(0,0,0,1,0,2,0,3,1,0));
            opts.add(cells(0,0,1,0,2,0,1,1,1,2));
            opts.add(cells(0,0,2,0,0,1,1,1,2,1));
            opts.add(cells(0,0,0,1,0,2,1,2,2,2));
        } else if (count == 6) {
            opts.add(cells(0,0,1,0,2,0,0,1,1,1,2,1));
            opts.add(cells(0,0,1,0,2,0,3,0,4,0,5,0));
            opts.add(cells(0,0,0,1,0,2,0,3,1,0,2,0));
        } else if (count == 9) {
            opts.add(cells(0,0,1,0,2,0,0,1,1,1,2,1,0,2,1,2,2,2));
            opts.add(cells(0,0,1,0,2,0,3,0,4,0,0,1,1,1,2,1,3,1));
        } else {
            opts.add(cells(0,0,1,0,2,0,3,0,0,1,1,1,2,1,3,1,0,2,1,2,2,2,3,2));
            opts.add(cells(0,0,1,0,2,0,3,0,4,0,5,0,0,1,1,1,2,1,3,1,4,1,5,1));
        }

        return opts.get(rng.nextInt(opts.size()));
    }

    private ArrayList<Cell> cells(int... xy) {
        ArrayList<Cell> out = new ArrayList<>();
        for (int i = 0; i + 1 < xy.length; i += 2)
            out.add(new Cell(xy[i], xy[i + 1]));
        return out;
    }

    private String encodeShape(List<Cell> shape) {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < shape.size(); i++) {
            if (i > 0) sb.append(';');
            sb.append(shape.get(i).x).append(',').append(shape.get(i).y);
        }
        return sb.toString();
    }

    private List<Cell> decodeShape(String encoded) {
        ArrayList<Cell> out = new ArrayList<>();
        if (encoded == null || encoded.length() == 0) {
            out.add(new Cell(0, 0));
            return out;
        }

        for (String pt : encoded.split(";")) {
            String[] xy = pt.split(",");
            if (xy.length == 2) {
                try {
                    out.add(new Cell(Integer.parseInt(xy[0]), Integer.parseInt(xy[1])));
                } catch (Exception ignored) {}
            }
        }

        if (out.isEmpty()) out.add(new Cell(0, 0));
        return out;
    }

    private List<Cell> rotatedCells(GearBlock b, int rotation) {
        List<Cell> src = decodeShape(b.shape);
        ArrayList<Cell> out = new ArrayList<>();

        for (Cell p : src) {
            int x = p.x, y = p.y;
            for (int r = 0; r < rotation; r++) {
                int nx = -y, ny = x;
                x = nx; y = ny;
            }
            out.add(new Cell(x, y));
        }

        int minX = 999, minY = 999;
        for (Cell p : out) {
            minX = Math.min(minX, p.x);
            minY = Math.min(minY, p.y);
        }
        for (Cell p : out) {
            p.x -= minX;
            p.y -= minY;
        }
        return out;
    }

    private boolean canPlace(GearBlock item, int ox, int oy, int rotation, String ignoreId) {
        if (item.exclusive && activeBagOwner != item.characterId) return false;
        if (activeBagOwner > 0 && !isCaptured(activeBagOwner)) return false;

        HashSet<Integer> occupied = new HashSet<>();

        for (GearBlock other : items) {
            if (other.id.equals(ignoreId)) continue;
            if (other.location != LOC_BAG || other.bagOwner != activeBagOwner) continue;
            if (other.x < 0 || other.y < 0) continue;

            for (Cell p : rotatedCells(other, other.rotation)) {
                int x = other.x + p.x, y = other.y + p.y;
                if (isCellUnlocked(x, y)) occupied.add(y * GRID_W + x);
            }
        }

        for (Cell p : rotatedCells(item, rotation)) {
            int x = ox + p.x, y = oy + p.y;
            if (!isCellUnlocked(x, y)) return false;
            if (occupied.contains(y * GRID_W + x)) return false;
        }
        return true;
    }

    private boolean findFirstPlacement(GearBlock item) {
        int startRot = item.rotation;
        for (int dr = 0; dr < 4; dr++) {
            int rot = (startRot + dr) % 4;
            for (int y = 0; y < activeGridRows(); y++) {
                for (int x = 0; x < GRID_W; x++) {
                    if (canPlace(item, x, y, rot, item.id)) {
                        item.location = LOC_BAG;
                        item.bagOwner = activeBagOwner;
                        item.x = x;
                        item.y = y;
                        item.rotation = rot;
                        return true;
                    }
                }
            }
        }
        item.rotation = startRot;
        return false;
    }

    private GearBlock findBagBlockAt(float px, float py) {
        if (!bagGridRect.contains(px, py)) return null;
        float cell = bagGridRect.width() / GRID_W;
        int gx = (int)((px - bagGridRect.left) / cell);
        int gy = (int)((py - bagGridRect.top) / cell);

        for (int i = items.size() - 1; i >= 0; i--) {
            GearBlock b = items.get(i);
            if (b.location != LOC_BAG || b.bagOwner != activeBagOwner) continue;
            for (Cell q : rotatedCells(b, b.rotation))
                if (b.x + q.x == gx && b.y + q.y == gy) return b;
        }
        return null;
    }

    private GearBlock findStorageBlockAt(float x, float y) {
        for (RowHit hit : heldHits)
            if (hit.rect.contains(x, y) && hit.block != null) return hit.block;
        for (RowHit hit : groundHits)
            if (hit.rect.contains(x, y) && hit.block != null) return hit.block;
        return null;
    }

    private void rotateBlock(GearBlock b) {
        if (b == null) return;
        int next = (b.rotation + 1) % 4;

        if (b.location == LOC_BAG) {
            int oldOwner = activeBagOwner;
            activeBagOwner = b.bagOwner;
            boolean ok = canPlace(b, b.x, b.y, next, b.id);
            activeBagOwner = oldOwner;
            if (!ok) {
                showBubble("그 자리에서는 회전할 공간이 부족합니다.", 1200L);
                return;
            }
        }

        b.rotation = next;
        saveItems();
        invalidate();
    }

    private void beginBlockPointer(GearBlock b, float px, float py) {
        pointerBlock = b;
        selectedBlock = b;
        draggingBlock = null;
        movedSinceDown = false;

        if (b == null) return;

        dragOriginalX = b.x;
        dragOriginalY = b.y;
        dragOriginalLocation = b.location;
        dragOriginalOwner = b.bagOwner;

        dragOffsetX = 0;
        dragOffsetY = 0;

        if (b.location == LOC_BAG && bagGridRect.contains(px, py)) {
            float cell = bagGridRect.width() / GRID_W;
            int gx = (int)((px - bagGridRect.left) / cell);
            int gy = (int)((py - bagGridRect.top) / cell);
            dragOffsetX = Math.max(0, gx - b.x);
            dragOffsetY = Math.max(0, gy - b.y);
        }
    }

    private void maybeStartDrag(float px, float py) {
        if (pointerBlock == null || draggingBlock != null) return;
        if (Math.hypot(px - downX, py - downY) < 20f) return;
        draggingBlock = pointerBlock;
        movedSinceDown = true;
        updateBagDrag(px, py);
    }

    private void updateBagDrag(float px, float py) {
        if (draggingBlock == null) return;

        if (bagGridRect.contains(px, py)) {
            float cell = bagGridRect.width() / GRID_W;
            int gx = (int)Math.floor((px - bagGridRect.left) / cell);
            int gy = (int)Math.floor((py - bagGridRect.top) / cell);
            dragPreviewX = gx - dragOffsetX;
            dragPreviewY = gy - dragOffsetY;
        } else {
            dragPreviewX = dragPreviewY = -99;
        }

        invalidate();
    }

    private void endBagPointer(float px, float py) {
        if (pointerBlock == null) return;

        if (draggingBlock == null) {
            rotateBlock(pointerBlock);
            pointerBlock = null;
            return;
        }

        GearBlock b = draggingBlock;
        boolean moved = false;

        if (bagGridRect.contains(px, py)) {
            float cell = bagGridRect.width() / GRID_W;
            int gx = (int)Math.floor((px - bagGridRect.left) / cell);
            int gy = (int)Math.floor((py - bagGridRect.top) / cell);
            int tx = gx - dragOffsetX;
            int ty = gy - dragOffsetY;

            if (canPlace(b, tx, ty, b.rotation, b.id)) {
                b.location = LOC_BAG;
                b.bagOwner = activeBagOwner;
                b.x = tx;
                b.y = ty;
                moved = true;
            }
        } else if (heldRect.contains(px, py)) {
            b.location = LOC_HELD;
            b.bagOwner = 0;
            b.x = b.y = -1;
            moved = true;
        } else if (groundRect.contains(px, py)) {
            b.location = LOC_GROUND;
            b.bagOwner = 0;
            b.x = b.y = -1;
            moved = true;
        }

        if (!moved) {
            b.location = dragOriginalLocation;
            b.bagOwner = dragOriginalOwner;
            b.x = dragOriginalX;
            b.y = dragOriginalY;
        }

        pointerBlock = null;
        draggingBlock = null;
        dragPreviewX = dragPreviewY = -99;
        saveItems();
        invalidate();
    }

    private boolean previewContains(GearBlock b, int x, int y) {
        if (dragPreviewX < -50) return false;
        for (Cell q : rotatedCells(b, b.rotation))
            if (dragPreviewX + q.x == x && dragPreviewY + q.y == y) return true;
        return false;
    }

    private void tidyBag() {
        ArrayList<GearBlock> list = new ArrayList<>();
        HashMap<String, int[]> backup = new HashMap<>();

        for (GearBlock b : items) {
            if (b.location != LOC_BAG || b.bagOwner != activeBagOwner) continue;
            list.add(b);
            backup.put(b.id, new int[]{b.x, b.y, b.rotation});
            b.x = b.y = -1;
        }

        Collections.sort(list, new Comparator<GearBlock>() {
            @Override public int compare(GearBlock a, GearBlock b) {
                return Integer.compare(b.cellCount, a.cellCount);
            }
        });

        boolean ok = true;
        for (GearBlock b : list) {
            if (!findFirstPlacement(b)) {
                ok = false;
                break;
            }
        }

        if (!ok) {
            for (GearBlock b : list) {
                int[] v = backup.get(b.id);
                if (v == null) continue;
                b.location = LOC_BAG;
                b.bagOwner = activeBagOwner;
                b.x = v[0];
                b.y = v[1];
                b.rotation = v[2];
            }
            showBubble("자동 정리 실패. 기존 배치를 유지합니다.", 1300L);
        } else {
            saveItems();
        }

        invalidate();
    }

    private void setForgeMode(ForgeMode next) {
        forgeMode = next;
        forgeTargetId = null;
        forgeMaterialIds.clear();
        forgeMessage = "블록을 선택하세요.";
        invalidate();
    }

    private GearBlock findById(String id) {
        if (id == null) return null;
        for (GearBlock b : items) if (id.equals(b.id)) return b;
        return null;
    }

    private String nameOf(String id) {
        GearBlock b = findById(id);
        return b == null ? "없음" : b.name;
    }

    private void forgeItemTap(GearBlock b) {
        if (b == null) return;

        if (forgeMode == ForgeMode.DISMANTLE) {
            toggleForgeMaterial(b.id, 10);
            return;
        }

        if (forgeTargetId == null) {
            forgeTargetId = b.id;
            forgeMaterialIds.remove(b.id);
            invalidate();
            return;
        }

        if (forgeTargetId.equals(b.id)) {
            forgeTargetId = null;
            forgeMaterialIds.remove(b.id);
            invalidate();
            return;
        }

        toggleForgeMaterial(b.id, forgeMode == ForgeMode.SYNTHESIS ? 1 : 10);
    }

    private void toggleForgeMaterial(String id, int max) {
        if (forgeMaterialIds.contains(id)) {
            forgeMaterialIds.remove(id);
        } else if (forgeMaterialIds.size() < max) {
            forgeMaterialIds.add(id);
        } else {
            forgeMessage = "재료는 최대 " + max + "개까지 선택할 수 있습니다.";
        }
        invalidate();
    }

    private void clearForgeSelection() {
        forgeTargetId = null;
        forgeMaterialIds.clear();
        forgeMessage = "선택을 초기화했습니다.";
        invalidate();
    }

    private void executeForge() {
        if (forgeMode == ForgeMode.ENHANCE) executeEnhance();
        else if (forgeMode == ForgeMode.SYNTHESIS) executeSynthesis();
        else executeDismantle();
        saveItems();
        invalidate();
    }

    private void executeEnhance() {
        GearBlock target = findById(forgeTargetId);

        if (target == null || forgeMaterialIds.isEmpty()) {
            forgeMessage = "대상과 재료를 선택하세요.";
            return;
        }
        if (target.enhance >= 10) {
            forgeMessage = "이미 +10 최대 강화입니다.";
            return;
        }

        int count = forgeMaterialIds.size();
        int chance = Math.min(100, count * 10);
        boolean success = rng.nextFloat() * 100f < chance;
        consumeForgeMaterials();

        if (success) {
            target.enhance++;
            target.atk += 1;
            target.hp += 5;
            if (target.enhance % 2 == 0) target.def += 1;
            forgeMessage = "강화 성공! +" + target.enhance +
                " · 공 " + target.atk + " / 방 " + target.def + " / 체 " + target.hp;
        } else {
            forgeMessage = "강화 실패. 재료 " + count + "개 소모.";
        }

        forgeTargetId = null;
        forgeMaterialIds.clear();
    }

    private void executeSynthesis() {
        GearBlock target = findById(forgeTargetId);
        if (target == null || forgeMaterialIds.size() != 1) {
            forgeMessage = "대상 1개와 재료 1개를 선택하세요.";
            return;
        }

        GearBlock material = findById(forgeMaterialIds.get(0));
        if (material == null) {
            forgeMessage = "재료 블록을 찾을 수 없습니다.";
            forgeTargetId = null;
            forgeMaterialIds.clear();
            return;
        }

        int a = material.atk, d = material.def, h = material.hp;
        String name = material.name;
        items.remove(material);

        if (rng.nextFloat() < .01f) {
            target.atk += a;
            target.def += d;
            target.hp += h;
            forgeMessage = "합성 성공! " + name + " 능력 흡수 · 공+" + a + " 방+" + d + " 체+" + h;
        } else {
            forgeMessage = "합성 실패. " + name + " 소모.";
        }

        forgeTargetId = null;
        forgeMaterialIds.clear();
    }

    private void executeDismantle() {
        if (forgeMaterialIds.isEmpty()) {
            forgeMessage = "분해할 재료를 선택하세요.";
            return;
        }

        int count = forgeMaterialIds.size();
        int chance = Math.min(100, count * 10);
        boolean success = rng.nextFloat() * 100f < chance;
        consumeForgeMaterials();

        if (success) {
            int next = Math.max(0, prefs.getInt("bag_extra_cells", 0)) + 1;
            prefs.edit().putInt("bag_extra_cells", next).apply();
            forgeMessage = "분해 성공! 플레이어 가방 +1칸 · 현재 " + playerGridCapacity() + "칸";
        } else {
            forgeMessage = "분해 실패. 재료 " + count + "개 모두 소모.";
        }

        forgeTargetId = null;
        forgeMaterialIds.clear();
    }

    private void consumeForgeMaterials() {
        ArrayList<String> ids = new ArrayList<>(forgeMaterialIds);
        for (String id : ids) {
            if (id.equals(forgeTargetId)) continue;
            GearBlock b = findById(id);
            if (b != null) items.remove(b);
        }
    }

    private void openJail() {
        if (jailSelectedCharacter <= 0 || !isCaptured(jailSelectedCharacter)) {
            jailSelectedCharacter = 0;
            for (int i = 1; i <= 10; i++) {
                if (isCaptured(i)) {
                    jailSelectedCharacter = i;
                    break;
                }
            }
        }
        mode = Mode.JAIL;
        invalidate();
    }

    private void saveItems() {
        try {
            JSONArray arr = new JSONArray();
            for (GearBlock b : items) {
                JSONObject o = new JSONObject();
                o.put("id", b.id);
                o.put("name", b.name);
                o.put("cellCount", b.cellCount);
                o.put("atk", b.atk);
                o.put("def", b.def);
                o.put("hp", b.hp);
                o.put("correction", b.correction);
                o.put("exclusive", b.exclusive);
                o.put("characterId", b.characterId);
                o.put("shape", b.shape);
                o.put("x", b.x);
                o.put("y", b.y);
                o.put("rotation", b.rotation);
                o.put("location", b.location);
                o.put("bagOwner", b.bagOwner);
                o.put("enhance", b.enhance);
                arr.put(o);
            }
            prefs.edit().putString("bag_v2", arr.toString()).apply();
        } catch (Exception ignored) {}
    }

    private void loadItems() {
        items.clear();

        String json = prefs.getString("bag_v2", "");
        if (json == null || json.length() == 0)
            json = prefs.getString("bag_v1", "");

        if (json == null || json.length() == 0) return;

        try {
            JSONArray arr = new JSONArray(json);
            for (int i = 0; i < arr.length(); i++) {
                JSONObject o = arr.getJSONObject(i);
                GearBlock b = new GearBlock();
                b.id = o.optString("id", UUID.randomUUID().toString());
                b.name = o.optString("name", "블록");
                b.cellCount = Math.max(1, o.optInt("cellCount", 1));
                b.atk = Math.max(1, o.optInt("atk", 1));
                b.def = Math.max(1, o.optInt("def", 1));
                b.hp = Math.max(1, o.optInt("hp", 1));
                b.correction = o.optInt("correction", 0);
                b.exclusive = o.optBoolean("exclusive", false);
                b.characterId = Math.max(1, Math.min(10, o.optInt("characterId", 1)));
                b.shape = o.optString("shape", "0,0");
                b.x = o.optInt("x", -1);
                b.y = o.optInt("y", -1);
                b.rotation = ((o.optInt("rotation", 0) % 4) + 4) % 4;
                b.location = o.has("location")
                    ? o.optInt("location", LOC_GROUND)
                    : (b.x >= 0 && b.y >= 0 ? LOC_BAG : LOC_HELD);
                b.bagOwner = Math.max(0, Math.min(10, o.optInt("bagOwner", 0)));
                b.enhance = Math.max(0, Math.min(10, o.optInt("enhance", 0)));

                if (b.exclusive && b.location == LOC_BAG && b.bagOwner != b.characterId) {
                    b.location = LOC_HELD;
                    b.bagOwner = 0;
                    b.x = b.y = -1;
                }
                if (b.location != LOC_BAG) {
                    b.bagOwner = 0;
                    b.x = b.y = -1;
                }

                items.add(b);
            }
        } catch (Exception ignored) {}
    }

    @Override public boolean onTouchEvent(MotionEvent e) {
        float x = e.getX(), y = e.getY();

        if (e.getActionMasked() == MotionEvent.ACTION_DOWN) {
            downX = x;
            downY = y;
            downTime = System.currentTimeMillis();
            movedSinceDown = false;

            if (mode == Mode.BAG) {
                GearBlock b = findBagBlockAt(x, y);
                if (b == null) b = findStorageBlockAt(x, y);
                beginBlockPointer(b, x, y);
            }
            return true;
        }

        if (e.getActionMasked() == MotionEvent.ACTION_MOVE) {
            if (mode == Mode.BAG && pointerBlock != null) {
                maybeStartDrag(x, y);
                if (draggingBlock != null) updateBagDrag(x, y);
            }
            return true;
        }

        if (e.getActionMasked() != MotionEvent.ACTION_UP &&
            e.getActionMasked() != MotionEvent.ACTION_CANCEL)
            return true;

        long duration = System.currentTimeMillis() - downTime;

        if (mode == Mode.MAIN) {
            if (fightBtn.contains(x, y)) {
                beginCombat();
            } else if (bagBtn.contains(x, y)) {
                activeBagOwner = 0;
                selectedBlock = null;
                mode = Mode.BAG;
                invalidate();
            } else if (forgeBtn.contains(x, y)) {
                forgeTargetId = null;
                forgeMaterialIds.clear();
                forgeMessage = "블록을 선택하세요.";
                mode = Mode.FORGE;
                invalidate();
            } else if (jailBtn.contains(x, y)) {
                openJail();
            } else if (upBtn.contains(x, y)) {
                if (currentStep < maxUnlockedStep) {
                    currentStep++;
                    saveProgress();
                    resetCombat();
                } else {
                    showBubble(stageLabel() + "을 먼저 클리어해야 합니다.", 1200L);
                }
                invalidate();
            } else if (downBtn.contains(x, y)) {
                if (currentStep > 0) {
                    currentStep--;
                    saveProgress();
                    resetCombat();
                }
                invalidate();
            } else if (refightBtn.contains(x, y)) {
                resetCombat();
                invalidate();
            }
            return true;
        }

        if (mode == Mode.COMBAT) {
            processGesture(downX, downY, x, y, duration);
            return true;
        }

        if (mode == Mode.GACHA) {
            if (gachaReady) collectGacha();
            return true;
        }

        if (mode == Mode.BAG) {
            if (pointerBlock != null) {
                endBagPointer(x, y);
                return true;
            }

            if (tidyBtn.contains(x, y)) {
                tidyBag();
            } else if (closeBagBtn.contains(x, y)) {
                mode = activeBagOwner == 0 ? Mode.MAIN : Mode.JAIL;
                invalidate();
            }
            return true;
        }

        if (mode == Mode.FORGE) {
            if (forgeEnhanceTab.contains(x, y)) setForgeMode(ForgeMode.ENHANCE);
            else if (forgeSynthesisTab.contains(x, y)) setForgeMode(ForgeMode.SYNTHESIS);
            else if (forgeDismantleTab.contains(x, y)) setForgeMode(ForgeMode.DISMANTLE);
            else if (forgeClearBtn.contains(x, y)) clearForgeSelection();
            else if (forgeExecuteBtn.contains(x, y)) executeForge();
            else if (forgeCloseBtn.contains(x, y)) {
                mode = Mode.MAIN;
                invalidate();
            } else {
                for (RowHit hit : forgeHits) {
                    if (hit.rect.contains(x, y) && hit.block != null) {
                        forgeItemTap(hit.block);
                        break;
                    }
                }
            }
            return true;
        }

        if (mode == Mode.JAIL) {
            if (jailOpenBagBtn.contains(x, y) &&
                jailSelectedCharacter > 0 && isCaptured(jailSelectedCharacter)) {
                activeBagOwner = jailSelectedCharacter;
                selectedBlock = null;
                mode = Mode.BAG;
                invalidate();
            } else if (jailCloseBtn.contains(x, y)) {
                mode = Mode.MAIN;
                invalidate();
            } else {
                for (RowHit hit : jailHits) {
                    if (hit.rect.contains(x, y) && hit.intValue > 0 && isCaptured(hit.intValue)) {
                        jailSelectedCharacter = hit.intValue;
                        invalidate();
                        break;
                    }
                }
            }
            return true;
        }

        return true;
    }

    private int blockColor(GearBlock b) {
        if (b.exclusive) return 0xff9d3fc1;
        if (b.correction > 0) return 0xffd18b23;
        if (b.correction < 0) return 0xff50657a;
        return 0xffaaa58f;
    }

    private void drawSpeechBubble(Canvas c, String text, float left, float top, float right, float bottom, float scale) {
        float cx = (left + right) * .5f, cy = (top + bottom) * .5f;
        int save = c.save();
        c.scale(scale, scale, cx, cy);

        RectF bubble = new RectF(left, top, right, bottom);
        roundRect(c, bubble, 22f, 0xfff4f1ea);

        Path tail = new Path();
        tail.moveTo(left + 34f, bottom - 2f);
        tail.lineTo(left + 62f, bottom - 2f);
        tail.lineTo(left + 38f, bottom + 26f);
        tail.close();
        paint.setColor(0xfff4f1ea);
        c.drawPath(tail, paint);

        drawWrapped(c, text, left + 18f, top + 34f, right - left - 36f, 21f, 0xff201a1b, true);
        c.restoreToCount(save);
    }

    private void drawButton(Canvas c, RectF r, String label, int color, float size, boolean bold) {
        roundRect(c, r, 13f, color);
        strokeRect(c, r, 0xff665744, 2f);
        drawText(c, label, r.centerX(), r.centerY() + size * .34f, size, TXT, bold, Paint.Align.CENTER);
    }

    private void drawBitmapCover(Canvas c, Bitmap bm, RectF box) {
        if (bm == null || bm.getWidth() <= 0 || bm.getHeight() <= 0) return;
        RectF dst = coverRect(bm, box);
        int save = c.save();
        c.clipRect(box);
        c.drawBitmap(bm, null, dst, paint);
        c.restoreToCount(save);
    }

    private RectF coverRect(Bitmap bm, RectF box) {
        float sx = box.width() / bm.getWidth();
        float sy = box.height() / bm.getHeight();
        float scale = Math.max(sx, sy);
        float ww = bm.getWidth() * scale, hh = bm.getHeight() * scale;

        return new RectF(
            box.centerX() - ww / 2f, box.centerY() - hh / 2f,
            box.centerX() + ww / 2f, box.centerY() + hh / 2f
        );
    }

    private void drawWrapped(Canvas c, String text, float x, float y, float width, float size, int color, boolean bold) {
        paint.setTypeface(Typeface.create("sans-serif", bold ? Typeface.BOLD : Typeface.NORMAL));
        paint.setTextSize(size);
        paint.setColor(color);
        paint.setStyle(Paint.Style.FILL);
        paint.setTextAlign(Paint.Align.LEFT);

        String rest = text == null ? "" : text;
        float yy = y;

        while (rest.length() > 0) {
            int count = paint.breakText(rest, true, width, null);
            if (count <= 0) break;
            c.drawText(rest.substring(0, count), x, yy, paint);
            rest = rest.substring(count);
            yy += size * 1.42f;
        }
    }

    private void drawText(Canvas c, String s, float x, float y, float size, int color, boolean bold) {
        drawText(c, s, x, y, size, color, bold, Paint.Align.LEFT);
    }

    private void drawText(Canvas c, String s, float x, float y, float size, int color, boolean bold, Paint.Align align) {
        paint.setStyle(Paint.Style.FILL);
        paint.setTypeface(Typeface.create("sans-serif", bold ? Typeface.BOLD : Typeface.NORMAL));
        paint.setTextSize(size);
        paint.setColor(color);
        paint.setTextAlign(align);
        c.drawText(s, x, y, paint);
        paint.setTextAlign(Paint.Align.LEFT);
    }

    private void roundRect(Canvas c, RectF r, float radius, int color) {
        paint.setStyle(Paint.Style.FILL);
        paint.setColor(color);
        c.drawRoundRect(r, radius, radius, paint);
    }

    private void strokeRect(Canvas c, RectF r, int color, float width) {
        paint.setStyle(Paint.Style.STROKE);
        paint.setStrokeWidth(width);
        paint.setColor(color);
        c.drawRoundRect(r, 12f, 12f, paint);
        paint.setStyle(Paint.Style.FILL);
    }

    private RectF inset(RectF r, float amount) {
        return new RectF(r.left + amount, r.top + amount, r.right - amount, r.bottom - amount);
    }

    private int withAlpha(int rgb, float alpha) {
        int a = Math.max(0, Math.min(255, Math.round(alpha * 255f)));
        return (rgb & 0x00ffffff) | (a << 24);
    }

    private float clamp01(float v) { return Math.max(0f, Math.min(1f, v)); }

    private float lerp(float a, float b, float t) {
        return a + (b - a) * clamp01(t);
    }

    private static final class Cell {
        int x, y;
        Cell(int x, int y) { this.x = x; this.y = y; }
    }

    private static final class GearBlock {
        String id;
        String name;
        int cellCount;
        int atk;
        int def;
        int hp;
        int correction;
        boolean exclusive;
        int characterId;
        String shape;
        int x;
        int y;
        int rotation;
        int location;
        int bagOwner;
        int enhance;
    }

    private static final class GachaOutcome {
        int correction;
        boolean captureAttempt;
        boolean captureSucceeded;
        GearBlock block;
    }

    private static final class RowHit {
        RectF rect;
        GearBlock block;
        int intValue;

        RowHit(RectF rect, GearBlock block) {
            this.rect = new RectF(rect);
            this.block = block;
        }

        RowHit(RectF rect, int value) {
            this.rect = new RectF(rect);
            this.intValue = value;
        }
    }
}
