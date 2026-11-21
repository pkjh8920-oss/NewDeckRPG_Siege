// ============================================================
// DevPanel.cs
// 개발자 전용 테스트 도구 (치트). F1 로 열고 닫음
//
// ★ 유저는 못 씀: Enabled 가 false 면 F1 도, 위쪽 [개발] 버튼도 아예 안 생김
//   Enabled = 유니티 에디터에서 실행 중  또는  "Development Build" 로 빌드한 게임
//   → 팀원 테스트용 빌드: Build Settings 에서 Development Build 체크
//   → 유저에게 나눠 줄 빌드: 체크 해제 (그러면 이 패널과 테스트 버튼이 전부 사라짐)
//   Debug.isDebugBuild = 개발 빌드인지 알려 주는 유니티 값 (자바의 System.getProperty 로 설정 읽는 느낌)
//
// 할 수 있는 것: HP 회복, 무적, 즉시 승리, 골드, 스탯 +1, 카드·아티팩트 넣기, 다음 칸·스테이지 이동
// 실제 규칙 변경은 BattleSystem.DevWin / RunState.Dev... 가 함. 이 파일은 버튼만
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DevPanel : MonoBehaviour
{
    public static bool Enabled
    {
        get { return Application.isEditor || Debug.isDebugBuild; }
    }

    // BattleScreen 이 넣어 주는 것들
    public System.Func<RunState> GetRun;
    public System.Func<BattleSystem> GetBattle;   // 전투 중이 아니면 null
    public System.Action AfterChange;             // 화면 다시 그리기
    public System.Action<string> Toast;

    private int target;                            // 대상 플레이어 번호
    private Text targetLabel;
    private RectTransform listLayer;               // 카드·아티팩트 고르기 목록

    public static DevPanel Create(Transform parent)
    {
        Image bg = UIFactory.MakeImage("DevPanel", parent, new Color(0.05f, 0.08f, 0.06f, 0.96f));
        UIFactory.Place(bg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20, -120), new Vector2(430, 830));   // 3-180: 도감 줄 때문에 780 → 830
        bg.gameObject.AddComponent<Outline>().effectColor = UIFactory.Hex("#5AE07A");
        DevPanel panel = bg.gameObject.AddComponent<DevPanel>();
        panel.Build();
        bg.gameObject.SetActive(false);
        return panel;
    }

    public void Toggle()
    {
        if (!Enabled) return;
        gameObject.SetActive(!gameObject.activeSelf);
        if (gameObject.activeSelf) { transform.SetAsLastSibling(); listLayer.gameObject.SetActive(false); UpdateTarget(); }
    }

    void Build()
    {
        Text title = UIFactory.MakeText("Title", transform, "개발 도구 (F1)  — 유저 빌드에는 없음", 20, UIFactory.Hex("#7AF09A"), TextAnchor.MiddleCenter);
        UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -8), new Vector2(410, 30));

        float y = -50;
        targetLabel = UIFactory.MakeText("Target", transform, "", 20, Color.white, TextAnchor.MiddleCenter);
        UIFactory.Place(targetLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(250, 36));
        Btn("◀", new Vector2(-170, y), 50, () => { target--; UpdateTarget(); });
        Btn("▶", new Vector2(170, y), 50, () => { target++; UpdateTarget(); });

        y -= 50;
        Section("전투", y); y -= 34;
        Btn("즉시 승리", new Vector2(-105, y), 195, () => { BattleSystem b = GetBattle(); if (b != null) { b.DevWin(); Done("즉시 승리"); } else Say("전투 중이 아님"); });
        Btn("무적 켜기/끄기", new Vector2(105, y), 195, () => { PlayerCombatant p = Target(); if (p == null) return; p.DevInvincible = !p.DevInvincible; Done(p.Name + " 무적 " + (p.DevInvincible ? "켜짐" : "꺼짐")); });
        y -= 48;
        Btn("HP 전부 회복", new Vector2(-105, y), 195, () => { foreach (PlayerCombatant p in Players()) { if (p.IsDead) p.Hp = 1; p.Heal(p.MaxHp); } Done("모두 HP 회복"); });
        Btn("에너지 +3", new Vector2(105, y), 195, () => { PlayerCombatant p = Target(); if (p == null) return; p.Energy += 3; Done(p.Name + " 에너지 +3"); });

        y -= 56;
        Section("플레이어", y); y -= 34;
        Btn("골드 +100", new Vector2(-105, y), 195, () => { PlayerCombatant p = Target(); if (p == null) return; p.Gold += 100; Done(p.Name + " 골드 +100"); });
        Btn("자원 가득", new Vector2(105, y), 195, () => { PlayerCombatant p = Target(); if (p == null) return; p.Mana = p.MaxMana; Done(p.Name + " " + p.ManaName + " 가득"); });
        y -= 48;
        Btn("힘 +1", new Vector2(-140, y), 125, () => Stat(0, "힘"));
        Btn("체력 +1", new Vector2(0, y), 125, () => Stat(1, "체력"));
        Btn("민첩·마나 +1", new Vector2(140, y), 125, () => Stat(2, "민첩"));
        y -= 48;
        Btn("카드 넣기", new Vector2(-105, y), 195, ShowCardList);
        Btn("아티팩트 넣기", new Vector2(105, y), 195, ShowRelicList);

        y -= 56;
        Section("진행", y); y -= 34;
        Btn("다음 칸으로", new Vector2(-105, y), 195, () => { RunState r = GetRun(); if (r == null) return; r.DevSkipStep(); Done("다음 칸으로 (" + r.ProgressText + ")"); });
        Btn("런 다시 (HP만)", new Vector2(105, y), 195, () => { foreach (PlayerCombatant p in Players()) p.Hp = p.MaxHp; Done("HP 리셋"); });
        y -= 48;
        for (int s = 1; s <= GameBalance.MaxStage; s++)
        {
            int stage = s;
            Btn(s + "스테이지", new Vector2(-157 + (s - 1) * 105, y), 98, () => { RunState r = GetRun(); if (r == null) return; r.DevGoToStage(stage); Done(stage + "스테이지로 이동"); });
        }

        // 3-180 도감 (개발자용): 전부 보기 = 이번 실행 동안만 안 본 것도 공개 (저장 안 함) / 초기화 = 본 기록 전부 지움
        y -= 48;
        Btn("도감 전부 보기", new Vector2(-105, y), 195, () => { Discovery.RevealAll = !Discovery.RevealAll; Done("도감 전부 보기 " + (Discovery.RevealAll ? "켜짐" : "꺼짐")); });
        Btn("도감 초기화", new Vector2(105, y), 195, () => { Discovery.Clear(); DiscoveryStore.SaveIfDirty(); Done("도감 기록 초기화 (지금 덱·아티팩트는 0.5초 뒤 다시 기록됨)"); });

        y -= 60;
        Text help = UIFactory.MakeText("Help", transform, "팀원 테스트 빌드 = Build Settings 의 Development Build 체크\n유저용 빌드는 체크 해제 → 이 패널·위쪽 테스트 버튼이 사라짐", 15, UIFactory.Hex("#A0C8A8"), TextAnchor.UpperCenter);
        UIFactory.Place(help.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(410, 60));

        // 카드·아티팩트 목록 (패널 오른쪽에 뜸)
        Image list = UIFactory.MakeImage("List", transform, new Color(0.04f, 0.06f, 0.05f, 0.98f));
        listLayer = list.rectTransform;
        UIFactory.Place(listLayer, new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(10, 0), new Vector2(760, 780));
        list.gameObject.AddComponent<Outline>().effectColor = UIFactory.Hex("#5AE07A");
        listLayer.gameObject.SetActive(false);
    }

    // ---------------- 목록 ----------------
    void ShowCardList()
    {
        PlayerCombatant p = Target();
        if (p == null) return;
        List<CardData> pool = CardLibrary.RewardPool(p.Class);
        OpenList(p.Name + "에게 넣을 카드 (누르면 덱에 추가, 오른쪽 + = 강화판)");
        for (int i = 0; i < pool.Count; i++)
        {
            CardData c = pool[i];
            int col = i % 2, row = i / 2;
            Vector2 pos = new Vector2(-185 + col * 370, -60 - row * 42);
            string label = "[" + CardData.RarityName(c.Rarity) + "] " + c.Name;
            ListButton(label, pos, 300, CardView.RarityColor(c.Rarity), () => { p.Deck.Add(c.UpgradeTo != null ? MakeFresh(c) : c); Done(p.Name + " 덱에 [" + c.Name + "]"); });
            ListButton("+", pos + new Vector2(180, 0), 50, CardView.UpgradedNameColor, () => { CardData up = c.MakeUpgraded(); if (up != null) { p.Deck.Add(up); Done(p.Name + " 덱에 [" + up.Name + "]"); } });
        }
    }

    // 같은 카드를 여러 번 넣을 수 있게 매번 새로 만듦 (강화 함수의 반대: 강화 전 카드를 새로)
    static CardData MakeFresh(CardData c)
    {
        foreach (CardData fresh in CardLibrary.RewardPool(PlayerClass.BlackScythe)) if (fresh.Id == c.Id) return fresh;
        foreach (CardData fresh in CardLibrary.RewardPool(PlayerClass.CosmicPhoenix)) if (fresh.Id == c.Id) return fresh;
        foreach (CardData fresh in CardLibrary.RewardPool(PlayerClass.FrostMage)) if (fresh.Id == c.Id) return fresh;
        foreach (CardData fresh in CardLibrary.RewardPool(PlayerClass.Sniper)) if (fresh.Id == c.Id) return fresh;
        return c;
    }

    void ShowRelicList()
    {
        PlayerCombatant p = Target();
        if (p == null) return;
        OpenList(p.Name + "에게 줄 아티팩트 (이미 가진 것·다른 직업 것은 빠짐)");
        int i = 0;
        foreach (RelicData r in RelicLibrary.All)
        {
            if (r.Rarity == RelicRarity.Starter) continue;
            if (r.ClassOnly != PlayerClass.Common && r.ClassOnly != p.Class) continue;
            if (p.HasRelic(r.Id)) continue;
            int col = i % 2, row = i / 2;
            RelicData relic = r;
            ListButton("[" + r.RarityName() + "] " + r.Name, new Vector2(-185 + col * 370, -60 - row * 42), 350, RelicView.RarityColor(r.Rarity),
                () => { RunState rs = GetRun(); if (rs != null) rs.GiveRelic(p, relic); Done(p.Name + " 아티팩트 [" + relic.Name + "]"); ShowRelicList(); });
            i++;
        }
    }

    void OpenList(string header)
    {
        listLayer.gameObject.SetActive(true);
        for (int i = listLayer.childCount - 1; i >= 0; i--) Destroy(listLayer.GetChild(i).gameObject);
        Text t = UIFactory.MakeText("Header", listLayer, header, 20, UIFactory.Hex("#7AF09A"), TextAnchor.MiddleCenter);
        UIFactory.Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -14), new Vector2(740, 30));
        Button close = UIFactory.MakeButton("Close", listLayer, "닫기", UIFactory.Hex("#3A4A3A"), () => listLayer.gameObject.SetActive(false), 20);
        UIFactory.Place((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 14), new Vector2(160, 44));
    }

    void ListButton(string label, Vector2 pos, float width, Color color, System.Action onClick)
    {
        Button b = UIFactory.MakeButton("Item", listLayer, label, UIFactory.Hex("#1E2A22"), () => onClick(), 17);
        UIFactory.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), pos, new Vector2(width, 38));
        Text text = b.GetComponentInChildren<Text>();
        text.color = Color.Lerp(color, Color.white, 0.35f);
        text.alignment = width > 100 ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
        UIFactory.Stretch(text.rectTransform, 10, 6, 0, 0);
    }

    // ---------------- 작은 도우미 ----------------
    List<PlayerCombatant> Players()
    {
        RunState r = GetRun();
        return r != null ? r.Players : new List<PlayerCombatant>();
    }

    PlayerCombatant Target()
    {
        List<PlayerCombatant> list = Players();
        if (list.Count == 0) { Say("런이 없음 (게임을 먼저 시작)"); return null; }
        target = Mathf.Clamp(target, 0, list.Count - 1);
        return list[target];
    }

    void UpdateTarget()
    {
        List<PlayerCombatant> list = Players();
        if (list.Count == 0) { targetLabel.text = "대상: (런 없음)"; return; }
        target = (target % list.Count + list.Count) % list.Count;   // 끝에서 넘기면 처음으로
        PlayerCombatant p = list[target];
        targetLabel.text = "대상: " + p.Name + " " + ClassLibrary.ColoredClassName(p.Class);
    }

    void Stat(int which, string name)
    {
        PlayerCombatant p = Target();
        if (p == null) return;
        RunState.DevAddStat(p, which);
        Done(p.Name + " " + name + " +1 (힘 " + p.Str + " / 체력 " + p.Con + " / " + p.AgiName + " " + p.Agi + ")");
    }

    void Done(string message)
    {
        Say(message);
        if (AfterChange != null) AfterChange();
        transform.SetAsLastSibling();
    }

    void Say(string message)
    {
        if (Toast != null) Toast("[개발] " + message);
    }

    void Section(string name, float y)
    {
        Text t = UIFactory.MakeText("Section", transform, "— " + name + " —", 18, UIFactory.Hex("#A0C8A8"), TextAnchor.MiddleCenter);
        UIFactory.Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(400, 26));
    }

    void Btn(string label, Vector2 pos, float width, System.Action onClick)
    {
        Button b = UIFactory.MakeButton(label, transform, label, UIFactory.Hex("#24402C"), () => onClick(), 18);
        UIFactory.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), pos, new Vector2(width, 40));
    }
}
