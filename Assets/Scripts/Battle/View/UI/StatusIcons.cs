// ============================================================
// StatusIcons.cs
// 캐릭터·몬스터 체력바 아래에 붙는 "상태 아이콘 줄"
//   ❄ 동결 2/5   🔥 발화 7   ⚡ 감전   ☠ 처형 12   …  (그림 + 오른쪽 아래 숫자)
//   아이콘에 마우스를 올리면 설명이 뜸 (BattleScreen 의 말풍선)
//
// 그림 파일: Resources/Icons/Status_이름.png  (예: Status_Freeze.png)
//   그림을 바꾸고 싶으면 같은 이름으로 덮어쓰면 됨. 파일이 없으면 색 동그라미 + 글자로 대신 그림
// 이 파일은 "무엇을 보여 줄지" 정하는 곳 + 그리는 곳. 규칙 계산은 안 함
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 아이콘 하나에 필요한 정보 (struct = 값 묶음. 자바의 record 와 비슷)
public struct StatusBadge
{
    public string Icon;    // 그림 이름 (Status_ 뒤)
    public string Count;   // 오른쪽 아래 숫자 (빈 글자면 숫자 없음)
    public string Tip;     // 마우스를 올리면 나오는 설명
    public Color Tint;     // 그림이 없을 때 동그라미 색

    public StatusBadge(string icon, string count, string tip, Color tint)
    {
        Icon = icon;
        Count = count;
        Tip = tip;
        Tint = tint;
    }
}

public static class StatusIcons
{
    public const float Size = 38f;   // 아이콘 한 칸 크기
    public const float Gap = 4f;

    static Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    static Sprite Load(string icon)
    {
        Sprite s;
        if (cache.TryGetValue(icon, out s)) return s;
        s = Resources.Load<Sprite>("Icons/Status_" + icon);
        cache[icon] = s;
        return s;
    }

