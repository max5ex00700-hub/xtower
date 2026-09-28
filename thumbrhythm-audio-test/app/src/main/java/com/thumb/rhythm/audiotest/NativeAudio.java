package com.thumb.rhythm.audiotest;

final class NativeAudio {
    static {
        System.loadLibrary("native-audio");
    }

    static native boolean nativeLoadSong(String path);
    static native void nativeHit(double positionSec, float gain);
    static native void nativeStart();
    static native void nativeStop();

    private NativeAudio() {}
}
