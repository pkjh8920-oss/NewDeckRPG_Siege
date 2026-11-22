// ============================================================
// ScreenTransition.cs
// 화면 넘어갈 때 소용돌이 효과 ("상태창을 누르면 게임 속으로 빨려 들어가는" 느낌)
//
//   1) 지금 화면을 그림으로 찍음 (ScreenCapture)
//   2) 그 그림을 소용돌이 셰이더(Resources/Shaders/Swirl)로 점점 세게 비틀고 어둡게 (0.7초)
//   3) 완전히 어두워진 순간 화면을 바꿈 (switchAction)
//   4) 어둠이 걷히며 새 화면이 나타남 (0.45초)
//
// 셰이더를 못 찾으면 그냥 검게 어두워졌다 밝아지는 효과로 대신함
// 쓰는 법: ScreenTransition.Play(this, () => { 화면 바꾸는 코드 });
// ============================================================

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ScreenTransition : MonoBehaviour
{
    public const float SwirlTime = 0.7f;
    public const float RevealTime = 0.45f;
    public const float MaxAngle = 9f;        // 라디안 (약 1.4바퀴)

    private static bool playing;
    private static float playingSince;   // 3-241: 언제 시작했는지 (멈춰 버렸으면 풀어 주려고)

    // 3-242: ▶ 를 다시 누르면 처음으로 (소용돌이 도중에 ■ 로 멈추면 playing 이 true 로 남아서 [싱글플레이]가 영영 안 눌렸음)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        playing = false;
        playingSince = 0f;
    }

    // host 는 예전 호출과 맞추려고 받기만 함.
    // 코루틴은 따로 만든 오브젝트에서 돌림 → 화면을 바꾸는 코드가 StopAllCoroutines 를 불러도 효과가 멈추지 않음
    public static void Play(MonoBehaviour host, System.Action switchAction)
    {
        // 이미 넘어가는 중이면 무시 (두 번 눌러도 한 번만)
        //   3-241: 단, 3초가 넘도록 안 끝났으면 중간에 멈춘 것 → 풀어 줌
        //   (예전: 효과가 중간에 멈추면 playing 이 계속 true 라서 [싱글플레이] 같은 버튼이 다시는 안 눌렸음)
        if (playing && Time.unscaledTime - playingSince < 3f) return;
        playing = true;
        playingSince = Time.unscaledTime;
        GameObject runner = new GameObject("ScreenTransitionRunner");
        ScreenTransition st = runner.AddComponent<ScreenTransition>();
        st.StartCoroutine(Run(switchAction, runner));
    }

    static IEnumerator Run(System.Action switchAction, GameObject runner)
    {
        SoundManager.Play("Transition");   // 3-252: 화면 넘어가는 소리
        // 1) 화면 찍기 (그리기가 다 끝난 프레임 끝에서)
        yield return new WaitForEndOfFrame();
        Texture2D shot = null;
        try { shot = ScreenCapture.CaptureScreenshotAsTexture(); }
        catch (System.Exception e) { Debug.LogWarning("[화면 전환] 화면 찍기 실패: " + e.Message); }
        if (shot == null)
        {
            // 3-241: 화면을 못 찍으면 효과 없이 바로 바꿈 (멈추지 않게)
            try { if (switchAction != null) switchAction(); }
            catch (System.Exception e) { Debug.LogException(e); }
            playing = false;
            Destroy(runner);
            yield break;
        }
        shot.wrapMode = TextureWrapMode.Clamp;

        // 맨 위에 덮는 전용 캔버스
        GameObject canvasGo = new GameObject("ScreenTransition");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        canvasGo.AddComponent<GraphicRaycaster>();   // 넘어가는 동안 클릭 막기

        GameObject imgGo = new GameObject("Swirl");
        imgGo.transform.SetParent(canvasGo.transform, false);
        RawImage img = imgGo.AddComponent<RawImage>();
        img.texture = shot;
        RectTransform rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Shader shader = Shader.Find("Hidden/DeckRPG/Swirl");
        Material mat = null;
        if (shader != null)
        {
            mat = new Material(shader);
            mat.SetFloat("_Aspect", (float)Screen.width / Mathf.Max(1, Screen.height));
            img.material = mat;
        }

        // 2) 소용돌이 + 어두워짐 (천천히 시작해서 빨라짐)
        float t = 0f;
        while (t < SwirlTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / SwirlTime);
            float ease = k * k;
            if (mat != null)
            {
                mat.SetFloat("_Angle", MaxAngle * ease);
                mat.SetFloat("_Ring", Mathf.Clamp01(k * 1.6f));
                mat.SetFloat("_Dark", Mathf.Clamp01((k - 0.45f) / 0.55f));
            }
            else
            {
                img.color = Color.Lerp(Color.white, Color.black, k);
            }
            rt.localScale = Vector3.one * (1f + 0.15f * ease);   // 빨려 들어가듯 살짝 확대
            yield return null;
        }

        // 3) 완전히 어두운 동안 화면 바꾸기
        if (mat != null) mat.SetFloat("_Dark", 1f);
        try
        {
            if (switchAction != null) switchAction();
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);   // 화면 바꾸다 오류가 나도 덮개는 걷히게
        }
        yield return null;

        // 4) 어둠이 걷힘 (덮개를 투명하게)
        t = 0f;
        while (t < RevealTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / RevealTime);
            Color c = img.color;
            c.a = 1f - k;
            img.color = c;
            yield return null;
        }

        Destroy(canvasGo);
        Destroy(shot);
        if (mat != null) Destroy(mat);
        playing = false;
        Destroy(runner);
    }
}
