// ============================================================
// CardView.cs
// 화면에 보이는 카드 한 장입니다. (규칙 정보는 CardData, 이 클래스는 "보이는 모습"만)
//
// 하는 일
//   1) 카드 모양 만들기 (종류별 색, 코스트, 이름, 설명)
//   2) 목표 위치·각도·크기를 향해 매 프레임 부드럽게 움직이기
//   3) 마우스 올리기 / 드래그 이벤트를 받아서 BattleScreen 에 알려 주기
//
// IPointerEnterHandler 같은 인터페이스를 구현하면,
// 유니티가 마우스가 카드에 들어올 때 OnPointerEnter 를 자동으로 불러 줍니다.
// (자바의 implements MouseListener 와 비슷한 개념)
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class CardView : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public const float Width = 230f;
    public const float Height = 320f;

    // [System.NonSerialized] = 유니티가 Inspector에 저장하려고 하지 않게 함
    // (CardData 는 일반 C# 클래스라 저장 대상이 아님 → 경고 UAC1001 방지)
    [System.NonSerialized] public CardData Data;
    [System.NonSerialized] public BattleScreen Owner;
    public RectTransform Rt;

    // BattleScreen 이 매 프레임 정해 주는 목표값. 카드는 여기를 향해 부드럽게 움직임
    public Vector2 TargetPos;
    public float TargetAngle;
    public float TargetScale = 1f;
    public float MoveSpeed = 14f;

    public bool IsLeaving;       // 손에서 나가는 중 (버림 더미로 날아가는 중)
    private float leaveTimer;
    private float showTimer;     // 낸 카드를 화면 가운데에 보여 주는 남은 시간
    private Vector2 finalDest;
    private float finalScale;
    public static readonly Vector2 ShowcasePos = new Vector2(0, 430);

    // 3-182 연출 2단계: 카드 사용 연출
    //   낸 카드가 가운데로 올라오면: 카드 뒤에 직업 색 빛이 숨쉬듯 번쩍이고, 도착 순간 빛 고리 + 불똥 + 반짝이가 터짐
    //   버림 더미로 날아갈 때: 빛이 빨리 사라지면서 작은 반짝이가 흩어짐
    //   카드 뒤 빛은 카드의 "형제"(같은 부모)로 만들어서 카드 바로 뒤에 그림 (자식으로 만들면 카드 위에 그려져서)
    public Color CastColor = new Color(1f, 0.85f, 0.5f, 1f);   // BattleScreen 이 직업 색으로 바꿔 줌
    private Image castGlow;
    private float castAge;
    private bool castBurstDone, castFadeDone;   // 낸 카드가 잠깐 멈추는 곳 (손패 기준점 기준)

    private Image hit;           // 투명한 판: 마우스 이벤트를 받는 부분
    private Image shadow;        // 카드 그림자 (마우스를 올리면 멀어져서 카드가 떠 보임)
    private Image glow;          // 카드 바깥의 빛 (쓸 수 있는 카드면 초록색으로 깜빡임)
    private RectTransform shine; // 마우스를 따라 움직이는 반사광 (반짝이는 느낌)
    private Image shineImage;

    // 3D 기울기: 마우스가 카드 위 어디에 있는지에 따라 카드가 그쪽으로 기울어짐
    private bool pointerInside;
    private Vector2 pointerLocal;      // 카드 가운데 기준 마우스 위치
    private float tiltX, tiltY;        // 지금 기울기 (도)
    private float currentAngle;        // 지금 z 회전 (부채꼴 각도)
    private Image frame;         // 종류 색 테두리
    private bool playable;
    private Image costOrb;
    private GameObject costRingObj;
    private bool hasCardArt;     // 완성된 카드 그림을 쓰는지 (Resources/Cards/카드ID.png)
    private Text costText;
    private Image manaBadge;
    private Text manaText;
    private Image boundBadge;   // 3-102: 영혼 결속 마녀가 "결속"을 건 카드 표시 (3-173: 사슬 그림)
    private bool boundPulse;    // 3-173: 그림 덮개면 은은하게 깜빡임
    private Image sealBadge;    // 3-196 지옥 "사용 금지" (붉은 사슬 + 자물쇠 + "사용 금지" 띠)

    // 카드 종류별 색
    public static Color TypeColor(CardType type)
    {
        switch (type)
        {
            case CardType.Attack: return UIFactory.Hex("#9B3B3B");
            case CardType.Skill: return UIFactory.Hex("#3B6A9B");
            default: return UIFactory.Hex("#A07A2E");   // 파워
        }
    }

    // 등급 색: 기본·일반 회색 / 희귀 파랑 / 영웅 보라 / 전설 금색
    public static Color RarityColor(CardRarity rarity)
    {
        switch (rarity)
        {
            case CardRarity.Rare: return UIFactory.Hex("#4A9BFF");
            case CardRarity.Hero: return UIFactory.Hex("#B46BFF");
            case CardRarity.Legend: return UIFactory.Hex("#FFC845");
            default: return UIFactory.Hex("#9A9AA6");
        }
    }

    // 강화된 카드 이름 색 (초록)
    public static readonly Color UpgradedNameColor = new Color(0.55f, 1f, 0.55f);

    // ---------------- 모양 만들기 ----------------
    public void Build(CardData data, BattleScreen owner, string manaName)
    {
        Data = data;
        Owner = owner;
        Rt = (RectTransform)transform;
        Rt.anchorMin = Rt.anchorMax = new Vector2(0.5f, 0.5f);
        Rt.pivot = new Vector2(0.5f, 0.5f);
        Rt.sizeDelta = new Vector2(Width + 18, Height + 18);   // 바깥 빛 두께(9)만큼 크게

        Color typeColor = TypeColor(data.Type);

        // 마우스 이벤트를 받는 투명한 판 (투명해도 마우스 이벤트는 받음)
        hit = gameObject.AddComponent<Image>();
        hit.color = new Color(0, 0, 0, 0);

        // 그림자 (카드 아래 오른쪽으로 살짝)
        shadow = UIFactory.MakeImage("Shadow", transform, new Color(0, 0, 0, 0.4f));
        UIFactory.Stretch(shadow.rectTransform, 9, 9, 9, 9);
        shadow.raycastTarget = false;

        // 바깥 빛 (평소엔 투명)
        glow = UIFactory.MakeImage("Glow", transform, new Color(0.3f, 1f, 0.45f, 0f));
        UIFactory.Stretch(glow.rectTransform);
        glow.raycastTarget = false;

        // 테두리 — 종류 색. 아래 부품들은 전부 이 테두리 안에 넣음
        frame = UIFactory.MakeImage("Frame", transform, typeColor);
        UIFactory.Stretch(frame.rectTransform, 9, 9, 9, 9);
        frame.raycastTarget = false;
        Transform f = frame.transform;

        // 테두리 안쪽 선 = 등급 색 (희귀 파랑, 영웅 보라, 전설 금색)
        Color rarityColor = RarityColor(data.Rarity);
        Image rim = UIFactory.MakeImage("Rim", f, rarityColor);
        UIFactory.Stretch(rim.rectTransform, 4, 4, 4, 4);
        rim.raycastTarget = false;

        // 몸통: 위는 종류 색이 살짝 섞인 어두운 색 → 아래는 더 어둡게 (그라데이션)
        Image body = UIFactory.MakeImage("Body", f, Color.white);
        UIFactory.Stretch(body.rectTransform, 8, 8, 8, 8);
        body.raycastTarget = false;
        Image bodyGrad = UIFactory.MakeImage("BodyGrad", body.transform, Color.white, false);
        bodyGrad.sprite = UIFactory.MakeGradient(Color.Lerp(UIFactory.Hex("#262833"), typeColor, 0.25f), UIFactory.Hex("#12131A"));
        UIFactory.Stretch(bodyGrad.rectTransform, 2, 2, 2, 2);
        bodyGrad.raycastTarget = false;
        body.color = UIFactory.Hex("#0C0D12");

        // 이름 띠 (리본)
        Image banner = UIFactory.MakeImage("Banner", body.transform, Color.Lerp(typeColor, Color.black, 0.25f));
        UIFactory.Place(banner.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -8), new Vector2(Width - 30, 40));
        banner.raycastTarget = false;
        Text nameText = UIFactory.MakeText("Name", banner.transform, data.Name, 24, data.Upgraded ? UpgradedNameColor : Color.white, TextAnchor.MiddleCenter);
        UIFactory.Stretch(nameText.rectTransform, 30, 6, 0, 0);
        nameText.fontStyle = FontStyle.Bold;
        nameText.resizeTextForBestFit = true;    // 이름이 길면 글자를 자동으로 줄임
        nameText.resizeTextMinSize = 14;
        nameText.resizeTextMaxSize = 24;
        nameText.horizontalOverflow = HorizontalWrapMode.Wrap;
        nameText.verticalOverflow = VerticalWrapMode.Truncate;
        nameText.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.7f);
        UIFactory.Supersample(nameText);   // 카드를 키워도 이름이 선명하게

        // 그림 자리 (나중에 진짜 카드 그림으로 교체) + 종류 아이콘
        Image art = UIFactory.MakeImage("Art", body.transform, Color.white, false);
        art.sprite = UIFactory.MakeGradient(Color.Lerp(typeColor, Color.white, 0.15f), Color.Lerp(typeColor, Color.black, 0.55f));
        UIFactory.Place(art.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -54), new Vector2(Width - 40, 108));
        art.raycastTarget = false;
        BuildTypeIcon(art.transform, data.Type);

        // 종류 (공격 / 스킬 / 파워) — 그림 아래 작은 띠
        Image typeTag = UIFactory.MakeImage("TypeTag", body.transform, UIFactory.Hex("#0A0A0E"));
        UIFactory.Place(typeTag.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -164), new Vector2(110, 24));
        typeTag.raycastTarget = false;
        typeTag.gameObject.AddComponent<Outline>().effectColor = rarityColor;
        string rarityLabel = data.Rarity == CardRarity.Basic ? "" : CardData.RarityName(data.Rarity) + " · ";
        Text typeText = UIFactory.MakeText("Type", typeTag.transform, rarityLabel + data.TypeName(), 16, Color.Lerp(rarityColor, Color.white, 0.4f), TextAnchor.MiddleCenter);
        UIFactory.Stretch(typeText.rectTransform);
        UIFactory.Supersample(typeText);

        // 설명 (길면 글자 자동 축소)
        Text desc = UIFactory.MakeText("Desc", body.transform, data.Description, 19, UIFactory.Hex("#EDE6D2"), TextAnchor.MiddleCenter);
        UIFactory.Place(desc.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 12), new Vector2(Width - 34, 120));
        desc.resizeTextForBestFit = true;
        desc.resizeTextMinSize = 14;
        desc.resizeTextMaxSize = 19;
        desc.verticalOverflow = VerticalWrapMode.Truncate;
        desc.lineSpacing = 0.95f;
        UIFactory.Supersample(desc);

        // 완성된 카드 그림 (Resources/Cards/카드ID.png, 강화판 = 카드ID_plus.png, 230×320)
        //   그림에 이름·비용·설명·테두리가 다 들어 있으므로 위에서 만든 부품들을 덮음.
        //   비용 동그라미·마나 표는 이 그림보다 나중에 만들어서 위에 오지만, 평소엔 숨기고
        //   비용이 바뀌었거나(할인) 모자랄 때만 보여 줌 (SetCostState)
        // 2026-11-15: 강화 카드 그림도 추가 (Cards/카드ID_plus.png). 없으면 코드로 그림
        Sprite cardArt = Resources.Load<Sprite>("Cards/" + data.Id + (data.Upgraded ? "_plus" : ""));
        if (cardArt != null)
        {
            Image full = UIFactory.MakeImage("CardArt", f, Color.white, false);
            full.sprite = cardArt;
            UIFactory.Stretch(full.rectTransform);
            full.raycastTarget = false;
            hasCardArt = true;
            frame.color = new Color(0, 0, 0, 0);   // 그림에 테두리가 있으니 코드 테두리는 투명하게
        }

        // 반사광: 몸통 안에서 세로 띠가 마우스 x 를 따라 좌우로 움직임
        shineImage = UIFactory.MakeImage("Shine", body.transform, new Color(1f, 1f, 1f, 0f), false);
        shine = shineImage.rectTransform;
        shine.anchorMin = new Vector2(0.5f, 0f);
        shine.anchorMax = new Vector2(0.5f, 1f);
        shine.pivot = new Vector2(0.5f, 0.5f);
        shine.sizeDelta = new Vector2(56, -4);
        shineImage.raycastTarget = false;
        if (hasCardArt) shine.SetParent(f, false);   // 그림 위에서도 반사광이 보이게

        // 에너지 코스트 (왼쪽 위 동그라미: 검은 고리 + 금색 원)
        Image costRing = UIFactory.MakeCircle("CostRing", f, UIFactory.Hex("#1A1206"));
        UIFactory.Place(costRing.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), hasCardArt ? new Vector2(22, -22) : new Vector2(16, -16), hasCardArt ? new Vector2(44, 44) : new Vector2(60, 60));
        costRingObj = costRing.gameObject;
        costRing.raycastTarget = false;
        costOrb = UIFactory.MakeCircle("CostOrb", costRing.transform, UIFactory.Hex("#E8A838"));
        UIFactory.Stretch(costOrb.rectTransform, 5, 5, 5, 5);
        costOrb.raycastTarget = false;
        costText = UIFactory.MakeText("Cost", costOrb.transform, data.Cost.ToString(), 32, Color.white, TextAnchor.MiddleCenter);
        UIFactory.Stretch(costText.rectTransform);
        costText.fontStyle = FontStyle.Bold;
        costText.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
        UIFactory.Supersample(costText);

        // 마나 코스트 (에너지 아래 보라색 표)
        if (data.ManaCost > 0 || data.UsesAllMana)
        {
            manaBadge = UIFactory.MakeImage("ManaBadge", f, UIFactory.Hex("#6A3FA0"));
            UIFactory.Place(manaBadge.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(-6, -58), new Vector2(80, 28));
            manaBadge.raycastTarget = false;
            string label = data.UsesAllMana ? manaName + " 전부" : manaName + " " + data.ManaCost;
            manaText = UIFactory.MakeText("Mana", manaBadge.transform, label, 16, Color.white, TextAnchor.MiddleCenter);
            UIFactory.Stretch(manaText.rectTransform);
            manaText.fontStyle = FontStyle.Bold;
            UIFactory.Supersample(manaText);
        }
        if (hasCardArt)
        {
            costRingObj.SetActive(false);                              // 그림 속 비용 숫자를 그대로 보여 줌
            if (manaBadge != null) manaBadge.gameObject.SetActive(false);
        }
    }

    // 손패가 아닌 곳(보상 화면, 대장간)에 보여 주는 카드. onClick 이 있으면 누를 수 있고, 마우스를 올리면 커지며 빛남
    public static CardView CreateDisplay(Transform parent, CardData data, string manaName, Vector2 pos, float scale, System.Action onClick)
    {
        GameObject go = new GameObject("Card_" + data.Name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        CardView v = go.AddComponent<CardView>();
        v.Build(data, null, manaName);   // Owner 가 없으면 손패용 드래그는 안 함
        v.Rt.anchoredPosition = pos;
        v.TargetPos = pos;
        v.Rt.localScale = new Vector3(scale, scale, 1f);
        v.TargetScale = scale;
        if (onClick != null)
        {
            Button button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => onClick());
            HoverRelay hover = go.AddComponent<HoverRelay>();
            hover.OnChange = (on) =>
            {
                v.TargetScale = on ? scale * 1.08f : scale;
                v.playable = on;   // 초록 빛
                if (on) go.transform.SetAsLastSibling();
            };
        }
        return v;
    }

    // 그림 자리에 종류별 간단한 아이콘 (공격 = 칼, 스킬 = 방패, 파워 = 별)
    static void BuildTypeIcon(Transform parent, CardType type)
    {
        Color c = new Color(1f, 1f, 1f, 0.22f);
        if (type == CardType.Attack)
        {
            Image blade = UIFactory.MakeImage("Blade", parent, c);
            UIFactory.Place(blade.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(6, 8), new Vector2(16, 80));
            blade.rectTransform.localEulerAngles = new Vector3(0, 0, -40f);
            Image guard = UIFactory.MakeImage("Guard", parent, c);
            UIFactory.Place(guard.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-14, -16), new Vector2(42, 10));
            guard.rectTransform.localEulerAngles = new Vector3(0, 0, -40f);
        }
        else if (type == CardType.Skill)
        {
            Image top = UIFactory.MakeImage("ShieldTop", parent, c);
            UIFactory.Place(top.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(64, 50));
            Image bottom = UIFactory.MakeImage("ShieldTip", parent, c);
            UIFactory.Place(bottom.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -16), new Vector2(44, 44));
            bottom.rectTransform.localEulerAngles = new Vector3(0, 0, 45f);
        }
        else
        {
            for (int i = 0; i < 2; i++)
            {
                Image star = UIFactory.MakeImage("Star" + i, parent, c);
                UIFactory.Place(star.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56, 56));
                star.rectTransform.localEulerAngles = new Vector3(0, 0, i * 45f);
            }
            Image core = UIFactory.MakeCircle("Core", parent, new Color(1f, 1f, 1f, 0.3f));
            UIFactory.Place(core.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30));
        }
    }

    // 쓸 수 있는지에 따라 코스트 색 바꾸기 (부족하면 빨간색)
    public void SetCostState(int energyCost, bool energyOk, bool manaOk, bool canPlay)
    {
        playable = canPlay;
        costText.text = energyCost.ToString();
        costText.color = energyOk ? Color.white : UIFactory.Hex("#FF5A5A");
        if (manaText != null) manaText.color = manaOk ? Color.white : UIFactory.Hex("#FF8A8A");
        if (hasCardArt)
        {
            // 그림 카드: 비용이 그림과 다르거나(할인) 에너지가 모자랄 때만 코드 비용 동그라미를 그림 위에 덮어 보여 줌
            costRingObj.SetActive(energyCost != Data.Cost || !energyOk);
            if (manaBadge != null) manaBadge.gameObject.SetActive(!manaOk);   // 영혼이 모자라면 빨간 표
        }
    }

    // 손에서 나갈 때: 목표 위치로 날아간 뒤 스스로 사라짐
    public void Leave(Vector2 destination, float endScale)
    {
        Leave(destination, endScale, false);
    }

    // showcase = true 면 먼저 화면 가운데에 크게 보여 준 뒤 목적지로 날아감 (낸 카드)
    public void Leave(Vector2 destination, float endScale, bool showcase)
    {
        IsLeaving = true;
        playable = false;
        hit.raycastTarget = false;
        pointerInside = false;
        finalDest = destination;
        finalScale = endScale;
        if (showcase)
        {
            TargetPos = ShowcasePos;
            TargetScale = 1.25f;
            TargetAngle = 0f;
            MoveSpeed = 18f;
            showTimer = GameBalance.FxCardShowTime;
            transform.SetAsLastSibling();
            BeginCast();   // 3-182
            SoundManager.Play("CardPlay");   // 3-190 카드 내는 소리
        }
        else
        {
            StartFinalLeg();
        }
    }

    void StartFinalLeg()
    {
        showTimer = 0f;
        TargetPos = finalDest;
        TargetScale = finalScale;
        TargetAngle = 0f;
        MoveSpeed = 10f;
        leaveTimer = 0.45f;
    }

    // 바로 그 위치로 옮기기 (드래그 중에 손가락을 그대로 따라가게)
    public void Snap(Vector2 pos)
    {
        TargetPos = pos;
        Rt.anchoredPosition = pos;
    }

    void Update()
    {
        // 3-102 결속 표시: 카드 데이터의 Bound 가 바뀌면 보라 띠를 켜고 끔 (처음 필요할 때 만듦)
        if (Data != null && (Data.Bound || boundBadge != null))
        {
            if (boundBadge == null)
            {
                // 3-173: 결속 그림 (UI/Card_Bound = 그림 칸 위 X 자 사슬 + 자물쇠 + "결속" 리본 + 테두리 보라 빛)
                //   카드 전체 크기로 덮음. 이름·비용·설명 글자는 안 가리게 그렸음 (사슬은 그림 칸에만)
                //   그림이 없으면 예전 보라 띠 (단, 이름을 가리지 않게 그림 칸 가운데로 옮김)
                Sprite boundArt = Resources.Load<Sprite>("UI/Card_Bound");
                if (boundArt != null)
                {
                    boundBadge = UIFactory.MakeImage("Bound", transform, Color.white, false);
                    boundBadge.sprite = boundArt;
                    UIFactory.Stretch(boundBadge.rectTransform);
                    boundBadge.raycastTarget = false;
                    boundPulse = true;
                }
                else
                {
                    boundBadge = UIFactory.MakeImage("Bound", transform, UIFactory.Hex("#7A2FB8"));
                    UIFactory.Place(boundBadge.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, Height * 0.12f), new Vector2(150, 34));
                    boundBadge.raycastTarget = false;
                    Text bt = UIFactory.MakeText("Label", boundBadge.transform, "결속", 20, Color.white, TextAnchor.MiddleCenter);
                    UIFactory.Stretch(bt.rectTransform);
                    bt.fontStyle = FontStyle.Bold;
                }
            }
            if (boundBadge.gameObject.activeSelf != Data.Bound) boundBadge.gameObject.SetActive(Data.Bound);
            if (Data.Bound)
            {
                boundBadge.transform.SetAsLastSibling();
                // 은은하게 숨쉬는 빛: 알파 0.82 ~ 1.0 (unscaledTime → 일시정지 중에도 자연스러움)
                if (boundPulse) boundBadge.color = new Color(1f, 1f, 1f, 0.91f + 0.09f * Mathf.Sin(Time.unscaledTime * 3f));
            }
        }

        // 3-196 지옥 사용 금지: 결속 그림을 붉게 바꾼 UI/Card_Sealed + 띠 위 "사용 금지" 글자 (그림 띠의 글자 칸은 비어 있음)
        if (Data != null && (Data.Sealed || sealBadge != null))
        {
            if (sealBadge == null)
            {
                Sprite sealArt = Resources.Load<Sprite>("UI/Card_Sealed");
                sealBadge = UIFactory.MakeImage("Sealed", transform, sealArt != null ? Color.white : new Color(0.55f, 0.05f, 0.08f, 0.85f), false);
                sealBadge.raycastTarget = false;
                if (sealArt != null)
                {
                    sealBadge.sprite = sealArt;
                    UIFactory.Stretch(sealBadge.rectTransform);
                }
                else UIFactory.Place(sealBadge.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Width * 0.36f, Height * 0.055f));
                // 띠 자리: 그림(460×640) 기준 가로 158~304, 세로 302~337 → 카드 가운데, 폭 약 32%
                Text st = UIFactory.MakeText("Label", sealBadge.transform, "사용 금지", 20, Color.white, TextAnchor.MiddleCenter);
                UIFactory.Place(st.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -Height * 0.002f), new Vector2(Width * 0.32f, Height * 0.055f));
                st.fontStyle = FontStyle.Bold;
                st.resizeTextForBestFit = true;
                st.verticalOverflow = VerticalWrapMode.Truncate;
                st.resizeTextMinSize = 10;
                st.resizeTextMaxSize = 22;
                st.raycastTarget = false;
                st.gameObject.AddComponent<Outline>().effectColor = new Color(0.25f, 0f, 0f, 0.9f);
            }
            if (sealBadge.gameObject.activeSelf != Data.Sealed) sealBadge.gameObject.SetActive(Data.Sealed);
            if (Data.Sealed) sealBadge.transform.SetAsLastSibling();
        }

        // 1 - e^(-속도 × 시간) : 프레임 속도와 상관없이 일정하게 부드러운 움직임
        float t = 1f - Mathf.Exp(-MoveSpeed * Time.deltaTime);
        Rt.anchoredPosition = Vector2.Lerp(Rt.anchoredPosition, TargetPos, t);
        currentAngle = Mathf.LerpAngle(currentAngle, TargetAngle, t);

        // 3D 기울기: 마우스 쪽이 화면 안으로 들어가는 느낌 (최대 약 12도)
        float wantX = 0f, wantY = 0f;
        if (pointerInside && !IsLeaving)
        {
            wantX = Mathf.Clamp(pointerLocal.y / (Height / 2f), -1f, 1f) * -10f;
            wantY = Mathf.Clamp(pointerLocal.x / (Width / 2f), -1f, 1f) * 12f;
        }
        float tt = 1f - Mathf.Exp(-12f * Time.deltaTime);
        tiltX = Mathf.Lerp(tiltX, wantX, tt);
        tiltY = Mathf.Lerp(tiltY, wantY, tt);
        Rt.localEulerAngles = new Vector3(tiltX, tiltY, currentAngle);

        // 그림자: 떠오를수록(크게 보일수록) 멀리, 기울기 반대쪽으로
        float lift = Mathf.Clamp01((Rt.localScale.x - 1f) / 0.3f);
        Vector2 shadowOffset = new Vector2(6f + 10f * lift - tiltY * 0.6f, -10f - 16f * lift + tiltX * 0.6f);
        shadow.rectTransform.offsetMin = new Vector2(9, 9) + shadowOffset;
        shadow.rectTransform.offsetMax = new Vector2(-9, -9) + shadowOffset;
        Color sc = shadow.color;
        sc.a = IsLeaving ? 0f : 0.35f + 0.15f * lift;
        shadow.color = sc;

        // 반사광: 마우스 x 위치를 따라가고, 마우스가 올라와 있을 때만 보임
        Color shc = shineImage.color;
        shc.a = Mathf.MoveTowards(shc.a, (pointerInside && !IsLeaving) ? 0.13f : 0f, Time.deltaTime * 0.8f);
        shineImage.color = shc;
        float maxX = Width / 2f - 46f;   // 몸통 밖으로 안 나가게
        shine.anchoredPosition = new Vector2(Mathf.Clamp(pointerLocal.x * 0.8f, -maxX, maxX), 0f);
        float s = Mathf.Lerp(Rt.localScale.x, TargetScale, t);
        Rt.localScale = new Vector3(s, s, 1f);

        // 쓸 수 있는 카드: 초록 빛이 숨쉬듯 깜빡임
        Color g = glow.color;
        float wantAlpha = playable ? 0.55f + 0.3f * Mathf.Sin(Time.time * 4f) : 0f;
        g.a = Mathf.MoveTowards(g.a, wantAlpha, Time.deltaTime * 4f);
        glow.color = g;

        UpdateCast();   // 3-182 카드 사용 연출

        if (IsLeaving)
        {
            if (showTimer > 0f)
            {
                showTimer -= Time.deltaTime;
                if (showTimer <= 0f) StartFinalLeg();
            }
            else
            {
                leaveTimer -= Time.deltaTime;
                if (leaveTimer <= 0f) Destroy(gameObject);
            }
        }
    }

    // ---------------- 3-182 카드 사용 연출 ----------------
    // 카드 가운데 점을 "부모 상자 가운데 기준" 좌표로 (Vfx 와 빛은 부모 가운데를 기준으로 놓기 때문)
    Vector2 CenterInParent()
    {
        RectTransform parent = (RectTransform)transform.parent;
        Vector3 world = Rt.TransformPoint(Rt.rect.center);
        Vector2 local = parent.InverseTransformPoint(world);
        return local - parent.rect.center;
    }

    void BeginCast()
    {
        if (castGlow != null || transform.parent == null) return;
        castGlow = UIFactory.MakeImage("CastGlow", transform.parent, new Color(CastColor.r, CastColor.g, CastColor.b, 0f), false);
        castGlow.sprite = UIFactory.SoftGlowSprite;
        castGlow.raycastTarget = false;
        RectTransform g = castGlow.rectTransform;
        g.anchorMin = g.anchorMax = new Vector2(0.5f, 0.5f);
        g.pivot = new Vector2(0.5f, 0.5f);
        g.SetSiblingIndex(transform.GetSiblingIndex());   // 카드 바로 뒤 (카드는 한 칸 뒤로 밀려서 위에 그려짐)
        castAge = 0f;
        castBurstDone = false;
        castFadeDone = false;
    }

    void UpdateCast()
    {
        if (castGlow == null) return;
        castAge += Time.deltaTime;
        RectTransform g = castGlow.rectTransform;
        Vector2 c = CenterInParent();
        g.anchoredPosition = c;
        float sc = Rt.localScale.x;
        g.sizeDelta = new Vector2(Width * 2.1f, Height * 1.8f) * sc;
        g.localEulerAngles = Vector3.zero;

        float a;
        if (showTimer > 0f)
        {
            // 가운데에 머무는 동안: 빠르게 밝아졌다가 숨쉬듯 깜빡임
            a = Mathf.Clamp01(castAge / 0.12f) * (0.75f + 0.2f * Mathf.Sin(castAge * 18f));
            // 가운데에 거의 닿은 순간 한 번: 빛 고리 + 불똥 + 반짝이
            if (!castBurstDone && Vector2.Distance(Rt.anchoredPosition, ShowcasePos) < 40f)
            {
                castBurstDone = true;
                Transform layer = transform.parent;
                Vfx.Ring(layer, c, new Color(CastColor.r, CastColor.g, CastColor.b, 0.9f));
                Vfx.Sparks(layer, c, CastColor, 12);
                Vfx.Rise(layer, c, new Color(1f, 1f, 1f, 0.9f), 8);
                Vfx.Burst(layer, c, 300f, new Color(Mathf.Lerp(CastColor.r, 1f, 0.5f), Mathf.Lerp(CastColor.g, 1f, 0.5f), Mathf.Lerp(CastColor.b, 1f, 0.5f), 1f));   // 3-186 빛 터짐 프레임 (직업 색 살짝)
            }
        }
        else
        {
            // 날아가는 동안: 빛이 빨리 사라짐 + 처음에 작은 반짝이 흩어짐
            if (!castFadeDone)
            {
                castFadeDone = true;
                Vfx.Rise(transform.parent, c, CastColor, 6);
            }
            a = 0.75f * Mathf.Clamp01((leaveTimer - 0.25f) / 0.2f);
        }
        castGlow.color = new Color(CastColor.r, CastColor.g, CastColor.b, a);
        if (a <= 0f && showTimer <= 0f)
        {
            Destroy(castGlow.gameObject);
            castGlow = null;
        }
    }

    void OnDestroy()
    {
        if (castGlow != null) Destroy(castGlow.gameObject);
    }

    // ---------------- 마우스 이벤트 → BattleScreen 에 전달 ----------------
    public void OnPointerMove(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(Rt, eventData.position, null, out pointerLocal);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
        if (!IsLeaving && Owner != null) Owner.OnCardHover(this, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        if (!IsLeaving && Owner != null) Owner.OnCardHover(this, false);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!IsLeaving && Owner != null) Owner.OnCardBeginDrag(this, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!IsLeaving && Owner != null) Owner.OnCardDrag(this, eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!IsLeaving && Owner != null) Owner.OnCardEndDrag(this, eventData);
    }
}
