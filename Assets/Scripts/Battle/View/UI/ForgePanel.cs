// ============================================================
// ForgePanel.cs
// 대장간 화면. 대장간 그림 위에 드워프 장인 "브론"과 모루가 서 있음
//
//   1) 들어오면 브론이 말을 검 (아래 대화 상자, 글자가 한 자씩 나옴)
//   2) 모루를 누르면 → [카드 강화] / [장비 강화] 고르기
//        카드 강화: 덱에서 1장 고르기 → 강화 전·후 비교 → [강화하기]
//        장비 강화 (3-123): 무기 강화(공격 피해 +3) / 갑옷 강화(전투 시작 방어도 +3) / 체력 강화(최대 HP +5), 각 최대 3번
//        (카드 강화든 장비 강화든 한 사람당 한 번)
//   3) 위쪽 P1 / P2 탭 = 각자 자기 화면에서 고르는 것 (한 PC 테스트용. 네트워크가 붙으면 자기 것만 보임)
//   4) 모두 강화하면 브론이 인사하고 자동으로 나감. [떠나기] 로 먼저 나갈 수도 있음
//
// 3-148: 새 그림 (Resources/UI/Forge/*) 이 있으면
//   위 탭 = 금속 틀 탭 / 모루 메뉴 = 사슬 판 (카드 강화 · 장비 강화 단추가 그림에 있음)
//   장비 강화 = 가운데 큰 판 (무기 · 갑옷 · 체력 줄과 그림이 그림에 있음, 글자만 코드가 씀) / [떠나기] = 그림 단추
//   단추 자리는 그림 픽셀 좌표 (UIFactory.ArtSpot). 그림이 없으면 예전 모양 그대로
// 규칙(강화 가능 여부, 단계)은 RunState 가 정함. 이 파일은 그리기와 버튼만
// 자바와 비교: System.Action<T> = 자바의 Consumer<T>, System.Func<T, R> = 자바의 Function<T, R>
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ForgePanel : MonoBehaviour
{
    public const string SmithName = "드워프 장인 브론";

    // BattleScreen 이 넣어 주는 함수들
    public System.Func<PlayerCombatant, WeaponBranch, string> OnUpgrade;   // 강화하고 결과 문장을 돌려줌
    public System.Func<PlayerCombatant, int, string> OnCardUpgrade;        // 덱의 몇 번째 카드를 강화하고 결과 문장을 돌려줌
    public System.Action OnLeave;                                          // 대장간을 나감

    private RunState run;
    private int current;                     // 지금 고르고 있는 플레이어 번호 (P1 = 0)

    private RectTransform tabLayer;
    private RectTransform dwarf;
    private RectTransform anvil;
    private Image anvilGlow;
    private Text anvilHint;
    private Text dialogText;
    private RectTransform menu;              // 모루를 누르면 나오는 선택 상자
    private RectTransform menuButtons;
    private Text menuTitle;
    private Button leaveButton;
    private RectTransform cardLayer;         // 카드 강화: 덱 고르기 / 비교 화면
    private int cardPage;
    private const int CardsPerPage = 16;     // 8장 × 2줄

    // 3-148: 새 그림
    private Sprite tabSprite, tabOnSprite;
    private bool artMenu;                    // 모루 메뉴가 그림 판인지
    private RectTransform equip;             // 장비 강화 판 (그림이 있을 때만)
    private RectTransform equipContent;      // 장비 강화 판 위 글자·단추 (열 때마다 새로)
    private Text equipTitle;
    const float MenuW = 398f, MenuH = 374f;  // 모루 메뉴 그림 크기 (좌표 단위)
    const float EquipW = 1012f, EquipH = 755f;
    static readonly float[] EquipRowY = { 177f, 327f, 477f };   // 장비 강화 판 속 줄 3개의 위쪽 y (줄 높이 125)

    private string fullLine = "";            // 대화 상자에 다 나올 문장
    private float typed;                     // 지금까지 나온 글자 수 (한 자씩 나오는 효과)
    private bool anvilHover;
    private bool leaving;

    public static ForgePanel Create(Transform parent)
    {
        RectTransform rt = UIFactory.MakeRect("ForgePanel", parent);
        UIFactory.Stretch(rt, 0, 0, 64, 0);   // 맨 위 64 = 위쪽 버튼 줄은 가리지 않음
        ForgePanel panel = rt.gameObject.AddComponent<ForgePanel>();
        panel.BuildStatic();
        rt.gameObject.SetActive(false);
        return panel;
    }

    void BuildStatic()
    {
        // 바탕: 검은 판 + 대장간 그림 (화면 비율이 달라도 꽉 채움)
        Image black = UIFactory.MakeImage("Black", transform, Color.black, false);
        UIFactory.Stretch(black.rectTransform);
        Image bg = UIFactory.MakeImage("ForgeArt", transform, Color.white, false);
        bg.sprite = Resources.Load<Sprite>("Backgrounds/Forge");
        UIFactory.Stretch(bg.rectTransform);
        bg.raycastTarget = false;
        AspectRatioFitter fit = bg.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fit.aspectRatio = 16f / 9f;
        if (bg.sprite == null) bg.color = UIFactory.Hex("#2A1C16");

        // 제목
        Text title = UIFactory.MakeText("Title", transform, "대장간", 54, UIFactory.Hex("#FFD27A"), TextAnchor.MiddleCenter);
        UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -16), new Vector2(600, 70));

        // 플레이어 탭
        tabLayer = UIFactory.MakeRect("Tabs", transform);
        UIFactory.Place(tabLayer, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -96), new Vector2(1000, 64));

        // 모루 (버튼): 뒤에 은은한 빛 → 마우스를 올리면 밝아짐
        anvil = UIFactory.MakeRect("Anvil", transform);
        UIFactory.Place(anvil, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-170, 150), new Vector2(480, 380));   // 2026-11-14: 새 모루 그림(나무 그루터기 포함) → 바닥에 놓이게 크게·아래로
        anvilGlow = UIFactory.MakeImage("Glow", anvil, new Color(1f, 0.6f, 0.2f, 0.25f), false);
        anvilGlow.sprite = UIFactory.SoftGlowSprite;
        UIFactory.Place(anvilGlow.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(620, 420));
        anvilGlow.raycastTarget = false;
        Image anvilImg = UIFactory.MakeImage("AnvilArt", anvil, Color.white, false);
        anvilImg.sprite = Resources.Load<Sprite>("Art/Anvil");
        anvilImg.preserveAspect = true;
        UIFactory.Stretch(anvilImg.rectTransform);
        if (anvilImg.sprite == null) anvilImg.color = UIFactory.Hex("#5A5E68");
        Button anvilButton = anvilImg.gameObject.AddComponent<Button>();
        anvilButton.transition = Selectable.Transition.None;   // 색은 직접 바꿈 (빛)
        anvilButton.onClick.AddListener(OnAnvilClicked);
        anvilButton.onClick.AddListener(() => SoundManager.Play("Click"));   // 3-259: 다른 버튼과 같은 누르는 소리
        HoverRelay hover = anvilImg.gameObject.AddComponent<HoverRelay>();
        hover.OnChange = (on) => anvilHover = on;

        anvilHint = UIFactory.MakeText("Hint", anvil, "▼ 모루를 눌러 강화", 26, UIFactory.Hex("#FFE2A0"), TextAnchor.MiddleCenter);
        UIFactory.Place(anvilHint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0, 20), new Vector2(400, 40));

        // 드워프 장인 (모루 오른쪽, 화로 앞)
        // 2026-11-09: 새 대장장이 그림 (불타는 망치 때문에 옆으로 넓음, 1016×1179) → 칸을 넓고 크게
        dwarf = UIFactory.MakeRect("Dwarf", transform);
        UIFactory.Place(dwarf, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(430, 225), new Vector2(560, 650));
        Image dwarfImg = UIFactory.MakeImage("DwarfArt", dwarf, Color.white, false);
        dwarfImg.sprite = Resources.Load<Sprite>("Art/Dwarf");
        dwarfImg.preserveAspect = true;
        dwarfImg.raycastTarget = false;
        UIFactory.Stretch(dwarfImg.rectTransform);
        if (dwarfImg.sprite == null) dwarfImg.color = UIFactory.Hex("#B8763A");

        // 아래 대화 상자 (이름표 + 대사)
        Image box = UIFactory.MakeImage("DialogBox", transform, new Color(0.08f, 0.05f, 0.04f, 0.92f));
        UIFactory.Place(box.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(1500, 190));
        box.gameObject.AddComponent<Outline>().effectColor = UIFactory.Hex("#C89A50");
        Image plate = UIFactory.MakeImage("NamePlate", box.transform, UIFactory.Hex("#6A3A1E"));
        UIFactory.Place(plate.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(40, 0), new Vector2(330, 52));
        plate.gameObject.AddComponent<Outline>().effectColor = UIFactory.Hex("#C89A50");
        Text nameText = UIFactory.MakeText("Name", plate.transform, SmithName, 26, UIFactory.Hex("#FFE2A0"), TextAnchor.MiddleCenter);
        UIFactory.Stretch(nameText.rectTransform);
        dialogText = UIFactory.MakeText("Line", box.transform, "", 28, UIFactory.Hex("#F4E6C8"), TextAnchor.UpperLeft);
        UIFactory.Stretch(dialogText.rectTransform, 48, 48, 46, 20);
        // 대화 상자를 누르면 글자가 한 번에 다 나옴
        Button skip = box.gameObject.AddComponent<Button>();
        skip.transition = Selectable.Transition.None;
        skip.onClick.AddListener(() => typed = fullLine.Length);

        // 떠나기
        leaveButton = UIFactory.MakeButton("Leave", transform, "떠나기", UIFactory.Hex("#4A3020"), () => { if (NetGate.Intercept("FLEAVE|" + NetGate.Me)) return; Leave(); }, 28);
        UIFactory.Place((RectTransform)leaveButton.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40, 250), new Vector2(220, 70));

        // 모루 선택 상자 (평소엔 숨김). 왼쪽 벽 앞에 떠서 드워프·모루를 가리지 않음
        Image m = UIFactory.MakeImage("Menu", transform, new Color(0.1f, 0.07f, 0.05f, 0.95f));
        menu = m.rectTransform;
        UIFactory.Place(menu, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-600, 60), new Vector2(560, 520));
        m.gameObject.AddComponent<Outline>().effectColor = UIFactory.Hex("#C89A50");
        menuTitle = UIFactory.MakeText("MenuTitle", menu, "", 30, UIFactory.Hex("#FFD27A"), TextAnchor.MiddleCenter);
        UIFactory.Place(menuTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -16), new Vector2(520, 80));
        menuButtons = UIFactory.MakeRect("Buttons", menu);
        UIFactory.Stretch(menuButtons, 30, 30, 100, 30);
        menu.gameObject.SetActive(false);

        // 카드 강화 화면 (평소엔 숨김). 대장간 위를 어둡게 덮음
        Image cl = UIFactory.MakeImage("CardLayer", transform, new Color(0.02f, 0.015f, 0.01f, 0.92f), false);
        cardLayer = cl.rectTransform;
        UIFactory.Stretch(cardLayer);
        cardLayer.gameObject.SetActive(false);

        BuildArt(title);
    }

    // 3-148: 새 그림이 있으면 탭·메뉴·떠나기 모양을 바꾸고, 장비 강화 판을 만듦
    void BuildArt(Text title)
    {
        tabSprite = Resources.Load<Sprite>("UI/Forge/Forge_Tab");
        tabOnSprite = Resources.Load<Sprite>("UI/Forge/Forge_TabOn");
        if (tabSprite != null && tabOnSprite != null)
        {
            title.rectTransform.anchoredPosition = new Vector2(0, -4);     // 목업처럼 제목을 조금 위로
            tabLayer.anchoredPosition = new Vector2(0, -88);
        }

        Sprite menuArt = Resources.Load<Sprite>("UI/Forge/Forge_Menu");
        if (menuArt != null)
        {
            artMenu = true;
            Image m = menu.GetComponent<Image>();
            m.sprite = menuArt;
            m.type = Image.Type.Simple;
            m.color = Color.white;
            Outline ol = m.GetComponent<Outline>();
            if (ol != null) Destroy(ol);
            // 목업 자리: 화면 왼쪽, 세로 가운데 (그림 398×374 → 화면 746×701)
            UIFactory.Place(menu, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(45, -10), new Vector2(746, 701));
            UIFactory.Stretch(menuButtons);
            RectTransform tr = UIFactory.ArtSpot("TitleSpot", menu, MenuW, MenuH, 30, 40, 368, 86);
            menuTitle.transform.SetParent(tr, false);
            UIFactory.Stretch(menuTitle.rectTransform);
            menuTitle.fontSize = 34;
            menuTitle.color = UIFactory.Hex("#FFB050");
            menuTitle.resizeTextForBestFit = true;
            menuTitle.verticalOverflow = VerticalWrapMode.Truncate;   // 3-154: 글자 자동 축소는 세로가 Truncate 일 때만 동작 (Overflow 면 줄지 않고 칸 밖으로 넘침)
            menuTitle.resizeTextMinSize = 18;
            menuTitle.resizeTextMaxSize = 34;
        }

        Sprite equipArt = Resources.Load<Sprite>("UI/Forge/Forge_Equip");
        if (equipArt != null && artMenu)
        {
            Image dim = UIFactory.MakeImage("EquipDim", transform, new Color(0f, 0f, 0f, 0.55f), false);
            equip = dim.rectTransform;
            UIFactory.Stretch(equip);
            Image panel = UIFactory.MakeImage("EquipPanel", equip, Color.white, false);
            panel.sprite = equipArt;
            panel.raycastTarget = false;
            UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(EquipW, EquipH) * 0.8f);   // 아래 대화 상자와 안 겹치게 조금 위로
            equipContent = UIFactory.MakeRect("Content", panel.transform);
            UIFactory.Stretch(equipContent);
            RectTransform tspot = UIFactory.ArtSpot("TitleSpot", panel.transform, EquipW, EquipH, 210, 66, 790, 130);
            equipTitle = UIFactory.MakeText("Title", tspot, "", 44, UIFactory.Hex("#FFB050"), TextAnchor.MiddleCenter);
            UIFactory.Stretch(equipTitle.rectTransform);
            equipTitle.resizeTextForBestFit = true;
            equipTitle.verticalOverflow = VerticalWrapMode.Truncate;   // 3-154: 글자 자동 축소는 세로가 Truncate 일 때만 동작 (Overflow 면 줄지 않고 칸 밖으로 넘침)
            equipTitle.resizeTextMinSize = 20;
            equipTitle.resizeTextMaxSize = 44;
            // 브론의 대화 상자는 판 위에 보이게: 판을 대화 상자 바로 앞 순서로
            Transform dialog = transform.Find("DialogBox");
            if (dialog != null) equip.SetSiblingIndex(dialog.GetSiblingIndex());
            equip.gameObject.SetActive(false);
        }

        Sprite leaveArt = Resources.Load<Sprite>("UI/Forge/Forge_Leave");
        if (leaveArt != null)
        {
            Image li = leaveButton.GetComponent<Image>();
            li.sprite = leaveArt;
            li.type = Image.Type.Simple;
            li.color = Color.white;
            ((RectTransform)leaveButton.transform).sizeDelta = new Vector2(274, 94);
            Text lt = leaveButton.GetComponentInChildren<Text>();
            lt.fontStyle = FontStyle.Bold;
            lt.color = UIFactory.Hex("#F4EAD6");
        }
    }

    // 3-148: 그림 속 단추 자리 (x0,y0)~(x1,y1) 에 투명 단추. 마우스를 올리면 은은한 빛, 못 고르면 어둡게 덮음
    RectTransform ArtHotspot(Transform parent, float w, float h, float x0, float y0, float x1, float y1, bool enabled, System.Action onClick)
    {
        RectTransform spot = UIFactory.ArtSpot("Spot", parent, w, h, x0, y0, x1, y1);
        Image glow = UIFactory.MakeImage("Glow", spot, new Color(1f, 0.7f, 0.35f, 0f), false);
        glow.sprite = UIFactory.SoftGlowSprite;
        UIFactory.Stretch(glow.rectTransform, -24, -24, -18, -18);
        glow.raycastTarget = false;
        Image hit = UIFactory.MakeImage("Hit", spot, enabled ? new Color(0, 0, 0, 0) : new Color(0, 0, 0, 0.5f), false);
        UIFactory.Stretch(hit.rectTransform, 6, 6, 6, 6);
        hit.raycastTarget = true;
        if (enabled)
        {
            Button b = hit.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => { SoundManager.Play("Click"); if (onClick != null) onClick(); });   // 3-259
            HoverRelay hover = hit.gameObject.AddComponent<HoverRelay>();
            hover.OnChange = (on) => { glow.color = new Color(1f, 0.7f, 0.35f, on ? 0.35f : 0f); };
        }
        return spot;
    }

    // 그림 속 글자 상자 (제목 한 줄 + 설명)
    void ArtLabel(Transform parent, float w, float h, float x0, float y0, float x1, float y1, string title, string desc, bool enabled, int titleSize, int descSize)
    {
        RectTransform spot = UIFactory.ArtSpot("Label", parent, w, h, x0, y0, x1, y1);
        Text t = UIFactory.MakeText("Text", spot, "<size=" + titleSize + "><b>" + title + "</b></size>" + (desc.Length > 0 ? "\n" + desc : ""), descSize,
            enabled ? UIFactory.Hex("#F4EAD6") : UIFactory.Hex("#9A8E80"), TextAnchor.MiddleCenter);
        UIFactory.Stretch(t.rectTransform);
        t.supportRichText = true;
        t.resizeTextForBestFit = true;   // 3-154: 설명이 길면 작게 (제목은 <size> 로 고정)
        t.resizeTextMinSize = 14;
        t.resizeTextMaxSize = descSize;
        t.verticalOverflow = VerticalWrapMode.Truncate;
    }

    // ---------------- 보이기 ----------------

    public void Show(RunState runState)
    {
        run = runState;
        leaving = false;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        current = FirstNotDone(0);
        if (current < 0) current = 0;
        if (NetGate.Active) current = NetGate.Me;   // 3-244 온라인: 나는 내 것만
        netReady.Clear();
        menu.gameObject.SetActive(false);
        cardLayer.gameObject.SetActive(false);
        HideEquip();
        BuildTabs();
        Say("어서 오게, 수비대 양반들! 놈들이랑 붙었다더니 무기 꼴이 말이 아니군.\n모루를 눌러 보게. 손봐 주는 건 한 사람당 한 번이다!");
    }

    public void Hide()
    {
        StopAllCoroutines();
        gameObject.SetActive(false);
    }

    PlayerCombatant Current { get { return run.Players[current]; } }

    // ---------------- 3-244 온라인 ----------------
    //   강화 = 자기 것만, 명령 "FORGE|자리|W|갈래" / "FORGE|자리|C|덱 번호"
    //   모두 강화를 마치면 바로 다 같이 나감 / [떠나기] = "FLEAVE|자리", 모두 누르면 나감
    private readonly HashSet<int> netReady = new HashSet<int>();

    public void ApplyForge(int slot, string kind, int value)
    {
        if (run == null || !gameObject.activeSelf || slot < 0 || slot >= run.Players.Count) return;
        PlayerCombatant p = run.Players[slot];
        if (!run.CanForge(p)) return;
        string message = null;
        if (kind == "W" && OnUpgrade != null) message = OnUpgrade(p, (WeaponBranch)value);
        else if (kind == "C" && OnCardUpgrade != null) message = OnCardUpgrade(p, value);
        if (message == null) return;
        Hammer(slot == NetGate.Me ? 26 : 8);
        if (slot == NetGate.Me)
        {
            menu.gameObject.SetActive(false);
            cardLayer.gameObject.SetActive(false);
            HideEquip();
            Say("됐네! " + message);
        }
        BuildTabs();
        NetCheckAllDone();
    }

    // 강화할 수 있는 사람이 (나간 사람 빼고) 아무도 없으면 다 같이 나감
    public void NetCheckAllDone()
    {
        if (run == null || !gameObject.activeSelf) return;
        for (int i = 0; i < run.Players.Count; i++)
            if (!NetGate.IsAway(i) && run.CanForge(run.Players[i])) return;
        Leave();
    }

    public void ApplyLeaveReady(int slot)
    {
        if (run == null || !gameObject.activeSelf) return;
        netReady.Add(slot);
        int ready = 0, need = 0;
        for (int i = 0; i < run.Players.Count; i++)
        {
            if (NetGate.IsAway(i)) continue;
            need++;
            if (netReady.Contains(i) || !run.CanForge(run.Players[i])) ready++;
        }
        if (ready >= need) { Leave(); return; }
        Say(netReady.Contains(NetGate.Me) ? "알았네, 동료들이 끝나면 같이 나가세. (" + ready + " / " + need + ")" : "누군가 떠날 채비를 하는군. (" + ready + " / " + need + ")");
    }

    // start 번부터 아직 강화 안 한 첫 사람 (없으면 -1)
    int FirstNotDone(int start)
    {
        for (int k = 0; k < run.Players.Count; k++)
        {
            int i = (start + k) % run.Players.Count;
            if (run.CanForge(run.Players[i])) return i;
        }
        return -1;
    }

    void BuildTabs()
    {
        for (int i = tabLayer.childCount - 1; i >= 0; i--) Destroy(tabLayer.GetChild(i).gameObject);
        int n = run.Players.Count;
        if (tabSprite != null && tabOnSprite != null) { BuildArtTabs(n); return; }   // 3-148
        float w = 300f, gap = 320f;
        for (int i = 0; i < n; i++)
        {
            PlayerCombatant p = run.Players[i];
            bool done = !run.CanForge(p);
            bool on = i == current;
            string label = "P" + (i + 1) + " " + ClassLibrary.ColoredClassName(p.Class) + (done ? "  ✓ 강화 완료" : "");
            Color col = on ? UIFactory.Hex("#8A5A26") : UIFactory.Hex("#3A2618");
            int index = i;
            Button b = UIFactory.MakeButton("Tab" + i, tabLayer, label, col, () => SelectPlayer(index), 24);
            UIFactory.Place((RectTransform)b.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - (n - 1) / 2f) * gap, 0), new Vector2(w, 56));
            if (on) b.gameObject.AddComponent<Outline>().effectColor = UIFactory.Hex("#FFD27A");
        }
    }

    // 3-148: 그림 탭 (고른 사람 = 금색 탭). 강화를 마친 사람은 이름 뒤 ✓ 대신 "완료"
    void BuildArtTabs(int n)
    {
        const float gap = 330f;
        for (int i = 0; i < n; i++)
        {
            PlayerCombatant p = run.Players[i];
            bool done = !run.CanForge(p);
            bool on = i == current;
            int index = i;
            Image img = UIFactory.MakeImage("Tab" + i, tabLayer, Color.white, false);
            img.sprite = on ? tabOnSprite : tabSprite;
            UIFactory.Place(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - (n - 1) / 2f) * gap, 0), on ? new Vector2(315, 99) : new Vector2(313, 86));
            Button b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            ColorBlock cb = b.colors;
            cb.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            b.colors = cb;
            b.onClick.AddListener(() => { SoundManager.Play("Click"); SelectPlayer(index); });   // 3-259
            string label = "P" + (i + 1) + " " + ClassLibrary.ColoredClassName(p.Class) + (done ? "  <color=#9CFF9C><size=17>완료</size></color>" : "");
            Text t = UIFactory.MakeText("Label", img.transform, label, 24, on ? UIFactory.Hex("#FFF2D0") : UIFactory.Hex("#E8DCC8"), TextAnchor.MiddleCenter);
            UIFactory.Stretch(t.rectTransform, 24, 24, 0, 0);
            t.fontStyle = FontStyle.Bold;
            t.supportRichText = true;
        }
    }

    void SelectPlayer(int index)
    {
        current = index;
        menu.gameObject.SetActive(false);
        cardLayer.gameObject.SetActive(false);
        HideEquip();
        BuildTabs();
        PlayerCombatant p = Current;
        if (run.CanForge(p)) Say(p.Name + " " + ClassLibrary.ColoredClassName(p.Class) + ", 자네 차례로군. 모루를 눌러 보게.");
        else Say("자네 건 이미 손봤네. 더 했다간 쇠가 버티질 못해!");
    }

    // ---------------- 모루 → 선택 ----------------

    void OnAnvilClicked()
    {
        if (leaving) return;
        if (!NetGate.CanControl(current)) { Say("그건 " + Current.Name + " 몫일세. 자네 건 자네 탭에서 하게!"); return; }   // 3-244
        PlayerCombatant p = Current;
        if (!run.CanForge(p))
        {
            Say("자네 건 이미 손봤네. 다음 사람!");
            return;
        }
        Hammer(6);
        ShowMainMenu();
        Say("무엇을 손볼 텐가? 카드든 무기든, 이 브론의 망치로 못 고칠 건 없지!");
    }

    void ShowMainMenu()
    {
        HideEquip();
        menu.gameObject.SetActive(true);
        ClearMenu();
        menuTitle.text = Current.Name + " " + ClassLibrary.ColoredClassName(Current.Class) + " — 무엇을 강화할까?";
        bool canCard = RunState.HasUpgradableCard(Current);
        if (artMenu)
        {
            // 3-148: 그림 속 두 단추 (카드 강화 / 장비 강화) 위에 투명 단추 + 글자
            ArtHotspot(menuButtons, MenuW, MenuH, 46, 98, 351, 205, canCard, () => { cardPage = 0; ShowDeckPicker(); });
            ArtLabel(menuButtons, MenuW, MenuH, 71, 121, 328, 189, "카드 강화", canCard ? "덱에서 1장을 골라 강화 (이름 뒤에 +)" : "강화할 수 있는 카드가 없음", canCard, 34, 22);
            ArtHotspot(menuButtons, MenuW, MenuH, 46, 223, 351, 333, true, ShowWeaponMenu);
            ArtLabel(menuButtons, MenuW, MenuH, 104, 238, 282, 318, "장비 강화", "무기 · 갑옷 · 체력 중 하나\n(각 최대 " + GameBalance.WeaponMaxLevel + "번)", true, 34, 22);
            return;
        }
        MenuButton("카드 강화", canCard ? "덱에서 1장을 골라 강화 (이름 뒤에 +)" : "강화할 수 있는 카드가 없음", canCard, new Vector2(0, 100), () => { cardPage = 0; ShowDeckPicker(); });
        MenuButton("장비 강화", "무기 · 갑옷 · 체력 중 하나\n(각 최대 " + GameBalance.WeaponMaxLevel + "번)", true, new Vector2(0, -60), ShowWeaponMenu);
    }

    void ShowWeaponMenu()
    {
        if (equip != null) { ShowEquipArt(); return; }   // 3-148: 큰 그림 판
        ClearMenu();
        PlayerCombatant p = Current;
        // 3-123: 갈래 이름이 무기 / 갑옷 / 체력 으로 바뀜 (저장 칸은 예전 그대로 Blade / Hilt / Core)
        menuTitle.text = p.Name + "  (무기 " + p.WeaponBlade + " / 갑옷 " + p.WeaponHilt + " / 체력 " + p.WeaponCore + ")";
        Say("무기를 벼릴까, 갑옷을 덧댈까, 아니면 몸을 단련할까?");

        WeaponBranch[] branches = { WeaponBranch.Blade, WeaponBranch.Hilt, WeaponBranch.Core };
        for (int i = 0; i < branches.Length; i++)
        {
            WeaponBranch b = branches[i];
            int level = RunState.WeaponLevel(p, b);
            bool maxed = level >= GameBalance.WeaponMaxLevel;
            string title = RunState.BranchName(b) + "   " + level + " → " + (maxed ? "최대" : (level + 1).ToString()) + " / " + GameBalance.WeaponMaxLevel;
            string desc = maxed ? "더 올릴 수 없음" : RunState.BranchEffect(b, level + 1);
            WeaponBranch branch = b;   // 람다 안에서 쓰려고 복사
            MenuButton(title, desc, !maxed, new Vector2(0, 140 - i * 110), () => DoUpgrade(branch), 100);
        }
        MenuButton("뒤로", "", true, new Vector2(0, -165), ShowMainMenu, 50);
    }

    // 3-148: 장비 강화 판 (그림 속 줄 3개: 무기 / 갑옷 / 체력, 아래 [뒤로])
    void ShowEquipArt()
    {
        PlayerCombatant p = Current;
        menu.gameObject.SetActive(false);
        equip.gameObject.SetActive(true);
        for (int i = equipContent.childCount - 1; i >= 0; i--) Destroy(equipContent.GetChild(i).gameObject);
        equipTitle.text = p.Name + " (무기 " + p.WeaponBlade + " / 갑옷 " + p.WeaponHilt + " / 체력 " + p.WeaponCore + ")";
        Say("무기를 벼릴까, 갑옷을 덧댈까, 아니면 몸을 단련할까?");

        WeaponBranch[] branches = { WeaponBranch.Blade, WeaponBranch.Hilt, WeaponBranch.Core };
        for (int i = 0; i < branches.Length; i++)
        {
            WeaponBranch b = branches[i];
            int level = RunState.WeaponLevel(p, b);
            bool maxed = level >= GameBalance.WeaponMaxLevel;
            float y = EquipRowY[i];
            WeaponBranch branch = b;   // 람다 안에서 쓰려고 복사
            ArtHotspot(equipContent, EquipW, EquipH, 143, y, 863, y + 125, !maxed, () => DoUpgrade(branch));
            // "무기 강화 0 → 1/3" (숫자는 주황) / 아래 효과
            string title = RunState.BranchName(b) + "  <color=#FF8A3A>" + level + " → " + (maxed ? "최대" : (level + 1) + "/" + GameBalance.WeaponMaxLevel) + "</color>";
            string desc = maxed ? "더 올릴 수 없음" : RunState.BranchEffect(b, level + 1);
            ArtLabel(equipContent, EquipW, EquipH, 330, y + 12, 700, y + 113, title, desc, !maxed, 36, 24);
        }
        ArtHotspot(equipContent, EquipW, EquipH, 360, 632, 656, 712, true, ShowMainMenu);
        ArtLabel(equipContent, EquipW, EquipH, 360, 632, 656, 712, "뒤로", "", true, 34, 24);
    }

    void HideEquip()
    {
        if (equip != null) equip.gameObject.SetActive(false);
    }

    // ---------------- 카드 강화 ----------------
    // 1) 덱에서 강화할 수 있는 카드만 보여 줌 (한 쪽에 16장, 넘치면 ◀ ▶)
    void ShowDeckPicker()
    {
        menu.gameObject.SetActive(false);
        ClearCardLayer();
        cardLayer.gameObject.SetActive(true);
        cardLayer.SetAsLastSibling();
        PlayerCombatant p = Current;

        List<int> indexes = new List<int>();
        for (int i = 0; i < p.Deck.Count; i++) if (p.Deck[i].CanUpgrade) indexes.Add(i);
        int pages = Mathf.Max(1, Mathf.CeilToInt(indexes.Count / (float)CardsPerPage));
        cardPage = Mathf.Clamp(cardPage, 0, pages - 1);

        LayerText(p.Name + " " + ClassLibrary.ColoredClassName(p.Class) + " — 강화할 카드를 고르세요", 34, UIFactory.Hex("#FFD27A"), new Vector2(0, -40));
        const float scale = 0.6f;
        float w = (CardView.Width + 30) * scale, h = (CardView.Height + 40) * scale;
        int start = cardPage * CardsPerPage;
        for (int k = 0; k < CardsPerPage && start + k < indexes.Count; k++)
        {
            int deckIndex = indexes[start + k];
            int col = k % 8, row = k / 8;
            Vector2 pos = new Vector2((col - 3.5f) * w, 150 - row * h);
            CardView.CreateDisplay(cardLayer, p.Deck[deckIndex], p.ManaName, pos, scale, () => ShowCompare(deckIndex));
        }
        if (pages > 1)
        {
            LayerButton("◀", new Vector2(-180, -330), new Vector2(90, 60), () => { cardPage--; ShowDeckPicker(); }, cardPage > 0);
            LayerText((cardPage + 1) + " / " + pages, 26, Color.white, new Vector2(0, -330), true);
            LayerButton("▶", new Vector2(180, -330), new Vector2(90, 60), () => { cardPage++; ShowDeckPicker(); }, cardPage < pages - 1);
        }
        Button back = LayerButton("뒤로", new Vector2(0, -420), new Vector2(220, 64), () => { cardLayer.gameObject.SetActive(false); ShowMainMenu(); }, true);
        // 3-153: 장비 강화 판의 [뒤로] 와 같은 그림 (UI/Forge/Forge_Back). 없으면 예전 색 버튼
        Sprite backArt = Resources.Load<Sprite>("UI/Forge/Forge_Back");
        if (backArt != null)
        {
            Image bi = back.GetComponent<Image>();
            bi.sprite = backArt;
            bi.type = Image.Type.Simple;
            bi.color = Color.white;
            Outline ol = back.GetComponent<Outline>();
            if (ol != null) Destroy(ol);
            ((RectTransform)back.transform).sizeDelta = new Vector2(290, 78);
            Text bt = back.GetComponentInChildren<Text>();
            bt.fontStyle = FontStyle.Bold;
            bt.color = UIFactory.Hex("#F4EAD6");
            bt.fontSize = 30;
        }
        Say("어느 녀석을 손볼 텐가? 하나만 고르게. 망치는 한 번뿐이야!");
    }

    // 2) 고른 카드: 왼쪽 지금 → 오른쪽 강화 후
    void ShowCompare(int deckIndex)
    {
        PlayerCombatant p = Current;
        CardData before = p.Deck[deckIndex];
        CardData after = before.MakeUpgraded();
        if (after == null) return;
        ClearCardLayer();
        LayerText("[" + before.Name + "] 강화", 36, UIFactory.Hex("#FFD27A"), new Vector2(0, -40));
        CardView.CreateDisplay(cardLayer, before, p.ManaName, new Vector2(-260, 60), 1.1f, null);
        CardView.CreateDisplay(cardLayer, after, p.ManaName, new Vector2(260, 60), 1.1f, null);
        LayerText("▶", 80, UIFactory.Hex("#FFB040"), new Vector2(0, 60), true);
        LayerText("지금", 26, Color.gray, new Vector2(-260, -160), true);
        LayerText("강화 후", 26, CardView.UpgradedNameColor, new Vector2(260, -160), true);
        LayerButton("강화하기", new Vector2(130, -330), new Vector2(240, 70), () => DoCardUpgrade(deckIndex), true);
        LayerButton("다른 카드", new Vector2(-130, -330), new Vector2(240, 70), ShowDeckPicker, true);
        Say("호오, " + before.Name + (HasBatchim(before.Name) ? "이라" : "라") + ". 날을 세우면 훨씬 쓸 만해질 걸세.");
    }

    void DoCardUpgrade(int deckIndex)
    {
        if (OnCardUpgrade == null) return;
        if (NetGate.Intercept("FORGE|" + current + "|C|" + deckIndex)) { cardLayer.gameObject.SetActive(false); return; }   // 3-244 온라인
        string message = OnCardUpgrade(Current, deckIndex);
        cardLayer.gameObject.SetActive(false);
        FinishForge(message);
    }

    void ClearCardLayer()
    {
        for (int i = cardLayer.childCount - 1; i >= 0; i--) Destroy(cardLayer.GetChild(i).gameObject);
    }

    // center = true 면 화면 가운데 기준, 아니면 위쪽 가운데 기준
    void LayerText(string text, int size, Color color, Vector2 pos, bool center = false)
    {
        Text t = UIFactory.MakeText("Text", cardLayer, text, size, color, TextAnchor.MiddleCenter);
        Vector2 anchor = center ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 1f);
        UIFactory.Place(t.rectTransform, anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(1200, size + 30));
        t.raycastTarget = false;
    }

    Button LayerButton(string label, Vector2 pos, Vector2 size, System.Action onClick, bool enabled)   // 3-153: 만든 버튼을 돌려줌 (그림 바꾸기용)
    {
        Button b = UIFactory.MakeButton(label, cardLayer, label, enabled ? UIFactory.Hex("#5A3A20") : UIFactory.Hex("#3A3430"), () => onClick(), 26);
        b.interactable = enabled;
        UIFactory.Place((RectTransform)b.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
        if (enabled) b.gameObject.AddComponent<Outline>().effectColor = UIFactory.Hex("#C89A50");
        return b;
    }

    void DoUpgrade(WeaponBranch branch)
    {
        if (OnUpgrade == null) return;
        if (NetGate.Intercept("FORGE|" + current + "|W|" + (int)branch)) { menu.gameObject.SetActive(false); HideEquip(); return; }   // 3-244 온라인
        string message = OnUpgrade(Current, branch);
        FinishForge(message);
    }

    // 강화 하나가 끝난 뒤: 다음 사람 차례, 모두 끝났으면 자동으로 나감
    void FinishForge(string message)
    {
        Hammer(26);
        menu.gameObject.SetActive(false);
        HideEquip();
        int next = FirstNotDone(current + 1);
        if (next < 0)
        {
            BuildTabs();
            Say("됐네! " + message + "\n다들 무기가 번쩍번쩍하군. 가서 놈들을 혼쭐내 주게!");
            StartCoroutine(LeaveAfter(2.6f));
        }
        else
        {
            current = next;
            BuildTabs();
            Say("됐네! " + message + "\n다음은 " + Current.Name + " " + ClassLibrary.ColoredClassName(Current.Class) + ", 자네 차례일세.");
        }
    }

    IEnumerator LeaveAfter(float seconds)
    {
        leaving = true;
        yield return new WaitForSeconds(seconds);
        Leave();
    }

    void Leave()
    {
        if (!gameObject.activeSelf) return;   // 이미 나갔으면 무시 (두 번 눌러도 한 번만)
        if (OnLeave != null) OnLeave();
    }

    // ---------------- 작은 도우미 ----------------

    void ClearMenu()
    {
        for (int i = menuButtons.childCount - 1; i >= 0; i--) Destroy(menuButtons.GetChild(i).gameObject);
    }

    void MenuButton(string title, string desc, bool enabled, Vector2 pos, System.Action onClick, float height = 140f)
    {
        Color col = enabled ? UIFactory.Hex("#5A3A20") : UIFactory.Hex("#3A3430");
        Button b = UIFactory.MakeButton("Option", menuButtons, "", col, () => { if (onClick != null) onClick(); }, 24);
        b.interactable = enabled;
        UIFactory.Place((RectTransform)b.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(480, height));
        Text label = b.GetComponentInChildren<Text>();
        label.text = desc.Length > 0 ? "<b>" + title + "</b>\n<size=19>" + desc + "</size>" : "<b>" + title + "</b>";
        label.color = enabled ? UIFactory.Hex("#F4E6C8") : UIFactory.Hex("#9A8E80");
        UIFactory.Stretch(label.rectTransform, 12, 12, 6, 6);
        if (enabled) b.gameObject.AddComponent<Outline>().effectColor = UIFactory.Hex("#C89A50");
    }

    // 마지막 글자에 받침이 있는지 (낫 → 있음 "낫이로군", 지팡이 → 없음 "지팡이로군")
    // 한글 글자 번호: (글자 - '가') % 28 이 0 이면 받침 없음
    static bool HasBatchim(string word)
    {
        if (string.IsNullOrEmpty(word)) return false;
        char last = word[word.Length - 1];
        if (last < '가' || last > '힣') return false;
        return (last - '가') % 28 != 0;
    }

    // 브론이 말함 (한 자씩 나옴)
    void Say(string line)
    {
        fullLine = line;
        typed = 0f;
    }

    // 망치질: 모루 위에서 불똥이 튐
    void Hammer(int sparks)
    {
        if (sparks >= 8) SoundManager.Play("Forge");   // 3-252: 강화 망치 소리 (메뉴 열 때 작은 불똥은 소리 없음)
        Vector3 p = ((RectTransform)transform).InverseTransformPoint(anvil.position);
        Vector2 top = new Vector2(p.x, p.y + 200f);
        Vfx.Sparks(transform, top, UIFactory.Hex("#FFB040"), sparks);
        Vfx.Sparks(transform, top, UIFactory.Hex("#FFF0B0"), sparks / 2);
    }

    void Update()
    {
        // 대사: 1초에 40자
        if (typed < fullLine.Length)
        {
            typed = Mathf.Min(fullLine.Length, typed + Time.deltaTime * 40f);
            dialogText.text = fullLine.Substring(0, (int)typed);
        }
        else if (dialogText.text.Length != fullLine.Length)
        {
            dialogText.text = fullLine;
        }

        // 드워프 숨쉬기 (발은 고정)
        float s = Mathf.Sin(Time.time * 2f);
        dwarf.localScale = new Vector3(1f - 0.01f * s, 1f + 0.02f * s, 1f);

        // 모루: 마우스를 올리면 빛이 강해지고 살짝 커짐. 힌트 글자는 둥실둥실
        bool canClick = run != null && !leaving && run.CanForge(Current);
        float glowTarget = anvilHover && canClick ? 0.75f : (canClick ? 0.3f + 0.1f * Mathf.Sin(Time.time * 3f) : 0.1f);
        Color g = anvilGlow.color;
        g.a = Mathf.MoveTowards(g.a, glowTarget, Time.deltaTime * 2f);
        anvilGlow.color = g;
        float sc = anvilHover && canClick ? 1.04f : 1f;
        anvil.localScale = Vector3.Lerp(anvil.localScale, new Vector3(sc, sc, 1f), Time.deltaTime * 12f);
        anvilHint.enabled = canClick && !menu.gameObject.activeSelf && (equip == null || !equip.gameObject.activeSelf);
        anvilHint.rectTransform.anchoredPosition = new Vector2(0, 20 + Mathf.Sin(Time.time * 3f) * 6f);
    }
}
