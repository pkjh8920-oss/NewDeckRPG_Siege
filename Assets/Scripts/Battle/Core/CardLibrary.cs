// ============================================================
// CardLibrary.cs
// 실제 카드 목록입니다. (2026-11-09 등급·강화 최종본 + Claude 수정본)
//
// 카드 하나 = 함수 하나. 매개변수 up 이 true 면 강화판(이름 뒤 +)을 만듦
//   public static CardData ScytheStrike(bool up = false)
//   { int dmg = up ? 9 : 6; ... return Make(카드, 등급, up, () => ScytheStrike(true)); }
//   ↑ Make 가 등급·강화 여부·"강화판 만드는 함수"를 카드에 붙여 줌
//   bool up = false  ← 기본값 매개변수. ScytheStrike() 라고만 쓰면 up = false (자바에는 없는 문법, 오버로드 2개와 같음)
//
// new CardData(아이디, 이름, 종류, 에너지, 대상, 설명, 효과 부품들...) { 추가 설정 }
//   { ManaCost = 3 }   ← 객체 초기화 문법: 만든 직후에 칸 값을 정함 (자바라면 setter 를 따로 부름)
//   Do(ctx => { ... }) ← 그 카드만의 효과를 람다로 바로 적음 (CustomEffect)
//
// 숫자는 전부 GameBalance 에 있음. 아래 B 는 GameBalance 의 짧은 별명
// 시작 덱 = 기본 카드 10장 (공격 4, 방어 4, 직업 기본 카드 2)
// 보상 카드 = RewardPool(직업) — 기본 카드를 뺀 그 직업 카드 전부
// ============================================================

using System.Collections.Generic;
using B = GameBalance;

public static class CardLibrary
{
    // ---------------- 짧게 쓰기 위한 도우미 ----------------
    static CustomEffect Do(System.Action<EffectContext> body) { return new CustomEffect(body); }

    // 카드에 등급·강화 정보를 붙여서 돌려줌
    static CardData Make(CardData card, CardRarity rarity, bool up, System.Func<CardData> upgrade)
    {
        card.Rarity = rarity;
        card.Upgraded = up;
        card.UpgradeTo = upgrade;
        if (up) card.Name += "+";
        return card;
    }

    // 선택한 적 1명에게 공격 (카드 효과 안에서 씀)
    static int Hit(EffectContext ctx, int dmg, DamageKind kind, float extra = 0f, bool ignoreBlock = false, string tag = "")
    {
        return ctx.Battle.PlayerAttack(ctx.User, ctx.Target, dmg, kind, extra, true, true, tag, ignoreBlock);
    }
    static int HitEnemy(EffectContext ctx, EnemyCombatant e, int dmg, DamageKind kind, string tag = "")
    {
        return ctx.Battle.PlayerAttack(ctx.User, e, dmg, kind, 0f, true, true, tag);
    }
    static bool Alive(EnemyCombatant e) { return e != null && !e.IsDead; }
    static int Floor(float x) { return (int)System.Math.Floor(x); }

    // ================== 몬스터가 넣는 카드 (2026-11-15) ==================
    // [상처]: 낼 수 없는 카드. 손패 자리만 차지함. 전투가 끝나면 사라짐 (덱에는 안 들어감)
    public static CardData Wound()
    {
        // 3-149: 설명을 새 카드 그림 글과 같게 ("손패 한 칸 차지"). 규칙은 그대로
        CardData c = new CardData("wound", "상처", CardType.Skill, 0, TargetType.Self, "낼 수 없음\n(손패 한 칸 차지)") { Unplayable = true, StatusCard = true };
        c.Rarity = CardRarity.Basic;
        return c;
    }

    // 3-102 몬스터가 넣는 카드
    // [화상]: 자폭 불씨 정령·태양의 석상. 낼 수 없고, 턴 종료 때 손에 있으면 HP -2
    public static CardData BurnCard()
    {
        CardData c = new CardData("burn_card", "화상", CardType.Skill, 0, TargetType.Self,
            "낼 수 없음\n턴 종료 때 손에 있으면\nHP -" + B.BurnCardHpLoss) { Unplayable = true, BurnsInHand = true, StatusCard = true };
        c.Rarity = CardRarity.Basic;
        return c;
    }

    // 3-149 [점액]: 균열 슬라임 "끈적한 점액". 에너지 1 을 내면 소멸 (아무 효과 없음) → 에너지로 치울지, 손에 둘지 고민
    //   자바로 치면 Effects 가 빈 List 인 카드 = 내기만 하고 아무 일도 안 함
    public static CardData SlimeCard()
    {
        CardData c = new CardData("slime_card", "점액", CardType.Skill, B.SlimeCardCost, TargetType.Self,
            "에너지 " + B.SlimeCardCost + ": 소멸\n(아무 효과 없음)") { Exhaust = true, StatusCard = true };
        c.Rarity = CardRarity.Basic;
        return c;
    }

    // 3-149 [공허]: 달의 석상 "월광 저주". 낼 수 없음, 뽑으면 에너지 -1, 휘발성 (턴이 끝날 때 손에 있으면 소멸)
    public static CardData VoidCard()
    {
        CardData c = new CardData("void_card", "공허", CardType.Skill, 0, TargetType.Self,
            "낼 수 없음\n뽑으면 에너지 -" + B.VoidCardEnergyLoss + ", 휘발성\n(턴 종료 시 소멸)") { Unplayable = true, Ethereal = true, EnergyLossOnDraw = B.VoidCardEnergyLoss, StatusCard = true };
        c.Rarity = CardRarity.Basic;
        return c;
    }

    // [포자]: 기생 포자충이 쓰러질 때 뽑을 카드 더미 맨 위에 넣음. 낼 수 없는 카드 (손패 한 칸을 차지)
    public static CardData SporeCard()
    {
        CardData c = new CardData("spore_card", "포자", CardType.Skill, 0, TargetType.Self,
            "낼 수 없음\n(기생 포자충이 남긴 카드,\n전투가 끝나면 사라짐)") { Unplayable = true, StatusCard = true };
        c.Rarity = CardRarity.Basic;
        return c;
    }

    // ================== 공통 (비교용) ==================
    public static CardData Strike(bool up = false)
    {
        int dmg = up ? B.SStrikeDmgUp : B.StrikeDamage;
        return Make(new CardData("strike", "타격", CardType.Attack, B.StrikeCost, TargetType.SingleEnemy,
            dmg + " 피해\n마나 +" + B.ManaGainBasicCard,
            new DamageEffect(dmg, DamageKind.AD), new GainManaEffect(B.ManaGainBasicCard)) { IsBasicAttack = true },
            CardRarity.Basic, up, () => Strike(true));
    }
    public static CardData Defend(bool up = false)
    {
        int block = up ? B.SDefendBlockUp : B.DefendBlock;
        return Make(new CardData("defend", "방어", CardType.Skill, B.DefendCost, TargetType.Self,
            "방어도 " + block + "\n마나 +" + B.ManaGainBasicCard,
            new BlockEffect(block), new GainManaEffect(B.ManaGainBasicCard)),
            CardRarity.Basic, up, () => Defend(true));
    }
    public static List<CardData> CommonStarterDeck()
    {
        List<CardData> deck = new List<CardData>();
        for (int i = 0; i < 5; i++) deck.Add(Strike());
        for (int i = 0; i < 4; i++) deck.Add(Defend());
        return deck;
    }
    public static List<CardData> CommonTestCards() { return new List<CardData>(); }

    // ======================================================================
    // 검은 낫 (AD) — 영혼 0/20, 처형 스택, [밤]
    // ======================================================================

    // ---- 기본 카드 (시작 덱 전용) ----
    public static CardData ScytheStrike(bool up = false)
    {
        int dmg = up ? B.SStrikeDmgUp : B.SStrikeDmg;
        return Make(new CardData("s_strike", "타격", CardType.Attack, 1, TargetType.SingleEnemy,
            dmg + " 피해\n영혼 +" + B.SStrikeSoul,
            new DamageEffect(dmg, DamageKind.AD), new GainManaEffect(B.SStrikeSoul)) { IsBasicAttack = true },
            CardRarity.Basic, up, () => ScytheStrike(true));
    }
    public static CardData ScytheDefend(bool up = false)
    {
        int block = up ? B.SDefendBlockUp : B.SDefendBlock;
        return Make(new CardData("s_defend", "방어", CardType.Skill, 1, TargetType.Self,
            "방어도 " + block + "\n영혼 +" + B.SDefendSoul,
            new BlockEffect(block), new GainManaEffect(B.SDefendSoul)),
            CardRarity.Basic, up, () => ScytheDefend(true));
    }
    public static CardData ReaperSlash(bool up = false)
    {
        int dmg = up ? B.ReaperSlashDmgUp : B.ReaperSlashDmg;
        int heal = up ? B.ReaperSlashHealUp : B.ReaperSlashHeal;
        return Make(new CardData("reaper_slash", "사신 베기", CardType.Attack, B.ReaperSlashCost, TargetType.SingleEnemy,
            dmg + " 피해\nHP +" + heal,
            new DamageEffect(dmg, DamageKind.AD), new HealEffect(heal)),
            CardRarity.Basic, up, () => ReaperSlash(true));
    }
    public static CardData Veil(bool up = false)
    {
        int block = up ? B.VeilBlockUp : B.VeilBlock;
        int soul = up ? B.VeilSoulUp : B.VeilSoul;
        return Make(new CardData("veil", "장막", CardType.Skill, B.VeilCost, TargetType.Self,
            "방어도 " + block + "\n영혼 +" + soul,
            new BlockEffect(block), new GainManaEffect(soul)),
            CardRarity.Basic, up, () => Veil(true));
    }

    // ---- 공격 ----
    public static CardData SoulRelease(bool up = false)
    {
        int dmg = up ? B.SoulReleaseDmgUp : B.SoulReleaseDmg;
        int per = up ? B.SoulReleasePerSoulUp : B.SoulReleasePerSoul;
        return Make(new CardData("soul_release", "영혼 방출", CardType.Attack, B.SoulReleaseCost, TargetType.SingleEnemy,
            "영혼 전부 소모\n" + dmg + " + 영혼 1당 " + per + " 피해\nHP +" + B.SoulReleaseHeal,
            Do(ctx => Hit(ctx, dmg + ctx.ManaSpent * per, DamageKind.AD)),
            new HealEffect(B.SoulReleaseHeal)) { UsesAllMana = true },
            CardRarity.Common, up, () => SoulRelease(true));
    }

    public static CardData SoulCleave(bool up = false)
    {
        int dmg = up ? B.SoulCleaveDmgUp : B.SoulCleaveDmg;
        return Make(new CardData("soul_cleave", "영혼 가르기", CardType.Attack, B.SoulCleaveCost, TargetType.SingleEnemy,
            dmg + " 피해\n영혼 +" + B.SoulCleaveSoul,
            new DamageEffect(dmg, DamageKind.AD), new GainManaEffect(B.SoulCleaveSoul)),
            CardRarity.Common, up, () => SoulCleave(true));
    }

    public static CardData NightRaid(bool up = false)
    {
        int dmg = up ? B.NightRaidDmgUp : B.NightRaidDmg;
        return Make(new CardData("night_raid", "야습", CardType.Attack, B.NightRaidCost, TargetType.SingleEnemy,
            dmg + " 피해, 영혼 +" + B.NightRaidSoul + "\n밤이면 피해 2배",
            Do(ctx => Hit(ctx, dmg, DamageKind.AD, ctx.User.IsNight ? 1f : 0f, false, ctx.User.IsNight ? " [밤 x2]" : "")),
            new GainManaEffect(B.NightRaidSoul)),
            CardRarity.Common, up, () => NightRaid(true));
    }

    public static CardData Moonlight(bool up = false)
    {
        int dmg = up ? B.MoonlightDmgUp : B.MoonlightDmg;
        int stacks = up ? B.MoonlightStacksUp : B.MoonlightStacks;
        return Make(new CardData("moonlight", "월광섬", CardType.Attack, B.MoonlightCost, TargetType.SingleEnemy,
            dmg + " 피해, 취약 1\n처형 스택 " + stacks + "\n밤이면 취약 2",
            Do(ctx =>
            {
                Hit(ctx, dmg, DamageKind.AD);
                ctx.Battle.ApplyStatus(ctx.Target, StatusType.Vulnerable, ctx.User.IsNight ? 2 : 1);
                ctx.Battle.AddExecuteStacks(ctx.User, ctx.Target, stacks);
            })),
            CardRarity.Common, up, () => Moonlight(true));
    }

    // 그림자 습격: 새 문서에서 빠졌지만 [밤]을 켜는 카드가 전설 하나뿐이라 일반으로 다시 넣음 (Claude)
    public static CardData ShadowRaid(bool up = false)
    {
        int dmg = up ? B.ShadowRaidDmgUp : B.ShadowRaidDmg;
        return Make(new CardData("shadow_raid", "그림자 습격", CardType.Attack, B.ShadowRaidCost, TargetType.SingleEnemy,
            dmg + " 피해\n이번 턴 첫 공격 카드면\n" + B.ShadowRaidNightTurns + "턴 동안 [밤], 영혼 +" + B.ShadowRaidSoul,   // 3-172
            Do(ctx =>
            {
                bool first = ctx.User.AttacksPlayedThisTurn == 0;
                if (first) ctx.Battle.EnterNight(ctx.User, false);
                Hit(ctx, dmg, DamageKind.AD);
                if (first) { ctx.User.GainMana(B.ShadowRaidSoul); ctx.Battle.AddLog("  영혼 +" + B.ShadowRaidSoul); }
            })),
            CardRarity.Common, up, () => ShadowRaid(true));
    }

    public static CardData Ambush(bool up = false)
    {
        return Make(new CardData("ambush", "기습", CardType.Attack, B.AmbushCost, TargetType.SingleEnemy,
            "선천성\n" + B.AmbushDmg + " 피해, 영혼 +" + B.AmbushSoul + (up ? "" : "\n소멸"),
            new DamageEffect(B.AmbushDmg, DamageKind.AD), new GainManaEffect(B.AmbushSoul)) { Innate = true, Exhaust = !up },
            CardRarity.Rare, up, () => Ambush(true));
    }

    public static CardData BloodDance(bool up = false)
    {
        int dmg = up ? B.BloodDanceDmgUp : B.BloodDanceDmg;
        return Make(new CardData("blood_dance", "피의 검무", CardType.Attack, B.BloodDanceCost, TargetType.SingleEnemy,
            dmg + " 피해 (방어도 무시)\n취약 1, 약화 1",
            Do(ctx => Hit(ctx, dmg, DamageKind.AD, 0f, true)),
            new ApplyStatusEffect(StatusType.Vulnerable, 1), new ApplyStatusEffect(StatusType.Weak, 1)) { ManaCost = B.BloodDanceSoul },
            CardRarity.Rare, up, () => BloodDance(true));
    }

