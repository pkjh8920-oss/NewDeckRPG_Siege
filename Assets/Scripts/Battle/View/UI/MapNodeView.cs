// ============================================================
// MapNodeView.cs
// 지도 위의 칸 하나 (보이는 모습 + 마우스 입력)
//
//   갈 수 있는 칸 : 밝고, 커졌다 작아졌다 (둥실둥실) + 뒤에 은은한 빛
//   지나온 칸     : 빨간 동그라미 표시
//   지금 있는 칸  : 위에 금색 화살표
//   못 가는 칸    : 흐리게
//
// 아이콘은 그림 파일 없이 도형으로 그립니다 (BuildIcon). 지도 오른쪽 설명표에서도 같이 씀.
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class MapNodeView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [System.NonSerialized] public MapNode Node;
    [System.NonSerialized] public MapPanel Owner;
    public bool IsAvailable;

    private RectTransform rt;
    private RectTransform icon;
    private Image glow;
    private RectTransform marker;
    private CanvasGroup group;
    private bool hovering;
    private float phase;

    // 잉크색 (양피지 위에 그린 느낌)
    public static readonly Color Ink = new Color(0.23f, 0.16f, 0.11f);
    public static readonly Color Paper = new Color(0.85f, 0.75f, 0.58f);
    public static bool OnDarkMap;   // MapPanel 이 정함: 어두운 그림 지도 위인지

    // 어두운 지도용 그림 아이콘 이름. 정문(0층) 전투 = 칼 두 자루, 나머지 전투 = 칼 한 자루 (예시 그림처럼)
    static string DarkIconName(MapNode node)
    {
        switch (node.Type)
        {
            case MapNodeType.Monster: return OnDarkMap && node.Floor == 0 ? "Map_Veteran" : "Map_Monster";
            case MapNodeType.Veteran: return "Map_Veteran";   // 3-111
            case MapNodeType.Elite: return "Map_Elite";
            case MapNodeType.Rest: return "Map_Rest";
            case MapNodeType.Treasure: return "Map_Treasure";
            case MapNodeType.Event: return "Map_Event";
            case MapNodeType.Shop: return "Map_Shop";
            case MapNodeType.Forge: return "Map_Forge";
            default: return "Map_Boss";
        }
    }

    public void Build(MapNode node, MapPanel owner, bool available, bool isCurrent)
    {
        Node = node;
        Owner = owner;
        IsAvailable = available;
        rt = (RectTransform)transform;
        phase = Random.Range(0f, 6f);
        float size = node.Type == MapNodeType.Boss ? 150f : 80f;
        rt.sizeDelta = new Vector2(size, size);

        // 마우스를 받는 투명한 판
        Image hit = gameObject.AddComponent<Image>();
        hit.color = new Color(0, 0, 0, 0);

        // 갈 수 있는 칸 뒤의 빛
        glow = UIFactory.MakeCircle("Glow", transform, new Color(1f, 0.95f, 0.7f, 0f));
        UIFactory.Stretch(glow.rectTransform, -10, -10, -10, -10);
        glow.raycastTarget = false;

        icon = UIFactory.MakeRect("Icon", transform);
        UIFactory.Stretch(icon);
        // 어두운 지도(최종장 성 그림): 뼈색·금빛 그림 아이콘 (Resources/Icons/Map_*.png). 없으면 예전 도형
        Sprite art = Resources.Load<Sprite>("Icons/" + DarkIconName(node));   // 2026-11-15: 3스테이지 양피지 지도도 같은 그림 아이콘
        if (art != null)
        {
            Image img = UIFactory.MakeImage("Art", icon, Color.white, false);
            img.sprite = art;
            img.preserveAspect = true;
            img.raycastTarget = false;
            UIFactory.Stretch(img.rectTransform, -14, -14, -14, -14);   // 빛 테두리만큼 살짝 크게
        }
        else BuildIcon(icon, node.Type, size);

        // 지나온 칸: 손으로 그린 듯한 빨간 동그라미
        if (node.Visited)
        {
            Image ring = UIFactory.MakeImage("Visited", transform, new Color(0.62f, 0.14f, 0.1f, 0.9f), false);
            ring.sprite = UIFactory.RingSprite;
            UIFactory.Stretch(ring.rectTransform, -8, -8, -8, -8);
            ring.raycastTarget = false;
        }

        // 지금 있는 칸: 위에 금색 화살표
        if (isCurrent)
        {
            Image arrow = UIFactory.MakeImage("Here", transform, UIFactory.Hex("#E8B040"), false);
            arrow.sprite = UIFactory.TriangleSprite;
            arrow.raycastTarget = false;
            marker = arrow.rectTransform;
            UIFactory.Place(marker, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, 24), new Vector2(30, 26));
            marker.localEulerAngles = new Vector3(0, 0, 180f);   // 아래를 가리키게
            arrow.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.6f);
        }

        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = (available || node.Visited || isCurrent) ? 1f : 0.45f;
    }

    void Update()
    {
        float target = 1f;
        if (IsAvailable) target = 1f + 0.1f * Mathf.Sin(Time.time * 4f + phase);   // 둥실둥실
        if (hovering) target = IsAvailable ? 1.3f : 1.12f;
        float s = Mathf.Lerp(rt.localScale.x, target, 1f - Mathf.Exp(-14f * Time.deltaTime));
        rt.localScale = new Vector3(s, s, 1f);

        Color g = glow.color;
        g.a = IsAvailable ? 0.35f + 0.2f * Mathf.Sin(Time.time * 4f + phase) : 0f;
        glow.color = g;

        if (marker != null)
        {
            marker.anchoredPosition = new Vector2(0, 26 + Mathf.Sin(Time.time * 3f) * 5f);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovering = true;
        if (Owner != null) Owner.OnNodeHover(this, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovering = false;
        if (Owner != null) Owner.OnNodeHover(this, false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (Owner != null) Owner.OnNodeClick(this);
    }

    // ---------------- 아이콘 그리기 (도형 조합) ----------------
    static Image Part(Transform parent, string name, Color c, Vector2 pos, Vector2 size, float angle, bool circle)
    {
        Image img = circle ? UIFactory.MakeCircle(name, parent, c) : UIFactory.MakeImage(name, parent, c);
        img.raycastTarget = false;
        UIFactory.Place(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
        img.rectTransform.localEulerAngles = new Vector3(0, 0, angle);
        return img;
    }

    static Image Tri(Transform parent, string name, Color c, Vector2 pos, Vector2 size, float angle)
    {
        Image img = UIFactory.MakeImage(name, parent, c, false);
        img.sprite = UIFactory.TriangleSprite;
        img.raycastTarget = false;
        UIFactory.Place(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
        img.rectTransform.localEulerAngles = new Vector3(0, 0, angle);
        return img;
    }

    // size = 칸 크기. 아이콘은 64 기준으로 그린 다음 크기에 맞게 키움
    // 칸 종류 → 그림 아이콘 파일 이름 (Resources/Icons/이름.png)
    public static string IconNameFor(MapNodeType type)
    {
        switch (type)
        {
            case MapNodeType.Monster: return "Map_Monster";
            case MapNodeType.Veteran: return "Map_Veteran";   // 3-111
            case MapNodeType.Elite: return "Map_Elite";
            case MapNodeType.Rest: return "Map_Rest";
            case MapNodeType.Treasure: return "Map_Treasure";
            case MapNodeType.Event: return "Map_Event";
            case MapNodeType.Shop: return "Map_Shop";
            case MapNodeType.Forge: return "Map_Forge";
            default: return "Map_Boss";
        }
    }

    // 그림 아이콘 하나 놓기 (3-78). 그림이 있으면 true, 없으면 아무것도 안 하고 false
    //   새 아이콘(동그란 테두리 + 빛)은 그림의 약 73% 가 테두리라서, 예전 도형과 크기가 비슷하게 보이게 ArtScale 배로 키움
    public const float ArtScale = 1.3f;
    public static bool BuildIconArt(RectTransform holder, string iconName, float size)
    {
        return MakeIconArt(holder, iconName, size) != null;
    }

    // BuildIconArt 와 같지만 만든 그림(Image)을 돌려줌 (색을 어둡게 하는 등 더 손볼 때). 그림이 없으면 null
    public static Image MakeIconArt(RectTransform holder, string iconName, float size)
    {
        Sprite art = Resources.Load<Sprite>("Icons/" + iconName);
        if (art == null) return null;
        Image img = UIFactory.MakeImage("IconArt", holder, Color.white, false);
        img.sprite = art;
        img.preserveAspect = true;
        img.raycastTarget = false;
        UIFactory.Place(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * ArtScale, size * ArtScale));
        return img;
    }

    // iconName: 따로 정한 그림 (예: 수성 3칸 정예 = "Map_Veteran"). 없으면 칸 종류로 정함
    //   2026-11-15 (3-78): 그림 아이콘이 있으면 그림, 없으면 예전처럼 도형으로 그림
    public static void BuildIcon(RectTransform holder, MapNodeType type, float size, string iconName = null)
    {
        if (BuildIconArt(holder, iconName ?? IconNameFor(type), size)) return;
        BuildShapeIcon(holder, type, size);
    }

    // 예전 도형 아이콘 (그림 파일이 없을 때)
    static void BuildShapeIcon(RectTransform holder, MapNodeType type, float size)
    {
        RectTransform root = UIFactory.MakeRect("Shape", holder);
        UIFactory.Place(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64, 64));
        float k = (type == MapNodeType.Boss ? size / 100f : size / 72f);
        root.localScale = new Vector3(k, k, 1f);
        Transform t = root;

        switch (type)
        {
            case MapNodeType.Veteran:   // 3-111: 그림(Map_Veteran)이 없을 때는 몬스터 모양으로
            case MapNodeType.Monster:
                Tri(t, "HornL", Ink, new Vector2(-14, 20), new Vector2(16, 20), 25f);
                Tri(t, "HornR", Ink, new Vector2(14, 20), new Vector2(16, 20), -25f);
                Part(t, "Head", Ink, new Vector2(0, 0), new Vector2(42, 40), 0f, true);
                Part(t, "EyeL", Paper, new Vector2(-8, 3), new Vector2(8, 8), 0f, true);
                Part(t, "EyeR", Paper, new Vector2(8, 3), new Vector2(8, 8), 0f, true);
                Part(t, "Mouth", Paper, new Vector2(0, -9), new Vector2(16, 5), 0f, false);
                break;

            case MapNodeType.Elite:
                Color red = UIFactory.Hex("#6E1C16");
                Tri(t, "HornL", red, new Vector2(-18, 22), new Vector2(20, 30), 32f);
                Tri(t, "HornR", red, new Vector2(18, 22), new Vector2(20, 30), -32f);
                Part(t, "Head", red, new Vector2(0, 0), new Vector2(50, 46), 0f, true);
                Tri(t, "BrowL", Paper, new Vector2(-9, 5), new Vector2(12, 9), 200f);
                Tri(t, "BrowR", Paper, new Vector2(9, 5), new Vector2(12, 9), 160f);
                Part(t, "Mouth", Paper, new Vector2(0, -11), new Vector2(22, 6), 0f, false);
                break;

            case MapNodeType.Rest:
                Part(t, "LogA", Ink, new Vector2(0, -20), new Vector2(40, 8), 18f, false);
                Part(t, "LogB", Ink, new Vector2(0, -20), new Vector2(40, 8), -18f, false);
                Tri(t, "Flame", UIFactory.Hex("#B8442A"), new Vector2(0, 4), new Vector2(34, 44), 0f);
                Tri(t, "Inner", UIFactory.Hex("#E8A040"), new Vector2(0, -2), new Vector2(18, 24), 0f);
                break;

            case MapNodeType.Treasure:
                Part(t, "Body", UIFactory.Hex("#7A4A22"), new Vector2(0, -6), new Vector2(46, 28), 0f, false);
                Part(t, "Lid", UIFactory.Hex("#5A3416"), new Vector2(0, 12), new Vector2(46, 16), 0f, false);
                Part(t, "Band", UIFactory.Hex("#D8B040"), new Vector2(0, 2), new Vector2(7, 44), 0f, false);
                Part(t, "Lock", UIFactory.Hex("#F0D060"), new Vector2(0, 2), new Vector2(11, 11), 0f, false);
                break;

            case MapNodeType.Event:
                Part(t, "Disc", Ink, Vector2.zero, new Vector2(46, 46), 0f, true);
                Text q = UIFactory.MakeText("Q", t, "?", 34, Paper, TextAnchor.MiddleCenter);
                UIFactory.Place(q.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 1), new Vector2(46, 46));
                q.fontStyle = FontStyle.Bold;
                break;

            case MapNodeType.Shop:
                Part(t, "Bag", UIFactory.Hex("#6A5020"), new Vector2(0, -6), new Vector2(42, 40), 0f, true);
                Part(t, "Neck", UIFactory.Hex("#6A5020"), new Vector2(0, 16), new Vector2(18, 12), 0f, false);
                Part(t, "Tie", UIFactory.Hex("#D8B040"), new Vector2(0, 11), new Vector2(24, 4), 0f, false);
                Text g = UIFactory.MakeText("G", t, "G", 22, UIFactory.Hex("#F0D060"), TextAnchor.MiddleCenter);
                UIFactory.Place(g.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -7), new Vector2(40, 30));
                g.fontStyle = FontStyle.Bold;
                break;

            case MapNodeType.Forge:
                Part(t, "AnvilTop", Ink, new Vector2(0, -6), new Vector2(46, 12), 0f, false);
                Part(t, "AnvilBase", Ink, new Vector2(0, -18), new Vector2(20, 16), 0f, false);
                Part(t, "Handle", UIFactory.Hex("#7A4A22"), new Vector2(4, 12), new Vector2(6, 30), -35f, false);
                Part(t, "Head", Ink, new Vector2(11, 22), new Vector2(24, 11), -35f, false);
                break;

            default:   // 보스
                Color dark = UIFactory.Hex("#3A1212");
                Tri(t, "HornL", dark, new Vector2(-24, 26), new Vector2(22, 34), 35f);
                Tri(t, "HornR", dark, new Vector2(24, 26), new Vector2(22, 34), -35f);
                Part(t, "Head", dark, new Vector2(0, 0), new Vector2(64, 60), 0f, true);
                Part(t, "EyeL", UIFactory.Hex("#E03030"), new Vector2(-12, 6), new Vector2(12, 12), 0f, true);
                Part(t, "EyeR", UIFactory.Hex("#E03030"), new Vector2(12, 6), new Vector2(12, 12), 0f, true);
                for (int i = 0; i < 4; i++)
                {
                    Tri(t, "Tooth" + i, Paper, new Vector2(-12 + i * 8, -14), new Vector2(7, 9), 180f);
                }
                break;
        }
    }
}
