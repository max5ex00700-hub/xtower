package com.thumb.rhythm.audiotest;

final class NativeAudio {
    static { System.loadLibrary("native-audio"); }

    static native boolean nativeLoadDrums(String stickPath, String hatPath);
    static native void nativeDrumHit(int mode, float gain);
    static native void nativeStart();
    static native void nativeStop();

    private NativeAudio() {}
}