    public static CardData ReaperWaltz(bool up = false)
    {
        int dmg = up ? B.ReaperWaltzDmgUp : B.ReaperWaltzDmg;
        return Make(new CardData("reaper_waltz", "사신의 윤무", CardType.Attack, B.ReaperWaltzCost, TargetType.AllEnemies,
            "모든 적 " + dmg + " 피해 x" + B.ReaperWaltzHits + "\n밤이면 x" + B.ReaperWaltzNightHits,
            Do(ctx =>
            {
                int hits = ctx.User.IsNight ? B.ReaperWaltzNightHits : B.ReaperWaltzHits;
                for (int i = 0; i < hits; i++)
                    foreach (EnemyCombatant e in ctx.Battle.LivingEnemies()) HitEnemy(ctx, e, dmg, DamageKind.AD);
            })) { ManaCost = B.ReaperWaltzSoul },
            CardRarity.Rare, up, () => ReaperWaltz(true));
    }

    // 흑야 참격: 피해를 준 "뒤에" 스택을 0으로 (피해를 주는 순간 처형될 수도 있음)
    public static CardData DarkNightSlash(bool up = false)
    {
        int dmg = up ? B.DarkSlashDmgUp : B.DarkSlashDmg;
        int soul = up ? B.DarkSlashSoulUp : B.DarkSlashSoul;
        return Make(new CardData("dark_night_slash", "흑야 참격", CardType.Attack, B.DarkSlashCost, TargetType.SingleEnemy,
            dmg + " + 대상 처형 스택\n만큼 피해 (그 뒤 스택 0)\n영혼 +" + soul,
            Do(ctx =>
            {
                int stacks = ctx.Target.ExecuteStacks;
                Hit(ctx, dmg + stacks, DamageKind.AD, 0f, false, stacks > 0 ? " [스택 " + stacks + " → 피해]" : "");
                if (Alive(ctx.Target)) ctx.Target.ExecuteStacks = 0;
            }),
            new GainManaEffect(soul)),
            CardRarity.Rare, up, () => DarkNightSlash(true));
    }

    public static CardData HalfMoon(bool up = false)
    {
        int dmg = up ? B.HalfMoonDamageUp : B.HalfMoonDamage;
        return Make(new CardData("half_moon", "반월참", CardType.Attack, B.HalfMoonCost, TargetType.Self,
            "[차징] 다음 턴 시작 시\n모든 적 " + dmg + " 피해\n밤이면 " + (1f + B.HalfMoonNightBonus) + "배",
            Do(ctx =>
            {
                if (ctx.User.EclipseActive) ctx.Battle.FireHalfMoon(ctx.User, dmg);   // 월식: 즉시 발동
                else
                {
                    ctx.User.PendingHalfMoons.Add(dmg);
                    ctx.Battle.AddLog("  반월참 차징! (다음 턴 시작에 발동)");
                }
            })) { IsCharge = true },
            CardRarity.Hero, up, () => HalfMoon(true));
    }

    public static CardData DeathSentence(bool up = false)
    {
        int baseStacks = up ? B.SentenceBaseStacksUp : B.SentenceBaseStacks;
        return Make(new CardData("death_sentence", "사형 선고", CardType.Attack, B.SentenceCost, TargetType.SingleEnemy,
            B.SentenceDmg + " 피해\n처형 스택 " + baseStacks + "+민첩×" + B.SentenceStacksPerAgi + "\n처형 시 HP +" + B.SentenceHeal,
            Do(ctx =>
            {
                ctx.Target.SentencedBy = ctx.User;
                Hit(ctx, B.SentenceDmg, DamageKind.AD);
                if (Alive(ctx.Target))
                    ctx.Battle.AddExecuteStacks(ctx.User, ctx.Target, baseStacks + ctx.User.Agi * B.SentenceStacksPerAgi);
            })) { ManaCost = B.SentenceSoul },
            CardRarity.Hero, up, () => DeathSentence(true));
    }

    public static CardData HarvestScythe(bool up = false)
    {
        float mult = up ? B.HarvestScytheMultiplierUp : B.HarvestScytheMultiplier;
        return Make(new CardData("harvest_scythe", "수확의 낫", CardType.Attack, B.HarvestScytheCost, TargetType.SingleEnemy,
            B.HarvestScytheDmg + " 피해\n처형 스택이 있으면\n스택 " + mult + "배\n소멸",
            Do(ctx =>
            {
                bool had = ctx.Target.ExecuteStacks > 0;
                Hit(ctx, B.HarvestScytheDmg, DamageKind.AD);
                if (had && Alive(ctx.Target))
                {
                    int now = ctx.Target.ExecuteStacks;
                    ctx.Battle.AddExecuteStacks(ctx.User, ctx.Target, Floor(now * mult) - now);
                }
            })) { ManaCost = B.HarvestScytheSoul, Exhaust = true },
            CardRarity.Hero, up, () => HarvestScythe(true));
    }

    // ---- 스킬 ----
    public static CardData Absorb(bool up = false)
    {
        int heal = up ? B.AbsorbHealUp : B.AbsorbHeal;
        return Make(new CardData("absorb", "흡수", CardType.Skill, B.AbsorbCost, TargetType.SingleEnemy,
            "취약 2, 약화 1\n영혼 +" + B.AbsorbSoul + ", HP +" + heal,
            new ApplyStatusEffect(StatusType.Vulnerable, 2), new ApplyStatusEffect(StatusType.Weak, 1),
            new GainManaEffect(B.AbsorbSoul), new HealEffect(heal)),
            CardRarity.Common, up, () => Absorb(true));
    }

    public static CardData BlackCloak(bool up = false)
    {
        int block = up ? B.BlackCloakBlockUp : B.BlackCloakBlock;
        return Make(new CardData("black_cloak", "칠흑의 망토", CardType.Skill, B.BlackCloakCost, TargetType.Self,
            "방어도 " + block + "\n밤이면 방어도가\n다음 턴까지 유지",
            new BlockEffect(block),
            Do(ctx =>
            {
                if (!ctx.User.IsNight) return;
                ctx.User.RetainBlockOnce = true;
                ctx.Battle.AddLog("  밤: 방어도가 다음 턴까지 유지됨");
            })) { ManaCost = B.BlackCloakSoul },
            CardRarity.Common, up, () => BlackCloak(true));
    }

    public static CardData ReaperShroud(bool up = false)
    {
        return Make(new CardData("reaper_shroud", "사신의 수의", CardType.Skill, B.ShroudCost, TargetType.SingleEnemy,
            "[표식] 이번 턴에 대상이\n쓰러지면 에너지 +" + B.ShroudEnergy + "\n" + B.ShroudDraw + "장 뽑기, 영혼 +" + B.ShroudSoulGain + (up ? "" : "\n소멸"),
            Do(ctx =>
            {
                ctx.Target.ShroudBy = ctx.User;
                ctx.Battle.AddLog("  " + ctx.Target.Name + "에게 [표식]");
            })) { ManaCost = B.ShroudSoul, Exhaust = !up },
            CardRarity.Rare, up, () => ReaperShroud(true));
    }

    public static CardData SoulHarvest(bool up = false)
    {
        int soul = up ? B.SoulHarvestSoulUp : B.SoulHarvestSoul;
        int draw = up ? B.SoulHarvestDrawUp : B.SoulHarvestDraw;
        return Make(new CardData("soul_harvest", "영혼 수확", CardType.Skill, B.SoulHarvestCost, TargetType.Self,
            "영혼 +" + soul + ", 에너지 +" + B.SoulHarvestEnergy + "\n" + draw + "장 뽑기\n소멸",
            new GainManaEffect(soul), new GainEnergyEffect(B.SoulHarvestEnergy), new DrawEffect(draw)) { Exhaust = true },
            CardRarity.Hero, up, () => SoulHarvest(true));
    }

    public static CardData SoulSeal(bool up = false)
    {
        return Make(new CardData("soul_seal", "영혼 봉인", CardType.Skill, up ? B.SoulSealCostUp : B.SoulSealCost, TargetType.Self,
            "손패 1장을 골라 봉인\n다음 턴 시작에 손으로\n그 카드 에너지 -" + B.SoulSealDiscount,
            Do(ctx =>
            {
                if (ctx.User.Hand.Count == 0) { ctx.Battle.AddLog("  봉인할 카드가 없습니다"); return; }
                ctx.User.PendingSealPick = true;
                ctx.Battle.AddLog("  봉인할 카드를 고르세요");
            })),
            CardRarity.Hero, up, () => SoulSeal(true));
    }

    public static CardData DeathMark(bool up = false)
    {
        int soul = up ? B.DeathMarkKillSoulUp : B.DeathMarkKillSoul;
        int heal = up ? B.DeathMarkKillHealUp : B.DeathMarkKillHeal;
        return Make(new CardData("death_mark", "죽음의 인장", CardType.Skill, B.DeathMarkCost, TargetType.SingleEnemy,
            "인장 건 적에게\n내 공격 2배 (1명만)\n처치 시 영혼 +" + soul + ", HP +" + heal,
            Do(ctx =>
            {
                ctx.User.MarkedEnemy = ctx.Target;
                ctx.User.MarkUpgraded = up;
                ctx.Battle.AddLog("  " + ctx.Target.Name + "에게 [죽음의 인장]");
            })) { ManaCost = B.DeathMarkSoul },
            CardRarity.Legend, up, () => DeathMark(true));
    }

    public static CardData DarkPact(bool up = false)
    {
        return Make(new CardData("dark_pact", "어둠의 계약", CardType.Skill, B.DarkPactCost, TargetType.Self,
            "에너지 +" + B.DarkPactEnergy + ", " + B.DarkPactDraw + "장 뽑기\n영혼 +" + B.DarkPactSoul + (up ? "" : ", HP -" + B.DarkPactHpLoss) + "\n소멸",
            new GainEnergyEffect(B.DarkPactEnergy), new DrawEffect(B.DarkPactDraw), new GainManaEffect(B.DarkPactSoul),
            Do(ctx => { if (!up) ctx.Battle.PlayerLoseHp(ctx.User, B.DarkPactHpLoss); })) { Exhaust = true },
            CardRarity.Legend, up, () => DarkPact(true));
    }

    public static CardData DarkDominion(bool up = false)
    {
        int stacks = up ? B.DarkDominionStacksUp : B.DarkDominionStacks;
        return Make(new CardData("dark_dominion", "어둠의 지배", CardType.Skill, B.DarkDominionCost, TargetType.AllEnemies,
            "이번 턴 적 피해 -" + (int)System.Math.Round(B.DarkDominionReduction * 100) + "%\n모든 적 처형 스택 " + stacks + "\n" + B.DarkDominionDraw + "장 뽑기",
            Do(ctx =>
            {
                ctx.Battle.EnemyAttackReduction = System.Math.Max(ctx.Battle.EnemyAttackReduction, B.DarkDominionReduction);
                ctx.Battle.AddLog("  이번 적 차례 적 피해 -" + (int)(B.DarkDominionReduction * 100) + "%");
                foreach (EnemyCombatant e in ctx.Battle.LivingEnemies()) ctx.Battle.AddExecuteStacks(ctx.User, e, stacks);
            }),
            new DrawEffect(B.DarkDominionDraw)) { ManaCost = B.DarkDominionSoul },
            CardRarity.Legend, up, () => DarkDominion(true));
    }

    // ---- 파워 (같은 파워를 여러 장 쓰면 효과가 더해짐) ----
    public static CardData ReaperSteps(bool up = false)
    {
        int stacks = up ? B.ReaperStepsStacksUp : B.ReaperStepsStacks;
        return Make(new CardData("reaper_steps", "사신의 발걸음", CardType.Power, B.ReaperStepsCost, TargetType.Self,
            "매 턴 시작 시\n무작위 적 1명\n처형 스택 " + stacks,
            Do(ctx => ctx.User.ReaperStepsStacks += stacks)),
            CardRarity.Rare, up, () => ReaperSteps(true));
    }

    public static CardData FullMoon(bool up = false)
    {
        int bonus = up ? B.FullMoonBonusUp : B.FullMoonBonus;
        return Make(new CardData("full_moon", "만월", CardType.Power, B.FullMoonCost, TargetType.Self,
            "전투 동안 공격 카드가\n피해를 줄 때마다\n처형 스택 " + B.FullMoonStacks + ", 피해 +" + bonus,
            Do(ctx => { ctx.User.FullMoonStacks++; ctx.User.FullMoonBonus += bonus; })) { ManaCost = B.FullMoonSoul },
            CardRarity.Rare, up, () => FullMoon(true));
    }

    public static CardData RedMoon(bool up = false)
    {
        int bonus = up ? B.RedMoonBonusUp : B.RedMoonBonus;
        return Make(new CardData("red_moon", "적월", CardType.Power, B.RedMoonCost, TargetType.Self,
            "HP " + (int)(B.RedMoonHpRatio * 100) + "% 이하일 때\n공격 카드 피해 +" + bonus,
            Do(ctx => { ctx.User.RedMoonStacks++; ctx.User.RedMoonBonus += bonus; })) { ManaCost = B.RedMoonSoul },
            CardRarity.Rare, up, () => RedMoon(true));
    }

    public static CardData SoulExploit(bool up = false)
    {
        int per = up ? B.SoulExploitPerUp : B.SoulExploitPer;
        return Make(new CardData("soul_exploit", "영혼 착취", CardType.Power, B.SoulExploitCost, TargetType.Self,
            "적 차례가 끝날 때\n처형 스택 " + per + "당 피해 " + B.SoulExploitDamage + "\n영혼 +1 (턴당 최대 " + B.SoulExploitMaxSoul + ")",
            Do(ctx =>
            {
                ctx.User.SoulExploitStacks++;
                // 강화판이 있으면 더 좋은 쪽(작은 수)으로
                if (ctx.User.SoulExploitPer == 0 || per < ctx.User.SoulExploitPer) ctx.User.SoulExploitPer = per;
            })) { ManaCost = B.SoulExploitSoul },
            CardRarity.Hero, up, () => SoulExploit(true));
    }

    public static CardData DeathNight(bool up = false)
    {
        return Make(new CardData("death_night", "죽음의 밤", CardType.Power, up ? B.DeathNightCostUp : B.DeathNightCost, TargetType.Self,
            "전투 동안 [밤]\n공격 피해 +" + B.DeathNightFlatBonus + " (중첩 X)\n방어도 +" + B.DeathNightBlock + ", 매 턴 영혼 +" + B.DeathNightSoulPerTurn,
            Do(ctx => ctx.Battle.EnterNight(ctx.User, true)),
            new BlockEffect(B.DeathNightBlock)),
            CardRarity.Legend, up, () => DeathNight(true));
    }

    // 월식: 원래 손패는 버린 카드 더미로 (문서: 소멸 → 버림)
    public static CardData Eclipse(bool up = false)
    {
        return Make(new CardData("eclipse", "월식", CardType.Power, up ? B.EclipseCostUp : B.EclipseCost, TargetType.Self,
            "(전투당 1번)\n손패를 버리고 그만큼 반월참\n이번 턴 반월참\n에너지 0 + 즉시 발동",
            Do(ctx =>
            {
                PlayerCombatant p = ctx.User;
                p.EclipseUsed = true;
                p.EclipseActive = true;
                int count = p.Hand.Count;
                p.DiscardPile.AddRange(p.Hand);
                p.Hand.Clear();
                for (int i = 0; i < count; i++) p.Hand.Add(HalfMoon());   // 이번 전투에만 생기는 카드 (덱에는 안 들어감)
                ctx.Battle.AddLog("  ★ 월식! 손패 " + count + "장을 버리고 반월참으로");
            })) { ManaCost = B.EclipseSoul },
            CardRarity.Legend, up, () => Eclipse(true));
    }

