// ============================================================
// LevelUpPanel.cs
// 레벨 업 화면 (2026-11-15, 3-98 새 디자인)
//   레벨이 오른 사람마다 차례로: 남은 스탯 포인트를 힘 / 체력 / 민첩(마도사는 마나)에 나눠 올림
//   3-98: [+] 버튼 대신 "카드를 누르면" 그 스탯이 1 오름. [다시] = 이 화면을 열었을 때로 되돌림. 포인트를 다 쓰면 [확정]
//   배경 그림: Resources/UI/LevelUpPanel.png (제목·카드 틀·버튼 틀만 그려져 있고 글자는 코드가 씀)
//     그림이 없으면 예전처럼 어두운 상자 위에 같은 자리로 그림
//   스탯 규칙 (GameBalance): 힘 2 = 공격 피해 +1 (3-160) / 체력 1 = 최대 HP +4 (마법 직업 +2)
//     민첩: 기본 = 크리티컬 +2.5%, 관통 +3% / 스나이퍼 = 탄환 / 마도사(AP) = "마나" (시작 마나·턴 회복)
// 자바와 비교: System.Action onDone = 다 끝나면 부를 콜백 (Runnable)
//   글자 상자들은 처음 한 번만 만들고, 바뀔 때는 .text 만 바꿈 (Swing 에서 JLabel.setText 하는 것과 같음)
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LevelUpPanel : MonoBehaviour
{
    // 그림 원본 크기 (픽셀). 아래 좌표들은 전부 이 그림 기준 (왼쪽 위 = 0,0)
    const float ArtW = 1024f, ArtH = 765f;
    const float ScreenFill = 0.94f;      // 화면의 몇 %까지 채울지

    private RectTransform root;          // 전체 화면 (어두운 막)
    private RectTransform board;         // 그림 크기(1024×765)의 판. 화면에 맞춰 통째로 확대·축소
    private Text whoText, pointsText;
    private Text[] nameText = new Text[3], valueText = new Text[3], effectText = new Text[3], perText = new Text[3];
    private Button[] cardButton = new Button[3];
    private Button resetButton, confirmButton;
    private Text resetLabel, confirmLabel;

    private List<PlayerCombatant> queue = new List<PlayerCombatant>();
    private PlayerCombatant current;
    private System.Action onDone;
    // 되돌리기용: 화면을 열었을 때 값
    private int sStr, sCon, sAgi, sMaxHp, sHp, sPoints;

    public bool IsOpen { get { return gameObject.activeSelf; } }

    // ---- 카드 3장의 자리 (그림 기준 가운데 x) 와 색 ----
    static readonly float[] CardX = { 202f, 515f, 832f };
    static readonly Vector2[] CardSize = { new Vector2(220, 340), new Vector2(220, 340), new Vector2(228, 342) };
    static readonly string[] NameColor = { "#E35A4C", "#86C68E", "#86BDEB" };
    static readonly string[] PerColor = { "#E09488", "#93CC9F", "#C9D6E6" };

    public static LevelUpPanel Create(Transform parent)
    {
        RectTransform rt = UIFactory.MakeRect("LevelUp", parent);
        UIFactory.Stretch(rt);
        LevelUpPanel panel = rt.gameObject.AddComponent<LevelUpPanel>();
        panel.root = rt;
        Image dim = UIFactory.MakeImage("Dim", rt, new Color(0.02f, 0.015f, 0.04f, 0.9f), false);
        UIFactory.Stretch(dim.rectTransform);
        panel.Build();
        rt.gameObject.SetActive(false);
        return panel;
    }

    // 그림 좌표(왼쪽 위 기준, 가운데 점) → 판 위의 자리
    //   자바 Swing 처럼 위에서 아래로 y 가 커지는 좌표를, 유니티의 "가운데 기준·위로 갈수록 +" 좌표로 바꿈
    void PlaceArt(RectTransform rt, float cx, float cy, float w, float h)
    {
        UIFactory.Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(cx - ArtW / 2f, ArtH / 2f - cy), new Vector2(w, h));
    }

    // 판 위에 글자 하나 (판이 확대되므로 2배로 그려 둠 → 흐려지지 않음)
    Text ArtText(string name, float cx, float cy, float w, float h, int size, string color, TextAnchor align, bool bold = true)
    {
        Text t = UIFactory.MakeText(name, board, "", size, UIFactory.Hex(color), align);
        PlaceArt(t.rectTransform, cx, cy, w, h);
        if (bold) t.fontStyle = FontStyle.Bold;
        UIFactory.Supersample(t, 2f);
        return t;
    }

    void Build()
    {
        board = UIFactory.MakeRect("Board", root);
        UIFactory.Place(board, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(ArtW, ArtH));
        Sprite art = Resources.Load<Sprite>("UI/LevelUpPanel");
        Image bg = UIFactory.MakeImage("Art", board, art != null ? Color.white : new Color(0.08f, 0.07f, 0.12f, 0.98f), art == null);
        UIFactory.Stretch(bg.rectTransform);
        if (art != null) bg.sprite = art;
        else
        {
            // 그림이 없을 때: 제목도 코드로 씀
            Text title = ArtText("Title", 512, 62, 700, 80, 56, "#E8C98A", TextAnchor.MiddleCenter);
            title.text = "레벨 업!";
        }

        whoText = ArtText("Who", 512, 137, 760, 38, 24, "#EDE4D2", TextAnchor.MiddleCenter);
        pointsText = ArtText("Points", 512, 186, 300, 36, 26, "#9BD39B", TextAnchor.MiddleCenter);

        for (int i = 0; i < 3; i++)
        {
            int which = i;   // 람다가 붙잡을 값 (자바의 effectively final 변수와 같음)

            // 카드 전체가 버튼. 평소엔 투명, 마우스 올리면 살짝 밝게, 못 누르면 어둡게
            Image hit = UIFactory.MakeImage("Card" + i, board, Color.white);
            PlaceArt(hit.rectTransform, CardX[i], 420, CardSize[i].x, CardSize[i].y);
            Button b = hit.gameObject.AddComponent<Button>();
            b.targetGraphic = hit;
            ColorBlock cb = b.colors;
            cb.normalColor = new Color(1f, 1f, 1f, 0f);
            cb.highlightedColor = new Color(1f, 0.92f, 0.75f, 0.09f);
            cb.selectedColor = new Color(1f, 1f, 1f, 0f);
            cb.pressedColor = new Color(1f, 0.95f, 0.8f, 0.18f);
            cb.disabledColor = new Color(0f, 0f, 0f, 0.38f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.08f;
            b.colors = cb;
            b.onClick.AddListener(() => OnCard(which));
            cardButton[i] = b;

            nameText[i] = ArtText("Name" + i, CardX[i], 383, 210, 60, 44, NameColor[i], TextAnchor.MiddleCenter);
            // (44 크기 이상은 UIFactory 가 제목 폰트 + 검은 테두리를 자동으로 붙임)
            valueText[i] = ArtText("Value" + i, CardX[i], 432, 220, 48, 36, "#EBD7A8", TextAnchor.MiddleCenter);
            valueText[i].horizontalOverflow = HorizontalWrapMode.Overflow;   // "+3" 이 붙어도 줄바꿈 안 함
            effectText[i] = ArtText("Effect" + i, CardX[i], 480, 224, 32, 21, "#F0E6D2", TextAnchor.MiddleCenter);
            // 1당 설명: 길면 두 줄로 (스나이퍼·마도사)
            perText[i] = ArtText("Per" + i, CardX[i], 518, 222, 40, 14, PerColor[i], TextAnchor.UpperCenter, false);
        }

        resetButton = ArtButton("Reset", 348, 688, 246, 76, ResetPoints, out resetLabel, 30);
        resetLabel.text = "다시";   // 3-104: 글자를 안 넣어서 빈 버튼으로 보였음
        confirmButton = ArtButton("Confirm", 676, 688, 316, 80, Confirm, out confirmLabel, 28);
    }

    // 그림 속 버튼 틀 위에 투명한 버튼 + 글자
    Button ArtButton(string name, float cx, float cy, float w, float h, UnityEngine.Events.UnityAction click, out Text label, int size)
    {
        Image hit = UIFactory.MakeImage(name, board, Color.white);
        PlaceArt(hit.rectTransform, cx, cy, w - 16, h - 14);
        Button b = hit.gameObject.AddComponent<Button>();
        b.targetGraphic = hit;
        ColorBlock cb = b.colors;
        cb.normalColor = new Color(1f, 1f, 1f, 0f);
        cb.highlightedColor = new Color(1f, 0.92f, 0.75f, 0.06f);
        cb.selectedColor = new Color(1f, 1f, 1f, 0f);
        cb.pressedColor = new Color(0f, 0f, 0f, 0.25f);
        cb.disabledColor = new Color(0f, 0f, 0f, 0.2f);
        cb.colorMultiplier = 1f;
        b.colors = cb;
        b.onClick.AddListener(click);
        label = ArtText(name + "Label", cx, cy, w, h, size, "#EDE4D2", TextAnchor.MiddleCenter);
        return b;
    }

    // 판을 화면 크기에 맞춤 (창 크기가 바뀌어도 따라감)
    void LateUpdate()
    {
        if (board == null || root == null) return;
        Rect r = root.rect;
        if (r.width <= 0 || r.height <= 0) return;
        float s = Mathf.Min(r.width / ArtW, r.height / ArtH) * ScreenFill;
        board.localScale = new Vector3(s, s, 1f);
    }

    // 스탯 포인트가 남은 사람들을 차례로 보여 줌. 아무도 없으면 바로 onDone
    public void Open(List<PlayerCombatant> players, System.Action done)
    {
        queue.Clear();
        foreach (PlayerCombatant p in players)
        {
            RunState.DiscardUnusablePoints(p);
            if (p.StatPoints > 0) queue.Add(p);
        }
        onDone = done;
        if (queue.Count == 0) { Finish(); return; }
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        LateUpdate();
        Next();
    }

    void Next()
    {
        current = queue[0];
        queue.RemoveAt(0);
        sStr = current.Str; sCon = current.Con; sAgi = current.Agi;
        sMaxHp = current.MaxHp; sHp = current.Hp; sPoints = current.StatPoints;
        Redraw();
        // 3-213: 레벨 업 이펙트는 스탯 창이 아니라 전투 화면의 캐릭터 몸에서 먼저 나옴 (BattleScreen.LevelUpRoutine)
    }

    void Finish()
    {
        gameObject.SetActive(false);
        System.Action d = onDone;
        onDone = null;
        if (d != null) d();
    }

    // 카드를 누름 = 그 스탯 +1 (규칙은 RunState.SpendStat 이 검사)
    void OnCard(int which)
    {
        if (current == null) return;
        if (!RunState.SpendStat(current, which)) return;
        Redraw();
        StartCoroutine(Pop(valueText[which].rectTransform));
        StartCoroutine(Pop(nameText[which].rectTransform));
    }

    // 숫자가 "톡" 커졌다가 돌아오는 연출 (0.2초)
    IEnumerator Pop(RectTransform rt)
    {
        Vector3 baseScale = new Vector3(0.5f, 0.5f, 1f);   // Supersample 로 1/2 크기가 기본
        float t = 0f;
        const float dur = 0.2f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = 1f + 0.28f * Mathf.Sin(Mathf.Clamp01(t / dur) * Mathf.PI);
            rt.localScale = baseScale * k;
            yield return null;
        }
        rt.localScale = baseScale;
    }

    void Redraw()
    {
        PlayerCombatant p = current;

        whoText.text = p.Name + " " + ClassLibrary.ColoredClassName(p.Class) + "   Lv " + p.Level
            + (p.Level < GameBalance.MaxLevel ? "   (다음 레벨까지 경험치 " + (p.ExpToNext - p.Exp) + ")" : "   (최대 레벨)");
        pointsText.text = "남은 스탯 포인트 " + p.StatPoints;
        pointsText.color = p.StatPoints > 0 ? UIFactory.Hex("#9BD39B") : new Color(1f, 1f, 1f, 0.55f);

        int hpPer = p.HpPerCon;   // 3-147
        SetCard(0, "힘", p.Str, sStr, "공격 피해 +" + DamageCalculator.StrDamageBonus(p.Str), GameBalance.StrStep + "당 공격 피해 +" + GameBalance.AdPerStr);   // 3-133
        SetCard(1, "체력", p.Con, sCon, "최대 HP " + p.MaxHp, "1당 최대 HP +" + hpPer);
        if (p.IsAP)
        {
            // 마도사(AP): 3번째 스탯 = 마나 (시작 마나 = 2 + 마나/4, 턴당 회복 = 1 + 마나/5)
            SetCard(2, "마나", p.Agi, sAgi, "시작 마나 " + p.StartMana() + " · 턴당 +" + p.ManaRegen(),
                GameBalance.ApStartManaStep + "당 시작 마나 +1, " + GameBalance.ApRegenStep + "당 턴 회복 +1");
        }
        else if (p.Class == PlayerClass.Sniper && !p.AgiGivesCritPen)
        {
            // 스나이퍼 (3-80): 민첩 = 탄환만 (크리티컬·관통 없음)
            //   최대 탄환은 p.MaxMana 를 그대로 보여 줌 → 레드 위스퍼(2배)·무기 핵 강화도 같이 반영됨
            SetCard(2, "민첩", p.Agi, sAgi, "최대 탄환 " + p.MaxMana + " · 턴당 +" + p.ManaRegen(),
                GameBalance.SniperAgiStep + "당 최대 탄환 +" + GameBalance.SniperMaxPerStep + ", "
                + GameBalance.SniperAgiStep + "당 턴 충전 +" + GameBalance.SniperRegenPerStep);
        }
        else
        {
            SetCard(2, "민첩", p.Agi, sAgi, "크리티컬 " + Mathf.RoundToInt(p.Agi * GameBalance.CritPerAgi * 100) + "%",
                "1당 크리티컬 +" + (GameBalance.CritPerAgi * 100).ToString("0.#") + "%, 관통 +" + Mathf.RoundToInt(GameBalance.PenetrationPerAgi * 100) + "%");
        }

        bool canReset = p.StatPoints != sPoints;
        resetButton.interactable = canReset;
        resetLabel.color = canReset ? UIFactory.Hex("#EDE4D2") : new Color(0.93f, 0.89f, 0.82f, 0.4f);

        bool canConfirm = p.StatPoints == 0 || (p.Str >= GameBalance.StatMax && p.Con >= GameBalance.StatMax && p.Agi >= GameBalance.StatMax);
        confirmButton.interactable = canConfirm;
        confirmLabel.text = canConfirm ? "확정" : "포인트를 모두 쓰세요";
        confirmLabel.color = canConfirm ? UIFactory.Hex("#FFD27A") : new Color(0.93f, 0.89f, 0.82f, 0.75f);
    }

    void SetCard(int i, string label, int value, int before, string effect, string perPoint)
    {
        nameText[i].text = label;
        valueText[i].text = value + " / " + GameBalance.StatMax
            + (value > before ? "  <color=#9CFF9C>+" + (value - before) + "</color>" : "");
        effectText[i].text = effect;
        perText[i].text = perPoint;
        // 포인트가 남았고 최대치가 아니면 누를 수 있음
        cardButton[i].interactable = current.StatPoints > 0 && value < GameBalance.StatMax;
    }

    void ResetPoints()
    {
        PlayerCombatant p = current;
        p.Str = sStr; p.Con = sCon; p.Agi = sAgi; p.MaxHp = sMaxHp; p.Hp = sHp; p.StatPoints = sPoints;
        Redraw();
    }

    void Confirm()
    {
        RunState.DiscardUnusablePoints(current);
        if (queue.Count > 0) Next();
        else Finish();
    }
}
