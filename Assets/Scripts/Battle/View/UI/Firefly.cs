// ============================================================
// Firefly.cs
// 배경에 떠다니는 반딧불 (BattleScreen 이 만들어서 붙임)
// ============================================================

using UnityEngine;
using UnityEngine.UI;

// 배경에 떠다니는 반딧불: 천천히 흔들리며 올라가고, 깜빡거림
public class Firefly : MonoBehaviour
{
    private RectTransform rt;
    private Image img;
    private Vector2 basePos;
    private float phase, speed, rise, swing;

    void Start()
    {
        rt = (RectTransform)transform;
        img = GetComponent<Image>();
        Respawn(Random.Range(300f, 1000f));
    }

    void Respawn(float y)
    {
        basePos = new Vector2(Random.Range(0f, 1920f), y);
        phase = Random.Range(0f, 10f);
        speed = Random.Range(0.6f, 1.6f);
        rise = Random.Range(8f, 25f);
        swing = Random.Range(20f, 60f);
    }

    void Update()
    {
        basePos.y += rise * Time.deltaTime;
        if (basePos.y > 1020f) Respawn(300f);
        float t = Time.time * speed + phase;
        rt.anchoredPosition = basePos + new Vector2(Mathf.Sin(t) * swing, Mathf.Sin(t * 1.7f) * 12f);
        Color c = img.color;
        float blink = Mathf.Sin(t * 2.3f);
        c.a = 0.05f + 0.55f * blink * blink * blink * blink;   // 대부분 어둡다가 가끔 반짝
        img.color = c;
    }
}
