// ============================================================
// DiscoveryStore.cs  (3-180 도감)
// Core 의 Discovery(본 카드·아티팩트 목록)를 컴퓨터에 저장하고 불러오는 곳
//
// PlayerPrefs = 유니티가 작은 값을 컴퓨터에 저장해 주는 기능 (GameSettings 와 같은 방법)
//   키 "SeenCards"  = "card_id1,card_id2,..."
//   키 "SeenRelics" = "relic_id1,relic_id2,..."
//   자바로 치면 Preferences.put("SeenCards", 글자) / get("SeenCards", "")
//
// 런 저장 파일(run_save.json)과는 따로 → 새 게임을 시작하거나 런이 끝나도 도감 기록은 남음
// ============================================================

using UnityEngine;

public static class DiscoveryStore
{
    const string CardKey = "SeenCards";
    const string RelicKey = "SeenRelics";
    private static bool loaded;

    // 게임을 켤 때 한 번 (BattleScreen 이 부름)
    public static void Load()
    {
        if (loaded) return;
        loaded = true;
        Discovery.Load(PlayerPrefs.GetString(CardKey, ""), PlayerPrefs.GetString(RelicKey, ""));
    }

    // 새로 본 게 있을 때만 저장 (Dirty 깃발)
    public static void SaveIfDirty()
    {
        if (!Discovery.Dirty) return;
        PlayerPrefs.SetString(CardKey, Discovery.CardsText());
        PlayerPrefs.SetString(RelicKey, Discovery.RelicsText());
        PlayerPrefs.Save();
        Discovery.Dirty = false;
    }
}
