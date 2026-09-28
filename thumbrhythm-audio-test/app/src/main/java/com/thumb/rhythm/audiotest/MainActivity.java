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
import java.io.FileOutputStream;
import java.io.InputStream;

public class MainActivity extends Activity {
    private TextView status;
    private TextView gainText;
    private Button hitButton;
    private float hitGain = 1.0f;
    private int mode = 2; // 0=stick, 1=hat, 2=mix

    @Override protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        NativeAudio.nativeStart();

        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setGravity(Gravity.CENTER_HORIZONTAL);
        root.setPadding(dp(20), dp(34), dp(20), dp(28));
        root.setBackgroundColor(Color.rgb(7, 6, 12));

        TextView title = text("THUMB RHYTHM\nDRUM HIT TEST", 24, Color.WHITE);
        title.setGravity(Gravity.CENTER);
        root.addView(title, matchWrap());

        TextView desc = text(
            "Google Oboe 네이티브 출력\nCC0 BushDrum 원샷 샘플",
            14, Color.rgb(190, 190, 200));
        desc.setGravity(Gravity.CENTER);
        LinearLayout.LayoutParams descLp = matchWrap();
        descLp.setMargins(0, dp(12), 0, dp(18));
        root.addView(desc, descLp);

        LinearLayout modes = new LinearLayout(this);
        modes.setOrientation(LinearLayout.HORIZONTAL);
        modes.setGravity(Gravity.CENTER);

        Button stick = smallButton("스틱");
        Button hat = smallButton("하이햇");
        Button mix = smallButton("착 MIX");
        modes.addView(stick, equalButton());
        modes.addView(hat, equalButton());
        modes.addView(mix, equalButton());
        root.addView(modes, new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.MATCH_PARENT,
            LinearLayout.LayoutParams.WRAP_CONTENT));

        stick.setOnClickListener(v -> { mode = 0; status.setText("스틱 단독"); });
        hat.setOnClickListener(v -> { mode = 1; status.setText("클로즈드 하이햇 단독"); });
        mix.setOnClickListener(v -> { mode = 2; status.setText("착 MIX · 스틱 + 하이햇"); });

        gainText = text("타격 강도 100%", 14, Color.rgb(255, 211, 107));
        LinearLayout.LayoutParams gainLp = matchWrap();
        gainLp.setMargins(0, dp(22), 0, dp(4));
        root.addView(gainText, gainLp);

        SeekBar gain = new SeekBar(this);
        gain.setMax(160);
        gain.setProgress(60); // 40 + 60 = 100%
        root.addView(gain, new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.MATCH_PARENT,
            LinearLayout.LayoutParams.WRAP_CONTENT));
        gain.setOnSeekBarChangeListener(new SeekBar.OnSeekBarChangeListener() {
            @Override public void onProgressChanged(SeekBar seekBar, int progress, boolean fromUser) {
                int percent = 40 + progress;
                hitGain = percent / 100.0f;
                gainText.setText("타격 강도 " + percent + "%");
            }
            @Override public void onStartTrackingTouch(SeekBar seekBar) {}
            @Override public void onStopTrackingTouch(SeekBar seekBar) {}
        });

        hitButton = button("HIT");
        hitButton.setTextSize(36);
        hitButton.setEnabled(false);
        LinearLayout.LayoutParams hitLp = new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.MATCH_PARENT, dp(210));
        hitLp.setMargins(0, dp(18), 0, 0);
        root.addView(hitButton, hitLp);
        hitButton.setOnTouchListener((v, event) -> {
            if (event.getActionMasked() == MotionEvent.ACTION_DOWN) {
                NativeAudio.nativeDrumHit(mode, hitGain);
                v.setPressed(true);
                return true;
            }
            if (event.getActionMasked() == MotionEvent.ACTION_UP ||
                event.getActionMasked() == MotionEvent.ACTION_CANCEL) {
                v.setPressed(false);
                return true;
            }
            return true;
        });

        status = text("드럼 샘플 준비 중...", 13, Color.rgb(185, 185, 195));
        status.setGravity(Gravity.CENTER);
        LinearLayout.LayoutParams statusLp = matchWrap();
        statusLp.setMargins(0, dp(16), 0, 0);
        root.addView(status, statusLp);

        TextView credit = text(
            "Samples: BushDrum · CC0 1.0\nstick-h.wav + hihat-closed-short.wav",
            11, Color.rgb(120, 120, 135));
        credit.setGravity(Gravity.CENTER);
        LinearLayout.LayoutParams creditLp = matchWrap();
        creditLp.setMargins(0, dp(16), 0, 0);
        root.addView(credit, creditLp);

        setContentView(root);
        loadDrumAssets();
    }

    private void loadDrumAssets() {
        new Thread(() -> {
            try {
                File stick = copyAsset("stick_h.wav");
                File hat = copyAsset("hihat_closed_short.wav");
                boolean ok = NativeAudio.nativeLoadDrums(
                    stick.getAbsolutePath(), hat.getAbsolutePath());
                runOnUiThread(() -> {
                    hitButton.setEnabled(ok);
                    status.setText(ok
                        ? "준비 완료 · 착 MIX부터 눌러보세요."
                        : "네이티브 드럼 로딩 실패");
                });
            } catch (Throwable e) {
                runOnUiThread(() ->
                    status.setText("샘플 로딩 실패: " + e.getClass().getSimpleName()));
            }
        }).start();
    }

    private File copyAsset(String name) throws Exception {
        File dst = new File(getCacheDir(), name);
        try (InputStream in = getAssets().open(name);
             FileOutputStream out = new FileOutputStream(dst)) {
            byte[] buf = new byte[8192];
            int n;
            while ((n = in.read(buf)) > 0) out.write(buf, 0, n);
        }
        return dst;
    }

    @Override protected void onResume() {
        super.onResume();
        NativeAudio.nativeStart();
    }

    @Override protected void onDestroy() {
        NativeAudio.nativeStop();
        super.onDestroy();
    }

    private Button button(String label) {
        Button b = new Button(this);
        b.setText(label);
        b.setTextSize(17);
        b.setAllCaps(false);
        b.setTextColor(Color.WHITE);
        b.setBackgroundColor(Color.rgb(55, 40, 72));
        return b;
    }

    private Button smallButton(String label) {
        Button b = button(label);
        b.setTextSize(14);
        return b;
    }

    private TextView text(String value, int sp, int color) {
        TextView t = new TextView(this);
        t.setText(value);
        t.setTextSize(sp);
        t.setTextColor(color);
        return t;
    }

    private LinearLayout.LayoutParams equalButton() {
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
            0, dp(54), 1f);
        lp.setMargins(dp(3), 0, dp(3), 0);
        return lp;
    }

    private LinearLayout.LayoutParams matchWrap() {
        return new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.MATCH_PARENT,
            LinearLayout.LayoutParams.WRAP_CONTENT);
    }

    private int dp(int v) {
        return Math.round(v * getResources().getDisplayMetrics().density);
    }
}
