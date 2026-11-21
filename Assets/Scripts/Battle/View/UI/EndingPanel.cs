// ============================================================
// EndingPanel.cs
// 엔딩 화면 (3-112): 4스테이지 보스(설계자)를 쓰러뜨리면 승리 창 [엔딩 보기] → 이 화면
//   1) 하얀 빛이 번쩍였다가 사라짐
//   2) 배경 그림 (Backgrounds/Ending.png, 없으면 타이틀 그림을 어둡게)
//   3) 제목 → 엔딩 문구 3줄이 차례로 떠오름
//   4) 런 기록 (파티, 이긴 전투·엘리트·보스, 걸린 시간, 몇 번째 클리어)
//   5) [타이틀로] / [새 런]
// 3-148: 새 엔딩 그림 (Backgrounds/Ending.png, 1024×594) 이 있으면
//   제목·문구 3줄은 그림에 그려져 있으므로 글자는 숨기고, 그림 속 런 기록 판 안에 기록 글자, 그림 속 두 버튼 위에 투명 버튼 + 글자
//   (자리는 전부 그림 픽셀 좌표 → UIFactory.ArtSpot). 그림 전체가 보이게 FitInParent (남는 곳은 검은 바탕)
//   문구를 바꾸려면 그림을 다시 만들어야 함 (StoryLines 는 그림이 없을 때만 쓰임)
// 자바와 비교: IEnumerator + yield return = 시간에 걸쳐 나눠 실행하는 함수 (자바라면 Timer + 상태 변수로 나눠 써야 함)
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EndingPanel : MonoBehaviour
{
    // 엔딩 문구 (바꾸고 싶으면 여기 세 줄만)
    static readonly string[] StoryLines =
    {
        "설계자가 무너지자 침공의 그림자가 걷혔다.",
        "불타던 성벽 위로 새벽이 밝아 온다.",
        "성은 지켜졌다. 그리고 우리는 살아남았다.",
    };
    const string ClearCountKey = "ClearCount";   // PlayerPrefs: 지금까지 클리어한 횟수

    private RectTransform root;
    private Image flash;
    private Text title, recordText;
    private Text[] lines = new Text[3];
    private CanvasGroup recordGroup, buttonGroup;
    private System.Action onTitle, onNewRun;

    // 3-148: 새 엔딩 그림
    const float ArtW = 1024f, ArtH = 594f;
    private Image art;                     // 그림 (없으면 null → 예전 화면)
    private Text[] buttonLabels = new Text[2];

    public static EndingPanel Create(Transform parent)
    {
        RectTransform rt = UIFactory.MakeRect("Ending", parent);
        UIFactory.Stretch(rt);
        EndingPanel panel = rt.gameObject.AddComponent<EndingPanel>();
        panel.root = rt;
        panel.Build();
        rt.gameObject.SetActive(false);
        return panel;
    }

    void Build()
    {
        // 검은 바탕 (뒤 화면 클릭 막음)
        Image black = UIFactory.MakeImage("Black", root, Color.black, false);
        UIFactory.Stretch(black.rectTransform);

        // 3-148: 새 엔딩 그림이 있으면 그 그림 위에 그림 속 자리대로
        Sprite artSprite = Resources.Load<Sprite>("Backgrounds/Ending");
        if (artSprite != null && artSprite.rect.width > artSprite.rect.height * 1.6f)   // 새 그림(가로로 긴 1024×594)일 때만
        {
            BuildArt(artSprite);
        }
        else
        {
            // 배경 그림: 화면을 꽉 채우고 넘치는 부분은 잘림 (EnvelopeParent)
            Sprite oldArt = Resources.Load<Sprite>("Backgrounds/Ending");
            bool ownArt = oldArt != null;
            if (oldArt == null) oldArt = Resources.Load<Sprite>("Backgrounds/Title");
            if (oldArt != null)
            {
                Image bg = UIFactory.MakeImage("Art", root, ownArt ? new Color(0.75f, 0.75f, 0.75f) : new Color(0.4f, 0.38f, 0.45f), false);
                bg.sprite = oldArt;
                bg.raycastTarget = false;
                UIFactory.Stretch(bg.rectTransform);
                AspectRatioFitter fit = bg.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = oldArt.rect.width / oldArt.rect.height;
            }
            // 글자가 잘 보이게 아래쪽을 어둡게 (위 투명 → 아래 검정)
            Image shade = UIFactory.MakeImage("Shade", root, Color.white, false);
            shade.sprite = UIFactory.MakeGradient(new Color(0, 0, 0, 0.15f), new Color(0, 0, 0, 0.85f));
            shade.raycastTarget = false;
            UIFactory.Stretch(shade.rectTransform);

            title = UIFactory.MakeText("Title", root, "성을 지켜 냈다!", 84, UIFactory.Hex("#F0C850"), TextAnchor.MiddleCenter);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -170), new Vector2(1400, 110));
            title.fontStyle = FontStyle.Bold;

            for (int i = 0; i < 3; i++)
            {
                lines[i] = UIFactory.MakeText("Line" + i, root, StoryLines[i], 32, UIFactory.Hex("#EDE4D2"), TextAnchor.MiddleCenter);
                UIFactory.Place(lines[i].rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -280 - i * 52), new Vector2(1400, 48));
                lines[i].gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.85f);
            }

            // 런 기록 상자
            Image box = UIFactory.MakeImage("Record", root, new Color(0.06f, 0.05f, 0.08f, 0.82f));
            UIFactory.Place(box.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 170), new Vector2(1100, 300));
            box.gameObject.AddComponent<Outline>().effectColor = UIFactory.Hex("#C9A24A");
            recordGroup = box.gameObject.AddComponent<CanvasGroup>();
            recordText = UIFactory.MakeText("Text", box.transform, "", 24, Color.white, TextAnchor.UpperCenter);
            UIFactory.Stretch(recordText.rectTransform, 30, 30, 22, 16);
            recordText.supportRichText = true;
            recordText.resizeTextForBestFit = true;   // 4인 파티면 줄이 많아서 넘치지 않게
            recordText.verticalOverflow = VerticalWrapMode.Truncate;   // 3-154: 글자 자동 축소는 세로가 Truncate 일 때만 동작 (Overflow 면 줄지 않고 칸 밖으로 넘침)
            recordText.resizeTextMinSize = 14;
            recordText.resizeTextMaxSize = 24;

            // 버튼 두 개
            RectTransform buttons = UIFactory.MakeRect("Buttons", root);
            UIFactory.Place(buttons, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 60), new Vector2(700, 80));
            buttonGroup = buttons.gameObject.AddComponent<CanvasGroup>();
            Button toTitle = UIFactory.MakeButton("ToTitle", buttons, "타이틀로", UIFactory.Hex("#3A3446"), () => Close(onTitle), 30);
            UIFactory.Place((RectTransform)toTitle.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-170, 0), new Vector2(300, 74));
            UIFactory.SkinButton(toTitle, UIFactory.GothicButton);   // 3-181: 8번 공용 버튼 틀 (그림 없던 버튼)
            Button again = UIFactory.MakeButton("NewRun", buttons, "새 런", UIFactory.Hex("#7846C8"), () => Close(onNewRun), 30);
            UIFactory.Place((RectTransform)again.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(170, 0), new Vector2(300, 74));
            UIFactory.SkinButton(again, UIFactory.GothicButton);   // 3-181: 8번 공용 버튼 틀 (그림 없던 버튼)
        }

        // 맨 위: 하얀 빛 (처음에 번쩍)
        flash = UIFactory.MakeImage("Flash", root, Color.white, false);
        UIFactory.Stretch(flash.rectTransform);
        flash.raycastTarget = false;
    }

    // 3-148: 그림 화면 만들기 (예전 제목·문구 Text 는 만들어 두되 숨김 → Play() 가 그대로 돌아도 안 보임)
    void BuildArt(Sprite sprite)
    {
        art = UIFactory.MakeArtBackground("Art", root, sprite);
        UIFactory.Stretch(art.rectTransform);
        art.rectTransform.anchorMin = art.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        RectTransform a = art.rectTransform;

        title = UIFactory.MakeText("Title", root, "", 10, Color.clear, TextAnchor.MiddleCenter);
        title.gameObject.SetActive(false);
        for (int i = 0; i < 3; i++)
        {
            lines[i] = UIFactory.MakeText("Line" + i, root, "", 10, Color.clear, TextAnchor.MiddleCenter);
            lines[i].gameObject.SetActive(false);
        }

        // 런 기록: 그림 속 판 안쪽 (232,336)~(800,482)
        RectTransform recordSpot = UIFactory.ArtSpot("Record", a, ArtW, ArtH, 236, 340, 796, 480);
        recordGroup = recordSpot.gameObject.AddComponent<CanvasGroup>();
        recordText = UIFactory.MakeText("Text", recordSpot, "", 24, Color.white, TextAnchor.MiddleCenter);
        UIFactory.Stretch(recordText.rectTransform, 10, 10, 6, 6);
        recordText.supportRichText = true;
        recordText.resizeTextForBestFit = true;   // 4인 파티면 줄이 많아서 넘치지 않게
        recordText.verticalOverflow = VerticalWrapMode.Truncate;   // 3-154: 글자 자동 축소는 세로가 Truncate 일 때만 동작 (Overflow 면 줄지 않고 칸 밖으로 넘침)
        recordText.resizeTextMinSize = 14;
        recordText.resizeTextMaxSize = 26;

        // 버튼: 그림 속 [타이틀로] (330~500) / [새 런] (520~695)
        RectTransform buttons = UIFactory.MakeRect("Buttons", a);
        UIFactory.Stretch(buttons);
        buttonGroup = buttons.gameObject.AddComponent<CanvasGroup>();
        ArtButton(buttons, 0, "타이틀로", 336, 524, 496, 564, () => Close(onTitle));
        ArtButton(buttons, 1, "새 런", 526, 524, 690, 564, () => Close(onNewRun));
    }

    // 그림 속 버튼 틀 위에 투명 버튼 + 글자. 마우스를 올리면 은은한 빛
    void ArtButton(RectTransform parent, int index, string label, float x0, float y0, float x1, float y1, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform spot = UIFactory.ArtSpot("Button" + index, parent, ArtW, ArtH, x0, y0, x1, y1);
        Image glow = UIFactory.MakeImage("Glow", spot, new Color(1f, 0.85f, 0.6f, 0f), false);
        glow.sprite = UIFactory.SoftGlowSprite;
        UIFactory.Stretch(glow.rectTransform, -30, -30, -20, -20);
        glow.raycastTarget = false;
        Image hit = UIFactory.MakeImage("Hit", spot, new Color(0, 0, 0, 0), false);
        UIFactory.Stretch(hit.rectTransform);
        hit.raycastTarget = true;
        Button b = hit.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.onClick.AddListener(onClick);
        HoverRelay hover = hit.gameObject.AddComponent<HoverRelay>();
        hover.OnChange = (on) => { glow.color = new Color(1f, 0.85f, 0.6f, on ? 0.3f : 0f); };
        Text t = UIFactory.MakeText("Label", spot, label, 30, UIFactory.Hex("#F4EAD6"), TextAnchor.MiddleCenter);
        UIFactory.Stretch(t.rectTransform);
        t.fontStyle = FontStyle.Bold;
        buttonLabels[index] = t;
    }

    // run = 방금 끝난 런. 버튼을 누르면 이 화면을 닫고 그 콜백을 부름
    public void Show(RunState run, System.Action toTitle, System.Action newRun)
    {
        onTitle = toTitle;
        onNewRun = newRun;
        // 몇 번째 클리어인지 (컴퓨터에 저장. 자바의 Preferences 와 비슷)
        int clears = PlayerPrefs.GetInt(ClearCountKey, 0) + 1;
        PlayerPrefs.SetInt(ClearCountKey, clears);
        PlayerPrefs.Save();
        recordText.text = art != null ? ArtRecordText(run, clears) : RecordText(run, clears);

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        if (art != null)
        {
            // 그림 크기에 맞춰 글자 크기 (그림 1픽셀 = 화면 k칸)
            float k = UIFactory.ArtScale(art.rectTransform, ArtW);
            recordText.resizeTextMaxSize = Mathf.RoundToInt(14 * k);
            recordText.resizeTextMinSize = Mathf.RoundToInt(8 * k);
            foreach (Text t in buttonLabels) if (t != null) t.fontSize = Mathf.RoundToInt(15 * k);
        }
        StopAllCoroutines();
        StartCoroutine(Play());
    }

    public void Hide()
    {
        StopAllCoroutines();
        gameObject.SetActive(false);
    }

    void Close(System.Action after)
    {
        Hide();
        if (after != null) after();
    }

    // 런 기록 글자
    static string RecordText(RunState run, int clears)
    {
        string s = "<b><color=#F0C850>런 기록</color></b>   <size=20><color=#C8C0B0>" + clears + "번째 클리어</color></size>\n";
        foreach (PlayerCombatant p in run.Players)
        {
            s += p.Name + " " + ClassLibrary.ColoredClassName(p.Class) + "   Lv " + p.Level
               + "   HP " + Mathf.Max(p.Hp, 0) + "/" + p.MaxHp
               + "   덱 " + p.Deck.Count + "장   아티팩트 " + p.Relics.Count + "개   골드 " + p.Gold + "\n";
        }
        int sec = Mathf.FloorToInt(run.PlaySeconds);
        string time = sec >= 3600
            ? (sec / 3600) + "시간 " + (sec / 60 % 60) + "분"
            : (sec / 60) + "분 " + (sec % 60) + "초";
        s += "\n<color=#9CE0FF>이긴 전투 " + run.BattlesWon + "   ·   엘리트 " + run.EliteWins + "   ·   보스 " + run.BossWins
           + "   ·   걸린 시간 " + time + "</color>";
        return s;
    }

    // 3-148: 그림 화면용 런 기록 (목업처럼 항목마다 색: Lv 금색 · HP 빨강 · 덱 보라 · 아티팩트 하늘 · 골드 노랑)
    static string ArtRecordText(RunState run, int clears)
    {
        string s = "<color=#F0B850><b>런 기록</b></color>   <color=#E89A50>" + clears + "번째 클리어</color>\n";
        foreach (PlayerCombatant p in run.Players)
        {
            s += "<b>" + p.Name + " " + ClassLibrary.ColoredClassName(p.Class) + "</b>   <color=#F0D890>Lv " + p.Level + "</color>"
               + "   <color=#FF6A5A>HP " + Mathf.Max(p.Hp, 0) + "/" + p.MaxHp + "</color>"
               + "   <color=#C89CFF>덱 " + p.Deck.Count + "장</color>   <color=#7CCBFF>아티팩트 " + p.Relics.Count + "개</color>"
               + "   <color=#F2C25A>골드 " + p.Gold + "</color>\n";
        }
        int sec = Mathf.FloorToInt(run.PlaySeconds);
        string time = sec >= 3600 ? (sec / 3600) + "시간 " + (sec / 60 % 60) + "분" : (sec / 60) + "분 " + (sec % 60) + "초";
        s += "\n<color=#C8C4BC>이긴 전투 " + run.BattlesWon + "   ·   엘리트 " + run.EliteWins + "   ·   보스 " + run.BossWins
           + "   ·   걸린 시간 </color><color=#F2C25A>" + time + "</color>";
        return s;
    }

    // 연출 순서: 빛 → 제목 → 문구 3줄 → 기록 → 버튼 (WaitForSecondsRealtime = 게임 속도와 상관없이 실제 시간)
    IEnumerator Play()
    {
        SetAlpha(title, 0f);
        foreach (Text t in lines) SetAlpha(t, 0f);
        recordGroup.alpha = 0f;
        buttonGroup.alpha = 0f;
        buttonGroup.interactable = false;
        flash.color = Color.white;

        yield return Fade(a => flash.color = new Color(1f, 1f, 1f, 1f - a), 1.2f);
        yield return Fade(a => SetAlpha(title, a), 0.8f);
        for (int i = 0; i < lines.Length; i++)
        {
            yield return new WaitForSecondsRealtime(0.35f);
            int k = i;
            yield return Fade(a => SetAlpha(lines[k], a), 0.7f);
        }
        yield return new WaitForSecondsRealtime(0.4f);
        yield return Fade(a => recordGroup.alpha = a, 0.6f);
        yield return Fade(a => buttonGroup.alpha = a, 0.4f);
        buttonGroup.interactable = true;
    }

    // 0 → 1 로 dur 초 동안 바꾸며 set 을 부름
    IEnumerator Fade(System.Action<float> set, float dur)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            set(Mathf.Clamp01(t / dur));
            yield return null;
        }
        set(1f);
    }

    static void SetAlpha(Text t, float a)
    {
        Color c = t.color;
        c.a = a;
        t.color = c;
    }
}