    // ---------------- 공통 상태 (취약·약화·발화·감전·동상) ----------------
    static void AddCommon(List<StatusBadge> list, Combatant u)
    {
        int v;
        v = u.GetStatus(StatusType.Burn);
        if (v > 0) list.Add(new StatusBadge("Burn", v.ToString(), "발화 " + v + "\n턴이 시작될 때 " + v + " 피해 (방어도 무시), 그 뒤 1 감소", UIFactory.Hex("#F05A1E")));
        v = u.GetStatus(StatusType.Vulnerable);
        if (v > 0) list.Add(new StatusBadge("Vulnerable", v.ToString(), "취약 " + v + "턴\n받는 피해 50% 증가", UIFactory.Hex("#AA5ADC")));
        v = u.GetStatus(StatusType.Weak);
        if (v > 0) list.Add(new StatusBadge("Weak", v.ToString(), "약화 " + v + "턴\n주는 피해 " + (int)(GameBalance.WeakReduction * 100) + "% 감소", UIFactory.Hex("#96C878")));
        v = u.GetStatus(StatusType.Shock);
        if (v > 0) list.Add(new StatusBadge("Shock", v.ToString(), "감전 " + v + "턴\n주는 피해 " + (int)(GameBalance.ShockReduction * 100) + "% 감소", UIFactory.Hex("#FFDC3C")));
        if (u.HasStatus(StatusType.Frostbite)) list.Add(new StatusBadge("Frostbite", "", "동상\n이번 적 차례 행동 취소", UIFactory.Hex("#2E6ED2")));
        v = u.GetStatus(StatusType.Frail);
        if (v > 0) list.Add(new StatusBadge("Frail", v.ToString(), "손상 " + v + "턴\n얻는 방어도 " + (int)(GameBalance.FrailReduction * 100) + "% 감소", UIFactory.Hex("#C8A050")));
        PlayerCombatant pc = u as PlayerCombatant;
        if (pc != null && pc.DrawPenalty > 0) list.Add(new StatusBadge("DrawDown", "-" + pc.DrawPenalty, "방해\n다음 턴 카드 " + pc.DrawPenalty + "장 덜 뽑음", UIFactory.Hex("#8A8AA0")));
        // ---- 스나이퍼·공용 카드 상태 (2026-11-15) ----
        if (pc != null)
        {
            if (pc.AimStacks > 0) list.Add(new StatusBadge("Aim", pc.AimStacks.ToString(), "조준 " + pc.AimStacks + "\n다음 공격 카드 피해 ×" + (1f + 0.5f * pc.AimStacks).ToString("0.#"), UIFactory.Hex("#E04040")));
            if (pc.FreeBulletsTurn) list.Add(new StatusBadge("Overdrive", "", "오버드라이브\n이번 턴 탄환 소모 0", UIFactory.Hex("#FF8A30")));
            if (pc.SmokeReduce > 0f) list.Add(new StatusBadge("Smoke", "", "연막\n받는 피해 -" + (int)(pc.SmokeReduce * 100) + "%", UIFactory.Hex("#8A8A98")));
            if (pc.ExecutionerGaze > 0) list.Add(new StatusBadge("Gaze", pc.ExecutionerGaze.ToString(), "처형인의 시선\n표식 있는 적에게 공격 피해 +" + pc.ExecutionerGaze, UIFactory.Hex("#B02030")));
            if (pc.AmmoSupply > 0) list.Add(new StatusBadge("Ammo", pc.AmmoSupply.ToString(), "탄약 보급\n매 턴 탄환 +" + pc.AmmoSupply, UIFactory.Hex("#C8A040")));
            if (pc.CrimsonMag > 0) list.Add(new StatusBadge("Crimson", "", "진홍의 탄창\n공격 카드 2장마다 탄환 +2, 1장 뽑기", UIFactory.Hex("#C03050")));
            if (pc.WeakAnalysis > 0) list.Add(new StatusBadge("Analysis", "", "약점 분석\n표식을 걸 때마다 방어도·탄환", UIFactory.Hex("#A0A060")));
            if (pc.SteelWill > 0) list.Add(new StatusBadge("SteelWill", pc.SteelWill.ToString(), "강철 의지\n턴 종료 시 방어도 " + pc.SteelWill, UIFactory.Hex("#7090B0")));
            if (pc.ThornArmor > 0) list.Add(new StatusBadge("ThornArmor", pc.ThornArmor.ToString(), "가시 갑옷\n공격받으면 공격자에게 " + pc.ThornArmor + " 피해", UIFactory.Hex("#7AA050")));
            if (pc.BattleInstinct > 0) list.Add(new StatusBadge("Instinct", "", "전투 본능\n공격 카드로 처치하면 에너지 +1, 1장 뽑기", UIFactory.Hex("#D07030")));
            if (pc.Transcend > 0) list.Add(new StatusBadge("Transcend", "", "초월\n턴 시작에 무작위 손패 1장 에너지 0", UIFactory.Hex("#9070D0")));
        }
        EnemyCombatant ec = u as EnemyCombatant;
        if (ec != null && ec.Marks > 0) list.Add(new StatusBadge("Mark", ec.Marks.ToString(), "표식 " + ec.Marks + " (최대 10)\n받는 공격 피해 +" + ec.Marks, UIFactory.Hex("#E03A3A")));
        if (ec != null && ec.Thorns > 0) list.Add(new StatusBadge("Thorns", ec.Thorns.ToString(), "가시 " + ec.Thorns + "\n공격 카드로 때리면 때린 사람이 " + ec.Thorns + " 피해", UIFactory.Hex("#7AA050")));
        if (ec != null && ec.Ritual > 0) list.Add(new StatusBadge("Ritual", ec.Ritual.ToString(), "의식 " + ec.Ritual + "\n적 차례가 끝날 때마다 힘 +" + ec.Ritual, UIFactory.Hex("#B04040")));
    }

