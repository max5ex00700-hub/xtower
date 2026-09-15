package com.gongsu.calendar;

import android.Manifest;
import android.app.Activity;
import android.content.ActivityNotFoundException;
import android.content.ClipData;
import android.content.ContentValues;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.os.Environment;
import android.provider.MediaStore;
import android.util.Base64;
import android.webkit.GeolocationPermissions;
import android.webkit.ValueCallback;
import android.webkit.WebChromeClient;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.Toast;

import java.io.File;
import java.io.FileOutputStream;
import java.io.OutputStream;
import java.net.URLDecoder;
import java.nio.charset.StandardCharsets;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class MainActivity extends Activity {
    private WebView webView;
    private ValueCallback<Uri[]> filePathCallback;
    private static final int FILE_CHOOSER_REQUEST_CODE = 51;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        webView = new WebView(this);
        setContentView(webView);

        WebSettings s = webView.getSettings();
        s.setJavaScriptEnabled(true);
        s.setDomStorageEnabled(true);
        s.setDatabaseEnabled(true);
        s.setAllowFileAccess(true);
        s.setAllowContentAccess(true);
        s.setMediaPlaybackRequiresUserGesture(false);

        webView.addJavascriptInterface(new GongsuBridge(this), "GongsuDB");
        webView.setWebViewClient(new WebViewClient());
        webView.setWebChromeClient(new WebChromeClient() {
            @Override
            public void onGeolocationPermissionsShowPrompt(String origin, GeolocationPermissions.Callback callback) {
                callback.invoke(origin, false, false);
            }

            @Override
            public boolean onShowFileChooser(WebView webView, ValueCallback<Uri[]> callback,
                                             WebChromeClient.FileChooserParams params) {
                if (filePathCallback != null) {
                    filePathCallback.onReceiveValue(null);
                }
                filePathCallback = callback;
                try {
                    Intent intent = params.createIntent();
                    intent.addCategory(Intent.CATEGORY_OPENABLE);
                    intent.setType("application/json");
                    startActivityForResult(intent, FILE_CHOOSER_REQUEST_CODE);
                } catch (ActivityNotFoundException e) {
                    filePathCallback = null;
                    return false;
                }
                return true;
            }
        });

        webView.setDownloadListener((url, userAgent, contentDisposition, mimeType, contentLength) -> {
            if (url == null || !url.startsWith("data:")) return;
            saveDataUriToDownloads(url, contentDisposition);
        });

        webView.loadUrl("file:///android_asset/index.html");
    }

    private void saveDataUriToDownloads(String url, String contentDisposition) {
        try {
            int comma = url.indexOf(',');
            if (comma < 0) throw new IllegalArgumentException("bad data uri");
            String head = url.substring(0, comma);
            String payload = url.substring(comma + 1);
            byte[] bytes = head.contains(";base64")
                    ? Base64.decode(payload, Base64.DEFAULT)
                    : URLDecoder.decode(payload, "UTF-8").getBytes(StandardCharsets.UTF_8);

            String fileName = parseFilename(contentDisposition);
            if (fileName == null || fileName.isEmpty()) {
                fileName = "global_wage_backup_" + System.currentTimeMillis() + ".json";
            }
            if (!fileName.toLowerCase().endsWith(".json")) fileName += ".json";

            if (Build.VERSION.SDK_INT >= 29) {
                ContentValues values = new ContentValues();
                values.put(MediaStore.Downloads.DISPLAY_NAME, fileName);
                values.put(MediaStore.Downloads.MIME_TYPE, "application/json");
                values.put(MediaStore.Downloads.RELATIVE_PATH,
                        Environment.DIRECTORY_DOWNLOADS + "/GlobalWageCalculator");
                values.put(MediaStore.Downloads.IS_PENDING, 1);

                Uri item = getContentResolver().insert(MediaStore.Downloads.EXTERNAL_CONTENT_URI, values);
                if (item == null) throw new IllegalStateException("MediaStore insert failed");
                try (OutputStream out = getContentResolver().openOutputStream(item, "w")) {
                    if (out == null) throw new IllegalStateException("openOutputStream failed");
                    out.write(bytes);
                    out.flush();
                }
                values.clear();
                values.put(MediaStore.Downloads.IS_PENDING, 0);
                getContentResolver().update(item, values, null, null);
                Toast.makeText(this, "백업 저장 완료: Download/GlobalWageCalculator/" + fileName,
                        Toast.LENGTH_LONG).show();
            } else {
                if (checkSelfPermission(Manifest.permission.WRITE_EXTERNAL_STORAGE) != PackageManager.PERMISSION_GRANTED) {
                    requestPermissions(new String[]{Manifest.permission.WRITE_EXTERNAL_STORAGE}, 74);
                    Toast.makeText(this, "저장 권한을 허용한 뒤 백업을 다시 눌러주세요.", Toast.LENGTH_LONG).show();
                    return;
                }
                File dir = new File(Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_DOWNLOADS),
                        "GlobalWageCalculator");
                if (!dir.exists() && !dir.mkdirs()) throw new IllegalStateException("mkdir failed");
                File outFile = new File(dir, fileName);
                try (FileOutputStream out = new FileOutputStream(outFile)) {
                    out.write(bytes);
                    out.flush();
                }
                Toast.makeText(this, "백업 저장 완료: " + outFile.getAbsolutePath(), Toast.LENGTH_LONG).show();
            }
        } catch (Exception e) {
            Toast.makeText(this, "백업 파일 저장에 실패했습니다.", Toast.LENGTH_LONG).show();
        }
    }

    private String parseFilename(String contentDisposition) {
        if (contentDisposition == null) return null;
        Matcher m = Pattern.compile("filename\\*?=(?:UTF-8''|\\\")?([^\\\";]+)", Pattern.CASE_INSENSITIVE)
                .matcher(contentDisposition);
        if (!m.find()) return null;
        try { return URLDecoder.decode(m.group(1).trim(), "UTF-8"); }
        catch (Exception e) { return m.group(1).trim(); }
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        if (requestCode != FILE_CHOOSER_REQUEST_CODE) {
            super.onActivityResult(requestCode, resultCode, data);
            return;
        }
        if (filePathCallback == null) return;

        Uri[] result = null;
        if (resultCode == RESULT_OK && data != null) {
            ClipData clip = data.getClipData();
            if (clip != null) {
                int n = clip.getItemCount();
                result = new Uri[n];
                for (int i = 0; i < n; i++) result[i] = clip.getItemAt(i).getUri();
            } else if (data.getData() != null) {
                result = new Uri[]{data.getData()};
            }
        }
        filePathCallback.onReceiveValue(result);
        filePathCallback = null;
    }

    @Override
    public void onBackPressed() {
        if (webView != null && webView.canGoBack()) webView.goBack();
        else super.onBackPressed();
    }
}
