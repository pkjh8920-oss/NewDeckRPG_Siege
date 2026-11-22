// ============================================================
// SaveManager.cs  (2026-11-15, 3-73 이어하기)
// 런 저장 파일을 쓰고 읽는 곳 (화면 쪽. 파일·JSON 은 유니티 기능이라 Core 가 아니라 여기)
//
// 저장 위치: Application.persistentDataPath/run_save.json
//   윈도우 예) C:\Users\이름\AppData\LocalLow\회사이름\게임이름\run_save.json
//   에디터에서 플레이해도 같은 곳에 저장됨 (지우고 싶으면 그 파일을 지우면 됨)
//
// JsonUtility = 유니티가 주는 JSON 변환기. [System.Serializable] 클래스의 public 필드를 글자로 바꿈
//   자바로 치면 Gson 의 gson.toJson(obj) / gson.fromJson(text, RunSave.class) 와 같은 일
//
// 저장 파일은 하나뿐 (싱글·멀티 같이 씀). 3-150: 저장이 있는데 타이틀에서 싱글·멀티를 누르면 확인 창 → [새 게임] 이면 이 파일을 지움 (TitlePanel.ConfirmNewGame)
// ============================================================

using System.IO;
using UnityEngine;

// 3-210: 웹(브라우저)에서는 파일 대신 PlayerPrefs(브라우저 저장소 IndexedDB)에 저장
//   웹에서 파일로 쓰면 새로고침했을 때 남아 있을지 확실하지 않아서, 설정·도감처럼 PlayerPrefs 를 씀
//   PlayerPrefs = 이름표(키)를 붙여 글자를 보관하는 작은 저장소 (자바의 Preferences 와 비슷)
public static class SaveManager
{
#if UNITY_WEBGL && !UNITY_EDITOR
    static readonly bool UsePrefs = true;    // 3-224: const 대신 static readonly (const 면 컴파일러가 "절대 안 오는 코드" 경고 CS0162 를 냄)
#else
    static readonly bool UsePrefs = false;
#endif
    const string PrefsKey = "RunSave";

    static string FilePath { get { return Path.Combine(Application.persistentDataPath, "run_save.json"); } }

    // 저장 파일이 있는지 (타이틀의 [이어하기] 버튼을 보일지)
    public static bool HasSave
    {
        get { return UsePrefs ? PlayerPrefs.HasKey(PrefsKey) : File.Exists(FilePath); }
    }

    // 저장해도 되는 순간이면 저장. 저장했으면 true
    //   3-141: 자동 저장 없음 → 일시정지 메뉴의 [저장] 버튼을 눌렀을 때만 부름
    public static bool TrySave(RunState run)
    {
        if (run == null || !run.CanSave) return false;
        try
        {
            RunSave save = run.ToSave();
            save.SavedAt = System.DateTime.Now.ToString("MM/dd HH:mm");
            string json = JsonUtility.ToJson(save);
            if (UsePrefs)
            {
                PlayerPrefs.SetString(PrefsKey, json);
                PlayerPrefs.Save();   // 웹: 지금 바로 브라우저 저장소에 씀
                return true;
            }
            // 쓰는 도중에 게임이 꺼져도 예전 저장이 망가지지 않게: 임시 파일에 다 쓴 뒤 바꿔치기
            string temp = FilePath + ".tmp";
            File.WriteAllText(temp, json);
            File.Copy(temp, FilePath, true);
            File.Delete(temp);
            return true;
        }
        catch (System.Exception e)
        {
            // 저장이 실패해도 게임은 계속 (자바의 catch (Exception e) 와 같음)
            Debug.LogWarning("[저장] 실패: " + e.Message);
            return false;
        }
    }

    // 저장 파일 읽기. 없거나 망가졌으면 null
    public static RunSave LoadSave()
    {
        if (!HasSave) return null;
        try
        {
            string json = UsePrefs ? PlayerPrefs.GetString(PrefsKey, "") : File.ReadAllText(FilePath);
            RunSave save = JsonUtility.FromJson<RunSave>(json);
            if (save == null || save.Version != RunSave.CurrentVersion) return null;
            return save;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[저장] 읽기 실패: " + e.Message);
            return null;
        }
    }

    // 저장을 불러와서 런으로. 불러올 수 없으면 저장 파일을 지우고 null
    public static RunState LoadRun()
    {
        RunState run = null;
        try { run = RunState.FromSave(LoadSave()); }
        catch (System.Exception e) { Debug.LogWarning("[저장] 런 만들기 실패: " + e.Message); }
        if (run == null) Delete();
        return run;
    }

    // 런이 끝나면(패배·클리어) 지움 → 죽은 판을 이어하기로 되살릴 수 없게
    public static void Delete()
    {
        try
        {
            if (UsePrefs) { PlayerPrefs.DeleteKey(PrefsKey); PlayerPrefs.Save(); }
            else if (File.Exists(FilePath)) File.Delete(FilePath);
        }
        catch (System.Exception e) { Debug.LogWarning("[저장] 지우기 실패: " + e.Message); }
    }
}