    // ---------------- 몬스터 ----------------
    public static List<StatusBadge> ForEnemy(EnemyCombatant e, List<PlayerCombatant> players)
    {
        List<StatusBadge> list = new List<StatusBadge>();
        if (e.IsDead) return list;
        AddCommon(list, e);
        if (e.FreezeStacks > 0)
            list.Add(new StatusBadge("Freeze", e.FreezeStacks + "/" + e.FreezeThreshold,
                "동결 " + e.FreezeStacks + " / " + e.FreezeThreshold + "\n" + e.FreezeThreshold + "개가 되면 [동상] (행동 취소)", UIFactory.Hex("#96D7FF")));
        if (e.FreezeImmuneTurns > 0)
            list.Add(new StatusBadge("FreezeImmune", "", "빙결 면역\n동상 직후라 동결이 쌓이지 않음 (영구동토석은 무시)", UIFactory.Hex("#EB5A5A")));
        if (e.ShockStacks > 0)
            list.Add(new StatusBadge("ShockStack", e.ShockStacks + "/" + GameBalance.ShockStacksNeeded,
                "감전 스택 " + e.ShockStacks + " / " + GameBalance.ShockStacksNeeded + "\n" + GameBalance.ShockStacksNeeded + "개가 되면 [감전] " + GameBalance.ShockTurns + "턴", UIFactory.Hex("#BEAA5A")));
        if (e.ExecuteStacks > 0)
        {
            string need = e.IsBoss ? "HP × " + GameBalance.ExecuteRatioBoss + "보다 많으면"
                        : (e.IsElite ? "HP × " + GameBalance.ExecuteRatioElite + "보다 많으면" : "HP보다 많으면");
            list.Add(new StatusBadge("Execute", e.ExecuteStacks.ToString(), "처형 스택 " + e.ExecuteStacks + "\n스택이 " + need + " 즉시 처형", UIFactory.Hex("#AA5AFF")));
        }
        if (e.Strength > 0) list.Add(new StatusBadge("Strength", e.Strength.ToString(), "힘 " + e.Strength + "\n공격 1회당 피해 +" + e.Strength, UIFactory.Hex("#E6463C")));
        if (e.RageStacks > 0) list.Add(new StatusBadge("Rage", e.RageStacks.ToString(), "분노 " + e.RageStacks + "\n동료가 쓰러질 때마다 공격력 +" + (int)(GameBalance.SquadRageBonus * 100) + "%", UIFactory.Hex("#C82828")));
        foreach (PlayerCombatant p in players)
        {
            if (p.MarkedEnemy == e) list.Add(new StatusBadge("Mark", "", "죽음의 인장 (" + p.Name + ")\n" + p.Name + "의 공격 2배, 처치 시 영혼·HP 회복", UIFactory.Hex("#6E28AA")));
        }
        if (e.SentencedBy != null) list.Add(new StatusBadge("Sentence", "", "사형 선고 (" + e.SentencedBy.Name + ")\n처형되면 " + e.SentencedBy.Name + " HP +" + GameBalance.SentenceHeal, UIFactory.Hex("#963C3C")));
        if (e.ShroudBy != null) list.Add(new StatusBadge("Shroud", "", "표식 (" + e.ShroudBy.Name + ")\n이번 턴에 쓰러지면 에너지·카드·영혼 보상", UIFactory.Hex("#C83C3C")));
        if (e.BrandTurns > 0) list.Add(new StatusBadge("Brand", e.BrandTurns.ToString(), "낙인\n받는 발화 피해 2배 (" + e.BrandTurns + "턴)", UIFactory.Hex("#FF9632")));
        if (e.BlockLocked) list.Add(new StatusBadge("BlockLock", "", "만년설\n다음 적 차례 동안 방어도 0 고정", UIFactory.Hex("#7896BE")));
        if (e.HalfDamage) list.Add(new StatusBadge("HalfDamage", "", "빙하절단\n이번 적 차례 공격 피해 -50%", UIFactory.Hex("#AAC8E6")));
        return list;
    }

