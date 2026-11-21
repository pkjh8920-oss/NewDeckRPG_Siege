// ============================================================
// TutorialPanel.cs  (3-130)
// 튜토리얼 안내 창. 처음 보는 화면·상황에서 한 번만 뜸 (다음부터는 안 뜸)
//
//   - 글은 Core/TutorialData.cs 에 있음 (여기는 보여 주기만)
//   - "이미 봤는지" 는 PlayerPrefs 에 저장 (키: "tut_" + 아이디). 자바의 Preferences 와 비슷
//   - 안내가 여러 개 겹치면 줄을 세워 하나씩 보여 줌 (Queue)
//   - [이전] [다음] 으로 쪽 넘기기, 마지막 쪽에서 [확인]
//   - [튜토리얼 끄기] = 앞으로 안내 안 띄움. 일시정지 > 설정 에서 다시 켜거나 "처음부터 다시 보기" 가능
//   - 안내가 떠 있는 동안 뒤 화면은 클릭이 막힘 (어두운 막이 클릭을 받음)
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TutorialPanel : MonoBehaviour
{
    const string SeenPrefix = "tut_";
    const string OffKey = "tut_off";

    private RectTransform root, box;
    private Text title, body, pageText;
    private Button prevButton, nextButton, offButton;
    private Text nextLabel;

    private Queue<TutorialTip> queue = new Queue<TutorialTip>();
    private TutorialTip current;
    private int page;

    public static TutorialPanel Create(Transform parent)
    {
        RectTransform rt = UIFactory.MakeRect("Tutorial", parent);
        UIFactory.Stretch(rt);
        TutorialPanel panel = rt.gameObject.AddComponent<TutorialPanel>();
        panel.root = rt;
        panel.Build();
        rt.gameObject.SetActive(false);
        return panel;
    }

    // ---------------- 켜기·끄기 (설정에서도 씀) ----------------
    public static bool Enabled
    {
        get { return PlayerPrefs.GetInt(OffKey, 0) == 0; }
        set { PlayerPrefs.SetInt(OffKey, value ? 0 : 1); PlayerPrefs.Save(); }
    }

    // 모든 안내를 "아직 안 봄" 으로 되돌림 (처음부터 다시 보기)
    public static void ResetAll()
    {
        foreach (string id in TutorialData.AllIds()) PlayerPrefs.DeleteKey(SeenPrefix + id);
        PlayerPrefs.Save();
    }

    static bool Seen(string id) { return PlayerPrefs.GetInt(SeenPrefix + id, 0) == 1; }
    static void MarkSeen(string id) { PlayerPrefs.SetInt(SeenPrefix + id, 1); PlayerPrefs.Save(); }

    public bool IsOpen { get { return gameObject.activeSelf; } }

    // ---------------- 화면 만들기 ----------------
    void Build()
    {
        // 뒤를 살짝 어둡게 + 클릭 막기
        Image dim = UIFactory.MakeImage("Dim", root, new Color(0f, 0f, 0f, 0.45f), false);
        UIFactory.Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        Image b = UIFactory.MakeImage("Box", root, new Color(0.08f, 0.07f, 0.11f, 0.97f));
        box = b.rectTransform;
        UIFactory.Place(box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(860, 380));
        b.gameObject.AddComponent<Outline>().effectColor = UIFactory.Hex("#C8A05A");

        // 위쪽 "안내" 띠
        Text tag = UIFactory.MakeText("Tag", box, "안내", 20, UIFactory.Hex("#C8A05A"), TextAnchor.MiddleLeft);
        UIFactory.Place(tag.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28, -14), new Vector2(200, 30));

        title = UIFactory.MakeText("Title", box, "", 36, UIFactory.Hex("#FFD27A"), TextAnchor.MiddleCenter);
        UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -44), new Vector2(800, 56));
        title.fontStyle = FontStyle.Bold;

        Image line = UIFactory.MakeImage("Line", box, new Color(0.85f, 0.7f, 0.45f, 0.45f), false);
        UIFactory.Place(line.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -104), new Vector2(640, 2));
        line.raycastTarget = false;

        body = UIFactory.MakeText("Body", box, "", 25, UIFactory.Hex("#EDE4D2"), TextAnchor.MiddleCenter);
        UIFactory.Place(body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -122), new Vector2(780, 150));
        body.supportRichText = true;
        body.lineSpacing = 1.15f;
        body.resizeTextForBestFit = true;   // 3-154: 긴 안내 글도 상자 안에 (글자 25 → 최소 16)
        body.resizeTextMinSize = 16;
        body.resizeTextMaxSize = 25;
        body.verticalOverflow = VerticalWrapMode.Truncate;

        pageText = UIFactory.MakeText("Page", box, "", 18, new Color(1f, 1f, 1f, 0.5f), TextAnchor.MiddleCenter);
        UIFactory.Place(pageText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 86), new Vector2(200, 26));

        // 아래 버튼: [튜토리얼 끄기]   [이전] [다음/확인]
        offButton = UIFactory.MakeButton("Off", box, "튜토리얼 끄기", UIFactory.Hex("#3A3446"), TurnOff, 20);
        UIFactory.Place((RectTransform)offButton.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24, 22), new Vector2(180, 52));
        UIFactory.SkinButton(offButton, UIFactory.GothicButton);   // 3-181: 8번 공용 버튼 틀 (그림 없던 버튼)
        prevButton = UIFactory.MakeButton("Prev", box, "이전", UIFactory.Hex("#3A3F4C"), Prev, 24);
        UIFactory.Place((RectTransform)prevButton.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-224, 22), new Vector2(150, 56));
        UIFactory.SkinButton(prevButton, UIFactory.GothicButton);   // 3-181: 8번 공용 버튼 틀 (그림 없던 버튼)
        nextButton = UIFactory.MakeButton("Next", box, "다음", UIFactory.Hex("#3A4A2C"), Next, 26);
        UIFactory.Place((RectTransform)nextButton.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24, 22), new Vector2(190, 56));
        UIFactory.SkinButton(nextButton, UIFactory.GothicButton);   // 3-181: 8번 공용 버튼 틀 (그림 없던 버튼)
        nextLabel = nextButton.GetComponentInChildren<Text>();
    }

    // ---------------- 보여 주기 ----------------
    // 이 안내를 아직 안 봤으면 줄에 넣고 띄움. 이미 봤거나 꺼져 있으면 아무것도 안 함
    public void Show(string id)
    {
        if (!Enabled || Seen(id)) return;
        TutorialTip tip = TutorialData.Find(id);
        if (tip == null || tip.Pages == null || tip.Pages.Length == 0) return;
        if (current != null && current.Id == id) return;
        foreach (TutorialTip q in queue) if (q.Id == id) return;   // 이미 줄에 있음
        queue.Enqueue(tip);
        if (current == null) ShowNextTip();
        else transform.SetAsLastSibling();   // 다른 창이 위로 올라왔어도 안내가 맨 위
    }

    void ShowNextTip()
    {
        if (queue.Count == 0)
        {
            current = null;
            gameObject.SetActive(false);
            return;
        }
        current = queue.Dequeue();
        page = 0;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        DrawPage();
    }

    void DrawPage()
    {
        title.text = current.Title;
        body.text = current.Pages[page];
        int n = current.Pages.Length;
        pageText.text = n > 1 ? (page + 1) + " / " + n : "";
        prevButton.gameObject.SetActive(page > 0);
        nextLabel.text = page < n - 1 ? "다음" : "확인";
    }

    void Prev()
    {
        if (current == null || page == 0) return;
        page--;
        DrawPage();
    }

    void Next()
    {
        if (current == null) return;
        if (page < current.Pages.Length - 1) { page++; DrawPage(); return; }
        MarkSeen(current.Id);   // 마지막 쪽까지 보면 "봤음"
        ShowNextTip();
    }

    // 앞으로 안내를 안 띄움 (지금 줄에 있는 것도 닫음)
    void TurnOff()
    {
        Enabled = false;
        queue.Clear();
        current = null;
        gameObject.SetActive(false);
    }
}