    public static List<CardData> BlackScytheStarterDeck()
    {
        List<CardData> deck = new List<CardData>();
        // 3-143: 타격 4 + 방어 4 + 사신 베기 2 + 영혼 방출 1 = 11장 (영혼을 쓰는 카드 1장은 남김)
        for (int i = 0; i < 4; i++) deck.Add(ScytheStrike());
        for (int i = 0; i < 4; i++) deck.Add(ScytheDefend());
        for (int i = 0; i < 2; i++) deck.Add(ReaperSlash());
        deck.Add(SoulRelease());   // 2026-11-09: 장막 대신. 시작 덱에 영혼을 쓰는 카드가 없어 영혼이 넘치던 문제 (장막은 코드에 남겨 둠)
        return deck;
    }

    // 보상으로 나올 수 있는 검은 낫 카드 (기본 카드 제외)
    public static List<CardData> BlackScytheTestCards()
    {
        return new List<CardData>
        {
            SoulRelease(), SoulCleave(), NightRaid(), Moonlight(), ShadowRaid(), Absorb(), BlackCloak(),
            Ambush(), BloodDance(), ReaperWaltz(), DarkNightSlash(), ReaperShroud(), ReaperSteps(), FullMoon(), RedMoon(),
            HalfMoon(), DeathSentence(), HarvestScythe(), SoulHarvest(), SoulSeal(), SoulExploit(),
            DeathMark(), DarkPact(), DarkDominion(), DeathNight(), Eclipse()
        };
    }

    // ======================================================================
    // 코스믹 피닉스 (AD) — 홍염 0/10, [발화] (턴 시작 피해 후 1 감소, 최대 15/20/30)
    // ======================================================================

    // ---- 기본 카드 ----
    public static CardData PhoenixStrike(bool up = false)
    {
        int dmg = up ? B.PStrikeDmgUp : B.PStrikeDmg;
        return Make(new CardData("p_strike", "찌르기", CardType.Attack, 1, TargetType.SingleEnemy,
            dmg + " 피해\n홍염 +" + B.PStrikeFlame,
            new DamageEffect(dmg, DamageKind.AD), new GainManaEffect(B.PStrikeFlame)) { IsBasicAttack = true },
            CardRarity.Basic, up, () => PhoenixStrike(true));
    }
    public static CardData PhoenixDefend(bool up = false)
    {
        int block = up ? B.PDefendBlockUp : B.PDefendBlock;
        return Make(new CardData("p_defend", "방어", CardType.Skill, 1, TargetType.Self,
            "방어도 " + block + "\n홍염 +" + B.PDefendFlame,
            new BlockEffect(block), new GainManaEffect(B.PDefendFlame)),
            CardRarity.Basic, up, () => PhoenixDefend(true));
    }
    public static CardData PhoenixPierce(bool up = false)
    {
        int dmg = up ? B.PierceDmgUp : B.PierceDmg;
        int burn = up ? B.PierceBurnUp : B.PierceBurn;
        return Make(new CardData("phoenix_pierce", "피닉스 피어스", CardType.Attack, B.PierceCost, TargetType.SingleEnemy,
            dmg + " 피해\n발화 " + burn,
            new DamageEffect(dmg, DamageKind.AD), Do(ctx => ctx.Battle.ApplyBurn(ctx.User, ctx.Target, burn))),
            CardRarity.Basic, up, () => PhoenixPierce(true));
    }
    public static CardData PhoenixShield(bool up = false)
    {
        int block = up ? B.PShieldBlockUp : B.PShieldBlock;
        return Make(new CardData("phoenix_shield", "피닉스의 방패", CardType.Skill, B.PShieldCost, TargetType.Self,
            "방어도 " + block + "\n이번 턴 나를 때린 적\n발화 " + B.PShieldBurn,
            new BlockEffect(block), Do(ctx => ctx.User.ReflectBurn = true)) { ManaCost = B.PShieldFlame },
            CardRarity.Basic, up, () => PhoenixShield(true));
    }

    // ---- 공격 ----
    public static CardData AshExtinction(bool up = false)
    {
        int per = up ? B.AshDmgPerFlameUp : B.AshDmgPerFlame;
        int blockPer = up ? B.AshBlockPerFlameUp : B.AshBlockPerFlame;
        return Make(new CardData("ash_extinction", "재의 소멸", CardType.Attack, B.AshCost, TargetType.SingleEnemy,
            "홍염 전부 소모\n1당 피해 " + per + ", 방어도 " + blockPer + "\n홍염 3/6/9 → 발화 1/2/3",
            Do(ctx =>
            {
                int n = ctx.ManaSpent;
                Hit(ctx, n * per, DamageKind.AD);
                ctx.User.GainBlock(n * blockPer);
                ctx.Battle.AddLog("  방어도 +" + (n * blockPer));
                ctx.Battle.FlameMultipleBurn(ctx.User, ctx.Target, n);   // 한 번에 쓴 홍염 3의 배수 → 추가 발화
            })) { UsesAllMana = true },
            CardRarity.Common, up, () => AshExtinction(true));
    }

    public static CardData Rush(bool up = false)
    {
        int weak = up ? B.RushWeakUp : B.RushWeak;
        return Make(new CardData("rush", "돌진", CardType.Attack, B.RushCost, TargetType.SingleEnemy,
            "약화 " + weak + ", " + B.RushDmg + " 피해\n발화 " + B.RushBurn,
            new ApplyStatusEffect(StatusType.Weak, weak), new DamageEffect(B.RushDmg, DamageKind.AD),
            Do(ctx => ctx.Battle.ApplyBurn(ctx.User, ctx.Target, B.RushBurn))),
            CardRarity.Common, up, () => Rush(true));
    }

    public static CardData GuardBreak(bool up = false)
    {
        int vuln = up ? B.GuardBreakVulnUp : B.GuardBreakVuln;
        return Make(new CardData("guard_break", "가드 브레이크", CardType.Attack, B.GuardBreakCost, TargetType.SingleEnemy,
            B.GuardBreakDmg + " 피해\n취약 " + vuln,
            new DamageEffect(B.GuardBreakDmg, DamageKind.AD), new ApplyStatusEffect(StatusType.Vulnerable, vuln)),
            CardRarity.Common, up, () => GuardBreak(true));
    }

    public static CardData Sunspot(bool up = false)
    {
        float mult = up ? B.SunspotBurnMultUp : B.SunspotBurnMult;
        return Make(new CardData("sunspot", "흑점 폭발", CardType.Attack, B.SunspotCost, TargetType.SingleEnemy,
            B.SunspotDmg + " + 대상 발화" + (up ? "×1.5" : "") + "만큼 피해\n(발화 전부 소모)",
            Do(ctx =>
            {
                int burn = ctx.Target.GetStatus(StatusType.Burn);
                ctx.Target.SetStatus(StatusType.Burn, 0);
                int extra = Floor(burn * mult);
                Hit(ctx, B.SunspotDmg + extra, DamageKind.AD, 0f, false, burn > 0 ? " [발화 " + burn + " 폭발]" : "");
            })) { ManaCost = B.SunspotFlame },
            CardRarity.Rare, up, () => Sunspot(true));
    }

    public static CardData FlameLance(bool up = false)
    {
        return Make(new CardData("flame_lance", "화염의 창", CardType.Attack, up ? B.FlameLanceCostUp : B.FlameLanceCost, TargetType.SingleEnemy,
            B.FlameLanceDmg + " 피해 (방어도 무시)\n발화 " + B.FlameLanceBurn,
            Do(ctx => Hit(ctx, B.FlameLanceDmg, DamageKind.AD, 0f, true)),
            Do(ctx => ctx.Battle.ApplyBurn(ctx.User, ctx.Target, B.FlameLanceBurn))),
            CardRarity.Rare, up, () => FlameLance(true));
    }

    public static CardData Brand(bool up = false)
    {
        int turns = up ? B.BrandTurnsUp : B.BrandTurns;
        return Make(new CardData("brand", "낙인", CardType.Attack, B.BrandCost, TargetType.SingleEnemy,
            B.BrandDmg + " 피해\n" + (up ? "다음 턴까지" : "이번 턴") + " 대상이 받는\n발화 피해 2배",
            new DamageEffect(B.BrandDmg, DamageKind.AD),
            Do(ctx =>
            {
                if (!Alive(ctx.Target)) return;
                ctx.Target.BrandTurns = System.Math.Max(ctx.Target.BrandTurns, turns);
                ctx.Battle.AddLog("  [낙인]: 발화 피해 2배");
            })) { ManaCost = B.BrandFlame },
            CardRarity.Rare, up, () => Brand(true));
    }

    public static CardData BlueFlame(bool up = false)
    {
        float ratio = up ? B.BlueFlameRatioUp : B.BlueFlameRatio;
        return Make(new CardData("blue_flame", "푸른 불꽃", CardType.Attack, B.BlueFlameCost, TargetType.SingleEnemy,
            "내 최대 HP의 " + (int)System.Math.Round(ratio * 100) + "%\n만큼 피해",
            Do(ctx => Hit(ctx, Floor(ctx.User.MaxHp * ratio), DamageKind.AD))),
            CardRarity.Hero, up, () => BlueFlame(true));
    }

    public static CardData PhoenixNova(bool up = false)
    {
        int burn = up ? B.NovaBurnUp : B.NovaBurn;
        float perCon = up ? B.NovaPerConUp : B.NovaPerCon;
        return Make(new CardData("phoenix_nova", "파멸의 피닉스 노바", CardType.Attack, B.NovaCost, TargetType.AllEnemies,
            "모든 적 " + B.NovaDmg + "+체력×" + perCon + " 피해\n발화 " + burn,
            Do(ctx =>
            {
                int dmg = B.NovaDmg + Floor(ctx.User.Con * perCon);
                foreach (EnemyCombatant e in ctx.Battle.LivingEnemies())
                {
                    HitEnemy(ctx, e, dmg, DamageKind.AD);
                    ctx.Battle.ApplyBurn(ctx.User, e, burn);
                }
            })),
            CardRarity.Legend, up, () => PhoenixNova(true));
    }

    public static CardData Meteor(bool up = false)
    {
        float ratio = up ? B.MeteorRatioUp : B.MeteorRatio;
        return Make(new CardData("meteor", "유성 낙하", CardType.Attack, B.MeteorCost, TargetType.AllEnemies,
            "모든 적 " + B.MeteorDmg + " 피해\n발화가 가장 높은 적\n+최대 HP " + (int)System.Math.Round(ratio * 100) + "% 피해, 취약 1",
            Do(ctx =>
            {
                foreach (EnemyCombatant e in ctx.Battle.LivingEnemies()) HitEnemy(ctx, e, B.MeteorDmg, DamageKind.AD);
                EnemyCombatant top = null;
                foreach (EnemyCombatant e in ctx.Battle.LivingEnemies())
                    if (top == null || e.GetStatus(StatusType.Burn) > top.GetStatus(StatusType.Burn)) top = e;
                if (top == null) return;
                HitEnemy(ctx, top, Floor(ctx.User.MaxHp * ratio), DamageKind.AD, " [유성 추가]");
                ctx.Battle.ApplyStatus(top, StatusType.Vulnerable, 1);
            })),
            CardRarity.Legend, up, () => Meteor(true));
    }

    // ---- 스킬 ----
    public static CardData Samsara(bool up = false)
    {
        int block0 = up ? B.SamsaraBlockUp : B.SamsaraBlock;
        float perCon = up ? B.SamsaraPerConUp : B.SamsaraPerCon;
        return Make(new CardData("samsara", "윤회의 불꽃", CardType.Skill, B.SamsaraCost, TargetType.Self,
            "방어도 " + block0 + " + 체력×" + perCon,
            Do(ctx =>
            {
                int block = block0 + Floor(ctx.User.Con * perCon);
                ctx.User.GainBlock(block);
                ctx.Battle.AddLog(ctx.User.Name + " 방어도 +" + block);
            })),
            CardRarity.Common, up, () => Samsara(true));
    }

    public static CardData PhoenixFeather(bool up = false)
    {
        int draw = up ? B.FeatherDrawUp : B.FeatherDraw;
        return Make(new CardData("phoenix_feather", "피닉스의 깃털", CardType.Skill, B.FeatherCost, TargetType.Self,
            draw + "장 뽑기\n홍염 +" + B.FeatherFlame + "\n소멸",
            new DrawEffect(draw), new GainManaEffect(B.FeatherFlame)) { Exhaust = true },
            CardRarity.Rare, up, () => PhoenixFeather(true));
    }

    // 불씨 폭발: 최대치 = 내 발화 최대치 (체력 스탯 15/20/30, 폭염의 코어 +20)
    public static CardData EmberBurst(bool up = false)
    {
        return Make(new CardData("ember_burst", "불씨 폭발", CardType.Skill, up ? B.EmberBurstCostUp : B.EmberBurstCost, TargetType.SingleEnemy,
            "대상 발화 2배\n(내 발화 최대치까지)",
            Do(ctx => ctx.Battle.SetBurn(ctx.Target, ctx.Target.GetStatus(StatusType.Burn) * 2, ctx.Battle.BurnCapOf(ctx.User)))) { ManaCost = B.EmberBurstFlame },
            CardRarity.Rare, up, () => EmberBurst(true));
    }

    public static CardData FlameRegen(bool up = false)
    {
        int loss = up ? B.FlameRegenHpLossUp : B.FlameRegenHpLoss;
        return Make(new CardData("flame_regen", "불꽃 재생", CardType.Skill, B.FlameRegenCost, TargetType.Self,
            "HP -" + loss + "\n에너지 +" + B.FlameRegenEnergy + ", 홍염 +" + B.FlameRegenFlame + "\n소멸",
            Do(ctx => ctx.Battle.PlayerLoseHp(ctx.User, loss)),
            new GainEnergyEffect(B.FlameRegenEnergy), new GainManaEffect(B.FlameRegenFlame)) { Exhaust = true },
            CardRarity.Rare, up, () => FlameRegen(true));
    }

    // 열기 응축: 다음 스킬 N장 에너지 -1 (장수로 쌓임)
    public static CardData HeatCondense(bool up = false)
    {
        int skills = up ? B.HeatSkillsUp : B.HeatSkills;
        return Make(new CardData("heat_condense", "열기 응축", CardType.Skill, B.HeatCost, TargetType.Self,
            B.HeatDraw + "장 뽑기\n이번 턴 다음 스킬 " + (skills > 1 ? skills + "장 " : "") + "\n에너지 -1",
            new DrawEffect(B.HeatDraw), Do(ctx => ctx.User.NextSkillDiscount += skills)) { ManaCost = B.HeatFlame },
            CardRarity.Hero, up, () => HeatCondense(true));
    }

