// ============================================================
// PauseMenu.cs
// 게임 중 일시정지 메뉴 (유저용). Esc 또는 위쪽 [메뉴] 버튼
//
//   계속하기 / 저장 (3-141) / 설정(소리·전체 화면) / 타이틀로 / 게임 종료
//   3-141: 자동 저장이 없어서 [저장] 을 눌러야 타이틀의 [이어하기] 가 생김 (칸 고르기·지도 화면에서만 됨)
//   타이틀로·게임 종료는 한 번 더 눌러야 실행 (실수로 런을 날리지 않게)
//   열려 있는 동안 Time.timeScale = 0 → 연출·애니메이션이 멈춤 (자바로 치면 게임 루프를 일시정지)
// ============================================================

using UnityEngine;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{
    public System.Action OnTitle;   // 타이틀로 (BattleScreen.ShowTitle)
    public System.Func<string> OnSave;   // 3-141: [저장] 누르면 부름. 결과 글자를 돌려줌 (자바의 Supplier<String>)
    private Text saveText;

    private RectTransform main;
    private RectTransform settings;
    private Text titleConfirm;
    private Text quitConfirm;
    private bool titleArmed, quitArmed;   // 한 번 눌렀는지 (한 번 더 누르면 실행)
    private float savedTimeScale = 1f;

    public bool IsOpen { get { return gameObject.activeSelf; } }

    public static PauseMenu Create(Transform parent)
    {
        RectTransform rt = UIFactory.MakeRect("PauseMenu", parent);
        UIFactory.Stretch(rt);
        PauseMenu menu = rt.gameObject.AddComponent<PauseMenu>();
        menu.Build();
        rt.gameObject.SetActive(false);
        return menu;
    }

    // 3-158: 일시정지 그림 (UI/Pause/Pause_Panel, 762×936). 있으면 그림 판 + 그림 속 단추 5개 위에 투명 단추 + 글자
    const float PauseW = 762f, PauseH = 936f;
    static readonly float[] PauseRowY0 = { 195f, 326f, 455f, 584f, 712f };   // 단추 5개 위쪽 y (높이 103)
    private Sprite pauseArt;
    private int buttonIndex;
    private System.Action refreshSettings;   // 3-170: 설정 창을 열 때 숫자·켜짐/꺼짐을 지금 값으로 다시 그림

    void Build()
    {
        pauseArt = Resources.Load<Sprite>("UI/Pause/Pause_Panel");
        Image dim = UIFactory.MakeImage("Dim", transform, new Color(0, 0, 0, 0.75f), false);
        UIFactory.Stretch(dim.rectTransform);

        // ---- 메인 ----
        Image box = UIFactory.MakeImage("Main", transform, new Color(0.08f, 0.07f, 0.11f, 0.97f));
        main = box.rectTransform;
        UIFactory.Place(main, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520, 640));   // 3-141: [저장] 줄 때문에 560 → 640
        box.gameObject.AddComponent<Outline>().effectColor = UIFactory.Hex("#C8A05A");
        Text title = UIFactory.MakeText("Title", main, "일시정지", 48, UIFactory.Hex("#FFD27A"), TextAnchor.MiddleCenter);
        UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -24), new Vector2(480, 70));
        title.font = UIFactory.TitleFont;
        if (pauseArt != null)
        {
            // 그림 판: 제목 "일시정지" 와 단추 틀은 그림에 있음 → 코드 제목은 숨김
            box.sprite = pauseArt;
            box.type = Image.Type.Simple;
            box.color = Color.white;
            Destroy(box.GetComponent<Outline>());
            main.sizeDelta = new Vector2(PauseW, PauseH) * (900f / PauseH);
            title.gameObject.SetActive(false);
        }

        MainButton("계속하기", -130, UIFactory.Hex("#3A4A2C"), Close);
        saveText = MainButton("저장", -220, UIFactory.Hex("#2C4A5A"), PressSave);   // 3-141
        MainButton("설정", -310, UIFactory.Hex("#3A3F4C"), () => { if (refreshSettings != null) refreshSettings(); settings.gameObject.SetActive(true); main.gameObject.SetActive(false); });
        titleConfirm = MainButton("타이틀로", -400, UIFactory.Hex("#4A3A5A"), PressTitle);
        quitConfirm = MainButton("게임 종료", -490, UIFactory.Hex("#5A2C2C"), PressQuit);
