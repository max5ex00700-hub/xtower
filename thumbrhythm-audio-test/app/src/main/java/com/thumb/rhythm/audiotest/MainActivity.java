package com.thumb.rhythm.audiotest;

import android.app.Activity;
import android.graphics.Color;
import android.os.Bundle;
import android.view.Gravity;
import android.view.MotionEvent;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.SeekBar;
import android.widget.TextView;
import java.io.File;

public class MainActivity extends Activity {
    private TextView status;
    private TextView gainText;
    private float gain = 1.0f;

    @Override protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setGravity(Gravity.CENTER_HORIZONTAL);
        root.setPadding(dp(18), dp(34), dp(18), dp(28));
        root.setBackgroundColor(Color.rgb(8, 7, 13));

        TextView title = text("THUMB RHYTHM\n드럼 착착 샘플 테스트", 24, Color.WHITE);
        title.setGravity(Gravity.CENTER);
        root.addView(title, fullWrap());

        TextView sub = text(
            "CC0 BushDrum · LinnDrum LM-2 원샷\nGoogle Oboe 저지연 네이티브 재생",
            14, Color.rgb(188, 188, 200));
        sub.setGravity(Gravity.CENTER);
        LinearLayout.LayoutParams subLp = fullWrap();
        subLp.setMargins(0, dp(12), 0, dp(16));
        root.addView(sub, subLp);

        gainText = text("타격 강도 100%", 15, Color.rgb(255, 210, 105));
        root.addView(gainText, fullWrap());

        SeekBar seek = new SeekBar(this);
        seek.setMax(160);
        seek.setProgress(60); // 40% + 60% = 100%
        root.addView(seek, fullWrap());
        seek.setOnSeekBarChangeListener(new SeekBar.OnSeekBarChangeListener() {
            @Override public void onProgressChanged(SeekBar s, int p, boolean fromUser) {
                int pct = 40 + p;
                gain = pct / 100.0f;
                gainText.setText("타격 강도 " + pct + "%");
            }
            @Override public void onStartTrackingTouch(SeekBar s) {}
            @Override public void onStopTrackingTouch(SeekBar s) {}
        });

        Button stick = bigButton("착 1 · STICK");
        Button hat = bigButton("착 2 · CLOSED HAT");
        Button layer = bigButton("착 3 · STICK + HAT");

        root.addView(stick, bigLp());
        LinearLayout.LayoutParams hLp = bigLp();
        hLp.setMargins(0, dp(10), 0, 0);
        root.addView(hat, hLp);
        LinearLayout.LayoutParams lLp = bigLp();
        lLp.setMargins(0, dp(10), 0, 0);
        root.addView(layer, lLp);

        setPressHit(stick, 0);
        setPressHit(hat, 1);
        setPressHit(layer, 2);

        status = text("샘플 준비 중...", 13, Color.rgb(180, 180, 190));
        status.setGravity(Gravity.CENTER);
        LinearLayout.LayoutParams sLp = fullWrap();
        sLp.setMargins(0, dp(18), 0, 0);
        root.addView(status, sLp);

        setContentView(root);

        stick.setEnabled(false);
        hat.setEnabled(false);
        layer.setEnabled(false);

        new Thread(() -> {
            try {
                File stickFile = DrumSamples.write(getCacheDir(), "stick-h.wav", DrumSamples.STICK_B64);
                File hatFile = DrumSamples.write(getCacheDir(), "hihat-closed-short.wav", DrumSamples.HAT_B64);
                boolean a = NativeAudio.nativeLoadDrum(0, stickFile.getAbsolutePath());
                boolean b = NativeAudio.nativeLoadDrum(1, hatFile.getAbsolutePath());
                runOnUiThread(() -> {
                    boolean ok = a && b;
                    stick.setEnabled(ok);
                    hat.setEnabled(ok);
                    layer.setEnabled(ok);
                    status.setText(ok ? "준비 완료 · 세 소리를 직접 비교해 주세요." : "샘플 로딩 실패");
                });
            } catch (Throwable t) {
                runOnUiThread(() -> status.setText("오류: " + t.getClass().getSimpleName()));
            }
        }).start();
    }

    private void setPressHit(Button b, int mode) {
        b.setOnTouchListener((v, e) -> {
            if (e.getActionMasked() == MotionEvent.ACTION_DOWN) {
                NativeAudio.nativeDrumHit(mode, gain);
                v.setPressed(true);
                return true;
            }
            if (e.getActionMasked() == MotionEvent.ACTION_UP ||
                e.getActionMasked() == MotionEvent.ACTION_CANCEL) {
                v.setPressed(false);
                return true;
            }
            return true;
        });
    }

    private Button bigButton(String label) {
        Button b = new Button(this);
        b.setText(label);
        b.setTextSize(22);
        b.setAllCaps(false);
        b.setTextColor(Color.WHITE);
        b.setBackgroundColor(Color.rgb(63, 45, 81));
        return b;
    }

    private TextView text(String s, int sp, int color) {
        TextView t = new TextView(this);
        t.setText(s);
        t.setTextSize(sp);
        t.setTextColor(color);
        return t;
    }

    private LinearLayout.LayoutParams bigLp() {
        return new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.MATCH_PARENT, dp(115));
    }

    private LinearLayout.LayoutParams fullWrap() {
        return new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.MATCH_PARENT,
            LinearLayout.LayoutParams.WRAP_CONTENT);
    }

    private int dp(int v) {
        return Math.round(v * getResources().getDisplayMetrics().density);
    }

    @Override protected void onDestroy() {
        NativeAudio.nativeDrumStop();
        super.onDestroy();
    }
}
