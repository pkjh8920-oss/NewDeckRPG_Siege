// ============================================================
// MapPanel.cs
// 지도 화면 전체 (양피지, 칸, 점선, 설명표, 스테이지 시작 띠)
//
// BattleScreen 이 MapPanel.Create(...) 로 한 번 만들어 두고,
//   Show(run, showBanner) : 지도를 보여 줌 (칸과 선을 새로 그림)
//   Hide()                : 숨김 (전투 화면으로)
//   ShowMessage(text)     : 지도 위쪽에 잠깐 뜨는 안내 (모닥불 회복량 등)
// 칸을 누르면 OnNodeClicked 로 BattleScreen 에 알려 줍니다.
//
// System.Action<MapNode> = "MapNode 하나를 받는 함수"를 담는 변수
// (자바의 Consumer<MapNode> 와 같은 개념)
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MapPanel : MonoBehaviour
{
    public System.Action<MapNode> OnNodeClicked;

    private RectTransform paper;          // 양피지 (칸과 선이 올라가는 곳)
    private RectTransform lineLayer;
    private RectTransform nodeLayer;
    private Text infoTitle;               // 왼쪽: 마우스를 올린 칸 이름
    private Text infoBody;                // 왼쪽: 설명
    private Text partyText;               // 왼쪽 위: 파티 HP
    private Text headerText;              // 양피지 위쪽: "1스테이지 지도"
    private Text messageText;             // 잠깐 뜨는 안내
    private float messageTimer;
    private CanvasGroup banner;           // 스테이지 시작 띠
    private Text bannerText;
    private float bannerTimer;
    private Image gateScene;          // 최종장 시작 때 보여 주는 "열린 성문" 그림
    private float gateTimer;          // 남은 시간 (0 이면 안 보임)
    private const float GateTime = 4.2f;   // 3.2초 보여 주고 마지막 1초 동안 사라짐
    private float bannerDuration = 2.2f;  // 띠가 떠 있는 전체 시간
    private Text restLegendLabel;         // 설명표의 "초소/야영지" 글자 (스테이지마다 이름이 바뀜)
    private RunState run;

    // 최종장: 양피지 대신 성 그림(Backgrounds/FinalCastleMap) 위에 칸을 놓음 (2026-11-14)
    private Image castleMap;
    private Image paperEdge, paperFill;
    // 3스테이지 지도 그림 (3-178). 없으면 예전처럼 갈색 그라데이션 종이를 씀
    private Sprite paperGradient;                // 예전 그라데이션 종이 (그림이 없을 때 되돌릴 용도)
    private Sprite stage3MapArt;                 // Backgrounds/Stage3Map
    private bool onStage3Art;                    // 지금 3스테이지 그림 지도를 쓰는 중인지 (선 그릴 때도 씀, 3-179)
    private bool onCastle;                       // 지금 성 그림 지도를 쓰는 중인지
    private const float CastleW = 1806f, CastleH = 1008f;
    private const float CastleOffsetY = 18f;     // 성 그림 가운데가 양피지 가운데보다 얼마나 위인지
    private const float PaperW = 1080f;
    private const float PaperH = 940f;
    private const float FloorGap = 124f;
    private const float MapSpanFloors = 6f;   // 층 간격 기준: 예전 7층 지도의 아래~보스 칸 수 (3-83)
    private const float ColGap = 190f;

    public static MapPanel Create(Transform parent)
    {
        RectTransform rt = UIFactory.MakeRect("MapPanel", parent);
        UIFactory.Stretch(rt);
        MapPanel panel = rt.gameObject.AddComponent<MapPanel>();
        panel.BuildStatic();
        rt.gameObject.SetActive(false);
        return panel;
    }

    // 바뀌지 않는 부분 (배경, 양피지, 설명표, 띠)
    void BuildStatic()
    {
        // 뒤의 전투 화면을 가리는 어두운 판 (마우스도 막음)
        Image dim = UIFactory.MakeImage("Dim", transform, new Color(0.05f, 0.04f, 0.06f, 0.96f), false);
        UIFactory.Stretch(dim.rectTransform);   // 3-203: 화면 전체 (위쪽 버튼 줄 뒤까지 같은 배경)

        // 최종장용 성 그림 (평소엔 숨김). 양피지보다 먼저 만들어서 칸·선보다 뒤에 깔림
        castleMap = UIFactory.MakeImage("CastleMap", transform, Color.white, false);
        castleMap.sprite = Resources.Load<Sprite>("Backgrounds/FinalCastleMap");
        UIFactory.Place(castleMap.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -50 + CastleOffsetY), new Vector2(CastleW, CastleH));
        castleMap.raycastTarget = false;
        castleMap.gameObject.SetActive(false);

        // 양피지: 진한 테두리 + 밝은 종이 + 안쪽 얼룩
        Image edge = UIFactory.MakeImage("PaperEdge", transform, UIFactory.Hex("#6A4A2A"));
        paperEdge = edge;
        UIFactory.Place(edge.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -50), new Vector2(PaperW + 24, PaperH + 4));
        edge.raycastTarget = false;
        Image paperImg = UIFactory.MakeImage("Paper", edge.transform, Color.white);
        paperGradient = UIFactory.MakeGradient(UIFactory.Hex("#D6BC8C"), UIFactory.Hex("#B8976A"));
        paperImg.sprite = paperGradient;
        // 3스테이지 지도 그림 미리 불러두기 (Java로 치면 생성자에서 이미지 파일을 한 번만 읽어두는 것)
        stage3MapArt = Resources.Load<Sprite>("Backgrounds/Stage3Map");
        paperImg.type = Image.Type.Simple;
        UIFactory.Stretch(paperImg.rectTransform, 12, 12, 12, 12);
        paperImg.raycastTarget = false;
        paperFill = paperImg;
        paper = paperImg.rectTransform;
        for (int i = 0; i < 6; i++)
        {
            // 종이 얼룩 (분위기용)
            Image stain = UIFactory.MakeCircle("Stain", paper, new Color(0.45f, 0.32f, 0.18f, 0.06f));
            float s = Random.Range(160f, 360f);
            UIFactory.Place(stain.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(Random.Range(-420f, 420f), Random.Range(-380f, 380f)), new Vector2(s, s * Random.Range(0.6f, 1f)));
            stain.raycastTarget = false;
        }

        lineLayer = UIFactory.MakeRect("Lines", paper);
        UIFactory.Stretch(lineLayer);
        nodeLayer = UIFactory.MakeRect("Nodes", paper);
        UIFactory.Stretch(nodeLayer);

        headerText = UIFactory.MakeText("Header", transform, "", 30, UIFactory.Hex("#E8D4A8"), TextAnchor.MiddleCenter);
        UIFactory.Place(headerText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -66), new Vector2(900, 44));
        headerText.fontStyle = FontStyle.Bold;

        // 왼쪽: 파티 정보 + 칸 설명
        Image left = UIFactory.MakeImage("InfoBox", transform, new Color(0.1f, 0.08f, 0.07f, 0.9f));
        UIFactory.Place(left.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(30, -30), new Vector2(340, 640));
        left.raycastTarget = false;
        partyText = UIFactory.MakeText("Party", left.transform, "", 22, Color.white, TextAnchor.UpperLeft);
        UIFactory.Place(partyText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -20), new Vector2(300, 200));
        infoTitle = UIFactory.MakeText("InfoTitle", left.transform, "", 30, UIFactory.Hex("#FFD27A"), TextAnchor.UpperLeft);
        UIFactory.Place(infoTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -250), new Vector2(300, 44));
        infoTitle.fontStyle = FontStyle.Bold;
        infoBody = UIFactory.MakeText("InfoBody", left.transform, "", 21, UIFactory.Hex("#E0D8C4"), TextAnchor.UpperLeft);
        UIFactory.Place(infoBody.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -300), new Vector2(300, 320));

        // 오른쪽: 칸 종류 설명표
        Image right = UIFactory.MakeImage("Legend", transform, new Color(0.1f, 0.08f, 0.07f, 0.9f));
        UIFactory.Place(right.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-30, -30), new Vector2(300, 640));
        right.raycastTarget = false;
        Image legendPaper = UIFactory.MakeImage("LegendPaper", right.transform, UIFactory.Hex("#C9AE80"));
        UIFactory.Stretch(legendPaper.rectTransform, 10, 10, 10, 10);
        legendPaper.raycastTarget = false;
        Text legendTitle = UIFactory.MakeText("LegendTitle", legendPaper.transform, "칸 종류", 26, MapNodeView.Ink, TextAnchor.MiddleCenter);
        UIFactory.Place(legendTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -10), new Vector2(260, 40));
        legendTitle.fontStyle = FontStyle.Bold;
        MapNodeType[] types = { MapNodeType.Monster, MapNodeType.Veteran, MapNodeType.Elite, MapNodeType.Rest, MapNodeType.Treasure,
                                MapNodeType.Shop, MapNodeType.Forge, MapNodeType.Boss };   // 2026-11-15: 이벤트 삭제 / 3-111: 정예 추가
        for (int i = 0; i < types.Length; i++)
        {
            float y = -78 - i * 66;
            RectTransform iconHolder = UIFactory.MakeRect("LegendIcon" + i, legendPaper.transform);
            UIFactory.Place(iconHolder, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(54, y), new Vector2(56, 56));
            MapNodeView.BuildIcon(iconHolder, types[i], types[i] == MapNodeType.Boss ? 70f : 52f);
            bool ready = types[i] != MapNodeType.Event;
            Text label = UIFactory.MakeText("LegendLabel" + i, legendPaper.transform,
                RunMap.TypeName(types[i]) + (ready ? "" : "  <size=16>(준비 중)</size>"), 24, MapNodeView.Ink, TextAnchor.MiddleLeft);
            UIFactory.Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(100, y), new Vector2(180, 40));
            if (types[i] == MapNodeType.Rest) restLegendLabel = label;
        }

        // 잠깐 뜨는 안내
        messageText = UIFactory.MakeText("Message", transform, "", 28, UIFactory.Hex("#FFE6A0"), TextAnchor.MiddleCenter);
        UIFactory.Place(messageText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -116), new Vector2(1100, 44));
        messageText.fontStyle = FontStyle.Bold;
        messageText.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.9f);

        // 성문이 열리는 장면 (최종장 시작할 때만). 그림 전체 화면 → 몇 초 뒤 사라지며 지도가 보임 (2026-11-10)
        gateScene = UIFactory.MakeImage("GateScene", transform, Color.white, false);
        UIFactory.Stretch(gateScene.rectTransform);
        gateScene.sprite = Resources.Load<Sprite>("Backgrounds/FortressGateOpen");
        gateScene.preserveAspect = false;
        gateScene.gameObject.SetActive(false);

        // 스테이지 시작 띠 ("1스테이지")
        Image band = UIFactory.MakeImage("Banner", transform, new Color(0.18f, 0.13f, 0.09f, 0.85f), false);
        UIFactory.Place(band.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(PaperW + 24, 190));
        band.raycastTarget = false;
        bannerText = UIFactory.MakeText("BannerText", band.transform, "", 84, UIFactory.Hex("#F0C850"), TextAnchor.MiddleCenter);
        UIFactory.Stretch(bannerText.rectTransform);
        bannerText.fontStyle = FontStyle.Bold;
        bannerText.supportRichText = true;
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

        for (int i = lineLayer.childCount - 1; i >= 0; i--) Destroy(lineLayer.GetChild(i).gameObject);
        for (int i = nodeLayer.childCount - 1; i >= 0; i--) Destroy(nodeLayer.GetChild(i).gameObject);

        RunMap map = run.Map;
        List<MapNode> available = map.Available();

        // 최종장이고 성 그림이 있으면: 양피지를 숨기고 성 그림 위에 칸을 놓음
        onCastle = run.IsFinalChapter && castleMap.sprite != null && map.Boss != null && map.Boss.SpotX >= 0f;
        castleMap.gameObject.SetActive(onCastle);
        paperEdge.enabled = !onCastle;
        paperFill.enabled = !onCastle;
        // 3스테이지(최종장이 아닐 때)이고 지도 그림이 있으면: 그라데이션 대신 그림 지도를 종이에 깖 (3-178)
        // 그림 자체에 얼룩·불탄 자국이 그려져 있으니 가짜 얼룩 동그라미는 숨김
        onStage3Art = !onCastle && !run.IsFinalChapter && stage3MapArt != null;
        paperFill.sprite = onStage3Art ? stage3MapArt : paperGradient;
        paperFill.preserveAspect = false;   // 종이 크기에 꽉 채움 (그림 비율 1.19 ≒ 종이 1.17이라 거의 안 찌그러짐)
        for (int i = 0; i < paper.childCount; i++)
            if (paper.GetChild(i).name == "Stain") paper.GetChild(i).gameObject.SetActive(!onCastle && !onStage3Art);
        MapNodeView.OnDarkMap = onCastle;   // 어두운 그림 위에서는 칸 아이콘 뒤에 양피지색 동그라미를 깔음

        // 선 먼저 (칸 아래에 깔리도록)
        foreach (MapNode a in map.AllNodes())
        {
            foreach (MapNode b in a.Next)
            {
                bool walked = a.Visited && b.Visited;
                bool open = (a == map.Current) && available.Contains(b);
                DrawDotted(PosOf(a), PosOf(b), walked, open, b.Type == MapNodeType.Boss);
            }
        }

        // 칸
        foreach (MapNode n in map.AllNodes())
        {
            RectTransform nrt = UIFactory.MakeRect("Node_" + n.Floor + "_" + n.Column, nodeLayer);
            nrt.anchorMin = nrt.anchorMax = new Vector2(0.5f, 0.5f);
            nrt.pivot = new Vector2(0.5f, 0.5f);
            nrt.anchoredPosition = PosOf(n);
            MapNodeView view = nrt.gameObject.AddComponent<MapNodeView>();
            view.Build(n, this, available.Contains(n), n == map.Current);
        }

        if (restLegendLabel != null) restLegendLabel.text = RunMap.TypeName(MapNodeType.Rest);
        string mapName = run.IsFinalChapter ? "최종장" : run.Stage + "스테이지 · 반격";
        headerText.text = mapName + "   ·   " + (map.Current == null ? "출발할 칸을 고르세요" : (map.Current.Floor + 1) + "층");
        RefreshParty();
        infoTitle.text = "";
        infoBody.text = onCastle
            ? "설계자의 성 안입니다.\n빛나는 칸을 눌러 나아가세요.\n\n칼 = 정예 전투\n뿔 해골 = 엘리트\n모닥불 = 초소 / 모루 = 대장간\n상자 = 보물\n\n맨 위 왕관 해골 = 설계자"
            : "빛나는 칸 중에서 다음에 갈 곳을 고르세요.\n\n길마다 성격이 달라요.\n위험한 길 = 몬스터·엘리트가 많음\n평온한 길 = 이벤트·보물이 많음";

        if (showBanner)
        {
            if (run.IsFinalChapter)
            {
                // 수문장을 쓰러뜨려 성문이 열림 → 열린 성문 그림 위에 "최종장"
                if (gateScene.sprite != null)
                {
                    gateTimer = GateTime;
                    gateScene.gameObject.SetActive(true);
                    gateScene.color = Color.white;
                    gateScene.raycastTarget = true;   // 장면이 보이는 동안 지도 클릭 막기
                }
                ShowBanner("최종장", "열린 성문 너머, 설계자의 방까지", 3.6f);
            }
            else
            {
                ShowBanner(run.Stage + "스테이지  ·  반격", "수문장이 지키는 요새의 문으로", 2.2f);
            }
        }
    }

    // 가운데 띠 (큰 글자 + 작은 글자). seconds 동안 나타났다 사라짐
    public void ShowBanner(string title, string sub, float seconds)
    {
        bannerText.text = title + "\n<size=30>" + sub + "</size>";
        bannerDuration = seconds;
        bannerTimer = seconds;
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
        string s = "<b>파티</b>\n";
        foreach (PlayerCombatant p in run.Players)
        {
            s += p.Name + " " + ClassLibrary.ColoredClassName(p.Class) + "\n   HP " + p.Hp + " / " + p.MaxHp + "\n";
        }
        partyText.text = s;
    }

    // 칸의 위치 (양피지 가운데 기준). 줄마다 살짝 어긋나게 해서 손으로 그린 느낌
    Vector2 PosOf(MapNode n)
    {
        if (onCastle)
        {
            // 성 그림 지도: 칸마다 정해 둔 자리 (MapData.FinalChapter 의 SpotX, SpotY)
            return new Vector2((n.SpotX - 0.5f) * CastleW, (0.5f - n.SpotY) * CastleH + CastleOffsetY);
        }

        // 층 수가 적은 지도(최종장 5층)도 양피지 전체에 고르게 펼치도록 간격을 늘림
        //   3-83: 층 수가 바뀌어도(7 → 8) 양피지 높이는 그대로 쓰도록, 예전 7층 지도의 전체 높이(FloorGap × 6)를 기준으로 나눔
        int floorCount = run.Map.Floors.Length;
        float gap = floorCount > 1 ? FloorGap * MapSpanFloors / (floorCount - 1) : FloorGap;
        float y = -PaperH / 2f + 90f + n.Floor * gap;
        if (n.Type == MapNodeType.Boss) return new Vector2(0, y + 10f);
        float x = (n.Column - (GameBalance.MapColumns - 1) / 2f) * ColGap;
        System.Random r = new System.Random(n.Floor * 131 + n.Column * 17 + run.Stage * 7);   // 같은 칸은 항상 같은 어긋남
        x += (float)(r.NextDouble() * 50 - 25);
        y += (float)(r.NextDouble() * 30 - 15);
        return new Vector2(x, y);
    }

    // a → b 점선. walked = 지나온 길(진하게), open = 지금 갈 수 있는 길(조금 진하게)
    void DrawDotted(Vector2 a, Vector2 b, bool walked, bool open, bool toBoss)
    {
        Vector2 dir = b - a;
        float length = dir.magnitude;
        dir /= length;
        float startGap = 40f, endGap = toBoss ? 80f : 40f;
        Color c = walked ? new Color(0.25f, 0.12f, 0.06f, 0.95f)
                : (open ? new Color(0.3f, 0.2f, 0.1f, 0.85f) : new Color(0.3f, 0.2f, 0.1f, 0.4f));
        if (onCastle)
        {
            DrawGlowDashes(a, b, dir, length, startGap, endGap, walked, open);
            return;
        }
        float size = walked ? 9f : (open ? 8f : 6f);
        if (onStage3Art)
        {
            // 3스테이지 그림 지도 (3-179): 그림 속 산·불 때문에 옅은 점이 묻힘
            // → 점을 더 크고 진하게 + 점 밑에 밝은 양피지색 테두리 동그라미를 깔아서 어디서든 또렷하게
            //   (Java로 치면 글자에 흰 외곽선을 둘러 배경과 떼어 놓는 것과 같은 방법)
            DrawStage3Dots(a, dir, length, startGap, endGap, walked, open);
            return;
        }
        for (float d = startGap; d < length - endGap; d += 17f)
        {
            Image dot = UIFactory.MakeCircle("Dot", lineLayer, c);
            dot.raycastTarget = false;
            dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            dot.rectTransform.sizeDelta = new Vector2(size, size);
            dot.rectTransform.anchoredPosition = a + dir * d;
        }
    }

    // 3스테이지 그림 지도용 점선 (3-179)
    // 밝은 테두리 동그라미(halo) 위에 진한 갈색 점. 갈 수 없는 길도 예전(투명도 0.4)보다 진하게
    void DrawStage3Dots(Vector2 a, Vector2 dir, float length, float startGap, float endGap, bool walked, bool open)
    {
        Color dotColor = walked ? new Color(0.30f, 0.08f, 0.04f, 1f)          // 지나온 길 = 짙은 적갈색
                       : (open ? new Color(0.16f, 0.09f, 0.04f, 1f)            // 갈 수 있는 길 = 아주 진한 갈색
                               : new Color(0.22f, 0.14f, 0.07f, 0.9f));       // 나머지 길 = 조금 옅은 갈색
        Color haloColor = new Color(0.98f, 0.92f, 0.76f, walked || open ? 0.85f : 0.55f);  // 밝은 양피지색
        float size = walked ? 11f : (open ? 10f : 8f);
        float haloExtra = 5f;     // 테두리 두께 = (haloExtra / 2) 씩 바깥으로
        for (float d = startGap; d < length - endGap; d += 18f)
        {
            Vector2 pos = a + dir * d;
            // 1) 밑에 까는 밝은 동그라미 (먼저 만든 게 뒤에 그려짐)
            Image halo = UIFactory.MakeCircle("DotHalo", lineLayer, haloColor);
            halo.raycastTarget = false;
            halo.rectTransform.anchorMin = halo.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            halo.rectTransform.sizeDelta = new Vector2(size + haloExtra, size + haloExtra);
            halo.rectTransform.anchoredPosition = pos;
            // 2) 위에 올리는 진한 점
            Image dot = UIFactory.MakeCircle("Dot", lineLayer, dotColor);
            dot.raycastTarget = false;
            dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            dot.rectTransform.sizeDelta = new Vector2(size, size);
            dot.rectTransform.anchoredPosition = pos;
        }
    }

    // 최종장 성 지도: 빛나는 금색 짧은 선(대시) + 아래에 은은한 빛 띠. 지나온 길은 붉은색 (예시 그림 느낌)
    void DrawGlowDashes(Vector2 a, Vector2 b, Vector2 dir, float length, float startGap, float endGap, bool walked, bool open)
    {
        Color dash = walked ? new Color(1f, 0.35f, 0.25f, 1f) : (open ? new Color(1f, 0.82f, 0.4f, 1f) : new Color(1f, 0.82f, 0.4f, 0.55f));
        Color glow = walked ? new Color(1f, 0.2f, 0.1f, 0.35f) : new Color(1f, 0.6f, 0.15f, open ? 0.3f : 0.12f);
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        // 빛 띠 (선 전체 길이, 두껍고 흐림)
        Image band = UIFactory.MakeImage("Glow", lineLayer, glow, false);
        band.sprite = UIFactory.SoftGlowSprite;
        band.raycastTarget = false;
        RectTransform brt = band.rectTransform;
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
        brt.sizeDelta = new Vector2(length - startGap - endGap + 30f, 34f);
        brt.anchoredPosition = a + dir * (startGap + (length - startGap - endGap) / 2f);
        brt.localEulerAngles = new Vector3(0, 0, angle);

        // 대시
        for (float d = startGap; d < length - endGap; d += 24f)
        {
            Image seg = UIFactory.MakeImage("Dash", lineLayer, dash, false);
            seg.raycastTarget = false;
            RectTransform srt = seg.rectTransform;
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
            srt.sizeDelta = new Vector2(14f, walked || open ? 5f : 4f);
            srt.anchoredPosition = a + dir * (d + 7f);
            srt.localEulerAngles = new Vector3(0, 0, angle);
        }
    }

    // 이 칸의 자리 (멀티 투표 표시를 붙이려고). 없으면 null
    public RectTransform RectOf(MapNode node)
    {
        foreach (MapNodeView v in nodeLayer.GetComponentsInChildren<MapNodeView>())
            if (v.Node == node) return (RectTransform)v.transform;
        return null;
    }

    // ---------------- 입력 (MapNodeView 가 부름) ----------------
    public void OnNodeHover(MapNodeView view, bool enter)
    {
        if (!enter) return;
        MapNode n = view.Node;
        infoTitle.text = RunMap.TypeName(n.Type) + "  <size=20>" + (n.Floor + 1) + "층</size>";
        string body = RunMap.Description(n.Type);
        // (적 구성은 들어갈 때 무작위로 정해짐 → 미리 보여 주지 않음)
        if (n.Type != MapNodeType.Boss) body += "\n\n" + RunMap.ThemeName(n.Theme);
        body += view.IsAvailable ? "\n\n<color=#9CFF9C>클릭해서 이동</color>" : (n.Visited ? "\n\n(지나온 칸)" : "\n\n(지금은 갈 수 없음)");
        infoBody.text = body;
    }

    public void OnNodeClick(MapNodeView view)
    {
        if (!view.IsAvailable || bannerTimer > 0.8f || gateTimer > 0.5f) return;   // 띠가 떠 있는 동안은 잠깐 막음
        if (OnNodeClicked != null) OnNodeClicked(view.Node);
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

        if (gateTimer > 0f)
        {
            gateTimer -= Time.deltaTime;
            gateScene.color = new Color(1f, 1f, 1f, Mathf.Clamp01(gateTimer / 1f));   // 마지막 1초 동안 사라짐
            if (gateTimer <= 0f) gateScene.gameObject.SetActive(false);
        }

        if (bannerTimer > 0f)
        {
            bannerTimer -= Time.deltaTime;
            // 처음 0.3초 나타남 → 유지 → 마지막 0.6초 사라짐
            float shown = bannerDuration - bannerTimer;
            banner.alpha = Mathf.Min(Mathf.Clamp01(shown / 0.3f), Mathf.Clamp01(bannerTimer / 0.6f));
        }
        else
        {
            banner.alpha = 0f;
        }
    }
}
