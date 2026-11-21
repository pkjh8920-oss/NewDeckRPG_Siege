// ============================================================
// AltarPanel.cs
// 제물의 제단 (3-114): 엘리트를 잡은 뒤 보상·레벨 업이 끝나면 나옴 (안 해도 됨)
//   1단계: 덱에서 카드 3장(GameBalance.AltarSacrifice)을 골라 바침  ([바치지 않기] 로 넘길 수 있음)
//   2단계 (3-127): 직업 전용 카드 10장 (위 5 + 아래 5). 한 장씩 등급을 따로 뽑음 (보통 일반 8 + 희귀 2) → 원하는 1장을 얻음
//   멀티면 사람마다 차례로 (레벨 업 화면처럼)
// 3-129: 같은 화면을 [피의 제단] 에도 씀 (blood = true)
//   1·2스테이지 4칸 이벤트 자리. 2장 바침 → 직업 카드 3장(희귀 이상) 중 1장 + 유물 1개, 대가 최대 HP -10%
//   자바로 치면 같은 클래스에 boolean 모드 플래그를 두고 숫자·문장만 바꿔 쓰는 것
// 자바와 비교: 카드 고르기 = HashSet 처럼 "골랐는지" 목록을 두고 클릭할 때마다 넣고 빼기
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AltarPanel : MonoBehaviour
{
    private RectTransform root, grid;
    private GameObject bloodScene;
    // 3-153: 피의 제단 버튼 그림 (UI/Altar/Altar_Offer · Altar_Leave). 제물의 제단은 예전 색 버튼 그대로
    private Sprite offerArt, leaveArt;
    private Sprite gothicOfferArt, gothicLeaveArt;   // 3-181: 제물의 제단 버튼 그림 (4번 그림: 청동 가시틀 / 쇠 가시틀)
    private Color mainColor, skipColor;
    private Vector2 mainSize, skipSize;     // 3-148: 피의 제단 배경 그림 (Backgrounds/BloodAltar) + 어둡게 덮는 판. 피의 제단일 때만 보임
    private Text title, sub;
    private Button mainButton, skipButton;
    private Text mainLabel;

    private RunState run;
    private List<PlayerCombatant> queue = new List<PlayerCombatant>();
    private PlayerCombatant current;
    private System.Action onDone;
    private List<CardData> selected = new List<CardData>();          // 바칠 카드 (1단계)
    private Dictionary<CardData, Image> marks = new Dictionary<CardData, Image>();   // 고른 카드 위 붉은 표시
    private bool choosingReward;                                       // 2단계인지
    private bool blood;                                                // 3-129: true = 피의 제단 모드
    private List<string> results = new List<string>();                 // 3-129: 피의 제단 결과 문장 (끝나면 화면에 알림)

    // 모드에 따라 달라지는 숫자
    int NeedCount { get { return blood ? GameBalance.BloodAltarSacrifice : GameBalance.AltarSacrifice; } }
    string AltarName { get { return blood ? RunState.BloodAltarName : "제물의 제단"; } }
    public List<string> Results { get { return results; } }
    public bool NobodyEligible { get; private set; }                   // 3-129: 덱이 적어서 아무도 못 썼는지

    public static AltarPanel Create(Transform parent)
    {
        RectTransform rt = UIFactory.MakeRect("Altar", parent);
        UIFactory.Stretch(rt);
        AltarPanel panel = rt.gameObject.AddComponent<AltarPanel>();
        panel.root = rt;
        panel.Build();
        rt.gameObject.SetActive(false);
        return panel;
    }

    void Build()
    {
        Image dim = UIFactory.MakeImage("Dim", root, new Color(0.03f, 0.01f, 0.02f, 0.94f), false);
        UIFactory.Stretch(dim.rectTransform);
        // 3-148: 피의 제단 배경 그림 (화면을 꽉 채움, 넘치는 곳은 잘림) + 카드·글자가 잘 보이게 반투명 검은 판
        Sprite bloodArt = Resources.Load<Sprite>("Backgrounds/BloodAltar");
        if (bloodArt != null)
        {
            RectTransform scene = UIFactory.MakeRect("BloodScene", root);
            UIFactory.Stretch(scene);
            scene.gameObject.AddComponent<RectMask2D>();
            Image bg = UIFactory.MakeImage("Art", scene, Color.white, false);
            bg.sprite = bloodArt;
            bg.raycastTarget = false;
            bg.rectTransform.anchorMin = bg.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            AspectRatioFitter fit = bg.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = bloodArt.rect.width / bloodArt.rect.height;
            Image shade = UIFactory.MakeImage("Shade", scene, new Color(0f, 0f, 0f, 0.5f), false);   // 0.5 = 그림이 보이면서 카드가 묻히지 않을 정도
            UIFactory.Stretch(shade.rectTransform);
            shade.raycastTarget = false;
            bloodScene = scene.gameObject;
            bloodScene.SetActive(false);
        }
        // 붉은 제단 빛 (가운데 아래)
        Image glow = UIFactory.MakeImage("Glow", root, new Color(0.8f, 0.15f, 0.1f, 0.18f), false);
        glow.sprite = UIFactory.SoftGlowSprite;
        glow.raycastTarget = false;
        UIFactory.Place(glow.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(1600, 700));

        title = UIFactory.MakeText("Title", root, "제물의 제단", 52, UIFactory.Hex("#E86A4A"), TextAnchor.MiddleCenter);
        UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -24), new Vector2(1400, 70));
        title.fontStyle = FontStyle.Bold;
        sub = UIFactory.MakeText("Sub", root, "", 24, UIFactory.Hex("#EDE4D2"), TextAnchor.MiddleCenter);
        UIFactory.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -96), new Vector2(1500, 64));
        sub.supportRichText = true;
        sub.resizeTextForBestFit = true;   // 3-154
        sub.resizeTextMinSize = 16;
        sub.resizeTextMaxSize = 24;
        sub.verticalOverflow = VerticalWrapMode.Truncate;

        grid = UIFactory.MakeRect("Grid", root);
        UIFactory.Place(grid, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(1700, 780));

        mainButton = UIFactory.MakeButton("Main", root, "제물 바치기", UIFactory.Hex("#8A2A20"), OnMain, 28);
        UIFactory.Place((RectTransform)mainButton.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(160, 26), new Vector2(300, 66));
        mainLabel = mainButton.GetComponentInChildren<Text>();
        skipButton = UIFactory.MakeButton("Skip", root, "바치지 않기", UIFactory.Hex("#3A3446"), OnSkip, 26);
        UIFactory.Place((RectTransform)skipButton.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-160, 26), new Vector2(260, 66));
        offerArt = Resources.Load<Sprite>("UI/Altar/Altar_Offer");
        leaveArt = Resources.Load<Sprite>("UI/Altar/Altar_Leave");
        gothicOfferArt = Resources.Load<Sprite>("UI/Altar/Altar_Gothic_Offer");
        gothicLeaveArt = Resources.Load<Sprite>("UI/Altar/Altar_Gothic_Leave");
        mainColor = mainButton.GetComponent<Image>().color;
        skipColor = skipButton.GetComponent<Image>().color;
        mainSize = ((RectTransform)mainButton.transform).sizeDelta;
        skipSize = ((RectTransform)skipButton.transform).sizeDelta;
    }

    // 3-153: 버튼 모양 바꾸기. art 가 있으면 그림 버튼(크기 artSize), 없으면 예전 둥근 색 버튼(색·크기 되돌림)
    //   글자는 그대로 코드가 씀 ("제물 바치기", "떠나기" ...)
    static void SetButtonArt(Button b, Sprite art, Color oldColor, Vector2 oldSize, Vector2 artSize)
    {
        Image img = b.GetComponent<Image>();
        RectTransform rt = (RectTransform)b.transform;
        if (art != null)
        {
            img.sprite = art;
            img.type = Image.Type.Simple;
            img.color = Color.white;
            rt.sizeDelta = artSize;
        }
        else
        {
            img.sprite = UIFactory.Rounded;
            img.type = Image.Type.Sliced;
            img.color = oldColor;
            rt.sizeDelta = oldSize;
        }
    }

    public bool IsOpen { get { return gameObject.activeSelf; } }

    // 덱이 충분한 사람만 차례로. 아무도 없으면 바로 done
    //   bloodAltar = true 면 피의 제단 (3-129)
    public void Open(RunState r, List<PlayerCombatant> players, System.Action done, bool bloodAltar = false)
    {
        run = r;
        onDone = done;
        blood = bloodAltar;
        if (bloodScene != null) bloodScene.SetActive(blood);   // 3-148: 피의 제단이면 배경 그림
        // 3-153: 피의 제단 = 가시 틀 + 붉은 보석 / 3-181: 제물의 제단 = 새 고딕 틀 (그림 비율 약 2.65 : 1)
        if (blood)
        {
            SetButtonArt(mainButton, offerArt, mainColor, mainSize, new Vector2(330, 107));
            SetButtonArt(skipButton, leaveArt, skipColor, skipSize, new Vector2(262, 74));
        }
        else
        {
            SetButtonArt(mainButton, gothicOfferArt, mainColor, mainSize, new Vector2(318, 120));
            SetButtonArt(skipButton, gothicLeaveArt, skipColor, skipSize, new Vector2(290, 110));
        }
        results.Clear();
        queue.Clear();
        foreach (PlayerCombatant p in players)
            if (blood ? run.BloodAltarAvailable(p) : run.AltarAvailable(p)) queue.Add(p);
        skipButton.GetComponentInChildren<Text>().text = blood ? "떠나기" : "바치지 않기";
        NobodyEligible = queue.Count == 0;
        if (queue.Count == 0) { Finish(); return; }
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Next();
    }

    void Next()
    {
        ((RectTransform)mainButton.transform).anchoredPosition = new Vector2(160, 26);   // 1단계 자리로 (2단계에서 가운데로 옮겼던 것 되돌리기)
        current = queue[0];
        queue.RemoveAt(0);
        selected.Clear();
        choosingReward = false;
        DrawSacrifice();
    }

    void Finish()
    {
        gameObject.SetActive(false);
        System.Action d = onDone;
        onDone = null;
        if (d != null) d();
    }

    void ClearGrid()
    {
        for (int i = grid.childCount - 1; i >= 0; i--) Destroy(grid.GetChild(i).gameObject);
        marks.Clear();
    }

    // 카드 n장을 grid 안에 줄 맞춰 놓을 자리와 크기 (장 수가 많으면 작게)
    //   forceCols > 0 이면 한 줄 장 수를 그 값으로 고정 (3-127 제단 보상 = 5장씩 2줄)
    void Layout(int n, out int cols, out float scale, int forceCols = 0)
    {
        cols = forceCols > 0 ? forceCols : (n <= 7 ? Mathf.Max(n, 1) : (n <= 20 ? 10 : 12));
        int rows = Mathf.CeilToInt(n / (float)cols);
        float byWidth = grid.rect.width / (cols * (CardView.Width + 24f));
        float byHeight = grid.rect.height / (rows * (CardView.Height + 24f));
        scale = Mathf.Min(0.85f, byWidth, byHeight);
    }

    Vector2 SlotPos(int i, int n, int cols, float scale)
    {
        int rows = Mathf.CeilToInt(n / (float)cols);
        int row = i / cols, col = i % cols;
        int inRow = Mathf.Min(cols, n - row * cols);
        float w = (CardView.Width + 24f) * scale, h = (CardView.Height + 24f) * scale;
        return new Vector2((col - (inRow - 1) / 2f) * w, ((rows - 1) / 2f - row) * h);
    }

    // ---- 1단계: 바칠 카드 고르기 ----
    void DrawSacrifice()
    {
        ClearGrid();
        int need = NeedCount;
        title.text = AltarName + (run.Players.Count > 1 ? "  ·  " + current.Name + " " + ClassLibrary.ColoredClassName(current.Class) : "");
        sub.text = SacrificeText(need);
        int n = current.Deck.Count;
        int cols; float scale;
        Layout(n, out cols, out scale);
        for (int i = 0; i < n; i++)
        {
            CardData c = current.Deck[i];
            CardView v = CardView.CreateDisplay(grid, c, current.ManaName, SlotPos(i, n, cols, scale), scale, () => ToggleSacrifice(c));
            // 고른 카드 위 붉은 막 + "제물" 글자
            Image mark = UIFactory.MakeImage("Mark", v.transform, new Color(0.6f, 0.05f, 0.05f, 0.55f));
            UIFactory.Stretch(mark.rectTransform, 9, 9, 9, 9);
            mark.raycastTarget = false;
            Text mt = UIFactory.MakeText("T", mark.transform, "제물", 44, Color.white, TextAnchor.MiddleCenter);
            UIFactory.Stretch(mt.rectTransform);
            mt.fontStyle = FontStyle.Bold;
            mark.gameObject.SetActive(selected.Contains(c));
            marks[c] = mark;
        }
        mainLabel.text = "제물 바치기";
        mainButton.interactable = selected.Count == need;
        skipButton.gameObject.SetActive(true);
    }

    // 1단계 안내 문장
    string SacrificeText(int need)
    {
        if (blood)
            return "덱에서 카드 <color=#FF8A6A>" + need + "장</color>을 바치면, 직업 카드 " + GameBalance.BloodAltarOfferCount + "장(희귀 이상) 중 <color=#F0C850>1장</color> + <color=#F0C850>유물 1개</color>\n"
                 + "대가: <color=#FF5A4A>최대 HP -" + run.BloodAltarHpCost(current) + " (영구)</color>"
                 + "   <size=20><color=#C8C0B0>고른 카드 " + selected.Count + " / " + need + "</color></size>";
        return "덱에서 카드 <color=#FF8A6A>" + need + "장</color>을 바치면, 직업 카드 " + GameBalance.AltarOfferCount + "장 중 <color=#F0C850>원하는 1장</color>을 얻습니다.\n"
             + "<size=20><color=#C8C0B0>고른 카드 " + selected.Count + " / " + need + "   (다시 누르면 취소)</color></size>";
    }

    void ToggleSacrifice(CardData c)
    {
        if (choosingReward) return;
        if (selected.Contains(c)) selected.Remove(c);
        else if (selected.Count < NeedCount) selected.Add(c);
        foreach (KeyValuePair<CardData, Image> kv in marks) kv.Value.gameObject.SetActive(selected.Contains(kv.Key));
        int need = NeedCount;
        sub.text = SacrificeText(need);
        mainButton.interactable = selected.Count == need;
    }

    // ---- 2단계: 등급이 정해지고, 그 등급 카드 중 1장 ----
    void OnMain()
    {
        if (choosingReward || selected.Count != NeedCount) return;
        choosingReward = true;
        // 3-127: 직업 카드 10장 (등급은 한 장씩 따로) / 3-129 피의 제단: 3장 (희귀 이상)
        List<CardData> list = blood ? run.BloodAltarOffer(current) : run.AltarOffer(current);
        foreach (CardData oc in list) Discovery.SeeCard(oc.Id);   // 3-180 도감: 제단 후보 카드도 "본 것" (후보 목록은 런에 안 남아서 여기서 기록)
        ClearGrid();
        title.text = blood ? "피의 제단이 응답했다!" : "제단이 응답했다!";
        sub.text = OfferSummary(list) + "\n<size=20><color=#C8C0B0>원하는 1장을 고르세요. (바친 카드 " + selected.Count + "장은 덱에서 사라집니다)</color></size>";
        int n = list.Count;
        int cols; float scale;
        Layout(n, out cols, out scale, blood ? 0 : GameBalance.AltarOfferCols);   // 제물: 위 5 + 아래 5 / 피: 한 줄
        for (int i = 0; i < n; i++)
        {
            CardData c = list[i];
            CardView.CreateDisplay(grid, c, current.ManaName, SlotPos(i, n, cols, scale), scale, () => TakeReward(c));
        }
        mainButton.interactable = false;
        mainLabel.text = "카드를 고르세요";
        skipButton.gameObject.SetActive(false);   // 바친 뒤에는 되돌릴 수 없음
        // 3-127: [바치지 않기] 가 사라지니 안내 버튼을 가운데로
        ((RectTransform)mainButton.transform).anchoredPosition = new Vector2(0, 26);
    }

    void TakeReward(CardData c)
    {
        if (!choosingReward) return;
        if (blood) results.Add(run.BloodAltarExchange(current, selected, c));   // 유물 + 최대 HP 감소까지
        else run.AltarExchange(current, selected, c);
        choosingReward = false;
        if (queue.Count > 0) Next();
        else Finish();
    }

    void OnSkip()
    {
        if (choosingReward) return;
        if (queue.Count > 0) Next();
        else Finish();
    }

    // "일반 8 · 희귀 2" 처럼 이번에 나온 등급 개수를 색깔 글자로
    string OfferSummary(List<CardData> list)
    {
        CardRarity[] tiers = { CardRarity.Common, CardRarity.Rare, CardRarity.Hero, CardRarity.Legend };
        string s = "";
        foreach (CardRarity t in tiers)
        {
            int n = 0;
            foreach (CardData c in list) if (c.Rarity == t) n++;
            if (n == 0) continue;
            if (s.Length > 0) s += "  ·  ";
            s += "<color=#" + ColorUtility.ToHtmlStringRGB(CardView.RarityColor(t)) + ">" + RarityName(t) + " " + n + "</color>";
        }
        return s;
    }

    static string RarityName(CardRarity r)
    {
        switch (r)
        {
            case CardRarity.Rare: return "희귀";
            case CardRarity.Hero: return "영웅";
            case CardRarity.Legend: return "전설";
            default: return "일반";
        }
    }
}
