// ============================================================
// WebQualitySwitch.cs  (3-235 웹 빌드 화질 바꾸기 메뉴)
//
// 유니티 위쪽 메뉴 [DeckRPG] → [웹 화질: 깃허브용 (100MB 아래)] / [웹 화질: itch.io용 (고화질)]
//   깃허브용 : 캐릭터·이펙트·배경 = DXT5, 배경 최대 1280, 이펙트 768 / UI·카드·아이콘 = BC7  → 약 90MB (3-235 때)
//   3-275 깃허브용을 더 작게: 소리·그림이 늘어서 143MB 가 됨 → 아이콘 빼고 모두 DXT5(품질 50),
//         배경 1024 · 캐릭터 768 · 이펙트 512 · UI 1024, 배경음은 웹에서만 음질 0.25 · 32kHz → 100MB 아래 목표
//         itch.io용을 누르면 전부 원래대로 (배경음 웹 설정도 지움)
//   3-279 그래도 113MB → 깃허브용 크기를 한 단계 더: 캐릭터 768 → 512 · 이펙트 512 → 256 (배경·UI 는 화면을 꽉 채워서 1024 그대로), 배경음 22kHz · 음질 0.1
//   3-276 그래도 123MB → 깃허브용은 DXT5 에 Crunch 압축까지 (해상도는 그대로, 파일을 한 번 더 압축) + 배경음 음질 0.15
//   itch.io용: 모든 그림 BC7(품질 100), 배경 최대 2048, 캐릭터·이펙트 1024                  → 약 120MB
//   바꾸는 건 "웹(WebGL) 빌드 설정"뿐. 에디터 화면·Windows 빌드는 그대로
//   누르면 그림을 다시 압축해서 몇 분~수십 분 걸림
//
// Editor 폴더 안의 스크립트 = 유니티 에디터에서만 쓰이고, 게임 빌드에는 안 들어감
// 자바로 치면: IDE 플러그인처럼 "개발 도구에만 붙는 코드"
// ============================================================

using UnityEditor;
using UnityEngine;

public static class WebQualitySwitch
{
    [MenuItem("DeckRPG/웹 화질: 깃허브용 (100MB 아래)")]
    static void GitHub() { Apply(false); }

    [MenuItem("DeckRPG/웹 화질: itch.io용 (고화질)")]
    static void Itch() { Apply(true); }

    static void Apply(bool high)
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources" });
        int changed = 0;
        AssetDatabase.StartAssetEditing();   // 다 바꾼 뒤 한 번에 다시 불러오기 (빠름)
        try
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;
                EditorUtility.DisplayProgressBar("웹 화질 바꾸는 중", path, i / (float)guids.Length);

                // 폴더로 종류 구분: Art(캐릭터·몬스터) / Fx(이펙트) / Backgrounds(배경) / 나머지(UI·카드·아이콘)
                bool art = path.Contains("/Resources/Art/");
                bool fx = path.Contains("/Resources/Fx/");
                bool bg = path.Contains("/Resources/Backgrounds/");
                bool icon = path.Contains("/Resources/Icons/");
                bool big = art || fx || bg;

                TextureImporterPlatformSettings s = ti.GetPlatformTextureSettings("WebGL");
                s.overridden = true;
                s.crunchedCompression = false;
                if (high || icon)
                {
                    s.format = TextureImporterFormat.BC7;   // 선·글자가 깨끗한 고화질 압축 (아이콘은 작아서 깃허브용도 BC7)
                    s.compressionQuality = 100;
                }
                else
                {
                    s.format = TextureImporterFormat.DXT5Crunched;   // 3-276: Crunch = DXT5 를 한 번 더 압축 (용량 절반 이하)
                    s.crunchedCompression = true;
                    s.compressionQuality = 50;
                }
                // 3-279: 크기는 2의 거듭제곱(256·512·1024·2048)으로
                if (bg) s.maxTextureSize = high ? 2048 : 1024;
                else if (fx) s.maxTextureSize = high ? 1024 : 256;
                else if (art) s.maxTextureSize = high ? 1024 : 512;
                else s.maxTextureSize = high ? 2048 : 1024;   // UI·카드 (카드는 232×320 이라 그대로)

                ti.SetPlatformTextureSettings(s);
                ti.mipmapEnabled = false;                   // 2D 게임이라 밉맵 필요 없음 (켜지면 압축이 안 되고 용량 큼)
                ti.SaveAndReimport();
                changed++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
        }
        int sounds = ApplyBgm(high);
        Debug.Log("[웹 화질] " + (high ? "itch.io용 (고화질)" : "깃허브용 (100MB 아래)") + " 으로 바꿈: 그림 " + changed + "장, 배경음 " + sounds + "곡");
    }

    // 3-275: 배경음(Sound/BGM)의 "웹 빌드 전용" 음질 설정. 소리 파일 자체는 안 바꾸고 .meta 의 WebGL 설정만
    //   깃허브용 = 음질 0.25 · 32kHz (배경음은 용량이 커서) / itch.io용 = 웹 전용 설정을 지워서 원래 설정 그대로
    static int ApplyBgm(bool high)
    {
        string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Resources/Sound/BGM" });
        int n = 0;
        foreach (string g in guids)
        {
            AudioImporter ai = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(g)) as AudioImporter;
            if (ai == null) continue;
            if (high) ai.ClearSampleSettingOverride("WebGL");
            else
            {
                AudioImporterSampleSettings ss = ai.defaultSampleSettings;
                ss.quality = 0.1f;    // 3-276: 0.25 → 0.15 / 3-279: 0.1
                ss.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
                ss.sampleRateOverride = 22050;   // 3-279: 32kHz → 22kHz
                ai.SetOverrideSampleSettings("WebGL", ss);
            }
            ai.SaveAndReimport();
            n++;
        }
        return n;
    }
}
