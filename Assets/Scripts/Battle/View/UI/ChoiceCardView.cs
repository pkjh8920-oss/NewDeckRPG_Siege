// ============================================================
// ChoiceCardView.cs
// 수성 화면(ChoicePanel)의 선택지 카드 한 장 (보이는 모습 + 마우스 입력)
//
//   ??? 카드 : 큰 물음표 + 정찰병 힌트 문장
//   보이는 카드: 아이콘 + 이름 + 힌트 문장
// 마우스를 올리면 살짝 커지고, 누르면 ChoicePanel 에 알려 줌. (MapNodeView 와 같은 방식)
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ChoiceCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [System.NonSerialized] public SiegeChoice Choice;
    [System.NonSerialized] public ChoicePanel Owner;

    private RectTransform rt;
    private Image glow;
    private bool hovering;
    private float phase;

    public void Build(SiegeChoice choice, ChoicePanel owner, Vector2 size, int index)
    {
        Choice = choice;
        Owner = owner;
        rt = (RectTransform)transform;
        phase = index * 1.1f;

        // 마우스를 받는 투명한 판
        Image hit = gameObject.AddComponent<Image>();
        hit.color = new Color(0, 0, 0, 0);

        // 뒤의 빛 (마우스를 올리면 밝아짐)
        glow = UIFactory.MakeImage("Glow", transform, new Color(1f, 0.85f, 0.45f, 0f));
        UIFactory.Stretch(glow.rectTransform, -14, -14, -14, -14);
        glow.raycastTarget = false;

        // 카드 테두리 + 양피지
        bool boss = choice.Kind == SiegeChoiceKind.Boss;
        Image edge = UIFactory.MakeImage("Edge", transform, boss ? UIFactory.Hex("#5A1A1A") : UIFactory.Hex("#6A4A2A"));
        UIFactory.Stretch(edge.rectTransform);
        edge.raycastTarget = false;
        Image paper = UIFactory.MakeImage("Paper", edge.transform, Color.white);
        paper.sprite = UIFactory.MakeGradient(UIFactory.Hex("#D9C190"), UIFactory.Hex("#B4936A"));
        paper.type = Image.Type.Simple;
        UIFactory.Stretch(paper.rectTransform, 10, 10, 10, 10);
        paper.raycastTarget = false;

        // 3-91b: 새 카드 테두리 (Resources/UI/ChoiceCardFrame.png = 룬 보석 모서리 + 가시 테두리 + 어두운 판)
        //   9분할(Sliced): 모서리 48px 는 그대로, 가운데만 늘어남 → 카드 크기가 달라도 테두리 무늬가 안 늘어남
        //   그림이 있으면 양피지 대신 어두운 판이라 글씨를 밝은 색으로. 그림이 없으면 예전 양피지 카드
        Sprite frame = Resources.Load<Sprite>("UI/ChoiceCardFrame");
        bool dark = frame != null;
        if (dark)
        {
            edge.sprite = frame;
            edge.type = Image.Type.Sliced;
            edge.color = boss ? new Color(1f, 0.72f, 0.72f, 1f) : Color.white;   // 보스 카드는 붉게
            paper.sprite = null;
            paper.color = new Color(0, 0, 0, 0);   // 판은 투명 (자리 잡는 용도로만)
        }
        Color titleColor = dark ? UIFactory.Hex("#EED6A6") : ChoicePanel.Ink;
        Color hintColor = dark ? UIFactory.Hex("#CBB893") : UIFactory.Hex("#3A2A1C");

        // 위쪽 그림: ??? 면 큰 물음표, 아니면 칸 아이콘
        float iconSize = size.x * 0.42f;
        RectTransform iconHolder = UIFactory.MakeRect("Icon", paper.transform);
        UIFactory.Place(iconHolder, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -size.y * 0.27f), new Vector2(iconSize, iconSize));
        if (choice.Hidden && MapNodeView.BuildIconArt(iconHolder, "Map_Event", iconSize))
        {
            // ??? 카드 = 미지 아이콘 (3-78). 그림이 없으면 아래 예전 물음표 동그라미
        }
        else if (choice.Hidden)
        {
            Image disc = UIFactory.MakeCircle("Disc", iconHolder, ChoicePanel.Ink);
            UIFactory.Stretch(disc.rectTransform);
            disc.raycastTarget = false;
            Text q = UIFactory.MakeText("Q", disc.transform, "?", (int)(iconSize * 0.7f), UIFactory.Hex("#D9C190"), TextAnchor.MiddleCenter);
            UIFactory.Stretch(q.rectTransform);
            q.fontStyle = FontStyle.Bold;
        }
        else
        {
            // 정예 몬스터 침공(3칸) = 칼 두 자루 그림 (3-78)
            string iconName = (choice.Fight == 'V' || (choice.Title != null && choice.Title.Contains("정예"))) ? "Map_Veteran" : null;
            // 3-129 피의 제단: 전용 그림(Icons/Map_BloodAltar)이 있으면 그것, 없으면 이벤트 그림 그대로
            if (choice.Title == RunState.BloodAltarName && Resources.Load<Sprite>("Icons/Map_BloodAltar") != null) iconName = "Map_BloodAltar";
            MapNodeView.BuildIcon(iconHolder, choice.NodeType, choice.Kind == SiegeChoiceKind.Boss ? iconSize * 1.3f : iconSize, iconName);
        }

        // 이름
        string title = choice.Hidden ? "???" : choice.RealName;
        Text titleText = UIFactory.MakeText("Title", paper.transform, title, size.x > 260 ? 34 : 28, titleColor, TextAnchor.MiddleCenter);
        UIFactory.Place(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -size.y * 0.53f), new Vector2(size.x - 30, 44));
        titleText.fontStyle = FontStyle.Bold;
        // 3-146: 긴 이름("피할 수 없는 침공 (엘리트)")이 두 줄로 넘쳐 아래 선과 겹치던 문제
        //   → bestFit: 이름 칸(높이 44) 안에 다 들어갈 때까지 글자를 줄임 (칸 밖으로 안 나감)
        titleText.verticalOverflow = VerticalWrapMode.Truncate;
        titleText.resizeTextForBestFit = true;
        titleText.resizeTextMinSize = 16;
        titleText.resizeTextMaxSize = titleText.fontSize;
        if (dark) titleText.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);   // 어두운 판 위에서 또렷하게

        // 구분선
        Image line = UIFactory.MakeImage("Line", paper.transform, dark ? new Color(0.85f, 0.7f, 0.45f, 0.45f) : new Color(0.35f, 0.24f, 0.14f, 0.5f), false);
        UIFactory.Place(line.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -size.y * 0.6f), new Vector2(size.x * 0.7f, 2));
        line.raycastTarget = false;

        // 힌트 문장
        Text hint = UIFactory.MakeText("Hint", paper.transform, choice.Hint, size.x > 260 ? 21 : 18, hintColor, TextAnchor.UpperCenter);
        UIFactory.Place(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -size.y * 0.64f), new Vector2(size.x - 40, size.y * 0.32f));
        hint.resizeTextForBestFit = true;   // 3-154: 정찰병 힌트가 길면 작게 (카드 아래로 안 넘치게)
        hint.resizeTextMinSize = 13;
        hint.resizeTextMaxSize = size.x > 260 ? 21 : 18;
        hint.verticalOverflow = VerticalWrapMode.Truncate;
    }

    void Update()
    {
        float target = hovering ? 1.08f : 1f + 0.015f * Mathf.Sin(Time.time * 2.5f + phase);
        float s = Mathf.Lerp(rt.localScale.x, target, 1f - Mathf.Exp(-14f * Time.deltaTime));
        rt.localScale = new Vector3(s, s, 1f);

        Color g = glow.color;
        g.a = Mathf.MoveTowards(g.a, hovering ? 0.55f : 0.12f, Time.deltaTime * 3f);
        glow.color = g;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovering = true;
        if (Owner != null) Owner.OnCardHover(this, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovering = false;
        if (Owner != null) Owner.OnCardHover(this, false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (Owner != null) Owner.OnCardClick(this);
    }
}
