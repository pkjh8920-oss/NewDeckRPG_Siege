// ============================================================
// WebQuit.cs  (3-204 웹에서 [종료] 누르면 탭 닫기)
//
// 웹(브라우저)에서는 Application.Quit() 을 부르면 창은 안 닫히고 게임만 멈춤(먹통)
//   → 대신 브라우저 쪽 자바스크립트(WebQuit.jslib)를 불러서
//     ① window.close() 로 탭 닫기 시도
//     ② 브라우저가 막으면 (주소창에 직접 들어온 탭은 보통 막음) "게임을 종료했습니다" 화면으로 바꿈
//
// 자바로 치면: native 메서드 선언과 비슷함
//   [DllImport("__Internal")] = "이 함수 몸통은 C# 이 아니라 웹 빌드에 같이 들어간 자바스크립트에 있다"
// 에디터·Windows 빌드에서는 이 코드가 아예 빠짐 (#if UNITY_WEBGL && !UNITY_EDITOR)
// ============================================================

#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

public static class WebQuit
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void WebQuitGame();
#endif

    // 웹이면 탭 닫기(또는 종료 화면), 아니면 아무것도 안 함 (true = 웹에서 처리했음)
    public static bool TryQuit()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        WebQuitGame();
        return true;
#else
        return false;
#endif
    }
}