    // ---------------- 플레이어 ----------------
    public static List<StatusBadge> ForPlayer(PlayerCombatant p)
    {
        List<StatusBadge> list = new List<StatusBadge>();
        if (p.IsDead) return list;
        AddCommon(list, p);
        if (p.NightForever) list.Add(new StatusBadge("Night", "", "죽음의 밤\n전투 동안 [밤]. 공격 피해 +" + GameBalance.DeathNightFlatBonus + ", 매 턴 영혼 +" + GameBalance.DeathNightSoulPerTurn, UIFactory.Hex("#D74678")));
        else if (p.NightThisTurn) list.Add(new StatusBadge("Night", p.NightTurns.ToString(), "밤 (" + (p.NightTurns > 1 ? p.NightTurns + "턴 남음" : "이번 턴까지") + ")\n야습 2배, 윤무 4회, 칠흑의 망토 유지", UIFactory.Hex("#D74678")));
        if (p.PendingHalfMoon > 0) list.Add(new StatusBadge("Charge", p.PendingHalfMoon.ToString(), "반월참 차징 " + p.PendingHalfMoon + "\n다음 턴 시작에 모든 적에게 발동", UIFactory.Hex("#AA78FF")));
        if (p.RetainBlockOnce) list.Add(new StatusBadge("Retain", "", "방어도 유지\n다음 턴 시작에 방어도가 사라지지 않음", UIFactory.Hex("#4678C8")));
        if (p.SealedCard != null) list.Add(new StatusBadge("Sealed", "", "봉인: " + p.SealedCard.Name + "\n다음 턴 시작에 에너지 -" + GameBalance.SoulSealDiscount + "로 돌아옴", UIFactory.Hex("#BE96F0")));
        return list;
    }