    public static CardData GuardStance(bool up = false)
    {
        int block = up ? B.GuardStanceBlockUp : B.GuardStanceBlock;
        return Make(new CardData("guard_stance", "가드 스탠스", CardType.Skill, B.GuardStanceCost, TargetType.Self,
            "방어도 " + block + "\n다음 턴 시작 시\n에너지 +" + B.GuardStanceEnergy + ", " + B.GuardStanceDraw + "장 뽑기",
            new BlockEffect(block),
            Do(ctx => { ctx.User.GuardStanceEnergy += B.GuardStanceEnergy; ctx.User.GuardStanceDraw += B.GuardStanceDraw; })),
            CardRarity.Hero, up, () => GuardStance(true));
    }

    public static CardData FlameWall(bool up = false)
    {
        // 2026-11-15 (3-71): 멀티 전용 (MultiOnly). 혼자면 "아군 피해 -40%" 가 쓸모없고 "나는 1.5배" 단점만 남음
        CardData card = Make(new CardData("flame_wall", "화염 장벽", CardType.Skill, B.FlameWallCost, TargetType.Self,
            "방어도 " + B.FlameWallBlock + " + 체력×" + B.FlameWallPerCon + "\n이번 턴 아군 받는 피해 -" + (int)System.Math.Round(B.FlameWallReduction * 100) + "%" + (up ? "" : "\n나는 " + B.FlameWallSelfMultiplier + "배"),
            Do(ctx =>
            {
                int block = B.FlameWallBlock + ctx.User.Con * B.FlameWallPerCon;
                ctx.User.GainBlock(block);
                ctx.User.FlameWall = true;
                if (up) ctx.User.FlameWallNoPenalty = true;
                ctx.Battle.AddLog(ctx.User.Name + " 방어도 +" + block + ", [화염 장벽]");
            })),
            CardRarity.Hero, up, () => FlameWall(true));
        card.MultiOnly = true;   // 강화판도 이 함수(FlameWall(true))로 만들어지니 같이 붙음
        return card;
    }

    // ---- 파워 ----
    public static CardData SunWrath(bool up = false)
    {
        int burn = up ? B.SunWrathBurnUp : B.SunWrathBurn;
        return Make(new CardData("sun_wrath", "태양의 진노", CardType.Power, B.SunWrathCost, TargetType.Self,
            "내 턴에 HP를 잃거나\n홍염을 쓸 때마다\n모든 적 발화 " + burn,
            Do(ctx => ctx.User.SunWrathBurn += burn)),
            CardRarity.Rare, up, () => SunWrath(true));
    }

    public static CardData BurningThorns(bool up = false)
    {
        return Make(new CardData("burning_thorns", "작열하는 가시", CardType.Power, B.ThornsCost, TargetType.Self,
            "맞을 때마다 공격자 발화 " + B.ThornsBurn + "\n홍염 +" + B.ThornsFlame + (up ? " (횟수 제한 없음)" : " (턴당 최대 " + B.ThornsMaxPerTurn + "회)"),
            Do(ctx => { ctx.User.BurningThornsStacks++; if (up) ctx.User.ThornsUnlimited = true; })) { ManaCost = B.ThornsFlame },
            CardRarity.Rare, up, () => BurningThorns(true));
    }

    public static CardData FlameTraining(bool up = false)
    {
        return Make(new CardData("flame_training", "화염 단련", CardType.Power, up ? B.FlameTrainingCostUp : B.FlameTrainingCost, TargetType.Self,
            "기본 공격 카드\n피해 +" + B.FlameTrainingDmg + ", 발화 " + B.FlameTrainingBurn + "\n(중첩 최대 " + B.FlameTrainingMax + ")",
            Do(ctx => ctx.User.FlameTrainingStacks = System.Math.Min(ctx.User.FlameTrainingStacks + 1, B.FlameTrainingMax))),
            CardRarity.Common, up, () => FlameTraining(true));
    }

    public static CardData EternalEmber(bool up = false)
    {
        float ratio = up ? B.EternalEmberHpRatioUp : B.EternalEmberHpRatio;
        return Make(new CardData("eternal_ember", "영원한 불씨", CardType.Power, B.EternalEmberCost, TargetType.Self,
            "턴 시작 시 HP " + (int)System.Math.Round(ratio * 100) + "% 이하면\n홍염 +" + B.EternalEmberFlame + ", 방어도 +" + B.EternalEmberBlock,
            Do(ctx =>
            {
                ctx.User.EternalEmberStacks++;
                ctx.User.EternalEmberRatio = System.Math.Max(ctx.User.EternalEmberRatio, ratio);
            })),
            CardRarity.Hero, up, () => EternalEmber(true));
    }

    public static CardData PhoenixGuard(bool up = false)
    {
        float red = up ? B.PhoenixGuardReductionUp : B.PhoenixGuardReduction;
        return Make(new CardData("phoenix_guard", "피닉스의 가호", CardType.Power, B.PGuardCost, TargetType.Self,
            "방어도 " + B.PGuardBlock + "\n발화 걸린 적에게 받는 피해 -" + (int)System.Math.Round(red * 100) + "%\n매 턴 방어도 " + B.PGuardTurnBlock + "+체력×0.5",
            new BlockEffect(B.PGuardBlock),
            Do(ctx =>
            {
                ctx.User.PhoenixGuardStacks++;
                ctx.User.PhoenixGuardReduction = System.Math.Max(ctx.User.PhoenixGuardReduction, red);
            })),
            CardRarity.Legend, up, () => PhoenixGuard(true));
    }

    public static CardData PhoenixHeart(bool up = false)
    {
        float heal = up ? B.PhoenixHeartHealUp : B.PhoenixHeartHeal;
        return Make(new CardData("phoenix_heart", "피닉스의 심장", CardType.Power, B.PHeartCost, TargetType.Self,
            "(전투당 1회) 치명상 시\nHP 1로 생존, 최대 HP " + (int)System.Math.Round(heal * 100) + "% 회복\n홍염 " + B.PhoenixHeartFlame + "까지 충전",
            Do(ctx =>
            {
                if (ctx.User.PhoenixHeartUsed) return;
                ctx.User.PhoenixHeartReady = true;
                ctx.User.PhoenixHeartHeal = System.Math.Max(ctx.User.PhoenixHeartHeal, heal);
            })) { ManaCost = B.PHeartFlame },
            CardRarity.Legend, up, () => PhoenixHeart(true));
    }

    public static CardData UndyingFlame(bool up = false)
    {
        int max = up ? B.UndyingFlameMaxFlameUp : B.UndyingFlameMaxFlame;
        return Make(new CardData("undying_flame", "꺼지지 않는 불꽃", CardType.Power, B.UndyingFlameCost, TargetType.Self,
            "적 발화가 줄지 않음\n내 턴 종료 시 발화 걸린\n적 1당 홍염 +1 (최대 " + max + ")",
            Do(ctx =>
            {
                ctx.User.UndyingFlame = true;
                ctx.User.UndyingFlameMax = System.Math.Max(ctx.User.UndyingFlameMax, max);
            })),
            CardRarity.Legend, up, () => UndyingFlame(true));
    }

    public static List<CardData> PhoenixStarterDeck()
    {
        List<CardData> deck = new List<CardData>();
        // 3-143: 찌르기 4 + 피어스 2 + 방어 4 + 재의 소멸 1 = 11장 (피닉스 방패는 시작 덱에서 뺌, 보상으로는 나옴)
        for (int i = 0; i < 4; i++) deck.Add(PhoenixStrike());
        for (int i = 0; i < 2; i++) deck.Add(PhoenixPierce());
        for (int i = 0; i < 4; i++) deck.Add(PhoenixDefend());
        deck.Add(AshExtinction());   // 2026-11-09: 찌르기 1장 대신. 홍염을 쓰는 카드가 없어 홍염이 넘치던 문제
        return deck;
    }

    public static List<CardData> PhoenixTestCards()
    {
        return new List<CardData>
        {
            AshExtinction(), Rush(), GuardBreak(), Samsara(), FlameTraining(),
            Sunspot(), FlameLance(), Brand(), PhoenixFeather(), EmberBurst(), FlameRegen(), SunWrath(), BurningThorns(),
            BlueFlame(), HeatCondense(), GuardStance(), FlameWall(), EternalEmber(),
            PhoenixNova(), Meteor(), PhoenixGuard(), PhoenixHeart(), UndyingFlame()
        };
    }

    // ======================================================================
    // 마도사 (AP) — 마나 0/30 (민첩으로 시작·회복), 동결 → 동상, 감전, [벼락]
    // ======================================================================

    // ---- 기본 카드 ----
    public static CardData IceBullet(bool up = false)
    {
        int dmg = up ? B.IceBulletDmgUp : B.IceBulletDmg;
        int mana = up ? B.IceBulletManaUp : B.IceBulletMana;
        return Make(new CardData("ice_bullet", "빙결 탄환", CardType.Attack, B.IceBulletCost, TargetType.SingleEnemy,
            dmg + " 피해, 동결 " + B.IceBulletFreeze + "\n마나 +" + mana,
            new DamageEffect(dmg, DamageKind.AP),
            Do(ctx => ctx.Battle.AddFreeze(ctx.User, ctx.Target, B.IceBulletFreeze)),
            new GainManaEffect(mana)) { IsBasicAttack = true },
            CardRarity.Basic, up, () => IceBullet(true));
    }
    public static CardData Spark(bool up = false)
    {
        int dmg = up ? B.SparkDmgUp : B.SparkDmg;
        int mana = up ? B.SparkManaUp : B.SparkMana;
        return Make(new CardData("spark", "스파크", CardType.Attack, B.SparkCost, TargetType.SingleEnemy,
            "[벼락] " + dmg + " 피해\n감전 스택 " + B.SparkShock + ", 마나 +" + mana,
            new DamageEffect(dmg, DamageKind.AP),
            Do(ctx => ctx.Battle.AddShock(ctx.User, ctx.Target, B.SparkShock)),
            new GainManaEffect(mana)) { IsBasicAttack = true, IsLightning = true },
            CardRarity.Basic, up, () => Spark(true));
    }
    public static CardData MageDefend(bool up = false)
    {
        int block = up ? B.MDefendBlockUp : B.MDefendBlock;
        int mana = up ? B.MDefendManaUp : B.MDefendMana;
        return Make(new CardData("m_defend", "방어", CardType.Skill, 1, TargetType.Self,
            "방어도 " + block + "\n마나 +" + mana,
            new BlockEffect(block), new GainManaEffect(mana)),
            CardRarity.Basic, up, () => MageDefend(true));
    }
    public static CardData IceVeil(bool up = false)
    {
        int block = up ? B.IceVeilBlockUp : B.IceVeilBlock;
        return Make(new CardData("ice_veil", "얼음 장막", CardType.Skill, B.IceVeilCost, TargetType.Self,
            "방어도 " + block,
            new BlockEffect(block)) { ManaCost = B.IceVeilMana },
            CardRarity.Basic, up, () => IceVeil(true));
    }

    static bool Frozen(EnemyCombatant e) { return e.FreezeStacks > 0 || e.HasStatus(StatusType.Frostbite); }

    // ---- 공격 ----
    public static CardData ColdWave(bool up = false)
    {
        int dmg = up ? B.ColdWaveDmgUp : B.ColdWaveDmg;
        return Make(new CardData("cold_wave", "한파", CardType.Attack, B.ColdWaveCost, TargetType.AllEnemies,
            "모든 적 " + dmg + " 피해\n동결 1",
            Do(ctx =>
            {
                foreach (EnemyCombatant e in ctx.Battle.LivingEnemies())
                {
                    HitEnemy(ctx, e, dmg, DamageKind.AP);
                    ctx.Battle.AddFreeze(ctx.User, e, 1);
                }
            })) { ManaCost = B.ColdWaveMana },
            CardRarity.Common, up, () => ColdWave(true));
    }

    // 고드름: 1회마다 무작위 적. 맞힌 적에게 동결 1 (카드 한 장당 최대 2)
    public static CardData Icicle(bool up = false)
    {
        int dmg = up ? B.IcicleDmgUp : B.IcicleDmg;
        return Make(new CardData("icicle", "고드름", CardType.Attack, B.IcicleCost, TargetType.AllEnemies,
            "무작위 적 " + dmg + " 피해 x" + B.IcicleHits + "\n맞힐 때 동결 1\n(최대 " + B.IcicleMaxFreeze + ")",
            Do(ctx =>
            {
                int frozen = 0;
                for (int i = 0; i < B.IcicleHits; i++)
                {
                    EnemyCombatant e = ctx.Battle.RandomLivingEnemy();
                    if (e == null) break;
                    HitEnemy(ctx, e, dmg, DamageKind.AP);
                    if (frozen < B.IcicleMaxFreeze && Alive(e)) { ctx.Battle.AddFreeze(ctx.User, e, 1); frozen++; }
                }
            })) { ManaCost = B.IcicleMana },
            CardRarity.Common, up, () => Icicle(true));
    }

    public static CardData PlasmaBolt(bool up = false)
    {
        int dmg = up ? B.PlasmaDmgUp : B.PlasmaDmg;
        return Make(new CardData("plasma_bolt", "플라즈마 볼트", CardType.Attack, B.PlasmaCost, TargetType.SingleEnemy,
            "[벼락] " + dmg + " 피해\n감전 스택 " + B.PlasmaShock + "\n이미 감전이면 마나 +" + B.PlasmaMana,
            Do(ctx =>
            {
                bool shocked = ctx.Target.HasStatus(StatusType.Shock);
                Hit(ctx, dmg, DamageKind.AP);
                ctx.Battle.AddShock(ctx.User, ctx.Target, B.PlasmaShock);
                if (shocked) { ctx.User.GainMana(B.PlasmaMana); ctx.Battle.AddLog("  마나 +" + B.PlasmaMana); }
            })) { IsLightning = true },
            CardRarity.Common, up, () => PlasmaBolt(true));
    }

    public static CardData MageThunderStrike(bool up = false)
    {
        int chain = up ? B.MThunderStrikeChainUp : B.MThunderStrikeChain;
        return Make(new CardData("m_thunder_strike", "뇌전 일격", CardType.Attack, B.MThunderStrikeCost, TargetType.SingleEnemy,
            "[벼락] " + B.MThunderStrikeDmg + " 피해\n다른 적 연쇄 " + chain + "\n마나 +" + B.MThunderStrikeMana,
            Do(ctx =>
            {
                EnemyCombatant main = ctx.Target;
                Hit(ctx, B.MThunderStrikeDmg, DamageKind.AP);
                foreach (EnemyCombatant e in ctx.Battle.LivingEnemies())
                    if (e != main) HitEnemy(ctx, e, chain, DamageKind.AP, " [연쇄]");
            }),
            new GainManaEffect(B.MThunderStrikeMana)) { IsLightning = true },
            CardRarity.Common, up, () => MageThunderStrike(true));
    }

