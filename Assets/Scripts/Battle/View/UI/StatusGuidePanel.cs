// ============================================================
// StatusGuidePanel.cs  (3-215 상태 사전)
// 위쪽 줄 [상태] 버튼 → 전투에 나오는 상태 아이콘(빙결·감전·조준·표식 …)이 무슨 뜻인지 한눈에
//
//   세 칸으로 나눔:  ① 적에게 거는 효과  ② 적의 강화  ③ 아군 효과 (직업 자원·파워)
//   아이콘 그림 = Resources/Icons/Status_이름.png (전투에서 쓰는 그림 그대로). 없으면 색 동그라미
//   배경 = 도감 배경 (Backgrounds/Compendium_BG)
//   설명 글은 StatusIcons.cs 의 말풍선 문구를 바탕으로 숫자는 GameBalance 값을 그대로 씀 (규칙이 바뀌면 같이 바뀜)
//
// 자바로 치면: 정보 묶음(Entry) 배열을 만들어 놓고, for 문으로 줄마다 [그림][이름][설명] 라벨을 찍는 화면
// ============================================================

using UnityEngine;
using UnityEngine.UI;

public class StatusGuidePanel : MonoBehaviour
{
    // 한 줄 정보 (struct = 값 묶음)
    struct Entry
    {
        public string Icon, Name, Desc, Color;
        public Entry(string icon, string name, string desc, string color) { Icon = icon; Name = name; Desc = desc; Color = color; }
    }

    public bool IsOpen { get { return gameObject.activeSelf; } }

    public static StatusGuidePanel Create(Transform parent)
    {
        RectTransform rt = UIFactory.MakeRect("StatusGuide", parent);
        UIFactory.Stretch(rt);
        StatusGuidePanel p = rt.gameObject.AddComponent<StatusGuidePanel>();
        p.Build();
        rt.gameObject.SetActive(false);
        return p;
    }

    public void Open() { gameObject.SetActive(true); transform.SetAsLastSibling(); }
    public void Hide() { gameObject.SetActive(false); }

    static int Pct(float v) { return (int)System.Math.Round(v * 100); }

