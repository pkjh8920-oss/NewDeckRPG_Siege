// ============================================================
// ChoicePanel.cs
// 수성(1~3스테이지) 화면: 지도 대신 "다음 칸"의 선택지 카드만 보여 줌
//
//   ??? 칸 : 카드 3장 (쉬운 / 어려운 / 비전투가 섞여 있음). 정찰병 힌트만 보임
//   4칸    : 비전투 5장 (모닥불·대장간·보물·이벤트·상점, 그대로 보임)
//   고정 칸: 카드 1장 (1칸 쉬운 전투 / 6칸 침공 / 9칸 모닥불 / 10칸 보스)
//
// BattleScreen 이 ChoicePanel.Create(...) 로 한 번 만들어 두고,
//   Show(run, showBanner) : 지금 칸의 선택지를 보여 줌
//   Hide()                : 숨김 (전투 화면으로)
//   ShowMessage(text)     : 위쪽에 잠깐 뜨는 안내 (모닥불 회복량 등)
// 카드를 누르면 OnChoiceClicked 로 BattleScreen 에 알려 줌. (MapPanel 과 같은 방식)
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ChoicePanel : MonoBehaviour
{
    public System.Action<SiegeChoice> OnChoiceClicked;

    private RectTransform cardLayer;      // 선택지 카드가 올라가는 곳
    private RectTransform progressLayer;  // 위쪽 칸 진행 표시 (10칸)
    private Text headerText;              // "1스테이지 · 웨어울프족의 침공"
    private Text mottoText;               // 3-109: 스테이지 문구 (헤더 바로 아래)
    private Text guideText;               // "다음 칸을 고르세요" 등
    private Text partyText;               // 왼쪽: 파티 HP
    private Text infoTitle;               // 왼쪽: 마우스를 올린 카드 이름
    private Text infoBody;                // 왼쪽: 설명
    private Text messageText;
    private float messageTimer;
    private CanvasGroup banner;
    private Text bannerText;
    private float bannerTimer;
    private RunState run;
    private Image bgArt;                  // 배경 그림 (3-76). 스테이지마다 바꿀 수 있음 (3-77)
    private Sprite bgDefault;             // 기본 배경 (SiegeChoice.png)

    public static readonly Color Ink = new Color(0.23f, 0.16f, 0.11f);

    public static ChoicePanel Create(Transform parent)
    {
        RectTransform rt = UIFactory.MakeRect("ChoicePanel", parent);
        UIFactory.Stretch(rt);
        ChoicePanel panel = rt.gameObject.AddComponent<ChoicePanel>();
        panel.BuildStatic();
        rt.gameObject.SetActive(false);
        return panel;
    }

    // 바뀌지 않는 부분 (배경, 설명표, 띠)
    void BuildStatic()
    {
        // 뒤의 전투 화면을 가리는 어두운 판 (마우스도 막음). 성벽 위 밤하늘 느낌
        Image dim = UIFactory.MakeImage("Dim", transform, Color.white, false);
        dim.sprite = UIFactory.MakeGradient(UIFactory.Hex("#1A1E2C"), UIFactory.Hex("#07080C"));
        UIFactory.Stretch(dim.rectTransform, 0, 0, 64, 0);   // 맨 위 64 = 위쪽 버튼 줄은 가리지 않음

        // 2026-11-15 (3-76): 배경 그림 (Backgrounds/SiegeChoice.png = 달밤의 성문과 들판)
        //   그림이 있으면: 화면 비율이 달라도 그림 비율은 지키면서 빈틈 없이 꽉 채움 (EnvelopeParent)
        //                 + 위에 살짝 어두운 막 (카드·글자가 잘 보이게) + 아래 성벽 띠는 숨김 (그림에 성벽이 있음)
        //   그림이 없으면: 예전처럼 어두운 그라데이션 + 성벽 띠
        Sprite bgSprite = Resources.Load<Sprite>("Backgrounds/SiegeChoice");
        if (bgSprite != null)
        {
            // 위쪽 64 (버튼 줄) 아래만 보이게 자르는 틀. RectMask2D = 틀 밖으로 나간 그림을 잘라 줌
            RectTransform frame = UIFactory.MakeRect("BgFrame", transform);
            UIFactory.Stretch(frame, 0, 0, 64, 0);
            frame.gameObject.AddComponent<RectMask2D>();
            Image art = UIFactory.MakeImage("BgArt", frame, Color.white, false);
            art.sprite = bgSprite;
            bgArt = art;
            bgDefault = bgSprite;
            art.raycastTarget = false;
            UIFactory.Stretch(art.rectTransform);
            AspectRatioFitter fit = art.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = bgSprite.rect.width / bgSprite.rect.height;
            // 어두운 막: 위는 조금, 아래(카드 자리)는 조금 더 어둡게
            Image shade = UIFactory.MakeImage("BgShade", frame, Color.white, false);
            shade.sprite = UIFactory.MakeGradient(new Color(0.02f, 0.02f, 0.06f, 0.25f), new Color(0.02f, 0.02f, 0.06f, 0.55f));
            shade.raycastTarget = false;
            UIFactory.Stretch(shade.rectTransform);
            dim.color = new Color(0, 0, 0, 0);   // 예전 그라데이션 판은 투명하게 (마우스 막는 역할만 남김)
        }

        // 아래쪽 성벽 (분위기용 띠)
        Image wall = UIFactory.MakeImage("Wall", transform, UIFactory.Hex("#262230"), false);
        UIFactory.Place(wall.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(1920, 120));
        wall.raycastTarget = false;
        for (int i = 0; i < 24; i++)
        {
            // 성벽 위 톱니 모양
            Image merlon = UIFactory.MakeImage("Merlon", wall.transform, UIFactory.Hex("#262230"), false);
            UIFactory.Place(merlon.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(i * 82 + 10, 0), new Vector2(50, 34));
            merlon.raycastTarget = false;
        }
        if (bgSprite != null) wall.gameObject.SetActive(false);   // 배경 그림에 성벽이 있으니 띠는 숨김 (3-76)

        headerText = UIFactory.MakeText("Header", transform, "", 34, UIFactory.Hex("#E8D4A8"), TextAnchor.MiddleCenter);
        UIFactory.Place(headerText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -78), new Vector2(1100, 48));
        headerText.fontStyle = FontStyle.Bold;

        // 3-109: 스테이지 문구 한 줄 (헤더 아래). 그 아래 10칸 진행 줄·안내 글을 조금 내림
        mottoText = UIFactory.MakeText("Motto", transform, "", 23, UIFactory.Hex("#F0C870"), TextAnchor.MiddleCenter);
        UIFactory.Place(mottoText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -124), new Vector2(1200, 30));
        mottoText.fontStyle = FontStyle.BoldAndItalic;
        mottoText.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.85f);

        progressLayer = UIFactory.MakeRect("Progress", transform);
        UIFactory.Place(progressLayer, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -192), new Vector2(1100, 90));

        guideText = UIFactory.MakeText("Guide", transform, "", 26, UIFactory.Hex("#C8C0B0"), TextAnchor.MiddleCenter);
        UIFactory.Place(guideText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -256), new Vector2(1100, 40));

        cardLayer = UIFactory.MakeRect("Cards", transform);
        UIFactory.Place(cardLayer, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(90, -70), new Vector2(1500, 520));

        // 왼쪽: 파티 정보 + 카드 설명
        Image left = UIFactory.MakeImage("InfoBox", transform, new Color(0.1f, 0.08f, 0.07f, 0.9f));
        UIFactory.Place(left.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(30, -60), new Vector2(330, 600));
        left.raycastTarget = false;
        // 3-181: 정보 판 그림 (3번 그림: 해골 모서리 + 덩굴 쇠틀). 글자 자리는 그대로 (틀 안쪽 여백이 이미 20 정도라 맞음)
        Sprite infoArt = Resources.Load<Sprite>("UI/Siege/Siege_InfoPanel");
        if (infoArt != null)
        {
            left.sprite = infoArt;
            left.type = Image.Type.Simple;
            left.color = Color.white;
        }
        partyText = UIFactory.MakeText("Party", left.transform, "", 22, Color.white, TextAnchor.UpperLeft);
        UIFactory.Place(partyText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -20), new Vector2(290, 200));
        infoTitle = UIFactory.MakeText("InfoTitle", left.transform, "", 28, UIFactory.Hex("#FFD27A"), TextAnchor.UpperLeft);
        UIFactory.Place(infoTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -240), new Vector2(290, 40));
        infoTitle.fontStyle = FontStyle.Bold;
        infoBody = UIFactory.MakeText("InfoBody", left.transform, "", 20, UIFactory.Hex("#E0D8C4"), TextAnchor.UpperLeft);
        UIFactory.Place(infoBody.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -290), new Vector2(290, 300));

        messageText = UIFactory.MakeText("Message", transform, "", 28, UIFactory.Hex("#FFE6A0"), TextAnchor.MiddleCenter);
        UIFactory.Place(messageText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(90, 150), new Vector2(1200, 44));
        messageText.fontStyle = FontStyle.Bold;
        messageText.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.9f);

        // 스테이지 시작 띠
        Image band = UIFactory.MakeImage("Banner", transform, new Color(0.14f, 0.1f, 0.12f, 0.9f), false);
        UIFactory.Place(band.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1300, 210));
        band.raycastTarget = false;
        bannerText = UIFactory.MakeText("BannerText", band.transform, "", 80, UIFactory.Hex("#F0C850"), TextAnchor.MiddleCenter);
        UIFactory.Stretch(bannerText.rectTransform);
        bannerText.fontStyle = FontStyle.Bold;
        banner = band.gameObject.AddComponent<CanvasGroup>();
        banner.alpha = 0f;
        banner.blocksRaycasts = false;
    }

    // ---------------- 보여 주기 ----------------
    public void Show(RunState runState, bool showBanner)
    {
        run = runState;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();   // 화면 맨 앞에

        // 스테이지별 배경 (3-77): Backgrounds/SiegeChoice_2.png 처럼 스테이지 번호를 붙인 그림이 있으면 그 그림,
        //   없으면 기본 SiegeChoice.png. 그림 비율이 달라도 AspectRatioFitter 가 맞춰 줌
        if (bgArt != null)
        {
            Sprite stageBg = Resources.Load<Sprite>("Backgrounds/SiegeChoice_" + run.Stage);
            Sprite use = stageBg != null ? stageBg : bgDefault;
            if (bgArt.sprite != use)
            {
                bgArt.sprite = use;
                AspectRatioFitter fit = bgArt.GetComponent<AspectRatioFitter>();
                if (fit != null) fit.aspectRatio = use.rect.width / use.rect.height;
            }
        }

        // 3-144/145: 종족 이름은 화면에 안 보임 → 모든 스테이지 "몬스터" (어떤 무리인지 모르게)
        string race = "몬스터";
        headerText.text = run.Stage + "스테이지  ·  " + race + "의 침공  ·  칸 " + run.Step + " / " + GameBalance.SiegeSteps;
        mottoText.text = StageMotto(run.Stage, race);

        BuildProgress();
        BuildCards();
        RefreshParty();

        infoTitle.text = "";
        infoBody.text = "카드에 마우스를 올리면 설명이 나와요.\n\n??? 칸은 정찰병의 말만 듣고 골라야 해요.\n쉬운 전투, 어려운 전투, 싸움이 없는 칸이 하나씩 섞여 있어요.";

        if (showBanner)
        {
            bannerText.text = run.Stage + "스테이지\n<size=32>" + StageMotto(run.Stage, race) + "</size>";
            bannerTimer = 2.4f;
        }
    }

    // 3-109: 스테이지마다 한 줄 문구 (수성 1·2스테이지). race = "오크족" 처럼 종족 이름
    public static string StageMotto(int stage, string race)
    {
        switch (stage)
        {
            // 3-110: 1스테이지 = 이미 성벽 안까지 뚫고 들어온 몬스터를 몰아냄 / 2스테이지 = 성벽 위에서 밀려오는 몬스터를 막음
            case 1: return "성벽이 뚫렸다! 성 안으로 쏟아져 들어온 " + race + "를 몰아내라";   // 3-144: race = "몬스터" (받침 없음 → 를)
            case 2: return "성벽 위로 올라라! 밀려오는 " + race + "를 성벽에서 막아 내라";   // 3-145: 몬스터 → 를
            default: return race + "의 침공을 막아라";
        }
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void ShowMessage(string text)
    {
        messageText.text = text;
        messageTimer = 3f;
    }

    void RefreshParty()
    {
        // 3-93: 3명 이상이면 한 사람당 한 줄 (이름 + HP), 2명 이하는 예전처럼 두 줄
        bool compact = run.Players.Count > 2;
        string s = "<b>파티</b>\n";
        foreach (PlayerCombatant p in run.Players)
        {
            if (compact) s += p.Name + " " + ClassLibrary.ColoredClassName(p.Class) + "  <size=18><color=#FF9A9A>HP " + p.Hp + "/" + p.MaxHp + "</color></size>\n";
            else s += p.Name + " " + ClassLibrary.ColoredClassName(p.Class) + "\n   HP " + p.Hp + " / " + p.MaxHp + "\n";
        }
        partyText.text = s;
        LayoutInfo();
    }

    // 3-93: 파티 목록 길이에 맞춰 아래 카드 설명(제목·본문)을 밀어 내림
    //   예전에는 설명 제목이 240 자리에 고정이라, 4인 멀티에서 파티 목록(9줄)과 겹쳐 "???" 가 이름 위에 나왔음
    //   preferredHeight = 이 글이 지금 너비에서 실제로 차지하는 높이 (줄바꿈까지 계산해 줌)
    void LayoutInfo()
    {
        float partyH = Mathf.Max(60f, partyText.preferredHeight);
        partyText.rectTransform.sizeDelta = new Vector2(290, partyH);
        float titleY = -20f - partyH - 18f;
        infoTitle.rectTransform.anchoredPosition = new Vector2(0, titleY);
        infoBody.rectTransform.anchoredPosition = new Vector2(0, titleY - 50f);
        infoBody.rectTransform.sizeDelta = new Vector2(290, Mathf.Max(120f, 600f + titleY - 50f - 16f));   // 판(600) 바닥까지
    }

    // 위쪽 칸 진행 표시: 지나온 칸 = 진하게, 지금 칸 = 금색 테두리, 고정 칸은 아이콘
    void BuildProgress()
    {
        for (int i = progressLayer.childCount - 1; i >= 0; i--) Destroy(progressLayer.GetChild(i).gameObject);

        int n = GameBalance.SiegeSteps;
        float gap = Mathf.Min(120f, 1000f / (n - 1));   // 칸이 많아지면 간격을 좁힘 (10칸 = 111)
        for (int step = 1; step <= n; step++)
        {
            float x = (step - (n + 1) / 2f) * gap;

            // 칸 사이 선
            if (step < n)
            {
                Image line = UIFactory.MakeImage("Line", progressLayer, new Color(0.8f, 0.7f, 0.5f, step < run.Step ? 0.8f : 0.25f), false);
                UIFactory.Place(line.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x + 30, 0), new Vector2(gap - 60, 4));
                line.raycastTarget = false;
            }

            bool done = step < run.Step;
            bool current = step == run.Step;

            // 3-81: 칸 동그라미를 그림 아이콘으로 꽉 채움 (예시 그림처럼). 그림이 없으면 예전 동그라미 + 작은 아이콘
            string iconName = StepIconName(step);
            RectTransform spot = UIFactory.MakeRect("Step" + step, progressLayer);
            UIFactory.Place(spot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0), new Vector2(58, 58));
            Image art = MapNodeView.MakeIconArt(spot, iconName, StepIconSize);
            if (art != null)
            {
                // 아직 안 간 칸은 조금 어둡게, 지금 칸은 금빛 고리 (그림 위에 겹쳐서 잘 보이게)
                if (!done && !current) art.color = new Color(0.62f, 0.62f, 0.66f, 1f);
                if (current)
                {
                    Image ring = UIFactory.MakeImage("Current", spot, UIFactory.Hex("#F0C850"), false);
                    ring.sprite = UIFactory.RingSprite;
                    UIFactory.Place(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(StepIconSize + 14, StepIconSize + 14));
                    ring.raycastTarget = false;
                }
            }
            else
            {
                if (current)
                {
                    Image ring = UIFactory.MakeImage("Current", progressLayer, UIFactory.Hex("#F0C850"), false);
                    ring.sprite = UIFactory.RingSprite;
                    UIFactory.Place(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0), new Vector2(74, 74));
                    ring.raycastTarget = false;
                }
                Image disc = UIFactory.MakeCircle("Disc", spot,
                    done ? UIFactory.Hex("#8A6A3A") : (current ? UIFactory.Hex("#D6BC8C") : UIFactory.Hex("#4A4238")));
                UIFactory.Stretch(disc.rectTransform);
                disc.raycastTarget = false;
                string mark = (step == GameBalance.NonCombatStep) ? "쉼" : "?";
                Text q = UIFactory.MakeText("Q", disc.transform, mark, 28, Ink, TextAnchor.MiddleCenter);
                UIFactory.Stretch(q.rectTransform);
                q.fontStyle = FontStyle.Bold;
            }

            Text num = UIFactory.MakeText("Num", progressLayer, step + "칸", 18, new Color(1f, 1f, 1f, current ? 1f : 0.55f), TextAnchor.MiddleCenter);
            UIFactory.Place(num.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, -52), new Vector2(80, 24));
        }
    }

    // 위쪽 10칸 아이콘 크기 (3-81). 그림은 이 값 × MapNodeView.ArtScale 로 놓임 → 테두리가 칸 동그라미만 하게
    const float StepIconSize = 62f;

    // 칸 번호 → 아이콘 그림 이름 (3-81)
    //   고정 칸 = 정해진 아이콘 / ??? 칸 = 지나갔으면 실제로 고른 것, 아직이면 미지(?) / 4칸 = 쉬어 가기(모닥불), 지나갔으면 고른 것
    string StepIconName(int step)
    {
        if (step == GameBalance.SiegeSteps) return "Map_Boss";
        if (step == GameBalance.SiegeRestStep) return "Map_Rest";
        // 3-110: 칸별 구성표의 고정 칸 (일반 / 정예 / 엘리트 / 2웨이브)
        string code = run.StepCode(step);
        if (code == "N" || code == "W") return "Map_Monster";
        if (code == "V") return "Map_Veteran";
        if (code == "E") return "Map_Elite";
        int chosen = run.ChosenAt(step);
        if (chosen >= 0) return MapNodeView.IconNameFor((MapNodeType)chosen);
        if (step == GameBalance.NonCombatStep) return "Map_Rest";
        return "Map_Event";
    }

    // 지금 칸의 선택지 카드
    void BuildCards()
    {
        for (int i = cardLayer.childCount - 1; i >= 0; i--) Destroy(cardLayer.GetChild(i).gameObject);

        List<SiegeChoice> choices = run.Choices;
        int n = choices.Count;
        bool many = n > 3;
        Vector2 size = many ? new Vector2(240, 360) : new Vector2(300, 430);
        float gap = many ? 268f : 360f;

        bool anyHidden = false;
        foreach (SiegeChoice c in choices) if (c.Hidden) anyHidden = true;
        if (n == 1 && choices[0].IsBattle && choices[0].Kind != SiegeChoiceKind.Boss) guideText.text = "피할 수 없는 침공입니다! 카드를 눌러 맞서 싸우세요.";
        else if (n == 1) guideText.text = "이번 칸은 정해져 있어요. 카드를 눌러 진행하세요.";
        else if (anyHidden) guideText.text = "정찰병의 보고를 듣고 다음 칸을 고르세요.";
        else guideText.text = "싸움 없는 칸입니다. 하나를 고르세요.";

        for (int i = 0; i < n; i++)
        {
            RectTransform crt = UIFactory.MakeRect("Choice" + i, cardLayer);
            float x = (i - (n - 1) / 2f) * gap;
            UIFactory.Place(crt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0), size);
            ChoiceCardView view = crt.gameObject.AddComponent<ChoiceCardView>();
            view.Build(choices[i], this, size, i);
        }
    }

    // 이 선택지 카드의 자리 (멀티 투표 표시를 붙이려고). 없으면 null
    public RectTransform RectOf(SiegeChoice choice)
    {
        foreach (ChoiceCardView v in cardLayer.GetComponentsInChildren<ChoiceCardView>())
            if (v.Choice == choice) return (RectTransform)v.transform;
        return null;
    }

    // ---------------- 입력 (ChoiceCardView 가 부름) ----------------
    public void OnCardHover(ChoiceCardView view, bool enter)
    {
        if (!enter) return;
        SiegeChoice c = view.Choice;
        if (c.Hidden)
        {
            infoTitle.text = "???";
            infoBody.text = c.Hint + "\n\n<color=#C8C0B0>무엇이 있을지는 들어가 봐야 알아요.</color>\n\n<color=#9CFF9C>클릭해서 이동</color>";
        }
        else
        {
            infoTitle.text = c.RealName;
            infoBody.text = Describe(c) + "\n\n" + c.Hint + "\n\n<color=#9CFF9C>클릭해서 이동</color>";
        }
    }

    public void OnCardClick(ChoiceCardView view)
    {
        if (bannerTimer > 0.8f) return;   // 띠가 떠 있는 동안은 잠깐 막음
        if (OnChoiceClicked != null) OnChoiceClicked(view.Choice);
    }

    // 가려지지 않은 카드의 설명
    public static string Describe(SiegeChoice c)
    {
        switch (c.Kind)
        {
            case SiegeChoiceKind.EasyFight:
                if (c.Fight == 'V') return "훈련된 정예 몬스터가 몰려옵니다. 일반보다 단단하고 아픕니다.";
                if (c.Fight == 'W') return "일반 몬스터가 두 번에 나눠 몰려옵니다 (2웨이브).\n첫 무리를 다 잡으면 다음 무리가 바로 들어옵니다.";
                return "일반 몬스터 무리가 몰려옵니다.";
            case SiegeChoiceKind.HardFight: return "엘리트가 이끄는 무리입니다. 위험하지만 보상이 좋습니다.";
            case SiegeChoiceKind.Boss: return "침공의 우두머리, 수문장과 싸웁니다.\n3스테이지 수문장을 쓰러뜨리면 지도를 얻습니다.";
            default: return RunMap.Description(c.NodeType);
        }
    }

    void Update()
    {
        if (messageTimer > 0f)
        {
            messageTimer -= Time.deltaTime;
            Color c = messageText.color;
            c.a = Mathf.Clamp01(messageTimer / 0.6f);
            messageText.color = c;
        }

        if (bannerTimer > 0f)
        {
            bannerTimer -= Time.deltaTime;
            float shown = 2.4f - bannerTimer;
            banner.alpha = Mathf.Min(Mathf.Clamp01(shown / 0.3f), Mathf.Clamp01(bannerTimer / 0.6f));
        }
        else
        {
            banner.alpha = 0f;
        }
    }
}
