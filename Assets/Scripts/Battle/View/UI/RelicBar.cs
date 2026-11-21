// ============================================================
// RelicBar.cs
// 위쪽 버튼 줄 바로 아래 왼쪽: 플레이어 정보 판
//   2026-11-15: 예전엔 모든 플레이어를 한 줄씩 보여 줬는데, 이제 "지금 화면의 플레이어 한 명"만 보여 줌
//     (멀티: 각자 자기 캐릭터만. 한 PC 테스트에서는 P1/P2 탭으로 바꾼 사람)
//   1줄: P1 검은 낫   HP 50/60   골드 35          [포션 3칸]
//   2줄: 힘 20  체력 10  민첩 15
//   3줄: 아티팩트 아이콘들 (마우스를 올리면 설명)
//   포션은 전투 중 내 차례에 누르면 사용 (OnPotion)
//   2026-11-15 (3-85): 접기·펼치기. 오른쪽 아래 [▲ 접기] → 한 줄짜리 작은 띠 (수성 위쪽 칸 표시를 가리지 않게)
//     접힌 띠를 누르면 다시 펼쳐짐. 접었는지는 게임을 꺼도 기억 (PlayerPrefs)
//   3-148: 접힌 띠 새 그림 (UI/Party/Party_Bar) 이 있으면 장식 틀 (330×113): 위 줄 "P1 검은 낫 + 직업 문양", 아래 줄 "HP 85/85 + 하트", 오른쪽 ▼ 버튼 + "▼ 펼치기"
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RelicBar : MonoBehaviour
{
    public System.Action<string, RectTransform> OnHover;
    public System.Action<int> OnPotion;     // 포션 칸을 눌렀을 때 (몇 번째 칸)
    public System.Action OnToggle;          // 접기·펼치기를 눌렀을 때 (3-91: 멀티 P1~P4 탭도 같이 숨기기·보이기)
    private string shownKey = "";
    private PlayerCombatant lastPlayer;      // 접기·펼치기를 누르면 같은 내용으로 다시 그리려고 기억
    private bool lastCanUse;
    private bool showAllRelics;              // 3-118: "+N" 칸을 눌러 아티팩트 전체 목록을 펼쳤는지

    // 접혀 있는지 (static = 모든 화면이 같은 값. 자바의 static 필드와 같음)
    //   PlayerPrefs 는 static 초기화 때 부르면 유니티가 오류를 내서, 처음 그릴 때 한 번 읽음 (prefLoaded)
    public static bool Collapsed;
    private static bool prefLoaded;
    const float FullW = 640f, CollapsedW = 340f, CollapsedH = 40f;
    public const int RelicCols = 12, RelicRows = 2;   // 아티팩트 칸 (3-94: 한 줄 → 2줄 × 12칸)

    // 직업색 (3-94): Dark = 판 위쪽 색, Mid = HP 막대 아래쪽, Light = 테두리·HP 막대 위쪽·빈 칸
    public struct Theme { public Color Dark, Mid, Light; }
    public static Theme ThemeOf(PlayerClass c)   // 3-96: 보상 화면 이름 탭도 같은 직업색을 씀
    {
        switch (c)
        {
            case PlayerClass.BlackScythe:   return new Theme { Dark = UIFactory.Hex("#3A1A5A"), Mid = UIFactory.Hex("#7A3AC0"), Light = UIFactory.Hex("#C08CFF") };   // 보라
            case PlayerClass.CosmicPhoenix: return new Theme { Dark = UIFactory.Hex("#5A1A12"), Mid = UIFactory.Hex("#C8361E"), Light = UIFactory.Hex("#FF9A5A") };   // 붉은 불꽃
            case PlayerClass.FrostMage:     return new Theme { Dark = UIFactory.Hex("#132E5A"), Mid = UIFactory.Hex("#2A6AC8"), Light = UIFactory.Hex("#7CC8FF") };   // 푸른 얼음
            default:                        return new Theme { Dark = UIFactory.Hex("#3A3C44"), Mid = UIFactory.Hex("#8A8E9A"), Light = UIFactory.Hex("#E6E9F0") };   // 은빛 (스나이퍼)
        }
    }

    public static RelicBar Create(Transform parent)
    {
        RectTransform rt = UIFactory.MakeRect("PlayerInfo", parent);
        UIFactory.Place(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20, -70), new Vector2(640, Height));
        return rt.gameObject.AddComponent<RelicBar>();
    }

    // 판 높이 (아래에 다른 것을 놓을 때 씀)
    public const float Height = 190f;   // 3-94: 132 → 190 (HP 막대 + 아티팩트 2줄)

    // 내용이 바뀌었을 때만 다시 그림. p == null 이면 숨김. canUsePotion = 지금 포션을 쓸 수 있는지 (전투 중 내 차례)
    public void Show(PlayerCombatant p, bool canUsePotion = false)
    {
        gameObject.SetActive(p != null);
        if (p == null) return;
        lastPlayer = p;
        lastCanUse = canUsePotion;
        if (!prefLoaded) { prefLoaded = true; Collapsed = PlayerPrefs.GetInt("InfoCollapsed", 0) == 1; }
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append(Collapsed ? "C" : "F");   // 접힘 상태가 바뀌어도 다시 그리게
        sb.Append(showAllRelics ? "A" : "-");
        sb.Append(p.Name).Append(p.Level).Append(p.Exp).Append(p.StatPoints).Append(p.Hp).Append('/').Append(p.MaxHp).Append(p.Gold).Append(p.Str).Append(p.Con).Append(p.Agi).Append(canUsePotion).Append(':');
        foreach (RelicData r in p.Relics) sb.Append(r.Id).Append(',');
        foreach (string id in p.Potions) sb.Append(id).Append(',');
        string key = sb.ToString();
        if (key == shownKey) return;
        shownKey = key;

        for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);

        RectTransform rt = (RectTransform)transform;
        if (Collapsed && BuildCollapsedArt(p, rt)) return;   // 3-148: 새 그림 띠
        if (Collapsed)
        {
            // 접힌 띠: 이름 · HP 만 한 줄. 띠 전체가 버튼 (누르면 펼침)
            rt.sizeDelta = new Vector2(CollapsedW, CollapsedH);
            Image bar = UIFactory.MakeImage("Back", transform, new Color(0.03f, 0.025f, 0.06f, 0.8f));
            UIFactory.Stretch(bar.rectTransform);
            bar.raycastTarget = true;
            bar.gameObject.AddComponent<Outline>().effectColor = new Color(0.78f, 0.63f, 0.35f, 0.6f);
            Button open = bar.gameObject.AddComponent<Button>();
            open.onClick.AddListener(Toggle);
            string mini = "<b>" + p.Name + " " + ClassLibrary.ColoredClassName(p.Class) + "</b>  <color=#FF8A8A>HP " + p.Hp + "/" + p.MaxHp + "</color>  <color=#C8C0D8>▼ 펼치기</color>";
            Text tm = UIFactory.MakeText("Mini", transform, mini, 19, Color.white, TextAnchor.MiddleLeft);
            UIFactory.Place(tm.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12, 0), new Vector2(CollapsedW - 20, CollapsedH));
            tm.supportRichText = true;
            tm.raycastTarget = false;
            tm.resizeTextForBestFit = true;
            tm.verticalOverflow = VerticalWrapMode.Truncate;   // 3-154: 글자 자동 축소는 세로가 Truncate 일 때만 동작 (Overflow 면 줄지 않고 칸 밖으로 넘침)
            tm.resizeTextMinSize = 12;
            tm.resizeTextMaxSize = 19;
            return;
        }
        rt.sizeDelta = new Vector2(FullW, Height);
        Theme th = ThemeOf(p.Class);

        // ---- 판 (3-94: 직업색 판) ----
        //   Resources/UI/InfoPanel_직업.png 이 있으면 그 그림을 배경으로 (나중에 직업별 배경 그림을 받으면 넣기만 하면 됨)
        //   없으면: 직업색 그라데이션 + 직업 초상화를 흐리게 + 직업색 테두리
        RectTransform clip = UIFactory.MakeRect("Clip", transform);
        UIFactory.Stretch(clip);
        clip.gameObject.AddComponent<RectMask2D>();   // 초상화가 판 밖으로 안 나가게
        Sprite custom = Resources.Load<Sprite>("UI/InfoPanel_" + p.Class);
        Image back = UIFactory.MakeImage("Back", clip, Color.white, false);
        UIFactory.Stretch(back.rectTransform);
        back.raycastTarget = false;
        if (custom != null) back.sprite = custom;
        else
        {
            back.sprite = UIFactory.MakeGradient(th.Dark, new Color(0.04f, 0.03f, 0.06f, 0.94f));
            Sprite portrait = Resources.Load<Sprite>("Art/Portrait_" + p.Class);
            if (portrait != null)
            {
                Image pic = UIFactory.MakeImage("Portrait", clip, new Color(1f, 1f, 1f, 0.22f), false);
                pic.sprite = portrait;
                pic.preserveAspect = true;
                pic.raycastTarget = false;
                UIFactory.Place(pic.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(150, -20), new Vector2(320, 320));
            }
        }
        Outline border = back.gameObject.AddComponent<Outline>();
        border.effectColor = th.Light;
        border.effectDistance = new Vector2(2, -2);

        // ---- 1줄: 이름 + 골드 ----
        Text nameText = UIFactory.MakeText("Name", transform, "<b>" + p.Name + " " + ClassLibrary.ColoredClassName(p.Class) + "</b>", 25, Color.white, TextAnchor.MiddleLeft);
        UIFactory.Place(nameText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14, -6), new Vector2(300, 34));
        nameText.supportRichText = true;
        nameText.raycastTarget = false;
        nameText.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.85f);
        Image coin = UIFactory.MakeCircle("Coin", transform, UIFactory.Hex("#E8B830"));
        UIFactory.Place(coin.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(330, -23), new Vector2(24, 24));
        coin.raycastTarget = false;
        Text coinG = UIFactory.MakeText("G", coin.transform, "G", 15, UIFactory.Hex("#7A5410"), TextAnchor.MiddleCenter);
        UIFactory.Stretch(coinG.rectTransform);
        coinG.fontStyle = FontStyle.Bold;
        coinG.raycastTarget = false;
        Text gold = UIFactory.MakeText("Gold", transform, p.Gold.ToString(), 22, UIFactory.Hex("#FFD27A"), TextAnchor.MiddleLeft);
        UIFactory.Place(gold.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(347, -23), new Vector2(90, 30));
        gold.fontStyle = FontStyle.Bold;
        gold.raycastTarget = false;

        // ---- 2줄: HP 막대 (직업색) + 숫자 ----
        const float barW = 410f, barH = 24f;
        Image barBack = UIFactory.MakeImage("HpBack", transform, new Color(0f, 0f, 0f, 0.6f), false);
        UIFactory.Place(barBack.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14, -44), new Vector2(barW, barH));
        barBack.raycastTarget = false;
        Image barFill = UIFactory.MakeImage("HpFill", barBack.transform, Color.white, false);
        barFill.sprite = UIFactory.MakeGradient(th.Light, th.Mid);
        barFill.raycastTarget = false;
        float ratio = p.MaxHp > 0 ? Mathf.Clamp01((float)p.Hp / p.MaxHp) : 0f;
        barFill.rectTransform.anchorMin = new Vector2(0f, 0f);
        barFill.rectTransform.anchorMax = new Vector2(ratio, 1f);
        barFill.rectTransform.offsetMin = new Vector2(2, 2);
        barFill.rectTransform.offsetMax = new Vector2(-2, -2);
        Text hp = UIFactory.MakeText("Hp", barBack.transform, "HP " + p.Hp + " / " + p.MaxHp, 18, Color.white, TextAnchor.MiddleCenter);
        UIFactory.Stretch(hp.rectTransform);
        hp.fontStyle = FontStyle.Bold;
        hp.raycastTarget = false;
        hp.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.9f);

        // ---- 3줄: 레벨·스탯 ----
        string line2 = "<color=#9CE0FF>Lv " + p.Level + "</color>" + (p.Level < GameBalance.MaxLevel ? " <size=14>(경험치 " + p.Exp + "/" + p.ExpToNext + ")</size>" : " <size=14>(최대)</size>")
                     + "   힘 " + p.Str + "  체력 " + p.Con + "  " + p.AgiName + " " + p.Agi + (p.StatPoints > 0 ? "  <color=#9CFF9C>포인트 " + p.StatPoints + "</color>" : "");
        Text t2 = UIFactory.MakeText("Line2", transform, line2, 17, UIFactory.Hex("#D8D0E4"), TextAnchor.MiddleLeft);
        t2.supportRichText = true;
        UIFactory.Place(t2.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14, -72), new Vector2(barW, 24));
        t2.raycastTarget = false;

        // ---- 아티팩트 칸 (3-94: 2줄 × 12칸 = 24칸, 빈 칸도 보여 줌. 더 많으면 마지막 칸에 +N) ----
        Text relicHead = UIFactory.MakeText("RelicHead", transform, "아티팩트 " + p.Relics.Count, 14, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleLeft);
        UIFactory.Place(relicHead.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14, -97), new Vector2(200, 18));
        relicHead.raycastTarget = false;
        const float size = 30f, gap = 4f;
        const int cols = RelicCols, rows = RelicRows;
        int slots = cols * rows;
        for (int i = 0; i < slots; i++)
        {
            float x = 14 + size / 2f + (i % cols) * (size + gap);
            float y = -115 - size / 2f - (i / cols) * (size + gap);
            bool overflow = i == slots - 1 && p.Relics.Count > slots;
            if (i < p.Relics.Count && !overflow)
            {
                Image icon = RelicView.MakeIcon(transform, p.Relics[i], size, OnHover);
                UIFactory.Place(icon.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(size, size));
            }
            else
            {
                // 빈 칸 (흐린 네모) / 넘치면 "+N"
                Image empty = UIFactory.MakeImage("Empty" + i, transform, new Color(th.Light.r, th.Light.g, th.Light.b, overflow ? 0.35f : 0.12f), false);
                UIFactory.Place(empty.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(size, size));
                empty.raycastTarget = false;
                if (overflow)
                {
                    Text more = UIFactory.MakeText("More", empty.transform, showAllRelics ? "▲" : "+" + (p.Relics.Count - slots + 1), 14, Color.white, TextAnchor.MiddleCenter);
                    UIFactory.Stretch(more.rectTransform);
                    more.fontStyle = FontStyle.Bold;
                    more.raycastTarget = false;
                    // 3-118: "+N" 칸을 누르면 아래에 전체 목록을 펼침 (다시 누르면 접음)
                    empty.raycastTarget = true;
                    Button mb = empty.gameObject.AddComponent<Button>();
                    mb.onClick.AddListener(ToggleAllRelics);
                }
            }
        }

        // 3-118: 넘친 아티팩트 전체 목록 (판 바로 아래에 펼침, 아이콘에 마우스를 올리면 설명)
        if (showAllRelics && p.Relics.Count > slots)
        {
            const int popCols = 14;
            int popRows = (p.Relics.Count + popCols - 1) / popCols;
            float popW = 14 * 2 + popCols * (size + gap) - gap;
            float popH = 34 + popRows * (size + gap) + 10;
            Image pop = UIFactory.MakeImage("AllRelics", transform, new Color(0.04f, 0.03f, 0.07f, 0.95f));
            UIFactory.Place(pop.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0, -6), new Vector2(popW, popH));
            pop.raycastTarget = true;
            pop.gameObject.AddComponent<Outline>().effectColor = th.Light;
            Text ph2 = UIFactory.MakeText("Head", pop.transform, "아티팩트 전체 " + p.Relics.Count + "개  <color=#C8C0D8>(▲ 칸을 누르면 닫힘)</color>", 15, Color.white, TextAnchor.MiddleLeft);
            UIFactory.Place(ph2.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14, -6), new Vector2(popW - 28, 22));
            ph2.supportRichText = true;
            ph2.raycastTarget = false;
            for (int i = 0; i < p.Relics.Count; i++)
            {
                float x = 14 + size / 2f + (i % popCols) * (size + gap);
                float y = -34 - size / 2f - (i / popCols) * (size + gap);
                Image icon = RelicView.MakeIcon(pop.transform, p.Relics[i], size, OnHover);
                UIFactory.Place(icon.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(size, size));
            }
        }

        // 포션 칸 (오른쪽)
        Text ph = UIFactory.MakeText("PotionHead", transform, canUsePotion ? "포션 (눌러서 사용)" : "포션", 15, new Color(1, 1, 1, 0.55f), TextAnchor.MiddleCenter);
        UIFactory.Place(ph.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-10, -6), new Vector2(170, 22));
        ph.raycastTarget = false;
        for (int i = 0; i < GameBalance.PotionSlots; i++)
        {
            PotionData potion = i < p.Potions.Count ? PotionLibrary.Get(p.Potions[i]) : null;
            Image slot = PotionView.MakeIcon(transform, potion, 50f);
            UIFactory.Place(slot.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-150 + i * 56, -62), new Vector2(50, 50));
            if (potion == null) { slot.raycastTarget = false; continue; }
            slot.raycastTarget = true;
            int index = i;
            string tip = potion.Name + "\n" + potion.Description + (canUsePotion ? "\n<color=#9CFF9C>클릭해서 사용</color>" : "\n(전투 중 내 차례에 사용)");
            RectTransform srt = slot.rectTransform;
            HoverRelay hover = slot.gameObject.AddComponent<HoverRelay>();
            hover.OnChange = (on) => { if (OnHover != null) OnHover(on ? tip : null, srt); };
            if (canUsePotion)
            {
                Button b = slot.gameObject.AddComponent<Button>();
                b.onClick.AddListener(() => { if (OnHover != null) OnHover(null, null); if (OnPotion != null) OnPotion(index); });
            }
        }

        // [▲ 접기] 버튼 (오른쪽 아래 구석, 포션 칸 아래) - 3-85
        Button fold = UIFactory.MakeButton("Fold", transform, "▲ 접기", new Color(0.2f, 0.16f, 0.26f, 0.9f), Toggle, 14);
        UIFactory.Place((RectTransform)fold.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-6, 5), new Vector2(66, 22));
    }

    // 3-148: 접힌 띠 (그림 틀). 그림이 없으면 false → 예전 한 줄 띠
    //   틀 그림(474×162 원본) 속 자리: 돌판 안쪽 x 5~97%, 위에서 27~95% / ▼ 단추 x 80~96%, 위에서 30~62%
    public const float BarArtW = 330f, BarArtH = 113f;
    bool BuildCollapsedArt(PlayerCombatant p, RectTransform rt)
    {
        Sprite frame = Resources.Load<Sprite>("UI/Party/Party_Bar");
        if (frame == null) return false;
        rt.sizeDelta = new Vector2(BarArtW, BarArtH);
        Image bar = UIFactory.MakeImage("Back", transform, Color.white, false);
        bar.sprite = frame;
        UIFactory.Stretch(bar.rectTransform);
        bar.raycastTarget = true;
        Button open = bar.gameObject.AddComponent<Button>();
        ColorBlock cb = open.colors;
        cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);   // 마우스를 올리면 살짝 밝게
        open.colors = cb;
        open.onClick.AddListener(Toggle);

        // 위 줄: P1 검은 낫 + 직업 문양
        Text nameLine = UIFactory.MakeText("Name", transform, p.Name + " " + ClassLibrary.ColoredClassName(p.Class), 22, UIFactory.Hex("#F4E2BC"), TextAnchor.MiddleLeft);
        nameLine.rectTransform.anchorMin = new Vector2(0.075f, 0.5f);
        nameLine.rectTransform.anchorMax = new Vector2(0.62f, 0.7f);
        nameLine.rectTransform.offsetMin = nameLine.rectTransform.offsetMax = Vector2.zero;
        nameLine.font = UIFactory.TitleFont;
        nameLine.resizeTextForBestFit = true;
        nameLine.verticalOverflow = VerticalWrapMode.Truncate;   // 3-154: 글자 자동 축소는 세로가 Truncate 일 때만 동작 (Overflow 면 줄지 않고 칸 밖으로 넘침)
        nameLine.resizeTextMinSize = 12;
        nameLine.resizeTextMaxSize = 22;
        nameLine.horizontalOverflow = HorizontalWrapMode.Overflow;
        Sprite emblem = Resources.Load<Sprite>("UI/Party/Party_Emblem_" + p.Class);
        if (emblem != null)
        {
            Image em = UIFactory.MakeImage("Emblem", transform, Color.white, false);
            em.sprite = emblem;
            em.raycastTarget = false;
            em.rectTransform.anchorMin = em.rectTransform.anchorMax = new Vector2(0.69f, 0.6f);
            em.rectTransform.sizeDelta = new Vector2(30, 30);
        }

        // 아래 줄: HP 85/85 + 하트
        Text hp = UIFactory.MakeText("Hp", transform, "HP " + p.Hp + "/" + p.MaxHp, 22, UIFactory.Hex("#FF6A5A"), TextAnchor.MiddleLeft);
        hp.rectTransform.anchorMin = new Vector2(0.11f, 0.12f);
        hp.rectTransform.anchorMax = new Vector2(0.5f, 0.42f);
        hp.rectTransform.offsetMin = hp.rectTransform.offsetMax = Vector2.zero;
        hp.fontStyle = FontStyle.Bold;
        hp.resizeTextForBestFit = true;
        hp.verticalOverflow = VerticalWrapMode.Truncate;   // 3-154: 글자 자동 축소는 세로가 Truncate 일 때만 동작 (Overflow 면 줄지 않고 칸 밖으로 넘침)
        hp.resizeTextMinSize = 12;
        hp.resizeTextMaxSize = 22;
        Sprite heart = Resources.Load<Sprite>("UI/Party/Party_Heart");
        if (heart != null)
        {
            Image h = UIFactory.MakeImage("Heart", transform, Color.white, false);
            h.sprite = heart;
            h.raycastTarget = false;
            h.rectTransform.anchorMin = h.rectTransform.anchorMax = new Vector2(0.55f, 0.27f);
            h.rectTransform.sizeDelta = new Vector2(26, 26);
        }

        // 오른쪽 아래: ▼ 펼치기 (▼ 단추는 그림에 있음)
        Text more = UIFactory.MakeText("More", transform, "▼ 펼치기", 16, UIFactory.Hex("#D8D0C4"), TextAnchor.MiddleRight);
        more.rectTransform.anchorMin = new Vector2(0.62f, 0.1f);
        more.rectTransform.anchorMax = new Vector2(0.95f, 0.36f);
        more.rectTransform.offsetMin = more.rectTransform.offsetMax = Vector2.zero;
        more.fontStyle = FontStyle.Bold;
        return true;
    }

    // 접기 ↔ 펼치기 (3-85)
    void ToggleAllRelics()
    {
        showAllRelics = !showAllRelics;
        if (OnHover != null) OnHover(null, null);
        shownKey = "";
        Show(lastPlayer, lastCanUse);
    }

    void Toggle()
    {
        Collapsed = !Collapsed;
        PlayerPrefs.SetInt("InfoCollapsed", Collapsed ? 1 : 0);
        if (OnHover != null) OnHover(null, null);   // 떠 있던 설명 창 닫기
        shownKey = "";                              // 내용이 같아도 다시 그리게
        Show(lastPlayer, lastCanUse);
        if (OnToggle != null) OnToggle();
    }
}
