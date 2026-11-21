// ============================================================
// FloatingText.cs
// 피해·회복·방어도 숫자가 "펑" 하고 크게 튀어나왔다가, 위로 떠오르면서 사라지는 효과
//   0 ~ 0.15초 : 1.8배 크기에서 1배로 줄어듦 (튀어나오는 느낌)
//   그 뒤       : 점점 느려지면서 위로 올라가고, 끝에서 투명해짐
// ============================================================

using UnityEngine;
using UnityEngine.UI;

public class FloatingText : MonoBehaviour
{
    public float PopScale = 1.8f;   // 처음 크기 (큰 피해일수록 BattleScreen 이 더 크게 줌)

    private const float LifeTime = 1.2f;
    private float age;
    private Text text;
    private RectTransform rt;
    private float drift;            // 옆으로 살짝 흘러가는 속도

    void Awake()
    {
        text = GetComponent<Text>();
        rt = (RectTransform)transform;
        drift = Random.Range(-40f, 40f);
    }

    void Update()
    {
        age += Time.deltaTime;

        // 크기: 처음 0.15초 동안 PopScale → 1
        float popT = Mathf.Clamp01(age / 0.15f);
        float s = Mathf.Lerp(PopScale, 1f, popT * popT);
        rt.localScale = new Vector3(s, s, 1f);

        // 위로 올라가는 속도는 시간이 지날수록 느려짐
        float speed = Mathf.Lerp(160f, 20f, age / LifeTime);
        rt.anchoredPosition += new Vector2(drift * Time.deltaTime, speed * Time.deltaTime);

        // 마지막 0.4초 동안 투명해짐
        Color c = text.color;
        c.a = Mathf.Clamp01((LifeTime - age) / 0.4f);
        text.color = c;
        if (age >= LifeTime) Destroy(gameObject);
    }
}
