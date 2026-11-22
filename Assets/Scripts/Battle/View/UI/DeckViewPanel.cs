// ============================================================
// DeckViewPanel.cs
// 카드 목록 보기 (슬더스2 의 "덱 보기")
//
//   위쪽 [덱] 버튼  → 내 덱 전체 (멀티면 P1 / P2 탭)
//   전투 중 왼쪽 아래 뽑기 더미 / 오른쪽 아래 버림 더미를 누르면 → 그 더미의 카드
//   한 쪽에 21장 (7장 × 3줄), 넘치면 ◀ ▶.  정렬: 종류(공격·스킬·파워) → 등급 → 이름
//   카드에 마우스를 올리면 살짝 커짐 (누르는 기능은 없음, 보기만)
//
// 뽑기 더미는 순서를 섞어서 보여 줌 (다음에 뭐가 나올지 알 수 없게, 슬더스와 같음)
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DeckViewPanel : MonoBehaviour
{
    private const int PerRow = 7;
    private const int Rows = 3;
    private const float Scale = 0.7f;

    private Text title;
    private Text countText;
    private Text sideText;     // 덱 모드 왼쪽: 그 사람의 HP·골드·레벨·아티팩트·포션 (2026-11-15: 동료 것도 볼 수 있게)
    private RectTransform tabLayer;
    private RectTransform grid;
    private RectTransform pager;

    // 지금 보여 주는 것
    private List<PlayerCombatant> players;   // 덱 모드일 때 (탭)
    private int current;
    private List<CardData> cards;            // 더미 모드일 때 (뽑기·버림)
    private string pileTitle;
    private string manaName = "마나";
    private int page;

    public static DeckViewPanel Create(Transform parent)
    {
        RectTransform rt = UIFactory.MakeRect("DeckView", parent);
        UIFactory.Stretch(rt, 0, 0, 64, 0);   // 위쪽 버튼 줄은 가리지 않음
        DeckViewPanel panel = rt.gameObject.AddComponent<DeckViewPanel>();
        panel.BuildStatic();
        rt.gameObject.SetActive(false);
        return panel;
    }

    void BuildStatic()
    {
        Image dim = UIFactory.MakeImage("Dim", transform, new Color(0.03f, 0.025f, 0.05f, 0.985f), false);
        UIFactory.Stretch(dim.rectTransform);

        title = UIFactory.MakeText("Title", transform, "", 46, UIFactory.Hex("#FFD27A"), TextAnchor.MiddleCenter);
        UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -16), new Vector2(1000, 60));
        countText = UIFactory.MakeText("Count", transform, "", 24, UIFactory.Hex("#C8C0D8"), TextAnchor.MiddleCenter);
        UIFactory.Place(countText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -74), new Vector2(1400, 34));

        sideText = UIFactory.MakeText("Side", transform, "", 20, UIFactory.Hex("#E8E0F0"), TextAnchor.UpperLeft);
        UIFactory.Place(sideText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24, -20), new Vector2(280, 900));
        sideText.supportRichText = true;
        sideText.raycastTarget = false;

        tabLayer = UIFactory.MakeRect("Tabs", transform);
        UIFactory.Place(tabLayer, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -120), new Vector2(1200, 56));

        grid = UIFactory.MakeRect("Grid", transform);
        UIFactory.Place(grid, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), Vector2.zero);

        pager = UIFactory.MakeRect("Pager", transform);
        UIFactory.Place(pager, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 50), Vector2.zero);

        Button close = UIFactory.MakeButton("Close", transform, "닫기", UIFactory.Hex("#4A3A5A"), Hide, 28);
        UIFactory.Place((RectTransform)close.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-50, 30), new Vector2(220, 70));
        UIFactory.SkinButton(close, UIFactory.GothicButton);   // 3-181: 8번 공용 버튼 틀 (그림 없던 버튼)
    }

    public bool IsOpen { get { return gameObject.activeSelf; } }

    // 덱 모드: 플레이어들의 덱 (탭으로 전환). first = 처음 보여 줄 플레이어 번호
    public void ShowDecks(List<PlayerCombatant> list, int first)
    {
        players = list;
        cards = null;
        current = Mathf.Clamp(first, 0, list.Count - 1);
        page = 0;
        Open();
    }

    // 더미 모드: 뽑기 더미 / 버림 더미 / 소멸 더미
    public void ShowPile(string pileName, List<CardData> pile, string resourceName, bool shuffle)
    {
        players = null;
        cards = new List<CardData>(pile);
        if (shuffle)
        {
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                CardData t = cards[i]; cards[i] = cards[j]; cards[j] = t;
            }
        }
        pileTitle = pileName;
        manaName = resourceName;
        page = 0;
        Open();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    void Open()
    {
        SoundManager.Play("PageFlip");   // 3-252: 책장 넘기는 소리
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Redraw();
    }

    void Redraw()
    {
        for (int i = grid.childCount - 1; i >= 0; i--) Destroy(grid.GetChild(i).gameObject);
        for (int i = pager.childCount - 1; i >= 0; i--) Destroy(pager.GetChild(i).gameObject);
        for (int i = tabLayer.childCount - 1; i >= 0; i--) Destroy(tabLayer.GetChild(i).gameObject);

        List<CardData> list;
        if (players != null)
        {
            PlayerCombatant p = players[current];
            manaName = p.ManaName;
            list = Sorted(p.Deck);
            title.text = "덱 — " + p.Name + " " + ClassLibrary.ColoredClassName(p.Class);
            countText.text = Summary(list);
            sideText.text = SideInfo(p);
            BuildTabs();
        }
        else
        {
            sideText.text = "";
            list = cards;
            title.text = pileTitle;
            countText.text = list.Count + "장" + (list.Count > 0 ? "" : " (비어 있음)");
        }

        int perPage = PerRow * Rows;
        int pages = Mathf.Max(1, Mathf.CeilToInt(list.Count / (float)perPage));
        page = Mathf.Clamp(page, 0, pages - 1);
        float w = (CardView.Width + 34) * Scale, h = (CardView.Height + 36) * Scale;
        int start = page * perPage;
        int onPage = Mathf.Min(perPage, list.Count - start);
        for (int k = 0; k < onPage; k++)
        {
            int col = k % PerRow, row = k / PerRow;
            int inRow = Mathf.Min(PerRow, onPage - row * PerRow);
            Vector2 pos = new Vector2((col - (inRow - 1) / 2f) * w, (1 - row) * h);
            CardView v = CardView.CreateDisplay(grid, list[start + k], manaName, pos, Scale, null);
            // 보기 전용이지만 마우스를 올리면 커지게
            float s = Scale;
            HoverRelay hover = v.gameObject.AddComponent<HoverRelay>();
            hover.OnChange = (on) => { v.TargetScale = on ? s * 1.35f : s; if (on) v.transform.SetAsLastSibling(); };
        }

        if (pages > 1)
        {
            PageButton("◀", new Vector2(-170, 0), page > 0, () => { page--; Redraw(); });
            Text t = UIFactory.MakeText("Page", pager, (page + 1) + " / " + pages, 26, Color.white, TextAnchor.MiddleCenter);
            UIFactory.Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(160, 40));
            PageButton("▶", new Vector2(170, 0), page < pages - 1, () => { page++; Redraw(); });
        }
    }

    // 왼쪽 정보: 동료 탭을 누르면 동료의 아티팩트·포션도 보임
    static string SideInfo(PlayerCombatant p)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append("<b><size=24>").Append(p.Name).Append(" ").Append(ClassLibrary.ColoredClassName(p.Class)).Append("</size></b>\n");
        sb.Append("<color=#FF8A8A>HP ").Append(p.Hp).Append(" / ").Append(p.MaxHp).Append("</color>   <color=#FFD27A>골드 ").Append(p.Gold).Append("</color>\n");
        sb.Append("<color=#9CE0FF>Lv ").Append(p.Level).Append("</color>   힘 ").Append(p.Str).Append("  체력 ").Append(p.Con).Append("  ").Append(p.AgiName).Append(" ").Append(p.Agi).Append("\n\n");
        sb.Append("<b><color=#F0C850>아티팩트 ").Append(p.Relics.Count).Append("</color></b>\n");
        if (p.Relics.Count == 0) sb.Append("  (없음)\n");
        foreach (RelicData r in p.Relics) sb.Append("· ").Append(r.Name).Append(" <size=16><color=#B8B0C8>").Append(r.Description).Append("</color></size>\n");
        sb.Append("\n<b><color=#F0C850>포션 ").Append(p.Potions.Count).Append(" / ").Append(GameBalance.PotionSlots).Append("</color></b>\n");
        foreach (string id in p.Potions)
        {
            PotionData pd = PotionLibrary.Get(id);
            if (pd != null) sb.Append("· ").Append(pd.Name).Append("\n");
        }
        return sb.ToString();
    }

    void BuildTabs()
    {
        if (players == null || players.Count < 2) return;
        int n = players.Count;
        for (int i = 0; i < n; i++)
        {
            int index = i;
            bool on = i == current;
            Button b = UIFactory.MakeButton("Tab" + i, tabLayer, players[i].Name + " " + ClassLibrary.ColoredClassName(players[i].Class) + " (" + players[i].Deck.Count + ")",
                on ? UIFactory.Hex("#5A4A8A") : UIFactory.Hex("#2C2840"), () => { current = index; page = 0; Redraw(); }, 22);
            UIFactory.Place((RectTransform)b.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - (n - 1) / 2f) * 320, 0), new Vector2(300, 50));
            if (on) b.gameObject.AddComponent<Outline>().effectColor = UIFactory.Hex("#FFD27A");
        }
    }

    void PageButton(string label, Vector2 pos, bool enabled, System.Action onClick)
    {
        Button b = UIFactory.MakeButton(label, pager, label, UIFactory.Hex("#3A3446"), () => onClick(), 28);
        b.interactable = enabled;
        UIFactory.Place((RectTransform)b.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(90, 56));
    }

    // 덱 요약: "12장  ·  공격 6 / 스킬 5 / 파워 1  ·  강화 2"
    static string Summary(List<CardData> list)
    {
        int atk = 0, skill = 0, power = 0, up = 0;
        foreach (CardData c in list)
        {
            if (c.Type == CardType.Attack) atk++;
            else if (c.Type == CardType.Skill) skill++;
            else power++;
            if (c.Upgraded) up++;
        }
        return list.Count + "장   ·   공격 " + atk + " / 스킬 " + skill + " / 파워 " + power + (up > 0 ? "   ·   강화 " + up : "");
    }

    // 정렬: 종류 → 등급 → 이름 (덱 순서는 그대로 두고 복사본을 정렬)
    static List<CardData> Sorted(List<CardData> deck)
    {
        List<CardData> list = new List<CardData>(deck);
        list.Sort((a, b) =>
        {
            int c = a.Type.CompareTo(b.Type);
            if (c != 0) return c;
            c = a.Rarity.CompareTo(b.Rarity);
            if (c != 0) return c;
            return string.CompareOrdinal(a.Name, b.Name);
        });
        return list;
    }
}