#if UNITY_WEBGL && !UNITY_EDITOR
        // 3-202: 웹에서는 "게임 종료"를 누르면 게임이 멈추기만 해서 버튼을 숨김 (브라우저 탭을 닫으면 종료)
        { Button qb = quitConfirm.GetComponentInParent<Button>(); if (qb != null) qb.gameObject.SetActive(false); }
#endif
        Text hint = UIFactory.MakeText("Hint", main, "Esc 로 닫기", 18, new Color(1, 1, 1, 0.5f), TextAnchor.MiddleCenter);
        UIFactory.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 18), new Vector2(300, 30));
        if (pauseArt != null)
        {
            RectTransform hs = UIFactory.ArtSpot("HintSpot", main, PauseW, PauseH, 280, 858, 482, 902);
            hint.transform.SetParent(hs, false);
            UIFactory.Stretch(hint.rectTransform);
            hint.fontSize = 22;
            hint.color = UIFactory.Hex("#F0E2C0");
            hint.fontStyle = FontStyle.Bold;
        }

        // ---- 설정 ----
        if (BuildSettingsArt()) return;   // 3-170: 그림 판이 있으면 그림 판 설정 (아래 예전 코드는 그림이 없을 때만)

        Image sbox = UIFactory.MakeImage("Settings", transform, new Color(0.08f, 0.07f, 0.11f, 0.97f));
        settings = sbox.rectTransform;
        UIFactory.Place(settings, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620, 600));   // 3-130: 튜토리얼 줄 때문에 520 → 600
        sbox.gameObject.AddComponent<Outline>().effectColor = UIFactory.Hex("#C8A05A");
        Text stitle = UIFactory.MakeText("Title", settings, "설정", 42, UIFactory.Hex("#FFD27A"), TextAnchor.MiddleCenter);
        UIFactory.Place(stitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -20), new Vector2(500, 60));
        VolumeRow("전체 소리", -110, () => GameSettings.MasterVolume, v => GameSettings.MasterVolume = v);
        VolumeRow("배경음악", -180, () => GameSettings.BgmVolume, v => GameSettings.BgmVolume = v);
        VolumeRow("효과음", -250, () => GameSettings.SfxVolume, v => GameSettings.SfxVolume = v);
        Text fullLabel = UIFactory.MakeText("FullLabel", settings, "전체 화면", 26, Color.white, TextAnchor.MiddleLeft);
        UIFactory.Place(fullLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-150, -320), new Vector2(220, 50));
        Button full = UIFactory.MakeButton("Full", settings, "", UIFactory.Hex("#3A3F4C"), null, 24);
        UIFactory.Place((RectTransform)full.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(120, -320), new Vector2(160, 50));
        Text fullText = full.GetComponentInChildren<Text>();
        fullText.text = GameSettings.Fullscreen ? "켜짐" : "꺼짐";
        full.onClick.AddListener(() =>
        {
            GameSettings.Fullscreen = !GameSettings.Fullscreen;
            fullText.text = GameSettings.Fullscreen ? "켜짐" : "꺼짐";
            GameSettings.Save();
        });
        // 3-130 튜토리얼: [켜짐/꺼짐] + [처음부터 다시 보기]
        Text tutLabel = UIFactory.MakeText("TutLabel", settings, "튜토리얼", 26, Color.white, TextAnchor.MiddleLeft);
        UIFactory.Place(tutLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-150, -390), new Vector2(220, 50));
        Button tut = UIFactory.MakeButton("Tut", settings, "", UIFactory.Hex("#3A3F4C"), null, 24);
        UIFactory.Place((RectTransform)tut.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(10, -390), new Vector2(120, 50));
        Text tutText = tut.GetComponentInChildren<Text>();
        tutText.text = TutorialPanel.Enabled ? "켜짐" : "꺼짐";
        tut.onClick.AddListener(() =>
        {
            TutorialPanel.Enabled = !TutorialPanel.Enabled;
            tutText.text = TutorialPanel.Enabled ? "켜짐" : "꺼짐";
        });
        Button tutReset = UIFactory.MakeButton("TutReset", settings, "다시 보기", UIFactory.Hex("#3A3F4C"), null, 22);
        UIFactory.Place((RectTransform)tutReset.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(170, -390), new Vector2(170, 50));
        Text resetText = tutReset.GetComponentInChildren<Text>();
        tutReset.onClick.AddListener(() =>
        {
            TutorialPanel.ResetAll();
            TutorialPanel.Enabled = true;
            tutText.text = "켜짐";
            resetText.text = "초기화됨";   // 다음에 그 화면을 보면 안내가 다시 뜸
        });

        Button back = UIFactory.MakeButton("Back", settings, "뒤로", UIFactory.Hex("#3A3F4C"), () => { GameSettings.Save(); settings.gameObject.SetActive(false); main.gameObject.SetActive(true); }, 26);
        UIFactory.Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(200, 60));
        settings.gameObject.SetActive(false);
    }

    // ==================== 3-170: 그림 판 설정 ====================
    // UI/Pause/Settings_Panel (913×882): 제목 "설정", 줄 이름(전체 소리…튜토리얼), [-][+] 단추, "뒤로" 는 그림에 있음
    // 코드가 얹는 것: 투명 단추, 60% 숫자, 켜짐/꺼짐 단추 그림+글자, "다시 보기" 글자
    // 좌표 = 그림 픽셀 (왼쪽 위 0,0)
    static readonly float[] SetRowY = { 172f, 272f, 373f };   // 소리 3줄 [-] 단추 위쪽 y (높이 74)
    bool BuildSettingsArt()
    {
        Sprite panel = Resources.Load<Sprite>("UI/Pause/Settings_Panel");
        if (panel == null) return false;
        SettingsArt.Art art = SettingsArt.MakePanel("Settings", transform, panel, 860f);
        settings = art.Rt;

        System.Action r1 = ArtVolume(art, "Master", 0, () => GameSettings.MasterVolume, v => GameSettings.MasterVolume = v);
        System.Action r2 = ArtVolume(art, "Bgm", 1, () => GameSettings.BgmVolume, v => GameSettings.BgmVolume = v);
        System.Action r3 = ArtVolume(art, "Sfx", 2, () => GameSettings.SfxVolume, v => GameSettings.SfxVolume = v);

        // 전체 화면 (넓은 청록 단추)
        System.Action r4 = SettingsArt.Toggle(art, "Full", new SettingsArt.Box(499, 472, 760, 556),
            Resources.Load<Sprite>("UI/Pause/Settings_FullOn"), Resources.Load<Sprite>("UI/Pause/Settings_FullOff"), 36,
            () => GameSettings.Fullscreen,
            () => { GameSettings.Fullscreen = !GameSettings.Fullscreen; GameSettings.Save(); });

        // 튜토리얼 켜짐/꺼짐 (작은 단추) + [다시 보기]
        System.Action r5 = SettingsArt.Toggle(art, "Tut", new SettingsArt.Box(379, 572, 561, 658),
            Resources.Load<Sprite>("UI/Pause/Settings_TutOn"), Resources.Load<Sprite>("UI/Pause/Settings_TutOff"), 34,
            () => TutorialPanel.Enabled,
            () => { TutorialPanel.Enabled = !TutorialPanel.Enabled; });
        SettingsArt.Box resetBox = new SettingsArt.Box(579, 574, 825, 654);
        Text resetText = SettingsArt.Label(art, "TutResetText", resetBox, "다시 보기", 34, UIFactory.Hex("#F6EAD2"));
        SettingsArt.Hotspot(art, "TutReset", resetBox, () =>
        {
            TutorialPanel.ResetAll();          // 모든 안내를 "아직 안 봄" 으로
            TutorialPanel.Enabled = true;
            r5();                              // 튜토리얼 단추를 "켜짐" 으로 다시 그림
            resetText.text = "초기화됨";       // 다음에 그 화면을 보면 안내가 다시 뜸
        });

        // 뒤로 (글자는 그림에 있음)
        SettingsArt.Hotspot(art, "Back", new SettingsArt.Box(310, 749, 597, 834), () => Back());

        // 열 때마다 지금 값으로 (람다 여러 개를 하나로 묶음 = 자바의 Runnable 안에서 차례로 run())
        refreshSettings = () => { r1(); r2(); r3(); r4(); r5(); resetText.text = "다시 보기"; };
        settings.gameObject.SetActive(false);
        return true;
    }

    System.Action ArtVolume(SettingsArt.Art art, string name, int row, System.Func<float> get, System.Action<float> set)
    {
        float y0 = SetRowY[row], y1 = y0 + 74f;
        return SettingsArt.VolumeRow(art, name,
            new SettingsArt.Box(473, y0, 552, y1),          // [-]
            new SettingsArt.Box(706, y0, 785, y1),          // [+]
            new SettingsArt.Box(560, y0 + 10, 700, y1 - 10),   // 60% (두 단추 사이 어두운 칸)
            null, null, 38, get, set);                      // 이 그림엔 막대가 없음 → 숫자만
    }

    public void Open()
    {
        GameSettings.Load();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        main.gameObject.SetActive(true);
        settings.gameObject.SetActive(false);
        if (refreshSettings != null) refreshSettings();   // 3-170
        ResetConfirm();
        savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
    }

    public void Close()
    {
        if (!gameObject.activeSelf) return;
        GameSettings.Save();
        gameObject.SetActive(false);
        Time.timeScale = savedTimeScale;
    }

    // Esc: 설정 화면이면 메인으로, 메인이면 닫기
    public void Back()
    {
        if (settings.gameObject.activeSelf) { GameSettings.Save(); settings.gameObject.SetActive(false); main.gameObject.SetActive(true); }
        else Close();
    }

    void PressTitle()
    {
        if (!titleArmed)
        {
            ResetConfirm();
            titleArmed = true;
            titleConfirm.text = "저장하지 않은 진행은 사라집니다. 한 번 더 누르기";
            return;
        }
        Close();
        if (OnTitle != null) OnTitle();
    }

    // 3-141: 저장. 결과("저장했습니다 (12:30)" 등)를 버튼 글자로 보여 줌. 메뉴를 다시 열면 "저장" 으로 돌아옴
    void PressSave()
    {
        ResetConfirm();
        saveText.text = OnSave != null ? OnSave() : "저장할 수 없습니다";
    }

    void PressQuit()
    {
        if (!quitArmed)
        {
            ResetConfirm();
            quitArmed = true;
            quitConfirm.text = "저장하지 않은 진행은 사라집니다. 한 번 더 누르기";
            return;
        }
        GameSettings.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;   // 에디터: 플레이 멈춤
#elif UNITY_WEBGL
        // 3-202: 웹에서는 Application.Quit() 이 게임을 멈추기만 함 → 아무것도 안 함
#else
        Application.Quit();                                // 빌드한 게임: 창 닫기
#endif
    }

    void ResetConfirm()
    {
        titleArmed = quitArmed = false;
        titleConfirm.text = "타이틀로";
        quitConfirm.text = "게임 종료";
        if (saveText != null) saveText.text = "저장";
    }

    Text MainButton(string label, float y, Color color, UnityEngine.Events.UnityAction onClick)
    {
        int index = buttonIndex++;
        if (pauseArt != null && index < PauseRowY0.Length) return ArtMainButton(label, index, onClick);   // 3-158
        Button b = UIFactory.MakeButton(label, main, label, color, onClick, 26);
        UIFactory.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(420, 70));
        Text t = b.GetComponentInChildren<Text>();
        t.resizeTextForBestFit = true;
        t.verticalOverflow = VerticalWrapMode.Truncate;   // 3-154: 글자 자동 축소는 세로가 Truncate 일 때만 동작 (Overflow 면 줄지 않고 칸 밖으로 넘침)
        t.resizeTextMinSize = 16;
        t.resizeTextMaxSize = 26;
        return t;
    }

    // 3-158: 그림 속 단추 틀 위에 투명 단추 + 글자 (마우스를 올리면 은은한 빛)
    Text ArtMainButton(string label, int index, UnityEngine.Events.UnityAction onClick)
    {
        float y0 = PauseRowY0[index];
        RectTransform spot = UIFactory.ArtSpot(label, main, PauseW, PauseH, 79, y0, 682, y0 + 103);
        Image glow = UIFactory.MakeImage("Glow", spot, new Color(1f, 0.85f, 0.55f, 0f), false);
        glow.sprite = UIFactory.SoftGlowSprite;
        UIFactory.Stretch(glow.rectTransform, -24, -24, -16, -16);
        glow.raycastTarget = false;
        Image hit = UIFactory.MakeImage("Hit", spot, new Color(0, 0, 0, 0), false);
        UIFactory.Stretch(hit.rectTransform);
        hit.raycastTarget = true;
        Button b = hit.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.onClick.AddListener(onClick);
        HoverRelay hover = hit.gameObject.AddComponent<HoverRelay>();
        hover.OnChange = (on) => { glow.color = new Color(1f, 0.85f, 0.55f, on ? 0.28f : 0f); };
        Text t = UIFactory.MakeText("Label", spot, label, 34, UIFactory.Hex("#F6EAD2"), TextAnchor.MiddleCenter);
        UIFactory.Stretch(t.rectTransform, 150, 150, 8, 8);   // 양옆 그림(나뭇잎·상자·성) 을 안 가리게 가운데만
        t.fontStyle = FontStyle.Bold;
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = 16;
        t.resizeTextMaxSize = 34;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.raycastTarget = false;
        t.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.85f);
        return t;
    }

    // 소리 줄: 이름  [-]  80%  [+]   (0.1 씩)
    void VolumeRow(string label, float y, System.Func<float> get, System.Action<float> set)
    {
        Text name = UIFactory.MakeText(label, settings, label, 26, Color.white, TextAnchor.MiddleLeft);
        UIFactory.Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-150, y), new Vector2(220, 50));
        Text value = UIFactory.MakeText("Value", settings, "", 26, UIFactory.Hex("#FFD27A"), TextAnchor.MiddleCenter);
        UIFactory.Place(value.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(120, y), new Vector2(100, 50));
        System.Action show = () => value.text = Mathf.RoundToInt(get() * 100) + "%";
        show();
        Button minus = UIFactory.MakeButton("Minus", settings, "-", UIFactory.Hex("#3A3F4C"), () => { set(Mathf.Clamp01(Mathf.Round((get() - 0.1f) * 10f) / 10f)); GameSettings.Apply(); show(); }, 30);
        UIFactory.Place((RectTransform)minus.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(40, y), new Vector2(54, 50));
        Button plus = UIFactory.MakeButton("Plus", settings, "+", UIFactory.Hex("#3A3F4C"), () => { set(Mathf.Clamp01(Mathf.Round((get() + 0.1f) * 10f) / 10f)); GameSettings.Apply(); show(); }, 30);
        UIFactory.Place((RectTransform)plus.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(200, y), new Vector2(54, 50));
    }
}