    public static CardData StaticDischarge(bool up = false)
    {
        return Make(new CardData("static_discharge", "정전기 방출", CardType.Attack, B.StaticCost, TargetType.SingleEnemy,
            "[벼락] " + B.StaticDmg + " 피해\n1장 뽑기\n감전 스택 1",
            new DamageEffect(B.StaticDmg, DamageKind.AP), new DrawEffect(1),
            Do(ctx => ctx.Battle.AddShock(ctx.User, ctx.Target, 1))) { ManaCost = up ? B.StaticManaUp : B.StaticMana, IsLightning = true },
            CardRarity.Common, up, () => StaticDischarge(true));
    }

    public static CardData CurrentArrow(bool up = false)
    {
        int dmg = up ? B.CurrentArrowDmgUp : B.CurrentArrowDmg;
        return Make(new CardData("current_arrow", "전류 화살", CardType.Attack, B.CurrentArrowCost, TargetType.SingleEnemy,
            "[벼락] " + dmg + " 피해\n감전 스택 " + B.CurrentArrowShock + "\n마나 +" + B.CurrentArrowGain,
            new DamageEffect(dmg, DamageKind.AP),
            Do(ctx => ctx.Battle.AddShock(ctx.User, ctx.Target, B.CurrentArrowShock)),
            new GainManaEffect(B.CurrentArrowGain)) { ManaCost = B.CurrentArrowMana, IsLightning = true },
            CardRarity.Rare, up, () => CurrentArrow(true));
    }

    public static CardData ChainLightning(bool up = false)
    {
        int dmg = up ? B.ChainLightningDmgUp : B.ChainLightningDmg;
        return Make(new CardData("chain_lightning", "연쇄 번개", CardType.Attack, B.ChainLightningCost, TargetType.AllEnemies,
            "[벼락] 무작위 적 " + dmg + " 피해\nx" + B.ChainLightningHits + " (감전된 적이 있으면 x" + (B.ChainLightningHits + 1) + ")",
            Do(ctx =>
            {
                bool anyShocked = false;
                foreach (EnemyCombatant e in ctx.Battle.LivingEnemies()) if (e.HasStatus(StatusType.Shock)) anyShocked = true;
                int hits = anyShocked ? B.ChainLightningHits + 1 : B.ChainLightningHits;
                for (int i = 0; i < hits; i++)
                {
                    EnemyCombatant e = ctx.Battle.RandomLivingEnemy();
                    if (e == null) break;
                    HitEnemy(ctx, e, dmg, DamageKind.AP);
                }
            })) { ManaCost = B.ChainLightningMana, IsLightning = true },
            CardRarity.Rare, up, () => ChainLightning(true));
    }

    public static CardData IceSpike(bool up = false)
    {
        int bonus = up ? B.IceSpikeBonusUp : B.IceSpikeBonus;
        return Make(new CardData("ice_spike", "얼음 송곳", CardType.Attack, B.IceSpikeCost, TargetType.SingleEnemy,
            B.IceSpikeDmg + " 피해\n동결 스택·동상이면\n+" + bonus,
            Do(ctx => Hit(ctx, B.IceSpikeDmg + (Frozen(ctx.Target) ? bonus : 0), DamageKind.AP, 0f, false, Frozen(ctx.Target) ? " [얼음 +" + bonus + "]" : ""))) { ManaCost = B.IceSpikeMana },
            CardRarity.Hero, up, () => IceSpike(true));
    }

    public static CardData IceShatter(bool up = false)
    {
        int per = up ? B.ShatterPerStackUp : B.ShatterPerStack;
        return Make(new CardData("ice_shatter", "빙결 파쇄", CardType.Attack, B.ShatterCost, TargetType.AllEnemies,
            "모든 적 " + B.ShatterDmg + " 피해\n동결 스택 소모\n1당 +" + per,
            Do(ctx =>
            {
                foreach (EnemyCombatant e in ctx.Battle.LivingEnemies())
                {
                    int n = e.FreezeStacks;
                    e.FreezeStacks = 0;
                    HitEnemy(ctx, e, B.ShatterDmg + n * per, DamageKind.AP, n > 0 ? " [동결 " + n + " 파쇄]" : "");
                }
            })) { ManaCost = B.ShatterMana },
            CardRarity.Hero, up, () => IceShatter(true));
    }

    public static CardData Thunderbolt(bool up = false)
    {
        int dmg = up ? B.ThunderboltDmgUp : B.ThunderboltDmg;
        return Make(new CardData("thunderbolt", "천둥 벼락", CardType.Attack, B.ThunderboltCost, TargetType.SingleEnemy,
            "[벼락] " + dmg + " 피해\n이번 턴 [벼락] 카드\n1장당 +" + B.ThunderboltPerCard + ", 감전 스택 " + B.ThunderboltShock,
            Do(ctx =>
            {
                int before = ctx.User.LightningPlayedThisTurn;
                Hit(ctx, dmg + before * B.ThunderboltPerCard, DamageKind.AP, 0f, false, before > 0 ? " [벼락 " + before + "장]" : "");
                ctx.Battle.AddShock(ctx.User, ctx.Target, B.ThunderboltShock);
            })) { ManaCost = B.ThunderboltMana, IsLightning = true },
            CardRarity.Hero, up, () => Thunderbolt(true));
    }

    public static CardData GlacierCut(bool up = false)
    {
        return Make(new CardData("glacier_cut", "빙하절단", CardType.Attack, B.GlacierCutCost, TargetType.SingleEnemy,
            B.GlacierCutDmg + " 피해\n즉시 [동상] (행동 취소)\n보스: 이번 턴 피해 -" + (int)System.Math.Round(B.GlacierCutBossReduction * 100) + "%",
            Do(ctx =>
            {
                Hit(ctx, B.GlacierCutDmg, DamageKind.AP);
                if (!Alive(ctx.Target)) return;
                if (ctx.Target.IsBoss)
                {
                    ctx.Target.HalfDamage = true;
                    ctx.Battle.AddLog("  " + ctx.Target.Name + " 이번 적 차례 공격 피해 -" + (int)System.Math.Round(B.GlacierCutBossReduction * 100) + "%");
                }
                else ctx.Battle.TriggerFrostbite(ctx.Target, ctx.User);
            })) { ManaCost = up ? B.GlacierCutManaUp : B.GlacierCutMana },
            CardRarity.Legend, up, () => GlacierCut(true));
    }

    public static CardData EternalSnow(bool up = false)
    {
        return Make(new CardData("eternal_snow", "만년설", CardType.Attack, B.EternalSnowCost, TargetType.SingleEnemy,
            B.EternalSnowDmg + " 피해\n동결 스택·동상이면\n다음 적 차례 방어도 0",
            Do(ctx =>
            {
                bool frozen = Frozen(ctx.Target);
                Hit(ctx, B.EternalSnowDmg, DamageKind.AP);
                if (frozen && Alive(ctx.Target))
                {
                    ctx.Target.BlockLocked = true;
                    ctx.Battle.AddLog("  " + ctx.Target.Name + " 다음 적 차례 동안 방어도 0 고정");
                }
            })) { ManaCost = up ? B.EternalSnowManaUp : B.EternalSnowMana },
            CardRarity.Legend, up, () => EternalSnow(true));
    }

    public static CardData AbsoluteZero(bool up = false)
    {
        return Make(new CardData("absolute_zero", "절대영도", CardType.Attack, up ? B.AbsZeroCostUp : B.AbsZeroCost, TargetType.SingleEnemy,
            B.AbsZeroDmg + " 피해, 취약 2\n동결 스택이 기준보다\n1 낮으면 즉시 [동상]",
            Do(ctx =>
            {
                Hit(ctx, B.AbsZeroDmg, DamageKind.AP);
                if (!Alive(ctx.Target)) return;
                ctx.Battle.ApplyStatus(ctx.Target, StatusType.Vulnerable, 2);
                if (ctx.Target.FreezeStacks == ctx.Target.FreezeThreshold - 1) ctx.Battle.TriggerFrostbite(ctx.Target, ctx.User);
            })) { ManaCost = B.AbsZeroMana },
            CardRarity.Legend, up, () => AbsoluteZero(true));
    }

    public static CardData LightningFall(bool up = false)
    {
        int per = up ? B.LightningFallPerManaUp : B.LightningFallPerMana;
        return Make(new CardData("lightning_fall", "낙뢰", CardType.Attack, B.LightningFallCost, TargetType.SingleEnemy,
            "[벼락] 마나 전부 소모\n" + B.LightningFallDmg + " + 마나 1당 " + per + "\n피해",
            Do(ctx => Hit(ctx, B.LightningFallDmg + ctx.ManaSpent * per, DamageKind.AP, 0f, false, " [마나 " + ctx.ManaSpent + "]"))) { UsesAllMana = true, IsLightning = true },
            CardRarity.Legend, up, () => LightningFall(true));
    }

    // ---- 스킬 ----
    public static CardData CurrentBarrier(bool up = false)
    {
        int block = up ? B.CurrentBarrierBlockUp : B.CurrentBarrierBlock;
        return Make(new CardData("current_barrier", "전류 방벽", CardType.Skill, B.CurrentBarrierCost, TargetType.Self,
            "방어도 " + block + "\n이번 턴 나를 때린 적\n감전 스택 1",
            new BlockEffect(block), Do(ctx => ctx.User.ShockBarrier = true)),
            CardRarity.Common, up, () => CurrentBarrier(true));
    }

    public static CardData WeakSpot(bool up = false)
    {
        int t = up ? B.WeakSpotTurnsUp : B.WeakSpotTurns;
        return Make(new CardData("weak_spot", "약점 포착", CardType.Skill, B.WeakSpotCost, TargetType.SingleEnemy,
            "약화 " + t + ", 취약 " + t + "\n감전 스택 1",
            new ApplyStatusEffect(StatusType.Weak, t), new ApplyStatusEffect(StatusType.Vulnerable, t),
            Do(ctx => ctx.Battle.AddShock(ctx.User, ctx.Target, 1))),
            CardRarity.Common, up, () => WeakSpot(true));
    }

    public static CardData ManaAmp(bool up = false)
    {
        int mana = up ? B.ManaAmpManaUp : B.ManaAmpMana;
        return Make(new CardData("mana_amp", "마력 증폭", CardType.Skill, B.ManaAmpCost, TargetType.Self,
            "방어도 " + B.ManaAmpBlock + "\n마나 +" + mana + "\n소멸",
            new BlockEffect(B.ManaAmpBlock), new GainManaEffect(mana)) { Exhaust = true },
            CardRarity.Common, up, () => ManaAmp(true));
    }

    public static CardData ManaShield(bool up = false)
    {
        int block = up ? B.ManaShieldBlockUp : B.ManaShieldBlock;
        return Make(new CardData("mana_shield", "마나 실드", CardType.Skill, B.ManaShieldCost, TargetType.Self,
            "방어도 " + block + "\n다음 스킬 에너지 -1\n소멸",
            new BlockEffect(block), Do(ctx => ctx.User.NextSkillDiscount += 1)) { ManaCost = B.ManaShieldMana, Exhaust = true },
            CardRarity.Rare, up, () => ManaShield(true));
    }

    public static CardData Ionize(bool up = false)
    {
        int shock = up ? B.IonizeShockUp : B.IonizeShock;
        return Make(new CardData("ionize", "이온화", CardType.Skill, B.IonizeCost, TargetType.AllEnemies,
            "모든 적 감전 스택 " + shock + "\n이번 턴 다음 [벼락] 카드\n마나 0 (낙뢰 제외)",
            Do(ctx =>
            {
                foreach (EnemyCombatant e in ctx.Battle.LivingEnemies()) ctx.Battle.AddShock(ctx.User, e, shock);
                ctx.User.NextLightningFree = true;
            })),
            CardRarity.Rare, up, () => Ionize(true));
    }

    public static CardData ManaCycle(bool up = false)
    {
        int energy = up ? B.ManaCycleEnergyUp : B.ManaCycleEnergy;
        return Make(new CardData("mana_cycle", "마나 순환", CardType.Skill, B.ManaCycleCost, TargetType.Self,
            "이번 턴 카드 " + B.ManaCycleNeed + "장 이상 썼으면\n에너지 +" + energy + ", 1장 뽑기\n마나 +" + B.ManaCycleMana + "\n소멸",
            Do(ctx =>
            {
                if (ctx.User.CardsPlayedThisTurn >= B.ManaCycleNeed)
                {
                    ctx.User.Energy += energy;
                    ctx.Battle.Draw(ctx.User, 1);
                    ctx.Battle.AddLog("  마나 순환 성공: 에너지 +" + energy + ", 1장 뽑기");
                }
            }),
            new GainManaEffect(B.ManaCycleMana)) { Exhaust = true },
            CardRarity.Rare, up, () => ManaCycle(true));
    }

    public static CardData ManaRebuild(bool up = false)
    {
        int block = up ? B.RebuildBlockUp : B.RebuildBlock;
        int gain = up ? B.RebuildGainUp : B.RebuildGain;
        return Make(new CardData("mana_rebuild", "마력 재구성", CardType.Skill, B.RebuildCost, TargetType.Self,
            "방어도 " + block + ", 마나 +" + gain + "\n1장 뽑기",
            new BlockEffect(block), new GainManaEffect(gain), new DrawEffect(1)) { ManaCost = B.RebuildMana },
            CardRarity.Hero, up, () => ManaRebuild(true));
    }

    public static CardData LimitBreak(bool up = false)
    {
        int block0 = up ? B.LimitBreakBlockUp : B.LimitBreakBlock;
        return Make(new CardData("limit_break", "한계 돌파", CardType.Skill, B.LimitBreakCost, TargetType.Self,
            "방어도 " + block0 + "\nHP " + (int)System.Math.Round(B.LimitBreakHp * 100) + "% 이하면 2배",
            Do(ctx =>
            {
                int block = block0 * (ctx.User.Hp <= ctx.User.MaxHp * B.LimitBreakHp ? 2 : 1);
                ctx.User.GainBlock(block);
                ctx.Battle.AddLog(ctx.User.Name + " 방어도 +" + block);
            })) { ManaCost = B.LimitBreakMana },
            CardRarity.Hero, up, () => LimitBreak(true));
    }

    public static CardData GlacierBarrier(bool up = false)
    {
        int block = up ? B.GlacierBarrierBlockUp : B.GlacierBarrierBlock;
        float red = up ? B.GlacierBarrierReductionUp : B.GlacierBarrierReduction;
        return Make(new CardData("glacier_barrier", "빙하 결계", CardType.Skill, B.GlacierBarrierCost, TargetType.Self,
            "방어도 " + block + "\n이번 턴 받는 피해 -" + (int)System.Math.Round(red * 100) + "%",
            new BlockEffect(block),
            Do(ctx => ctx.User.GlacierBarrier = System.Math.Max(ctx.User.GlacierBarrier, red))) { ManaCost = B.GlacierBarrierMana },
            CardRarity.Hero, up, () => GlacierBarrier(true));
    }

