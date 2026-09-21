using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using UnityEngine.Networking;

public sealed class XTapOriginalApkAssets : MonoBehaviour
{
    public static XTapOriginalApkAssets Instance { get; private set; }
    public bool Ready { get; private set; }
    public string Error { get; private set; }

    private byte[] apkBytes;
    private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AudioClip> audioClips = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public IEnumerator Load()
    {
        // 1) Prefer a complete source APK when present.
        string fullPath = Path.Combine(Application.streamingAssetsPath, "xtop_source.apk");
        using (var req = UnityWebRequest.Get(fullPath))
        {
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success && req.downloadHandler.data != null && req.downloadHandler.data.Length > 1024)
            {
                apkBytes = req.downloadHandler.data;
                Ready = true;
                yield break;
            }
        }

        // 2) GitHub web upload has a per-file size limit, so also support two smaller parts.
        byte[] p1 = null;
        byte[] p2 = null;

        string part1Path = Path.Combine(Application.streamingAssetsPath, "xtop_source.part1");
        using (var req1 = UnityWebRequest.Get(part1Path))
        {
            yield return req1.SendWebRequest();
            if (req1.result == UnityWebRequest.Result.Success) p1 = req1.downloadHandler.data;
        }

        string part2Path = Path.Combine(Application.streamingAssetsPath, "xtop_source.part2");
        using (var req2 = UnityWebRequest.Get(part2Path))
        {
            yield return req2.SendWebRequest();
            if (req2.result == UnityWebRequest.Result.Success) p2 = req2.downloadHandler.data;
        }

        if (p1 == null || p1.Length == 0 || p2 == null || p2.Length == 0)
        {
            Error = "원본 X탑 데이터가 없습니다. xtop_source.part1 / part2를 StreamingAssets에 올려주세요.";
            yield break;
        }

        apkBytes = new byte[p1.Length + p2.Length];
        Buffer.BlockCopy(p1, 0, apkBytes, 0, p1.Length);
        Buffer.BlockCopy(p2, 0, apkBytes, p1.Length, p2.Length);

        if (apkBytes.Length < 1024)
        {
            Error = "원본 X탑 데이터 결합에 실패했습니다.";
            yield break;
        }

        Ready = true;
    }

    public Sprite GetSprite(string entry)
    {
        if (!Ready || apkBytes == null) return null;
        Sprite cached;
        if (sprites.TryGetValue(entry, out cached)) return cached;

        byte[] bytes = Read(entry);
        if (bytes == null) return null;
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!tex.LoadImage(bytes)) { Destroy(tex); return null; }
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var sp = Sprite.Create(tex, new Rect(0,0,tex.width,tex.height), new Vector2(.5f,.5f), 100f);
        sprites[entry] = sp;
        return sp;
    }

    public AudioClip GetWav(string entry)
    {
        AudioClip cached;
        if (audioClips.TryGetValue(entry, out cached)) return cached;
        byte[] bytes = Read(entry);
        if (bytes == null || bytes.Length < 44) return null;

        try
        {
            int channels = BitConverter.ToInt16(bytes, 22);
            int sampleRate = BitConverter.ToInt32(bytes, 24);
            int bits = BitConverter.ToInt16(bytes, 34);
            int dataPos = FindData(bytes);
            if (dataPos < 0 || bits != 16) return null;
            int dataLen = BitConverter.ToInt32(bytes, dataPos + 4);
            int start = dataPos + 8;
            int samples = Math.Min(dataLen, bytes.Length - start) / 2;
            float[] data = new float[samples];
            for (int i=0;i<samples;i++) data[i] = BitConverter.ToInt16(bytes, start+i*2) / 32768f;
            var clip = AudioClip.Create(Path.GetFileNameWithoutExtension(entry), samples / Math.Max(1,channels), channels, sampleRate, false);
            clip.SetData(data, 0);
            audioClips[entry] = clip;
            return clip;
        }
        catch { return null; }
    }

    private static int FindData(byte[] b)
    {
        for (int i=12;i+8<b.Length;i++)
            if (b[i]=='d' && b[i+1]=='a' && b[i+2]=='t' && b[i+3]=='a') return i;
        return -1;
    }

    private byte[] Read(string entryName)
    {
        try
        {
            using (var ms = new MemoryStream(apkBytes, false))
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Read, false))
            {
                var e = zip.GetEntry(entryName);
                if (e == null) return null;
                using (var s = e.Open())
                using (var o = new MemoryStream())
                { s.CopyTo(o); return o.ToArray(); }
            }
        }
        catch { return null; }
    }
}
