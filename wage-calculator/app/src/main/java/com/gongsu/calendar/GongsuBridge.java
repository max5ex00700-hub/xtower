package com.gongsu.calendar;

import android.Manifest;
import android.app.Activity;
import android.content.Context;
import android.content.SharedPreferences;
import android.content.pm.PackageManager;
import android.os.Build;
import android.webkit.JavascriptInterface;

public class GongsuBridge {
    private static final String PREF = "gongsu_native_db";
    private static final String KEY = "json";
    private final Activity activity;
    private final SharedPreferences prefs;

    public GongsuBridge(Activity activity) {
        this.activity = activity;
        this.prefs = activity.getSharedPreferences(PREF, Context.MODE_PRIVATE);
    }

    @JavascriptInterface
    public String loadJson() {
        return prefs.getString(KEY, "");
    }

    @JavascriptInterface
    public void saveJson(String json) {
        if ("notification".equals(json)) {
            if (Build.VERSION.SDK_INT >= 33 &&
                    activity.checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED) {
                activity.runOnUiThread(() -> activity.requestPermissions(
                        new String[]{Manifest.permission.POST_NOTIFICATIONS}, 73));
            }
            return;
        }
        prefs.edit().putString(KEY, json == null ? "" : json).apply();
    }
}
