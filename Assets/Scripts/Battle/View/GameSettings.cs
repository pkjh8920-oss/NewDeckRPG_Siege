// ============================================================
// GameSettings.cs
// 설정 값 (소리 크기, 전체 화면)을 기억하는 곳
//
// PlayerPrefs = 유니티가 컴퓨터에 작은 값을 저장해 주는 기능 (게임을 껐다 켜도 남음)
//   자바의 java.util.prefs.Preferences 와 비슷함
//
// 지금은 소리 파일이 없어서 실제로 들리는 건 없음.
// 나중에 효과음·배경음악을 넣을 때 BgmVolume, SfxVolume 을 곱해서 쓰면 됨
//   예) audioSource.volume = GameSettings.BgmVolume;
// 전체 소리(MasterVolume)는 AudioListener.volume 에 바로 들어가서 모든 소리에 적용됨
// ============================================================

using UnityEngine;

public static class GameSettings
{
    public static float MasterVolume = 1f;   // 0 ~ 1
    public static float BgmVolume = 0.8f;
    public static float SfxVolume = 0.8f;
    public static bool Fullscreen = true;

    private static bool loaded;

    public static void Load()
    {
        if (loaded) return;
        loaded = true;
        MasterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
        BgmVolume = PlayerPrefs.GetFloat("BgmVolume", 0.8f);
        SfxVolume = PlayerPrefs.GetFloat("SfxVolume", 0.8f);
        Fullscreen = PlayerPrefs.GetInt("Fullscreen", Screen.fullScreen ? 1 : 0) == 1;
        Apply();
    }

    public static void Save()
    {
        PlayerPrefs.SetFloat("MasterVolume", MasterVolume);
        PlayerPrefs.SetFloat("BgmVolume", BgmVolume);
        PlayerPrefs.SetFloat("SfxVolume", SfxVolume);
        PlayerPrefs.SetInt("Fullscreen", Fullscreen ? 1 : 0);
        PlayerPrefs.Save();
        Apply();
    }

    public static void Apply()
    {
        AudioListener.volume = MasterVolume;
        // 에디터의 Game 창에서는 전체 화면이 안 바뀜 (빌드한 게임에서만 적용)
        if (!Application.isEditor) Screen.fullScreen = Fullscreen;
    }
}
