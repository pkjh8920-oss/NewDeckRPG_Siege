// ============================================================
// Combatant.cs
// "전투에 참가하는 존재" 의 공통 부모 클래스입니다.
// 플레이어(PlayerCombatant)와 몬스터(EnemyCombatant)가 이 클래스를 상속합니다.
//
// 자바와 비교:
//   abstract class Combatant        ← 자바와 똑같이 abstract 클래스
//   PlayerCombatant : Combatant     ← 자바의 extends 대신 C#은 콜론(:)
//   virtual / override              ← 자바는 모든 메서드가 자동 오버라이드 가능,
//                                     C#은 부모에 virtual, 자식에 override 를 붙여야 함
//
// 이 파일은 UnityEngine 을 쓰지 않습니다. (규칙 계산과 화면 표시 분리)
// ============================================================

using System.Collections.Generic;

// 상태 이상 종류. 새 상태가 생기면 여기에 한 줄 추가합니다.
// 숫자의 뜻이 상태마다 다름:
//   취약·약화·감전·동상 = 남은 턴 수
//   발화 = 스택 (턴 시작에 스택만큼 피해, 그 뒤 1 감소)
public enum StatusType
{
    Vulnerable, // 취약: 받는 피해 +50%
    Burn,       // 발화: 내 턴 시작에 스택만큼 피해(방어도 무시) → 1 감소 (30 → 29 → ... → 0)
    Weak,       // 약화: 주는 피해 -30%
    Shock,      // 감전 상태: 주는 피해 -20% (감전 스택 4개가 모이면 2턴)
    Frostbite,  // 동상: 이번 적 차례 행동 취소 (동결 스택이 가득 차면 걸림)
    Frail       // 손상 (2026-11-15, 슬더스2 Frail): 얻는 방어도 -25%. 몬스터가 플레이어에게 걸음
}

public abstract class Combatant
{
    public string Name;
    public int Hp;
    public int MaxHp;
    public int Block;   // 방어도

    // 상태 이상 → 숫자 (턴 수 또는 스택)
    // Dictionary 는 자바의 HashMap 과 같습니다.
    public Dictionary<StatusType, int> Statuses = new Dictionary<StatusType, int>();

    // => 는 "return 한 줄짜리 메서드/속성" 을 짧게 쓰는 문법입니다.
    public bool IsDead => Hp <= 0;

    public int GetStatus(StatusType type)
    {
        int value;
        // TryGetValue: 키가 있으면 true 와 함께 값을 value 에 넣어 줌. 없으면 false.
        if (Statuses.TryGetValue(type, out value)) return value;
        return 0;
    }

    public bool HasStatus(StatusType type)
    {
        return GetStatus(type) > 0;
    }

    public void SetStatus(StatusType type, int value)
    {
        if (value <= 0) Statuses.Remove(type);   // 0이면 아예 지움
        else Statuses[type] = value;
    }

    // 얻는 방어도 배율 (3-99): 1 = 그대로. 심연의 사신 낫 0.7, 영겁의 화로(카드를 내는 동안만) 0.6 → BattleSystem 이 정함
    public float BlockGainMult = 1f;
    public int BlockGainMinus;   // 3-176 예리한 칼날 조각: 방어도를 얻을 때마다 이만큼 적게 (배율 다음에 뺌)

    public void GainBlock(int amount)
    {
        if (amount <= 0) return;
        // 손상: 얻는 방어도 25% 감소 (소수점 버림). 예) 방어도 8 → 6
        if (HasStatus(StatusType.Frail)) amount = (int)System.Math.Floor(amount * (1f - GameBalance.FrailReduction));
        if (BlockGainMult < 1f) amount = (int)System.Math.Floor(amount * BlockGainMult);   // 아티팩트 페널티 (내림)
        if (BlockGainMinus > 0) { amount -= BlockGainMinus; if (amount <= 0) return; }    // 3-176 예리한 칼날 조각
        Block += amount;
    }

    // 공격 피해를 받습니다. 방어도가 먼저 깎이고, 남은 만큼 HP가 깎입니다.
    //   ignoreBlock = true  → 방어도를 아예 무시 (피의 검무, 화염의 창)
    //   ignoreRatio = 0.3   → 방어도의 30%는 없는 것처럼 계산 (민첩 관통). 무시한 만큼 방어도는 그대로 남음
    //     예) 방어도 10, 관통 30%, 피해 12 → 방어도 7만 막음 → 방어도 10-7=3 남고 HP -5
    // 반환값: 실제로 깎인 HP
    public int TakeAttackDamage(int amount, bool ignoreBlock, float ignoreRatio = 0f)
    {
        if (amount <= 0) return 0;

        int remaining = amount;
        if (!ignoreBlock && Block > 0)
        {
            int usable = Block - (int)System.Math.Floor(Block * ignoreRatio);   // 관통 후 막을 수 있는 방어도
            int absorbed = System.Math.Min(usable, remaining);
            Block -= absorbed;
            remaining -= absorbed;
        }
        return LoseHp(remaining);
    }

    // 방어도와 상관없이 HP를 바로 깎습니다. (발화 같은 지속 피해)
    public virtual int LoseHp(int amount)
    {
        if (amount <= 0) return 0;
        int before = Hp;
        Hp -= amount;
        if (Hp < 0) Hp = 0;
        return before - Hp;
    }

    // HP 회복 (최대 HP를 넘지 않음). 반환값: 실제로 회복한 양
    public int Heal(int amount)
    {
        if (amount <= 0 || IsDead) return 0;
        int before = Hp;
        Hp += amount;
        if (Hp > MaxHp) Hp = MaxHp;
        return Hp - before;
    }

    // 크리티컬 확률 / 방어도 관통 비율. 기본은 0 (몬스터). 플레이어가 override
    public virtual float CritChance() { return 0f; }
    public virtual float PenetrationRatio() { return 0f; }

    // 화면 표시용: "취약 2, 발화 4" 같은 글자
    public virtual string StatusText()
    {
        List<string> parts = new List<string>();
        foreach (KeyValuePair<StatusType, int> pair in Statuses)
        {
            parts.Add(StatusName(pair.Key) + " " + pair.Value);
        }
        return parts.Count == 0 ? "" : string.Join(", ", parts);
    }

    public static string StatusName(StatusType type)
    {
        switch (type)
        {
            case StatusType.Vulnerable: return "취약";
            case StatusType.Burn: return "발화";
            case StatusType.Weak: return "약화";
            case StatusType.Shock: return "감전";
            case StatusType.Frostbite: return "동상";
            case StatusType.Frail: return "손상";
            default: return type.ToString();
        }
    }
}