    void Build()
    {
        // 배경 (도감과 같은 그림) + 어두운 막. 뒤 화면 클릭도 막음
        Image bg = UIFactory.MakeImage("Bg", transform, new Color(0.03f, 0.025f, 0.05f, 0.985f), false);
        UIFactory.Stretch(bg.rectTransform);
        Sprite bgArt = Resources.Load<Sprite>("Backgrounds/Compendium_BG");
        if (bgArt != null) { bg.sprite = bgArt; bg.color = Color.white; }
        Image shade = UIFactory.MakeImage("Shade", transform, new Color(0.02f, 0.01f, 0.04f, 0.62f), false);
        UIFactory.Stretch(shade.rectTransform);
        shade.raycastTarget = false;

        Text title = UIFactory.MakeText("Title", transform, "상태 사전", 50, UIFactory.Hex("#FFD27A"), TextAnchor.MiddleCenter);
        UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -18), new Vector2(1000, 64));
        title.font = UIFactory.TitleFont;
        title.gameObject.AddComponent<Outline>().effectColor = new Color(0.1f, 0.04f, 0f, 0.9f);
        Text sub = UIFactory.MakeText("Sub", transform, "전투 중 캐릭터·몬스터 체력바 아래 아이콘에 마우스를 올려도 설명이 나와요", 20, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleCenter);
        UIFactory.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -84), new Vector2(1400, 30));

        Entry[] debuffs =
        {
            new Entry("Burn", "발화", "턴이 시작될 때 숫자만큼 피해 (방어도 무시), 그 뒤 1 감소", "#F05A1E"),
            new Entry("Vulnerable", "취약", "받는 피해 50% 증가 (숫자 = 남은 턴)", "#AA5ADC"),
            new Entry("Weak", "약화", "주는 피해 " + Pct(GameBalance.WeakReduction) + "% 감소 (숫자 = 남은 턴)", "#96C878"),
            new Entry("Frail", "손상", "얻는 방어도 " + Pct(GameBalance.FrailReduction) + "% 감소 (숫자 = 남은 턴)", "#C8A050"),
            new Entry("Freeze", "동결", "쌓여서 정해진 수가 되면 [동상]", "#96D7FF"),
            new Entry("Frostbite", "동상", "이번 적 차례 행동 취소", "#2E6ED2"),
            new Entry("ShockStack", "감전 스택", GameBalance.ShockStacksNeeded + "개가 되면 [감전] " + GameBalance.ShockTurns + "턴", "#BEAA5A"),
            new Entry("Shock", "감전", "주는 피해 " + Pct(GameBalance.ShockReduction) + "% 감소", "#FFDC3C"),
            new Entry("Execute", "처형 스택", "스택이 남은 HP보다 많아지면 즉시 처형 (엘리트·보스는 더 필요)", "#AA5AFF"),
            new Entry("Mark", "표식", "받는 공격 피해 +숫자 (최대 10)", "#E03A3A"),
            new Entry("Brand", "낙인", "받는 발화 피해 2배", "#FF9632"),
            new Entry("BlockLock", "만년설", "다음 적 차례 동안 방어도 0 고정", "#7896BE"),
            new Entry("HalfDamage", "빙하절단", "이번 적 차례 공격 피해 -50%", "#AAC8E6"),
            new Entry("Sentence", "사형 선고", "처형되면 건 사람 HP 회복", "#963C3C"),
            new Entry("Shroud", "죽음의 표식", "이번 턴에 쓰러지면 에너지·카드·영혼 보상", "#C83C3C"),
            new Entry("DrawDown", "방해", "다음 턴 카드를 덜 뽑음 (몬스터가 우리에게 검)", "#8A8AA0"),
        };
        Entry[] enemyBuffs =
        {
            new Entry("Strength", "힘", "공격 1회당 피해 +숫자", "#E6463C"),
            new Entry("Ritual", "의식", "적 차례가 끝날 때마다 힘 +숫자", "#B04040"),
            new Entry("Rage", "분노", "동료가 쓰러질 때마다 공격력 +" + Pct(GameBalance.SquadRageBonus) + "%", "#C82828"),
            new Entry("Thorns", "가시", "공격 카드로 때리면 때린 사람이 숫자만큼 피해", "#7AA050"),
            new Entry("FreezeImmune", "빙결 면역", "동상 직후라 동결이 쌓이지 않음", "#EB5A5A"),
        };
        Entry[] allyBuffs =
        {
            new Entry("Aim", "조준", "다음 공격 카드 피해 ×(1 + 0.5 × 숫자)", "#E04040"),
            new Entry("Overdrive", "오버드라이브", "이번 턴 탄환 소모 0", "#FF8A30"),
            new Entry("Smoke", "연막", "다음 내 턴까지 받는 피해 감소", "#8A8A98"),
            new Entry("Gaze", "처형인의 시선", "표식 있는 적에게 공격 피해 +숫자", "#B02030"),
            new Entry("Ammo", "탄약 보급", "매 턴 탄환 +숫자", "#C8A040"),
            new Entry("Crimson", "진홍의 탄창", "공격 카드 2장마다 탄환 +2, 1장 뽑기", "#C03050"),
            new Entry("Analysis", "약점 분석", "표식을 걸 때마다 방어도·탄환", "#A0A060"),
            new Entry("SteelWill", "강철 의지", "턴 종료 시 방어도 +숫자", "#7090B0"),
            new Entry("ThornArmor", "가시 갑옷", "공격받으면 공격자에게 숫자만큼 피해", "#7AA050"),
            new Entry("Instinct", "전투 본능", "공격 카드로 처치하면 에너지 +1, 1장 뽑기", "#D07030"),
            new Entry("Transcend", "초월", "턴 시작에 무작위 손패 1장 에너지 0", "#9070D0"),
            new Entry("Night", "밤", "카인: 야습 2배, 윤무 4회 (죽음의 밤 = 전투 내내)", "#D74678"),
            new Entry("Charge", "반월참 차징", "다음 턴 시작에 모든 적에게 발동", "#AA78FF"),
            new Entry("Retain", "방어도 유지", "다음 턴 시작에 방어도가 사라지지 않음", "#4678C8"),
            new Entry("Sealed", "봉인", "봉인한 카드가 다음 턴에 에너지 할인되어 돌아옴", "#BE96F0"),
        };

        // 세 칸 (화면 가로를 3등분). 칸 위치 = 가운데 기준 -600 / 0 / +600
        Column("적에게 거는 효과", "#FF9A7A", debuffs, -610f);
        // 3-217: 가운데 = 아군 효과, 오른쪽 = 적의 강화 (짧은 칸을 오른쪽에 둬서 [닫기] 버튼과 안 겹침)
        Column("아군 효과 (직업 자원·파워)", "#8AD8FF", allyBuffs, 0f);
        Column("적의 강화", "#FF6A5A", enemyBuffs, 610f);

        Button back = UIFactory.MakeButton("Back", transform, "닫기", UIFactory.Hex("#3A3446"), Hide, 30);
        UIFactory.Place((RectTransform)back.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-50, 30), new Vector2(240, 80));
        UIFactory.SkinButton(back, UIFactory.GothicButton);
    }

    // 한 칸: 제목 + 줄들. 줄 = [아이콘 40][이름][설명(두 줄까지)]
    void Column(string header, string headerColor, Entry[] list, float x)
    {
        const float width = 580f, top = -130f;
        float rowH = Mathf.Min(54f, 860f / Mathf.Max(list.Length, 1));   // 줄이 많으면 줄 높이를 줄여서 화면 안에

        Image panel = UIFactory.MakeImage("Col_" + header, transform, new Color(0.06f, 0.045f, 0.09f, 0.78f), false);
        UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(x, top), new Vector2(width, 60 + rowH * list.Length + 20));
        panel.raycastTarget = false;
        panel.gameObject.AddComponent<Outline>().effectColor = UIFactory.Hex("#6A5490");

        Text h = UIFactory.MakeText("Header", panel.transform, header, 28, UIFactory.Hex(headerColor), TextAnchor.MiddleCenter);
        UIFactory.Place(h.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -8), new Vector2(width - 20, 44));
        h.font = UIFactory.TitleFont;

        for (int i = 0; i < list.Length; i++)
        {
            Entry e = list[i];
            float y = -60f - i * rowH;

            Image icon = UIFactory.MakeImage("Icon_" + e.Icon, panel.transform, UIFactory.Hex(e.Color), false);
            UIFactory.Place(icon.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16, y - (rowH - 40) / 2f), new Vector2(40, 40));
            Sprite s = Resources.Load<Sprite>("Icons/Status_" + e.Icon);
            if (s != null) { icon.sprite = s; icon.color = Color.white; icon.preserveAspect = true; }
            icon.raycastTarget = false;

            Text name = UIFactory.MakeText("Name", panel.transform, e.Name, 21, UIFactory.Hex(e.Color), TextAnchor.MiddleLeft);
            UIFactory.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(66, y), new Vector2(150, rowH));
            name.fontStyle = FontStyle.Bold;
            name.resizeTextForBestFit = true; name.verticalOverflow = VerticalWrapMode.Truncate;
            name.resizeTextMinSize = 12; name.resizeTextMaxSize = 21;
            name.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);

            Text desc = UIFactory.MakeText("Desc", panel.transform, e.Desc, 18, UIFactory.Hex("#E8E0D0"), TextAnchor.MiddleLeft);
            UIFactory.Place(desc.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(220, y), new Vector2(width - 232, rowH));
            desc.resizeTextForBestFit = true; desc.verticalOverflow = VerticalWrapMode.Truncate;
            desc.resizeTextMinSize = 12; desc.resizeTextMaxSize = 18;
        }
    }
}