    public static CardData CryoSleep(bool up = false)
    {
        int gain = up ? B.CryoGainUp : B.CryoGain;
        return Make(new CardData("cryo_sleep", "냉동 수면", CardType.Skill, B.CryoCost, TargetType.Self,
            "최대 HP 10% 회복\n마나 +" + gain + "\n소멸",
            Do(ctx =>
            {
                int healed = ctx.User.Heal(DamageCalculator.RoundHalfUp(ctx.User.MaxHp * B.CryoHeal));
                ctx.Battle.AddLog("  HP +" + healed);
            }),
            new GainManaEffect(gain)) { ManaCost = B.CryoMana, Exhaust = true },
            CardRarity.Hero, up, () => CryoSleep(true));
    }

    // ---- 파워 ----
    public static CardData Hail(bool up = false)
    {
        int dmg = up ? B.HailDamageUp : B.HailDamage;
        return Make(new CardData("hail", "우박", CardType.Power, B.HailCost, TargetType.Self,
            "적이 [동상]이 될 때마다\n그 적에게 " + dmg + " 피해",
            Do(ctx => ctx.User.HailDamage += dmg)) { ManaCost = B.HailMana },
            CardRarity.Rare, up, () => Hail(true));
    }

    public static CardData StormField(bool up = false)
    {
        int dmg = up ? B.StormFieldDamageUp : B.StormFieldDamage;
        return Make(new CardData("storm_field", "뇌전 지대", CardType.Power, B.StormFieldCost, TargetType.Self,
            "[벼락] 내 턴 종료마다\n무작위 적 " + dmg + " 피해\n마나 +" + B.StormFieldMana,
            Do(ctx => ctx.User.StormFieldHits.Add(dmg))) { ManaCost = B.StormFieldMana3, IsLightning = true },
            CardRarity.Rare, up, () => StormField(true));
    }

    public static CardData Blizzard(bool up = false)
    {
        return Make(new CardData("blizzard", "눈보라", CardType.Power, B.BlizzardCost, TargetType.Self,
            (up ? "매 턴" : "2턴마다") + " 모든 적\n동결 1",
            Do(ctx => { ctx.User.BlizzardStacks++; if (up) ctx.User.BlizzardEveryTurn = true; })) { ManaCost = B.BlizzardMana },
            CardRarity.Rare, up, () => Blizzard(true));
    }

    public static CardData StaticField(bool up = false)
    {
        int block = up ? B.StaticFieldBlockUp : B.StaticFieldBlock;
        return Make(new CardData("static_field", "정전기 역장", CardType.Power, B.StaticFieldCost, TargetType.Self,
            "감전 스택을 걸 때마다\n방어도 " + block,
            Do(ctx => ctx.User.StaticFieldBlock += block)) { ManaCost = B.StaticFieldMana },
            CardRarity.Rare, up, () => StaticField(true));
    }

    public static CardData FrostPrison(bool up = false)
    {
        return Make(new CardData("frost_prison", "혹한의 감옥", CardType.Power, up ? B.FrostPrisonCostUp : B.FrostPrisonCost, TargetType.Self,
            "동결을 걸 때마다 1장 뽑기\n(턴당 최대 " + B.FrostPrisonMaxDraw + ")\n동상 상태 적은 적 차례 끝에 " + B.FrostPrisonDamage + " 피해",
            Do(ctx => ctx.User.FrostPrisonStacks++)) { ManaCost = B.FrostPrisonMana },
            CardRarity.Hero, up, () => FrostPrison(true));
    }

    public static CardData ThunderGod(bool up = false)
    {
        int bonus = up ? B.ThunderGodBonusUp : B.ThunderGodBonus;
        return Make(new CardData("thunder_god", "뇌신 권능", CardType.Power, B.ThunderGodCost, TargetType.Self,
            "[벼락] 카드를 쓸 때마다\n이번 전투 [벼락] 피해 +" + bonus + "\n(최대 " + B.ThunderGodMax + "번)",
            Do(ctx =>
            {
                ctx.User.ThunderGodActive = true;
                ctx.User.ThunderGodBonus = System.Math.Max(ctx.User.ThunderGodBonus, bonus);
            })) { ManaCost = B.ThunderGodMana },
            CardRarity.Hero, up, () => ThunderGod(true));
    }

    public static CardData ManaRelease(bool up = false)
    {
        return Make(new CardData("mana_release", "마력해방", CardType.Power, up ? B.ManaReleaseCostUp : B.ManaReleaseCost, TargetType.Self,
            "이번 전투 동안\n턴 시작 에너지 +1\n매 턴 마나 +" + B.ManaReleaseRegen,
            Do(ctx => ctx.User.ManaReleaseStacks++)) { ManaCost = B.ManaReleaseMana },
            CardRarity.Legend, up, () => ManaRelease(true));
    }

    public static List<CardData> FrostMageStarterDeck()
    {
        List<CardData> deck = new List<CardData>();
        // 3-143: 빙결 탄환 4 + 방어 4 + 스파크 2 + 얼음 장막 1 = 11장
        for (int i = 0; i < 4; i++) deck.Add(IceBullet());
        for (int i = 0; i < 4; i++) deck.Add(MageDefend());
        for (int i = 0; i < 2; i++) deck.Add(Spark());
        deck.Add(IceVeil());
        return deck;
    }

    public static List<CardData> FrostMageTestCards()
    {
        return new List<CardData>
        {
            ColdWave(), Icicle(), PlasmaBolt(), MageThunderStrike(), StaticDischarge(), CurrentBarrier(), WeakSpot(), ManaAmp(),
            CurrentArrow(), ChainLightning(), ManaShield(), Ionize(), ManaCycle(), Hail(), StormField(), Blizzard(), StaticField(),
            IceSpike(), IceShatter(), Thunderbolt(), ManaRebuild(), LimitBreak(), GlacierBarrier(), CryoSleep(), FrostPrison(), ThunderGod(),
            GlacierCut(), EternalSnow(), AbsoluteZero(), LightningFall(), ManaRelease()
        };
    }


    // ======================================================================
    // 스나이퍼 (2026-11-15, 팀원 기획서). 자원 = 탄환 (ManaCost 칸이 탄환 소모)
    //   [조준] = 다음 공격 카드 피해 +50% × 개수 (BattleSystem 이 처리)
    //   [표식] = 적에게 쌓임 (최대 10). 1개당 그 적이 받는 공격 피해 +1. ctx.Battle.AddMarks 로 검
    // ======================================================================
    static void Mark(EffectContext ctx, int n) { ctx.Battle.AddMarks(ctx.User, ctx.Target, n); }

    // ---- 기본 ----
    public static CardData SniperStrike(bool up = false)
    {
        int dmg = up ? B.SnStrikeDmgUp : B.SnStrikeDmg;
        return Make(new CardData("sn_strike", "샷", CardType.Attack, 1, TargetType.SingleEnemy,
            dmg + " 피해\n탄환 +" + B.SnStrikeBullet,
            new DamageEffect(dmg, DamageKind.AD), new GainManaEffect(B.SnStrikeBullet)) { IsBasicAttack = true },
            CardRarity.Basic, up, () => SniperStrike(true));
    }
    public static CardData SniperDefend(bool up = false)
    {
        int block = up ? B.SnDefendBlockUp : B.SnDefendBlock;
        return Make(new CardData("sn_defend", "장전", CardType.Skill, 1, TargetType.Self,
            "방어도 " + block + "\n탄환 +" + B.SnDefendBullet,
            new BlockEffect(block), new GainManaEffect(B.SnDefendBullet)),
            CardRarity.Basic, up, () => SniperDefend(true));
    }

    // ---- 공격 ----
    public static CardData Shot(bool up = false)
    {
        int dmg = up ? B.ShotDmgUp : B.ShotDmg, mark = up ? B.ShotMarkUp : B.ShotMark;
        return Make(new CardData("sn_shot", "사격", CardType.Attack, 1, TargetType.SingleEnemy,
            dmg + " 피해\n[표식] " + mark,
            new DamageEffect(dmg, DamageKind.AD), Do(ctx => Mark(ctx, mark))) { ManaCost = B.ShotBullet },
            CardRarity.Common, up, () => Shot(true));
    }
    public static CardData Headshot(bool up = false)
    {
        int dmg = up ? B.HeadshotDmgUp : B.HeadshotDmg;
        return Make(new CardData("sn_headshot", "헤드 샷", CardType.Attack, B.HeadshotCost, TargetType.SingleEnemy,
            dmg + " 피해\n[조준] 상태면 피해 2배\n처치하면 탄환 +" + B.HeadshotKillBullets,
            Do(ctx =>
            {
                int d = ctx.User.AimedThisCard ? dmg * 2 : dmg;
                Hit(ctx, d, DamageKind.AD);
                if (ctx.Target != null && ctx.Target.IsDead) { ctx.User.GainMana(B.HeadshotKillBullets); ctx.Battle.AddLog("  헤드 샷 처치: 탄환 +" + B.HeadshotKillBullets); }
            })) { ManaCost = B.HeadshotBullet },
            CardRarity.Hero, up, () => Headshot(true));
    }
    public static CardData Incendiary(bool up = false)
    {
        int dmg = up ? B.IncendiaryDmgUp : B.IncendiaryDmg, mark = B.IncendiaryMark, weak = up ? B.IncendiaryWeakUp : B.IncendiaryWeak;
        return Make(new CardData("sn_incendiary", "소이탄", CardType.Attack, 1, TargetType.SingleEnemy,
            dmg + " 피해\n약화 " + weak + ", [표식] " + mark,
            new DamageEffect(dmg, DamageKind.AD), new ApplyStatusEffect(StatusType.Weak, weak), Do(ctx => Mark(ctx, mark))) { ManaCost = B.IncendiaryBullet },
            CardRarity.Common, up, () => Incendiary(true));
    }
    public static CardData APRound(bool up = false)
    {
        int dmg = up ? B.APRoundDmgUp : B.APRoundDmg;
        return Make(new CardData("sn_ap_round", "철갑탄", CardType.Attack, 1, TargetType.SingleEnemy,
            "대상의 방어도를 전부 부순 뒤\n" + dmg + " 피해",
            Do(ctx =>
            {
                if (Alive(ctx.Target) && ctx.Target.Block > 0) { ctx.Battle.AddLog("  " + ctx.Target.Name + " 방어도 " + ctx.Target.Block + " 파괴"); ctx.Target.Block = 0; }
                Hit(ctx, dmg, DamageKind.AD);
            })) { ManaCost = B.APRoundBullet },
            CardRarity.Rare, up, () => APRound(true));
    }
    public static CardData Spray(bool up = false)
    {
        int bas = up ? B.SprayBaseUp : B.SprayBase, per = up ? B.SprayPerBulletUp : B.SprayPerBullet;
        return Make(new CardData("sn_spray", "난사", CardType.Attack, B.SprayCost, TargetType.AllEnemies,
            "탄환을 전부 쓰고\n모든 적에게 " + bas + " 피해\n+ 쓴 탄환 1개당 " + per,
            Do(ctx =>
            {
                int d = bas + per * ctx.ManaSpent;
                foreach (EnemyCombatant e in ctx.Battle.LivingEnemies()) HitEnemy(ctx, e, d, DamageKind.AD);
            })) { UsesAllMana = true },
            CardRarity.Hero, up, () => Spray(true));
    }
    public static CardData DoomShot(bool up = false)
    {
        int dmg = up ? B.DoomDmgUp : B.DoomDmg, per = up ? B.DoomPerMarkUp : B.DoomPerMark;
        return Make(new CardData("sn_doom", "종말의 일격", CardType.Attack, B.DoomCost, TargetType.SingleEnemy,
            dmg + " 피해 (방어도 무시)\n대상의 [표식] 1개당 +" + per,
            Do(ctx => { int marks = Alive(ctx.Target) ? ctx.Target.Marks : 0; Hit(ctx, dmg + per * marks, DamageKind.AD, 0f, true); })) { ManaCost = B.DoomBullet },
            CardRarity.Legend, up, () => DoomShot(true));
    }
    public static CardData RedFlash(bool up = false)
    {
        int dmg = up ? B.RedFlashDmgUp : B.RedFlashDmg;
        return Make(new CardData("sn_red_flash", "붉은 섬광", CardType.Attack, 1, TargetType.AllEnemies,
            "모든 적에게 " + dmg + " 피해\n약화 " + (up ? B.RedFlashWeakUp : B.RedFlashWeak),
            new DamageEffect(dmg, DamageKind.AD, true), new ApplyStatusEffect(StatusType.Weak, up ? B.RedFlashWeakUp : B.RedFlashWeak, true)) { ManaCost = B.RedFlashBullet },
            CardRarity.Hero, up, () => RedFlash(true));
    }
    public static CardData PierceShot(bool up = false)
    {
        int dmg = up ? B.PierceShotDmgUp : B.PierceShotDmg;
        return Make(new CardData("sn_pierce", "관통탄", CardType.Attack, 1, TargetType.SingleEnemy,
            dmg + " 피해 (방어도 무시)",
            Do(ctx => Hit(ctx, dmg, DamageKind.AD, 0f, true))) { ManaCost = B.PierceShotBullet },
            CardRarity.Common, up, () => PierceShot(true));
    }
    public static CardData RapidFire(bool up = false)
    {
        int dmg = up ? B.RapidDmgUp : B.RapidDmg;
        return Make(new CardData("sn_rapid", "속사", CardType.Attack, 0, TargetType.SingleEnemy,
            dmg + " 피해 × " + B.RapidHits,
            new DamageEffect(dmg, DamageKind.AD, false, B.RapidHits)) { ManaCost = B.RapidBullet },
            CardRarity.Rare, up, () => RapidFire(true));
    }
    public static CardData VitalShot(bool up = false)
    {
        int dmg = up ? B.VitalDmgUp : B.VitalDmg;
        return Make(new CardData("sn_vital", "급소 노리기", CardType.Attack, 1, TargetType.SingleEnemy,
            dmg + " 피해\n[표식] " + (up ? B.VitalMarkUp : B.VitalMark) + ", 취약 " + B.VitalVuln,
            new DamageEffect(dmg, DamageKind.AD), Do(ctx => Mark(ctx, up ? B.VitalMarkUp : B.VitalMark)), new ApplyStatusEffect(StatusType.Vulnerable, B.VitalVuln)) { ManaCost = B.VitalBullet },
            CardRarity.Common, up, () => VitalShot(true));
    }
    public static CardData SuppressShot(bool up = false)
    {
        int dmg = up ? B.SuppressDmgUp : B.SuppressDmg;
        return Make(new CardData("sn_suppress", "견제 사격", CardType.Attack, 1, TargetType.SingleEnemy,
            dmg + " 피해\n[표식] " + (up ? B.SuppressMarkUp : B.SuppressMark),
            new DamageEffect(dmg, DamageKind.AD), Do(ctx => Mark(ctx, up ? B.SuppressMarkUp : B.SuppressMark))) { ManaCost = B.SuppressBullet },
            CardRarity.Common, up, () => SuppressShot(true));
    }
    public static CardData Grenade(bool up = false)
    {
        int dmg = up ? B.GrenadeDmgUp : B.GrenadeDmg;
        return Make(new CardData("sn_grenade", "유탄 발사", CardType.Attack, B.GrenadeCost, TargetType.AllEnemies,
            "모든 적에게 " + dmg + " 피해\n취약 " + (up ? B.GrenadeVulnUp : B.GrenadeVuln),
            new DamageEffect(dmg, DamageKind.AD, true), new ApplyStatusEffect(StatusType.Vulnerable, up ? B.GrenadeVulnUp : B.GrenadeVuln, true)) { ManaCost = B.GrenadeBullet },
            CardRarity.Rare, up, () => Grenade(true));
    }

