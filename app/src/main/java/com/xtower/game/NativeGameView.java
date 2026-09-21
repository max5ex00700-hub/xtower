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
import java.util.List;
import java.util.Locale;
import java.util.Random;
import java.util.UUID;

public final class NativeGameView extends View {
    private static final int ENEMY_MAX_HP = 60;
    private static final int GRID_W = 8;
    private static final int GRID_H = 3;

    private static final int BG = 0xff080708;
    private static final int PANEL = 0xff151216;
    private static final int TXT = 0xfff3eee6;
    private static final int MUTED = 0xffbcb2a7;
    private static final int GOLD = 0xffd4a648;
    private static final int RED = 0xff7a171c;

    private enum Mode { MAIN, COMBAT, GACHA, BAG }

    private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG | Paint.FILTER_BITMAP_FLAG);
    private final Random rng = new Random();
    private final Handler handler = new Handler(Looper.getMainLooper());
    private final SharedPreferences prefs;

    private Mode mode = Mode.MAIN;
    private long lastFrameMs;

    private final Bitmap[] stageBitmaps = new Bitmap[5];
    private final HashMap<String, Bitmap[]> actionBitmaps = new HashMap<>();
    private Bitmap captureBitmap;
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

    private int enemyHp = ENEMY_MAX_HP;
    private int hitCount;
    private boolean won;
    private long inputLockUntil;

    private float downX, downY;
    private long downTime;

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

    private final ArrayList<GearBlock> bag = new ArrayList<>();
    private GearBlock selectedBlock;
    private GearBlock draggingBlock;
    private int dragOffsetX, dragOffsetY;
    private int dragPreviewX = -99, dragPreviewY = -99;
    private int dragOriginalX, dragOriginalY;
    private final RectF bagGridRect = new RectF();
    private final RectF rotateBtn = new RectF();
    private final RectF tidyBtn = new RectF();
    private final RectF closeBagBtn = new RectF();

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
        loadImages();
        loadAudio();
        loadBag();
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

    private void loadImages() {
        String[] stages = {"f1_p00", "f1_p02", "f1_p04", "f1_p06", "f1_p08"};
        String[] fallbacks = {"s0", "s1", "s2", "s3", "s4"};
        for (int i = 0; i < 5; i++) {
            stageBitmaps[i] = bitmapByName(stages[i]);
            if (stageBitmaps[i] == null) stageBitmaps[i] = bitmapByName(fallbacks[i]);
        }

        captureBitmap = bitmapByName("f1_cap");
        if (captureBitmap == null) captureBitmap = stageBitmaps[4];

        String[] groups = {"p", "k", "b", "d"};
        for (String group : groups) {
            Bitmap[] arr = new Bitmap[10];
            for (int i = 0; i < 10; i++) {
                arr[i] = bitmapByName("f1_" + group + String.format(Locale.US, "%02d", i));
            }
            actionBitmaps.put(group, arr);
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

        String[] sfxNames = {
            "fight_punch_light", "fight_punch_medium", "fight_smash_heavy",
            "fight_swing_light", "fight_swing_heavy",
            "hit", "hurt", "win", "sparkle"
        };
        for (String name : sfxNames) loadSound(name, sfx);

        String[] voiceNames = {
            "female_grunt1", "female_grunt2", "female_gasp1", "female_gasp2",
            "female_agony1", "female_agony2", "female_scream1", "female_whimper1",
            "female_moan1", "female_moan2", "female_moan3", "female_moan4",
            "female_exhale1", "female_breath1", "female_sigh1", "female_sigh2",
            "female_ooh1"
        };
        for (String name : voiceNames) loadSound(name, voices);
    }

    private void loadSound(String name, HashMap<String, Integer> target) {
        int id = getResources().getIdentifier(name, "raw", getContext().getPackageName());
        if (id != 0) target.put(name, soundPool.load(getContext(), id, 1));
    }

    private void playSound(HashMap<String, Integer> source, String name, float volume) {
        if (soundPool == null) return;
        Integer id = source.get(name);
        if (id != null && id != 0) soundPool.play(id, volume, volume, 1, 0, 1f);
    }

    private void playSfx(String name, float volume) {
        playSound(sfx, name, volume);
    }

    private void playVoice(String name, float volume) {
        playSound(voices, name, volume);
    }

    private void playRandomVoice(String[] pool, float volume) {
        if (pool.length == 0) return;
        playVoice(pool[rng.nextInt(pool.length)], volume);
    }

    @Override protected void onDraw(Canvas canvas) {
        super.onDraw(canvas);

        long now = System.currentTimeMillis();
        float dt = Math.max(0f, Math.min(.05f, (now - lastFrameMs) / 1000f));
        lastFrameMs = now;

        updateWeakPoint(now, dt);

        switch (mode) {
            case MAIN:
                drawMain(canvas, now);
                break;
            case COMBAT:
                drawCombat(canvas, now);
                break;
            case GACHA:
                drawGacha(canvas, now);
                break;
            case BAG:
                drawBag(canvas, now);
                break;
        }

        if (needsAnimation(now)) postInvalidateOnAnimation();
    }

    private boolean needsAnimation(long now) {
        return mode == Mode.GACHA && !gachaReady
            || now < bubbleUntil
            || now < hitFxUntil
            || now < jellyUntil
            || now < actionUntil
            || weakActive;
    }

    private void drawMain(Canvas c, long now) {
        int w = getWidth();
        int h = getHeight();
        c.drawColor(BG);

        RectF art = new RectF(0, 0, w, h);
        drawBitmapCover(c, stageBitmaps[0], art);

        paint.setColor(0x66000000);
        c.drawRect(0, 0, w, h * .30f, paint);
        paint.setColor(0xaa000000);
        c.drawRect(0, h * .72f, w, h, paint);

        drawText(c, "X탑", 24f, 62f, 46f, 0xfff1e5d2, true);
        drawText(c, "NATIVE ENGINE TEST", w - 24f, 34f, 13f, 0xffd9b65d, false, Paint.Align.RIGHT);
        drawText(c, "FLOOR", 24f, 105f, 15f, 0xffbdb0a0, false);
        drawText(c, "1", 24f, 148f, 46f, 0xffffffff, true);

        drawSpeechBubble(c, "...또 오는 거야?", w * .56f, 88f, w - 18f, 174f, 1f);

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
        float gap = 6f;
        float left = 8f;
        float bw = (w - left * 2f - gap * 5f) / 6f;
        for (int i = 0; i < btns.length; i++) {
            float x = left + i * (bw + gap);
            btns[i].set(x, h - navH + 12f, x + bw, h - 10f);
            int col = i == 3 || i == 0 ? 0xff312a28 : 0xff242122;
            drawButton(c, btns[i], labels[i], col, 14f, false);
            if (i != 0 && i != 3) {
                paint.setColor(0x66000000);
                c.drawRoundRect(btns[i], 12f, 12f, paint);
            }
        }

        String cap = isCaptured(1) ? "1층 포획됨" : "1층 미포획";
        drawText(c, cap, w - 20f, h - navH - 18f, 13f,
            isCaptured(1) ? 0xffc990e8 : 0xffbeb4aa, true, Paint.Align.RIGHT);
    }

    private void drawCombat(Canvas c, long now) {
        int w = getWidth();
        int h = getHeight();
        c.drawColor(Color.BLACK);

        Bitmap current;
        if (won && captureBitmap != null) current = captureBitmap;
        else if (actionBitmap != null && now < actionUntil) current = actionBitmap;
        else current = stageBitmaps[combatStage()];

        if (current != null) {
            if (now < jellyUntil) drawBitmapJelly(c, current, now);
            else drawBitmapCover(c, current, new RectF(0, 0, w, h));
        }

        paint.setColor(0x66000000);
        c.drawRect(0, 0, w, Math.max(64f, h * .07f), paint);
        drawText(c, "HP " + enemyHp + " / " + ENEMY_MAX_HP, 18f, 36f, 16f, 0xffffffff, true);

        float hpPct = enemyHp / (float) ENEMY_MAX_HP;
        RectF bar = new RectF(18f, 46f, w - 18f, 56f);
        roundRect(c, bar, 6f, 0xff2b2425);
        RectF fill = new RectF(bar.left, bar.top, bar.left + bar.width() * hpPct, bar.bottom);
        roundRect(c, fill, 6f, 0xffa01e25);

        if (weakActive) drawWeakPoint(c, now);
        if (now < hitFxUntil) drawHitFx(c, now);
        if (now < bubbleUntil) {
            float progress = Math.min(1f, (now - bubbleStart) / 110f);
            float pop = progress < .72f
                ? lerp(.86f, 1.045f, progress / .72f)
                : lerp(1.045f, 1f, (progress - .72f) / .28f);
            float bw = Math.min(w * .55f, Math.max(250f, 185f + bubbleText.length() * 18f));
            float right = w - 18f;
            float top = Math.max(88f, h * .12f);
            drawSpeechBubble(c, bubbleText, right - bw, top, right, top + 116f, pop);
        }
    }

    private void drawBitmapJelly(Canvas c, Bitmap bm, long now) {
        if (bm == null) return;

        int w = getWidth();
        int h = getHeight();
        RectF dst = coverRect(bm, new RectF(0, 0, w, h));

        float duration = Math.max(1f, jellyUntil - jellyStart);
        float t = clamp01((now - jellyStart) / duration);
        float decay = 1f - t;
        float wave = (float) Math.cos(t * Math.PI * 5.0);
        float amp = (jellyHeavy ? 34f : (jellySwipe ? 25f : 19f)) * decay * wave;
        float radius = Math.max(150f, Math.min(w, h) * (jellyHeavy ? .34f : .28f));

        float swipeLen = (float) Math.hypot(jellySwipeX, jellySwipeY);
        float sx = swipeLen > .01f ? jellySwipeX / swipeLen : 0f;
        float sy = swipeLen > .01f ? jellySwipeY / swipeLen : 0f;

        int k = 0;
        for (int yy = 0; yy <= meshH; yy++) {
            float v = yy / (float) meshH;
            float baseY = dst.top + v * dst.height();
            for (int xx = 0; xx <= meshW; xx++) {
                float u = xx / (float) meshW;
                float baseX = dst.left + u * dst.width();

                float dx = baseX - jellyX;
                float dy = baseY - jellyY;
                float dist = (float) Math.hypot(dx, dy);
                float falloff = clamp01(1f - dist / radius);
                falloff = falloff * falloff;

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
        c.clipRect(0, 0, w, h);
        c.drawBitmapMesh(bm, meshW, meshH, meshVerts, 0, null, 0, paint);
        c.restoreToCount(save);
    }

    private void drawWeakPoint(Canvas c, long now) {
        float px = weakX * getWidth();
        float py = weakY * getHeight();
        float pulse = .55f + .75f * ((float) Math.sin(now * .0085f) + 1f) * .5f;
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
        int w = getWidth();
        int h = getHeight();
        c.drawColor(0xff07080b);

        RectF machine = new RectF(w * .08f, h * .07f, w * .92f, h * .93f);
        roundRect(c, machine, 26f, 0xff131820);
        strokeRect(c, machine, 0xffd2a236, 5f);

        drawText(c, "X-TOWER REWARD", w / 2f, machine.top + 52f, 26f, 0xffffd65f, true, Paint.Align.CENTER);

        float cx = w / 2f;
        float cy = h * .37f;
        float wheelR = Math.min(w * .34f, h * .18f);
        float rotation;

        if (!gachaReady) {
            float p = clamp01((now - gachaStart) / (float) gachaDuration);
            float e = 1f - (float) Math.pow(1f - p, 4);
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

        RectF chute = new RectF(w * .28f, h * .56f, w * .72f, h * .65f);
        roundRect(c, chute, 12f, 0xff0a0c10);
        strokeRect(c, chute, 0xff6d7178, 3f);

        if (!gachaReady) {
            drawText(c, "보정 룰렛 회전 중", w / 2f, h * .72f, 20f, 0xffb8bbc3, true, Paint.Align.CENTER);
            return;
        }

        String corr = gachaCorrection > 0 ? "+" + gachaCorrection + "%" : gachaCorrection + "%";
        drawText(c, "보정  " + corr, w / 2f, h * .69f, 25f, 0xffffcf50, true, Paint.Align.CENTER);

        if (pendingOutcome != null && pendingOutcome.captureAttempt) {
            drawCaptureBall(c, cx, h * .60f, pendingOutcome.captureSucceeded);
            drawText(c, pendingOutcome.captureSucceeded ? "포획 성공!" : "포획 실패",
                w / 2f, h * .78f, 27f,
                pendingOutcome.captureSucceeded ? 0xfff1c95d : 0xffd7d7d7,
                true, Paint.Align.CENTER);
            drawText(c, pendingOutcome.captureSucceeded ? "1층 캐릭터 포획 상태 저장" : "포획 확률 1%",
                w / 2f, h * .82f, 16f, MUTED, false, Paint.Align.CENTER);
        } else if (pendingOutcome != null && pendingOutcome.block != null) {
            GearBlock b = pendingOutcome.block;
            drawRewardShape(c, b, cx, h * .60f, Math.min(34f, w * .07f));
            drawText(c, b.name + " · " + b.cellCount + "칸",
                w / 2f, h * .77f, 23f, TXT, true, Paint.Align.CENTER);
            drawText(c, "공 +" + b.atk + "   방 +" + b.def + "   체 +" + b.hp,
                w / 2f, h * .815f, 18f, 0xffffce55, true, Paint.Align.CENTER);
        }

        drawText(c, "화면을 터치해서 계속", w / 2f, h * .89f, 16f, MUTED, false, Paint.Align.CENTER);
    }

    private void drawRouletteWheel(Canvas c, float cx, float cy, float radius, float rotationDeg) {
        paint.setStyle(Paint.Style.STROKE);
        paint.setStrokeWidth(8f);
        paint.setColor(0xff464b56);
        c.drawCircle(cx, cy, radius, paint);
        paint.setStyle(Paint.Style.FILL);

        float seg = 360f / corrections.length;
        for (int i = 0; i < corrections.length; i++) {
            float a = (float) Math.toRadians(i * seg + rotationDeg - 90f);
            float x = cx + (float) Math.cos(a) * radius * .77f;
            float y = cy + (float) Math.sin(a) * radius * .77f;

            int corr = corrections[i];
            int color = corr == 0 ? 0xff8a348f : (corr > 0 ? 0xffb9791e : 0xff34485f);
            paint.setColor(color);
            c.drawCircle(x, y, radius * .12f, paint);

            String label = corr > 0 ? "+" + corr : String.valueOf(corr);
            drawText(c, label, x, y + 5f, Math.max(11f, radius * .07f), Color.WHITE, true, Paint.Align.CENTER);
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
            RectF inner = new RectF(r.left + cell * .16f, r.top + cell * .16f,
                r.right - cell * .16f, r.bottom - cell * .16f);
            roundRect(c, inner, 3f, 0xdd15171b);
        }
    }

    private void drawBag(Canvas c, long now) {
        int w = getWidth();
        int h = getHeight();
        c.drawColor(0xff080708);

        RectF panel = new RectF(12f, 20f, w - 12f, h - 20f);
        roundRect(c, panel, 22f, 0xff151315);
        strokeRect(c, panel, 0xff6f5c42, 3f);

        drawText(c, "배 낭   8 × 3", w / 2f, 70f, 30f, 0xfff2e5cf, true, Paint.Align.CENTER);

        int totalAtk = 0, totalDef = 0, totalHp = 0;
        for (GearBlock b : bag) {
            totalAtk += b.atk;
            totalDef += b.def;
            totalHp += b.hp;
        }
        drawText(c, "블록 " + bag.size() + "개", 30f, 108f, 16f, MUTED, false);
        drawText(c, "공 +" + totalAtk + "  방 +" + totalDef + "  체 +" + totalHp,
            w - 30f, 108f, 16f, 0xffffce56, true, Paint.Align.RIGHT);

        float cell = (w - 48f) / GRID_W;
        float gridTop = 145f;
        bagGridRect.set(24f, gridTop, 24f + cell * GRID_W, gridTop + cell * GRID_H);

        roundRect(c, bagGridRect, 8f, 0xff0c0d0f);
        strokeRect(c, bagGridRect, 0xff5d5140, 2f);

        for (int y = 0; y < GRID_H; y++) {
            for (int x = 0; x < GRID_W; x++) {
                RectF r = cellRect(x, y, cell);
                int col = 0xff1a1a1f;
                if (draggingBlock != null && isPreviewCell(draggingBlock, x, y)) {
                    boolean valid = canPlace(draggingBlock, dragPreviewX, dragPreviewY, draggingBlock.rotation, draggingBlock.id);
                    col = valid ? 0xff275b34 : 0xff702929;
                }
                roundRect(c, inset(r, 2f), 4f, col);
            }
        }

        for (GearBlock b : bag) drawBagBlock(c, b, cell);

        float detailTop = bagGridRect.bottom + 36f;
        if (selectedBlock == null) {
            drawText(c, "블록을 눌러 선택하거나 드래그해서 옮기세요.",
                w / 2f, detailTop, 17f, 0xffd7cec0, false, Paint.Align.CENTER);
        } else {
            String corr = selectedBlock.correction > 0
                ? "+" + selectedBlock.correction + "%"
                : selectedBlock.correction + "%";
            drawText(c, selectedBlock.name + "  [" + selectedBlock.cellCount + "칸 / " + corr + "]",
                w / 2f, detailTop, 18f, TXT, true, Paint.Align.CENTER);
            drawText(c, "공 +" + selectedBlock.atk + "   방 +" + selectedBlock.def + "   체 +" + selectedBlock.hp,
                w / 2f, detailTop + 32f, 17f, 0xffffcf5a, true, Paint.Align.CENTER);
        }

        float buttonTop = h - 205f;
        rotateBtn.set(34f, buttonTop, w * .48f, buttonTop + 60f);
        tidyBtn.set(w * .52f, buttonTop, w - 34f, buttonTop + 60f);
        closeBagBtn.set(w * .28f, h - 112f, w * .72f, h - 52f);

        drawButton(c, rotateBtn, "↻ 90° 회전", selectedBlock == null ? 0xff272526 : 0xff44392e, 18f, true);
        drawButton(c, tidyBtn, "자동 정리", 0xff44392e, 18f, true);
        drawButton(c, closeBagBtn, "닫기", 0xff512027, 18f, true);
    }

    private RectF cellRect(int x, int y, float cell) {
        return new RectF(
            bagGridRect.left + x * cell,
            bagGridRect.top + y * cell,
            bagGridRect.left + (x + 1) * cell,
            bagGridRect.top + (y + 1) * cell
        );
    }

    private void drawBagBlock(Canvas c, GearBlock b, float cell) {
        int ox = b.x;
        int oy = b.y;
        if (draggingBlock == b && dragPreviewX > -50) {
            ox = dragPreviewX;
            oy = dragPreviewY;
        }

        List<Cell> cells = rotatedCells(b, b.rotation);
        int col = blockColor(b);
        for (Cell q : cells) {
            int gx = ox + q.x;
            int gy = oy + q.y;
            if (gx < 0 || gx >= GRID_W || gy < 0 || gy >= GRID_H) continue;

            RectF r = inset(cellRect(gx, gy, cell), 4f);
            roundRect(c, r, 5f, col);

            RectF inner = new RectF(
                r.left + cell * .15f, r.top + cell * .15f,
                r.right - cell * .15f, r.bottom - cell * .15f
            );
            roundRect(c, inner, 3f, 0xdd15171b);
        }

        if (selectedBlock == b && draggingBlock == null) {
            List<Cell> cells2 = rotatedCells(b, b.rotation);
            for (Cell q : cells2) {
                RectF r = inset(cellRect(b.x + q.x, b.y + q.y, cell), 2f);
                strokeRect(c, r, 0xffffe38a, 3f);
            }
        }
    }

    private boolean isPreviewCell(GearBlock b, int x, int y) {
        if (dragPreviewX < -50) return false;
        for (Cell q : rotatedCells(b, b.rotation)) {
            if (dragPreviewX + q.x == x && dragPreviewY + q.y == y) return true;
        }
        return false;
    }

    private int blockColor(GearBlock b) {
        if (b.exclusive) return 0xff9d3fc1;
        if (b.correction > 0) return 0xffd18b23;
        if (b.correction < 0) return 0xff50657a;
        return 0xffaaa58f;
    }

    private void drawSpeechBubble(Canvas c, String text, float left, float top, float right, float bottom, float scale) {
        float cx = (left + right) * .5f;
        float cy = (top + bottom) * .5f;

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

    private void resetCombat() {
        enemyHp = ENEMY_MAX_HP;
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
        float q = enemyHp / (float) ENEMY_MAX_HP;
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

        float dx = ex - sx;
        float dy = ey - sy;
        float dist = (float) Math.hypot(dx, dy);
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

        float weakPx = weakX * getWidth();
        float weakPy = weakY * getHeight();
        boolean weakHit = weakActive
            && Math.hypot(weakPx - impactX, weakPy - impactY) <= Math.max(58f, getWidth() * .06f);

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

        int damage = weakHit ? 18 : (swipe ? 8 : 5);
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
            if (enemyHp <= Math.round(ENEMY_MAX_HP * .25f) && rng.nextFloat() < .45f)
                playRandomVoice(lowHpVoices, .72f);
            else
                playRandomVoice(swipe ? swipeHitVoices : normalHitVoices, swipe ? .78f : .66f);
        }

        if (enemyHp <= 0) {
            won = true;
            weakActive = false;
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
        Bitmap[] arr = actionBitmaps.get(prefix);
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

        if (swipe)
            playSfx("k".equals(prefix) ? "fight_swing_heavy" : "fight_swing_light", .66f);

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

        float angle = rng.nextFloat() * (float) Math.PI * 2f;
        weakVx = (float) Math.cos(angle) * .34f;
        weakVy = (float) Math.sin(angle) * .34f;
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

    private String randomLine(String[] lines) {
        return lines[rng.nextInt(lines.length)];
    }

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

        if (gachaCorrection == 0 && !isCaptured(1)) {
            pendingOutcome.captureAttempt = true;
            pendingOutcome.captureSucceeded = rng.nextFloat() < .01f;
            if (pendingOutcome.captureSucceeded) {
                prefs.edit().putBoolean("captured_char_1", true).apply();
            }
        } else {
            boolean exclusive = gachaCorrection == 0 && isCaptured(1);
            pendingOutcome.block = rollBlock(gachaCorrection, exclusive);
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
        b.characterId = 1;
        b.name = exclusive
            ? "1층 전용 " + nouns[rng.nextInt(nouns.length)]
            : prefixes[rng.nextInt(prefixes.length)] + " " + nouns[rng.nextInt(nouns.length)];
        b.shape = encodeShape(shapeFor(cells));
        b.x = -1;
        b.y = -1;
        b.rotation = 0;
        return b;
    }

    private boolean isCaptured(int characterId) {
        return prefs.getBoolean("captured_char_" + characterId, false);
    }

    private void collectGacha() {
        if (!gachaReady || pendingOutcome == null) return;

        if (pendingOutcome.block != null) {
            if (!tryAddBlock(pendingOutcome.block)) {
                showBubble("가방 8×3에 들어갈 공간이 없습니다.", 1800L);
                return;
            }
        }

        pendingOutcome = null;
        resetCombat();
        mode = Mode.MAIN;
        invalidate();
    }

    private boolean tryAddBlock(GearBlock b) {
        b.x = -1;
        b.y = -1;
        if (!findFirstPlacement(b)) return false;
        bag.add(b);
        saveBag();
        return true;
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
            opts.add(cells(0,0,1,0,0,1,1,1,0,2));
            opts.add(cells(0,0,1,0,1,1,2,1,2,2));
            opts.add(cells(1,0,0,1,1,1,2,1,1,2));
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
        for (int i = 0; i + 1 < xy.length; i += 2) out.add(new Cell(xy[i], xy[i + 1]));
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
        String[] pts = encoded.split(";");
        for (String pt : pts) {
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
            int x = p.x;
            int y = p.y;
            for (int r = 0; r < rotation; r++) {
                int nx = -y;
                int ny = x;
                x = nx;
                y = ny;
            }
            out.add(new Cell(x, y));
        }

        int minX = 999;
        int minY = 999;
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
        boolean[][] occupied = new boolean[GRID_W][GRID_H];

        for (GearBlock other : bag) {
            if (other.id.equals(ignoreId) || other.x < 0 || other.y < 0) continue;
            for (Cell p : rotatedCells(other, other.rotation)) {
                int x = other.x + p.x;
                int y = other.y + p.y;
                if (x >= 0 && x < GRID_W && y >= 0 && y < GRID_H) occupied[x][y] = true;
            }
        }

        for (Cell p : rotatedCells(item, rotation)) {
            int x = ox + p.x;
            int y = oy + p.y;
            if (x < 0 || x >= GRID_W || y < 0 || y >= GRID_H) return false;
            if (occupied[x][y]) return false;
        }
        return true;
    }

    private boolean findFirstPlacement(GearBlock item) {
        int startRot = item.rotation;
        for (int dr = 0; dr < 4; dr++) {
            int rot = (startRot + dr) % 4;
            for (int y = 0; y < GRID_H; y++) {
                for (int x = 0; x < GRID_W; x++) {
                    if (canPlace(item, x, y, rot, item.id)) {
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

    private void rotateSelected() {
        if (selectedBlock == null) return;
        int next = (selectedBlock.rotation + 1) % 4;
        if (canPlace(selectedBlock, selectedBlock.x, selectedBlock.y, next, selectedBlock.id)) {
            selectedBlock.rotation = next;
            saveBag();
        } else {
            showBubble("그 자리에서는 회전할 공간이 부족합니다.", 1400L);
        }
        invalidate();
    }

    private void tidyBag() {
        if (bag.isEmpty()) return;

        int[] oldX = new int[bag.size()];
        int[] oldY = new int[bag.size()];
        int[] oldR = new int[bag.size()];
        for (int i = 0; i < bag.size(); i++) {
            oldX[i] = bag.get(i).x;
            oldY[i] = bag.get(i).y;
            oldR[i] = bag.get(i).rotation;
            bag.get(i).x = -1;
            bag.get(i).y = -1;
        }

        ArrayList<GearBlock> order = new ArrayList<>(bag);
        Collections.sort(order, new Comparator<GearBlock>() {
            @Override public int compare(GearBlock a, GearBlock b) {
                return Integer.compare(b.cellCount, a.cellCount);
            }
        });

        boolean ok = true;
        for (GearBlock b : order) {
            if (!findFirstPlacement(b)) {
                ok = false;
                break;
            }
        }

        if (!ok) {
            for (int i = 0; i < bag.size(); i++) {
                bag.get(i).x = oldX[i];
                bag.get(i).y = oldY[i];
                bag.get(i).rotation = oldR[i];
            }
            showBubble("현재 블록 조합은 8×3 안에 모두 배치할 수 없습니다.", 1600L);
        } else {
            saveBag();
        }
        invalidate();
    }

    private GearBlock findBagBlockAt(float px, float py) {
        if (!bagGridRect.contains(px, py)) return null;
        float cell = bagGridRect.width() / GRID_W;

        int gx = (int) ((px - bagGridRect.left) / cell);
        int gy = (int) ((py - bagGridRect.top) / cell);

        for (int i = bag.size() - 1; i >= 0; i--) {
            GearBlock b = bag.get(i);
            for (Cell q : rotatedCells(b, b.rotation)) {
                if (b.x + q.x == gx && b.y + q.y == gy) return b;
            }
        }
        return null;
    }

    private void beginBagDrag(float px, float py) {
        GearBlock b = findBagBlockAt(px, py);
        if (b == null) {
            selectedBlock = null;
            draggingBlock = null;
            return;
        }

        selectedBlock = b;
        draggingBlock = b;
        dragOriginalX = b.x;
        dragOriginalY = b.y;

        float cell = bagGridRect.width() / GRID_W;
        int gx = (int) ((px - bagGridRect.left) / cell);
        int gy = (int) ((py - bagGridRect.top) / cell);
        dragOffsetX = gx - b.x;
        dragOffsetY = gy - b.y;
        dragPreviewX = b.x;
        dragPreviewY = b.y;
    }

    private void moveBagDrag(float px, float py) {
        if (draggingBlock == null) return;
        float cell = bagGridRect.width() / GRID_W;
        int gx = (int) Math.floor((px - bagGridRect.left) / cell);
        int gy = (int) Math.floor((py - bagGridRect.top) / cell);
        dragPreviewX = gx - dragOffsetX;
        dragPreviewY = gy - dragOffsetY;
        invalidate();
    }

    private void endBagDrag() {
        if (draggingBlock == null) return;

        if (canPlace(draggingBlock, dragPreviewX, dragPreviewY, draggingBlock.rotation, draggingBlock.id)) {
            draggingBlock.x = dragPreviewX;
            draggingBlock.y = dragPreviewY;
            saveBag();
        } else {
            draggingBlock.x = dragOriginalX;
            draggingBlock.y = dragOriginalY;
        }

        draggingBlock = null;
        dragPreviewX = -99;
        dragPreviewY = -99;
        invalidate();
    }

    private void saveBag() {
        try {
            JSONArray arr = new JSONArray();
            for (GearBlock b : bag) {
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
                arr.put(o);
            }
            prefs.edit().putString("bag_v1", arr.toString()).apply();
        } catch (Exception ignored) {}
    }

    private void loadBag() {
        bag.clear();
        String json = prefs.getString("bag_v1", "");
        if (json == null || json.length() == 0) return;

        try {
            JSONArray arr = new JSONArray(json);
            for (int i = 0; i < arr.length(); i++) {
                JSONObject o = arr.getJSONObject(i);
                GearBlock b = new GearBlock();
                b.id = o.optString("id", UUID.randomUUID().toString());
                b.name = o.optString("name", "블록");
                b.cellCount = o.optInt("cellCount", 1);
                b.atk = o.optInt("atk", 1);
                b.def = o.optInt("def", 1);
                b.hp = o.optInt("hp", 1);
                b.correction = o.optInt("correction", 0);
                b.exclusive = o.optBoolean("exclusive", false);
                b.characterId = o.optInt("characterId", 1);
                b.shape = o.optString("shape", "0,0");
                b.x = o.optInt("x", -1);
                b.y = o.optInt("y", -1);
                b.rotation = o.optInt("rotation", 0);
                bag.add(b);
            }
        } catch (Exception ignored) {}
    }

    @Override public boolean onTouchEvent(MotionEvent e) {
        float x = e.getX();
        float y = e.getY();

        if (e.getActionMasked() == MotionEvent.ACTION_DOWN) {
            downX = x;
            downY = y;
            downTime = System.currentTimeMillis();

            if (mode == Mode.BAG) {
                if (bagGridRect.contains(x, y)) beginBagDrag(x, y);
            }
            return true;
        }

        if (e.getActionMasked() == MotionEvent.ACTION_MOVE) {
            if (mode == Mode.BAG && draggingBlock != null) moveBagDrag(x, y);
            return true;
        }

        if (e.getActionMasked() != MotionEvent.ACTION_UP && e.getActionMasked() != MotionEvent.ACTION_CANCEL)
            return true;

        long duration = System.currentTimeMillis() - downTime;

        if (mode == Mode.MAIN) {
            if (fightBtn.contains(x, y)) {
                beginCombat();
            } else if (bagBtn.contains(x, y)) {
                selectedBlock = null;
                draggingBlock = null;
                mode = Mode.BAG;
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
            if (draggingBlock != null) {
                endBagDrag();
                return true;
            }

            if (rotateBtn.contains(x, y)) {
                rotateSelected();
            } else if (tidyBtn.contains(x, y)) {
                tidyBag();
            } else if (closeBagBtn.contains(x, y)) {
                mode = Mode.MAIN;
                invalidate();
            } else {
                selectedBlock = findBagBlockAt(x, y);
                invalidate();
            }
            return true;
        }

        return true;
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
        float ww = bm.getWidth() * scale;
        float hh = bm.getHeight() * scale;
        return new RectF(
            box.centerX() - ww / 2f,
            box.centerY() - hh / 2f,
            box.centerX() + ww / 2f,
            box.centerY() + hh / 2f
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

    private float clamp01(float v) {
        return Math.max(0f, Math.min(1f, v));
    }

    private float lerp(float a, float b, float t) {
        return a + (b - a) * clamp01(t);
    }

    private static final class Cell {
        int x;
        int y;
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
    }

    private static final class GachaOutcome {
        int correction;
        boolean captureAttempt;
        boolean captureSucceeded;
        GearBlock block;
    }
}
