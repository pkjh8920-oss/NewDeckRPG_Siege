// ============================================================
// DamageCalculator.cs
// 피해 계산 공식만 모아 둔 곳입니다.
//
// 플레이어 공격:
//   피해 = (카드 기본 + 힘 + 고정 보너스)
//          × (1 + 크리티컬 0.5 + 취약 0.5 + 죽음의 인장 1.0 + 카드 자체 배수)
//          × (약화면 0.7)
//   - 배수는 곱하지 않고 "더합니다". 크리티컬+취약 = ×2.0 (×2.25 아님)
//   - 약화·감전만 마지막에 곱함 (주는 쪽이 약해진 것)
//   - 방어도 관통(AD만): 민첩 × 3% 만큼 적 방어도를 무시 (피해 숫자는 그대로)
//   - 소수점은 버립니다 (예: 7.5 → 7)
//
// 몬스터 공격:
//   피해 = (공격력 + 힘) × 스테이지 배율 × (1 + 취약 0.5) × 약화 0.7 × 감전 0.8 × (기타 감소)
// ============================================================

public struct DamageResult
{
    public int Damage;
    public bool IsCrit;
    public float IgnoreRatio;   // 무시하는 방어도 비율 (관통)
}

public static class DamageCalculator
{
    // 힘 → 공격 피해 보너스. 3-160: 힘 2점마다 +1 (StrStep = 2) (정수 나눗셈: 힘 5 = +2)
    //   자바의 int 나눗셈과 같음 (5 / 2 = 2)
    public static int StrDamageBonus(int str)
    {
        return str / GameBalance.StrStep * GameBalance.AdPerStr;
    }

    // flatBonus       : 더하기 보너스 (죽음의 밤 +3, 무기 날 등)
    // extraMultiplier : 카드 자체의 배수 보너스 (반월참 밤 +0.5 등)
    // addStatBonus    : false 면 힘 보너스를 더하지 않음
    // allowCrit       : false 면 크리티컬·관통 판정을 하지 않음
    public static DamageResult PlayerAttack(PlayerCombatant attacker, EnemyCombatant target,
                                            int baseDamage, DamageKind kind, int flatBonus,
                                            float extraMultiplier, bool addStatBonus, bool allowCrit,
                                            System.Random rng)
    {
        DamageResult result = new DamageResult();

        // 1) 더하기 부분
        float statBonus = addStatBonus ? StrDamageBonus(attacker.Str) : 0f;   // 3-160: 힘 2점당 +1
        float raw = baseDamage + statBonus + flatBonus;

        // 2) 크리티컬 (rng.NextDouble() = 0.0 이상 1.0 미만의 무작위 수), 관통 비율
        if (allowCrit)
        {
            result.IsCrit = rng.NextDouble() < attacker.CritChance();
            if (kind == DamageKind.AD) result.IgnoreRatio = attacker.PenetrationRatio();
        }

        // 3) 배수 (더해서 합침)
        float multiplier = 1f;
        if (result.IsCrit) multiplier += GameBalance.CritBonus;
        if (target.HasStatus(StatusType.Vulnerable)) multiplier += GameBalance.VulnerableBonus;
        if (attacker.MarkedEnemy == target) multiplier += GameBalance.DeathMarkBonus; // 내가 건 인장만
        multiplier += extraMultiplier;

        // 4) 내가 약화 상태면 0.7배
        if (attacker.HasStatus(StatusType.Weak)) multiplier *= 1f - GameBalance.WeakReduction;

        int damage = (int)System.Math.Floor(raw * multiplier);
        if (damage < 0) damage = 0;
        result.Damage = damage;
        return result;
    }

    // 몬스터 → 플레이어 1회 피해 (감소 효과는 BattleSystem 이 extraCut 으로 넘김. 0.4 = 40% 감소)
    public static int EnemyAttack(EnemyCombatant attacker, EnemyMove move, Combatant target, float extraFactor)
    {
        float multiplier = 1f;
        if (target.HasStatus(StatusType.Vulnerable)) multiplier += GameBalance.VulnerableBonus;
        if (attacker.HasStatus(StatusType.Weak)) multiplier *= 1f - GameBalance.WeakReduction;
        if (attacker.HasStatus(StatusType.Shock)) multiplier *= 1f - GameBalance.ShockReduction;
        multiplier *= extraFactor;

        int damage = (int)System.Math.Floor(attacker.ScaledDamage(move) * multiplier);
        if (damage < 0) damage = 0;
        return damage;
    }

    // 학교에서 배운 반올림 (0.5는 항상 올림): 4.5 → 5, 5.5 → 6
    // C#의 Math.Round(4.5) 는 기본값이 "짝수 쪽 반올림" 이라 4가 나오므로
    // MidpointRounding.AwayFromZero 를 꼭 붙여서 씁니다.
    // + 0.0001 = float 오차(28.4999...) 보정
    public static int RoundHalfUp(double value)
    {
        return (int)System.Math.Round(value + 0.0001, System.MidpointRounding.AwayFromZero);
    }
}
