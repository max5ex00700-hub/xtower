package com.thumb.rhythm.audiotest;

import android.app.Activity;
import android.content.Intent;
import android.graphics.Color;
import android.media.AudioAttributes;
import android.media.MediaPlayer;
import android.net.Uri;
import android.os.Bundle;
import android.provider.OpenableColumns;
import android.view.Gravity;
import android.view.MotionEvent;
import android.view.View;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.SeekBar;
import android.widget.TextView;
import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.util.Locale;

public class MainActivity extends Activity {
    private static final int PICK_AUDIO = 42;
    private MediaPlayer player;
    private TextView status;
    private TextView gainText;
    private Button playButton;
    private Button hitButton;
    private float hitGain = 1.0f;
    private File currentFile;

    @Override protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        NativeAudio.nativeStart();

        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setGravity(Gravity.CENTER_HORIZONTAL);
        root.setPadding(dp(20), dp(36), dp(20), dp(30));
        root.setBackgroundColor(Color.rgb(7, 6, 12));

        TextView title = text("THUMB RHYTHM\nNATIVE AUDIO TEST", 24, Color.WHITE);
        title.setGravity(Gravity.CENTER);
        root.addView(title, matchWrap());

        TextView desc = text(
            "Google Oboe + miniaudio\n원곡에서 실제 타격 조각을 뽑아 저지연으로 재생합니다.",
            14, Color.rgb(190, 190, 200));
        desc.setGravity(Gravity.CENTER);
        LinearLayout.LayoutParams descLp = matchWrap();
        descLp.setMargins(0, dp(14), 0, dp(20));
        root.addView(desc, descLp);

        Button pick = button("1. 음악 선택");
        root.addView(pick, wideButton());
        pick.setOnClickListener(v -> pickAudio());

        playButton = button("2. 재생");
        playButton.setEnabled(false);
        LinearLayout.LayoutParams playLp = wideButton();
        playLp.setMargins(0, dp(10), 0, 0);
        root.addView(playButton, playLp);
        playButton.setOnClickListener(v -> togglePlayback());

        gainText = text("타격 강도 100%", 14, Color.rgb(255, 211, 107));
        LinearLayout.LayoutParams gainLp = matchWrap();
        gainLp.setMargins(0, dp(22), 0, dp(4));
        root.addView(gainText, gainLp);

        SeekBar gain = new SeekBar(this);
        gain.setMax(140);
        gain.setProgress(60); // 40% + 60% = 100%
        root.addView(gain, new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT));
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
        hitButton.setTextSize(34);
        hitButton.setEnabled(false);
        LinearLayout.LayoutParams hitLp = new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.MATCH_PARENT, dp(170));
        hitLp.setMargins(0, dp(18), 0, 0);
        root.addView(hitButton, hitLp);
        hitButton.setOnTouchListener((v, event) -> {
            if (event.getActionMasked() == MotionEvent.ACTION_DOWN) {
                triggerHit();
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

        status = text("음악을 선택해 주세요.", 13, Color.rgb(180, 180, 190));
        status.setGravity(Gravity.CENTER);
        LinearLayout.LayoutParams statusLp = matchWrap();
        statusLp.setMargins(0, dp(16), 0, 0);
        root.addView(status, statusLp);

        setContentView(root);
    }

    private void pickAudio() {
        Intent i = new Intent(Intent.ACTION_OPEN_DOCUMENT);
        i.addCategory(Intent.CATEGORY_OPENABLE);
        i.setType("audio/*");
        i.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
        startActivityForResult(i, PICK_AUDIO);
    }

    @Override protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (requestCode != PICK_AUDIO || resultCode != RESULT_OK || data == null) return;
        Uri uri = data.getData();
        if (uri == null) return;
        status.setText("원곡을 네이티브 PCM으로 변환 중...");
        playButton.setEnabled(false);
        hitButton.setEnabled(false);

        new Thread(() -> {
            try {
                File dst = new File(getCacheDir(), "native_audio_song.bin");
                try (InputStream in = getContentResolver().openInputStream(uri);
                     FileOutputStream out = new FileOutputStream(dst)) {
                    if (in == null) throw new IllegalStateException("파일을 열 수 없습니다.");
                    byte[] buf = new byte[128 * 1024];
                    int n;
                    while ((n = in.read(buf)) > 0) out.write(buf, 0, n);
                }
                boolean nativeOk = NativeAudio.nativeLoadSong(dst.getAbsolutePath());
                runOnUiThread(() -> {
                    if (!nativeOk) {
                        status.setText("네이티브 디코딩 실패");
                        return;
                    }
                    currentFile = dst;
                    preparePlayer(dst);
                });
            } catch (Throwable e) {
                runOnUiThread(() -> status.setText("불러오기 실패: " + e.getClass().getSimpleName()));
            }
        }).start();
    }

    private void preparePlayer(File file) {
        releasePlayer();
        try {
            player = new MediaPlayer();
            player.setAudioAttributes(new AudioAttributes.Builder()
                .setUsage(AudioAttributes.USAGE_GAME)
                .setContentType(AudioAttributes.CONTENT_TYPE_MUSIC)
                .build());
            player.setDataSource(file.getAbsolutePath());
            player.setVolume(1.0f, 1.0f);
            player.setOnPreparedListener(mp -> {
                playButton.setEnabled(true);
                hitButton.setEnabled(true);
                playButton.setText("2. 재생");
                status.setText("준비 완료. 재생 후 HIT를 리듬에 맞춰 눌러 보세요.");
            });
            player.setOnCompletionListener(mp -> playButton.setText("2. 다시 재생"));
            player.prepareAsync();
        } catch (Throwable e) {
            status.setText("재생 준비 실패: " + e.getClass().getSimpleName());
        }
    }

    private void togglePlayback() {
        if (player == null) return;
        if (player.isPlaying()) {
            player.pause();
            playButton.setText("2. 계속 재생");
        } else {
            if (player.getCurrentPosition() >= player.getDuration() - 100) player.seekTo(0);
            player.start();
            playButton.setText("일시정지");
        }
    }

    private void triggerHit() {
        if (player == null || !player.isPlaying()) return;
        double seconds = player.getCurrentPosition() / 1000.0;
        NativeAudio.nativeHit(seconds, hitGain);
        status.setText(String.format(Locale.US,
            "HIT %.3fs · 원곡 실제 조각 · Oboe", seconds));
    }

    private void releasePlayer() {
        if (player != null) {
            try { player.stop(); } catch (Throwable ignored) {}
            player.release();
            player = null;
        }
    }

    @Override protected void onResume() {
        super.onResume();
        NativeAudio.nativeStart();
    }

    @Override protected void onDestroy() {
        releasePlayer();
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

    private TextView text(String value, int sp, int color) {
        TextView t = new TextView(this);
        t.setText(value);
        t.setTextSize(sp);
        t.setTextColor(color);
        return t;
    }

    private LinearLayout.LayoutParams wideButton() {
        return new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.MATCH_PARENT, dp(58));
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
