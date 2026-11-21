// ============================================================
// DifficultyPanel.cs  (3-196 난이도 선택 화면)
// 캐릭터 선택 → [이 화면] → 1스테이지
//
//   위: "난이도" 제목 + 안내 (멀티면 "P1(방장)이 고릅니다")
//   가운데: 버튼 5개 (쉬움 · 보통 · 어려움 · 매우 어려움 · 지옥). 고른 버튼 = 금빛 판
//   아래: 설명 상자 (마우스를 올린 난이도, 없으면 고른 난이도의 효과)
//   왼쪽 아래 [뒤로] = 캐릭터 선택으로 / 오른쪽 아래 [시작] = 이 난이도로 런 시작
//
// 처음부터 5개 모두 고를 수 있음. 기본 = 보통 (아무것도 안 눌러도 보통으로 시작)
// 그림 (3-199): 버튼 = UI/Difficulty/Diff_Easy · Normal · Hard · VeryHard · Hell (난이도마다 판 색이 다른 틀)
//              설명 = UI/Difficulty/Diff_Box (사슬·가시 양피지), 배경 = Backgrounds/Compendium_BG
//   고른 버튼 = 뒤에 난이도 색 빛 + 조금 크게. 새 그림이 없으면 예전 도감 버튼 그림(Comp_Toggle)으로
//   그림이 없으면 색 네모로 보임
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DifficultyPanel : MonoBehaviour
{
    private static readonly Difficulty[] Order = { Difficulty.Easy, Difficulty.Normal, Difficulty.Hard, Difficulty.VeryHard, Difficulty.Hell };
    // 난이도마다 글자 색 (쉬움 초록 → 지옥 붉은색)
    private static readonly string[] Colors = { "#9BE07A", "#F0E6D2", "#FFC86A", "#FF8A4A", "#FF4A4A" };
    // 3-199: 버튼 위 글자 색 (판 색을 밝게 한 색) · 버튼 그림 이름 · 고른 버튼 뒤 빛 색
    private static readonly string[] LabelColors = { "#DDF2B4", "#F6F6F6", "#FFF0B8", "#FFD4B0", "#FF6A5A" };
    private static readonly string[] ArtNames = { "Diff_Easy", "Diff_Normal", "Diff_Hard", "Diff_VeryHard", "Diff_Hell" };
    private static readonly string[] GlowColors = { "#8CFF5A", "#FFFFFF", "#FFD23A", "#FF7A2A", "#FF2A2A" };
    private readonly List<Image> glows = new List<Image>();
    private readonly List<Sprite> arts = new List<Sprite>();   // 난이도별 버튼 그림 (없으면 null)

    private Difficulty selected = Difficulty.Normal;
    private Difficulty? hovered;
    private readonly List<Image> buttons = new List<Image>();
    private Sprite offArt, onArt;
    private Text subtitle, descTitle, descBody, startLabel;
    private System.Action<Difficulty> onStart;
    private System.Action onBack;

    public bool IsOpen { get { return gameObject.activeSelf; } }

    public static DifficultyPanel Create(Transform parent)
    {
        RectTransform rt = UIFactory.MakeRect("Difficulty", parent);
        UIFactory.Stretch(rt);
        DifficultyPanel p = rt.gameObject.AddComponent<DifficultyPanel>();
        p.Build();
        rt.gameObject.SetActive(false);
        return p;
    }

    void Build()
    {
        // 배경 (도감과 같은 보랏빛 성문) + 어두운 막
        Image bg = UIFactory.MakeImage("Bg", transform, UIFactory.Hex("#0C0A12"), false);
        UIFactory.Stretch(bg.rectTransform);
        Sprite bgArt = Resources.Load<Sprite>("Backgrounds/Compendium_BG");
        if (bgArt != null) { bg.sprite = bgArt; bg.color = Color.white; }
        Image shade = UIFactory.MakeImage("Shade", transform, new Color(0.02f, 0.01f, 0.04f, 0.55f), false);
        UIFactory.Stretch(shade.rectTransform);
        shade.raycastTarget = false;

        Text title = UIFactory.MakeText("Title", transform, "난이도", 64, UIFactory.Hex("#FFD27A"), TextAnchor.MiddleCenter);
        UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -60), new Vector2(800, 80));
        title.font = UIFactory.TitleFont;
        title.gameObject.AddComponent<Outline>().effectColor = new Color(0.1f, 0.04f, 0f, 0.9f);
        subtitle = UIFactory.MakeText("Sub", transform, "", 26, UIFactory.Hex("#E8DCC4"), TextAnchor.MiddleCenter);
        UIFactory.Place(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -140), new Vector2(1200, 40));
        subtitle.fontStyle = FontStyle.Bold;

        offArt = Resources.Load<Sprite>("UI/Compendium/Comp_Toggle_Off");
        onArt = Resources.Load<Sprite>("UI/Compendium/Comp_Toggle_On");

        // 버튼 5개 (가로 한 줄)
        for (int i = 0; i < Order.Length; i++)
        {
            Difficulty d = Order[i];
            // 3-199: 고른 버튼 뒤에서 빛나는 부드러운 빛 (버튼보다 먼저 만들어야 뒤에 그려짐)
            Image glow = UIFactory.MakeImage("Glow_" + d, transform, UIFactory.Hex(GlowColors[i]), false);
            glow.sprite = UIFactory.SoftGlowSprite;
            UIFactory.Place(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - 2) * 340f, 190f), new Vector2(360, 180));
            glow.raycastTarget = false;
            glows.Add(glow);
            arts.Add(Resources.Load<Sprite>("UI/Difficulty/" + ArtNames[i]));

            Image img = UIFactory.MakeImage("Btn_" + d, transform, UIFactory.Hex("#2C2840"), false);
            UIFactory.Place(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - 2) * 340f, 190f), new Vector2(320, 126));
            img.raycastTarget = true;
            Text t = UIFactory.MakeText("Label", img.transform, DifficultyInfo.Name(d), 32, UIFactory.Hex(LabelColors[i]), TextAnchor.MiddleCenter);
            UIFactory.Stretch(t.rectTransform, 30, 30, 30, 30);
            t.font = UIFactory.TitleFont;
            t.raycastTarget = false;
            Outline o = t.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0, 0, 0, 0.95f);
            o.effectDistance = new Vector2(2f, -2f);   // 3-199: 밝은 판(보통·어려움) 위에서도 읽히게 조금 두껍게
            Button b = img.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => { SoundManager.Play("Click"); selected = d; Redraw(); });
            HoverRelay hover = img.gameObject.AddComponent<HoverRelay>();
            hover.OnChange = (on) =>
            {
                if (on) hovered = d; else if (hovered == d) hovered = null;
                Redraw();
            };
            buttons.Add(img);
        }

        // 설명 상자
        Image box = UIFactory.MakeImage("Desc", transform, new Color(0.1f, 0.08f, 0.14f, 0.95f));
        UIFactory.Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -160), new Vector2(780, 472));   // 3-199: 양피지 그림 비율(약 1.65:1)
        box.raycastTarget = false;
        Sprite boxArt = Resources.Load<Sprite>("UI/Difficulty/Diff_Box");   // 3-199
        if (boxArt == null) boxArt = Resources.Load<Sprite>("UI/Compendium/Comp_DetailBox");
        if (boxArt != null) { box.sprite = boxArt; box.type = Image.Type.Simple; box.color = Color.white; }
        descTitle = UIFactory.MakeText("DescTitle", box.transform, "", 38, UIFactory.Hex("#FFD27A"), TextAnchor.UpperCenter);
        UIFactory.Place(descTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -64), new Vector2(600, 52));   // 3-199: 양피지 위쪽 룬 무늬 바로 아래
        descTitle.font = UIFactory.TitleFont;
        descTitle.raycastTarget = false;
        descTitle.gameObject.AddComponent<Outline>().effectColor = new Color(0.12f, 0.06f, 0.02f, 0.95f);
        descBody = UIFactory.MakeText("DescBody", box.transform, "", 25, UIFactory.Hex("#E8E0D0"), TextAnchor.UpperCenter);
        UIFactory.Stretch(descBody.rectTransform, 110, 110, 128, 80);   // 3-199: 양피지 테두리 룬 안쪽
        descBody.lineSpacing = 1.25f;
        descBody.fontStyle = FontStyle.Bold;
        descBody.raycastTarget = false;
        Outline bo = descBody.gameObject.AddComponent<Outline>();   // 3-199: 밝은 갈색 양피지 위에서 읽히게 어두운 테두리
        bo.effectColor = new Color(0.1f, 0.06f, 0.03f, 0.95f);
        bo.effectDistance = new Vector2(1.5f, -1.5f);
        descBody.resizeTextForBestFit = true;
        descBody.verticalOverflow = VerticalWrapMode.Truncate;
        descBody.resizeTextMinSize = 16;
        descBody.resizeTextMaxSize = 25;

        // 아래 버튼
        Button back = UIFactory.MakeButton("Back", transform, "뒤로", UIFactory.Hex("#3A3446"), () => { if (onBack != null) onBack(); }, 30);
        UIFactory.Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(60, 40), new Vector2(240, 84));
        UIFactory.SkinButton(back, UIFactory.GothicButton);
        Button start = UIFactory.MakeButton("Start", transform, "시작", UIFactory.Hex("#7846C8"), () => { if (onStart != null) onStart(selected); }, 30);
        UIFactory.Place((RectTransform)start.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60, 40), new Vector2(340, 90));
        UIFactory.SkinButton(start, UIFactory.GothicButton);
        startLabel = start.GetComponentInChildren<Text>();
    }

    // players = 인원 (멀티면 방장이 고른다는 안내), current = 처음 골라 둘 난이도 (지난 런과 같게)
    public void Show(int players, Difficulty current, System.Action<Difficulty> start, System.Action back)
    {
        selected = current;
        hovered = null;
        onStart = start;
        onBack = back;
        subtitle.text = players > 1 ? "P1(방장)이 난이도를 고르세요. 마우스를 올리면 효과가 보입니다." : "난이도를 고르세요. 마우스를 올리면 효과가 보입니다.";
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Redraw();
    }

    public void Hide() { gameObject.SetActive(false); }

    void Redraw()
    {
        for (int i = 0; i < buttons.Count; i++)
        {
            bool on = Order[i] == selected;
            Image img = buttons[i];
            glows[i].enabled = on;                                                       // 3-199: 고른 것만 뒤에 빛
            bool big = on || hovered == Order[i];
            img.rectTransform.localScale = big ? new Vector3(1.06f, 1.06f, 1f) : Vector3.one;
            if (arts[i] != null)
            {
                img.sprite = arts[i];                                                    // 3-199: 난이도 색 판
                img.color = on || hovered == Order[i] ? Color.white : new Color(0.82f, 0.82f, 0.82f, 1f);   // 안 고른 건 살짝 어둡게
            }
            else if (offArt != null)
            {
                img.sprite = (on && onArt != null) ? onArt : offArt;
                img.color = Color.white;
            }
            else img.color = on ? UIFactory.Hex("#5A4A8A") : UIFactory.Hex("#2C2840");
        }
        Difficulty show = hovered ?? selected;   // ?? = 왼쪽이 없으면(null) 오른쪽 (자바의 Optional.orElse)
        int ci = System.Array.IndexOf(Order, show);
        descTitle.text = DifficultyInfo.Name(show) + (show == selected ? "  (선택됨)" : "");
        descTitle.color = UIFactory.Hex(Colors[ci]);
        descBody.text = DifficultyInfo.Description(show);
        startLabel.text = DifficultyInfo.Name(selected) + " 시작";
    }
}
