// ============================================================
// PotionLibrary.cs
// 포션 (2026-11-15). 보물 상자에서 아티팩트 대신 나올 수 있음
//   한 사람당 최대 GameBalance.PotionSlots 개. 전투 중 내 차례에 눌러서 씀 (에너지 안 듦, 한 번 쓰면 사라짐)
//   대상을 고르지 않는 포션만 (나 자신 또는 모든 적) → 화면이 간단함
//   종류: 회복 / 데미지(화염) / 강화 / 취약
//
// 자바와 비교: PotionData = 값만 담는 클래스(DTO), PotionLibrary = static 메서드만 있는 도우미 클래스
// ============================================================

using System.Collections.Generic;

public class PotionData
{
    public string Id;
    public string Name;
    public string Description;
    public string ColorHex;   // 아이콘 색 (화면에서 씀). Core 는 UnityEngine 을 안 쓰므로 글자로

    public PotionData(string id, string name, string description, string colorHex)
    {
        Id = id;
        Name = name;
        Description = description;
        ColorHex = colorHex;
    }
}

public static class PotionLibrary
{
    // 2026-11-15: 회복 · 데미지 · 강화 · 취약 4종
    public const string Heal = "potion_heal";
    public const string Fire = "potion_fire";
    public const string Power = "potion_power";
    public const string Vulnerable = "potion_vuln";
    // 3-157: 새 포션 5종
    public const string Block = "potion_block";
    public const string Energy = "potion_energy";
    public const string Swift = "potion_swift";
    public const string Weak = "potion_weak";
    public const string Phoenix = "potion_phoenix";   // 희귀, 멀티 전용

    static List<PotionData> all;

    public static List<PotionData> All
    {
        get
        {
            if (all == null)
            {
                all = new List<PotionData>
                {
                    new PotionData(Heal, "회복 포션", "아군 1명(자신 포함) 최대 HP의 " + (int)(GameBalance.PotionHealRatio * 100) + "% 회복", "#E04848"),
                    new PotionData(Fire, "화염 포션", "모든 적에게 " + GameBalance.PotionFireDamage + " 피해", "#FF6A20"),
                    new PotionData(Power, "강화 포션", "아군 1명(자신 포함) 이번 전투 동안 공격 피해 +" + GameBalance.PotionPowerBonus, "#F0B030"),
                    new PotionData(Vulnerable, "취약 포션", "모든 적 취약 " + GameBalance.PotionVulnTurns, "#9A50E0"),
                    new PotionData(Block, "방어 포션", "아군 1명(자신 포함) 방어도 +" + GameBalance.PotionBlock, "#8AB8D8"),
                    new PotionData(Energy, "에너지 포션", "이번 턴 에너지 +" + GameBalance.PotionEnergy, "#3A8AF0"),
                    new PotionData(Swift, "신속 포션", "카드 " + GameBalance.PotionDraw + "장 뽑기", "#E8E0B0"),
                    new PotionData(Weak, "약화 포션", "모든 적 약화 " + GameBalance.PotionWeakTurns, "#5A5A60"),
                    new PotionData(Phoenix, "불사조 깃털", "[희귀] 쓰러진 동료를 최대 HP " + (int)(GameBalance.PotionReviveRatio * 100) + "%로 부활 (멀티)", "#E85A2A"),
                };
            }
            return all;
        }
    }

    public static PotionData Get(string id)
    {
        foreach (PotionData p in All) if (p.Id == id) return p;
        return null;
    }

    // 3-157: 무게를 둔 뽑기 (자바로 치면 가중치 룰렛)
    //   보통 포션 무게 3, 불사조 깃털 1 (희귀). 혼자 하는 판(playerCount 1)에는 불사조 깃털이 안 나옴 (살릴 동료가 없음)
    public static PotionData Random(System.Random rng, int playerCount = 1)
    {
        int total = 0;
        foreach (PotionData p in All) total += Weight(p, playerCount);
        int roll = rng.Next(total);
        foreach (PotionData p in All)
        {
            roll -= Weight(p, playerCount);
            if (roll < 0) return p;
        }
        return All[0];
    }

    static int Weight(PotionData p, int playerCount)
    {
        if (p.Id == Phoenix) return playerCount > 1 ? GameBalance.PotionRareWeight : 0;
        return GameBalance.PotionCommonWeight;
    }
}
