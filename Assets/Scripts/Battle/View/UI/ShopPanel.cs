// ============================================================
// ShopPanel.cs
// 상점 화면 (2026-11-15)
//   플레이어마다 따로: 자기 직업 카드 5장 / 아티팩트 2개 / 포션 2개. 각자 자기 골드로 삼
//   멀티면 위쪽 탭으로 P1 / P2 를 바꿔 봄. [떠나기] = 다음으로
//   물건 규칙(가격·사기)은 RunState.MakeShop / Buy 가 정하고, 이 파일은 그리기만
//   3-148: 새 상점 그림 (Resources/Backgrounds/Shop.png, 1024×572) 이 있으면 그 그림 위에 그림 속 자리 그대로
//     위 글자 줄 → P1~P4 탭 (UI/Shop/Shop_Tab·Shop_TabOn) → 선반 위 카드 5장 + 가격 → 받침대 4개 (아티팩트 2 · 포션 2) → [떠나기]
//     자리는 전부 "그림 픽셀 좌표" 로 적어 둠 (UIFactory.ArtSpot). 그림이 없으면 예전 화면 그대로
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ShopPanel : MonoBehaviour
{
    public System.Action OnLeave;
    public System.Action<string, RectTransform> OnHover;
    public System.Action OnBought;            // 산 뒤 (위쪽 플레이어 정보 갱신용)
    private RunState run;
    private int current;
    private RectTransform content;
    private Text message;
    private float messageTimer;

    // 3-148: 새 상점 그림
    const float ArtW = 1024f, ArtH = 572f;                         // 그림 크기 (좌표 단위)
    static readonly float[] RelicSlotX = { 327f, 452f };           // 받침대 가운데 x: 회색·파란 받침대 = 아티팩트
    static readonly float[] PotionSlotX = { 576f, 699f };          // 보라 받침대 = 포션
    const float SlotY = 357f, SlotR = 22f;                         // 받침대 동그라미 가운데 y / 안쪽 반지름
    const float CardY = 210f, CardH = 130f, CardGap = 110f;       // 선반 위 카드: 가운데 y / 그림 속 카드 높이 / 카드 사이
    const float TabY = 101f, TabGap = 140f;                        // 탭 가운데 y / 탭 사이
    private Image art;                // 상점 그림 (없으면 null → 예전 화면)
    private RectTransform artContent; // 그림 위에 다시 그리는 것들 (Redraw 때마다 지우고 새로)
    private Sprite tabSprite, tabOnSprite;

    public static ShopPanel Create(Transform parent)
    {
        RectTransform rt = UIFactory.MakeRect("Shop", parent);
        UIFactory.Stretch(rt, 0, 0, 64, 0);
        ShopPanel panel = rt.gameObject.AddComponent<ShopPanel>();
        Image dim = UIFactory.MakeImage("Dim", rt, Color.white, false);
        dim.sprite = UIFactory.MakeGradient(UIFactory.Hex("#2A1E16"), UIFactory.Hex("#0C0908"));
        UIFactory.Stretch(dim.rectTransform);
        // 3-148: 상점 그림 (있으면)
        Sprite shopArt = Resources.Load<Sprite>("Backgrounds/Shop");
        if (shopArt != null)
        {
            dim.sprite = null;
            dim.color = UIFactory.Hex("#0B0807");   // 그림 옆에 남는 곳 (화면 비율이 다를 때)
            panel.art = UIFactory.MakeArtBackground("Art", rt, shopArt);
            UIFactory.Stretch(panel.art.rectTransform);   // FitInParent 가 크기를 다시 맞춤
            panel.art.rectTransform.anchorMin = panel.art.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            panel.artContent = UIFactory.MakeRect("ArtContent", panel.art.transform);
            UIFactory.Stretch(panel.artContent);
            panel.tabSprite = Resources.Load<Sprite>("UI/Shop/Shop_Tab");
            panel.tabOnSprite = Resources.Load<Sprite>("UI/Shop/Shop_TabOn");
        }
        panel.content = UIFactory.MakeRect("Content", rt);
        UIFactory.Stretch(panel.content);
        panel.message = UIFactory.MakeText("Message", rt, "", 28, UIFactory.Hex("#FFE6A0"), TextAnchor.MiddleCenter);
        UIFactory.Place(panel.message.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 120), new Vector2(1200, 44));
        panel.message.fontStyle = FontStyle.Bold;
        panel.message.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.9f);
        rt.gameObject.SetActive(false);
        return panel;
    }

    public void Show(RunState r, int first)
    {
        run = r;
        current = Mathf.Clamp(first, 0, run.Players.Count - 1);
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        message.text = "";
        Redraw();
    }

    public void Hide() { gameObject.SetActive(false); }

    void Redraw()
    {
        for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
        PlayerCombatant p = run.Players[current];
        List<ShopItem> items;
        if (!run.ShopStock.TryGetValue(p, out items)) items = new List<ShopItem>();
        if (art != null) { RedrawArt(p, items); return; }   // 3-148: 새 그림 화면

        Text title = UIFactory.MakeText("Title", content, "상점", 52, UIFactory.Hex("#FFD27A"), TextAnchor.MiddleCenter);
        UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -14), new Vector2(600, 64));
        title.font = UIFactory.TitleFont;
        Text gold = UIFactory.MakeText("Gold", content, p.Name + " " + ClassLibrary.ColoredClassName(p.Class) + "   골드 " + p.Gold + "   ·   포션 " + p.Potions.Count + " / " + GameBalance.PotionSlots, 26, Color.white, TextAnchor.MiddleCenter);
        UIFactory.Place(gold.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -78), new Vector2(1200, 36));

        // 멀티: 플레이어 탭
        if (run.Players.Count > 1)
        {
            int n = run.Players.Count;
            for (int i = 0; i < n; i++)
            {
                int index = i;
                bool on = i == current;
                Button b = UIFactory.MakeButton("Tab" + i, content, run.Players[i].Name + " " + ClassLibrary.ColoredClassName(run.Players[i].Class),
                    on ? UIFactory.Hex("#6A4A2A") : UIFactory.Hex("#2C2420"), () => { current = index; Redraw(); }, 22);
                UIFactory.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2((i - (n - 1) / 2f) * 260, -160), new Vector2(240, 46));
                if (on) b.gameObject.AddComponent<Outline>().effectColor = UIFactory.Hex("#FFD27A");
            }
        }

        // 카드 줄
        List<ShopItem> cards = items.FindAll(x => x.Card != null);
        const float cardScale = 0.72f;
        RectTransform cardRow = UIFactory.MakeRect("Cards", content);
        UIFactory.Place(cardRow, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -350), Vector2.zero);
        for (int i = 0; i < cards.Count; i++)
        {
            ShopItem item = cards[i];
            float x = (i - (cards.Count - 1) / 2f) * (CardView.Width * cardScale + 40);
            CardView v = CardView.CreateDisplay(cardRow, item.Card, p.ManaName, new Vector2(x, 0), cardScale, item.Sold ? (System.Action)null : () => TryBuy(p, item));
            PriceTag(cardRow, new Vector2(x, -CardView.Height * cardScale / 2f - 26), item, p);
            if (item.Sold) SoldMark(v.transform);
        }

        // 아티팩트 · 포션 줄
        List<ShopItem> others = items.FindAll(x => x.Card == null);
        RectTransform row = UIFactory.MakeRect("Others", content);
        UIFactory.Place(row, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -630), Vector2.zero);
        for (int i = 0; i < others.Count; i++)
        {
            ShopItem item = others[i];
            float x = (i - (others.Count - 1) / 2f) * 230f;
            Image icon = item.Relic != null ? RelicView.MakeIcon(row, item.Relic, 96, null) : PotionView.MakeIcon(row, item.Potion, 96);
            UIFactory.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 10), new Vector2(96, 96));
            icon.raycastTarget = true;
            string tip = item.Relic != null
                ? item.Relic.Name + " [" + item.Relic.RarityName() + "]\n" + item.Relic.Description + (item.Relic.HasPenalty ? "\n페널티: " + item.Relic.Penalty : "")
                : item.Potion.Name + "\n" + item.Potion.Description;
            RectTransform irt = icon.rectTransform;
            HoverRelay hover = icon.gameObject.AddComponent<HoverRelay>();
            hover.OnChange = (on) => { if (OnHover != null) OnHover(on ? tip : null, irt); };
            if (!item.Sold)
            {
                Button b = icon.gameObject.AddComponent<Button>();
                b.onClick.AddListener(() => TryBuy(p, item));
            }
            Text nm = UIFactory.MakeText("Name", row, item.Name, 20, Color.white, TextAnchor.MiddleCenter);
            UIFactory.Place(nm.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, -58), new Vector2(220, 28));
            nm.resizeTextForBestFit = true;   // 3-105: 긴 이름(수비대장의 식량 주머니 등)이 두 줄로 넘쳐 가격과 겹치지 않게
            nm.verticalOverflow = VerticalWrapMode.Truncate;   // 3-154: 글자 자동 축소는 세로가 Truncate 일 때만 동작 (Overflow 면 줄지 않고 칸 밖으로 넘침)
            nm.resizeTextMinSize = 13;
            nm.resizeTextMaxSize = 20;
            nm.raycastTarget = false;
            PriceTag(row, new Vector2(x, -90), item, p);
            if (item.Sold) SoldMark(icon.transform);
        }

        Button leave = UIFactory.MakeButton("Leave", content, "떠나기", UIFactory.Hex("#4A3A2A"), () => { if (OnHover != null) OnHover(null, null); if (OnLeave != null) OnLeave(); }, 30);
        UIFactory.Place((RectTransform)leave.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-50, 30), new Vector2(240, 74));
        UIFactory.SkinButton(leave, UIFactory.GothicButton);   // 3-181: 8번 공용 버튼 틀 (그림 없던 버튼)
    }

    // ==================== 3-148: 새 상점 그림 화면 ====================
    // 그림 속 자리마다 ArtSpot 으로 상자를 만들고 그 안에 글자·카드·아이콘을 채움
    //   k = 그림 1픽셀이 화면에서 몇 칸인지 → 글자 크기·카드 크기를 그림 크기에 맞춤
    void RedrawArt(PlayerCombatant p, List<ShopItem> items)
    {
        for (int i = artContent.childCount - 1; i >= 0; i--) Destroy(artContent.GetChild(i).gameObject);
        float k = UIFactory.ArtScale(art.rectTransform, ArtW);
        RectTransform root = artContent;

        // ---- 위 글자 줄: P1 검은 낫   골드 100  ·  포션 0 / 2 ----
        RectTransform line = UIFactory.ArtSpot("GoldLine", root, ArtW, ArtH, 330, 50, 694, 80);
        Text gold = UIFactory.MakeText("Text", line, "<b>" + p.Name + " " + ClassLibrary.ColoredClassName(p.Class) + "</b>    <color=#F2C25A>골드 " + p.Gold
            + "</color>    ·    포션 " + p.Potions.Count + " / " + GameBalance.PotionSlots, Mathf.RoundToInt(15 * k), UIFactory.Hex("#F4EAD6"), TextAnchor.MiddleCenter);
        UIFactory.Stretch(gold.rectTransform);

        // ---- 플레이어 탭 (멀티) ----
        int n = run.Players.Count;
        if (n > 1)
        {
            for (int i = 0; i < n; i++)
            {
                int index = i;
                bool on = i == current;
                float cx = ArtW / 2f + (i - (n - 1) / 2f) * TabGap;
                RectTransform spot = on ? UIFactory.ArtSpot("Tab" + i, root, ArtW, ArtH, cx - 67, TabY - 23, cx + 67, TabY + 24)
                                        : UIFactory.ArtSpot("Tab" + i, root, ArtW, ArtH, cx - 71, TabY - 19, cx + 71, TabY + 19);
                Sprite sp = on ? tabOnSprite : tabSprite;
                Image img = UIFactory.MakeImage("Back", spot, sp != null ? Color.white : (on ? UIFactory.Hex("#C89A2A") : UIFactory.Hex("#3A3C44")), sp == null);
                if (sp != null) img.sprite = sp;
                UIFactory.Stretch(img.rectTransform);
                img.raycastTarget = true;
                Button b = img.gameObject.AddComponent<Button>();
                b.targetGraphic = img;
                ColorBlock cb = b.colors;
                cb.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);   // 마우스를 올리면 살짝 밝게
                b.colors = cb;
                b.onClick.AddListener(() => { current = index; Redraw(); });
                PlayerCombatant pl = run.Players[i];
                Text label = UIFactory.MakeText("Label", spot, pl.Name + " " + ClassLibrary.ColoredClassName(pl.Class), Mathf.RoundToInt(12.5f * k),
                    on ? UIFactory.Hex("#2A1A06") : UIFactory.Hex("#F0EAE0"), TextAnchor.MiddleCenter);
                UIFactory.Stretch(label.rectTransform, 10 * k, 10 * k, 0, 0);
                label.fontStyle = FontStyle.Bold;
                if (on) foreach (Shadow sh in label.GetComponents<Shadow>()) sh.effectColor = new Color(1f, 0.95f, 0.7f, 0.35f);   // 금색 탭 위 어두운 글자는 밝은 그림자
            }
        }

        // ---- 선반 위 카드 ----
        List<ShopItem> cards = items.FindAll(x => x.Card != null);
        float cardScale = CardH * k / CardView.Height;
        for (int i = 0; i < cards.Count; i++)
        {
            ShopItem item = cards[i];
            float cx = ArtW / 2f + (i - (cards.Count - 1) / 2f) * CardGap;
            RectTransform holder = UIFactory.ArtSpot("Card" + i, root, ArtW, ArtH, cx - 1, CardY - 1, cx + 1, CardY + 1);
            CardView v = CardView.CreateDisplay(holder, item.Card, p.ManaName, Vector2.zero, cardScale, item.Sold ? (System.Action)null : () => TryBuy(p, item));
            if (item.Sold) SoldMark(v.transform);
            ArtPrice(root, k, cx, 288f, item, p, 13f);
        }

        // ---- 받침대: 아티팩트는 앞 두 자리, 포션은 뒤 두 자리 ----
        List<ShopItem> relics = items.FindAll(x => x.Relic != null);
        List<ShopItem> potions = items.FindAll(x => x.Potion != null);
        bool fits = relics.Count <= RelicSlotX.Length && potions.Count <= PotionSlotX.Length;
        List<ShopItem> others = new List<ShopItem>(relics);
        others.AddRange(potions);
        for (int i = 0; i < others.Count; i++)
        {
            ShopItem item = others[i];
            float cx;
            if (fits) cx = item.Relic != null ? RelicSlotX[relics.IndexOf(item)] : PotionSlotX[potions.IndexOf(item)];
            else cx = RelicSlotX[0] + (PotionSlotX[PotionSlotX.Length - 1] - RelicSlotX[0]) * (others.Count > 1 ? i / (float)(others.Count - 1) : 0.5f);   // 물건 수가 바뀌었을 때: 고르게 나눔
            RectTransform slot = UIFactory.ArtSpot("Slot" + i, root, ArtW, ArtH, cx - SlotR, SlotY - SlotR, cx + SlotR, SlotY + SlotR);
            Image icon = item.Relic != null ? RelicView.MakeIcon(slot, item.Relic, SlotR * 2 * k, null) : PotionView.MakeIcon(slot, item.Potion, SlotR * 2 * k);
            UIFactory.Stretch(icon.rectTransform);
            icon.raycastTarget = true;
            string tip = item.Relic != null
                ? item.Relic.Name + " [" + item.Relic.RarityName() + "]\n" + item.Relic.Description + (item.Relic.HasPenalty ? "\n페널티: " + item.Relic.Penalty : "")
                : item.Potion.Name + "\n" + item.Potion.Description;
            RectTransform irt = icon.rectTransform;
            HoverRelay hover = icon.gameObject.AddComponent<HoverRelay>();
            hover.OnChange = (on) => { if (OnHover != null) OnHover(on ? tip : null, irt); };
            if (!item.Sold)
            {
                Button b = icon.gameObject.AddComponent<Button>();
                b.onClick.AddListener(() => TryBuy(p, item));
            }
            else SoldMark(icon.transform);
            RectTransform nameSpot = UIFactory.ArtSpot("Name" + i, root, ArtW, ArtH, cx - 62, 386, cx + 62, 404);
            Text nm = UIFactory.MakeText("Text", nameSpot, item.Name, Mathf.RoundToInt(10.5f * k), Color.white, TextAnchor.MiddleCenter);
            UIFactory.Stretch(nm.rectTransform);
            nm.fontStyle = FontStyle.Bold;
            nm.resizeTextForBestFit = true;   // 긴 이름이 두 줄로 넘치지 않게 (3-105 와 같은 이유)
            nm.verticalOverflow = VerticalWrapMode.Truncate;   // 3-154: 글자 자동 축소는 세로가 Truncate 일 때만 동작 (Overflow 면 줄지 않고 칸 밖으로 넘침)
            nm.resizeTextMinSize = Mathf.RoundToInt(7 * k);
            nm.resizeTextMaxSize = Mathf.RoundToInt(10.5f * k);
            ArtPrice(root, k, cx, 416f, item, p, 11f);
        }

        // ---- [떠나기]: 그림 속 버튼 틀 위에 투명 버튼 + 글자 ----
        RectTransform leaveSpot = UIFactory.ArtSpot("Leave", root, ArtW, ArtH, 872, 522, 1010, 564);
        Image glow = UIFactory.MakeImage("Glow", leaveSpot, new Color(1f, 0.85f, 0.55f, 0f), false);
        glow.sprite = UIFactory.SoftGlowSprite;
        UIFactory.Stretch(glow.rectTransform, -20 * k, -20 * k, -14 * k, -14 * k);
        glow.raycastTarget = false;
        Image hit = UIFactory.MakeImage("Hit", leaveSpot, new Color(0, 0, 0, 0), false);
        UIFactory.Stretch(hit.rectTransform);
        hit.raycastTarget = true;
        Button leave = hit.gameObject.AddComponent<Button>();
        leave.transition = Selectable.Transition.None;
        leave.onClick.AddListener(() => { if (OnHover != null) OnHover(null, null); if (OnLeave != null) OnLeave(); });
        HoverRelay leaveHover = hit.gameObject.AddComponent<HoverRelay>();
        leaveHover.OnChange = (on) => { glow.color = new Color(1f, 0.85f, 0.55f, on ? 0.35f : 0f); };   // 마우스를 올리면 은은하게 빛남
        Text leaveText = UIFactory.MakeText("Label", leaveSpot, "떠나기", Mathf.RoundToInt(14 * k), UIFactory.Hex("#F4EAD6"), TextAnchor.MiddleCenter);
        UIFactory.Stretch(leaveText.rectTransform);
        leaveText.fontStyle = FontStyle.Bold;

        BuildSellArea(p, root, k);   // 3-163
    }

    // ==================== 3-163: 아티팩트 팔기 (끌어다 놓기) ====================
    //   아래: "내 아티팩트" 줄 (이 탭 플레이어 것) + 왼쪽 아래 판매대
    //   아이콘을 판매대로 끌어다 놓으면 팜 (값 = 상점 가격의 절반). 시작·공용 아티팩트는 흐리게, 끌 수 없음
    private RectTransform sellZone;
    private Image sellGlow;
    private Image dragGhost;

    void BuildSellArea(PlayerCombatant p, RectTransform root, float k)
    {
        // 판매대 (왼쪽 아래)
        sellZone = UIFactory.ArtSpot("SellZone", root, ArtW, ArtH, 34, 456, 228, 560);
        sellGlow = UIFactory.MakeImage("Glow", sellZone, new Color(1f, 0.8f, 0.3f, 0f), false);
        sellGlow.sprite = UIFactory.SoftGlowSprite;
        UIFactory.Stretch(sellGlow.rectTransform, -20 * k, -20 * k, -16 * k, -16 * k);
        sellGlow.raycastTarget = false;
        Image zone = UIFactory.MakeImage("Back", sellZone, new Color(0.08f, 0.05f, 0.03f, 0.78f));
        UIFactory.Stretch(zone.rectTransform);
        zone.raycastTarget = false;
        // 3-181: 판매대 그림 (5번 그림: 해골·사슬 쇠틀). 없으면 예전 어두운 판 + 금색 테두리
        Sprite sellArt = Resources.Load<Sprite>("UI/Shop/Shop_SellBox");
        float textInsetX = 6 * k, textInsetY = 4 * k;
        if (sellArt != null)
        {
            zone.sprite = sellArt;
            zone.type = Image.Type.Simple;
            zone.color = Color.white;
            textInsetX = 26 * k;   // 쇠틀 두께만큼 글자를 안쪽으로
            textInsetY = 14 * k;
        }
        else zone.gameObject.AddComponent<Outline>().effectColor = UIFactory.Hex("#C89A50");
        Text zt = UIFactory.MakeText("Text", sellZone, "<b>판매대</b>\n<size=" + Mathf.RoundToInt(9.5f * k) + "><color=#D8CCB4>아티팩트를 여기로 끌어 오면\n상점 가격의 절반에 팝니다</color></size>",
            Mathf.RoundToInt(15 * k), UIFactory.Hex("#FFD27A"), TextAnchor.MiddleCenter);
        UIFactory.Stretch(zt.rectTransform, textInsetX, textInsetX, textInsetY, textInsetY);
        zt.supportRichText = true;
        zt.raycastTarget = false;

        // 내 아티팩트 줄 (받침대 이름·가격 아래 ~ 바닥)
        RectTransform strip = UIFactory.ArtSpot("MyRelics", root, ArtW, ArtH, 244, 450, 860, 516);
        Text head = UIFactory.MakeText("Head", strip, p.Name + " 아티팩트 (판매대로 끌어서 팔기)", Mathf.RoundToInt(10 * k), UIFactory.Hex("#E8DCC4"), TextAnchor.UpperLeft);
        head.rectTransform.anchorMin = new Vector2(0f, 0.68f); head.rectTransform.anchorMax = Vector2.one;
        head.rectTransform.offsetMin = head.rectTransform.offsetMax = Vector2.zero;
        head.raycastTarget = false;
        float size = 30f * k, gap = 4f * k;
        int perRow = Mathf.Max(1, Mathf.FloorToInt((616f * k + gap) / (size + gap)));
        for (int i = 0; i < p.Relics.Count && i < perRow; i++)
        {
            RelicData r = p.Relics[i];
            bool can = RunState.CanSellRelic(r);
            Image icon = RelicView.MakeIcon(strip, r, size, null);
            UIFactory.Place(icon.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(i * (size + gap), 2 * k), new Vector2(size, size));
            icon.raycastTarget = true;
            if (!can) icon.gameObject.AddComponent<CanvasGroup>().alpha = 0.4f;   // 못 파는 것 = 흐리게 (설명은 그대로 보임)
            string tip = r.Name + " [" + r.RarityName() + "]\n" + r.Description + (r.HasPenalty ? "\n페널티: " + r.Penalty : "")
                       + (can ? "\n<color=#FFD27A>판매 " + run.RelicSellPrice(r) + " 골드</color>" : "\n<color=#A0A0A0>팔 수 없음 (시작·공용)</color>");
            RectTransform irt = icon.rectTransform;
            HoverRelay hover = icon.gameObject.AddComponent<HoverRelay>();
            hover.OnChange = (on) => { if (OnHover != null) OnHover(on ? tip : null, irt); };
            if (can) MakeDraggable(icon, p, r, size);
        }
        if (p.Relics.Count > perRow)
        {
            Text more = UIFactory.MakeText("More", strip, "+" + (p.Relics.Count - perRow), Mathf.RoundToInt(11 * k), Color.white, TextAnchor.LowerRight);
            UIFactory.Stretch(more.rectTransform);
            more.raycastTarget = false;
        }
    }

    // 아이콘 끌기: 누르면 작은 복사본(유령)이 마우스를 따라오고, 판매대 위에서 놓으면 팜
    void MakeDraggable(Image icon, PlayerCombatant p, RelicData r, float size)
    {
        DragRelay drag = icon.gameObject.AddComponent<DragRelay>();
        drag.OnBegin = (e) =>
        {
            if (OnHover != null) OnHover(null, null);
            dragGhost = UIFactory.MakeImage("DragGhost", transform, Color.white, false);
            Sprite art = Resources.Load<Sprite>("Icons/Relic_" + r.Id);
            dragGhost.sprite = art != null ? art : icon.sprite;   // 그림이 없는 아티팩트는 동그라미
            dragGhost.color = new Color(1f, 1f, 1f, 0.85f);
            dragGhost.raycastTarget = false;   // 유령이 마우스를 가리면 판매대를 못 찾음
            dragGhost.rectTransform.sizeDelta = new Vector2(size * 1.4f, size * 1.4f);
            MoveGhost(e);
        };
        drag.OnMove = (e) =>
        {
            MoveGhost(e);
            bool over = RectTransformUtility.RectangleContainsScreenPoint(sellZone, e.position, e.pressEventCamera);
            sellGlow.color = new Color(1f, 0.8f, 0.3f, over ? 0.55f : 0.2f);   // 판매대 위면 밝게
        };
        drag.OnDrop = (e) =>
        {
            if (dragGhost != null) Destroy(dragGhost.gameObject);
            dragGhost = null;
            sellGlow.color = new Color(1f, 0.8f, 0.3f, 0f);
            if (!RectTransformUtility.RectangleContainsScreenPoint(sellZone, e.position, e.pressEventCamera)) return;   // 판매대 밖 = 취소
            message.text = run.SellRelic(p, r);
            messageTimer = 2.5f;
            Redraw();
            if (OnBought != null) OnBought();   // 골드·아티팩트 줄 다시 그리기 (사기와 같은 알림)
        };
    }

    void MoveGhost(UnityEngine.EventSystems.PointerEventData e)
    {
        if (dragGhost == null) return;
        Vector2 local;
        RectTransform parent = (RectTransform)transform;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, e.position, e.pressEventCamera, out local))
        {
            dragGhost.rectTransform.anchorMin = dragGhost.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            dragGhost.rectTransform.anchoredPosition = local - new Vector2(parent.rect.width * (0.5f - parent.pivot.x), parent.rect.height * (0.5f - parent.pivot.y));   // local 은 pivot 기준 → 가운데 기준으로
        }
    }

    // 그림 속 가격 글자 (가운데 cx, cy). 살 수 있으면 금색, 모자라면 빨강, 팔렸으면 회색
    void ArtPrice(RectTransform root, float k, float cx, float cy, ShopItem item, PlayerCombatant p, float size)
    {
        RectTransform spot = UIFactory.ArtSpot("Price", root, ArtW, ArtH, cx - 55, cy - 10, cx + 55, cy + 10);
        bool afford = p.Gold >= item.Price;
        string text = item.Sold ? "판매됨" : item.Price + " 골드";
        Text t = UIFactory.MakeText("Text", spot, text, Mathf.RoundToInt(size * k), item.Sold ? Color.gray : (afford ? UIFactory.Hex("#FFD27A") : UIFactory.Hex("#FF6A6A")), TextAnchor.MiddleCenter);
        UIFactory.Stretch(t.rectTransform);
        t.fontStyle = FontStyle.Bold;
    }

    void PriceTag(RectTransform parent, Vector2 pos, ShopItem item, PlayerCombatant p)
    {
        bool afford = p.Gold >= item.Price;
        string text = item.Sold ? "판매됨" : item.Price + " 골드";
        Text t = UIFactory.MakeText("Price", parent, text, 22, item.Sold ? Color.gray : (afford ? UIFactory.Hex("#FFD27A") : UIFactory.Hex("#FF6A6A")), TextAnchor.MiddleCenter);
        UIFactory.Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(200, 30));
        t.fontStyle = FontStyle.Bold;
        t.raycastTarget = false;
    }

    void SoldMark(Transform target)
    {
        Image dim = UIFactory.MakeImage("Sold", target, new Color(0, 0, 0, 0.65f), false);
        UIFactory.Stretch(dim.rectTransform);
        dim.raycastTarget = false;
    }

    void TryBuy(PlayerCombatant p, ShopItem item)
    {
        if (OnHover != null) OnHover(null, null);
        message.text = run.Buy(p, item);
        messageTimer = 2.5f;
        Redraw();
        if (OnBought != null) OnBought();
    }

    void Update()
    {
        if (messageTimer > 0f)
        {
            messageTimer -= Time.unscaledDeltaTime;
            if (messageTimer <= 0f) message.text = "";
        }
    }
}