    // ---- 스킬 ----
    public static CardData SmokeBomb(bool up = false)
    {
        int block = up ? B.SmokeBlockUp : B.SmokeBlock; float red = up ? B.SmokeReduceUp : B.SmokeReduce;
        return Make(new CardData("sn_smoke", "연막탄", CardType.Skill, 1, TargetType.Self,
            "방어도 " + block + "\n다음 내 턴까지 받는 피해 -" + (int)(red * 100) + "%",
            new BlockEffect(block), Do(ctx => ctx.User.SmokeReduce = System.Math.Max(ctx.User.SmokeReduce, red))) { IsThrow = true },   // 투척 자세 (3-74)
            CardRarity.Common, up, () => SmokeBomb(true));
    }
    public static CardData PreciseAim(bool up = false)
    {
        int bullets = up ? B.PreciseAimBulletsUp : B.PreciseAimBullets;
        return Make(new CardData("sn_precise_aim", "정밀 조준", CardType.Skill, 1, TargetType.Self,
            "[조준] +" + B.PreciseAim + "\n탄환 +" + bullets,
            Do(ctx => ctx.Battle.GainAim(ctx.User, B.PreciseAim, null)), new GainManaEffect(bullets)),
            CardRarity.Common, up, () => PreciseAim(true));
    }
    public static CardData TacticalRetreat(bool up = false)
    {
        int block = up ? B.RetreatBlockUp : B.RetreatBlock;
        return Make(new CardData("sn_retreat", "전술 후퇴", CardType.Skill, B.RetreatCost, TargetType.Self,
            "방어도 " + block + "\n손패 1장을 골라 버리고\n탄환 +" + (up ? B.RetreatBulletsUp : B.RetreatBullets),
            new BlockEffect(block),
            Do(ctx =>
            {
                // 2026-11-15: 버릴 카드를 직접 고름 (손패 카드를 위로 끌어올리면 버려짐 → 그때 탄환)
                //   손패가 비어 있으면 버릴 게 없으니 탄환을 바로 줌
                int bullets = up ? B.RetreatBulletsUp : B.RetreatBullets;
                if (ctx.User.Hand.Count > 0) ctx.User.PendingDiscardPick = bullets;
                else ctx.User.GainMana(bullets);
            })),
            CardRarity.Rare, up, () => TacticalRetreat(true));
    }
    public static CardData Stealth(bool up = false)
    {
        int block = up ? B.StealthBlockUp : B.StealthBlock;
        return Make(new CardData("sn_stealth", "은신", CardType.Skill, B.StealthCost, TargetType.Self,
            "방어도 " + block + "\n다음 내 턴 시작에 방어도가\n사라지지 않음",
            new BlockEffect(block), Do(ctx => ctx.User.RetainBlockOnce = true)) { ManaCost = B.StealthBullet },
            CardRarity.Hero, up, () => Stealth(true));
    }
    public static CardData Overdrive(bool up = false)
    {
        int draw = up ? B.OverdriveDrawUp : B.OverdriveDraw;
        return Make(new CardData("sn_overdrive", "오버드라이브", CardType.Skill, 0, TargetType.Self,
            "이번 턴 카드의 탄환 소모 0\n" + draw + "장 뽑기\n소멸",
            Do(ctx => { ctx.User.FreeBulletsTurn = true; ctx.Battle.AddLog("  오버드라이브: 이번 턴 탄환 소모 0"); }), new DrawEffect(draw)) { Exhaust = true },
            CardRarity.Legend, up, () => Overdrive(true));
    }
    public static CardData ChainTrap(bool up = false)
    {
        int mark = B.ChainTrapMark, block = up ? B.ChainTrapBlockUp : B.ChainTrapBlock, weak = up ? B.ChainTrapWeakUp : B.ChainTrapWeak;
        return Make(new CardData("sn_chain_trap", "사슬 함정", CardType.Skill, 1, TargetType.SingleEnemy,
            "약화 " + weak + ", [표식] " + mark + "\n방어도 " + block,
            new ApplyStatusEffect(StatusType.Weak, weak), Do(ctx => Mark(ctx, mark)), new BlockEffect(block)) { IsThrow = true },   // 투척 자세 (3-74)
            CardRarity.Common, up, () => ChainTrap(true));
    }
    public static CardData Resupply(bool up = false)
    {
        int energy = up ? B.ResupplyEnergyUp : B.ResupplyEnergy;
        return Make(new CardData("sn_resupply", "보급", CardType.Skill, 0, TargetType.Self,
            "에너지 +" + energy + "\n탄환을 최대치까지 충전\n소멸",
            new GainEnergyEffect(energy), Do(ctx => { ctx.User.GainMana(ctx.User.MaxMana); ctx.Battle.AddLog("  탄환 가득 (" + ctx.User.Mana + ")"); })) { Exhaust = true },
            CardRarity.Legend, up, () => Resupply(true));
    }
    public static CardData FullReload(bool up = false)
    {
        int bullets = up ? B.FullReloadBulletsUp : B.FullReloadBullets;
        return Make(new CardData("sn_full_reload", "전탄 장전", CardType.Skill, 1, TargetType.Self,
            "탄환 +" + bullets + "\n다음 턴 시작에 " + B.FullReloadDraw + "장 더 뽑기",
            new GainManaEffect(bullets), Do(ctx => ctx.User.GuardStanceDraw += B.FullReloadDraw)),
            CardRarity.Common, up, () => FullReload(true));
    }
    public static CardData LaserSight(bool up = false)
    {
        int mark = up ? B.LaserMarkUp : B.LaserMark, aim = up ? B.LaserAimUp : B.LaserAim;
        return Make(new CardData("sn_laser", "레이저 사이트", CardType.Skill, 0, TargetType.SingleEnemy,
            "[조준] +" + aim + "\n대상에게 [표식] " + mark,
            Do(ctx => { ctx.Battle.GainAim(ctx.User, aim, ctx.Target); Mark(ctx, mark); })) { ManaCost = B.LaserBullet },
            CardRarity.Rare, up, () => LaserSight(true));
    }

    // ---- 파워 ----
    public static CardData ExecutionerGaze(bool up = false)
    {
        int bonus = B.GazeBonus;
        return Make(new CardData("sn_gaze", "처형인의 시선", CardType.Power, up ? B.GazeCostUp : B.GazeCost, TargetType.Self,
            "[표식]이 있는 적에게 주는\n모든 공격 피해 +" + bonus,
            Do(ctx => ctx.User.ExecutionerGaze += bonus)) { ManaCost = B.GazeBullet },
            CardRarity.Hero, up, () => ExecutionerGaze(true));
    }
    public static CardData AmmoSupply(bool up = false)
    {
        return Make(new CardData("sn_ammo_supply", "탄약보급", CardType.Power, up ? B.AmmoSupplyCostUp : B.AmmoSupplyCost, TargetType.Self,
            "매 턴 탄환 +" + B.AmmoSupplyBullets,
            Do(ctx => ctx.User.AmmoSupply++)),
            CardRarity.Rare, up, () => AmmoSupply(true));
    }
    public static CardData CrimsonMagazine(bool up = false)
    {
        return Make(new CardData("sn_crimson", "진홍의 탄창", CardType.Power, up ? B.CrimsonCostUp : B.CrimsonCost, TargetType.Self,
            "공격 카드를 " + B.CrimsonEvery + "장 쓸 때마다\n탄환 +" + B.CrimsonBullets + ", " + B.CrimsonDraw + "장 뽑기",
            Do(ctx => ctx.User.CrimsonMag++)),
            CardRarity.Legend, up, () => CrimsonMagazine(true));
    }
    public static CardData WeakAnalysisCard(bool up = false)
    {
        int block = up ? B.AnalysisBlockUp : B.AnalysisBlock;
        return Make(new CardData("sn_analysis", "약점 분석", CardType.Power, 1, TargetType.Self,
            (up ? "방어도 " + B.AnalysisInstantBlockUp + "\n" : "") + "적에게 [표식]을 걸 때마다\n방어도 +" + block + ", 탄환 +" + B.AnalysisBullets,
            Do(ctx => { if (up) ctx.User.GainBlock(B.AnalysisInstantBlockUp); ctx.User.WeakAnalysis += block; })) { ManaCost = B.AnalysisBullet },
            CardRarity.Rare, up, () => WeakAnalysisCard(true));
    }

    public static List<CardData> SniperStarterDeck()
    {
        List<CardData> deck = new List<CardData>();
        // 3-143: 장전 4 + 샷 4 + 정밀 조준 2 + 사격 1 = 11장 (탄환을 쓰는 사격 1장은 남김)
        for (int i = 0; i < 4; i++) deck.Add(SniperDefend());   // 장전
        for (int i = 0; i < 4; i++) deck.Add(SniperStrike());   // 샷
        for (int i = 0; i < 2; i++) deck.Add(PreciseAim());
        deck.Add(Shot());                                       // 사격
        return deck;
    }

    // ---- [총검술] (2026-11-15): 탄환 없이 쓰는 근접 공격. 균형 잡힌 대검 코등이와 함께 ----
    public static CardData BayonetStab(bool up = false)
    {
        int dmg = up ? B.BayonetStabDmgUp : B.BayonetStabDmg;
        return Make(new CardData("sn_bayonet_stab", "총검 찌르기", CardType.Attack, 1, TargetType.SingleEnemy,
            "[총검술]\n" + dmg + " 피해\n탄환 +" + B.BayonetStabBullet,
            new DamageEffect(dmg, DamageKind.AD), new GainManaEffect(B.BayonetStabBullet)) { IsBayonet = true },
            CardRarity.Common, up, () => BayonetStab(true));
    }
    public static CardData ButtStrike(bool up = false)
    {
        int dmg = up ? B.ButtStrikeDmgUp : B.ButtStrikeDmg, block = up ? B.ButtStrikeBlockUp : B.ButtStrikeBlock;
        return Make(new CardData("sn_butt_strike", "개머리판 강타", CardType.Attack, 1, TargetType.SingleEnemy,
            "[총검술]\n" + dmg + " 피해, 약화 " + B.ButtStrikeWeak + "\n방어도 " + block,
            new DamageEffect(dmg, DamageKind.AD), new ApplyStatusEffect(StatusType.Weak, B.ButtStrikeWeak), new BlockEffect(block)) { IsBayonet = true },
            CardRarity.Common, up, () => ButtStrike(true));
    }
    public static CardData BayonetCharge(bool up = false)
    {
        int dmg = up ? B.BayonetChargeDmgUp : B.BayonetChargeDmg;
        return Make(new CardData("sn_bayonet_charge", "총검 돌격", CardType.Attack, 2, TargetType.SingleEnemy,
            "[총검술]\n" + dmg + " 피해, [표식] " + B.BayonetChargeMark + "\n이미 [표식]이 있던 적이면 [조준] +" + B.BayonetChargeAim,
            Do(ctx =>
            {
                bool wasMarked = Alive(ctx.Target) && ctx.Target.Marks > 0;
                Hit(ctx, dmg, DamageKind.AD);
                Mark(ctx, B.BayonetChargeMark);
                if (wasMarked) ctx.Battle.GainAim(ctx.User, B.BayonetChargeAim, ctx.Target);
            })) { IsBayonet = true },
            CardRarity.Rare, up, () => BayonetCharge(true));
    }

    public static List<CardData> SniperTestCards()
    {
        return new List<CardData>
        {
            BayonetStab(), ButtStrike(), BayonetCharge(),
            Shot(), Headshot(), Incendiary(), APRound(), Spray(), DoomShot(), RedFlash(), PierceShot(), RapidFire(), VitalShot(), SuppressShot(), Grenade(),
            SmokeBomb(), PreciseAim(), TacticalRetreat(), Stealth(), Overdrive(), ChainTrap(), Resupply(), FullReload(), LaserSight(),
            ExecutionerGaze(), AmmoSupply(), CrimsonMagazine(), WeakAnalysisCard()
        };
    }

