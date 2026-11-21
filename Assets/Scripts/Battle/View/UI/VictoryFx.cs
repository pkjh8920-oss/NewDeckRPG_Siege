// ============================================================
// VictoryFx.cs  (3-182 연출 3단계: 승리 연출)
// 승리 창(VictoryFrame)에 붙어서, 창이 켜질 때마다 한 번 재생
//
//   0 ~ 0.45초 : 창이 작게(0.6배) 투명하게 시작 → 살짝 크게(1.06배) 튀었다가 → 원래 크기로 (탄력 있게 등장)
//   계속        : 창 뒤에서 금빛 광선(빛줄기 16개)이 천천히 돌고, 숨쉬듯 밝아졌다 어두워짐
//   처음 2초    : 창 주변에서 금빛 반짝이가 계속 피어오름
//   제목 글자   : 창이 다 커진 뒤 금빛 테두리가 한 번 번쩍
//
// 광선 그림은 파일 없이 코드로 한 번 만들어 둠 (Texture2D 에 점을 하나하나 칠함)
//   자바로 치면 BufferedImage 에 setRGB 로 그림을 그려서 쓰는 것과 같음
// 시간은 unscaledDeltaTime (히트스톱이 마지막 타격에 걸려 있어도 연출은 정상 속도)
// ============================================================

using UnityEngine;
using UnityEngine.UI;

public class VictoryFx : MonoBehaviour
{
    public Image Rays;          // 창 뒤 광선 (BattleScreen 이 만들어서 넣어 줌)
    public Text Title;          // "승리! (N턴)" 글자 (번쩍임용)
    public Transform SparkLayer;

    private float age;
    private float sparkTimer;
    private Outline titleGlow;

    void OnEnable()
    {
        age = 0f;
        sparkTimer = 0f;
        transform.localScale = Vector3.one * 0.6f;
        if (Rays != null) Rays.gameObject.SetActive(true);
        if (Title != null && titleGlow == null)
        {
            titleGlow = Title.gameObject.AddComponent<Outline>();
            titleGlow.effectDistance = new Vector2(3, -3);
        }
        if (titleGlow != null) titleGlow.effectColor = new Color(1f, 0.85f, 0.3f, 0f);
    }

    void OnDisable()
    {
        if (Rays != null) Rays.gameObject.SetActive(false);
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        age += dt;
        float intro = GameBalance.FxVictoryIntro;

        // 1) 탄력 있게 등장: 0.6 → 1.06 (70% 지점) → 1.0
        float t = Mathf.Clamp01(age / intro);
        float s;
        if (t < 0.7f) s = Mathf.Lerp(0.6f, 1.06f, EaseOut(t / 0.7f));
        else s = Mathf.Lerp(1.06f, 1f, (t - 0.7f) / 0.3f);
        transform.localScale = new Vector3(s, s, 1f);
        CanvasGroup g = GetComponent<CanvasGroup>();
        if (g == null) g = gameObject.AddComponent<CanvasGroup>();
        g.alpha = Mathf.Clamp01(age / (intro * 0.5f));

        // 2) 광선: 천천히 돌고 숨쉬기. 처음엔 크게 퍼지며 나타남
        if (Rays != null)
        {
            RectTransform r = Rays.rectTransform;
            r.localEulerAngles = new Vector3(0, 0, -age * 12f);
            float grow = Mathf.Lerp(0.3f, 1f, EaseOut(Mathf.Clamp01(age / 0.6f)));
            r.localScale = new Vector3(grow, grow, 1f);
            float a = Mathf.Clamp01(age / 0.4f) * (0.55f + 0.15f * Mathf.Sin(age * 2.5f));
            Rays.color = new Color(1f, 0.82f, 0.38f, a);
        }

        // 3) 반짝이: 처음 2초 동안 0.12초마다 창 둘레에서 피어오름
        if (SparkLayer != null && age < 2.0f)
        {
            sparkTimer -= dt;
            if (sparkTimer <= 0f)
            {
                sparkTimer = 0.12f;
                RectTransform me = (RectTransform)transform;
                Vector2 half = me.rect.size * 0.5f;
                Vector2 basePos = me.anchoredPosition;
                Vector2 pos = basePos + new Vector2(Random.Range(-half.x, half.x), Random.Range(-half.y * 0.6f, half.y));
                Vfx.Rise(SparkLayer, pos, new Color(1f, 0.86f, 0.42f, 1f), 3);
            }
        }

        // 4) 제목 금빛 번쩍임: 등장이 끝난 직후 0.5초 동안 밝아졌다 사라짐
        if (titleGlow != null)
        {
            float k = (age - intro) / 0.5f;
            float ga = k < 0f || k > 1f ? 0f : Mathf.Sin(k * Mathf.PI);
            titleGlow.effectColor = new Color(1f, 0.85f, 0.3f, ga * 0.9f);
        }
    }

    static float EaseOut(float x) { return 1f - (1f - x) * (1f - x) * (1f - x); }

    // ---------------- 광선 그림 (코드로 한 번 만듦) ----------------
    private static Sprite raySprite;
    public static Sprite RaySprite
    {
        get
        {
            if (raySprite != null) return raySprite;
            int size = 256;
            int rays = 16;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            float c = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / c;          // 0(가운데) ~ 1(가장자리)
                    float ang = Mathf.Atan2(dy, dx);                       // -π ~ π
                    float wave = Mathf.Cos(ang * rays);                    // 빛줄기 16개: +1 인 곳이 줄기 가운데
                    float ray = Mathf.Clamp01((wave - 0.55f) / 0.45f);     // 줄기만 남기고 부드러운 가장자리
                    float fall = Mathf.Clamp01(1f - d);                    // 바깥으로 갈수록 투명
                    float core = Mathf.Clamp01(1f - d * 2.2f) * 0.6f;      // 가운데 둥근 빛
                    float a = Mathf.Clamp01(ray * fall * fall + core * core);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            raySprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return raySprite;
        }
    }
}
