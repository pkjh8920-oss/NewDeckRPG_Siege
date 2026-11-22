// ============================================================
// TitlePanel.cs
// 게임을 켜면 처음 나오는 타이틀 화면
//
//   싱글플레이 → 바로 런 시작 (Inspector 의 playerClass 직업으로 1명)
//   멀티플레이 → 방 화면: 방 코드, 자리 4개 (플레이어 추가·빼기, 직업 고르기) → 게임 시작
//                ※ 지금은 한 PC에서 같이 하는 방식. 온라인 연결(네트워크)은 아직 없음
//   설정       → 소리 크기 (전체 / 배경음악 / 효과음), 전체 화면
//   종료       → 게임 끄기 (에디터에서는 플레이 멈춤)
//
// 그림: Resources/Backgrounds/Title.png (16:9, 상태창과 후드 쓴 사람. 메뉴 글자는 지운 그림)
//       (11-09: 보랏빛 다크 판타지 그림. 금빛 로고는 그림에 들어 있음, 메뉴 글자·커서·✦ 는 지움)
//       메뉴 글자는 게임이 직접 씀 (어느 해상도에서도 또렷, 마우스를 올리면 금빛 + 보랏빛)
//       → "상태창을 눌러 게임 속으로 들어가는" 느낌
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TitlePanel : MonoBehaviour
{
    // BattleScreen 이 넣어 주는 함수들
    public System.Action OnSingle;
    public System.Action<int, string> OnMulti;          // 방 인원 수, 방 코드 → 캐릭터 선택 화면에서 차례로 고름
    public System.Action OnContinue;
    public System.Action OnCompendium;                  // 3-180 [도감]                    // [이어하기] (저장 파일이 있을 때만 보임, 2026-11-15 3-73)
    public PlayerClass DefaultClass = PlayerClass.BlackScythe;

    // 그림 크기와 그림 속 메뉴 글자 위치 (그림 픽셀 기준, 위에서부터 y 가운데)
    //   그림을 바꾸면 이 숫자만 새 그림에 맞춰 고치면 됨
    const float ImageW = 1024f, ImageH = 572f;
    // 3-180: [도감] 줄을 넣어서 4줄 → 5줄 (간격 약 38 → 32, 상태창 안쪽 264~426 안에 다 들어감)
    static readonly float[] MenuCenterY = { 280f, 312f, 344f, 376f, 408f };
    const float MenuLeft = 430f, MenuRight = 600f, MenuHalfHeight = 15f;

    // 멀티는 같은 직업을 두 명이 못 고름 → 최대 인원 = 고를 수 있는 직업 수 (2026-11-15: 스나이퍼 추가로 4)
    public const int MaxPlayers = 4;
    public const int MaxSlots = 4;   // 방 화면에 보이는 칸 (4번째는 "직업이 늘어나면 열림")

    private RectTransform menuLayer;
    private RectTransform lobby;
    // 3-150: 저장이 있을 때 새 게임 확인 창 ("이어하던 판이 지워집니다")
    private RectTransform newGameDialog;
    private Text newGameText;
    private System.Action newGameGo;          // [새 게임] 을 누르면 이어서 할 일 (싱글 → 캐릭터 선택 / 멀티 → 방)
    private RectTransform slotLayer;
    private Text roomCodeText;
    private Button startButton;
    private RectTransform settings;
    private List<Image> menuGlows = new List<Image>();
    private List<RectTransform> menuMarkers = new List<RectTransform>();
    private List<Text> menuLabels = new List<Text>();
    private int hoveredMenu = -1;
    private Button continueButton;           // [이어하기] 버튼 (저장이 있을 때만 보임)
    private Text continueLabel;
    private bool continueHasArt;             // 버튼 그림을 쓰는지 (그림이면 "이어하기" 는 그림에 있고 아래 줄만 글씨)

    private int lobbyPlayers = 2;            // 방 인원 (방장 포함)
    private string roomCode = "";
    private RectTransform lobbyHome;         // 방 만들기 / 코드로 참가
    private RectTransform lobbyRoom;         // 만든 방 (인원)
    private InputField codeInput;
    private Text joinMessage;

    public static TitlePanel Create(Transform parent)
    {
        RectTransform rt = UIFactory.MakeRect("TitlePanel", parent);
        UIFactory.Stretch(rt);
        TitlePanel panel = rt.gameObject.AddComponent<TitlePanel>();
        panel.Build();
        rt.gameObject.SetActive(false);
        return panel;
    }

    void Build()
    {
        GameSettings.Load();

        // 바탕: 검정 → 타이틀 그림 (16:9, 화면 비율이 달라도 빈틈 없이 꽉 채움)
        Image black = UIFactory.MakeImage("Black", transform, Color.black, false);
        UIFactory.Stretch(black.rectTransform);

        Image art = UIFactory.MakeImage("TitleArt", transform, Color.white, false);
        art.sprite = Resources.Load<Sprite>("Backgrounds/Title");
        UIFactory.Stretch(art.rectTransform);
        AspectRatioFitter af = art.gameObject.AddComponent<AspectRatioFitter>();
        af.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        af.aspectRatio = ImageW / ImageH;
        art.raycastTarget = false;
        if (art.sprite == null) art.color = UIFactory.Hex("#14203A");

        // 게임 이름 로고: 지금 그림에는 금빛 로고(칼·날개 장식)가 들어 있어서 따로 안 올림
        //   로고를 따로 쓰고 싶으면 Backgrounds/TitleLogo.png 를 넣으면 그 자리에 올라감
        Sprite logoSprite = Resources.Load<Sprite>("Backgrounds/TitleLogo");
        if (logoSprite != null)
        {
            Image logo = UIFactory.MakeImage("Logo", art.transform, Color.white, false);
            logo.sprite = logoSprite;
            logo.preserveAspect = true;
            logo.raycastTarget = false;
            SetImageBox(logo.rectTransform, 270f, 30f, 754f, 180f);
        }

        // 상태창 머리말: [ 시스템 ] + 얇은 선 (보랏빛)
        Text header = UIFactory.MakeText("Header", art.transform, "[ 시스템 ]", 24, UIFactory.Hex("#C9B8E8"), TextAnchor.MiddleCenter);
        SetImageBox(header.rectTransform, 430f, 236f, 600f, 258f);
        header.resizeTextForBestFit = true;
        header.verticalOverflow = VerticalWrapMode.Truncate;   // 3-154: 글자 자동 축소는 세로가 Truncate 일 때만 동작 (Overflow 면 줄지 않고 칸 밖으로 넘침)
        header.resizeTextMinSize = 12;
        header.resizeTextMaxSize = 30;
        Image line = UIFactory.MakeImage("HeaderLine", art.transform, new Color(0.78f, 0.7f, 0.95f, 0.5f), false);
        SetImageBox(line.rectTransform, 420f, 261f, 610f, 262.5f);
        line.raycastTarget = false;

        // 메뉴 5개 (3-180): 상태창 안에 글자 + 투명 버튼 (그림 크기가 바뀌어도 비율로 따라감)
        menuLayer = art.rectTransform;
        string[] names = { "싱글플레이", "멀티플레이", "도감", "설정", "종료" };   // 3-180: 도감 추가
        for (int i = 0; i < names.Length; i++)
        {
            int index = i;
            RectTransform zone = UIFactory.MakeRect("Menu_" + names[i], art.transform);
            zone.anchorMin = new Vector2(MenuLeft / ImageW, 1f - (MenuCenterY[i] + MenuHalfHeight) / ImageH);
            zone.anchorMax = new Vector2(MenuRight / ImageW, 1f - (MenuCenterY[i] - MenuHalfHeight) / ImageH);
            zone.offsetMin = Vector2.zero;
            zone.offsetMax = Vector2.zero;

            // 마우스를 올리면 뒤에서 보랏빛이 은은하게
            Image glow = UIFactory.MakeImage("Glow", zone, new Color(0.62f, 0.4f, 1f, 0f), false);
            glow.sprite = UIFactory.SoftGlowSprite;
            UIFactory.Stretch(glow.rectTransform, -60, -60, -30, -30);
            glow.raycastTarget = false;
            menuGlows.Add(glow);

            // 메뉴 글자 (칸 크기에 맞춰 글자 크기가 자동으로 맞춰짐)
            Text label = UIFactory.MakeText("Label", zone, names[i], 40, UIFactory.Hex("#E6DCC8"), TextAnchor.MiddleCenter);
            UIFactory.Stretch(label.rectTransform);
            label.resizeTextForBestFit = true;
            label.verticalOverflow = VerticalWrapMode.Truncate;   // 3-154: 글자 자동 축소는 세로가 Truncate 일 때만 동작 (Overflow 면 줄지 않고 칸 밖으로 넘침)
            label.resizeTextMinSize = 16;
            label.resizeTextMaxSize = 48;
            Outline lo = label.gameObject.AddComponent<Outline>();
            lo.effectColor = new Color(0.12f, 0.05f, 0.2f, 0.85f);
            lo.effectDistance = new Vector2(1.5f, -1.5f);
            menuLabels.Add(label);

            // 왼쪽 표시 "▶"
            Text marker = UIFactory.MakeText("Marker", zone, "▶", 26, UIFactory.Hex("#FFE08A"), TextAnchor.MiddleCenter);
            UIFactory.Place(marker.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-24, 0), new Vector2(40, 40));
            marker.gameObject.SetActive(false);
            menuMarkers.Add(marker.rectTransform);

            // 투명 버튼 (알파 0 이어도 클릭은 받음)
            Image hit = UIFactory.MakeImage("Hit", zone, new Color(0, 0, 0, 0), false);
            UIFactory.Stretch(hit.rectTransform);
            Button b = hit.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => OnMenu(index));
            HoverRelay hover = hit.gameObject.AddComponent<HoverRelay>();
            hover.OnChange = (on) => { if (on) hoveredMenu = index; else if (hoveredMenu == index) hoveredMenu = -1; };
        }

        // [이어하기] (2026-11-15, 3-73): 메뉴 4개는 그림 속 글자 자리에 맞춰져 있어서,
        //   5번째 줄을 넣으려면 그림을 바꿔야 함 → 대신 화면 아래 가운데에 따로 버튼을 둠
        //   저장 파일이 있을 때만 보임 (Show 에서 확인)
        // 3-84: 버튼 그림 (Resources/UI/ContinueButton.png = 금테 보라 판 + "이어하기" 글씨)
        //   그림 안의 작은 글씨 줄은 지워 두고, 어디까지 했는지(싱글 · 2스테이지 칸 4/10 …)는 코드가 그 자리에 얹음
        //   그림이 없으면 예전 보라 네모 버튼
        Sprite continueArt = Resources.Load<Sprite>("UI/ContinueButton");
        UnityEngine.Events.UnityAction onContinue = () =>
        {
            if (lobby.gameObject.activeSelf || settings.gameObject.activeSelf) return;
            if (OnContinue != null) OnContinue();
        };
        if (continueArt != null)
        {
            Image contArt = UIFactory.MakeImage("Continue", transform, Color.white, false);
            contArt.sprite = continueArt;
            contArt.preserveAspect = true;
            contArt.raycastTarget = true;
            float h = 150f, w = h * continueArt.rect.width / continueArt.rect.height;   // 그림 비율 그대로 (817×218 → 약 562×150)
            UIFactory.Place(contArt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 50), new Vector2(w, h));
            continueButton = contArt.gameObject.AddComponent<Button>();
            continueButton.targetGraphic = contArt;
            ColorBlock cb = continueButton.colors;   // 마우스를 올리면 살짝 밝게, 누르면 살짝 어둡게
            cb.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            continueButton.colors = cb;
            continueButton.onClick.AddListener(onContinue);
            // 아래 줄 글씨 자리 (그림 높이의 아래쪽 약 15~35% 사이, 그림 기준 비율)
            continueLabel = UIFactory.MakeText("Info", contArt.transform, "", 22, UIFactory.Hex("#F2D27A"), TextAnchor.MiddleCenter);
            // 3-117: 한 줄에 "4인 멀티 · 2스테이지 칸 3/10 (11-16 21:30)" 을 다 넣으니 끝이 잘림
            //   → 칸을 넓히고 두 줄로 (윗줄 = 어디까지 했는지, 아랫줄 = 저장한 때, 작게)
            continueLabel.rectTransform.anchorMin = new Vector2(0.12f, 0.08f);
            continueLabel.rectTransform.anchorMax = new Vector2(0.88f, 0.38f);
            continueLabel.rectTransform.offsetMin = Vector2.zero;
            continueLabel.rectTransform.offsetMax = Vector2.zero;
            continueLabel.fontStyle = FontStyle.Bold;
            continueLabel.resizeTextForBestFit = true;
            continueLabel.verticalOverflow = VerticalWrapMode.Truncate;   // 3-154: 글자 자동 축소는 세로가 Truncate 일 때만 동작 (Overflow 면 줄지 않고 칸 밖으로 넘침)
            continueLabel.resizeTextMinSize = 10;
            continueLabel.resizeTextMaxSize = 22;
            continueLabel.lineSpacing = 0.9f;
            continueLabel.raycastTarget = false;
            Outline lo = continueLabel.gameObject.AddComponent<Outline>();
            lo.effectColor = new Color(0.1f, 0.03f, 0.15f, 0.9f);
            lo.effectDistance = new Vector2(1.5f, -1.5f);
            continueHasArt = true;
        }
        else
        {
            continueButton = UIFactory.MakeButton("Continue", transform, "이어하기", UIFactory.Hex("#5A3A8A"), onContinue, 30);
            UIFactory.Place(continueButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 70), new Vector2(460, 96));
            Outline co = continueButton.gameObject.AddComponent<Outline>();
            co.effectColor = UIFactory.Hex("#E2B866");
            co.effectDistance = new Vector2(2, -2);
            continueLabel = continueButton.GetComponentInChildren<Text>();
        }
        continueButton.gameObject.SetActive(false);

        BuildLobby();
        BuildSettings();
        BuildNewGameDialog();   // 3-150
    }

    // [이어하기] 버튼: 저장이 있으면 보이고, 어디까지 했는지 한 줄로 (예: "싱글 · 2스테이지 칸 4/10  (11/15 21:30)")
    void RefreshContinue()
    {
        RunSave save = SaveManager.LoadSave();
        continueButton.gameObject.SetActive(save != null);
        if (save == null) return;
        if (continueLabel != null)
        {
            continueLabel.supportRichText = true;
            string when = string.IsNullOrEmpty(save.SavedAt) ? "" : "저장 " + save.SavedAt;
            if (continueHasArt)
                continueLabel.text = save.Summary() + (when.Length > 0 ? "\n<size=15><color=#C8B48A>" + when + "</color></size>" : "");
            else
                continueLabel.text = "이어하기\n<size=20>" + save.Summary() + (when.Length > 0 ? "  (" + when + ")" : "") + "</size>";
        }
    }

    // 그림 픽셀 좌표(왼쪽 위 기준) 상자에 맞춰 놓기 → 화면 크기가 바뀌어도 그림과 같이 움직임
    void SetImageBox(RectTransform rt, float left, float top, float right, float bottom)
    {
        rt.anchorMin = new Vector2(left / ImageW, 1f - bottom / ImageH);
        rt.anchorMax = new Vector2(right / ImageW, 1f - top / ImageH);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    public void Show()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        lobby.gameObject.SetActive(false);
        settings.gameObject.SetActive(false);
        if (newGameDialog != null) newGameDialog.gameObject.SetActive(false);   // 3-150
        hoveredMenu = -1;
        RefreshContinue();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    void OnMenu(int index)
    {
        if (lobby.gameObject.activeSelf || settings.gameObject.activeSelf) return;
        if (newGameDialog != null && newGameDialog.gameObject.activeSelf) return;
        SoundManager.Play("Click");   // 3-190 타이틀 메뉴 (그림 위 글자라 버튼이 아니어서 따로)
        switch (index)
        {
            case 0:
                ConfirmNewGame(() => { if (OnSingle != null) OnSingle(); });   // 3-150: 저장이 있으면 먼저 확인
                break;
            case 1:
                ConfirmNewGame(OpenLobby);
                break;
            case 2:
                if (OnCompendium != null) OnCompendium();   // 3-180 도감
                break;
            case 3:
                OpenSettings();
                break;
            case 4:
                QuitGame();
                break;
        }
    }

    // ==================== 3-150: 새 게임 확인 ====================
    // 저장 파일은 하나뿐 (싱글·멀티 같이) → 새 게임을 시작하면 이어하던 판은 지워짐
    //   저장이 없으면 확인 없이 바로 go. 있으면 확인 창 → [새 게임] = 저장 지우고 go / [취소] = 아무것도 안 함 (저장 그대로)
    //   자바로 치면 "정말 삭제할까요?" JOptionPane.showConfirmDialog 를 띄우고 YES 일 때만 진행하는 것
    void ConfirmNewGame(System.Action go)
    {
        RunSave save = SaveManager.LoadSave();
        if (save == null) { go(); return; }
        newGameGo = go;
        string when = string.IsNullOrEmpty(save.SavedAt) ? "" : "  (저장 " + save.SavedAt + ")";
        newGameText.text = "이어하던 판이 있습니다.\n<color=#FFD27A>" + save.Summary() + when + "</color>\n\n"
                         + "새 게임을 시작하면 이 판은 <color=#FF7A6A>지워지고</color> 되돌릴 수 없습니다.\n새 게임을 시작할까요?";
        newGameDialog.gameObject.SetActive(true);
        newGameDialog.SetAsLastSibling();
    }

    void BuildNewGameDialog()
    {
        RectTransform box = MakeDialog("NewGameConfirm", new Vector2(860, 460), "새 게임");
        newGameDialog = (RectTransform)box.parent;
        newGameText = UIFactory.MakeText("Text", box, "", 26, UIFactory.Hex("#E6DCC8"), TextAnchor.MiddleCenter);
        UIFactory.Stretch(newGameText.rectTransform, 50, 50, 100, 130);
        newGameText.supportRichText = true;
        Button yes = UIFactory.MakeButton("Yes", box, "새 게임", UIFactory.Hex("#8A2A2A"), () =>
        {
            SaveManager.Delete();   // 예전 저장 지우기 → [이어하기] 버튼도 사라짐
            RefreshContinue();
            newGameDialog.gameObject.SetActive(false);
            System.Action go = newGameGo;
            newGameGo = null;
            if (go != null) go();
        }, 30);
        UIFactory.Place((RectTransform)yes.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(150, 34), new Vector2(260, 76));
        UIFactory.SkinButton(yes, UIFactory.GothicButton);   // 3-181: 8번 공용 버튼 틀 (그림 없던 버튼)
        yes.gameObject.AddComponent<Outline>().effectColor = UIFactory.Hex("#E2B866");
        Button no = UIFactory.MakeButton("No", box, "취소", UIFactory.Hex("#3A3446"), () =>
        {
            newGameGo = null;
            newGameDialog.gameObject.SetActive(false);   // 저장은 그대로
        }, 30);
        UIFactory.Place((RectTransform)no.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-150, 34), new Vector2(260, 76));
        UIFactory.SkinButton(no, UIFactory.GothicButton);   // 3-181: 8번 공용 버튼 틀 (그림 없던 버튼)
    }

    void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;   // 에디터: 플레이 멈춤
