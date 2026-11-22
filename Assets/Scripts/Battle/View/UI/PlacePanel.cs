// ============================================================
// PlacePanel.cs
// 비전투 장소 화면 (초소·야영지, 대장간). 가운데 상자 하나에
//   장소 이름 + 아이콘 / 등장인물 대사 (정찰병, 드워프 대장장이) / 설명 / 고를 수 있는 버튼들
// 을 보여 줌. 어떤 버튼을 둘지는 BattleScreen 이 정해서 넘겨 줌 (이 파일은 그리기만)
//
// 3-148: 초소·야영지 새 디자인 (Resources/UI/Camp/*) 이 있으면 모닥불을 누른 뒤
//   위: 모닥불 문양 + 장소 이름 + 금속 장식 / 가운데: 두루마리 (대사·설명) / 아래: 사슬 카드 (위 그림 + 이름 + 설명)
//   그림이 없으면 예전 양피지 상자 그대로
// 자바와 비교: System.Action = 자바의 Runnable (인자 없는 함수를 담는 변수)
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 장소 화면의 버튼 하나
public class PlaceOption
{
    public string Title;          // 버튼 큰 글자 (예: "치료")
    public string Desc;           // 버튼 작은 글자 (예: "모두 최대 HP 30% 회복")
    public bool Enabled = true;   // false 면 회색으로 눌리지 않음
    public System.Action OnClick;
    public string Art;            // 3-148: 카드 위쪽 그림 (Resources 경로). "UI/Camp/Camp_Art_…" 는 그림 칸을 꽉 채우고, 그 밖(아이콘)은 칸 가운데에 작게. 없으면 null

    public PlaceOption(string title, string desc, bool enabled, System.Action onClick)
    {
        Title = title;
        Desc = desc;
        Enabled = enabled;
        OnClick = onClick;
    }
}

public class PlacePanel : MonoBehaviour
{
    private RectTransform box;
    private RectTransform iconHolder;
    private Text titleText;
    private Text speakerText;
    private Text bodyText;
    private RectTransform optionLayer;
    private Text partyText;

    // 장면 모드 (2026-11-14): 배경 그림(야영지)을 화면 가득 보여 주고, 그림 속 모닥불을 누르면 선택 상자가 나옴
    private Image sceneBg;           // 배경 그림
    private RectTransform fireSpot;  // 모닥불 자리의 투명 버튼
    private Image fireGlow;          // 모닥불 위에서 숨 쉬듯 밝아지는 빛
    private Text fireHint;           // "모닥불을 눌러 쉬어 가기"
    private bool fireHover;

    // 보물 상자 모드 (2026-11-15): 화면 가운데 큰 상자. 누르면 OnChestOpen
    private RectTransform chestSpot;
    private Image chestGlow;
    private Text chestHint;
    private bool chestHover;
    private System.Action onChestOpen;
    // 3-88: 닫힌 상자 → 누르면 열린 상자 그림으로 바뀌고 빛이 터진 뒤 아이템 화면으로
    private Image chestArt;
    private Sprite chestClosed, chestOpened;
    private bool chestOpening;      // 열리는 중 (두 번 눌리지 않게, 숨 쉬는 효과 멈춤)
    private float chestOpenTime;    // 열기 시작한 시각 (빛 터지는 효과용)
    public const float ChestOpenDelay = 0.9f;   // 열린 상자를 보여 주는 시간 (초) → 그다음 아이템 화면

    // 3-148: 새 초소 화면
    private RectTransform campBox;        // 모닥불을 누르면 나오는 새 화면 (없으면 null → 예전 box)
    private Text campTitle, campSpeaker, campBody;
    private RectTransform campOptions;
    private bool campMode;                // 지금 보여 주는 장소가 새 초소 화면인지
    private Sprite campCard;
    private List<Image> campGlows = new List<Image>();   // 카드 뒤 빛 (마우스를 올린 카드만 밝게)
    private int campHover = -1;
    // 카드 틀 그림(156×194) 안의 자리 (비율). 위 그림 칸 / 아래 글자 칸
    static readonly Rect CardArtArea = new Rect(0.0705f, 1f - 0.407f, 0.923f - 0.0705f, 0.407f - 0.062f);   // x, y(아래 기준), 폭, 높이
    static readonly Rect CardTextArea = new Rect(0.07f, 1f - 0.954f, 0.86f, 0.954f - 0.49f);

