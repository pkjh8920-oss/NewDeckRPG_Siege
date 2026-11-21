// ============================================================
// CardEffects.cs
// 카드를 이루는 "효과 부품" 들입니다.
//
// 모든 부품은 CardEffect 를 상속하고, Apply() 하나만 구현합니다.
// BattleSystem 은 카드를 쓰면 카드 안의 부품들을 순서대로 Apply() 합니다.
//
//   DamageEffect      피해 주기
//   BlockEffect       방어도 얻기
//   ApplyStatusEffect 취약·약화 걸기
//   GainManaEffect    마나(영혼·홍염·마나) 얻기
//   DrawEffect        카드 뽑기
//   GainEnergyEffect  에너지 얻기
//   HealEffect        HP 회복
//   CustomEffect      그 카드만의 특별한 효과 (람다로 바로 적음)
//
// 2026-11-08 개편: 직업 카드가 90장 가까이 되어서, 카드마다 클래스를 만들지 않고
//   CustomEffect 에 람다(이름 없는 함수)로 효과를 적습니다. 카드 정의 바로 옆에 효과가 있어서 읽기 쉬움
//   자바의 람다와 같음:  ctx => { ... }   ==   (ctx) -> { ... }
// ============================================================

using System.Collections.Generic;

// 효과가 실행될 때 필요한 정보 묶음
public class EffectContext
{
    public BattleSystem Battle;     // 전투 전체 (피해 계산, 로그, 드로우 등)
    public PlayerCombatant User;    // 카드를 쓴 플레이어
    public EnemyCombatant Target;   // 선택한 적 (선택이 없는 카드면 null)
    public int ManaSpent;           // 이 카드에 쓴 마나 (영혼 방출처럼 "전부 쓰는" 카드에서 사용)
    public CardData Card;           // 지금 쓰는 카드
}

public abstract class CardEffect
{
    public abstract void Apply(EffectContext ctx);

    // 효과 대상 목록: allEnemies 가 true 면 살아 있는 적 전부, 아니면 선택한 적 1명
    protected List<EnemyCombatant> GetTargets(EffectContext ctx, bool allEnemies)
    {
        List<EnemyCombatant> result = new List<EnemyCombatant>();
        if (allEnemies) result.AddRange(ctx.Battle.LivingEnemies());
        else if (ctx.Target != null && !ctx.Target.IsDead) result.Add(ctx.Target);
        return result;
    }
}

// ---------------- 피해 ----------------
public class DamageEffect : CardEffect
{
    public int Amount;
    public DamageKind Kind;
    public bool AllEnemies;
    public int Hits;

    public DamageEffect(int amount, DamageKind kind, bool allEnemies = false, int hits = 1)
    {
        Amount = amount;
        Kind = kind;
        AllEnemies = allEnemies;
        Hits = hits;
    }

    public override void Apply(EffectContext ctx)
    {
        for (int i = 0; i < Hits; i++)
        {
            foreach (EnemyCombatant enemy in GetTargets(ctx, AllEnemies))
            {
                ctx.Battle.PlayerAttack(ctx.User, enemy, Amount, Kind, 0f);
            }
        }
    }
}

// ---------------- 방어도 ----------------
public class BlockEffect : CardEffect
{
    public int Amount;
    public BlockEffect(int amount) { Amount = amount; }

    public override void Apply(EffectContext ctx)
    {
        int before = ctx.User.Block;
        ctx.User.GainBlock(Amount);
        // 손상·아티팩트 페널티로 줄었을 수 있으니 실제로 늘어난 만큼 적음 (3-99)
        ctx.Battle.AddLog(ctx.User.Name + " 방어도 +" + (ctx.User.Block - before) + " (현재 " + ctx.User.Block + ")");
    }
}

// ---------------- 상태 이상 (취약·약화) ----------------
public class ApplyStatusEffect : CardEffect
{
    public StatusType Status;
    public int Turns;
    public bool AllEnemies;

    public ApplyStatusEffect(StatusType status, int turns, bool allEnemies = false)
    {
        Status = status;
        Turns = turns;
        AllEnemies = allEnemies;
    }

    public override void Apply(EffectContext ctx)
    {
        foreach (EnemyCombatant enemy in GetTargets(ctx, AllEnemies))
        {
            ctx.Battle.ApplyStatus(enemy, Status, Turns);
        }
    }
}

// ---------------- 마나 ----------------
public class GainManaEffect : CardEffect
{
    public int Amount;
    public GainManaEffect(int amount) { Amount = amount; }

    public override void Apply(EffectContext ctx)
    {
        // 마도사처럼 "타격·방어로 자원을 얻지 않는" 직업도 있음 (기본 카드는 BasicCardsGiveMana 로 따로 막음)
        int before = ctx.User.Mana;
        ctx.User.GainMana(Amount);
        if (ctx.User.Mana > before)
            ctx.Battle.AddLog("  " + ctx.User.ManaName + " +" + (ctx.User.Mana - before) + " (" + ctx.User.Mana + "/" + ctx.User.MaxMana + ")");
    }
}

// ---------------- 카드 뽑기 ----------------
public class DrawEffect : CardEffect
{
    public int Count;
    public DrawEffect(int count) { Count = count; }

    public override void Apply(EffectContext ctx)
    {
        ctx.Battle.Draw(ctx.User, Count);
        ctx.Battle.AddLog("  " + Count + "장 뽑기");
    }
}

// ---------------- 에너지 ----------------
public class GainEnergyEffect : CardEffect
{
    public int Amount;
    public GainEnergyEffect(int amount) { Amount = amount; }

    public override void Apply(EffectContext ctx)
    {
        ctx.User.Energy += Amount;
        ctx.Battle.AddLog("  에너지 +" + Amount + " (" + ctx.User.Energy + ")");
    }
}

// ---------------- HP 회복 ----------------
public class HealEffect : CardEffect
{
    public int Amount;
    public HealEffect(int amount) { Amount = amount; }

    public override void Apply(EffectContext ctx)
    {
        int healed = ctx.User.Heal(Amount);
        ctx.Battle.AddLog("  " + ctx.User.Name + " HP +" + healed);
    }
}

// ---------------- 그 카드만의 효과 (람다) ----------------
// System.Action<EffectContext> = "EffectContext 하나를 받고 아무것도 돌려주지 않는 함수"
// 자바의 Consumer<EffectContext> 와 같음
public class CustomEffect : CardEffect
{
    private System.Action<EffectContext> body;
    public CustomEffect(System.Action<EffectContext> body) { this.body = body; }

    public override void Apply(EffectContext ctx)
    {
        if (body != null) body(ctx);
    }
}
