// ============================================================
// WebQualitySwitch.cs  (3-235 웹 빌드 화질 바꾸기 메뉴)
//
// 유니티 위쪽 메뉴 [DeckRPG] → [웹 화질: 깃허브용 (100MB 아래)] / [웹 화질: itch.io용 (고화질)]
//   깃허브용 : 캐릭터·이펙트·배경 = DXT5, 배경 최대 1280, 이펙트 768 / UI·카드·아이콘 = BC7  → 약 90MB
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
                bool big = art || fx || bg;

                TextureImporterPlatformSettings s = ti.GetPlatformTextureSettings("WebGL");
                s.overridden = true;
                s.crunchedCompression = false;
                if (high || !big)
                {
                    s.format = TextureImporterFormat.BC7;   // 선·글자가 깨끗한 고화질 압축
                    s.compressionQuality = 100;
                }
                else
                {
                    s.format = TextureImporterFormat.DXT5;  // 용량이 작은 압축 (gzip 이 잘 됨)
                    s.compressionQuality = 75;
                }
                if (bg) s.maxTextureSize = high ? 2048 : 1280;
                else if (fx) s.maxTextureSize = high ? 1024 : 768;
                else if (art) s.maxTextureSize = 1024;
                else s.maxTextureSize = 2048;

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
        Debug.Log("[웹 화질] " + (high ? "itch.io용 (고화질)" : "깃허브용 (100MB 아래)") + " 으로 바꿈: 그림 " + changed + "장");
    }
}
