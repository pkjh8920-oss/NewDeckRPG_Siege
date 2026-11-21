// ============================================================
// RewardPanel.cs
// 전투 보상 / 보물 상자 화면
//
//   위쪽 P1 / P2 탭 = 각자 자기 보상 (한 PC 테스트용. 대장간과 같은 방식)
//   한 사람 화면:
//     골드 +N (이미 받음)
//     아티팩트 획득 (엘리트·보물) 또는 보스 아티팩트 3개 중 1개 고르기
//     카드 3장 중 1장 고르기 (건너뛰기 가능)
//   모두 고르면 [계속] → 다음 칸
//
// 보상 내용(무엇이 나오는지)은 RunState.MakeRewards 가 정함. 이 파일은 그리기와 버튼만
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RewardPanel : MonoBehaviour
{
    public System.Action OnClose;                          // 다 끝나고 [계속]을 누름
    public System.Action<string, RectTransform> OnHover;   // 아티팩트 설명 말풍선 (BattleScreen.ShowTip)

    private RunState run;
    private int current;
    private Text title;
    private RectTransform tabLayer;
    private RectTransform content;   // 지금 플레이어의 보상 (바꿀 때마다 새로 그림)
    private Button continueButton;
    private Text continueLabel;

    public static RewardPanel Create(Transform parent)
    {
        RectTransform rt = UIFactory.MakeRect("RewardPanel", parent);
        UIFactory.Stretch(rt, 0, 0, 64, 0);   // 맨 위 64 = 위쪽 버튼 줄은 가리지 않음
        RewardPanel panel = rt.gameObject.AddComponent<RewardPanel>();
        panel.BuildStatic();
        rt.gameObject.SetActive(false);
        return panel;
    }

    void BuildStatic()
    {
        Image dim = UIFactory.MakeImage("Dim", transform, new Color(0.03f, 0.025f, 0.05f, 0.985f), false);
        UIFactory.Stretch(dim.rectTransform);   // 뒤 화면을 누르지 못하게 막는 역할도 함

        title = UIFactory.MakeText("Title", transform, "", 54, UIFactory.Hex("#FFD27A"), TextAnchor.MiddleCenter);
        UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -20), new Vector2(900, 70));
        title.font = UIFactory.TitleFont;
        title.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
        // 3-181: 제목 아래 장식 선 (2번 그림)
        Sprite divider = Resources.Load<Sprite>("UI/Reward/Reward_Divider");
        if (divider != null)
        {
            Image line = UIFactory.MakeImage("Divider", transform, Color.white, false);
            line.sprite = divider;
            line.preserveAspect = true;
            UIFactory.Place(line.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -88), new Vector2(840, 60));
            line.raycastTarget = false;
        }

        tabLayer = UIFactory.MakeRect("Tabs", transform);
        UIFactory.Place(tabLayer, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -100), new Vector2(1200, 60));

        content = UIFactory.MakeRect("Content", transform);
        UIFactory.Stretch(content, 0, 0, 170, 0);

        continueButton = UIFactory.MakeButton("Continue", transform, "계속", UIFactory.Hex("#3A4A2C"), () => Close(), 30);
        UIFactory.Place((RectTransform)continueButton.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-50, 40), new Vector2(300, 80));
        continueLabel = continueButton.GetComponentInChildren<Text>();
        UIFactory.SkinButton(continueButton, UIFactory.GothicButton);   // 3-181: 8번 공용 틀
    }

    public void Show(RunState runState)
    {
        run = runState;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        switch (run.LastRewardKind)
        {
            case RewardKind.Treasure: title.text = "보물 상자"; break;
            case RewardKind.Boss: title.text = "수문장 격파 보상"; break;
            case RewardKind.Hard: title.text = "엘리트 격파 보상"; break;
            default: title.text = "전투 보상"; break;
        }
        current = FirstNotDone(0);
        if (current < 0) current = 0;
        Redraw();
    }

    public void Hide()
    {
        if (OnHover != null) OnHover(null, null);
        gameObject.SetActive(false);
    }

    int FirstNotDone(int start)
    {
        for (int k = 0; k < run.Rewards.Count; k++)
        {
            int i = (start + k) % run.Rewards.Count;
            if (!run.Rewards[i].Done) return i;
        }
        return -1;
    }

    void Redraw()
    {
        if (OnHover != null) OnHover(null, null);
        BuildTabs();
        for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
        if (run.Rewards.Count == 0) { UpdateContinue(); return; }

        PlayerReward r = run.Rewards[current];
        PlayerCombatant p = r.Player;

        // 골드·경험치·레벨 줄 (3-96: 금화 아이콘 + 골드 / 보라 별 아이콘 + 경험치 / 초록 레벨 업)
        //   HorizontalLayoutGroup = 자식들을 가로로 차례대로 놓아 줌 (자바 Swing 의 FlowLayout 과 비슷)
        //   ContentSizeFitter = 자식 크기만큼 줄의 너비를 맞춤 → 가운데 정렬이 항상 맞음
        RectTransform row = UIFactory.MakeRect("GoldRow", content);
        UIFactory.Place(row, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -10), new Vector2(10, 44));
        HorizontalLayoutGroup hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = 8; hl.childAlignment = TextAnchor.MiddleCenter;
        hl.childControlWidth = true; hl.childControlHeight = true;
        hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
        ContentSizeFitter fit = row.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        if (r.Gold > 0)
        {
            RowIcon(row, UIFactory.Hex("#E8B830"), false, "G");
            RowText(row, "골드 +" + r.Gold + " <size=24>(보유 " + p.Gold + ")</size>", UIFactory.Hex("#FFD27A"));
            if (p.HasRelic(RelicLibrary.TyrantCrown)) RowText(row, "<size=20>폭군의 왕관: 절반만</size>", UIFactory.Hex("#C8B080"));
            RowGap(row, 18);
        }
        if (r.ExpGained > 0)
        {
            RowIcon(row, UIFactory.Hex("#B07CFF"), true, "");
            RowText(row, "경험치 +" + r.ExpGained, UIFactory.Hex("#9CE0FF"));
            RowText(row, r.LevelsGained > 0 ? "레벨 업! Lv " + p.Level : "<size=24>(Lv " + p.Level + ")</size>", r.LevelsGained > 0 ? UIFactory.Hex("#9CFF9C") : UIFactory.Hex("#C8C0D8"));
        }
        if (r.Revived) { RowGap(row, 18); RowText(row, "쓰러졌다가 HP " + GameBalance.ReviveHp + "로 부활", UIFactory.Hex("#FF8A8A")); }

        float y = -70;
        if (r.Relic != null)
        {
            RelicBox(r.Relic, new Vector2(0, y), "아티팩트 획득!", null);
            y -= RelicBoxHeight + 20;
        }
        if (r.Potion != null)
        {
            PotionBox(r.Potion, new Vector2(0, y));
            y -= RelicBoxHeight + 20;
        }
        if (r.BossChoices != null)
        {
            Text head = UIFactory.MakeText("BossHead", content, r.BossDone
                ? (r.TakenBossRelic != null ? "보스 아티팩트: " + r.TakenBossRelic.Name : "보스 아티팩트를 건너뛰었습니다")
                : "보스 아티팩트 1개를 고르세요 (에너지 +1, 대신 페널티)", 26, Color.white, TextAnchor.MiddleCenter);
            UIFactory.Place(head.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(1200, 40));
            y -= 50;
            if (!r.BossDone)
            {
                int n = r.BossChoices.Count;
                for (int i = 0; i < n; i++)
                {
                    int index = i;
                    RelicBox(r.BossChoices[i], new Vector2((i - (n - 1) / 2f) * 470, y), "", () => { run.TakeBossRelic(r, index); AfterPick(r); });
                }
                SmallButton("건너뛰기", new Vector2(0, y - RelicBoxHeight - 16), () => { run.SkipBossRelic(r); AfterPick(r); });
                y -= RelicBoxHeight + 80;
            }
        }

        // 카드
        if (r.CardChoices.Count > 0 || r.TakenCards.Count > 0)
        {
            // 3-113: 4장 중 최대 2장. 고른 카드 이름을 머리말에 보여 줌
            string taken = "";
            foreach (CardData tc in r.TakenCards) taken += (taken.Length > 0 ? ", " : "") + "[" + tc.Name + "]";
            string head = r.CardDone
                ? (r.TakenCards.Count > 0 ? taken + " 을(를) 덱에 넣었습니다" : "카드를 건너뛰었습니다")
                : (r.TakenCards.Count > 0 ? taken + " 넣음  ·  " + r.PicksLeft + "장 더 고를 수 있어요" : "카드를 " + r.PicksLeft + "장까지 고르세요")
                  + (p.HasRelic(RelicLibrary.AlchemistMug) ? "  (연금술 머그: 전부 강화됨)" : "");
            Text cardHead = UIFactory.MakeText("CardHead", content, head, 26, Color.white, TextAnchor.MiddleCenter);
            UIFactory.Place(cardHead.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(1200, 40));
            if (!r.CardDone)
            {
                // 카드 칸: 남은 공간 가운데. 보스 아티팩트를 고르는 중이면 작게
                float scale = (r.BossChoices != null && !r.BossDone) ? 0.72f : 0.95f;
                RectTransform cards = UIFactory.MakeRect("Cards", content);
                UIFactory.Place(cards, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, y - 50 - CardView.Height * scale / 2f - 10), Vector2.zero);
                int n = r.CardChoices.Count;
                for (int i = 0; i < n; i++)
                {
                    int index = i;
                    float x = (i - (n - 1) / 2f) * (CardView.Width + 60) * scale;
                    CardView.CreateDisplay(cards, r.CardChoices[i], p.ManaName, new Vector2(x, 0), scale, () => { run.TakeRewardCard(r, index); AfterPick(r); });
                }
                SmallButton(r.TakenCards.Count > 0 ? "그만 고르기" : "건너뛰기", new Vector2(0, y - 50 - CardView.Height * scale - 60), () => { run.SkipRewardCard(r); AfterPick(r); });
            }
        }
        else if (run.LastRewardKind != RewardKind.Treasure && r.Relic == null && r.BossChoices == null)
        {
            Text none = UIFactory.MakeText("None", content, "받을 카드가 없습니다", 26, Color.gray, TextAnchor.MiddleCenter);
            UIFactory.Place(none.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(800, 40));
        }
        if (run.LastRewardKind == RewardKind.Treasure && r.Relic == null && r.Potion == null)
        {
            Text none = UIFactory.MakeText("Empty", content, "상자가 비어 있었다... (더 얻을 아티팩트가 없고 포션 칸도 가득)", 26, Color.gray, TextAnchor.MiddleCenter);
            UIFactory.Place(none.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(900, 40));
        }
        UpdateContinue();
    }

    // 하나를 고른 뒤: 이 사람이 다 끝났으면 다음 사람으로
    void AfterPick(PlayerReward r)
    {
        if (r.Done)
        {
            int next = FirstNotDone(current + 1);
            if (next >= 0) current = next;
        }
        Redraw();
    }

    void UpdateContinue()
    {
        bool all = run.AllRewardsDone;
        continueButton.interactable = all;
        continueLabel.text = all ? "계속" : "고르는 중...";
    }

    void Close()
    {
        if (!run.AllRewardsDone) return;
        Hide();
        if (OnClose != null) OnClose();
    }

    void BuildTabs()
    {
        for (int i = tabLayer.childCount - 1; i >= 0; i--) Destroy(tabLayer.GetChild(i).gameObject);
        int n = run.Rewards.Count;
        if (n < 2) return;
        float gap = 320f;
        for (int i = 0; i < n; i++)
        {
            PlayerReward r = run.Rewards[i];
            bool on = i == current;
            string label = r.Player.Name + " " + ClassLibrary.ColoredClassName(r.Player.Class) + (r.Done ? "  ✓" : "");
            int index = i;
            // 3-96: 직업색 이름표 (판 = 직업색 그라데이션, 테두리 = 직업 밝은 색, 양 끝에 보석 마름모)
            //   고르고 있는 사람 = 밝고 금테, 나머지 = 어둡게
            RelicBar.Theme th = RelicBar.ThemeOf(r.Player.Class);
            Image tab = UIFactory.MakeImage("Tab" + i, tabLayer, on ? Color.white : new Color(0.62f, 0.62f, 0.66f, 1f), false);
            tab.sprite = UIFactory.MakeGradient(th.Mid, th.Dark);
            UIFactory.Place(tab.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - (n - 1) / 2f) * gap, 0), new Vector2(290, 52));
            Outline to = tab.gameObject.AddComponent<Outline>();
            to.effectColor = on ? UIFactory.Hex("#FFD27A") : th.Light;
            to.effectDistance = on ? new Vector2(3, -3) : new Vector2(1.5f, -1.5f);
            Button tb = tab.gameObject.AddComponent<Button>();
            tb.onClick.AddListener(() => { current = index; Redraw(); });
            Diamond(tab.transform, new Vector2(-145, 0), 22, on ? th.Light : th.Mid);   // 왼쪽 보석
            Diamond(tab.transform, new Vector2(145, 0), 14, on ? UIFactory.Hex("#FFD27A") : th.Light);   // 오른쪽 장식
            Text tl = UIFactory.MakeText("Label", tab.transform, label, 24, on ? Color.white : UIFactory.Hex("#D8D4E0"), TextAnchor.MiddleCenter);
            UIFactory.Stretch(tl.rectTransform, 24, 18, 0, 0);
            tl.fontStyle = FontStyle.Bold;
            tl.raycastTarget = false;
            tl.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.85f);
            if (on) tab.rectTransform.localScale = new Vector3(1.05f, 1.05f, 1f);
        }
    }

    // 아티팩트 상자: 동그라미 + 이름 + 설명. onClick 이 있으면 누를 수 있음
    // 포션 획득 상자 (보물 상자에서)
    void PotionBox(PotionData potion, Vector2 pos)
    {
        Color c = PotionView.ColorOf(potion);
        Image box = UIFactory.MakeImage("PotionBox", content, new Color(0.08f, 0.07f, 0.11f, 0.95f));
        UIFactory.Place(box.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), pos, new Vector2(440, RelicBoxHeight));
        box.gameObject.AddComponent<Outline>().effectColor = c;
        Image icon = PotionView.MakeIcon(box.transform, potion, 80);
        UIFactory.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(58, 0), new Vector2(80, 80));
        icon.raycastTarget = false;
        // 3-105: 아티팩트 상자와 같은 줄 나눔 (머리말 / 이름 / 설명)
        Text hd = UIFactory.MakeText("Header", box.transform, "포션 획득!", 17, UIFactory.Hex("#FFD27A"), TextAnchor.UpperLeft);
        UIFactory.Place(hd.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(108, -8), new Vector2(322, 22));
        hd.fontStyle = FontStyle.Bold;
        Text name = UIFactory.MakeText("Name", box.transform, "<b>" + potion.Name + "</b>", 22, Color.white, TextAnchor.UpperLeft);
        UIFactory.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(108, -32), new Vector2(322, 30));
        name.supportRichText = true;
        name.resizeTextForBestFit = true;
        name.verticalOverflow = VerticalWrapMode.Truncate;   // 3-154: 글자 자동 축소는 세로가 Truncate 일 때만 동작 (Overflow 면 줄지 않고 칸 밖으로 넘침)
        name.resizeTextMinSize = 14;
        name.resizeTextMaxSize = 22;
        Text desc = UIFactory.MakeText("Desc", box.transform, potion.Description + "\n<size=15><color=#AAA0B8>전투 중 왼쪽 위 포션 칸을 눌러 사용</color></size>", 17, UIFactory.Hex("#E0D8C8"), TextAnchor.UpperLeft);
        UIFactory.Place(desc.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(108, -64), new Vector2(322, RelicBoxHeight - 72));
        desc.supportRichText = true;
        desc.resizeTextForBestFit = true;   // 3-154: 긴 설명이 상자 밖으로 넘치지 않게
        desc.resizeTextMinSize = 12;
        desc.resizeTextMaxSize = 17;
        desc.verticalOverflow = VerticalWrapMode.Truncate;
    }

    const float RelicBoxHeight = 150f;   // 3-105: 130 → 150 (머리말·이름·설명을 줄마다 나눔)

    void RelicBox(RelicData relic, Vector2 pos, string header, System.Action onClick)
    {
        Color c = RelicView.RarityColor(relic.Rarity);
        Image box = UIFactory.MakeImage("RelicBox", content, new Color(0.08f, 0.07f, 0.11f, 0.95f));
        UIFactory.Place(box.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), pos, new Vector2(440, RelicBoxHeight));
        box.gameObject.AddComponent<Outline>().effectColor = c;

        Image icon = RelicView.MakeIcon(box.transform, relic, 74, null);
        UIFactory.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(58, 0), new Vector2(74, 74));
        icon.raycastTarget = false;

        // 3-105: 한 줄에 "아티팩트 획득! + 이름 + 등급" 을 다 넣으면 두 줄로 넘쳐서 설명과 겹쳤음
        //   → 머리말(획득!) / 이름·등급 / 설명 을 각자 줄로 나누고, 이름 줄은 길면 글자를 줄여서 한 줄에 맞춤
        float top = -8f;
        if (header.Length > 0)
        {
            Text hd = UIFactory.MakeText("Header", box.transform, header, 17, UIFactory.Hex("#FFD27A"), TextAnchor.UpperLeft);
            UIFactory.Place(hd.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(108, top), new Vector2(322, 22));
            hd.fontStyle = FontStyle.Bold;
            hd.raycastTarget = false;
            top -= 24f;
        }
        string head = "<b>" + relic.Name + "</b>  <size=16><color=#" + ColorUtility.ToHtmlStringRGB(c) + ">[" + relic.RarityName() + "]</color></size>";
        Text name = UIFactory.MakeText("Name", box.transform, head, 22, Color.white, TextAnchor.UpperLeft);
        UIFactory.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(108, top), new Vector2(322, 30));
        name.supportRichText = true;
        name.resizeTextForBestFit = true;   // 이름이 길면 글자를 줄여서 한 줄에
        name.verticalOverflow = VerticalWrapMode.Truncate;   // 3-154: 글자 자동 축소는 세로가 Truncate 일 때만 동작 (Overflow 면 줄지 않고 칸 밖으로 넘침)
        name.resizeTextMinSize = 14;
        name.resizeTextMaxSize = 22;
        name.raycastTarget = false;
        top -= 32f;
        string body = relic.Description + (relic.HasPenalty ? "\n<color=#FF7A7A>페널티: " + relic.Penalty + "</color>" : "");
        Text desc = UIFactory.MakeText("Desc", box.transform, body, 17, UIFactory.Hex("#E0D8C8"), TextAnchor.UpperLeft);
        UIFactory.Place(desc.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(108, top), new Vector2(322, RelicBoxHeight + top - 8f));
        desc.supportRichText = true;
        desc.resizeTextForBestFit = true;
        desc.verticalOverflow = VerticalWrapMode.Truncate;   // 3-154: 글자 자동 축소는 세로가 Truncate 일 때만 동작 (Overflow 면 줄지 않고 칸 밖으로 넘침)
        desc.resizeTextMinSize = 12;
        desc.resizeTextMaxSize = 17;
        desc.raycastTarget = false;

        if (onClick != null)
        {
            Button b = box.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => onClick());
            Outline outline = box.GetComponent<Outline>();
            HoverRelay hover = box.gameObject.AddComponent<HoverRelay>();
            hover.OnChange = (on) =>
            {
                box.color = on ? new Color(0.16f, 0.13f, 0.22f, 0.98f) : new Color(0.08f, 0.07f, 0.11f, 0.95f);
                outline.effectDistance = on ? new Vector2(3, -3) : new Vector2(1, -1);
            };
        }
    }

    // 건너뛰기 버튼 (3-96): 어두운 쇠판 + 금테 두 겹 + 양 끝 금빛 마름모. 마우스를 올리면 밝아짐
    void SmallButton(string label, Vector2 pos, System.Action onClick)
    {
        // 3-181: [건너뛰기] 그림 버튼 (2번 그림: 육각 쇠틀 + 남색 판). 그림이 없으면 예전 그라데이션 판
        Sprite skipArt = Resources.Load<Sprite>("UI/Reward/Reward_Skip");
        if (skipArt != null)
        {
            Image art = UIFactory.MakeImage(label, content, Color.white, false);
            art.sprite = skipArt;
            art.preserveAspect = true;
            UIFactory.Place(art.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), pos, new Vector2(272, 84));   // 그림 비율 약 3.3 : 1
            Text at = UIFactory.MakeText("Label", art.transform, label, 28, UIFactory.Hex("#F0E6D2"), TextAnchor.MiddleCenter);
            UIFactory.Stretch(at.rectTransform);
            at.font = UIFactory.TitleFont;
            at.raycastTarget = false;
            Outline ao = at.gameObject.AddComponent<Outline>();
            ao.effectColor = new Color(0, 0, 0, 0.85f);
            ao.effectDistance = new Vector2(1.5f, -1.5f);
            Button ab = art.gameObject.AddComponent<Button>();
            ColorBlock acb = ab.colors;
            acb.highlightedColor = new Color(1.25f, 1.2f, 1.1f, 1f);
            acb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            ab.colors = acb;
            ab.onClick.AddListener(() => onClick());
            return;
        }
        Image plate = UIFactory.MakeImage(label, content, Color.white, false);
        plate.sprite = UIFactory.MakeGradient(UIFactory.Hex("#3A3A44"), UIFactory.Hex("#16161C"));
        UIFactory.Place(plate.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), pos, new Vector2(260, 62));
        Outline o1 = plate.gameObject.AddComponent<Outline>();
        o1.effectColor = UIFactory.Hex("#C9A24A");
        o1.effectDistance = new Vector2(2, -2);
        Image inner = UIFactory.MakeImage("Inner", plate.transform, new Color(0, 0, 0, 0), false);
        UIFactory.Stretch(inner.rectTransform, 5, 5, 5, 5);
        inner.raycastTarget = false;
        Outline o2 = inner.gameObject.AddComponent<Outline>();
        o2.effectColor = new Color(0.79f, 0.64f, 0.29f, 0.55f);
        o2.effectDistance = new Vector2(1, -1);
        Diamond(plate.transform, new Vector2(-132, 0), 20, UIFactory.Hex("#E2B866"));
        Diamond(plate.transform, new Vector2(132, 0), 20, UIFactory.Hex("#E2B866"));
        Text t = UIFactory.MakeText("Label", plate.transform, label, 26, UIFactory.Hex("#F0E6D2"), TextAnchor.MiddleCenter);
        UIFactory.Stretch(t.rectTransform);
        t.fontStyle = FontStyle.Bold;
        t.raycastTarget = false;
        Button b = plate.gameObject.AddComponent<Button>();
        ColorBlock cb = b.colors;
        cb.highlightedColor = new Color(1.25f, 1.2f, 1.1f, 1f);
        cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        b.colors = cb;
        b.onClick.AddListener(() => onClick());
    }

    // 마름모 장식 (네모를 45도 돌림) - 3-96
    static void Diamond(Transform parent, Vector2 pos, float size, Color color)
    {
        Image d = UIFactory.MakeImage("Gem", parent, color, false);
        UIFactory.Place(d.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
        d.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
        d.raycastTarget = false;
        Outline o = d.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(0, 0, 0, 0.7f);
        o.effectDistance = new Vector2(1, -1);
    }

    // 골드 줄 조각들 (3-96)
    static void RowText(RectTransform row, string text, Color color)
    {
        Text t = UIFactory.MakeText("T", row, text, 30, color, TextAnchor.MiddleCenter);
        t.supportRichText = true;
        t.fontStyle = FontStyle.Bold;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.raycastTarget = false;
        t.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
    }

    // 동그란 금화(G) 또는 별(마름모 빛) 아이콘
    static void RowIcon(RectTransform row, Color color, bool star, string letter)
    {
        RectTransform holder = UIFactory.MakeRect("Icon", row);
        LayoutElement le = holder.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = 34; le.preferredHeight = 34;
        if (star)
        {
            Image glow = UIFactory.MakeImage("Glow", holder, new Color(color.r, color.g, color.b, 0.35f), false);
            glow.sprite = UIFactory.SoftGlowSprite;
            UIFactory.Stretch(glow.rectTransform, -10, -10, -10, -10);
            glow.raycastTarget = false;
            Diamond(holder, Vector2.zero, 20, color);
            Image core = UIFactory.MakeImage("Core", holder, Color.white, false);
            UIFactory.Place(core.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(7, 7));
            core.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            core.raycastTarget = false;
        }
        else
        {
            Image coin = UIFactory.MakeCircle("Coin", holder, color);
            UIFactory.Stretch(coin.rectTransform);
            coin.raycastTarget = false;
            coin.gameObject.AddComponent<Outline>().effectColor = UIFactory.Hex("#7A5410");
            Text g = UIFactory.MakeText("L", coin.transform, letter, 20, UIFactory.Hex("#7A5410"), TextAnchor.MiddleCenter);
            UIFactory.Stretch(g.rectTransform);
            g.fontStyle = FontStyle.Bold;
            g.raycastTarget = false;
        }
    }

    static void RowGap(RectTransform row, float w)
    {
        RectTransform gapRt = UIFactory.MakeRect("Gap", row);
        gapRt.gameObject.AddComponent<LayoutElement>().preferredWidth = w;
    }
}
