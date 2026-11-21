// ============================================================
// VfxPart.cs
// 이펙트 조각 하나 (Vfx.cs 가 만들어서 붙임)
// 유니티는 MonoBehaviour 클래스 이름과 파일 이름이 같아야 해서 파일을 나눴습니다.
// ============================================================

using UnityEngine;
using UnityEngine.UI;

// 이펙트 조각 하나: 속도·중력으로 움직이고, 크기와 투명도가 시간에 따라 바뀜
public class VfxPart : MonoBehaviour
{
    public Vector2 Velocity;
    public float Gravity;            // 아래로 당기는 힘 (음수면 위로)
    public float Life = 0.5f;        // 전체 시간
    public Vector2 StartSize;
    public Vector2 MidSize;          // 중간(MidTime 시점) 크기
    public Vector2 EndSize;
    public float MidTime = 0.3f;     // 0~1, 전체 시간 중 어디가 "중간"인지
    public float FadeFrom = 0.5f;    // 0~1, 이 시점부터 투명해지기 시작
    public float Spin;               // 3-184: 초당 회전 각도 (마법진이 돌 때)
    public float FadeIn;             // 3-184: 0~1, 처음 이 시점까지 투명 → 진하게 (그림 이펙트가 툭 나타나지 않게)
    public float Delay;              // 3-184: 이 시간(초) 동안은 안 보이다가 시작 (번개가 마법진 뒤에 떨어지게)

    private float age;
    private RectTransform rt;
    private Graphic graphic;
    private float startAlpha;

    void Start()
    {
        rt = (RectTransform)transform;
        graphic = GetComponent<Graphic>();
        startAlpha = graphic.color.a;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (Delay > 0f)
        {
            Delay -= dt;
            Color hc = graphic.color; hc.a = 0f; graphic.color = hc;
            return;
        }
        age += dt;
        float t = Mathf.Clamp01(age / Life);

        Velocity.y -= Gravity * dt;
        rt.anchoredPosition += Velocity * dt;

        rt.sizeDelta = (t < MidTime)
            ? Vector2.Lerp(StartSize, MidSize, t / MidTime)
            : Vector2.Lerp(MidSize, EndSize, (t - MidTime) / (1f - MidTime));

        Color c = graphic.color;
        c.a = (t < FadeFrom) ? startAlpha : startAlpha * (1f - (t - FadeFrom) / (1f - FadeFrom));
        if (FadeIn > 0f && t < FadeIn) c.a *= t / FadeIn;
        graphic.color = c;
        if (Spin != 0f) rt.localEulerAngles += new Vector3(0, 0, Spin * dt);

        if (age >= Life) Destroy(gameObject);
    }
}

