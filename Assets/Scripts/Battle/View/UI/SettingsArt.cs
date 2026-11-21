// ============================================================
// SettingsArt.cs   (3-170)
// 설정 창을 "그림 판" 으로 만들 때 쓰는 도우미 모음 (일시정지 메뉴 설정 · 타이틀 설정 둘 다 씀)
//
//   그림(Resources/UI/Pause/Settings_*.png) 속 단추 자리 위에
//   투명 단추 + 마우스를 올리면 은은한 빛 + 바뀌는 글자(60%, 켜짐/꺼짐) 만 코드가 얹음
//   좌표는 전부 "그림 픽셀" (왼쪽 위가 0,0) → UIFactory.ArtSpot 이 비율 앵커로 바꿔 줌
//   → 창 크기가 바뀌어도 그림 속 그 자리에 붙어 있음
//
//   static 클래스 = 자바의 "static 메서드만 있는 유틸 클래스" (Math 처럼 new 안 하고 바로 씀)
// ============================================================

using UnityEngine;
using UnityEngine.UI;

public static class SettingsArt
{
    // 그림 속 상자 하나 (x0,y0)~(x1,y1). 자바로 치면 int[4] 대신 쓰는 작은 묶음
    public struct Box
    {
        public float X0, Y0, X1, Y1;
        public Box(float x0, float y0, float x1, float y1) { X0 = x0; Y0 = y0; X1 = x1; Y1 = y1; }
    }

    // 그림 크기 (w×h). 모든 함수가 이 값으로 비율을 계산
    public struct Art
    {
        public RectTransform Rt;
        public float W, H;
    }

    // 그림 판 만들기: parent 가운데에 그림을 놓고, 화면 높이 displayH 로 맞춤 (가로는 그림 비율대로)
    public static Art MakePanel(string name, Transform parent, Sprite sprite, float displayH)
    {
        Image img = UIFactory.MakeImage(name, parent, Color.white, false);
        img.raycastTarget = true;   // 판 뒤(게임 화면)가 눌리지 않게 막음
        img.sprite = sprite;
        img.type = Image.Type.Simple;
        Art a;
        a.Rt = img.rectTransform;
        a.W = sprite.rect.width;
        a.H = sprite.rect.height;
        UIFactory.Place(a.Rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(a.W, a.H) * (displayH / a.H));
        return a;
    }

    // 투명 단추 (마우스를 올리면 빛). 그림에 이미 단추가 그려져 있어서 눈에 보이는 건 빛뿐
    public static Button Hotspot(Art art, string name, Box b, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform spot = UIFactory.ArtSpot(name, art.Rt, art.W, art.H, b.X0, b.Y0, b.X1, b.Y1);
        Image glow = UIFactory.MakeImage("Glow", spot, new Color(1f, 0.85f, 0.55f, 0f), false);
        glow.sprite = UIFactory.SoftGlowSprite;
        UIFactory.Stretch(glow.rectTransform, -16, -16, -12, -12);
        glow.raycastTarget = false;
        Image hit = UIFactory.MakeImage("Hit", spot, new Color(0, 0, 0, 0), false);
        UIFactory.Stretch(hit.rectTransform);
        hit.raycastTarget = true;
        Button btn = hit.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        if (onClick != null) btn.onClick.AddListener(onClick);
        HoverRelay hover = hit.gameObject.AddComponent<HoverRelay>();
        hover.OnChange = (on) => { glow.color = new Color(1f, 0.85f, 0.55f, on ? 0.3f : 0f); };
        return btn;
    }

    // 그림 속 칸에 글자 (자동 축소 + 검은 테두리)
    public static Text Label(Art art, string name, Box b, string text, int maxSize, Color color)
    {
        RectTransform spot = UIFactory.ArtSpot(name, art.Rt, art.W, art.H, b.X0, b.Y0, b.X1, b.Y1);
        Text t = UIFactory.MakeText("Text", spot, text, maxSize, color, TextAnchor.MiddleCenter);
        UIFactory.Stretch(t.rectTransform);
        t.fontStyle = FontStyle.Bold;
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = 12;
        t.resizeTextMaxSize = maxSize;
        t.verticalOverflow = VerticalWrapMode.Truncate;   // 3-154: 자동 축소는 Truncate 일 때만 동작
        t.raycastTarget = false;
        t.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.85f);
        return t;
    }

    // 소리 한 줄: [-] (막대) [+]  60%
    //   bar = 막대 안쪽 칸 (없으면 막대 없이 숫자만), fill = 파란 채움 그림
    //   돌려주는 refresh() 를 부르면 지금 값으로 다시 그림 (창을 다시 열 때 등)
    //   Image.Type.Filled + fillAmount = 그림을 왼쪽부터 v 만큼만 보여 줌 (자바로 치면 drawImage 의 잘라 그리기)
    public static System.Action VolumeRow(Art art, string name, Box minus, Box plus, Box value, Box? bar, Sprite fill,
                                          int fontSize, System.Func<float> get, System.Action<float> set)
    {
        Text valueText = Label(art, name + "Value", value, "", fontSize, UIFactory.Hex("#F2C46A"));
        Image fillImg = null;
        if (bar.HasValue && fill != null)
        {
            Box b = bar.Value;
            RectTransform spot = UIFactory.ArtSpot(name + "Bar", art.Rt, art.W, art.H, b.X0, b.Y0, b.X1, b.Y1);
            fillImg = UIFactory.MakeImage("Fill", spot, Color.white, false);
            UIFactory.Stretch(fillImg.rectTransform);
            fillImg.sprite = fill;
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImg.raycastTarget = false;
        }
        System.Action refresh = () =>
        {
            float v = get();
            valueText.text = Mathf.RoundToInt(v * 100f) + "%";
            if (fillImg != null) fillImg.fillAmount = v;
        };
        refresh();
        // 10% 씩. Round 로 0.30000001 같은 오차를 없앰
        Hotspot(art, name + "Minus", minus, () => { set(Mathf.Clamp01(Mathf.Round((get() - 0.1f) * 10f) / 10f)); GameSettings.Apply(); GameSettings.Save(); refresh(); });
        Hotspot(art, name + "Plus", plus, () => { set(Mathf.Clamp01(Mathf.Round((get() + 0.1f) * 10f) / 10f)); GameSettings.Apply(); GameSettings.Save(); refresh(); });
        return refresh;
    }

    // 켜짐/꺼짐 단추: 켜지면 onSprite(청록), 꺼지면 offSprite(회색) + 글자
    //   그림 판의 같은 자리를 잘라 만든 그림이라 덮어도 이음새가 안 보임
    public static System.Action Toggle(Art art, string name, Box b, Sprite onSprite, Sprite offSprite, int fontSize,
                                       System.Func<bool> get, System.Action flip)
    {
        RectTransform spot = UIFactory.ArtSpot(name, art.Rt, art.W, art.H, b.X0, b.Y0, b.X1, b.Y1);
        Image face = UIFactory.MakeImage("Face", spot, Color.white, false);
        UIFactory.Stretch(face.rectTransform);
        face.raycastTarget = false;
        Text label = Label(art, name + "Text", b, "", fontSize, UIFactory.Hex("#F6EAD2"));
        System.Action refresh = () =>
        {
            bool on = get();
            face.sprite = on ? onSprite : offSprite;
            face.enabled = face.sprite != null;
            label.text = on ? "켜짐" : "꺼짐";
            label.color = on ? UIFactory.Hex("#F6EAD2") : UIFactory.Hex("#B8B2A8");
        };
        refresh();
        Hotspot(art, name + "Hit", b, () => { flip(); refresh(); });
        return refresh;
    }
}
