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

public static class SaveManager
{
    static string FilePath { get { return Path.Combine(Application.persistentDataPath, "run_save.json"); } }

    // 저장 파일이 있는지 (타이틀의 [이어하기] 버튼을 보일지)
    public static bool HasSave
    {
        get { return File.Exists(FilePath); }
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
            RunSave save = JsonUtility.FromJson<RunSave>(File.ReadAllText(FilePath));
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
        try { if (File.Exists(FilePath)) File.Delete(FilePath); }
        catch (System.Exception e) { Debug.LogWarning("[저장] 지우기 실패: " + e.Message); }
    }
}
