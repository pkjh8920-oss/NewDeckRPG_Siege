// ============================================================
// RadarChart.cs
// 능력 경향 그래프 (오각형 레이더). 캐릭터 선택 화면에서 씀
//
// 2026-11-09: 예전에는 삼각형을 직접 만들어 그렸는데(OnPopulateMesh) 실제 게임에서 그래프가 안 보여서,
//   "그림(텍스처)을 코드로 한 장 그려서 RawImage 로 보여 주는" 방식으로 바꿈 → 어떤 환경에서도 보임
//   RawImage = 텍스처 한 장을 화면에 보여 주는 유니티 부품 (Image 와 비슷, 스프라이트 대신 텍스처)
//   그리는 방법: 그림의 점(픽셀)마다 "다각형 안인가? 선에 가까운가?"를 계산해서 색을 칠함
//   자바로 치면 BufferedImage 에 setRGB 로 점을 찍어 그림을 만드는 것과 같음
//
// Values: 0~1 (꼭짓점 개수 = Values 길이). 위에서부터 시계 방향
// ============================================================

using UnityEngine;
using UnityEngine.UI;

public class RadarChart : RawImage
{
    public float[] Values = new float[5];
    public Color FillColor = new Color(0.66f, 0.44f, 1f, 0.45f);
    public Color LineColor = new Color(0.66f, 0.44f, 1f, 1f);
    public Color GridColor = new Color(0.4f, 0.35f, 0.55f, 0.6f);
    public float GridWidth = 1.5f;    // 화면 기준 두께 (그래프 지름 256 기준)
    public float LineWidth = 3f;
    public int Rings = 4;

    private const int Res = 384;      // 그림 크기 (화면 지름 256 의 1.5배로 그려서 선명하게)
    private Texture2D tex;
    private Color32[] pixels;

    public void SetValues(float[] values)
    {
        Values = values;
        Redraw();
    }

    // i 번째 꼭짓점 방향 (위쪽부터 시계 방향). 화면 좌표 기준 (y 가 위로 +)
    public static Vector2 Direction(int i, int n)
    {
        float a = Mathf.PI / 2f - 2f * Mathf.PI * i / n;
        return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
    }

    // 2026-11-09 (2): 처음 버전은 그림의 모든 점마다 선 36개와의 거리를 계산해서 느렸음
    //   (에디터에서 1초 넘게 멈춤 → 싱글플레이 누르면 보라 화면이 오래 보임)
    //   → 선마다 "그 선 근처 네모 칸"만 계산하도록 바꿈. 계산량이 수십 분의 1 로 줄어듦
    private float[] gridA, lineA;
    private bool[] fillMask;