    // 같은 내용이면 다시 그리지 않으려고 쓰는 "열쇠" 글자
    public static string Key(List<StatusBadge> list)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (StatusBadge b in list) sb.Append(b.Icon).Append(':').Append(b.Count).Append(':').Append(b.Tip.Length).Append('|');
        return sb.ToString();
    }

    // 3-118: 접힌 상태에서 한 줄에 보여 줄 최대 개수. 넘치면 마지막 칸에 "+N" (누르면 전부 펼침, 다시 누르면 접음)
    //   멀티(4인)에서 상태가 많으면 아이콘 줄이 옆 캐릭터·카드와 겹쳐서 안 보였음
    public const int MaxCollapsed = 6;

    // row 하나의 펼침 상태 (row 에 붙여 둠. 자바로 치면 그 줄 전용 필드를 가진 작은 객체)
    class RowState : MonoBehaviour
    {
        // 3-136: 실행 중에만 쓰는 값이라 저장(직렬화) 안 함 → 경고 UAC1001 없애기 (자바의 transient 와 같음)
        [System.NonSerialized] public bool Expanded;
        [System.NonSerialized] public List<StatusBadge> List;
        [System.NonSerialized] public System.Action<string, RectTransform> OnHover;
    }

    // row 안에 아이콘들을 가운데 정렬로 그림. onHover(설명, 아이콘 위치) / onHover(null, null) = 숨기기
    public static void Draw(RectTransform row, List<StatusBadge> list, System.Action<string, RectTransform> onHover)
    {
        for (int i = row.childCount - 1; i >= 0; i--) Object.Destroy(row.GetChild(i).gameObject);
        RowState state = row.GetComponent<RowState>();
        if (state == null) state = row.gameObject.AddComponent<RowState>();
        state.List = list;
        state.OnHover = onHover;

        // 3-118: 접혀 있고 넘치면 앞의 (MaxCollapsed-1)개 + "+N" 칸. 펼치면 전부 + "▲" 칸
        List<StatusBadge> shown = list;
        int hidden = 0;
        if (list.Count > MaxCollapsed)
        {
            if (state.Expanded) shown = new List<StatusBadge>(list);
            else
            {
                shown = list.GetRange(0, MaxCollapsed - 1);
                hidden = list.Count - shown.Count;
            }
            string names = "";
            for (int k = state.Expanded ? 0 : MaxCollapsed - 1; k < list.Count; k++)
            {
                string t = list[k].Tip;
                int nl = t.IndexOf('\n');
                names += "\n· " + (nl >= 0 ? t.Substring(0, nl) : t);
            }
            shown.Add(new StatusBadge("__More", state.Expanded ? "" : "", state.Expanded ? "접기\n눌러서 아이콘을 한 줄로" : "상태 " + hidden + "개 더 있음 (눌러서 펼치기)" + names, new Color(0.3f, 0.3f, 0.36f)));
        }

        // 한 줄에 최대 7개, 넘치면 다음 줄
        const int perRow = 7;
        for (int i = 0; i < shown.Count; i++)
        {
            StatusBadge b = shown[i];
            int line = i / perRow;
            int inLine = Mathf.Min(perRow, shown.Count - line * perRow);
            int col = i % perRow;
            float x = (col - (inLine - 1) / 2f) * (Size + Gap);
            float y = -line * (Size + Gap);

            bool isMore = b.Icon == "__More";
            Sprite sprite = isMore ? null : Load(b.Icon);
            Image icon;
            if (isMore)
            {
                // "+N" / "▲" 칸: 누르면 펼치기·접기
                icon = UIFactory.MakeCircle("More", row, b.Tint);
                Text mt = UIFactory.MakeText("Label", icon.transform, state.Expanded ? "▲" : "+" + hidden, 18, Color.white, TextAnchor.MiddleCenter);
                UIFactory.Stretch(mt.rectTransform);
                mt.fontStyle = FontStyle.Bold;
                mt.raycastTarget = false;
                Button mb = icon.gameObject.AddComponent<Button>();
                UIFactory.AddClick(mb);   // 3-266: 그림 버튼도 누르는 소리
                RowState st = state;
                RectTransform rowRef = row;
                mb.onClick.AddListener(() =>
                {
                    st.Expanded = !st.Expanded;
                    if (st.OnHover != null) st.OnHover(null, null);
                    Draw(rowRef, st.List, st.OnHover);
                });
            }
            else if (sprite != null)
            {
                icon = UIFactory.MakeImage(b.Icon, row, Color.white, false);
                icon.sprite = sprite;
                icon.preserveAspect = true;
            }
            else
            {
                icon = UIFactory.MakeCircle(b.Icon, row, b.Tint);
                Text letter = UIFactory.MakeText("Letter", icon.transform, b.Tip.Substring(0, 1), 20, Color.white, TextAnchor.MiddleCenter);
                UIFactory.Stretch(letter.rectTransform);
                letter.raycastTarget = false;
            }
            UIFactory.Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(x, y), new Vector2(Size, Size));
            icon.raycastTarget = true;   // 마우스를 받아야 설명이 뜸

            if (b.Count.Length > 0)
            {
                Text count = UIFactory.MakeText("Count", icon.transform, b.Count, 17, Color.white, TextAnchor.LowerRight);
                UIFactory.Place(count.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(8, -6), new Vector2(60, 24));
                count.fontStyle = FontStyle.Bold;
                count.raycastTarget = false;
                count.horizontalOverflow = HorizontalWrapMode.Overflow;
                count.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.95f);
            }

            string tip = b.Tip;
            RectTransform rt = icon.rectTransform;
            HoverRelay hover = icon.gameObject.AddComponent<HoverRelay>();
            hover.OnChange = (on) => { if (onHover != null) onHover(on ? tip : null, on ? rt : null); };
        }
    }
}
