// ============================================================
// TreasurePickPanel.cs
// 보물 상자를 연 뒤 "한 사람이 1개씩 고르기" 화면 (2026-11-15, 슬더스2 멀티 방식)
//
//   상자 안 물건 = 인원 + 1개 (아티팩트·포션). 한 PC 에서 P1 → P2 → ... 차례로 고름
//   위쪽 P 탭을 누르면 그 사람 차례로 바꿔서 다시 고를 수 있음
//   물건 아래에 누가 골랐는지 P 표시. 모두 고르면 [나눠 받기]
//   같은 물건을 여러 명이 고르면 주사위로 1명만, 나머지는 남은 물건 중 무작위 (규칙은 RunState.ResolveTreasure)
//   결과를 보여 주고 [계속] → OnDone
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TreasurePickPanel : MonoBehaviour
{
    public System.Action OnDone;                                // [계속] 을 눌렀을 때
    public System.Action<string, RectTransform> OnHover;       // 설명 말풍선
    private RunState run;
    private int picker;                                         // 지금 고르는 사람 번호
    private List<string> results;                               // 나눠 받은 결과 (null 이면 아직 고르는 중)
    private RectTransform content;

    // 플레이어 색 (P1 금색, P2 빨강, P3 파랑, P4 초록) - 투표 표시와 같은 색
    public static Color PlayerColor(int i)
    {
        switch (i)
        {
            case 0: return UIFactory.Hex("#F0C850");
            case 1: return UIFactory.Hex("#E86060");
            case 2: return UIFactory.Hex("#60A8F0");
            default: return UIFactory.Hex("#70D070");
        }
    }

    public static TreasurePickPanel Create(Transform parent)
    {
        RectTransform rt = UIFactory.MakeRect("TreasurePick", parent);
        UIFactory.Stretch(rt, 0, 0, 64, 0);
        TreasurePickPanel panel = rt.gameObject.AddComponent<TreasurePickPanel>();
        Image dim = UIFactory.MakeImage("Dim", rt, Color.white, false);
        dim.sprite = UIFactory.MakeGradient(UIFactory.Hex("#2A2216"), UIFactory.Hex("#0C0A08"));
        UIFactory.Stretch(dim.rectTransform);
        panel.content = UIFactory.MakeRect("Content", rt);
        UIFactory.Stretch(panel.content);
        rt.gameObject.SetActive(false);
        return panel;
    }

    public void Show(RunState r)
    {
        run = r;
        results = null;
        picker = FirstUnpicked();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Redraw();
    }

    public void Hide() { gameObject.SetActive(false); }

    // 아직 안 고른 첫 사람 (받을 수 있는 게 있는 사람만). 모두 골랐으면 지금 사람 그대로
    int FirstUnpicked()
    {
        for (int i = 0; i < run.Players.Count; i++)
            if (run.TreasurePicks[i] < 0 && run.CanTakeAnyTreasure(run.Players[i])) return i;
        return Mathf.Clamp(picker, 0, run.Players.Count - 1);
    }

    void Redraw()
    {
        for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);

        // 3-174: 상자 2개 → "보물 상자 1/2". 결과 화면은 이미 다음 상자로 넘어간 뒤라 번호를 하나 빼서 보여 줌
        int chestNo = run.TreasureChestIndex + 1 - (results != null && run.Place == RunPlace.Treasure ? 1 : 0);
        // 3-181: 제목 판 그림 (6번 그림: 날개·사슬 달린 쇠 판). 없으면 글자만
        Sprite titleArt = Resources.Load<Sprite>("UI/Treasure/Treasure_Title");
        if (titleArt != null)
        {
            Image plate = UIFactory.MakeImage("TitlePlate", content, Color.white, false);
            plate.sprite = titleArt;
            plate.preserveAspect = true;
            UIFactory.Place(plate.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -2), new Vector2(820, 162));
            plate.raycastTarget = false;
        }
        Text title = UIFactory.MakeText("Title", content, "보물 상자 " + chestNo + "/" + GameBalance.TreasureChests, 52, UIFactory.Hex("#FFD27A"), TextAnchor.MiddleCenter);
        UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, titleArt != null ? -42 : -14), new Vector2(600, 64));
        title.font = UIFactory.TitleFont;
        if (titleArt != null) title.gameObject.AddComponent<Outline>().effectColor = new Color(0.1f, 0.04f, 0f, 0.9f);

        if (results != null) { DrawResults(); return; }

        int n = run.Players.Count;
        PlayerCombatant me = run.Players[picker];
        string guide = n > 1
            ? me.Name + " " + ClassLibrary.ColoredClassName(me.Class) + " 이(가) 고를 차례입니다. 한 사람당 1개!\n<size=20>같은 걸 여러 명이 고르면 주사위로 1명만 받고, 진 사람은 남은 것 중 무작위로 받아요.</size>"
            : "하나를 고르세요.";
        Text g = UIFactory.MakeText("Guide", content, guide, 28, Color.white, TextAnchor.MiddleCenter);
        g.supportRichText = true;
        UIFactory.Place(g.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, titleArt != null ? -150 : -96), new Vector2(1400, 84));   // 3-181: 제목 판 아래로
        // 3-157: 안내 2줄이 아래 P 탭과 겹쳤음 → 안내를 조금 위로, 탭을 아래로(-200 → -232), 넘치면 글자 축소
        g.resizeTextForBestFit = true;
        g.resizeTextMinSize = 16;
        g.resizeTextMaxSize = 28;
        g.verticalOverflow = VerticalWrapMode.Truncate;

        // P 탭: 누르면 그 사람 차례로 (다시 고르기)
        if (n > 1)
        {
            for (int i = 0; i < n; i++)
            {
                int index = i;
                PlayerCombatant p = run.Players[i];
                bool picked = run.TreasurePicks[i] >= 0;
                bool none = !run.CanTakeAnyTreasure(p);
                string label = p.Name + (picked ? "  (고름)" : (none ? "  (없음)" : ""));
                // 3-157: P 자리마다 탭 그림 (UI/Treasure/Treasure_Tab_P1~P4: 금 · 빨강 · 파랑 · 초록). 없으면 예전 색 버튼
                Sprite tabArt = Resources.Load<Sprite>("UI/Treasure/Treasure_Tab_P" + (i + 1));
                if (tabArt != null)
                {
                    bool on = i == picker;
                    RectTransform holder = UIFactory.MakeRect("Tab" + i, content);
                    UIFactory.Place(holder, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2((i - (n - 1) / 2f) * 262, -232), new Vector2(244, 68));
                    if (on)
                    {
                        // 고르는 사람 = 뒤에 그 사람 색 빛
                        Image glow = UIFactory.MakeImage("Glow", holder, new Color(PlayerColor(i).r, PlayerColor(i).g, PlayerColor(i).b, 0.55f), false);
                        glow.sprite = UIFactory.SoftGlowSprite;
                        UIFactory.Stretch(glow.rectTransform, -30, -30, -24, -24);
                        glow.raycastTarget = false;
                    }
                    Image img = UIFactory.MakeImage("Frame", holder, on ? Color.white : new Color(0.72f, 0.72f, 0.72f, 1f), false);
                    img.sprite = tabArt;
                    UIFactory.Stretch(img.rectTransform);
                    Button tb = img.gameObject.AddComponent<Button>();
                    tb.transition = Selectable.Transition.None;
                    tb.onClick.AddListener(() => { picker = index; Redraw(); });
                    Text tl = UIFactory.MakeText("Label", holder, label, 22, on ? Color.Lerp(PlayerColor(i), Color.white, 0.35f) : PlayerColor(i), TextAnchor.MiddleCenter);
                    UIFactory.Stretch(tl.rectTransform, 30, 30, 4, 4);
                    tl.font = UIFactory.TitleFont;
                    tl.resizeTextForBestFit = true; tl.resizeTextMinSize = 12; tl.resizeTextMaxSize = 22;
                    tl.verticalOverflow = VerticalWrapMode.Truncate;
                    tl.raycastTarget = false;
                    tl.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.9f);
                    continue;
                }
                Button b = UIFactory.MakeButton("Tab" + i, content, label, i == picker ? UIFactory.Hex("#6A4A2A") : UIFactory.Hex("#2C2420"), () => { picker = index; Redraw(); }, 22);
                UIFactory.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2((i - (n - 1) / 2f) * 262, -232), new Vector2(240, 50));
                b.gameObject.AddComponent<Outline>().effectColor = PlayerColor(i);
            }
        }

        // 물건 줄
        List<ShopItem> items = run.TreasureItems;
        int m = items.Count;
        float gap = Mathf.Min(300f, 1500f / Mathf.Max(1, m));
        for (int j = 0; j < m; j++)
        {
            int jj = j;
            ShopItem item = items[j];
            float x = (j - (m - 1) / 2f) * gap;
            bool can = run.CanTakeTreasure(me, item);

            Image frame = UIFactory.MakeImage("Slot" + j, content, new Color(0.18f, 0.14f, 0.1f, 0.95f), false);
            UIFactory.Place(frame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 20), new Vector2(gap - 30, 330));
            frame.raycastTarget = true;
            // 3-181: 물건 틀 그림 (6번 그림). 누가 고른 물건 = 금테, 아니면 덩굴 쇠틀
            bool anyPicked = System.Array.IndexOf(run.TreasurePicks, j) >= 0;
            Sprite slotArt = Resources.Load<Sprite>(anyPicked ? "UI/Treasure/Treasure_ItemOn" : "UI/Treasure/Treasure_Item");
            if (slotArt != null)
            {
                frame.sprite = slotArt;
                frame.type = Image.Type.Simple;
                frame.color = can || anyPicked ? Color.white : new Color(0.6f, 0.6f, 0.6f, 1f);   // 못 받는 물건은 어둡게
            }
            if (slotArt == null && run.TreasurePicks.Length > picker && run.TreasurePicks[picker] == j)
                frame.gameObject.AddComponent<Outline>().effectColor = PlayerColor(picker);

            Image icon;
            if (item.Card != null)
            {
                // 공용 카드: 카드 그림이 있으면 그 그림, 없으면 카드 모양 네모 (2026-11-15)
                Sprite art = Resources.Load<Sprite>("Cards/" + item.Card.Id);
                icon = UIFactory.MakeImage("CardIcon", frame.transform, art != null ? Color.white : UIFactory.Hex("#5A6A8A"), false);
                if (art != null) icon.sprite = art;
                Text ct2 = UIFactory.MakeText("T", icon.transform, art != null ? "" : "카드\n" + item.Card.Cost, 22, Color.white, TextAnchor.MiddleCenter);
                UIFactory.Stretch(ct2.rectTransform);
            }
            else icon = item.Relic != null ? RelicView.MakeIcon(frame.transform, item.Relic, 110, null) : PotionView.MakeIcon(frame.transform, item.Potion, 110);
            UIFactory.Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -80), new Vector2(110, 110));
            icon.raycastTarget = false;
            if (!can) icon.color = new Color(1, 1, 1, 0.35f);

            string kind = item.Card != null ? "[공용 카드 · 에너지 " + item.Card.Cost + "]" : item.Relic != null ? "[" + item.Relic.RarityName() + " 아티팩트]" + (item.Relic.ClassOnly != PlayerClass.Common ? " " + ClassLibrary.ColoredClassName(item.Relic.ClassOnly) + " 전용" : "") : "[포션]";
            string desc = item.Card != null ? item.Card.Description : item.Relic != null ? item.Relic.Description + (item.Relic.HasPenalty ? "\n<color=#FF9A9A>페널티: " + item.Relic.Penalty + "</color>" : "") : item.Potion.Description;
            // 3-154: 이름 / 종류 / 설명을 따로 나눔 (한 글자 상자에 <size> 를 섞으면 글자 자동 축소가 안 돼서 긴 설명이 칸 밖으로 넘쳤음)
            Color tc = can ? Color.white : new Color(1, 1, 1, 0.4f);
            Text t = UIFactory.MakeText("Name", frame.transform, "<b>" + item.Name + "</b>", 24, tc, TextAnchor.MiddleCenter);
            UIFactory.Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(gap - 50, 32));
            t.supportRichText = true;
            t.resizeTextForBestFit = true; t.resizeTextMinSize = 14; t.resizeTextMaxSize = 24;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = false;
            Text tk = UIFactory.MakeText("Kind", frame.transform, kind, 18, can ? UIFactory.Hex("#C8B890") : new Color(0.78f, 0.72f, 0.56f, 0.4f), TextAnchor.MiddleCenter);
            UIFactory.Place(tk.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -182), new Vector2(gap - 50, 26));
            tk.resizeTextForBestFit = true; tk.resizeTextMinSize = 12; tk.resizeTextMaxSize = 18;
            tk.verticalOverflow = VerticalWrapMode.Truncate;
            tk.raycastTarget = false;
            Text td = UIFactory.MakeText("Desc", frame.transform, desc, 19, tc, TextAnchor.UpperCenter);
            UIFactory.Place(td.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -210), new Vector2(gap - 50, 112));
            td.supportRichText = true;
            td.resizeTextForBestFit = true; td.resizeTextMinSize = 12; td.resizeTextMaxSize = 19;
            td.verticalOverflow = VerticalWrapMode.Truncate;
            td.raycastTarget = false;
            if (!can)
            {
                Text no = UIFactory.MakeText("No", frame.transform, "받을 수 없음", 20, UIFactory.Hex("#FF8A8A"), TextAnchor.MiddleCenter);
                UIFactory.Place(no.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 14), new Vector2(200, 28));
                no.raycastTarget = false;
            }
            else
            {
                Button b = frame.gameObject.AddComponent<Button>();
                b.onClick.AddListener(() => Pick(jj));
            }

            // 누가 골랐는지 P 표시 (물건 아래)
            int shown = 0;
            for (int i = 0; i < n; i++)
            {
                if (run.TreasurePicks[i] != j) continue;
                Image chip = UIFactory.MakeImage("Pick" + i, content, PlayerColor(i), false);
                UIFactory.Place(chip.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x - 60 + shown * 42, -175), new Vector2(38, 30));
                chip.raycastTarget = false;
                Text ct = UIFactory.MakeText("T", chip.transform, "P" + (i + 1), 18, Color.black, TextAnchor.MiddleCenter);
                UIFactory.Stretch(ct.rectTransform);
                ct.fontStyle = FontStyle.Bold;
                shown++;
            }
        }

        bool all = run.AllTreasurePicked;
        Button ok = UIFactory.MakeButton("Resolve", content, all ? "나눠 받기" : "모두 고르면 나눠 받기", all ? UIFactory.Hex("#7846C8") : UIFactory.Hex("#3A3446"), Resolve, 30);
        UIFactory.Place((RectTransform)ok.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(380, 76));
        ok.interactable = all;
        UIFactory.SkinButton(ok, "UI/Treasure/Treasure_Button", 1.05f);   // 3-181: 6번 그림 버튼 (못 누를 때는 버튼 기본 어두운 색이 그림에 입혀짐)
    }

    void Pick(int item)
    {
        run.PickTreasure(picker, item);
        // 다음 안 고른 사람으로 자동으로 넘어감 (한 PC 에서 차례로)
        int next = -1;
        for (int k = 1; k <= run.Players.Count; k++)
        {
            int i = (picker + k) % run.Players.Count;
            if (run.TreasurePicks[i] < 0 && run.CanTakeAnyTreasure(run.Players[i])) { next = i; break; }
        }
        if (next >= 0) picker = next;
        Redraw();
    }

    void Resolve()
    {
        if (!run.AllTreasurePicked) return;
        results = run.ResolveTreasure();
        Redraw();
    }

    void DrawResults()
    {
        Text t = UIFactory.MakeText("Results", content, string.Join("\n", results), 30, Color.white, TextAnchor.MiddleCenter);
        UIFactory.Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1500, 500));
        // 3-174: 상자가 더 남았으면 (아직 보물 칸 안) "다음 상자" → 닫힌 두 번째 상자가 나옴
        bool moreChest = run.Place == RunPlace.Treasure;
        Button next = UIFactory.MakeButton("Continue", content, moreChest ? "다음 상자" : "계속", UIFactory.Hex("#4A3A2A"), () => { if (OnDone != null) OnDone(); }, 30);
        UIFactory.Place((RectTransform)next.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(300, 76));
        UIFactory.SkinButton(next, "UI/Treasure/Treasure_Button", 1.05f);   // 3-181: [나눠 받기] 와 같은 그림
    }
}