    void Redraw()
    {
        int n = Values != null ? Values.Length : 0;
        if (n < 3) return;
        if (tex == null)
        {
            tex = new Texture2D(Res, Res, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            pixels = new Color32[Res * Res];
            gridA = new float[Res * Res];
            lineA = new float[Res * Res];
            fillMask = new bool[Res * Res];
        }
        System.Array.Clear(gridA, 0, gridA.Length);
        System.Array.Clear(lineA, 0, lineA.Length);
        System.Array.Clear(fillMask, 0, fillMask.Length);

        // 그림 좌표: 가운데 (Res/2, Res/2), 반지름 Res/2 의 97% (가장자리 선이 잘리지 않게)
        float half = Res * 0.5f;
        float radius = half * 0.97f;
        float px = Res / Mathf.Max(1f, rectTransform.rect.width);   // 화면 1 = 그림 몇 픽셀
        if (float.IsInfinity(px) || px <= 0f) px = 2f;
        float gridW = GridWidth * px, lineW = LineWidth * px;
        Vector2 c = new Vector2(half, half);

        Vector2[] outer = new Vector2[n];
        Vector2[] value = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            Vector2 d = Direction(i, n);
            outer[i] = c + d * radius;
            value[i] = c + d * radius * Mathf.Clamp01(Values[i]);
        }

        // 1) 격자 선 (고리 + 가운데에서 꼭짓점으로)
        for (int ring = 1; ring <= Rings; ring++)
        {
            float k = (float)ring / Rings;
            for (int i = 0; i < n; i++)
                StampLine(gridA, c + (outer[i] - c) * k, c + (outer[(i + 1) % n] - c) * k, gridW);
        }
        for (int i = 0; i < n; i++) StampLine(gridA, c, outer[i], gridW);

        // 2) 값 다각형 채우기 (다각형을 감싸는 네모 안만 검사)
        float minX = Res, minY = Res, maxX = 0, maxY = 0;
        foreach (Vector2 v in value)
        {
            minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x);
            minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y);
        }
        int x0 = Mathf.Max(0, (int)minX), x1 = Mathf.Min(Res - 1, (int)maxX + 1);
        int y0 = Mathf.Max(0, (int)minY), y1 = Mathf.Min(Res - 1, (int)maxY + 1);
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                if (Inside(x + 0.5f, y + 0.5f, value)) fillMask[y * Res + x] = true;

        // 3) 값 테두리 + 꼭짓점 점
        for (int i = 0; i < n; i++) StampLine(lineA, value[i], value[(i + 1) % n], lineW);
        for (int i = 0; i < n; i++) StampDot(lineA, value[i], lineW * 1.3f);

        // 4) 색 합치기
        Color clear = new Color(0, 0, 0, 0);
        for (int i = 0; i < pixels.Length; i++)
        {
            Color col = clear;
            if (gridA[i] > 0f) col = Over(col, GridColor, gridA[i]);
            if (fillMask[i]) col = Over(col, FillColor, 1f);
            if (lineA[i] > 0f) col = Over(col, LineColor, lineA[i]);
            pixels[i] = col;
        }
        tex.SetPixels32(pixels);
        tex.Apply(false);
        texture = tex;
        color = Color.white;
    }

    // 선분 ab 를 buf 에 그림 (선 근처 네모 칸만 계산). 가장자리는 부드럽게 (계단 현상 방지)
    static void StampLine(float[] buf, Vector2 a, Vector2 b, float width)
    {
        float m = width * 0.5f + 1f;
        int x0 = Mathf.Max(0, (int)(Mathf.Min(a.x, b.x) - m)), x1 = Mathf.Min(Res - 1, (int)(Mathf.Max(a.x, b.x) + m));
        int y0 = Mathf.Max(0, (int)(Mathf.Min(a.y, b.y) - m)), y1 = Mathf.Min(Res - 1, (int)(Mathf.Max(a.y, b.y) + m));
        float abx = b.x - a.x, aby = b.y - a.y;
        float len2 = abx * abx + aby * aby;
        float hw = width * 0.5f + 0.5f;
        for (int y = y0; y <= y1; y++)
        {
            float py = y + 0.5f;
            for (int x = x0; x <= x1; x++)
            {
                float pxx = x + 0.5f;
                float t = len2 > 0f ? ((pxx - a.x) * abx + (py - a.y) * aby) / len2 : 0f;
                if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
                float dx = pxx - (a.x + abx * t), dy = py - (a.y + aby * t);
                float al = hw - Mathf.Sqrt(dx * dx + dy * dy);
                if (al <= 0f) continue;
                if (al > 1f) al = 1f;
                int i = y * Res + x;
                if (al > buf[i]) buf[i] = al;
            }
        }
    }

    // 동그란 점 (반지름 r)
    static void StampDot(float[] buf, Vector2 p, float r)
    {
        int x0 = Mathf.Max(0, (int)(p.x - r - 1)), x1 = Mathf.Min(Res - 1, (int)(p.x + r + 1));
        int y0 = Mathf.Max(0, (int)(p.y - r - 1)), y1 = Mathf.Min(Res - 1, (int)(p.y + r + 1));
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float dx = x + 0.5f - p.x, dy = y + 0.5f - p.y;
                float al = r + 0.5f - Mathf.Sqrt(dx * dx + dy * dy);
                if (al <= 0f) continue;
                if (al > 1f) al = 1f;
                int i = y * Res + x;
                if (al > buf[i]) buf[i] = al;
            }
    }

    // 점이 다각형 안에 있는지 (오른쪽으로 반직선을 그어 몇 번 만나는지 세기, 홀수면 안)
    static bool Inside(float x, float y, Vector2[] poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if ((poly[i].y > y) != (poly[j].y > y) &&
                x < (poly[j].x - poly[i].x) * (y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                inside = !inside;
        }
        return inside;
    }

    // 색 겹치기 (뒤 색 위에 앞 색을 alpha 만큼)
    static Color Over(Color under, Color over, float amount)
    {
        float a = over.a * amount;
        if (a <= 0f) return under;
        float outA = a + under.a * (1f - a);
        if (outA <= 0f) return new Color(0, 0, 0, 0);
        Color rgb = (over * a + under * under.a * (1f - a)) / outA;
        rgb.a = outA;
        return rgb;
    }

    protected override void OnDestroy()
    {
        if (tex != null) Destroy(tex);
        base.OnDestroy();
    }
}