    // ======================================================================
    // 공용 카드 (2026-11-15): 모든 직업이 전투 보상·상점·보물 상자에서 얻음
    //   "직업 자원"을 쓰는 카드(정신 집중)는 그 직업 자원(영혼·홍염·마나·탄환)을 줌
    // ======================================================================
    public static CardData CAmbush(bool up = false)
    {
        int dmg = up ? B.CAmbushDmgUp : B.CAmbushDmg;
        return Make(new CardData("c_ambush", "기습 타격", CardType.Attack, 0, TargetType.SingleEnemy,
            "선천성\n" + dmg + " 피해\n소멸",
            new DamageEffect(dmg, DamageKind.AD)) { Innate = true, Exhaust = true },
            CardRarity.Common, up, () => CAmbush(true));
    }
    public static CardData CJab(bool up = false)
    {
        int dmg = up ? B.CJabDmgUp : B.CJabDmg;
        return Make(new CardData("c_jab", "견제", CardType.Attack, 1, TargetType.SingleEnemy,
            dmg + " 피해\n약화 " + (up ? B.CJabWeakUp : B.CJabWeak),
            new DamageEffect(dmg, DamageKind.AD), new ApplyStatusEffect(StatusType.Weak, up ? B.CJabWeakUp : B.CJabWeak)),
            CardRarity.Common, up, () => CJab(true));
    }
    public static CardData CCombo(bool up = false)
    {
        int dmg = up ? B.CComboDmgUp : B.CComboDmg, per = up ? B.CComboPerCardUp : B.CComboPerCard;
        return Make(new CardData("c_combo", "연계 일격", CardType.Attack, 1, TargetType.SingleEnemy,
            dmg + " 피해\n이번 턴 쓴 카드 1장당 +" + per,
            Do(ctx => Hit(ctx, dmg + per * ctx.User.CardsPlayedThisTurn, DamageKind.AD))),
            CardRarity.Rare, up, () => CCombo(true));
    }
    public static CardData CShieldBash(bool up = false)
    {
        int dmg = up ? B.CShieldBashDmgUp : B.CShieldBashDmg;
        return Make(new CardData("c_shield_bash", "방패 찍기", CardType.Attack, B.CShieldBashCost, TargetType.SingleEnemy,
            dmg + " 피해\n취약 " + (up ? B.CShieldBashVulnUp : B.CShieldBashVuln),
            new DamageEffect(dmg, DamageKind.AD), new ApplyStatusEffect(StatusType.Vulnerable, up ? B.CShieldBashVulnUp : B.CShieldBashVuln)),
            CardRarity.Rare, up, () => CShieldBash(true));
    }
    public static CardData CDoubleCut(bool up = false)
    {
        int dmg = up ? B.CDoubleCutDmgUp : B.CDoubleCutDmg;
        return Make(new CardData("c_double_cut", "연속 베기", CardType.Attack, 1, TargetType.SingleEnemy,
            dmg + " 피해 × " + B.CDoubleCutHits,
            new DamageEffect(dmg, DamageKind.AD, false, B.CDoubleCutHits)),
            CardRarity.Common, up, () => CDoubleCut(true));
    }
    public static CardData CBarrierBreak(bool up = false)
    {
        int dmg = up ? B.CBreakDmgUp : B.CBreakDmg;
        return Make(new CardData("c_barrier_break", "방어막 깨기", CardType.Attack, 1, TargetType.SingleEnemy,
            "내 방어도를 전부 없앤 뒤\n" + dmg + " 피해",
            Do(ctx =>
            {
                int b = ctx.User.Block;
                ctx.User.Block = 0;
                if (b > 0) ctx.Battle.AddLog("  방어도 " + b + " 없앰");
                Hit(ctx, dmg, DamageKind.AD);
            })),
            CardRarity.Common, up, () => CBarrierBreak(true));
    }
    public static CardData CLoot(bool up = false)
    {
        int dmg = up ? B.CLootDmgUp : B.CLootDmg, gold = up ? B.CLootGoldUp : B.CLootGold;
        return Make(new CardData("c_loot", "약탈", CardType.Attack, 1, TargetType.SingleEnemy,
            dmg + " 피해\n골드 +" + gold + "\n소멸",
            new DamageEffect(dmg, DamageKind.AD), Do(ctx => { ctx.User.Gold += gold; ctx.Battle.AddLog("  골드 +" + gold); })) { Exhaust = true },
            CardRarity.Rare, up, () => CLoot(true));
    }
    public static CardData CApocalypse(bool up = false)
    {
        int dmg = up ? B.CApocalypseDmgUp : B.CApocalypseDmg, per = up ? B.CApocalypsePerExhaustUp : B.CApocalypsePerExhaust;
        return Make(new CardData("c_apocalypse", "아포칼립스", CardType.Attack, B.CApocalypseCost, TargetType.AllEnemies,
            "모든 적에게 " + dmg + " 피해\n이번 전투에서 소멸된 카드\n1장당 +" + per,
            Do(ctx =>
            {
                int d = dmg + per * ctx.User.ExhaustPile.Count;
                foreach (EnemyCombatant e in ctx.Battle.LivingEnemies()) HitEnemy(ctx, e, d, DamageKind.AD);
            })),
            CardRarity.Legend, up, () => CApocalypse(true));
    }
    public static CardData CEmergency(bool up = false)
    {
        int block = up ? B.CEmergencyBlockUp : B.CEmergencyBlock;
        return Make(new CardData("c_emergency", "긴급 방어", CardType.Skill, 0, TargetType.Self,
            "방어도 " + block + "\n소멸",
            new BlockEffect(block)) { Exhaust = true },
            CardRarity.Common, up, () => CEmergency(true));
    }
    public static CardData CFirstAid(bool up = false)
    {
        int heal = up ? B.CFirstAidHealUp : B.CFirstAidHeal;
        return Make(new CardData("c_first_aid", "응급 처치", CardType.Skill, 1, TargetType.Self,
            "HP +" + heal + "\n소멸",
            new HealEffect(heal)) { Exhaust = true },
            CardRarity.Common, up, () => CFirstAid(true));
    }
    public static CardData CRegroup(bool up = false)
    {
        int block = up ? B.CRegroupBlockUp : B.CRegroupBlock;
        return Make(new CardData("c_regroup", "전술적 후퇴", CardType.Skill, 1, TargetType.Self,
            "방어도 " + block + "\n" + B.CRegroupDraw + "장 뽑기",
            new BlockEffect(block), new DrawEffect(B.CRegroupDraw)),
            CardRarity.Common, up, () => CRegroup(true));
    }
    public static CardData CExpose(bool up = false)
    {
        int v = B.CExposeVuln;
        return Make(new CardData("c_expose", "약점 노출", CardType.Skill, up ? 0 : 1, TargetType.AllEnemies,
            "모든 적에게 취약 " + v,
            new ApplyStatusEffect(StatusType.Vulnerable, v, true)),
            CardRarity.Common, up, () => CExpose(true));
    }
    public static CardData CFocus(bool up = false)
    {
        int n = B.CFocusRes;
        return Make(new CardData("c_focus", "정신 집중", CardType.Skill, up ? 0 : 1, TargetType.Self,
            "직업 자원 +" + n + "\n(영혼 / 홍염 / 마나 / 탄환)",
            Do(ctx => { ctx.User.GainMana(n); ctx.Battle.AddLog("  " + ctx.User.ManaName + " +" + n); })),
            CardRarity.Rare, up, () => CFocus(true));
    }
    public static CardData COpportunity(bool up = false)
    {
        int draw = up ? B.COpportunityDrawUp : B.COpportunityDraw;
        return Make(new CardData("c_opportunity", "기회 포착", CardType.Skill, 0, TargetType.Self,
            draw + "장 뽑기\n이번 턴 공격 카드를 썼다면\n에너지 +" + B.COpportunityEnergy,
            new DrawEffect(draw),
            Do(ctx => { if (ctx.User.AttacksPlayedThisTurn > 0) { ctx.User.Energy += B.COpportunityEnergy; ctx.Battle.AddLog("  에너지 +" + B.COpportunityEnergy); } })),
            CardRarity.Hero, up, () => COpportunity(true));
    }
    public static CardData CIndomitable(bool up = false)
    {
        int block = up ? B.CIndomitableBlockUp : B.CIndomitableBlock;
        return Make(new CardData("c_indomitable", "불굴의 투지", CardType.Skill, B.CIndomitableCost, TargetType.Self,
            "방어도 " + block + "\n손패의 상태이상 카드\n(상처 등) 전부 소멸",
            new BlockEffect(block), Do(ctx => ctx.Battle.RemoveUnplayable(ctx.User, true))),
            CardRarity.Hero, up, () => CIndomitable(true));
    }
    public static CardData CTimeWarp(bool up = false)
    {
        return Make(new CardData("c_time_warp", "시간 왜곡", CardType.Skill, up ? B.CTimeWarpCostUp : B.CTimeWarpCost, TargetType.Self,
            "이번 턴 다음에 쓰는 카드\n1장이 2번 발동\n소멸",
            Do(ctx => { ctx.User.DoubleNextCard = true; ctx.Battle.AddLog("  다음 카드 2번 발동"); })) { Exhaust = true },
            CardRarity.Legend, up, () => CTimeWarp(true));
    }
    public static CardData CPurify(bool up = false)
    {
        return Make(new CardData("c_purify", "정화", CardType.Skill, up ? 0 : 1, TargetType.Self,
            "취약·약화·손상·발화 제거\n상태이상 카드(상처 등)\n전부 없앰",
            Do(ctx =>
            {
                PlayerCombatant u = ctx.User;
                u.SetStatus(StatusType.Vulnerable, 0); u.SetStatus(StatusType.Weak, 0);
                u.SetStatus(StatusType.Frail, 0); u.SetStatus(StatusType.Burn, 0);
                u.DrawPenalty = 0;
                ctx.Battle.AddLog("  디버프 제거");
                ctx.Battle.RemoveUnplayable(u, false);
            })),
            CardRarity.Rare, up, () => CPurify(true));
    }
    public static CardData CRefit(bool up = false)
    {
        int block = up ? B.CRefitBlockUp : B.CRefitBlock;
        return Make(new CardData("c_refit", "재정비", CardType.Skill, 1, TargetType.SingleEnemy,
            "방어도 " + block + "\n대상이 약화나 취약이면\n" + B.CRefitDraw + "장 뽑기",
            new BlockEffect(block),
            Do(ctx =>
            {
                if (Alive(ctx.Target) && (ctx.Target.HasStatus(StatusType.Weak) || ctx.Target.HasStatus(StatusType.Vulnerable)))
                    ctx.Battle.Draw(ctx.User, B.CRefitDraw);
            })),
            CardRarity.Common, up, () => CRefit(true));
    }
    public static CardData CSteelWill(bool up = false)
    {
        int block = up ? B.CSteelWillBlockUp : B.CSteelWillBlock;
        return Make(new CardData("c_steel_will", "강철 의지", CardType.Power, 1, TargetType.Self,
            "매 턴 종료 시 방어도 " + block,
            Do(ctx => ctx.User.SteelWill += block)),
            CardRarity.Common, up, () => CSteelWill(true));
    }
    public static CardData CTranscend(bool up = false)
    {
        return Make(new CardData("c_transcend", "초월", CardType.Power, up ? B.CTranscendCostUp : B.CTranscendCost, TargetType.Self,
            "매 턴 시작 시 무작위 손패\n1장이 이번 턴 에너지 0",
            Do(ctx => ctx.User.Transcend++)),
            CardRarity.Legend, up, () => CTranscend(true));
    }
    public static CardData CBattleInstinct(bool up = false)
    {
        return Make(new CardData("c_instinct", "전투 본능", CardType.Power, up ? B.CInstinctCostUp : 1, TargetType.Self,
            "공격 카드로 적을 처치하면\n에너지 +" + B.CInstinctEnergy + ", " + B.CInstinctDraw + "장 뽑기",
            Do(ctx => ctx.User.BattleInstinct++)),
            CardRarity.Hero, up, () => CBattleInstinct(true));
    }
    public static CardData CPotential(bool up = false)
    {
        CardData card = Make(new CardData("c_potential", "무한한 잠재력", CardType.Power, up ? B.CPotentialCostUp : 1, TargetType.Self,
            (up ? "선천성\n" : "") + "다른 직업의 무작위 희귀 카드\n1장을 손패에 추가\n(이번 전투 에너지 0)",
            Do(ctx =>
            {
                List<CardData> pool = new List<CardData>();
                PlayerClass[] all = { PlayerClass.BlackScythe, PlayerClass.CosmicPhoenix, PlayerClass.FrostMage, PlayerClass.Sniper };
                foreach (PlayerClass pc in all)
                {
                    if (pc == ctx.User.Class) continue;
                    foreach (CardData c in ClassPool(pc))
                        if (c.Rarity == CardRarity.Rare && c.Type != CardType.Power && (!c.MultiOnly || ctx.Battle.Players.Count > 1)) pool.Add(c);
                }
                if (pool.Count == 0) return;
                CardData pick = pool[ctx.Battle.RandomIndex(pool.Count)];
                pick.BattleDiscount = pick.Cost;
                pick.ManaCost = 0;   // 다른 직업 자원은 없으니 자원 소모도 0
                ctx.User.Hand.Add(pick);
                ctx.Battle.AddLog("  무한한 잠재력: [" + pick.Name + "] 손패에 추가 (에너지 0)");
            })),
            CardRarity.Hero, up, () => CPotential(true));
        card.Innate = up;   // 강화판: 선천성 (전투 첫 손패에 들어와서 바로 쓸 수 있음)
        return card;
    }
    public static CardData CThornArmor(bool up = false)
    {
        int block = up ? B.CThornArmorBlockUp : B.CThornArmorBlock, dmg = up ? B.CThornArmorDmgUp : B.CThornArmorDmg;
        return Make(new CardData("c_thorn_armor", "가시 갑옷", CardType.Power, 1, TargetType.Self,
            "방어도 " + block + "\n공격받을 때마다\n공격자에게 " + dmg + " 피해",
            new BlockEffect(block), Do(ctx => ctx.User.ThornArmor += dmg)),
            CardRarity.Rare, up, () => CThornArmor(true));
    }

    public static List<CardData> CommonPoolCards()
    {
        return new List<CardData>
        {
            CAmbush(), CJab(), CCombo(), CShieldBash(), CDoubleCut(), CBarrierBreak(), CLoot(), CApocalypse(),
            CEmergency(), CFirstAid(), CRegroup(), CExpose(), CFocus(), COpportunity(), CIndomitable(), CTimeWarp(), CPurify(), CRefit(),
            CSteelWill(), CTranscend(), CBattleInstinct(), CPotential(), CThornArmor()
        };
    }

    // ======================================================================
    // 보상 풀: 그 직업이 보상으로 받을 수 있는 카드 (기본 카드 제외)
    // ======================================================================
    //   2026-11-15: 직업 카드 + 공용 카드 (공용 카드도 보상·상점에 섞여 나옴)
    public static List<CardData> RewardPool(PlayerClass playerClass)
    {
        List<CardData> pool = ClassPool(playerClass);
        pool.AddRange(CommonPoolCards());
        return pool;
    }

    // 아이디로 카드 다시 만들기 (이어하기에서 씀, 2026-11-15 3-73). 못 찾으면 null
    //   카드는 효과 함수를 품고 있어서 JSON 으로 저장할 수 없음 → 아이디만 저장해 두고, 불러올 때
    //   모든 카드 목록(시작 덱·보상 풀·공용)에서 같은 아이디를 찾아 새로 만듦. 강화였으면 MakeUpgraded()
    //   자바로 치면 Map<String, Supplier<Card>> 에서 꺼내는 것과 같은 일 (여기서는 목록을 훑어서 찾음)
    public static CardData FromId(string id, bool upgraded)
    {
        List<CardData> all = new List<CardData>();
        all.AddRange(CommonStarterDeck());
        all.AddRange(BlackScytheStarterDeck());
        all.AddRange(PhoenixStarterDeck());
        all.AddRange(FrostMageStarterDeck());
        all.AddRange(SniperStarterDeck());
        all.AddRange(BlackScytheTestCards());
        all.AddRange(PhoenixTestCards());
        all.AddRange(FrostMageTestCards());
        all.AddRange(SniperTestCards());
        all.AddRange(CommonPoolCards());
        all.Add(Strike());
        all.Add(Defend());
        all.Add(Veil());
        foreach (CardData c in all)
        {
            if (c.Id != id) continue;
            if (!upgraded) return c;
            CardData up = c.MakeUpgraded();
            return up ?? c;   // ?? = 왼쪽이 null 이면 오른쪽 (강화판이 없는 카드면 그냥 원래 카드)
        }
        return null;
    }

    // 혼자(1명)면 멀티 전용 카드를 리스트에서 뺌 (2026-11-15, 3-71)
    //   자바의 list.removeIf(c -> c.multiOnly) 와 같은 일. C# 은 RemoveAll(조건) 을 씀
    //   => 는 자바의 -> (람다) 와 같음
    public static List<CardData> RemoveMultiOnly(List<CardData> pool, int playerCount)
    {
        if (playerCount <= 1) pool.RemoveAll(c => c.MultiOnly);
        return pool;
    }

    // 그 직업 카드만 (공용 제외)
    public static List<CardData> ClassPool(PlayerClass playerClass)
    {
        switch (playerClass)
        {
            case PlayerClass.BlackScythe: return BlackScytheTestCards();
            case PlayerClass.CosmicPhoenix: return PhoenixTestCards();
            case PlayerClass.FrostMage: return FrostMageTestCards();
            case PlayerClass.Sniper: return SniperTestCards();
            default: return new List<CardData>();
        }
    }
}