    // 3-155: 배경 그림마다 불(누르는 곳) 자리가 다름. 그림 안의 비율 (왼쪽, 아래, 오른쪽, 위). 없는 그림은 예전 야영지 자리
    //   CampFinal (4스테이지 보스 앞 야영지) = 가운데 둥근 마법진 위의 화로
    static readonly Dictionary<string, Vector4> FireSpots = new Dictionary<string, Vector4>
    {
        { "Backgrounds/Camp", new Vector4(0.39f, 0.24f, 0.60f, 0.62f) },
        { "Backgrounds/CampFinal", new Vector4(0.43f, 0.23f, 0.57f, 0.45f) },
    };

    public static PlacePanel Create(Transform parent)
    {
        RectTransform rt = UIFactory.MakeRect("PlacePanel", parent);
        UIFactory.Stretch(rt);
        PlacePanel panel = rt.gameObject.AddComponent<PlacePanel>();
        panel.BuildStatic();
        rt.gameObject.SetActive(false);
        return panel;
    }

    void BuildStatic()
    {
        // 뒤를 가리는 판 (맨 위 64 = 위쪽 버튼 줄은 가리지 않음)
        Image dim = UIFactory.MakeImage("Dim", transform, Color.white, false);
        dim.sprite = UIFactory.MakeGradient(UIFactory.Hex("#1C1A24"), UIFactory.Hex("#08080C"));
        UIFactory.Stretch(dim.rectTransform);   // 3-203: 화면 전체

        // 장면 배경: 위쪽 버튼 줄(64) 아래를 꽉 채움. 그림 비율(16:9)은 그대로 두고 넘치는 부분은 잘라 냄
        RectTransform sceneClip = UIFactory.MakeRect("SceneClip", transform);
        UIFactory.Stretch(sceneClip);   // 3-203: 위쪽 버튼 줄 뒤까지 장면 그림이 이어지게
        sceneClip.gameObject.AddComponent<RectMask2D>();
        sceneBg = UIFactory.MakeImage("Scene", sceneClip, Color.white, false);
        sceneBg.rectTransform.anchorMin = sceneBg.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        AspectRatioFitter fit = sceneBg.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;   // 부모를 다 덮을 때까지 키움
        fit.aspectRatio = 16f / 9f;
        sceneBg.gameObject.SetActive(false);

        // 모닥불 자리 (그림 안의 비율로 잡아서 화면 크기가 달라도 불 위에 있음)
        fireSpot = UIFactory.MakeRect("FireSpot", sceneBg.transform);
        fireSpot.anchorMin = new Vector2(0.39f, 0.24f);
        fireSpot.anchorMax = new Vector2(0.60f, 0.62f);
        fireSpot.offsetMin = fireSpot.offsetMax = Vector2.zero;
        fireGlow = UIFactory.MakeImage("FireGlow", fireSpot, new Color(1f, 0.6f, 0.2f, 0.3f), false);
        fireGlow.sprite = UIFactory.SoftGlowSprite;
        UIFactory.Stretch(fireGlow.rectTransform, -60, -60, -40, -40);
        fireGlow.raycastTarget = false;
        Image fireHit = UIFactory.MakeImage("FireHit", fireSpot, new Color(0, 0, 0, 0), false);
        UIFactory.Stretch(fireHit.rectTransform);
        fireHit.raycastTarget = true;
        Button fireButton = fireHit.gameObject.AddComponent<Button>();
        fireButton.transition = Selectable.Transition.None;
        fireButton.onClick.AddListener(OnFireClicked);
        HoverRelay fireRelay = fireHit.gameObject.AddComponent<HoverRelay>();
        fireRelay.OnChange = (on) => { fireHover = on; };
        fireHint = UIFactory.MakeText("FireHint", fireSpot, "모닥불을 눌러 쉬어 가기", 30, UIFactory.Hex("#FFE2A8"), TextAnchor.MiddleCenter);
        UIFactory.Place(fireHint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0, -10), new Vector2(520, 44));
        fireHint.fontStyle = FontStyle.Bold;
        fireHint.raycastTarget = false;
        fireHint.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.9f);

        // 보물 상자
        chestSpot = UIFactory.MakeRect("Chest", transform);
        UIFactory.Place(chestSpot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(600, 460));   // 3-88: 새 상자 그림(가로가 긴 그림)에 맞게 키움
        chestGlow = UIFactory.MakeImage("ChestGlow", chestSpot, new Color(1f, 0.8f, 0.3f, 0.3f), false);
        chestGlow.sprite = UIFactory.SoftGlowSprite;
        UIFactory.Stretch(chestGlow.rectTransform, -160, -160, -160, -160);
        chestGlow.raycastTarget = false;
        chestArt = UIFactory.MakeImage("ChestArt", chestSpot, Color.white, false);
        // 3-78: 큰 상자는 지도 아이콘(Map_Treasure)이 아니라 따로 둔 상자 그림(TreasureChest)
        // 3-88: TreasureChest = 새 닫힌 상자, TreasureChestOpen = 열린 상자 (빛이 쏟아짐). 두 그림은 같은 틀이라 바뀔 때 상자가 안 움직임
        chestClosed = Resources.Load<Sprite>("Icons/TreasureChest") ?? Resources.Load<Sprite>("Icons/Map_Treasure");
        chestOpened = Resources.Load<Sprite>("Icons/TreasureChestOpen");
        chestArt.sprite = chestClosed;
        chestArt.preserveAspect = true;
        UIFactory.Stretch(chestArt.rectTransform);
        chestArt.raycastTarget = true;
        Button chestButton = chestArt.gameObject.AddComponent<Button>();
        chestButton.transition = Selectable.Transition.None;
        chestButton.onClick.AddListener(OnChestClicked);
        HoverRelay chestRelay = chestArt.gameObject.AddComponent<HoverRelay>();
        chestRelay.OnChange = (on) => { chestHover = on; };
        chestHint = UIFactory.MakeText("ChestHint", chestSpot, "상자를 눌러 열기", 34, UIFactory.Hex("#FFE2A8"), TextAnchor.MiddleCenter);
        UIFactory.Place(chestHint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0, -20), new Vector2(600, 50));
        chestHint.fontStyle = FontStyle.Bold;
        chestHint.raycastTarget = false;
        chestHint.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.9f);
        chestSpot.gameObject.SetActive(false);

        // 왼쪽: 파티 HP
        Image left = UIFactory.MakeImage("Party", transform, new Color(0.1f, 0.08f, 0.07f, 0.9f));
        UIFactory.Place(left.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(30, -30), new Vector2(330, 260));
        left.raycastTarget = false;
        partyText = UIFactory.MakeText("PartyText", left.transform, "", 22, Color.white, TextAnchor.UpperLeft);
        UIFactory.Stretch(partyText.rectTransform, 20, 20, 18, 18);

        // 가운데 상자 (테두리 + 양피지)
        Image edge = UIFactory.MakeImage("Edge", transform, UIFactory.Hex("#6A4A2A"));
        UIFactory.Place(edge.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(90, -40), new Vector2(1060, 820));
        edge.raycastTarget = false;
        box = edge.rectTransform;
        Image paper = UIFactory.MakeImage("Paper", edge.transform, Color.white);
        paper.sprite = UIFactory.MakeGradient(UIFactory.Hex("#D9C190"), UIFactory.Hex("#B4936A"));
        paper.type = Image.Type.Simple;
        UIFactory.Stretch(paper.rectTransform, 12, 12, 12, 12);
        paper.raycastTarget = false;

        iconHolder = UIFactory.MakeRect("Icon", paper.transform);
        UIFactory.Place(iconHolder, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -80), new Vector2(100, 100));

        titleText = UIFactory.MakeText("Title", paper.transform, "", 46, ChoicePanel.Ink, TextAnchor.MiddleCenter);
        UIFactory.Place(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -158), new Vector2(900, 60));
        titleText.fontStyle = FontStyle.Bold;

        speakerText = UIFactory.MakeText("Speaker", paper.transform, "", 24, UIFactory.Hex("#5A2A1A"), TextAnchor.UpperCenter);
        UIFactory.Place(speakerText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -200), new Vector2(940, 120));
        speakerText.fontStyle = FontStyle.Italic;

        bodyText = UIFactory.MakeText("Body", paper.transform, "", 21, UIFactory.Hex("#3A2A1C"), TextAnchor.UpperCenter);
        UIFactory.Place(bodyText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -320), new Vector2(940, 60));

        optionLayer = UIFactory.MakeRect("Options", paper.transform);
        UIFactory.Place(optionLayer, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 24), new Vector2(980, 380));

        BuildCampBox();
    }

    // 3-148: 새 초소 화면 (목업 1024×572 의 자리를 화면 가운데 기준 좌표로 옮김: 가로 ×1.875, 세로 ×1.8)
    void BuildCampBox()
    {
        campCard = Resources.Load<Sprite>("UI/Camp/Camp_Card");
        Sprite scroll = Resources.Load<Sprite>("UI/Camp/Camp_Scroll");
        if (campCard == null || scroll == null) return;   // 그림이 없으면 예전 상자만 씀

        campBox = UIFactory.MakeRect("CampBox", transform);
        UIFactory.Stretch(campBox, 0, 0, 64, 0);
        Image shade = UIFactory.MakeImage("Shade", campBox, new Color(0f, 0f, 0f, 0.45f), false);   // 배경 그림을 살짝 어둡게
        UIFactory.Stretch(shade.rectTransform, 0, 0, -64, 0);   // 3-203: 위쪽 버튼 줄 뒤(64)까지 같이 어둡게 (top 이 음수 = 상자 밖 위로 늘림)

        Image emblem = UIFactory.MakeImage("Emblem", campBox, Color.white, false);
        emblem.sprite = Resources.Load<Sprite>("UI/Camp/Camp_Emblem");
        emblem.preserveAspect = true;
        emblem.raycastTarget = false;
        UIFactory.Place(emblem.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 392), new Vector2(128, 118));
        if (emblem.sprite == null) emblem.gameObject.SetActive(false);

        campTitle = UIFactory.MakeText("Title", campBox, "", 56, UIFactory.Hex("#F2D49A"), TextAnchor.MiddleCenter);
        UIFactory.Place(campTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 284), new Vector2(700, 80));
        campTitle.font = UIFactory.TitleFont;

        Image deco = UIFactory.MakeImage("Deco", campBox, Color.white, false);
        deco.sprite = Resources.Load<Sprite>("UI/Camp/Camp_TitleDeco");
        deco.raycastTarget = false;
        UIFactory.Place(deco.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 220), new Vector2(440, 44));
        if (deco.sprite == null) deco.gameObject.SetActive(false);

        // 두루마리: 위 = 등장인물 대사 (빨간 강조는 글에 들어 있는 <color>), 아래 = 설명
        Image sc = UIFactory.MakeImage("Scroll", campBox, Color.white, false);
        sc.sprite = scroll;
        sc.raycastTarget = false;
        UIFactory.Place(sc.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 80), new Vector2(1060, 280));
        sc.transform.SetSiblingIndex(deco.transform.GetSiblingIndex());   // 장식이 두루마리 위에 오게 (두루마리를 장식 앞 순서로)
        campSpeaker = UIFactory.MakeText("Speaker", sc.transform, "", 26, UIFactory.Hex("#F4EAD6"), TextAnchor.MiddleCenter);
        UIFactory.Stretch(campSpeaker.rectTransform, 90, 90, 34, 92);
        campSpeaker.fontStyle = FontStyle.Bold;
        campSpeaker.resizeTextForBestFit = true;   // 보스 힌트처럼 긴 대사도 두루마리 안에
        campSpeaker.verticalOverflow = VerticalWrapMode.Truncate;   // 3-154: 글자 자동 축소는 세로가 Truncate 일 때만 동작 (Overflow 면 줄지 않고 칸 밖으로 넘침)
        campSpeaker.resizeTextMinSize = 16;
        campSpeaker.resizeTextMaxSize = 26;
        campBody = UIFactory.MakeText("Body", sc.transform, "", 22, UIFactory.Hex("#E8DCC4"), TextAnchor.MiddleCenter);
        UIFactory.Stretch(campBody.rectTransform, 90, 90, 196, 34);
        campBody.resizeTextForBestFit = true;
        campBody.verticalOverflow = VerticalWrapMode.Truncate;   // 3-154: 글자 자동 축소는 세로가 Truncate 일 때만 동작 (Overflow 면 줄지 않고 칸 밖으로 넘침)
        campBody.resizeTextMinSize = 14;
        campBody.resizeTextMaxSize = 22;

        campOptions = UIFactory.MakeRect("Options", campBox);
        UIFactory.Place(campOptions, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -262), new Vector2(1800, 380));
        // 왼쪽 파티 HP 판이 새 화면 위에 보이게: 새 화면을 파티 판 바로 앞 순서로
        Transform party = transform.Find("Party");
        if (party != null) campBox.SetSiblingIndex(party.GetSiblingIndex());
        campBox.gameObject.SetActive(false);
    }

    // 3-148: 사슬 카드 하나 (틀 + 위 그림 + 이름 + 설명). 고를 수 없으면 어둡게
    void MakeCampCard(PlaceOption opt, int index, Vector2 pos, Vector2 size)
    {
        RectTransform card = UIFactory.MakeRect("Card" + index, campOptions);
        UIFactory.Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);

        Image glow = UIFactory.MakeImage("Glow", card, new Color(1f, 0.6f, 0.2f, 0f), false);
        glow.sprite = UIFactory.SoftGlowSprite;
        UIFactory.Stretch(glow.rectTransform, -size.x * 0.22f, -size.x * 0.22f, -size.y * 0.16f, -size.y * 0.16f);
        glow.raycastTarget = false;
        campGlows.Add(opt.Enabled ? glow : null);

        Color tint = opt.Enabled ? Color.white : new Color(0.5f, 0.5f, 0.5f, 1f);
        Image frame = UIFactory.MakeImage("Frame", card, tint, false);
        frame.sprite = campCard;
        UIFactory.Stretch(frame.rectTransform);

        Sprite art = !string.IsNullOrEmpty(opt.Art) ? Resources.Load<Sprite>(opt.Art) : null;
        if (art != null)
        {
            bool full = opt.Art.StartsWith("UI/Camp/Camp_Art_");   // 카드 그림 = 칸을 꽉 / 아이콘 = 가운데 작게
            RectTransform area = UIFactory.MakeRect("ArtArea", card);
            area.anchorMin = new Vector2(CardArtArea.xMin, CardArtArea.yMin);
            area.anchorMax = new Vector2(CardArtArea.xMax, CardArtArea.yMax);
            area.offsetMin = area.offsetMax = Vector2.zero;
            Image pic = UIFactory.MakeImage("Art", area, tint, false);
            pic.sprite = art;
            pic.raycastTarget = false;
            if (full) UIFactory.Stretch(pic.rectTransform);
            else
            {
                pic.preserveAspect = true;
                UIFactory.Stretch(pic.rectTransform, 0, 0, size.y * 0.03f, size.y * 0.03f);
            }
        }

        RectTransform textArea = UIFactory.MakeRect("TextArea", card);
        textArea.anchorMin = new Vector2(CardTextArea.xMin, CardTextArea.yMin);
        textArea.anchorMax = new Vector2(CardTextArea.xMax, CardTextArea.yMax);
        textArea.offsetMin = textArea.offsetMax = Vector2.zero;
        bool hasDesc = !string.IsNullOrEmpty(opt.Desc);
        Text title = UIFactory.MakeText("Title", textArea, opt.Title, Mathf.RoundToInt(size.y * 0.085f), opt.Enabled ? UIFactory.Hex("#F2D49A") : UIFactory.Hex("#A89C88"), TextAnchor.MiddleCenter);
        title.rectTransform.anchorMin = new Vector2(0f, hasDesc ? 0.6f : 0f);
        title.rectTransform.anchorMax = Vector2.one;
        title.rectTransform.offsetMin = title.rectTransform.offsetMax = Vector2.zero;
        title.font = UIFactory.TitleFont;
        title.resizeTextForBestFit = true;   // 이름이 긴 동료(멀티 집중 치료)도 한 줄에
        title.verticalOverflow = VerticalWrapMode.Truncate;   // 3-154: 글자 자동 축소는 세로가 Truncate 일 때만 동작 (Overflow 면 줄지 않고 칸 밖으로 넘침)
        title.resizeTextMinSize = 14;
        title.resizeTextMaxSize = Mathf.RoundToInt(size.y * 0.085f);
        if (hasDesc)
        {
            Text desc = UIFactory.MakeText("Desc", textArea, opt.Desc, Mathf.RoundToInt(size.y * 0.058f), opt.Enabled ? UIFactory.Hex("#F0E6D2") : UIFactory.Hex("#A89C88"), TextAnchor.UpperCenter);
            desc.rectTransform.anchorMin = Vector2.zero;
            desc.rectTransform.anchorMax = new Vector2(1f, 0.6f);
            desc.rectTransform.offsetMin = new Vector2(4, 4);
            desc.rectTransform.offsetMax = new Vector2(-4, 0);
            desc.resizeTextForBestFit = true;
            desc.verticalOverflow = VerticalWrapMode.Truncate;   // 3-154: 글자 자동 축소는 세로가 Truncate 일 때만 동작 (Overflow 면 줄지 않고 칸 밖으로 넘침)
            desc.resizeTextMinSize = 12;
            desc.resizeTextMaxSize = Mathf.RoundToInt(size.y * 0.058f);
        }

        // 카드 전체가 버튼
        frame.raycastTarget = true;
        Button b = frame.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.interactable = opt.Enabled;
        System.Action click = opt.OnClick;
        b.onClick.AddListener(() => { if (click != null) click(); });
        HoverRelay hover = frame.gameObject.AddComponent<HoverRelay>();
        int idx = index;
        hover.OnChange = (on) => { if (on) campHover = idx; else if (campHover == idx) campHover = -1; };
    }

    // 장소 화면 보이기
    //   iconType: 위쪽 아이콘 (MapNodeType.Rest / Forge)
    //   speaker : 등장인물 대사 (없으면 "")
    //   sceneName: 배경 그림 (Resources 경로, 예: "Backgrounds/Camp"). 있으면 장면 모드 = 모닥불을 눌러야 선택 상자가 나옴
    public void Show(RunState run, MapNodeType iconType, string title, string speaker, string body, List<PlaceOption> options, string sceneName = null)
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        Sprite scene = sceneName != null ? Resources.Load<Sprite>(sceneName) : null;
        sceneBg.sprite = scene;
        if (scene != null)
        {
            // 3-155: 그림 비율과 불 자리를 그림에 맞춤
            sceneBg.GetComponent<AspectRatioFitter>().aspectRatio = scene.rect.width / scene.rect.height;
            Vector4 spot;
            if (!FireSpots.TryGetValue(sceneName, out spot)) spot = FireSpots["Backgrounds/Camp"];
            fireSpot.anchorMin = new Vector2(spot.x, spot.y);
            fireSpot.anchorMax = new Vector2(spot.z, spot.w);
            fireSpot.offsetMin = fireSpot.offsetMax = Vector2.zero;
        }
        sceneBg.gameObject.SetActive(scene != null);
        fireSpot.gameObject.SetActive(scene != null);
        campMode = scene != null && campBox != null && iconType == MapNodeType.Rest;   // 3-148: 초소·야영지 + 새 그림이 있으면 새 화면
        box.gameObject.SetActive(scene == null);   // 장면 모드면 상자는 모닥불을 누른 뒤에
        if (campBox != null) campBox.gameObject.SetActive(false);
        chestSpot.gameObject.SetActive(false);

        for (int i = iconHolder.childCount - 1; i >= 0; i--) Destroy(iconHolder.GetChild(i).gameObject);
        MapNodeView.BuildIcon(iconHolder, iconType, 96f);

        titleText.text = title;
        speakerText.text = speaker;
        bodyText.text = body;

        string s = "<b>파티</b>\n";
        foreach (PlayerCombatant p in run.Players)
        {
            s += p.Name + " " + ClassLibrary.ColoredClassName(p.Class) + "\n   HP " + p.Hp + " / " + p.MaxHp
               + "   " + p.ManaName + " 최대 " + p.MaxMana + "\n";
        }
        partyText.text = s;

        // 버튼: 한 줄에 3개씩, 아래에서부터 쌓지 않고 위에서부터
        for (int i = optionLayer.childCount - 1; i >= 0; i--) Destroy(optionLayer.GetChild(i).gameObject);
        int n = options.Count;
        // 선택지가 0개일 때(보물 상자를 열기 전 등) perRow 가 0 이 되어 0으로 나누기 오류가 났음 → 최소 1
        int perRow = Mathf.Max(1, n <= 4 ? n : 3);
        float w = perRow >= 4 ? 225f : 300f;
        float h = n > 4 ? 104f : 140f;
        float gapX = w + 16f, gapY = h + 14f;
        int rows = (n + perRow - 1) / perRow;
        for (int i = 0; i < n; i++)
        {
            int row = i / perRow, col = i % perRow;
            int inRow = (row == rows - 1) ? n - row * perRow : perRow;   // 마지막 줄은 남은 개수만큼 가운데 정렬
            float x = (col - (inRow - 1) / 2f) * gapX;
            float y = 380f - h / 2f - row * gapY;   // 상자 위쪽부터 (상자 아래 기준 좌표)
            MakeOptionButton(options[i], new Vector2(x, y - 190f), new Vector2(w, h));   // -190 = 상자 가운데 기준으로 바꿈
        }
        if (campMode) FillCampBox(title, speaker, body, options);
    }

    // 3-148: 새 초소 화면 채우기 (카드 수에 따라 크기·간격 조절)
    void FillCampBox(string title, string speaker, string body, List<PlaceOption> options)
    {
        campTitle.text = title;
        campSpeaker.text = speaker;
        campBody.text = body;
        for (int i = campOptions.childCount - 1; i >= 0; i--) Destroy(campOptions.GetChild(i).gameObject);
        campGlows.Clear();
        campHover = -1;
        int n = options.Count;
        // 3-149: 4장(치료·보강·집중 치료·정찰)도 3장과 같은 크기. 5장(멀티 집중 치료 4명 + 뒤로)만 조금 작게
        //   카드 i 의 x = (i - (n-1)/2) × 간격 → 가운데(0)를 기준으로 좌우 대칭 (4장이면 -1.5, -0.5, +0.5, +1.5 칸)
        float k = n <= 4 ? 1f : 0.82f;
        Vector2 size = new Vector2(290f, 360f) * k;
        float gap = size.x + 64f * k;
        for (int i = 0; i < n; i++)
            MakeCampCard(options[i], i, new Vector2((i - (n - 1) / 2f) * gap, 0f), size);
    }

    void MakeOptionButton(PlaceOption opt, Vector2 pos, Vector2 size)
    {
        Color baseColor = opt.Enabled ? UIFactory.Hex("#3A2A1C") : UIFactory.Hex("#7A6A58");
        System.Action click = opt.OnClick;
        Button b = UIFactory.MakeButton("Option", optionLayer, "", baseColor, () => { if (click != null) click(); }, 24);
        b.interactable = opt.Enabled;
        RectTransform rt = (RectTransform)b.transform;
        UIFactory.Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);

        Text label = b.GetComponentInChildren<Text>();
        label.supportRichText = true;
        label.alignment = TextAnchor.MiddleCenter;
        label.fontSize = 22;
        label.text = "<b>" + opt.Title + "</b>\n<size=17>" + opt.Desc + "</size>";
        UIFactory.Stretch(label.rectTransform, 10, 10, 6, 6);
        label.color = opt.Enabled ? UIFactory.Hex("#F4E6C8") : UIFactory.Hex("#D8CCB8");
    }

    // 보물 상자 화면: 어두운 배경 + 가운데 큰 상자. 누르면 onOpen (보상 화면으로)
    public void ShowChest(RunState run, System.Action onOpen)
    {
        Show(run, MapNodeType.Treasure, "", "", "", new List<PlaceOption>());
        box.gameObject.SetActive(false);
        sceneBg.gameObject.SetActive(false);
        fireSpot.gameObject.SetActive(false);
        chestSpot.gameObject.SetActive(true);
        onChestOpen = onOpen;
        // 3-88: 다시 닫힌 상자부터
        chestOpening = false;
        chestArt.sprite = chestClosed;
        chestHint.gameObject.SetActive(true);
    }

    // 상자를 누름 (3-88): 열린 상자 그림 + 빛이 확 터짐 → ChestOpenDelay 초 뒤에 아이템 화면 (onChestOpen)
    //   열린 그림이 없으면 예전처럼 바로 아이템 화면
    void OnChestClicked()
    {
        if (chestOpening || onChestOpen == null) return;
        System.Action open = onChestOpen;
        onChestOpen = null;
        if (chestOpened == null) { open(); return; }
        chestOpening = true;
        chestOpenTime = Time.unscaledTime;
        chestArt.sprite = chestOpened;
        chestHint.gameObject.SetActive(false);
        StartCoroutine(OpenAfterDelay(open));
    }

    // 자바의 Thread.sleep 대신 유니티 코루틴: yield return 으로 기다렸다가 이어서 실행 (화면은 그동안 계속 움직임)
    System.Collections.IEnumerator OpenAfterDelay(System.Action open)
    {
        yield return new WaitForSecondsRealtime(ChestOpenDelay);
        open();
    }

    // 모닥불을 누름 → 선택 상자(치료 / 장비 보강 ...)가 나옴
    void OnFireClicked()
    {
        fireSpot.gameObject.SetActive(false);
        if (campMode) campBox.gameObject.SetActive(true);   // 3-148: 새 초소 화면
        else box.gameObject.SetActive(true);
    }

    // 모닥불을 다시 누르지 않고 선택 상자를 바로 보여 줌 (집중 치료 → 동료 고르기 같은 두 번째 화면)
    public void OpenBoxNow()
    {
        OnFireClicked();
    }

    void Update()
    {
        // 모닥불 빛: 숨 쉬듯 밝아졌다 어두워짐, 마우스를 올리면 더 밝게
        if (chestSpot != null && chestSpot.gameObject.activeInHierarchy && chestOpening)
        {
            // 열리는 중 (3-88): 빛이 확 커졌다가 잦아들고, 상자가 살짝 튀어 오름
            float t = Mathf.Clamp01((Time.unscaledTime - chestOpenTime) / ChestOpenDelay);
            float burst = Mathf.Sin(t * Mathf.PI);                 // 0 → 1 → 0
            chestSpot.localScale = Vector3.one * (1f + 0.06f * burst);
            chestGlow.color = new Color(1f, 0.85f, 0.35f, 0.35f + 0.55f * burst);
        }
        else if (chestSpot != null && chestSpot.gameObject.activeInHierarchy)
        {
            float s = chestHover ? 1.08f : 1f + 0.025f * Mathf.Sin(Time.unscaledTime * 3f);
            chestSpot.localScale = new Vector3(s, s, 1f);
            chestGlow.color = new Color(1f, 0.8f, 0.3f, chestHover ? 0.55f : 0.25f + 0.1f * Mathf.Sin(Time.unscaledTime * 3f));
        }
        // 3-148: 초소 카드 빛 - 평소 은은하게, 마우스를 올린 카드는 밝게
        if (campBox != null && campBox.gameObject.activeInHierarchy)
        {
            float pulse = 0.18f + 0.06f * Mathf.Sin(Time.unscaledTime * 2.5f);
            for (int i = 0; i < campGlows.Count; i++)
                if (campGlows[i] != null) campGlows[i].color = new Color(1f, 0.6f, 0.2f, i == campHover ? 0.6f : pulse);
        }
        if (fireSpot != null && fireSpot.gameObject.activeInHierarchy)
        {
            float a = fireHover ? 0.6f : 0.25f + 0.12f * Mathf.Sin(Time.unscaledTime * 3f);
            fireGlow.color = new Color(1f, 0.6f, 0.2f, a);
            fireHint.color = new Color(1f, 0.89f, 0.66f, fireHover ? 1f : 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 3f));
        }
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
