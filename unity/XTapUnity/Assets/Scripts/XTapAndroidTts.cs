using UnityEngine;

public sealed class XTapAndroidTts : MonoBehaviour
{
#if UNITY_ANDROID && !UNITY_EDITOR
    AndroidJavaObject tts;
    bool ready;
    string pendingText;

    sealed class InitListener : AndroidJavaProxy
    {
        readonly XTapAndroidTts owner;

        public InitListener(XTapAndroidTts owner)
            : base("android.speech.tts.TextToSpeech$OnInitListener")
        {
            this.owner = owner;
        }

        void onInit(int status)
        {
            owner.OnTtsInitialized(status);
        }
    }

    void Awake()
    {
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                tts = new AndroidJavaObject(
                    "android.speech.tts.TextToSpeech",
                    activity,
                    new InitListener(this)
                );
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("X탑 TTS 초기화 실패: " + e.Message);
        }
    }

    void OnTtsInitialized(int status)
    {
        // android.speech.tts.TextToSpeech.SUCCESS == 0
        if (status != 0 || tts == null) return;

        try
        {
            using (var locale = new AndroidJavaObject("java.util.Locale", "ko", "KR"))
            {
                tts.Call<int>("setLanguage", locale);
            }

            // Slightly brisk and bright so very short battle lines do not drag.
            tts.Call<int>("setSpeechRate", 1.10f);
            tts.Call<int>("setPitch", 1.08f);
            ready = true;

            if (!string.IsNullOrEmpty(pendingText))
            {
                string text = pendingText;
                pendingText = null;
                Speak(text);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("X탑 TTS 언어 설정 실패: " + e.Message);
        }
    }

    public void Speak(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        if (!ready || tts == null)
        {
            pendingText = text;
            return;
        }

        try
        {
            // minSdk is 24, so use the API 21+ speak overload.
            using (var bundle = new AndroidJavaObject("android.os.Bundle"))
            {
                tts.Call<int>("speak", text, 0, bundle, "xtap_battle_bubble");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("X탑 TTS 재생 실패: " + e.Message);
        }
    }

    public void StopSpeaking()
    {
        if (tts == null) return;
        try { tts.Call<int>("stop"); }
        catch { }
    }

    void OnDestroy()
    {
        if (tts == null) return;
        try { tts.Call<int>("stop"); } catch { }
        try { tts.Call("shutdown"); } catch { }
        tts.Dispose();
        tts = null;
        ready = false;
    }
#else
    public void Speak(string text) { }
    public void StopSpeaking() { }
#endif
}
