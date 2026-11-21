// ============================================================
// CompendiumPanel.cs  (3-180 도감)
// 게임에 있는 카드·아티팩트를 미리 보는 곳 (슬더스2 의 "도감")
//
//   [도감]  → 처음 화면: [카드] / [아티팩트] 중 고르기 (각각 "발견 12 / 136")
//   [카드]  → 위쪽 탭: 공용 / 카인 / 이그니스 / 제피르 / 실비아 / 방해 카드
//             한 쪽에 21장 (7 × 3), 넘치면 ◀ ▶.  [강화 보기] 를 켜면 전부 + 버전으로
//   [아티팩트] → 위쪽 탭: 시작 / 일반·특별 / 희귀 / 영웅 / 보스 / 공용
//             동그라미에 마우스를 올리면 오른쪽에 효과·페널티·나오는 곳
//
// 덱 보기(DeckViewPanel)와 다른 점: 덱은 "지금 내 카드", 도감은 "게임에 있는 전부"
// B 방식: 한 번이라도 본 것만 공개 (Discovery). 안 본 카드는 "?" 뒷면, 안 본 아티팩트는 "???"
//   본 기록은 DiscoveryStore 가 컴퓨터에 저장 → 게임을 껐다 켜도 남음
//
// 들어가는 곳: 타이틀 메뉴 [도감], 게임 중 위쪽 줄 [도감] 버튼
// Esc: 카드·아티팩트 화면 → 처음 화면 → 닫기 (BattleScreen.HandleKeys 가 Back() 을 부름)
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CompendiumPanel : MonoBehaviour
{
    // 카드 격자 (덱 보기와 같은 크기)
    private const int PerRow = 7;
    private const int Rows = 3;
    private const float CardScale = 0.7f;

    // 아티팩트 격자
    private const int RelicPerRow = 9;
    private const float RelicSize = 92f;
    private const float RelicGapX = 140f;
    private const float RelicGapY = 158f;

    private enum Mode { Home, Cards, Relics }
    private Mode mode;

    // 공통
    private Text title;
    private Text countText;
    private Button backButton;

    // 처음 화면
    private RectTransform home;
    private Text homeCardCount, homeRelicCount;

    // 카드 화면
    private RectTransform cardPage, cardTabs, cardGrid, cardPager;
    private Text upgradeLabel;
    private Image upgradeImage;                        // 3-194: 강화 보기 버튼 그림 (꺼짐 = 청록 판 / 켜짐 = 금빛 판)
    private Sprite toggleOffArt, toggleOnArt;
    private int cardTab;
    private int page;
    private bool showUpgraded;

    // 아티팩트 화면
    private RectTransform relicPage, relicTabs, relicGrid;
    private Text detailText;
    private int relicTab;

    // 카드 탭: 이름(색 포함) / 자원 이름(카드 비용 표시용) / 목록 만드는 함수
    //   System.Func<List<CardData>> = 자바의 Supplier<List<CardData>> (부를 때마다 새 목록)
    private struct CardTab
    {
        public string Label;
        public string ManaName;
        public System.Func<List<CardData>> Make;
        public CardTab(string label, string mana, System.Func<List<CardData>> make) { Label = label; ManaName = mana; Make = make; }
    }
    private static readonly CardTab[] CardTabs =
    {
        new CardTab("공용", "마나", CardLibrary.CommonPoolCards),
        new CardTab(ClassLibrary.ColoredClassName(PlayerClass.BlackScythe), "영혼", () => Discovery.ClassCards(PlayerClass.BlackScythe)),
        new CardTab(ClassLibrary.ColoredClassName(PlayerClass.CosmicPhoenix), "홍염", () => Discovery.ClassCards(PlayerClass.CosmicPhoenix)),
        new CardTab(ClassLibrary.ColoredClassName(PlayerClass.FrostMage), "마나", () => Discovery.ClassCards(PlayerClass.FrostMage)),
        new CardTab(ClassLibrary.ColoredClassName(PlayerClass.Sniper), "탄환", () => Discovery.ClassCards(PlayerClass.Sniper)),
        new CardTab("방해 카드", "마나", Discovery.StatusCards),
    };

    // 아티팩트 탭: 이름 / 그 탭에 들어가는 등급들
    private static readonly string[] RelicTabNames = { "시작", "일반 · 특별", "희귀", "영웅", "보스", "공용" };
    private static readonly RelicRarity[][] RelicTabRarities =
    {
        new[] { RelicRarity.Starter },
        new[] { RelicRarity.Common, RelicRarity.Special },
        new[] { RelicRarity.Rare },
        new[] { RelicRarity.Hero },
        new[] { RelicRarity.Boss },
        new[] { RelicRarity.Party },
    };

    // 3-189 그림 (없으면 예전 도형)
    private Sprite homeArt, unknownCardArt, unknownRelicArt, detailArt;

    public bool IsOpen { get { return gameObject.activeSelf; } }

    public static CompendiumPanel Create(Transform parent)
    {
        RectTransform rt = UIFactory.MakeRect("Compendium", parent);
        UIFactory.Stretch(rt);   // 화면 전체 (타이틀 위에서도 열림)
        CompendiumPanel panel = rt.gameObject.AddComponent<CompendiumPanel>();
        panel.BuildStatic();
        rt.gameObject.SetActive(false);
        return panel;
    }

    // ==================== 바뀌지 않는 부분 ====================
    void BuildStatic()
    {
        Image dim = UIFactory.MakeImage("Dim", transform, new Color(0.03f, 0.025f, 0.05f, 0.985f), false);
        UIFactory.Stretch(dim.rectTransform);   // 뒤 화면 클릭도 막음
        // 3-189: 도감 배경 그림 (보랏빛 성문·계단) + 위에 어두운 막 (글자가 잘 보이게)
        Sprite bgArt = Resources.Load<Sprite>("Backgrounds/Compendium_BG");
        if (bgArt != null)
        {
            dim.sprite = bgArt;
            dim.color = Color.white;
            dim.preserveAspect = false;
            Image shade = UIFactory.MakeImage("Shade", transform, new Color(0.02f, 0.01f, 0.04f, 0.45f), false);
            UIFactory.Stretch(shade.rectTransform);
            shade.raycastTarget = false;
        }
        homeArt = Resources.Load<Sprite>("UI/Compendium/Comp_HomeButton");
        unknownCardArt = Resources.Load<Sprite>("UI/Compendium/Comp_UnknownCard");
        unknownRelicArt = Resources.Load<Sprite>("UI/Compendium/Comp_UnknownRelic");
        detailArt = Resources.Load<Sprite>("UI/Compendium/Comp_DetailBox");

        title = UIFactory.MakeText("Title", transform, "도감", 50, UIFactory.Hex("#FFD27A"), TextAnchor.MiddleCenter);
        UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -20), new Vector2(1000, 64));
        title.font = UIFactory.TitleFont;
        countText = UIFactory.MakeText("Count", transform, "", 24, UIFactory.Hex("#C8C0D8"), TextAnchor.MiddleCenter);
        UIFactory.Place(countText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -84), new Vector2(1400, 34));

        BuildHome();
        BuildCardPage();
        BuildRelicPage();

        // 오른쪽 아래: 처음 화면에서는 [닫기], 안쪽 화면에서는 [뒤로]
        backButton = UIFactory.MakeButton("Back", transform, "닫기", UIFactory.Hex("#4A3A5A"), Back, 28);
        UIFactory.Place((RectTransform)backButton.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-50, 30), new Vector2(220, 70));
        UIFactory.SkinButton(backButton, UIFactory.GothicButton);   // 3-181: 8번 공용 버튼 틀 (그림 없던 버튼)
    }

    void BuildHome()
    {
        home = UIFactory.MakeRect("Home", transform);
        UIFactory.Stretch(home);
        homeCardCount = HomeButton("카드", new Vector2(-300, -20), () => ShowCards());
        homeRelicCount = HomeButton("아티팩트", new Vector2(300, -20), () => ShowRelics());
        Text hint = UIFactory.MakeText("Hint", home, "한 번이라도 본 카드·아티팩트만 보입니다. 아직 못 본 것은 ? 로 가려져 있어요.", 22, new Color(1f, 1f, 1f, 0.55f), TextAnchor.MiddleCenter);
        UIFactory.Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -260), new Vector2(1400, 40));
    }

    // 처음 화면의 큰 버튼 하나. 아래 줄 "발견 N / M" 글자를 돌려줌
    Text HomeButton(string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        Button b = UIFactory.MakeButton("Home_" + label, home, label, UIFactory.Hex("#3A2C5A"), onClick, 56);
        RectTransform rt = (RectTransform)b.transform;
        UIFactory.Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(460, 300));
        if (homeArt != null)
        {
            Image bi = b.GetComponent<Image>();
            bi.sprite = homeArt;
            bi.type = Image.Type.Simple;
            bi.color = Color.white;
            rt.sizeDelta = new Vector2(468, 306);   // 그림 비율 그대로
        }
        else
        {
            Outline o = b.gameObject.AddComponent<Outline>();
            o.effectColor = UIFactory.Hex("#C8A05A");
            o.effectDistance = new Vector2(2, -2);
        }
        Text main = b.GetComponentInChildren<Text>();
        main.rectTransform.offsetMin = new Vector2(0, 40);   // 큰 글자는 조금 위로
        Text count = UIFactory.MakeText("Count", rt, "", 26, UIFactory.Hex("#E8D8A8"), TextAnchor.MiddleCenter);
        UIFactory.Place(count.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(420, 40));
        count.raycastTarget = false;
        return count;
    }

    void BuildCardPage()
    {
        cardPage = UIFactory.MakeRect("CardPage", transform);
        UIFactory.Stretch(cardPage);
        cardTabs = UIFactory.MakeRect("Tabs", cardPage);
        UIFactory.Place(cardTabs, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -136), new Vector2(1400, 56));
        cardGrid = UIFactory.MakeRect("Grid", cardPage);
        UIFactory.Place(cardGrid, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -60), Vector2.zero);
        cardPager = UIFactory.MakeRect("Pager", cardPage);
        UIFactory.Place(cardPager, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 50), Vector2.zero);

        // [강화 보기] 켜짐/꺼짐 (왼쪽 아래)
        Button up = UIFactory.MakeButton("Upgrade", cardPage, "", UIFactory.Hex("#2C3A4A"), () => { showUpgraded = !showUpgraded; RedrawCards(); }, 24);
        UIFactory.Place((RectTransform)up.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(50, 30), new Vector2(260, 70));
        upgradeLabel = up.GetComponentInChildren<Text>();
        // 3-194: 강화 보기 그림 버튼 (UI/Compendium/Comp_Toggle_Off · On). 그림 비율 약 2.5 : 1
        toggleOffArt = Resources.Load<Sprite>("UI/Compendium/Comp_Toggle_Off");
        toggleOnArt = Resources.Load<Sprite>("UI/Compendium/Comp_Toggle_On");
        upgradeImage = up.GetComponent<Image>();
        if (toggleOffArt != null)
        {
            upgradeImage.type = Image.Type.Simple;
            upgradeImage.color = Color.white;
            ((RectTransform)up.transform).sizeDelta = new Vector2(290, 114);
            upgradeLabel.color = UIFactory.Hex("#F0E6D2");
            Outline lo = upgradeLabel.gameObject.AddComponent<Outline>();
            lo.effectColor = new Color(0, 0, 0, 0.85f);
            lo.effectDistance = new Vector2(1.5f, -1.5f);
        }
        cardPage.gameObject.SetActive(false);
    }

    void BuildRelicPage()
    {
        relicPage = UIFactory.MakeRect("RelicPage", transform);
        UIFactory.Stretch(relicPage);
        relicTabs = UIFactory.MakeRect("Tabs", relicPage);
        UIFactory.Place(relicTabs, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -136), new Vector2(1400, 56));
        relicGrid = UIFactory.MakeRect("Grid", relicPage);
        UIFactory.Place(relicGrid, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-230, 0), Vector2.zero);

        // 오른쪽 설명 상자 (마우스를 올린 아티팩트)
        Image box = UIFactory.MakeImage("Detail", relicPage, new Color(0.1f, 0.08f, 0.14f, 0.95f));
        UIFactory.Place(box.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-40, 0), new Vector2(440, 512));   // 3-189: 그림 비율
        box.raycastTarget = false;
        if (detailArt != null)
        {
            box.sprite = detailArt;   // 3-189: 사슬 달린 쇠틀 상자
            box.type = Image.Type.Simple;
            box.color = Color.white;
        }
        else box.gameObject.AddComponent<Outline>().effectColor = UIFactory.Hex("#6A5A8A");
        detailText = UIFactory.MakeText("Text", box.transform, "", 22, UIFactory.Hex("#E8E0F0"), TextAnchor.UpperLeft);
        UIFactory.Stretch(detailText.rectTransform, 40, 40, 44, 44);   // 3-189: 틀 두께만큼 안쪽
        detailText.supportRichText = true;
        detailText.raycastTarget = false;
        relicPage.gameObject.SetActive(false);
    }

    // ==================== 열기 · 닫기 ====================
    public void Open()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();   // 모든 화면 위에
        ShowHome();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    // [뒤로] / Esc: 안쪽 화면이면 처음 화면으로, 처음 화면이면 닫기
    public void Back()
    {
        if (mode == Mode.Home) Hide();
        else ShowHome();
    }

    void SetMode(Mode m)
    {
        mode = m;
        home.gameObject.SetActive(m == Mode.Home);
        cardPage.gameObject.SetActive(m == Mode.Cards);
        relicPage.gameObject.SetActive(m == Mode.Relics);
        backButton.GetComponentInChildren<Text>().text = m == Mode.Home ? "닫기" : "뒤로";
    }

    void ShowHome()
    {
        SetMode(Mode.Home);
        title.text = "도감";
        countText.text = "";
        int seen, total;
        CountCards(out seen, out total);
        homeCardCount.text = "발견 " + seen + " / " + total;
        CountRelics(out seen, out total);
        homeRelicCount.text = "발견 " + seen + " / " + total;
    }

    void ShowCards()
    {
        SetMode(Mode.Cards);
        page = 0;
        RedrawCards();
    }

    void ShowRelics()
    {
        SetMode(Mode.Relics);
        RedrawRelics();
    }

    // ==================== 카드 ====================
    void RedrawCards()
    {
        Clear(cardGrid);
        Clear(cardPager);
        Clear(cardTabs);

        CardTab tab = CardTabs[cardTab];
        List<CardData> list = Sorted(tab.Make());
        int seen = 0;
        foreach (CardData c in list) if (Discovery.CardSeen(c.Id)) seen++;
        title.text = "도감 — 카드";
        countText.text = tab.Label + "   발견 " + seen + " / " + list.Count + (showUpgraded ? "   ·   강화된 모습" : "");
        upgradeLabel.text = "강화 보기: " + (showUpgraded ? "켜짐" : "꺼짐");
        if (toggleOffArt != null) upgradeImage.sprite = (showUpgraded && toggleOnArt != null) ? toggleOnArt : toggleOffArt;   // 3-194

        // 탭 (지금 탭은 밝은 색 + 금테)
        int n = CardTabs.Length;
        for (int i = 0; i < n; i++)
        {
            int index = i;
            bool on = i == cardTab;
            Button b = UIFactory.MakeButton("Tab" + i, cardTabs, CardTabs[i].Label, on ? UIFactory.Hex("#5A4A8A") : UIFactory.Hex("#2C2840"),
                () => { cardTab = index; page = 0; RedrawCards(); }, 24);
            UIFactory.Place((RectTransform)b.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - (n - 1) / 2f) * 210, 0), new Vector2(196, 50));
            if (on) b.gameObject.AddComponent<Outline>().effectColor = UIFactory.Hex("#FFD27A");
        }

        // 카드 격자 (덱 보기와 같은 배치)
        int perPage = PerRow * Rows;
        int pages = Mathf.Max(1, Mathf.CeilToInt(list.Count / (float)perPage));
        page = Mathf.Clamp(page, 0, pages - 1);
        float w = (CardView.Width + 34) * CardScale, h = (CardView.Height + 36) * CardScale;
        int start = page * perPage;
        int onPage = Mathf.Min(perPage, list.Count - start);
        for (int k = 0; k < onPage; k++)
        {
            int col = k % PerRow, row = k / PerRow;
            int inRow = Mathf.Min(PerRow, onPage - row * PerRow);
            Vector2 pos = new Vector2((col - (inRow - 1) / 2f) * w, (1 - row) * h);
            CardData card = list[start + k];
            if (!Discovery.CardSeen(card.Id))
            {
                MakeUnknownCard(pos);
                continue;
            }
            if (showUpgraded && !card.Upgraded)
            {
                CardData up = card.MakeUpgraded();
                if (up != null) card = up;   // 강화판이 없는 카드(방해 카드 등)는 그대로
            }
            CardView v = CardView.CreateDisplay(cardGrid, card, tab.ManaName, pos, CardScale, null);
            float s = CardScale;
            HoverRelay hover = v.gameObject.AddComponent<HoverRelay>();
            hover.OnChange = (on) => { v.TargetScale = on ? s * 1.35f : s; if (on) v.transform.SetAsLastSibling(); };
        }

        if (pages > 1)
        {
            PageButton("◀", new Vector2(-170, 0), page > 0, () => { page--; RedrawCards(); });
            Text t = UIFactory.MakeText("Page", cardPager, (page + 1) + " / " + pages, 26, Color.white, TextAnchor.MiddleCenter);
            UIFactory.Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(160, 40));
            PageButton("▶", new Vector2(170, 0), page < pages - 1, () => { page++; RedrawCards(); });
        }
    }

    // 아직 못 본 카드: 카드 크기의 어두운 뒷면 + 큰 "?"
    void MakeUnknownCard(Vector2 pos)
    {
        Image back = UIFactory.MakeImage("Unknown", cardGrid, UIFactory.Hex("#1C1828"));
        UIFactory.Place(back.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(CardView.Width, CardView.Height) * CardScale);
        if (unknownCardArt != null)
        {
            // 3-189: 그림 뒷면 (? 가 그려져 있음) + 아래 글자만
            back.sprite = unknownCardArt;
            back.type = Image.Type.Simple;
            back.color = Color.white;
            back.raycastTarget = false;
            Text s2 = UIFactory.MakeText("Sub", back.transform, "아직 못 본 카드", 17, UIFactory.Hex("#C8B8A0"), TextAnchor.MiddleCenter);
            UIFactory.Place(s2.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(150, 28));
            s2.raycastTarget = false;
            return;
        }
        Outline o = back.gameObject.AddComponent<Outline>();
        o.effectColor = UIFactory.Hex("#4A3E66");
        o.effectDistance = new Vector2(2, -2);
        back.raycastTarget = false;
        Text q = UIFactory.MakeText("Q", back.transform, "?", 90, UIFactory.Hex("#5A4E7A"), TextAnchor.MiddleCenter);
        UIFactory.Stretch(q.rectTransform, 0, 0, 0, 30);
        q.fontStyle = FontStyle.Bold;
        q.raycastTarget = false;
        Text s = UIFactory.MakeText("Sub", back.transform, "아직 못 본 카드", 18, UIFactory.Hex("#7A6E96"), TextAnchor.MiddleCenter);
        UIFactory.Place(s.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 24), new Vector2(150, 30));
        s.raycastTarget = false;
    }

    void PageButton(string label, Vector2 pos, bool enabled, System.Action onClick)
    {
        Button b = UIFactory.MakeButton(label, cardPager, label, UIFactory.Hex("#3A3446"), () => onClick(), 28);
        b.interactable = enabled;
        UIFactory.Place((RectTransform)b.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(90, 56));
    }

    // 정렬: 등급(기본 → 일반 → 희귀 → 영웅) → 종류(공격·스킬·파워) → 이름
    static List<CardData> Sorted(List<CardData> src)
    {
        List<CardData> list = new List<CardData>(src);
        list.Sort((a, b) =>
        {
            int c = a.Rarity.CompareTo(b.Rarity);
            if (c != 0) return c;
            c = a.Type.CompareTo(b.Type);
            if (c != 0) return c;
            return string.CompareOrdinal(a.Name, b.Name);
        });
        return list;
    }

    // ==================== 아티팩트 ====================
    void RedrawRelics()
    {
        Clear(relicGrid);
        Clear(relicTabs);

        List<RelicData> list = RelicsOfTab(relicTab);
        int seen = 0;
        foreach (RelicData r in list) if (Discovery.RelicSeen(r.Id)) seen++;
        title.text = "도감 — 아티팩트";
        countText.text = RelicTabNames[relicTab] + "   발견 " + seen + " / " + list.Count;
        detailText.text = "<color=#9A90B0>아티팩트에 마우스를 올리면\n여기에 설명이 나옵니다.</color>";

        int n = RelicTabNames.Length;
        for (int i = 0; i < n; i++)
        {
            int index = i;
            bool on = i == relicTab;
            Button b = UIFactory.MakeButton("Tab" + i, relicTabs, RelicTabNames[i], on ? UIFactory.Hex("#5A4A8A") : UIFactory.Hex("#2C2840"),
                () => { relicTab = index; RedrawRelics(); }, 24);
            UIFactory.Place((RectTransform)b.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - (n - 1) / 2f) * 210, 0), new Vector2(196, 50));
            if (on) b.gameObject.AddComponent<Outline>().effectColor = UIFactory.Hex("#FFD27A");
        }

        // 격자: 한 줄 9개, 위에서부터 (보스 35개 = 4줄이 최대)
        int rows = Mathf.Max(1, Mathf.CeilToInt(list.Count / (float)RelicPerRow));
        float top = (rows - 1) / 2f * RelicGapY;   // 줄 수에 맞춰 가운데 정렬
        for (int k = 0; k < list.Count; k++)
        {
            int col = k % RelicPerRow, row = k / RelicPerRow;
            int inRow = Mathf.Min(RelicPerRow, list.Count - row * RelicPerRow);
            Vector2 pos = new Vector2((col - (inRow - 1) / 2f) * RelicGapX, top - row * RelicGapY + 14f);
            MakeRelicCell(list[k], pos);
        }
    }

    // 아티팩트 한 칸: 동그라미 + 아래 이름. 못 본 것은 어두운 동그라미 "?" + "???"
    void MakeRelicCell(RelicData r, Vector2 pos)
    {
        bool known = Discovery.RelicSeen(r.Id);
        Image icon;
        if (known)
        {
            icon = RelicView.MakeIcon(relicGrid, r, RelicSize, null);
        }
        else
        {
            icon = UIFactory.MakeCircle("Unknown_" + r.Id, relicGrid, UIFactory.Hex("#2A2438"));
            icon.rectTransform.sizeDelta = new Vector2(RelicSize, RelicSize);
            if (unknownRelicArt != null)
            {
                icon.sprite = unknownRelicArt;   // 3-189: ? 가 그려진 쇠 메달
                icon.color = Color.white;
                icon.rectTransform.sizeDelta = new Vector2(RelicSize, RelicSize) * 1.1f;
            }
            else
            {
                Text q = UIFactory.MakeText("Q", icon.transform, "?", 46, UIFactory.Hex("#6A5E8A"), TextAnchor.MiddleCenter);
                UIFactory.Stretch(q.rectTransform);
                q.fontStyle = FontStyle.Bold;
                q.raycastTarget = false;
            }
            icon.raycastTarget = true;
        }
        RectTransform rt = icon.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(RelicSize, RelicSize);   // 기준점을 바꾼 뒤 크기를 다시 정함
        rt.anchoredPosition = pos;

        // 이름 (직업 전용이면 그 직업 색)
        string nameColor = r.ClassOnly != PlayerClass.Common ? ClassLibrary.ClassColorHex(r.ClassOnly) : "#E8E0F0";
        Text name = UIFactory.MakeText("Name", relicGrid, known ? r.Name : "???", 16, UIFactory.Hex(known ? nameColor : "#6A5E8A"), TextAnchor.UpperCenter);
        UIFactory.Place(name.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), pos + new Vector2(0, -RelicSize / 2f - 4f), new Vector2(RelicGapX - 6f, 44));
        name.raycastTarget = false;

        // 마우스를 올리면: 살짝 커짐 + 오른쪽 설명
        HoverRelay hover = icon.gameObject.AddComponent<HoverRelay>();
        hover.OnChange = (on) =>
        {
            rt.localScale = on ? new Vector3(1.15f, 1.15f, 1f) : Vector3.one;
            if (on) detailText.text = Detail(r, known);
        };
    }

    // 오른쪽 설명 글
    static string Detail(RelicData r, bool known)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        string who = r.ClassOnly != PlayerClass.Common ? ClassLibrary.ColoredClassName(r.ClassOnly) + " 전용" : "모든 캐릭터";
        string rarityHex = "#" + ColorUtility.ToHtmlStringRGB(RelicView.RarityColor(r.Rarity));
        if (known)
        {
            sb.Append("<b><size=28>").Append(r.Name).Append("</size></b>\n");
            sb.Append("<color=").Append(rarityHex).Append(">[").Append(r.RarityName()).Append("]</color>   ").Append(who).Append("\n\n");
            sb.Append(r.Description).Append("\n");
            if (r.HasPenalty) sb.Append("\n<color=#FF7A7A>페널티: ").Append(r.Penalty).Append("</color>\n");
            if (r.Rarity == RelicRarity.Boss)
                sb.Append("\n<color=#9CE0FF>").Append(r.GivesEnergy ? "매 턴 에너지 +1" : "에너지 +1 없음 (보스 승격 아티팩트)").Append("</color>\n");
        }
        else
        {
            sb.Append("<b><size=28>???</size></b>\n");
            sb.Append("<color=").Append(rarityHex).Append(">[").Append(r.RarityName()).Append("]</color>   ").Append(who).Append("\n\n");
            sb.Append("<color=#9A90B0>아직 발견하지 못한 아티팩트입니다.</color>\n");
        }
        sb.Append("\n<color=#F0C850>나오는 곳</color>\n").Append(Discovery.RelicSource(r));
        if (r.MultiOnly && r.Rarity != RelicRarity.Party) sb.Append(" (멀티 전용)");
        return sb.ToString();
    }

    static List<RelicData> RelicsOfTab(int tab)
    {
        List<RelicData> list = new List<RelicData>();
        foreach (RelicData r in RelicLibrary.All)
            if (System.Array.IndexOf(RelicTabRarities[tab], r.Rarity) >= 0) list.Add(r);
        // 공용 먼저, 그다음 직업 순서 (카인 → 이그니스 → 제피르 → 실비아). 같은 직업 안에서는 목록 순서 그대로
        List<RelicData> sorted = new List<RelicData>();
        PlayerClass[] order = { PlayerClass.Common, PlayerClass.BlackScythe, PlayerClass.CosmicPhoenix, PlayerClass.FrostMage, PlayerClass.Sniper };
        foreach (PlayerClass pc in order)
            foreach (RelicData r in list) if (r.ClassOnly == pc) sorted.Add(r);
        return sorted;
    }

    // ==================== 개수 세기 ====================
    static void CountCards(out int seen, out int total)
    {
        seen = 0;
        total = 0;
        foreach (CardTab tab in CardTabs)
            foreach (CardData c in tab.Make())
            {
                total++;
                if (Discovery.CardSeen(c.Id)) seen++;
            }
    }

    static void CountRelics(out int seen, out int total)
    {
        seen = 0;
        total = RelicLibrary.All.Count;
        foreach (RelicData r in RelicLibrary.All) if (Discovery.RelicSeen(r.Id)) seen++;
    }

    static void Clear(RectTransform layer)
    {
        for (int i = layer.childCount - 1; i >= 0; i--) Destroy(layer.GetChild(i).gameObject);
    }
}
