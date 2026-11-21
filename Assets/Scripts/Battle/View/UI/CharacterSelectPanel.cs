// ============================================================
// CharacterSelectPanel.cs
// 캐릭터 선택 화면 — "상태창(STATUS)" 스타일 (2026-11-09, 스타일 2 시안)
//
//   ┌ STATUS ◆ ───────────────────────────── 캐릭터 선택 ┐
//   │ [초상화]   이름 / 직업 · AD·AP          능력 경향 그래프 │
//   │  "대사"    HP 막대 / 자원 막대 / 시작 덱 · 난이도 ★     │
//   │ [고유 특성 3개] [플레이 스타일 3줄] [대표 카드 3장]      │
//   └─────────────────────────────────────────────┘
//      [뒤로]        [캐릭터 칸 3 + 잠긴 칸 2]        [플레이]
//
// 초상화: Resources/Art/Portrait_직업이름.png 가 있으면 그것, 없으면 전투 그림
// 능력 경향 점수·대사·난이도는 화면에 보여 주는 소개용 숫자 (전투 규칙과 관계 없음, 가안)
// HP·자원 최대치·카드 이름·비용은 실제 게임 데이터를 읽어서 보여 줌
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CharacterSelectPanel : MonoBehaviour
{
    public System.Action<PlayerClass> OnPlay;            // 싱글: [플레이]
    public System.Action OnBack;                         // 싱글: [뒤로]
    public System.Action<List<PlayerClass>> OnPlayMulti; // 멀티: 모두 고른 뒤 [게임 시작] (P1, P2 ... 순서의 직업)
    public System.Action OnBackMulti;                    // 멀티: 방 화면으로
    public int Str, Con, Agi;        // 화면에 보여 줄 HP 계산용 (Inspector 스탯)

    static readonly PlayerClass[] Classes = { PlayerClass.BlackScythe, PlayerClass.CosmicPhoenix, PlayerClass.FrostMage, PlayerClass.Sniper };   // 2026-11-15 스나이퍼 추가
    const int LockedSlots = 1;   // 잠긴 칸 (다음 캐릭터 자리)

    // 능력 경향 축 (위에서부터 시계 방향)
    static readonly string[] RadarAxes = { "공격력", "생존", "난이도", "협동", "폭발력" };

    // ---------------- 캐릭터 소개 내용 ----------------
    class Profile
    {
        public string Sub;              // "사신 · AD (물리)"
        public string Quote;
        public string WeaponDesc;       // 무기 설명 (무기 이름은 ClassLibrary.WeaponName 에서 읽음)
                                        // 초상화에는 무기를 안 그리고, 무슨 무기를 쓰는지는 이 글로 알려 줌
        public int[] Radar;             // RadarAxes 순서, 0~10
        public int Stars;               // 난이도 ★ (1~5)
        public string[,] Traits;        // [i,0] 이름, [i,1] 설명
        public string[] Style;
        public System.Func<CardData>[] Cards;   // 대표 카드 (CardLibrary 에서 실제 카드를 만들어 이름·비용을 읽음)
        public Color Accent;
        public Color BgTop;
        public float Hue;               // 화면 전체 색조 (0 = 빨강, 0.57 = 하늘색). -1 = 원래 보라 그대로
    }

    static Profile GetProfile(PlayerClass c)
    {
        switch (c)
        {
            case PlayerClass.CosmicPhoenix:
                return new Profile
                {
                    Sub = "수호 기사 · AD (물리)",
                    Quote = "\"내가 서 있는 한, 아무도 쓰러지지 않는다.\"",
                    WeaponDesc = "방패와 함께 쓰는 창 · 찌르고, 방패로 아군을 지킴",
                    Radar = new[] { 6, 9, 4, 9, 6 },
                    Stars = 2,
                    Traits = new string[,]
                    {
                        { "발화", "적 턴 시작에 스택만큼 피해, 1 감소" },
                        { "홍염", "모았다가 재의 소멸로 한 번에 터뜨림" },
                        { "화염 장벽", "아군 피해 -40%, 대신 내가 1.5배" },
                    },
                    Style = new[] { "발화를 쌓아 시간이 지날수록 강해짐", "아군 앞에서 피해를 대신 받음", "체력 스탯이 곧 공격력" },
                    Cards = new System.Func<CardData>[] { () => CardLibrary.FlameWall(), () => CardLibrary.Sunspot(), () => CardLibrary.PhoenixNova() },
                    Accent = UIFactory.Hex("#FF4A36"),   // 2026-11-09: 주황 → 붉은색 (화면 전체가 붉게)
                    BgTop = UIFactory.Hex("#3A0A0A"),
                    Hue = 0.0f,
                };
            case PlayerClass.FrostMage:
                return new Profile
                {
                    Sub = "마도사 · AP (마법)",
                    Quote = "\"얼어붙어라. 그리고 벼락을 기다려라.\"",
                    WeaponDesc = "얼음과 번개 마법을 쏘는 지팡이",
                    Radar = new[] { 7, 3, 8, 7, 10 },
                    Stars = 4,
                    Traits = new string[,]
                    {
                        { "동결 → 동상", "동결이 가득 차면 적의 행동을 취소" },
                        { "감전", "감전 스택 4개면 2턴 동안 적 피해 -20%" },
                        { "[벼락]", "모은 마나를 낙뢰로 한 번에 쏟아부음" },
                    },
                    Style = new[] { "적을 얼려 공격을 끊어 냄", "마나를 모았다가 한 번에 터뜨림", "HP가 낮아 방어 카드 관리가 중요" },
                    Cards = new System.Func<CardData>[] { () => CardLibrary.GlacierCut(), () => CardLibrary.LightningFall(), () => CardLibrary.AbsoluteZero() },
                    Accent = UIFactory.Hex("#6AC8FF"),
                    BgTop = UIFactory.Hex("#0C1A2E"),
                    Hue = 0.57f,
                };
            case PlayerClass.Sniper:
                return new Profile
                {
                    Sub = "진홍의 저격수 · AD (원거리)",
                    Quote = "\"한 발이면 충분해. 표식이 쌓였다면.\"",
                    WeaponDesc = "탄환을 쓰는 대형 저격총",
                    Radar = new[] { 9, 5, 6, 3, 8 },
                    Stars = 4,
                    Traits = new string[,]
                    {
                        { "탄환", "시작 3 / 최대 6. 민첩 5마다 최대 +2, 매 턴 충전 +1" },
                        { "[조준]", "다음 공격 카드 피해 +50% (겹치면 더 커짐)" },
                        { "[표식]", "적에게 최대 10. 1개당 그 적이 받는 피해 +1" },
                    },
                    Style = new[] { "표식을 쌓고 큰 한 방으로 끝냄", "탄환을 아껴 쓰는 자원 관리", "자힐이 없어 방어 카드가 중요" },
                    Cards = new System.Func<CardData>[] { () => CardLibrary.Headshot(), () => CardLibrary.Spray(), () => CardLibrary.DoomShot() },
                    Accent = UIFactory.Hex("#E8B04A"),
                    BgTop = UIFactory.Hex("#24180A"),
                    Hue = 0.1f,
                };
            default:
                return new Profile
                {
                    Sub = "사신 · AD (물리)",
                    Quote = "\"죽음은 공평하다. 순서만 다를 뿐.\"",
                    WeaponDesc = "큰 낫을 휘둘러 베는 근접 무기",
                    Radar = new[] { 8, 4, 7, 5, 9 },
                    Stars = 3,
                    Traits = new string[,]
                    {
                        { "처형 스택", "스택이 남은 HP보다 많아지면 즉시 처형" },
                        { "밤", "밤이 되면 공격·윤무·반월참이 강해짐" },
                        { "죽음의 인장", "인장 건 적에게 내 공격 2배" },
                    },
                    Style = new[] { "처형 스택을 쌓아 한 번에 끝냄", "영혼을 모아 큰 카드로 터뜨림", "방어는 약함 — 빨리 잡는 쪽" },
                    Cards = new System.Func<CardData>[] { () => CardLibrary.DeathSentence(), () => CardLibrary.HalfMoon(), () => CardLibrary.DarkNightSlash() },
                    Accent = UIFactory.Hex("#A870FF"),
                    BgTop = UIFactory.Hex("#16102A"),
                    Hue = -1f,
                };
        }
    }

    static string ArtName(PlayerClass c)
    {
        switch (c)
        {
            case PlayerClass.BlackScythe: return "BlackScythe";
            case PlayerClass.CosmicPhoenix: return "CosmicPhoenix";
            case PlayerClass.FrostMage: return "FrostMage";
            case PlayerClass.Sniper: return "Sniper";
            default: return "";
        }
    }

    // ---------------- 화면 부품 ----------------
    const float PanelW = 1620f, PanelH = 810f;

    private PlayerClass selected = PlayerClass.BlackScythe;
    private Image background;
    private RectTransform panel;
    private List<Image> accentParts = new List<Image>();     // 직업 색으로 바뀌는 부품들
    private List<Text> accentTexts = new List<Text>();
    private Image portrait;
    private Image portraitGlow;
    private Text quoteText, nameText, subText, hpValue, resLabel, resValue, deckText, weaponText;
    private Image hpFill, resFrame;
    private RadarChart radar;
    private List<Text> radarValues = new List<Text>();
    private Text[] traitNames = new Text[3];
    private Text[] traitDescs = new Text[3];
    private Text[] styleLines = new Text[3];
    private RectTransform[] cardHolders = new RectTransform[3];   // 대표 카드 자리 (실제 카드 모양을 작게 넣음)

    // 직업마다 화면 색을 바꾸기 위해, 처음 만든 색(보라)을 기억해 둠
    private Dictionary<Graphic, Color> baseColors = new Dictionary<Graphic, Color>();
    private Dictionary<Outline, Color> baseOutlines = new Dictionary<Outline, Color>();
    private float themeHue = -1f;
    private List<Image> slotFrames = new List<Image>();
    private Image playButtonImage;
    private Button backButton;                 // 3-189: 직업 색 그림 버튼으로 바꾸려고 기억
    private bool csArt;                        // 3-189: UI/CharSelect 그림이 있는지
    private Button playButton;
    private Text playLabel;

    // ---- 멀티 (한 PC에서 P1 → P2 → P3 차례로 고르기, 같은 직업은 한 명만) ----
    private bool multi;
    private int multiCount;
    private string multiCode = "";
    private List<PlayerClass> picks = new List<PlayerClass>();   // 지금까지 고른 직업 (번호 = P 번호 - 1)
    private List<Image> slotBadges = new List<Image>();          // 캐릭터 칸 위 "P1" 표시
    private List<Text> slotBadgeTexts = new List<Text>();
    private List<Image> slotDims = new List<Image>();            // 이미 고른 칸을 어둡게
    private Text turnText;                                       // "P2 차례 — 캐릭터를 고르세요"
    private Text captionText;                                    // 오른쪽 위 "캐릭터 선택" / "방 ABCDE"

    public static CharacterSelectPanel Create(Transform parent)
    {
        RectTransform rt = UIFactory.MakeRect("CharacterSelect", parent);
        UIFactory.Stretch(rt);
        CharacterSelectPanel p = rt.gameObject.AddComponent<CharacterSelectPanel>();
        p.Build();
        p.CaptureTheme();
        rt.gameObject.SetActive(false);
        return p;
    }

    // 패널 왼쪽 위 기준 좌표로 놓기 (x 오른쪽, y 아래쪽)
    static void PlaceTL(RectTransform rt, float x, float y, float w, float h)
    {
        UIFactory.Place(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(w, h));
    }

    Text PanelText(string name, Transform parent, string content, int size, Color color, TextAnchor align, float x, float y, float w, float h)
    {
        Text t = UIFactory.MakeText(name, parent, content, size, color, align);
        PlaceTL(t.rectTransform, x, y, w, h);
        return t;
    }

    Image Line(Transform parent, float x, float y, float w, float h, Color color)
    {
        Image img = UIFactory.MakeImage("Line", parent, color, false);
        PlaceTL(img.rectTransform, x, y, w, h);
        img.raycastTarget = false;
        return img;
    }

    void Build()
    {
        // 배경: 직업 색이 살짝 섞인 어두운 그라데이션 + 가장자리 어둡게
        background = UIFactory.MakeImage("Background", transform, Color.white, false);
        UIFactory.Stretch(background.rectTransform);
        Image vig = UIFactory.MakeImage("Vignette", transform, new Color(0, 0, 0, 0.7f), false);
        vig.sprite = UIFactory.VignetteSprite;
        UIFactory.Stretch(vig.rectTransform);
        vig.raycastTarget = false;

        // 메인 패널
        Image pan = UIFactory.MakeImage("Panel", transform, new Color(0.055f, 0.047f, 0.086f, 0.94f));
        panel = pan.rectTransform;
        UIFactory.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 85), new Vector2(PanelW, PanelH));
        Outline po = pan.gameObject.AddComponent<Outline>();
        po.effectColor = new Color(0.47f, 0.39f, 0.63f, 1f);
        po.effectDistance = new Vector2(2, -2);

        // 모서리 꺾쇠 (직업 색)
        float L = 30f, T = 3f;
        float[,] corners = { { 6, 6 }, { PanelW - 6 - L, 6 }, { 6, PanelH - 6 - T }, { PanelW - 6 - L, PanelH - 6 - T } };
        for (int i = 0; i < 4; i++)
        {
            accentParts.Add(Line(panel, corners[i, 0], corners[i, 1], L, T, Color.white));
            float vx = (i % 2 == 0) ? 6 : PanelW - 6 - T;
            float vy = (i < 2) ? 6 : PanelH - 6 - L;
            accentParts.Add(Line(panel, vx, vy, T, L, Color.white));
        }

        // 머리줄
        PanelText("Status", panel, "STATUS  ◆", 22, UIFactory.Hex("#C8BEE6"), TextAnchor.MiddleLeft, 30, 18, 300, 36);
        captionText = PanelText("Caption", panel, "캐릭터 선택", 22, UIFactory.Hex("#968CB4"), TextAnchor.MiddleRight, PanelW - 430, 18, 400, 36);
        Line(panel, 24, 62, PanelW - 48, 1.5f, UIFactory.Hex("#4B3F66"));

        // 초상화 (뒤에 직업 색 빛)
        portraitGlow = UIFactory.MakeImage("PortraitGlow", panel, Color.white, false);
        portraitGlow.sprite = UIFactory.SoftGlowSprite;
        PlaceTL(portraitGlow.rectTransform, 210 - 220, 80 - 60, 440, 475);
        portraitGlow.raycastTarget = false;
        Image frame = UIFactory.MakeImage("PortraitFrame", panel, UIFactory.Hex("#120E1C"), false);
        PlaceTL(frame.rectTransform, 210 - 142, 74, 284, 367);
        frame.gameObject.AddComponent<Outline>().effectColor = UIFactory.Hex("#6E5A96");
        portrait = UIFactory.MakeImage("Portrait", frame.transform, Color.white, false);
        UIFactory.Stretch(portrait.rectTransform, 6, 6, 6, 6);
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;
        quoteText = PanelText("Quote", panel, "", 21, UIFactory.Hex("#BEB4D2"), TextAnchor.MiddleCenter, 10, 448, 400, 40);

        // 이름 / 직업 / 막대들
        nameText = PanelText("Name", panel, "", 70, UIFactory.Hex("#F2C46A"), TextAnchor.MiddleLeft, 430, 78, 760, 104);
        subText = PanelText("Sub", panel, "", 26, UIFactory.Hex("#C8BEDC"), TextAnchor.MiddleLeft, 434, 184, 600, 40);

        PanelText("HpLabel", panel, "HP", 24, UIFactory.Hex("#DCD7EB"), TextAnchor.MiddleLeft, 430, 248, 100, 36);
        Image hpBack = UIFactory.MakeImage("HpBack", panel, UIFactory.Hex("#231E30"));
        PlaceTL(hpBack.rectTransform, 540, 256, 360, 20);
        hpFill = UIFactory.MakeImage("HpFill", hpBack.transform, UIFactory.Hex("#DC505A"));
        UIFactory.Stretch(hpFill.rectTransform);
        hpValue = PanelText("HpValue", panel, "", 24, UIFactory.Hex("#DCD7EB"), TextAnchor.MiddleLeft, 920, 248, 160, 36);

        resLabel = PanelText("ResLabel", panel, "", 24, UIFactory.Hex("#DCD7EB"), TextAnchor.MiddleLeft, 430, 293, 100, 36);
        Image resBack = UIFactory.MakeImage("ResBack", panel, UIFactory.Hex("#231E30"));
        PlaceTL(resBack.rectTransform, 540, 301, 360, 20);
        resFrame = resBack;
        Outline ro = resBack.gameObject.AddComponent<Outline>();
        ro.effectColor = UIFactory.Hex("#826EDC");
        resValue = PanelText("ResValue", panel, "", 24, UIFactory.Hex("#DCD7EB"), TextAnchor.MiddleLeft, 920, 293, 160, 36);

        deckText = PanelText("Deck", panel, "", 24, UIFactory.Hex("#DCD7EB"), TextAnchor.MiddleLeft, 430, 338, 700, 40);
        deckText.supportRichText = true;

        // 무기 줄 (덱·난이도 줄 바로 아래)
        weaponText = PanelText("Weapon", panel, "", 24, UIFactory.Hex("#DCD7EB"), TextAnchor.MiddleLeft, 430, 383, 820, 40);
        weaponText.supportRichText = true;

        // 능력 경향 그래프
        float rcx = PanelW - 250, rcy = 325, rr = 128;
        accentTexts.Add(PanelText("RadarTitle", panel, "능력 경향", 24, Color.white, TextAnchor.MiddleCenter, rcx - 150, 82, 300, 40));
        GameObject rgo = new GameObject("Radar", typeof(RectTransform));
        rgo.transform.SetParent(panel, false);
        radar = rgo.AddComponent<RadarChart>();
        PlaceTL(radar.rectTransform, rcx - rr, rcy - rr, rr * 2, rr * 2);
        radar.raycastTarget = false;
        for (int i = 0; i < RadarAxes.Length; i++)
        {
            Vector2 dir = RadarChart.Direction(i, RadarAxes.Length);
            float lx = rcx + dir.x * rr * 1.28f, ly = rcy - dir.y * rr * 1.28f;
            PanelText("Axis" + i, panel, RadarAxes[i], 22, UIFactory.Hex("#D2C8E6"), TextAnchor.MiddleCenter, lx - 70, ly - 26, 140, 30);
            radarValues.Add(PanelText("AxisValue" + i, panel, "", 22, UIFactory.Hex("#F2C46A"), TextAnchor.MiddleCenter, lx - 40, ly + 2, 80, 28));
        }

        // 아래 3칸
        float by = 500, bh = PanelH - 24 - by, gap = 20, bw = (PanelW - 48 - gap * 2) / 3;
        string[] titles = { "고유 특성", "플레이 스타일", "대표 카드" };
        RectTransform[] boxes = new RectTransform[3];
        for (int b = 0; b < 3; b++)
        {
            Image box = UIFactory.MakeImage("Box" + b, panel, UIFactory.Hex("#14111E"));
            PlaceTL(box.rectTransform, 24 + b * (bw + gap), by, bw, bh);
            box.gameObject.AddComponent<Outline>().effectColor = UIFactory.Hex("#463C64");
            boxes[b] = box.rectTransform;
            accentTexts.Add(PanelText("Title", box.transform, "◆ " + titles[b], 22, Color.white, TextAnchor.MiddleLeft, 18, 8, bw - 36, 36));
            Line(box.transform, 14, 46, bw - 28, 1.5f, UIFactory.Hex("#3C3255"));
        }

        for (int i = 0; i < 3; i++)
        {
            float yy = 62 + i * 70;
            Image ring = UIFactory.MakeImage("TraitRing", boxes[0], Color.white, false);
            ring.sprite = UIFactory.RingSprite;
            PlaceTL(ring.rectTransform, 20, yy + 4, 30, 30);
            accentParts.Add(ring);
            Text dot = PanelText("TraitDot", boxes[0], "◆", 13, Color.white, TextAnchor.MiddleCenter, 20, yy + 4, 30, 30);
            accentTexts.Add(dot);
            traitNames[i] = PanelText("TraitName", boxes[0], "", 23, UIFactory.Hex("#F0E6FA"), TextAnchor.MiddleLeft, 64, yy - 2, bw - 80, 32);
            traitDescs[i] = PanelText("TraitDesc", boxes[0], "", 18, UIFactory.Hex("#AAA0BE"), TextAnchor.MiddleLeft, 64, yy + 28, bw - 80, 28);

            styleLines[i] = PanelText("Style", boxes[1], "", 22, UIFactory.Hex("#E1DAF0"), TextAnchor.MiddleLeft, 22, 64 + i * 58, bw - 40, 40);

            // 대표 카드: 실제 카드 모양을 작게 (Select 에서 채움). 마우스를 올리면 크게
            float slotW = (bw - 48) / 3;
            RectTransform holder = UIFactory.MakeRect("CardHolder" + i, boxes[2]);
            PlaceTL(holder, 24 + i * slotW + slotW / 2f, 64 + (bh - 84) / 2f, 0, 0);
            cardHolders[i] = holder;
        }

        // 아래 캐릭터 칸
        int total = Classes.Length + LockedSlots;
        for (int i = 0; i < total; i++)
        {
            float x = (i - (total - 1) / 2f) * 138f;
            Image fr = UIFactory.MakeImage("Slot" + i, transform, UIFactory.Hex("#1C1828"));
            UIFactory.Place(fr.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(x, 30), new Vector2(116, 140));
            Image inner = UIFactory.MakeImage("Inner", fr.transform, UIFactory.Hex("#0C0A12"));
            UIFactory.Stretch(inner.rectTransform, 6, 6, 6, 6);
            inner.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            if (i < Classes.Length)
            {
                PlayerClass cls = Classes[i];
                Image face = UIFactory.MakeImage("Face", inner.transform, Color.white, false);
                Sprite ps = Resources.Load<Sprite>("Art/Portrait_" + ArtName(cls));
                face.sprite = ps != null ? ps : Resources.Load<Sprite>("Art/" + ArtName(cls));
                face.preserveAspect = true;
                if (ps != null) UIFactory.Stretch(face.rectTransform);
                else UIFactory.Place(face.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, 12), new Vector2(230, 300));
                face.raycastTarget = false;
                Button b = fr.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => Select(cls));
                slotFrames.Add(fr);

                // 멀티: 이미 고른 칸은 어둡게 + 위에 "P1" 표시
                Image dim = UIFactory.MakeImage("TakenDim", inner.transform, new Color(0f, 0f, 0f, 0.55f), false);
                UIFactory.Stretch(dim.rectTransform);
                dim.raycastTarget = false;
                dim.gameObject.SetActive(false);
                slotDims.Add(dim);
                Image badge = UIFactory.MakeImage("PBadge", fr.transform, Color.white);
                UIFactory.Place(badge.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(64, 32));
                badge.raycastTarget = false;
                badge.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
                Text bt = UIFactory.MakeText("P", badge.transform, "", 22, UIFactory.Hex("#140C18"), TextAnchor.MiddleCenter);
                UIFactory.Stretch(bt.rectTransform);
                bt.fontStyle = FontStyle.Bold;
                badge.gameObject.SetActive(false);
                slotBadges.Add(badge);
                slotBadgeTexts.Add(bt);
            }
            else if (i == Classes.Length && System.Array.IndexOf(Classes, PlayerClass.Sniper) < 0 && Resources.Load<Sprite>("Art/Portrait_Sniper") != null)
            {
                // 2026-11-15: 새 캐릭터 스나이퍼 (카드가 아직 없어서 고를 수 없음 → "개발 중")
                Image face = UIFactory.MakeImage("Face", inner.transform, new Color(0.55f, 0.5f, 0.55f, 1f), false);   // 살짝 어둡게
                face.sprite = Resources.Load<Sprite>("Art/Portrait_Sniper");
                face.preserveAspect = true;
                UIFactory.Stretch(face.rectTransform);
                face.raycastTarget = false;
                Image band = UIFactory.MakeImage("DevBand", inner.transform, new Color(0.05f, 0.03f, 0.08f, 0.85f), false);
                UIFactory.Place(band.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 8), new Vector2(116, 30));
                band.raycastTarget = false;
                Text dev = UIFactory.MakeText("Dev", band.transform, "개발 중", 18, UIFactory.Hex("#FF8A9A"), TextAnchor.MiddleCenter);
                UIFactory.Stretch(dev.rectTransform);
                dev.fontStyle = FontStyle.Bold;
                Text name = UIFactory.MakeText("Name", inner.transform, ClassLibrary.ClassName(PlayerClass.Sniper), 17, UIFactory.Hex(ClassLibrary.ClassColorHex(PlayerClass.Sniper)),   // 3-169: 고유 색
                     TextAnchor.MiddleCenter);
                UIFactory.Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -16), new Vector2(116, 26));
                name.fontStyle = FontStyle.Bold;
                name.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.9f);
                fr.color = UIFactory.Hex("#3A1820");
            }
            else
            {
                Image shackle = UIFactory.MakeImage("Shackle", inner.transform, UIFactory.Hex("#55505F"), false);
                shackle.sprite = UIFactory.RingSprite;
                UIFactory.Place(shackle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 8), new Vector2(40, 46));
                Image body = UIFactory.MakeImage("LockBody", inner.transform, UIFactory.Hex("#55505F"));
                UIFactory.Place(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -16), new Vector2(54, 40));
            }
        }

        Button back = UIFactory.MakeButton("Back", transform, "뒤로", UIFactory.Hex("#282434"), OnBackPressed, 30);
        UIFactory.Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(60, 40), new Vector2(220, 80));
        backButton = back;
        // 3-189: 직업 색 그림 버튼 (카인 보라 · 이그니스 붉은 · 제피르 푸른 · 실비아 금빛). 그림 비율 약 1.4 : 1 이라 버튼을 조금 키움
        csArt = Resources.Load<Sprite>("UI/CharSelect/CS_Play_Kain") != null;
        if (csArt) ((RectTransform)back.transform).sizeDelta = new Vector2(190, 134);
        if (!csArt) UIFactory.SkinButton(back, UIFactory.GothicButton);   // 3-181: 8번 공용 버튼 틀 (3-189: 직업 그림이 있으면 그쪽)

        // 멀티 차례 안내 (캐릭터 칸 바로 위)
        //   플레이 버튼 바로 위 (아래 캐릭터 칸의 P 배지와 안 겹치게)
        turnText = UIFactory.MakeText("Turn", transform, "", 26, Color.white, TextAnchor.MiddleRight);
        UIFactory.Place(turnText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60, 140), new Vector2(560, 70));
        turnText.supportRichText = true;
        turnText.fontStyle = FontStyle.Bold;

        Button play = UIFactory.MakeButton("Play", transform, "플레이", UIFactory.Hex("#7846C8"), OnPlayPressed, 36);
        playButton = play;
        playLabel = play.GetComponentInChildren<Text>();
        playLabel.resizeTextForBestFit = true;
        playLabel.verticalOverflow = VerticalWrapMode.Truncate;   // 3-154: 글자 자동 축소는 세로가 Truncate 일 때만 동작 (Overflow 면 줄지 않고 칸 밖으로 넘침)
        playLabel.resizeTextMinSize = 18;
        playLabel.resizeTextMaxSize = 36;
        UIFactory.Place((RectTransform)play.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60, 40), new Vector2(260, 90));
        Outline plo = play.gameObject.AddComponent<Outline>();
        plo.effectColor = UIFactory.Hex("#F2C46A");
        plo.effectDistance = new Vector2(2, -2);
        playButtonImage = play.GetComponent<Image>();
        if (csArt)
        {
            // 3-191: 그림 버튼이 커져서(높이 162) 위 안내 글자("P1 차례 — 캐릭터를 고르세요")와 겹침 → 글자를 버튼 위로 올림
            UIFactory.Place(turnText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60, 215), new Vector2(560, 70));
            plo.enabled = false;
            ((RectTransform)play.transform).sizeDelta = new Vector2(230, 162);
            playLabel.rectTransform.offsetMin = new Vector2(30, 30);   // 글자는 안쪽 판 안에서만 (틀 위로 안 넘어가게)
            playLabel.rectTransform.offsetMax = new Vector2(-30, -30);
        }
    }

    // 싱글플레이
    public void Show(PlayerClass start)
    {
        multi = false;
        picks.Clear();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Select(System.Array.IndexOf(Classes, start) >= 0 ? start : Classes[0]);
    }

    // 멀티플레이: players 명이 P1 부터 차례로 고름 (같은 직업은 한 명만)
    public void ShowMulti(int players, string roomCode)
    {
        multi = true;
        multiCount = Mathf.Clamp(players, 1, Classes.Length);
        multiCode = roomCode;
        picks.Clear();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Select(Classes[0]);
    }

    bool IsTaken(PlayerClass cls) { return picks.Contains(cls); }

    // 아직 아무도 안 고른 첫 직업 (지금 보고 있는 것부터)
    PlayerClass FirstFree(PlayerClass from)
    {
        int start = System.Array.IndexOf(Classes, from);
        for (int k = 0; k < Classes.Length; k++)
        {
            PlayerClass c = Classes[(start + k) % Classes.Length];
            if (!IsTaken(c)) return c;
        }
        return from;
    }

    void OnPlayPressed()
    {
        if (!multi) { if (OnPlay != null) OnPlay(selected); return; }
        if (picks.Count >= multiCount)
        {
            if (OnPlayMulti != null) OnPlayMulti(new List<PlayerClass>(picks));
            return;
        }
        if (IsTaken(selected)) return;   // 다른 사람이 고른 직업은 못 고름
        picks.Add(selected);
        Select(picks.Count < multiCount ? FirstFree(selected) : selected);   // 다음 사람은 남은 직업부터 보여 줌
    }

    void OnBackPressed()
    {
        if (!multi) { if (OnBack != null) OnBack(); return; }
        if (picks.Count > 0)
        {
            // 마지막으로 고른 사람 취소 → 그 사람 차례로 돌아감
            PlayerClass last = picks[picks.Count - 1];
            picks.RemoveAt(picks.Count - 1);
            Select(last);
            return;
        }
        if (OnBackMulti != null) OnBackMulti();
    }

    // 멀티 표시: 칸 위 P 배지, 고른 칸 어둡게, 차례 안내, 버튼 글자
    void RefreshMulti()
    {
        for (int i = 0; i < Classes.Length && i < slotBadges.Count; i++)
        {
            int who = picks.IndexOf(Classes[i]);
            bool taken = multi && who >= 0;
            slotBadges[i].gameObject.SetActive(taken);
            slotDims[i].gameObject.SetActive(taken);
            if (taken)
            {
                slotBadges[i].color = TitlePanel.PlayerColor(who);
                slotBadgeTexts[i].text = "P" + (who + 1);
            }
        }

        if (!multi)
        {
            turnText.text = "";
            captionText.text = "캐릭터 선택";
            playLabel.text = "플레이";
            playButton.interactable = true;
            return;
        }

        captionText.text = "방 " + multiCode + "  ·  " + multiCount + "명";
        if (picks.Count < multiCount)
        {
            int n = picks.Count;
            string hex = ColorUtility.ToHtmlStringRGB(TitlePanel.PlayerColor(n));
            turnText.text = "<color=#" + hex + ">P" + (n + 1) + "</color> 차례 — 캐릭터를 고르세요\n<size=20>(" + n + " / " + multiCount + "명 고름)</size>";
            int owner = picks.IndexOf(selected);
            if (owner >= 0)
            {
                playLabel.text = "P" + (owner + 1) + "이(가) 고름";
                playButton.interactable = false;
            }
            else
            {
                playLabel.text = "P" + (n + 1) + " 선택";
                playButton.interactable = true;
            }
        }
        else
        {
            turnText.text = "모두 골랐습니다!\n<size=20>[뒤로] = 마지막 선택 취소</size>";
            playLabel.text = "게임 시작";
            playButton.interactable = true;
        }
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    // ---------------- 직업 색 테마 ----------------
    // 만든 직후 모든 그림·글자·테두리의 처음 색을 기억
    void CaptureTheme()
    {
        foreach (Graphic g in GetComponentsInChildren<Graphic>(true)) if (!baseColors.ContainsKey(g)) baseColors[g] = g.color;
        foreach (Outline o in GetComponentsInChildren<Outline>(true)) if (!baseOutlines.ContainsKey(o)) baseOutlines[o] = o.effectColor;
    }

    // 보라 계열 색만 골라서 색조(hue)를 바꿈. 금색 이름, 빨간 HP, 흰 글자는 그대로
    //   Color.RGBToHSV = 색을 색조·채도·밝기로 나눔 (자바의 java.awt.Color.RGBtoHSB 와 같은 일)
    static Color Retint(Color c, float hue)
    {
        if (hue < 0f) return c;
        float h, sat, v;
        Color.RGBToHSV(c, out h, out sat, out v);
        if (sat < 0.08f || h < 0.6f || h > 0.88f) return c;
        Color r = Color.HSVToRGB(hue, Mathf.Min(1f, sat * 1.15f), v);
        r.a = c.a;
        return r;
    }

    string RetintHex(string hex)
    {
        return ColorUtility.ToHtmlStringRGB(Retint(UIFactory.Hex(hex), themeHue));
    }

    void ApplyTheme(float hue)
    {
        themeHue = hue;
        foreach (KeyValuePair<Graphic, Color> kv in baseColors) if (kv.Key != null) kv.Key.color = Retint(kv.Value, hue);
        foreach (KeyValuePair<Outline, Color> kv in baseOutlines) if (kv.Key != null) kv.Key.effectColor = Retint(kv.Value, hue);
        radar.GridColor = Retint(new Color(0.4f, 0.35f, 0.55f, 0.6f), hue);
    }

    void Select(PlayerClass cls)
    {
        selected = cls;
        Profile p = GetProfile(cls);
        Color acc = p.Accent;
        ApplyTheme(p.Hue);   // 먼저 화면 전체 색 (피닉스 = 붉은 화면), 그 위에 직업 색 부품

        background.sprite = UIFactory.MakeGradient(p.BgTop, UIFactory.Hex("#060509"));
        foreach (Image img in accentParts) img.color = acc;
        foreach (Text t in accentTexts) t.color = acc;
        portraitGlow.color = new Color(acc.r, acc.g, acc.b, 0.45f);
        if (csArt)
        {
            // 3-189: 고른 직업 색 그림으로 바꿔 끼움 (SkinButton 은 같은 "Skin" 칸을 다시 씀)
            string key = cls == PlayerClass.CosmicPhoenix ? "Ignis" : cls == PlayerClass.FrostMage ? "Zephyr" : cls == PlayerClass.Sniper ? "Sylvia" : "Kain";
            UIFactory.SkinButton(playButton, "UI/CharSelect/CS_Play_" + key, 1f, false);
            UIFactory.SkinButton(backButton, "UI/CharSelect/CS_Back_" + key, 1f, false);
        }
        else playButtonImage.color = Color.Lerp(acc, Color.black, 0.35f);

        Sprite ps = Resources.Load<Sprite>("Art/Portrait_" + ArtName(cls));
        portrait.sprite = ps != null ? ps : Resources.Load<Sprite>("Art/" + ArtName(cls));
        quoteText.text = p.Quote;

        // HP·자원은 실제 플레이어를 만들어서 읽음 (GameBalance 와 항상 같게)
        PlayerCombatant preview = ClassLibrary.CreatePlayer(cls, "P1", Str, Con, Agi, false);
        nameText.text = ClassLibrary.ClassName(cls);
        nameText.color = UIFactory.Hex(ClassLibrary.ClassColorHex(cls));   // 3-169: 큰 이름도 캐릭터 고유 색 (예전엔 모두 금색)
        subText.text = p.Sub;
        hpValue.text = preview.MaxHp + "/" + preview.MaxHp;
        resLabel.text = preview.ManaName;
        int startRes = preview.StartMana();
        resValue.text = startRes + "/" + preview.MaxMana;
        resFrame.GetComponent<Outline>().effectColor = acc;
        string stars = "<color=#F2C46A>" + new string('★', p.Stars) + "</color><color=#" + RetintHex("#5A5070") + ">" + new string('★', 5 - p.Stars) + "</color>";
        deckText.text = "시작 덱 " + preview.Deck.Count + "장    ·    난이도 " + stars;

        // 무기: "무기  사신의 낫  - 큰 낫을 휘둘러 베는 근접 무기" (무기 이름만 직업 색)
        //   ColorUtility.ToHtmlStringRGB = Color 를 "A870FF" 같은 글자로 바꿔 줌 (리치 텍스트 색 지정용)
        string accHex = ColorUtility.ToHtmlStringRGB(acc);
        weaponText.text = "무기    <color=#" + accHex + ">" + ClassLibrary.WeaponName(cls) + "</color>"
                        + "<color=#" + RetintHex("#9A90B0") + ">  -  " + p.WeaponDesc + "</color>";

        float[] values = new float[p.Radar.Length];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = p.Radar[i] / 10f;
            radarValues[i].text = p.Radar[i].ToString();
        }
        radar.FillColor = new Color(acc.r, acc.g, acc.b, 0.45f);
        radar.LineColor = acc;
        radar.SetValues(values);

        for (int i = 0; i < 3; i++)
        {
            traitNames[i].text = p.Traits[i, 0];
            traitDescs[i].text = p.Traits[i, 1];
            styleLines[i].text = "▶  " + p.Style[i];
            CardData card = p.Cards[i]();
            // 대표 카드: 실제 카드 모양 (등급 테두리·비용·설명). 마우스를 올리면 크게 보임
            for (int k = cardHolders[i].childCount - 1; k >= 0; k--) Destroy(cardHolders[i].GetChild(k).gameObject);
            CardView cv = CardView.CreateDisplay(cardHolders[i], card, preview.ManaName, Vector2.zero, 0.6f, null);
            RectTransform holderRt = cardHolders[i];
            HoverRelay hover = cv.gameObject.AddComponent<HoverRelay>();
            hover.OnChange = (on) =>
            {
                cv.TargetScale = on ? 1.0f : 0.6f;
                cv.TargetPos = on ? new Vector2(0, 40) : Vector2.zero;
                if (on) holderRt.SetAsLastSibling();
            };
        }

        for (int i = 0; i < slotFrames.Count; i++)
        {
            bool on = Classes[i] == cls;
            slotFrames[i].color = on ? acc : Retint(UIFactory.Hex("#1C1828"), themeHue);
            slotFrames[i].rectTransform.localScale = on ? new Vector3(1.08f, 1.08f, 1f) : Vector3.one;
        }
        RefreshMulti();
    }
}
