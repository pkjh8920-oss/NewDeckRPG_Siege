// ============================================================
// VfxFrames.cs  (3-186)
// 그림 여러 장을 차례로 넘기는 이펙트 (책 귀퉁이 그림 넘기기 / 스프라이트 애니메이션)
//   예) Fx_Burst_0 ~ Fx_Burst_4 : 작은 빛 → 커짐 → 터짐 → 흩어짐 → 사라짐
// 자바로 치면 Timer 로 1/Fps 초마다 ImageIcon 을 다음 그림으로 바꾸는 것과 같음
// 마지막 그림까지 보여 주면 스스로 사라짐
// ============================================================

using UnityEngine;
using UnityEngine.UI;

public class VfxFrames : MonoBehaviour
{
    public Sprite[] Frames;
    public float Fps = 18f;
    private float age;
    private Image img;

    void Start()
    {
        img = GetComponent<Image>();
        if (Frames != null && Frames.Length > 0) img.sprite = Frames[0];
    }

    void Update()
    {
        if (Frames == null || Frames.Length == 0) { Destroy(gameObject); return; }
        age += Time.deltaTime;
        int i = (int)(age * Fps);
        if (i >= Frames.Length) { Destroy(gameObject); return; }
        img.sprite = Frames[i];
    }
}