#elif UNITY_WEBGL
        WebQuit.TryQuit();   // 3-204: 웹 = 탭 닫기 시도 (막히면 "게임을 종료했습니다" 화면). Application.Quit() 은 웹에서 먹통이 됨
#else
        Application.Quit();                                // 빌드한 게임: 창 닫기
#endif
    }

    // 가운데 상자 하나 (어두운 막 + 테두리)
    RectTransform MakeDialog(string name, Vector2 size, string title)
    {
        // 게임 테마: 어두운 보라 상자 + 금색 테두리 (캐릭터 선택 STATUS 창과 같은 느낌)
        Image dim = UIFactory.MakeImage(name, transform, new Color(0.02f, 0.01f, 0.04f, 0.8f), false);
        UIFactory.Stretch(dim.rectTransform);
        Image box = UIFactory.MakeImage("Box", dim.transform, new Color(0.07f, 0.055f, 0.11f, 0.97f));
        UIFactory.Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
        Outline ol = box.gameObject.AddComponent<Outline>();
        ol.effectColor = UIFactory.Hex("#B08A4A");
        ol.effectDistance = new Vector2(2, -2);
        // 모서리 꺾쇠 (금색)
        float L = 34f, T = 3f;
        Vector2[] corners = { new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 0), new Vector2(1, 0) };
        foreach (Vector2 c in corners)
        {
            Vector2 sign = new Vector2(c.x == 0 ? 1 : -1, c.y == 0 ? 1 : -1);
            Image h = UIFactory.MakeImage("CornerH", box.transform, UIFactory.Hex("#E2B866"), false);
            UIFactory.Place(h.rectTransform, c, c, new Vector2(8 * sign.x, 8 * sign.y), new Vector2(L, T));
            Image v = UIFactory.MakeImage("CornerV", box.transform, UIFactory.Hex("#E2B866"), false);
            UIFactory.Place(v.rectTransform, c, c, new Vector2(8 * sign.x, 8 * sign.y), new Vector2(T, L));
            h.raycastTarget = v.raycastTarget = false;
        }
        Text t = UIFactory.MakeText("Title", box.transform, title, 44, UIFactory.Hex("#FFD27A"), TextAnchor.MiddleCenter);
        UIFactory.Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -20), new Vector2(size.x - 40, 70));
        dim.gameObject.SetActive(false);
        return box.rectTransform;
    }

    // ==================== 멀티플레이: 방 ====================
    // 1) 방 만들기 / 방 코드로 참가
    // 2) 방: 코드 + 인원 (방장 P1 + 2~3명)  →  [캐릭터 선택]
    // 3) 캐릭터 선택 화면에서 P1 → P2 → P3 차례로 고름 (CharacterSelectPanel.ShowMulti). 같은 직업은 못 고름
    // 3-157: 멀티플레이 그림 (UI/Lobby/*) 이 있으면 그림 화면, 없으면 예전 보라 상자
    //   그림 크기 508×572 → 화면 782×880 (1.54배). 단추·글자 자리는 그림 픽셀 좌표 (UIFactory.ArtSpot)
    const float LobbyW = 508f, LobbyH = 572f, LobbyK = 880f / 572f;
    private bool lobbyArt;
    static readonly float[] LobbyRowY0 = { 176f, 251f, 327f, 402f };   // 방 그림 속 P1~P4 줄 위·아래 (y0, y0+72)
    const float LobbyRowX0 = 36f, LobbyRowX1 = 478f, LobbyRowH = 72f;

    void BuildLobby()
    {
        if (Resources.Load<Sprite>("UI/Lobby/Lobby_Home") != null && Resources.Load<Sprite>("UI/Lobby/Lobby_Room") != null)
        {
            BuildLobbyArt();
            return;
        }
        RectTransform box = MakeDialog("Lobby", new Vector2(980, 700), "멀티플레이");
        lobby = (RectTransform)box.parent;

        // ---- 1) 처음: 방 만들기 / 코드로 참가 ----
        lobbyHome = UIFactory.MakeRect("Home", box);
        UIFactory.Stretch(lobbyHome, 0, 0, 100, 0);

        Button create = UIFactory.MakeButton("Create", lobbyHome, "방 만들기", UIFactory.Hex("#5A3A8A"), CreateRoom, 34);
        UIFactory.Place((RectTransform)create.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -40), new Vector2(560, 100));
        create.gameObject.AddComponent<Outline>().effectColor = UIFactory.Hex("#E2B866");
        Text createSub = UIFactory.MakeText("CreateSub", lobbyHome, "내가 방장(P1)이 되어 방 코드를 만들어요", 22, UIFactory.Hex("#CFC4E6"), TextAnchor.MiddleCenter);
        UIFactory.Place(createSub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -152), new Vector2(800, 34));

        Image divider = UIFactory.MakeImage("Divider", lobbyHome, UIFactory.Hex("#4A3D66"), false);
        UIFactory.Place(divider.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -210), new Vector2(760, 2));
        Text or = UIFactory.MakeText("Or", lobbyHome, "또는 방 코드로 참가", 24, UIFactory.Hex("#E6DCC8"), TextAnchor.MiddleCenter);
        UIFactory.Place(or.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -244), new Vector2(600, 40));

        codeInput = MakeCodeInput(lobbyHome, new Vector2(-90, -320));
        Button join = UIFactory.MakeButton("Join", lobbyHome, "참가", UIFactory.Hex("#3A5A8A"), JoinRoom, 30);
        UIFactory.Place((RectTransform)join.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(210, -320), new Vector2(180, 80));
        joinMessage = UIFactory.MakeText("JoinMsg", lobbyHome, "", 22, UIFactory.Hex("#FFB070"), TextAnchor.MiddleCenter);
        UIFactory.Place(joinMessage.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -400), new Vector2(880, 60));

        Button homeBack = UIFactory.MakeButton("Back", lobbyHome, "뒤로", UIFactory.Hex("#3A3446"), () => lobby.gameObject.SetActive(false), 28);
        UIFactory.Place((RectTransform)homeBack.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(220, 70));

        // ---- 2) 방 ----
        lobbyRoom = UIFactory.MakeRect("Room", box);
        UIFactory.Stretch(lobbyRoom, 0, 0, 90, 0);
        roomCodeText = UIFactory.MakeText("RoomCode", lobbyRoom, "", 34, UIFactory.Hex("#FFD27A"), TextAnchor.MiddleCenter);
        UIFactory.Place(roomCodeText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -10), new Vector2(900, 50));
        Text note = UIFactory.MakeText("Note", lobbyRoom, "한 PC에서 함께 하기 (온라인 연결은 준비 중)\n다음 화면에서 P1부터 차례로 캐릭터를 고릅니다 · 같은 캐릭터는 한 명만", 21, UIFactory.Hex("#E6DCF0"), TextAnchor.MiddleCenter);
        UIFactory.Place(note.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -62), new Vector2(920, 56));

        slotLayer = UIFactory.MakeRect("Slots", lobbyRoom);
        UIFactory.Stretch(slotLayer, 40, 40, 130, 110);

        startButton = UIFactory.MakeButton("Start", lobbyRoom, "캐릭터 선택", UIFactory.Hex("#7846C8"), StartMulti, 30);
        UIFactory.Place((RectTransform)startButton.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(140, 30), new Vector2(280, 70));
        startButton.gameObject.AddComponent<Outline>().effectColor = UIFactory.Hex("#E2B866");
        Button back = UIFactory.MakeButton("Back", lobbyRoom, "방 나가기", UIFactory.Hex("#3A3446"), ShowLobbyHome, 28);
        UIFactory.Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-170, 30), new Vector2(240, 70));
    }

    // ==================== 3-157: 멀티플레이 그림 화면 ====================
    void BuildLobbyArt()
    {
        lobbyArt = true;
        Image dim = UIFactory.MakeImage("Lobby", transform, new Color(0.02f, 0.01f, 0.04f, 0.8f), false);
        UIFactory.Stretch(dim.rectTransform);
        lobby = dim.rectTransform;
        RectTransform box = UIFactory.MakeRect("Box", lobby);
        UIFactory.Place(box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(LobbyW * LobbyK, LobbyH * LobbyK));

        // ---- 1) 방 만들기 / 코드로 참가 ----
        Image homeArt = UIFactory.MakeImage("Home", box, Color.white, false);
        homeArt.sprite = Resources.Load<Sprite>("UI/Lobby/Lobby_Home");
        UIFactory.Stretch(homeArt.rectTransform);
        lobbyHome = homeArt.rectTransform;
        ArtButton(lobbyHome, "방 만들기", 40, UIFactory.Hex("#F4EAD6"), 88, 132, 442, 214, CreateRoom);
        ArtText(lobbyHome, "내가 방장(P1)이 되어 방 코드를 만들어요", 21, UIFactory.Hex("#E6DCC8"), 70, 220, 440, 250);
        ArtText(lobbyHome, "또는 방 코드로 참가", 24, UIFactory.Hex("#FF9A5A"), 120, 286, 390, 320);
        codeInput = MakeArtCodeInput(UIFactory.ArtSpot("CodeSpot", lobbyHome, LobbyW, LobbyH, 112, 350, 312, 412));
        ArtButton(lobbyHome, "참가", 32, UIFactory.Hex("#F4EAD6"), 330, 348, 440, 412, JoinRoom);
        joinMessage = ArtText(lobbyHome, "", 18, UIFactory.Hex("#FFB070"), 50, 424, 460, 478);
        ArtButton(lobbyHome, "뒤로", 28, UIFactory.Hex("#F4EAD6"), 192, 481, 330, 542, () => lobby.gameObject.SetActive(false));

        // ---- 2) 방 ----
        Image roomArt = UIFactory.MakeImage("Room", box, Color.white, false);
        roomArt.sprite = Resources.Load<Sprite>("UI/Lobby/Lobby_Room");
        UIFactory.Stretch(roomArt.rectTransform);
        lobbyRoom = roomArt.rectTransform;
        roomCodeText = ArtText(lobbyRoom, "", 34, UIFactory.Hex("#FFD27A"), 100, 86, 410, 124);
        roomCodeText.fontStyle = FontStyle.Bold;
        ArtText(lobbyRoom, "한 PC에서 함께 하기 (온라인 연결은 준비 중)\n다음 화면에서 P1부터 차례로 캐릭터를 고릅니다 · 같은 캐릭터는 한 명만", 18, UIFactory.Hex("#E6DCF0"), 44, 124, 464, 170);
        slotLayer = UIFactory.MakeRect("Slots", lobbyRoom);
        UIFactory.Stretch(slotLayer);
        ArtButton(lobbyRoom, "방 나가기", 28, UIFactory.Hex("#F4EAD6"), 118, 486, 240, 542, ShowLobbyHome);
        startButton = ArtButton(lobbyRoom, "캐릭터 선택", 28, UIFactory.Hex("#F4EAD6"), 262, 486, 395, 542, StartMulti);

        lobby.gameObject.SetActive(false);
    }

    // 그림 속 글자 (가운데 정렬, 길면 자동 축소)
    Text ArtText(RectTransform parent, string text, int size, Color color, float x0, float y0, float x1, float y1)
    {
        RectTransform spot = UIFactory.ArtSpot("Text", parent, LobbyW, LobbyH, x0, y0, x1, y1);
        Text t = UIFactory.MakeText("T", spot, text, size, color, TextAnchor.MiddleCenter);
        UIFactory.Stretch(t.rectTransform);
        t.supportRichText = true;
        t.resizeTextForBestFit = true; t.resizeTextMinSize = 12; t.resizeTextMaxSize = size;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.raycastTarget = false;
        return t;
    }

    // 그림 속 단추 자리에 투명 단추 + 글자. 마우스를 올리면 은은한 빛
    Button ArtButton(RectTransform parent, string label, int size, Color color, float x0, float y0, float x1, float y1, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform spot = UIFactory.ArtSpot("Btn", parent, LobbyW, LobbyH, x0, y0, x1, y1);
        Image glow = UIFactory.MakeImage("Glow", spot, new Color(1f, 0.82f, 0.55f, 0f), false);
        glow.sprite = UIFactory.SoftGlowSprite;
        UIFactory.Stretch(glow.rectTransform, -24, -24, -18, -18);
        glow.raycastTarget = false;
        Image hit = UIFactory.MakeImage("Hit", spot, new Color(0, 0, 0, 0), false);
        UIFactory.Stretch(hit.rectTransform);
        hit.raycastTarget = true;
        Button b = hit.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.onClick.AddListener(onClick);
        HoverRelay hover = hit.gameObject.AddComponent<HoverRelay>();
        hover.OnChange = (on) => { glow.color = new Color(1f, 0.82f, 0.55f, on ? 0.3f : 0f); };
        if (label.Length > 0)
        {
            Text t = UIFactory.MakeText("Label", spot, label, size, color, TextAnchor.MiddleCenter);
            UIFactory.Stretch(t.rectTransform, 6, 6, 2, 2);
            t.fontStyle = FontStyle.Bold;
            t.resizeTextForBestFit = true; t.resizeTextMinSize = 14; t.resizeTextMaxSize = size;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = false;
        }
        return b;
    }

    // 그림 속 사슬 틀 안의 방 코드 입력 칸 (배경은 그림, 글자만)
    InputField MakeArtCodeInput(RectTransform spot)
    {
        Image bg = UIFactory.MakeImage("CodeInput", spot, new Color(0, 0, 0, 0.01f), false);
        UIFactory.Stretch(bg.rectTransform);
        Text text = UIFactory.MakeText("Text", bg.transform, "", 36, UIFactory.Hex("#FFD27A"), TextAnchor.MiddleCenter);
        UIFactory.Stretch(text.rectTransform, 6, 6, 4, 4);
        text.supportRichText = false;
        Text placeholder = UIFactory.MakeText("Placeholder", bg.transform, "방 코드 5자리", 26, new Color(1f, 1f, 1f, 0.5f), TextAnchor.MiddleCenter);
        UIFactory.Stretch(placeholder.rectTransform, 6, 6, 4, 4);
        InputField input = bg.gameObject.AddComponent<InputField>();
        input.textComponent = text;
        input.placeholder = placeholder;
        input.characterLimit = 5;
        input.contentType = InputField.ContentType.Alphanumeric;
        input.onValueChanged.AddListener(v => { string up = v.ToUpperInvariant(); if (up != v) input.text = up; });
        return input;
    }

    // 방 그림의 P1~P4 줄. 빈 줄은 그림 그대로, 들어온 사람은 줄 그림(P1 금 / 나머지 파랑, P3·P4 는 색을 입힘)을 덮음
    void RebuildSlotsArt()
    {
        for (int i = slotLayer.childCount - 1; i >= 0; i--) Destroy(slotLayer.GetChild(i).gameObject);
        Sprite gold = Resources.Load<Sprite>("UI/Lobby/Lobby_Slot_Gold");
        Sprite blue = Resources.Load<Sprite>("UI/Lobby/Lobby_Slot_Blue");
        Color[] tint = { Color.white, Color.white, new Color(0.7f, 1f, 0.75f, 1f), new Color(1f, 0.75f, 0.9f, 1f) };   // P3 초록빛, P4 분홍빛
        for (int i = 0; i < MaxSlots; i++)
        {
            bool filled = i < lobbyPlayers;
            bool locked = i >= MaxPlayers;
            float y0 = LobbyRowY0[i], y1 = y0 + LobbyRowH;
            if (filled)
            {
                // 줄 그림은 위아래·좌우로 조금 더 큼 (사슬·번개 장식)
                RectTransform rowSpot = UIFactory.ArtSpot("Row" + i, slotLayer, LobbyW, LobbyH, LobbyRowX0 - 6, y0 - 4, LobbyRowX1 + 6, y1 + 4);
                Image row = UIFactory.MakeImage("Art", rowSpot, tint[i], false);
                row.sprite = i == 0 ? gold : blue;
                UIFactory.Stretch(row.rectTransform);
                row.raycastTarget = false;
            }
            // 배지 글자 P1
            Text badge = ArtTextIn(slotLayer, "P" + (i + 1), 30, filled ? Color.Lerp(PlayerColor(i), Color.white, 0.3f) : new Color(1f, 1f, 1f, 0.45f), 52, y0 + 14, 98, y1 - 14);
            badge.font = UIFactory.TitleFont;
            badge.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.85f);
            string label = filled ? (i == 0 ? "방장" : "참가함") : (locked ? "직업이 늘어나면 열림" : "빈 자리");
            Text who = ArtTextIn(slotLayer, label, 28, filled ? Color.Lerp(PlayerColor(i), Color.white, 0.5f) : new Color(1f, 1f, 1f, 0.5f), 106, y0 + 14, 300, y1 - 14);
            who.alignment = TextAnchor.MiddleLeft;
            who.fontStyle = FontStyle.Bold;
            if (filled)
            {
                bool canRemove = i > 0 && i == lobbyPlayers - 1;
                Text state = ArtTextIn(slotLayer, "캐릭터는 다음 화면에서", 19, UIFactory.Hex("#D8D0E4"), 250, y0 + 20, canRemove ? 418 : 468, y1 - 20);
                state.alignment = TextAnchor.MiddleRight;
                if (canRemove) ArtSprite(slotLayer, "UI/Lobby/Lobby_RemoveBtn", 424, y0 + 14, 468, y1 - 14, RemovePlayer);
            }
            else if (!locked && i == lobbyPlayers)
            {
                ArtSprite(slotLayer, "UI/Lobby/Lobby_AddBtn", 325, y0 + 7, 470, y1 - 7, AddPlayer);
                Text add = ArtTextIn(slotLayer, "+ 플레이어 추가", 22, UIFactory.Hex("#F4EAD6"), 335, y0 + 14, 460, y1 - 14);
                add.fontStyle = FontStyle.Bold;
            }
        }
    }

    Text ArtTextIn(RectTransform parent, string text, int size, Color color, float x0, float y0, float x1, float y1)
    {
        return ArtText(parent, text, size, color, x0, y0, x1, y1);
    }

    // 그림 단추 (추가 / ×). 마우스를 올리면 밝게
    void ArtSprite(RectTransform parent, string path, float x0, float y0, float x1, float y1, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform spot = UIFactory.ArtSpot("Btn", parent, LobbyW, LobbyH, x0, y0, x1, y1);
        Image img = UIFactory.MakeImage("Art", spot, Color.white, false);
        img.sprite = Resources.Load<Sprite>(path);
        img.preserveAspect = true;
        UIFactory.Stretch(img.rectTransform);
        Button b = img.gameObject.AddComponent<Button>();
        ColorBlock cb = b.colors;
        cb.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
        b.colors = cb;
        b.onClick.AddListener(onClick);
    }

    // 방 코드 입력 칸 (영문·숫자 5글자, 자동 대문자)
    InputField MakeCodeInput(Transform parent, Vector2 pos)
    {
        Image bg = UIFactory.MakeImage("CodeInput", parent, UIFactory.Hex("#1A1428"));
        UIFactory.Place(bg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), pos, new Vector2(360, 80));
        bg.gameObject.AddComponent<Outline>().effectColor = UIFactory.Hex("#8A6AC8");
        Text text = UIFactory.MakeText("Text", bg.transform, "", 36, UIFactory.Hex("#FFD27A"), TextAnchor.MiddleCenter);
        UIFactory.Stretch(text.rectTransform, 10, 10, 6, 6);
        text.supportRichText = false;
        Text placeholder = UIFactory.MakeText("Placeholder", bg.transform, "방 코드 5자리", 26, new Color(1f, 1f, 1f, 0.45f), TextAnchor.MiddleCenter);
        UIFactory.Stretch(placeholder.rectTransform, 10, 10, 6, 6);
        InputField input = bg.gameObject.AddComponent<InputField>();
        input.textComponent = text;
        input.placeholder = placeholder;
        input.characterLimit = 5;
        input.contentType = InputField.ContentType.Alphanumeric;
        input.onValueChanged.AddListener(v => { string up = v.ToUpperInvariant(); if (up != v) input.text = up; });
        return input;
    }

    void OpenLobby()
    {
        lobby.gameObject.SetActive(true);
        lobby.SetAsLastSibling();
        ShowLobbyHome();
    }

    // 캐릭터 선택에서 [뒤로] 로 돌아왔을 때: 방 화면 그대로 다시 보여 줌
    public void ReopenRoom()
    {
        lobby.gameObject.SetActive(true);
        lobby.SetAsLastSibling();
        lobbyHome.gameObject.SetActive(false);
        lobbyRoom.gameObject.SetActive(true);
        RebuildSlots();
    }

    void ShowLobbyHome()
    {
        lobbyHome.gameObject.SetActive(true);
        lobbyRoom.gameObject.SetActive(false);
        joinMessage.text = "";
        codeInput.text = "";
    }

    void CreateRoom()
    {
        roomCode = MakeRoomCode();
        lobbyPlayers = 2;   // 방장 + 1명으로 시작 (같은 PC에서 함께)
        roomCodeText.text = "방 코드   " + roomCode;
        lobbyHome.gameObject.SetActive(false);
        lobbyRoom.gameObject.SetActive(true);
        RebuildSlots();
    }

    // 코드로 참가: 온라인이 아직 없어서 안내만 (온라인이 붙으면 여기서 서버에 접속)
    void JoinRoom()
    {
        string code = codeInput.text.Trim();
        if (code.Length != 5) { joinMessage.text = "방 코드 5자리를 입력하세요"; return; }
        joinMessage.text = "[" + code + "] 방을 찾는 기능은 온라인 연결이 붙으면 열려요.\n지금은 [방 만들기]로 같은 PC에서 함께 할 수 있어요.";
    }

    // 헷갈리는 글자(0, O, 1, I)를 뺀 5글자 코드
    string MakeRoomCode()
    {
        const string letters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        string code = "";
        for (int i = 0; i < 5; i++) code += letters[Random.Range(0, letters.Length)];
        return code;
    }

    // 방 사람 색 (캐릭터 선택 화면의 P1·P2 표시와 같은 색)
    public static Color PlayerColor(int index)
    {
        switch (index)
        {
            case 0: return UIFactory.Hex("#FFD27A");   // P1 금색
            case 1: return UIFactory.Hex("#6AC8FF");   // P2 하늘색
            case 2: return UIFactory.Hex("#7CE38B");   // P3 초록
            default: return UIFactory.Hex("#FF8AC8");  // P4 분홍
        }
    }

    void RebuildSlots()
    {
        if (lobbyArt) { RebuildSlotsArt(); return; }   // 3-157
        for (int i = slotLayer.childCount - 1; i >= 0; i--) Destroy(slotLayer.GetChild(i).gameObject);

        for (int i = 0; i < MaxSlots; i++)
        {
            bool filled = i < lobbyPlayers;
            bool locked = i >= MaxPlayers;
            float y = -i * 96f;
            Color pc = PlayerColor(i);

            Image slot = UIFactory.MakeImage("Slot" + i, slotLayer, filled ? UIFactory.Hex("#1E1630") : UIFactory.Hex("#110D1A"));
            UIFactory.Place(slot.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(880, 82));
            if (filled) slot.gameObject.AddComponent<Outline>().effectColor = new Color(pc.r, pc.g, pc.b, 0.6f);

            // 왼쪽 P 배지 (사람 색)
            Image badge = UIFactory.MakeImage("Badge", slot.transform, filled ? pc : UIFactory.Hex("#2A2438"));
            UIFactory.Place(badge.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18, 0), new Vector2(76, 56));
            Text bt = UIFactory.MakeText("P", badge.transform, "P" + (i + 1), 28, filled ? UIFactory.Hex("#1A1020") : new Color(1f, 1f, 1f, 0.55f), TextAnchor.MiddleCenter);
            UIFactory.Stretch(bt.rectTransform);
            bt.fontStyle = FontStyle.Bold;

            string label;
            Color labelColor;
            if (filled) { label = i == 0 ? "방장" : "참가함"; labelColor = Color.white; }
            else if (locked) { label = "직업이 늘어나면 열림 (같은 직업은 한 명만)"; labelColor = new Color(1f, 1f, 1f, 0.5f); }
            else { label = "빈 자리"; labelColor = new Color(1f, 1f, 1f, 0.6f); }
            Text who = UIFactory.MakeText("Who", slot.transform, label, 26, labelColor, TextAnchor.MiddleLeft);
            UIFactory.Place(who.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(115, 0), new Vector2(520, 50));

            if (filled)
            {
                Text state = UIFactory.MakeText("State", slot.transform, "캐릭터는 다음 화면에서", 22, UIFactory.Hex("#CFC4E6"), TextAnchor.MiddleRight);
                UIFactory.Place(state.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(i > 0 ? -95 : -24, 0), new Vector2(300, 40));
                if (i > 0 && i == lobbyPlayers - 1)
                {
                    Button kick = UIFactory.MakeButton("Remove", slot.transform, "×", UIFactory.Hex("#5A2A3A"), RemovePlayer, 28);
                    UIFactory.Place((RectTransform)kick.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-18, 0), new Vector2(60, 56));
                }
            }
            else if (!locked && i == lobbyPlayers)
            {
                Button add = UIFactory.MakeButton("Add", slot.transform, "+ 플레이어 추가", UIFactory.Hex("#3A2A5A"), AddPlayer, 24);
                UIFactory.Place((RectTransform)add.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-18, 0), new Vector2(260, 56));
            }
        }

        startButton.interactable = lobbyPlayers >= 2;   // 멀티는 2명부터
    }

    void AddPlayer()
    {
        if (lobbyPlayers >= MaxPlayers) return;
        lobbyPlayers++;
        RebuildSlots();
    }

    void RemovePlayer()
    {
        if (lobbyPlayers <= 2) return;
        lobbyPlayers--;
        RebuildSlots();
    }

    void StartMulti()
    {
        if (lobbyPlayers < 2) return;
        if (OnMulti != null) OnMulti(lobbyPlayers, roomCode);
    }

    // ==================== 설정 ====================
    void BuildSettings()
    {
        if (BuildSettingsArt()) return;   // 3-170: 그림 판이 있으면 그림 판 설정 (아래 예전 코드는 그림이 없을 때만)
        RectTransform box = MakeDialog("Settings", new Vector2(820, 560), "설정");
        settings = (RectTransform)box.parent;

        AddVolumeRow(box, "전체 소리", 0, () => GameSettings.MasterVolume, v => GameSettings.MasterVolume = v);
        AddVolumeRow(box, "배경음악", 1, () => GameSettings.BgmVolume, v => GameSettings.BgmVolume = v);
        AddVolumeRow(box, "효과음", 2, () => GameSettings.SfxVolume, v => GameSettings.SfxVolume = v);

        // 전체 화면 켜기/끄기
        Text label = UIFactory.MakeText("FullLabel", box, "전체 화면", 28, Color.white, TextAnchor.MiddleLeft);
        UIFactory.Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(60, -380), new Vector2(220, 50));
        Button full = UIFactory.MakeButton("Full", box, "", UIFactory.Hex("#24406A"), null, 26);
        UIFactory.Place((RectTransform)full.transform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(300, -380), new Vector2(200, 56));
        Text fullText = full.GetComponentInChildren<Text>();
        fullText.text = GameSettings.Fullscreen ? "켜짐" : "꺼짐";
        full.onClick.AddListener(() =>
        {
            GameSettings.Fullscreen = !GameSettings.Fullscreen;
            fullText.text = GameSettings.Fullscreen ? "켜짐" : "꺼짐";
            GameSettings.Save();
        });

        Text hint = UIFactory.MakeText("Hint", box, "※ 소리 파일은 나중에 들어감. 전체 화면은 빌드한 게임에서만 바뀜", 18, new Color(1f, 1f, 1f, 0.5f), TextAnchor.MiddleCenter);
        UIFactory.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 110), new Vector2(760, 30));

        Button close = UIFactory.MakeButton("Close", box, "닫기", UIFactory.Hex("#3A3F4C"), () => { GameSettings.Save(); settings.gameObject.SetActive(false); }, 28);
        UIFactory.Place((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(220, 64));
        AddDevQuestion(box, new Vector2(-30, 30));   // 3-205: 숨은 개발자 모드
    }

    // ==================== 3-205: 숨은 개발자 모드 ([?] → 비밀번호) ====================
    //   설정 창 오른쪽 아래에 흐린 작은 [?]. 눌러야만 비밀번호 창이 뜸 (따로 설명 없음)
    //   맞으면 DevPanel.TryUnlock → 이번 실행 동안 개발 버튼·F1 사용 가능
    private RectTransform devDialog;
    private InputField devInput;
    private Text devMessage;

    void AddDevQuestion(RectTransform parent, Vector2 fromBottomRight)
    {
        Button q = UIFactory.MakeButton("DevQ", parent, "?", new Color(1f, 1f, 1f, 0.06f), OpenDevDialog, 20);
        UIFactory.Place((RectTransform)q.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), fromBottomRight, new Vector2(34, 34));
        Text t = q.GetComponentInChildren<Text>();
        if (t != null) t.color = new Color(1f, 1f, 1f, 0.35f);   // 일부러 눈에 잘 안 띄게 흐리게
    }

    void OpenDevDialog()
    {
        if (DevPanel.Enabled) { devMessageToast(); return; }   // 이미 켜져 있으면 창 없이 알림만
        if (devDialog == null) BuildDevDialog();
        devInput.text = "";
        devMessage.text = "";
        devDialog.gameObject.SetActive(true);
        devDialog.SetAsLastSibling();
        devInput.Select();
        devInput.ActivateInputField();
    }

    void devMessageToast()
    {
        if (devDialog == null) BuildDevDialog();
        devMessage.text = "이미 켜져 있습니다";
        devInput.text = "";
        devDialog.gameObject.SetActive(true);
        devDialog.SetAsLastSibling();
    }

    void BuildDevDialog()
    {
        RectTransform box = MakeDialog("DevDialog", new Vector2(620, 380), "?");
        devDialog = (RectTransform)box.parent;

        Image bg = UIFactory.MakeImage("Input", box, UIFactory.Hex("#1A1428"));
        UIFactory.Place(bg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -120), new Vector2(400, 70));
        bg.gameObject.AddComponent<Outline>().effectColor = UIFactory.Hex("#8A6AC8");
        Text text = UIFactory.MakeText("Text", bg.transform, "", 34, UIFactory.Hex("#FFD27A"), TextAnchor.MiddleCenter);
        UIFactory.Stretch(text.rectTransform, 10, 10, 6, 6);
        text.supportRichText = false;
        Text placeholder = UIFactory.MakeText("Placeholder", bg.transform, "비밀번호", 24, new Color(1f, 1f, 1f, 0.4f), TextAnchor.MiddleCenter);
        UIFactory.Stretch(placeholder.rectTransform, 10, 10, 6, 6);
        devInput = bg.gameObject.AddComponent<InputField>();
        devInput.textComponent = text;
        devInput.placeholder = placeholder;
        devInput.characterLimit = 20;
        devInput.contentType = InputField.ContentType.Password;   // 입력하면 ●●● 로 보임
        devInput.onEndEdit.AddListener(v =>
        {
#if ENABLE_INPUT_SYSTEM
            bool enter = UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.enterKey.wasPressedThisFrame;
#else
            bool enter = Input.GetKeyDown(KeyCode.Return);
#endif
            if (enter) SubmitDevPassword();   // 엔터로도 확인
        });

        devMessage = UIFactory.MakeText("Msg", box, "", 22, UIFactory.Hex("#FF8A7A"), TextAnchor.MiddleCenter);
        UIFactory.Place(devMessage.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -205), new Vector2(560, 36));

        Button ok = UIFactory.MakeButton("Ok", box, "확인", UIFactory.Hex("#3A4A2C"), SubmitDevPassword, 26);
        UIFactory.Place((RectTransform)ok.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(120, 30), new Vector2(200, 60));
        UIFactory.SkinButton(ok, UIFactory.GothicButton);
        Button cancel = UIFactory.MakeButton("Cancel", box, "취소", UIFactory.Hex("#3A3F4C"), () => devDialog.gameObject.SetActive(false), 26);
        UIFactory.Place((RectTransform)cancel.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-120, 30), new Vector2(200, 60));
        UIFactory.SkinButton(cancel, UIFactory.GothicButton);
        devDialog.gameObject.SetActive(false);
    }

    void SubmitDevPassword()
    {
        if (DevPanel.TryUnlock(devInput.text))
        {
            devMessage.color = UIFactory.Hex("#7AF09A");
            devMessage.text = "개발자 모드 켜짐 (F1)";
            devDialog.gameObject.SetActive(false);
        }
        else
        {
            devMessage.color = UIFactory.Hex("#FF8A7A");
            devMessage.text = "비밀번호가 다릅니다";
            devInput.text = "";
            devInput.ActivateInputField();
        }
    }

    // 소리 한 줄: [이름]  [-]  (막대) 50%  [+]   (10% 단위)
    //   System.Func<float> = 값을 읽는 함수 (자바 Supplier<Float>), System.Action<float> = 값을 넣는 함수 (Consumer<Float>)
    void AddVolumeRow(RectTransform box, string name, int row, System.Func<float> getter, System.Action<float> setter)
    {
        float y = -130 - row * 82;
        Text label = UIFactory.MakeText(name, box, name, 28, Color.white, TextAnchor.MiddleLeft);
        UIFactory.Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(60, y), new Vector2(200, 50));

        Image barBack = UIFactory.MakeImage("BarBack", box, UIFactory.Hex("#0B1220"));
        UIFactory.Place(barBack.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(360, y), new Vector2(280, 26));
        Image barFill = UIFactory.MakeImage("BarFill", barBack.transform, UIFactory.Hex("#5AC8FF"), false);
        barFill.rectTransform.anchorMin = new Vector2(0f, 0f);
        barFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        barFill.rectTransform.offsetMin = Vector2.zero;
        barFill.rectTransform.offsetMax = Vector2.zero;
        Text value = UIFactory.MakeText("Value", box, "", 24, UIFactory.Hex("#9FE6FF"), TextAnchor.MiddleLeft);
        UIFactory.Place(value.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(720, y), new Vector2(90, 40));

        System.Action refresh = () =>
        {
            float v = getter();
            barFill.rectTransform.anchorMax = new Vector2(v, 1f);
            value.text = Mathf.RoundToInt(v * 100f) + "%";
        };
        refresh();

        Button minus = UIFactory.MakeButton("Minus", box, "-", UIFactory.Hex("#24406A"), () =>
        {
            setter(Mathf.Clamp01(Mathf.Round((getter() - 0.1f) * 10f) / 10f));
            GameSettings.Save();
            refresh();
        }, 28);
        UIFactory.Place((RectTransform)minus.transform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(290, y), new Vector2(56, 50));
        Button plus = UIFactory.MakeButton("Plus", box, "+", UIFactory.Hex("#24406A"), () =>
        {
            setter(Mathf.Clamp01(Mathf.Round((getter() + 0.1f) * 10f) / 10f));
            GameSettings.Save();
            refresh();
        }, 28);
        UIFactory.Place((RectTransform)plus.transform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(654, y), new Vector2(56, 50));
    }

    // ==================== 3-170: 그림 판 설정 ====================
    // UI/Pause/Settings_Title_Panel (879×596): 제목·줄 이름·[-][+]·빈 막대·안내 글·"닫기" 는 그림에 있음
    // 코드가 얹는 것: 투명 단추, 파란 채움 막대(Settings_BarFill), 60% 숫자, 켜짐/꺼짐 단추 그림+글자
    static readonly float[] TitleSetRowY = { 112f, 198f, 285f };   // 소리 3줄 [-] 단추 위쪽 y (높이 59)
    private System.Action refreshSettings;
    bool BuildSettingsArt()
    {
        Sprite panel = Resources.Load<Sprite>("UI/Pause/Settings_Title_Panel");
        if (panel == null) return false;
        // 어두운 막 (MakeDialog 와 같은 역할) → 그 위에 그림 판
        Image dim = UIFactory.MakeImage("Settings", transform, new Color(0.02f, 0.01f, 0.04f, 0.8f), false);
        UIFactory.Stretch(dim.rectTransform);
        settings = dim.rectTransform;
        SettingsArt.Art art = SettingsArt.MakePanel("Panel", dim.transform, panel, 760f);   // 879×596 → 높이 760 (가로 약 1120)
        Sprite fill = Resources.Load<Sprite>("UI/Pause/Settings_BarFill");

        System.Action r1 = TitleVolume(art, "Master", 0, fill, () => GameSettings.MasterVolume, v => GameSettings.MasterVolume = v);
        System.Action r2 = TitleVolume(art, "Bgm", 1, fill, () => GameSettings.BgmVolume, v => GameSettings.BgmVolume = v);
        System.Action r3 = TitleVolume(art, "Sfx", 2, fill, () => GameSettings.SfxVolume, v => GameSettings.SfxVolume = v);
        System.Action r4 = SettingsArt.Toggle(art, "Full", new SettingsArt.Box(309, 370, 549, 438),
            Resources.Load<Sprite>("UI/Pause/Settings_Title_FullOn"), Resources.Load<Sprite>("UI/Pause/Settings_Title_FullOff"), 32,
            () => GameSettings.Fullscreen,
            () => { GameSettings.Fullscreen = !GameSettings.Fullscreen; GameSettings.Save(); });
        SettingsArt.Hotspot(art, "Close", new SettingsArt.Box(321, 490, 556, 563), () => { GameSettings.Save(); settings.gameObject.SetActive(false); });

        refreshSettings = () => { r1(); r2(); r3(); r4(); };
        AddDevQuestion(art.Rt, new Vector2(-46, 40));   // 3-205: 오른쪽 아래 작은 [?] = 숨은 개발자 모드
        dim.gameObject.SetActive(false);
        return true;
    }

    System.Action TitleVolume(SettingsArt.Art art, string name, int row, Sprite fill, System.Func<float> get, System.Action<float> set)
    {
        float y0 = TitleSetRowY[row], y1 = y0 + 59f;
        return SettingsArt.VolumeRow(art, name,
            new SettingsArt.Box(310, y0, 370, y1),                 // [-]
            new SettingsArt.Box(696, y0, 756, y1),                 // [+]
            new SettingsArt.Box(758, y0 + 6, 838, y1 - 6),         // 60% (+ 단추 오른쪽)
            new SettingsArt.Box(384, y0 + 17, 673, y0 + 43.5f),    // 막대 안쪽 칸 (파란 채움이 들어가는 곳)
            fill, 30, get, set);
    }

    void OpenSettings()
    {
        if (refreshSettings != null) refreshSettings();   // 3-170: 열 때 지금 값으로
        settings.gameObject.SetActive(true);
        settings.SetAsLastSibling();
    }

    void Update()
    {
        // 메뉴에 마우스를 올리면 금빛 + ▶ (방·설정 창이 떠 있으면 끔)
        bool dialogOpen = lobby.gameObject.activeSelf || settings.gameObject.activeSelf || (newGameDialog != null && newGameDialog.gameObject.activeSelf);
        for (int i = 0; i < menuGlows.Count; i++)
        {
            bool on = !dialogOpen && hoveredMenu == i;
            Color c = menuGlows[i].color;
            c.a = Mathf.MoveTowards(c.a, on ? 0.45f : 0f, Time.unscaledDeltaTime * 3f);
            menuGlows[i].color = c;
            // 글자: 마우스를 올리면 금빛 + 살짝 커짐
            menuLabels[i].color = Color.Lerp(menuLabels[i].color, on ? UIFactory.Hex("#FFD27A") : UIFactory.Hex("#E6DCC8"), Time.unscaledDeltaTime * 12f);
            float sc = on ? 1.08f : 1f;
            menuLabels[i].rectTransform.localScale = Vector3.Lerp(menuLabels[i].rectTransform.localScale, new Vector3(sc, sc, 1f), Time.unscaledDeltaTime * 12f);
            menuMarkers[i].gameObject.SetActive(on);
            if (on) menuMarkers[i].anchoredPosition = new Vector2(-24 + Mathf.Sin(Time.unscaledTime * 6f) * 4f, 0);
        }

        // Esc: 열린 창 닫기
        bool esc;
#if ENABLE_INPUT_SYSTEM
        esc = UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        esc = Input.GetKeyDown(KeyCode.Escape);
#endif
        if (esc)
        {
            if (devDialog != null && devDialog.gameObject.activeSelf) devDialog.gameObject.SetActive(false);   // 3-205
            else if (settings.gameObject.activeSelf) { GameSettings.Save(); settings.gameObject.SetActive(false); }
            else if (lobby.gameObject.activeSelf) lobby.gameObject.SetActive(false);
            else if (newGameDialog != null && newGameDialog.gameObject.activeSelf) { newGameGo = null; newGameDialog.gameObject.SetActive(false); }   // 3-150: Esc = 취소
        }
    }
}
