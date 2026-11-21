// ============================================================
// UIFactory.cs
// 화면 부품(이미지, 글자, 버튼)을 코드로 만드는 도우미 모음입니다.
//
// 그림 파일이 아직 없어서, 둥근 사각형과 원 모양을 코드로 직접 그려서 씁니다.
// 나중에 진짜 그림이 생기면 이 파일의 스프라이트만 바꾸면 됩니다.
//
// static class = 객체를 만들지 않고 UIFactory.MakeText(...) 처럼 바로 부르는 클래스
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public static class UIFactory
{
    private static Font font;
    private static Sprite roundedSprite;
    private static Sprite circleSprite;

    // 한글 폰트: 프로젝트에 넣은 "본고딕 굵게"(Resources/Fonts/NotoSansKR-Bold.otf)
    // 없으면 윈도우의 "맑은 고딕"을 가져다 씀 (컴퓨터마다 글자가 달라 보이지 않게 폰트 파일을 직접 넣음)
    public static Font DefaultFont
    {
        get
        {
            if (font == null) font = Resources.Load<Font>("Fonts/NotoSansKR-Bold");
            if (font == null)
            {
                font = Font.CreateDynamicFontFromOSFont(new string[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Arial" }, 32);
            }
            return font;
        }
    }

    // 제목용 폰트: "본명조 아주 굵게" (판타지 느낌). 큰 글자(TitleSize 이상)에 자동으로 씀
    private static Font titleFont;
    public const int TitleSize = 40;
    public static Font TitleFont
    {
        get
        {
            if (titleFont == null) titleFont = Resources.Load<Font>("Fonts/NotoSerifKR-Black");
            return titleFont != null ? titleFont : DefaultFont;
        }
    }

    // 모서리가 둥근 사각형 (늘려도 모서리가 찌그러지지 않는 9-slice 스프라이트)
    public static Sprite Rounded
    {
        get
        {
            if (roundedSprite == null) roundedSprite = MakeRoundedSprite(128, 28);   // 2배로 그려서 모서리를 매끈하게
            return roundedSprite;
        }
    }

    // 동그라미
    public static Sprite Circle
    {
        get
        {
            if (circleSprite == null) circleSprite = MakeCircleSprite(256);          // 큰 원도 매끈하게
            return circleSprite;
        }
    }

    // ---------------- 모양 그리기 ----------------

    private static Sprite MakeRoundedSprite(int size, int radius)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // 가장 가까운 모서리 원의 중심을 구해서, 그 원 밖이면 투명하게
                float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                float alpha = Mathf.Clamp01(radius - dist + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        Vector4 border = new Vector4(radius, radius, radius, radius);
        // pixelsPerUnit 200 = 2배로 그린 그림이라 화면에서는 반 크기로 (모서리 둥글기는 예전과 같음)
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 200f, 0, SpriteMeshType.FullRect, border);
    }

    private static Sprite MakeCircleSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float r = size / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                float alpha = Mathf.Clamp01(r - dist + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    // 가운데가 밝고 바깥으로 갈수록 투명해지는 부드러운 빛 (캐릭터 뒤 조명용)
    private static Sprite softGlowSprite;
    public static Sprite SoftGlowSprite
    {
        get
        {
            if (softGlowSprite == null)
            {
                int size = 128;
                Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                float r = size / 2f;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;   // 0(가운데) ~ 1(가장자리)
                        float a = Mathf.Clamp01(1f - d);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                    }
                tex.Apply();
                softGlowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            }
            return softGlowSprite;
        }
    }

    // ---------------- 필살기 연출용 모양 ----------------
    // 코드로 그린 텍스처를 한 번만 만들어 두고 계속 씀 (Dictionary = 자바의 HashMap)
    private static System.Collections.Generic.Dictionary<string, Sprite> fxSprites = new System.Collections.Generic.Dictionary<string, Sprite>();

    // 초승달: 큰 원에서, 오른쪽으로 비켜난 작은 원을 빼면 초승달 모양
    public static Sprite CrescentSprite { get { return FxSprite("crescent", 256, 256, (u, v) =>
    {
        float d1 = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f));
        float d2 = Vector2.Distance(new Vector2(u, v), new Vector2(0.62f, 0.5f));
        float outer = Mathf.Clamp01((0.5f - d1) * 80f);
        float inner = Mathf.Clamp01((d2 - 0.42f) * 80f);
        return outer * inner;
    }); } }

    // 세로 빛기둥: 가운데가 진하고 양옆으로 갈수록 투명
    public static Sprite BeamSprite { get { return FxSprite("beam", 64, 8, (u, v) =>
    {
        float x = Mathf.Abs(u - 0.5f) * 2f;   // 0(가운데) ~ 1(가장자리)
        return Mathf.Clamp01(1f - x) * Mathf.Clamp01(1f - x);
    }); } }

    // 화면 가장자리만 진한 비네트 (가운데는 투명)
    public static Sprite VignetteSprite { get { return FxSprite("vignette", 128, 128, (u, v) =>
    {
        float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) / 0.707f;   // 0 ~ 1
        return Mathf.Clamp01((d - 0.45f) / 0.55f);
    }); } }

    static Sprite FxSprite(string key, int w, int h, System.Func<float, float, float> alpha)
    {
        Sprite s;
        if (fxSprites.TryGetValue(key, out s)) return s;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha((x + 0.5f) / w, (y + 0.5f) / h)));
        tex.Apply();
        s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
        fxSprites[key] = s;
        return s;
    }

    // 가운데가 빈 고리 (방어도 이펙트용)
    private static Sprite ringSprite;
    public static Sprite RingSprite
    {
        get
        {
            if (ringSprite == null)
            {
                int size = 256;
                Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                float r = size / 2f;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                        float outer = Mathf.Clamp01(r - d + 0.5f);          // 바깥 원 안쪽이면 1
                        float inner = Mathf.Clamp01(d - (r - 20f) + 0.5f);  // 안쪽 원 바깥이면 1
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, outer * inner));
                    }
                tex.Apply();
                ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            }
            return ringSprite;
        }
    }

    // 위쪽을 가리키는 삼각형 (화살표 머리용)
    private static Sprite triangleSprite;
    public static Sprite TriangleSprite
    {
        get
        {
            if (triangleSprite == null)
            {
                int size = 64;
                Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        // 아래(y=0)는 넓고 위(y=size)로 갈수록 좁아짐
                        float half = (size - y) / 2f;
                        float dx = Mathf.Abs(x + 0.5f - size / 2f);
                        float a = Mathf.Clamp01(half - dx + 0.5f);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                tex.Apply();
                triangleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            }
            return triangleSprite;
        }
    }

    // 위에서 아래로 색이 변하는 그라데이션 (배경용)
    public static Sprite MakeGradient(Color top, Color bottom)
    {
        Texture2D tex = new Texture2D(2, 256, TextureFormat.RGBA32, false);   // 256단계 = 띠(줄무늬)가 안 보임
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < 256; y++)
        {
            Color c = Color.Lerp(bottom, top, y / 255f);   // 텍스처의 y=0 이 아래쪽
            tex.SetPixel(0, y, c);
            tex.SetPixel(1, y, c);
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 2, 256), new Vector2(0.5f, 0.5f));
    }

    // ---------------- 부품 만들기 ----------------

    // 빈 RectTransform (위치·크기만 가진 상자)
    public static RectTransform MakeRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    // 색이 칠해진 상자. rounded = true 면 모서리가 둥글게
    public static Image MakeImage(string name, Transform parent, Color color, bool rounded = true)
    {
        RectTransform rt = MakeRect(name, parent);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        if (rounded)
        {
            img.sprite = Rounded;
            img.type = Image.Type.Sliced;
        }
        return img;
    }

    public static Image MakeCircle(string name, Transform parent, Color color)
    {
        RectTransform rt = MakeRect(name, parent);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = Circle;
        img.color = color;
        return img;
    }

    // 글자. raycastTarget = false 로 마우스 클릭을 가로채지 않게 함 (카드 호버가 깜빡이지 않도록)
    public static Text MakeText(string name, Transform parent, string content, int size, Color color, TextAnchor align)
    {
        RectTransform rt = MakeRect(name, parent);
        Text t = rt.gameObject.AddComponent<Text>();
        bool title = size >= TitleSize;
        t.font = title ? TitleFont : DefaultFont;   // 큰 글자는 제목용 폰트
        t.lineSpacing = 0.9f;                       // 본고딕은 줄 간격이 넓어서 조금 좁힘 (맑은 고딕과 비슷하게)
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.supportRichText = true;
        // 글자 뒤 그림자: 밝은 배경·어두운 배경 어디서든 또렷하게 읽힘 (큰 글자는 테두리까지)
        //   작은 글자는 그림자를 옅게 (진하면 획이 뭉개져 보임)
        Shadow sh = rt.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, size >= 24 ? 0.6f : 0.45f);
        sh.effectDistance = new Vector2(size >= 24 ? 2f : 1f, size >= 24 ? -2f : -1f);
        if (title)
        {
            Outline ol = rt.gameObject.AddComponent<Outline>();
            ol.effectColor = new Color(0f, 0f, 0f, 0.75f);
            ol.effectDistance = new Vector2(2f, -2f);
        }
        return t;
    }

    // 글자를 k배 크게 그린 뒤 1/k 로 줄여 놓음 → 그 상자가 나중에 커져도(카드 마우스 올림 1.25배 등) 글자가 흐려지지 않음
    //   유니티 기본 글자(Text)는 "글자 크기 × 화면 배율"로 한 번 그려 두고 그 그림을 늘려서 보여 주기 때문에,
    //   물체를 키우면 글자가 뿌옇게 됨. 미리 크게 그려 두면 키워도 선명함 (사진을 크게 찍어 두는 것과 같음)
    //   이미 자리를 잡은 글자에 부르면 됨 (늘어나게 놓은 글자도 OK)
    public static void Supersample(Text t, float k = 2f)
    {
        RectTransform rt = t.rectTransform;
        RectTransform parent = rt.parent as RectTransform;
        if (parent == null) return;
        Rect r = rt.rect;
        Vector2 centerInParent = (Vector2)rt.localPosition + r.center;   // 부모 기준점에서 본 글자 상자 가운데
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = r.size * k;
        rt.anchoredPosition = centerInParent - parent.rect.center;
        rt.localScale = new Vector3(1f / k, 1f / k, 1f);
        t.fontSize = Mathf.RoundToInt(t.fontSize * k);
        t.resizeTextMinSize = Mathf.RoundToInt(t.resizeTextMinSize * k);
        t.resizeTextMaxSize = Mathf.RoundToInt(t.resizeTextMaxSize * k);
        foreach (Shadow sh in t.GetComponents<Shadow>()) sh.effectDistance *= k;   // Outline 도 Shadow 의 한 종류
    }

    // 버튼 (색 상자 + 가운데 글자)
    public static Button MakeButton(string name, Transform parent, string label, Color color, UnityAction onClick, int fontSize = 24)
    {
        Image img = MakeImage(name, parent, color);
        Button btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        ColorBlock colors = btn.colors;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
        btn.colors = colors;
        btn.onClick.AddListener(() => SoundManager.Play("Click"));   // 3-190: 모든 버튼 누르는 소리
        if (onClick != null) btn.onClick.AddListener(onClick);

        Text t = MakeText("Label", img.transform, label, fontSize, Color.white, TextAnchor.MiddleCenter);
        Stretch(t.rectTransform);
        return btn;
    }

    // ---------------- 3-181: 그림 버튼 입히기 ----------------
    // 이미 만든 버튼(MakeButton)에 그림 틀을 씌움. 그림이 없으면 아무것도 안 함 (예전 네모 버튼 그대로)
    //   - 버튼 자리(클릭 받는 네모)는 그대로 두고 투명하게 → 클릭 범위는 예전과 같음
    //   - 그 안에 "Skin" 그림을 깔고 AspectRatioFitter 로 그림 비율을 지킴
    //       EnvelopeParent = 버튼 네모를 다 덮도록 (가시 장식이 살짝 밖으로 나옴)
    //       FitInParent   = 버튼 네모 안에 다 들어가도록
    //   - 마우스를 올리면 밝게: 버튼 색 바꾸기 대상(targetGraphic)을 그림으로 바꿈
    //   자바로 치면 JButton 에 setIcon(배경 그림) + setContentAreaFilled(false) 를 하는 것과 비슷
    public const string GothicButton = "UI/Buttons/Btn_Gothic";   // 8번 그림: 그림 없던 버튼들에 쓰는 공용 틀
    public static Image SkinButton(Button btn, string spritePath, float scale = 1.08f, bool envelope = true)
    {
        Sprite s = Resources.Load<Sprite>(spritePath);
        if (s == null || btn == null) return null;
        Image baseImg = btn.GetComponent<Image>();
        Transform old = btn.transform.Find("Skin");
        Image skin;
        if (old != null)
        {
            skin = old.GetComponent<Image>();
            skin.gameObject.SetActive(true);
        }
        else
        {
            skin = MakeImage("Skin", btn.transform, Color.white, false);
            skin.transform.SetAsFirstSibling();   // 글자보다 뒤에
            Stretch(skin.rectTransform);
            skin.gameObject.AddComponent<AspectRatioFitter>();
        }
        AspectRatioFitter f = skin.GetComponent<AspectRatioFitter>();
        f.aspectMode = envelope ? AspectRatioFitter.AspectMode.EnvelopeParent : AspectRatioFitter.AspectMode.FitInParent;
        f.aspectRatio = s.rect.width / s.rect.height;
        skin.sprite = s;
        skin.type = Image.Type.Simple;
        skin.raycastTarget = false;
        skin.rectTransform.localScale = new Vector3(scale, scale, 1f);
        if (baseImg != null)
        {
            baseImg.color = new Color(1f, 1f, 1f, 0f);   // 원래 네모는 안 보이게 (알파 0 이어도 클릭은 받음)
            Outline o = baseImg.GetComponent<Outline>();
            if (o != null) o.enabled = false;
        }
        btn.targetGraphic = skin;
        foreach (Text t in btn.GetComponentsInChildren<Text>(true))
        {
            t.color = Hex("#F2E2C0");
            if (t.GetComponent<Outline>() == null)
            {
                Outline lo = t.gameObject.AddComponent<Outline>();
                lo.effectColor = new Color(0.08f, 0.04f, 0.02f, 0.9f);
                lo.effectDistance = new Vector2(1.5f, -1.5f);
            }
        }
        return skin;
    }

    // 그림 틀을 벗기고 예전 네모 버튼으로 (같은 버튼이 다른 그림을 쓸 때)
    public static void UnskinButton(Button btn, Color color)
    {
        if (btn == null) return;
        Transform old = btn.transform.Find("Skin");
        if (old != null) old.gameObject.SetActive(false);
        Image baseImg = btn.GetComponent<Image>();
        if (baseImg != null) { baseImg.color = color; btn.targetGraphic = baseImg; }
    }

    // ---------------- 위치 정하기 ----------------

    // anchor: 부모의 어디를 기준으로 할지 (0,0 = 왼쪽 아래, 1,1 = 오른쪽 위)
    // pivot : 내 상자의 어디를 그 위치에 맞출지
    public static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    // 부모 크기에 꽉 차게 (여백 지정 가능)
    public static void Stretch(RectTransform rt, float left = 0, float right = 0, float top = 0, float bottom = 0)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    // ---------------- 그림 기준 자리 잡기 (3-148) ----------------
    // 디자인 그림(예: 상점 1024×572) 한 장을 화면에 "통째로 보이게" 놓고, 그 위 버튼·글자를 그림 속 픽셀 좌표로 놓기 위한 도우미
    //   FitInParent = 그림 전체가 보이도록 줄이거나 키움 (넘치는 부분 없음, 남는 곳은 뒤 검은 바탕)
    //   자바로 치면 "그림 좌표 → 화면 좌표" 변환 함수를 하나 두고, 모든 버튼을 그 함수로 놓는 것
    public static Image MakeArtBackground(string name, Transform parent, Sprite sprite)
    {
        Image img = MakeImage(name, parent, Color.white, false);
        img.sprite = sprite;
        img.raycastTarget = false;
        RectTransform rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        AspectRatioFitter fit = img.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;   // 그림이 잘리지 않게 (글자·버튼이 그림 끝에 있어도 안전)
        fit.aspectRatio = sprite.rect.width / sprite.rect.height;
        return img;
    }

    // 그림 속 상자 (x0,y0)~(x1,y1) 자리에 늘어나는 상자를 만듦. 좌표는 그림 왼쪽 위가 (0,0), 단위는 그림 픽셀 (w×h = 그림 크기)
    //   anchor 를 비율로 잡으므로 화면 크기가 바뀌어도 그림 속 그 자리에 붙어 있음
    public static RectTransform ArtSpot(string name, Transform art, float w, float h, float x0, float y0, float x1, float y1)
    {
        RectTransform rt = MakeRect(name, art);
        rt.anchorMin = new Vector2(x0 / w, 1f - y1 / h);
        rt.anchorMax = new Vector2(x1 / w, 1f - y0 / h);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    // 그림 1픽셀이 지금 화면(캔버스)에서 몇 칸인지. 방금 켠 화면은 크기 맞춤이 아직 안 됐을 수 있어서 먼저 강제로 한 번 계산
    public static float ArtScale(RectTransform art, float w)
    {
        Canvas.ForceUpdateCanvases();
        float k = art.rect.width / w;
        return k > 0.01f ? k : 1920f / w;   // 그래도 0 이면 1920 너비 기준
    }

    public static Color Hex(string hex)
    {
        Color c;
        ColorUtility.TryParseHtmlString(hex, out c);
        return c;
    }
}
