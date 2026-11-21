// ============================================================
// BattleSystem.cs
// 전투 규칙의 "심판" 입니다. 화면(Unity)은 전혀 모르고, 규칙만 계산합니다.
//
// 한 라운드의 흐름
//   [라운드 시작] 살아 있는 플레이어 전원:
//       방어도 0 (웨이브 칸은 절반) → 발화 피해 → 에너지 3 → 마나 회복(마도사) → 파워 효과 → 카드 5장
//       → 지난 턴에 차징한 반월참 발동
//   [플레이어 차례] 동시 턴 (슬더스2 방식). 전원이 턴 종료하면 적 차례
//   [적 차례] 플레이어 쪽 취약·약화 1 감소
//            적마다: 방어도 0 → 발화 피해 → (동상이면 행동 취소) 의도대로 행동 (공격은 플레이어 전원에게)
//            턴 끝: 혹한의 감옥·영혼 착취 → 적 쪽 취약·약화·감전 1 감소, 동상 풀림 → 다음 의도
//
// 즉시 판정 (2026-11-08)
//   처형: 처형 스택이 붙거나 HP가 깎이는 순간 조건을 보고 바로 처형
//   동상: 동결 스택이 기준에 닿는 순간 [동상] (이번 적 차례 행동 취소)
//   감전: 감전 스택이 4가 되는 순간 [감전] 2턴
//
// 웨이브: 지금 웨이브의 적을 다 잡으면 다음 웨이브가 바로 들어옴. HP·손패·마나·에너지는 그대로
// ============================================================

using System.Collections.Generic;

public enum BattleResult
{
    Ongoing, // 진행 중
    Victory, // 승리
    Defeat   // 패배
}

public class BattleSystem
{
    public List<PlayerCombatant> Players;
    public List<EnemyCombatant> Enemies;
    public List<string> Log = new List<string>();

    public int Round;                 // 몇 번째 턴인지
    public BattleResult Result = BattleResult.Ongoing;
    public int Stage = 1;             // 1~4. StartBattle 전에 정해야 함
    public Difficulty Difficulty = Difficulty.Normal;   // 3-196 난이도. StartBattle 전에 정해야 함 (몬스터 체력·공격력 배율)

    // ---- 웨이브 ----
    public List<List<EnemyCombatant>> UpcomingWaves = new List<List<EnemyCombatant>>();
    public int WaveNumber = 1;
    public int WaveCount = 1;
    public int WaveVersion;           // 웨이브가 바뀔 때마다 +1 (화면이 "적이 바뀌었다"를 알아채는 용도)
    public int EnemyListVersion;      // 3-102: 웨이브 안에서 적 목록이 바뀔 때 +1 (점토 분열) → 화면이 적 칸만 다시 만듦
    public bool KeepHalfBlock;        // true 면 내 턴 시작에 방어도 절반 유지 (수성의 여러 웨이브 칸)

    public float EnemyAttackReduction; // 이번 적 차례에 적 전체 공격 피해 감소 (어둠의 지배 0.4). 적 차례가 끝나면 0
    public bool InEnemyPhase;          // 지금 적 차례인지 (태양의 진노는 내 턴에만)

    // 지금 쓰고 있는 카드의 성격 (피해 보너스가 "공격 카드일 때만" 붙는 파워들이 봄)
    private bool attackCardContext;    // 공격 카드 (만월, 적월)
    private bool lightningContext;     // [벼락] 카드 (뇌신 권능)
    private float aimExtra;            // 스나이퍼 [조준]: 이번 공격 카드 피해 배율 추가 (+0.5 × 조준 수)
    private bool basicContext;         // 기본 공격 카드 (화염 단련)
    // ---- 3-176 새 아티팩트: 지금 내는 카드의 성격 (PlayCard 가 켜고 끔) ----
    private bool bulletAttackContext;  // 스나이퍼가 탄환을 써서 낸 공격 카드 (정밀 탄두 상자, 가벼운 소음기 부품)
    private bool manaAttackContext;    // 마도사가 마나를 써서 낸 공격 카드 (서리 파편 반지)
    private bool bayonetContext;       // [총검술] 카드 (가벼운 소음기 부품 페널티)
    private bool firstAttackContext;   // 이번 턴 첫 공격 카드 (과열된 마력석)
    private bool glacialHeartDone;     // 만년설의 심장: 이 카드로 이미 마나를 얻었는지
    private bool lightLensDone;        // 가벼운 조준경 렌즈: 이 카드로 이미 탄환을 얻었는지
    private HashSet<PlayerCombatant> fallenSeen = new HashSet<PlayerCombatant>();   // 전우애의 방패: 이미 쓰러짐을 처리한 아군
    private bool drawingTurnStart;     // 지금 뽑는 게 턴 시작 뽑기인지 (절대 영도의 파편 페널티는 카드 효과 뽑기에만)

    private System.Random rng;        // 무작위 수 (seed 가 같으면 결과도 같음 → 버그 재현에 유용)

    public BattleSystem(List<PlayerCombatant> players, List<EnemyCombatant> enemies, int seed)
    {
        Players = players;
        Enemies = enemies;
        rng = new System.Random(seed);
    }

    // 웨이브가 여러 번인 전투. waves[0] = 처음 나오는 적, 나머지는 차례로 나옴
    public BattleSystem(List<PlayerCombatant> players, List<List<EnemyCombatant>> waves, int seed)
    {
        Players = players;
        Enemies = new List<EnemyCombatant>(waves[0]);
        for (int i = 1; i < waves.Count; i++) UpcomingWaves.Add(waves[i]);
        WaveCount = waves.Count;
        WaveNumber = 1;
        rng = new System.Random(seed);
    }

    public bool CanAct(PlayerCombatant player)
    {
        return player != null && Result == BattleResult.Ongoing && !player.IsDead && !player.HasEndedTurn;
    }

    // 아직 턴 종료를 안 누른 첫 번째 플레이어 (없으면 null)
    public PlayerCombatant ActivePlayer
    {
        get
        {
            if (Result != BattleResult.Ongoing) return null;
            int i = FindNextPlayer(0);
            return (i < 0) ? null : Players[i];
        }
    }

    public void AddLog(string message)
    {
        Log.Add(message);
    }

    // ==================== 전투 시작 ====================
    public void StartBattle()
    {
        ApplyPartyScaling(Enemies);
        InitSpecialEnemies(Enemies);   // 3-102: 외골격·유충 수

        foreach (PlayerCombatant p in Players)
        {
            ResetBattleState(p);
            p.PotionPowerBonus = 0;   // 강화 포션은 한 전투만
            p.DrawPile.AddRange(p.Deck);
            Shuffle(p.DrawPile);
            MoveInnateToTop(p.DrawPile);
            p.Mana = 0;
            p.GainMana(p.StartMana());   // 마도사: 민첩에 따른 시작 마나 (첫 턴 회복은 라운드 시작에)
        }

        AddLog("전투 시작! (" + Stage + "스테이지, " + Players.Count + "인"
               + (WaveCount > 1 ? ", 웨이브 " + WaveCount + "번" : "")
               + (KeepHalfBlock ? ", 방어도 절반 유지" : "") + ")");
        StartRound();
        BattleStartRelics();
        OnWaveStart();
    }

    // ==================== 아티팩트: 전투 시작 ====================
    // 첫 턴 손패를 뽑은 뒤에 발동 (숫돌이 "첫 손패"를 보기 때문)
    private void BattleStartRelics()
    {
        foreach (PlayerCombatant p in Players)
        {
            if (p.IsDead) continue;
            if (p.HasRelic(RelicLibrary.ReaperMark))
            {
                EnemyCombatant e = RandomLivingEnemy();
                if (e != null) { AddLog(p.Name + " 낡은 사신의 표식"); AddExecuteStacks(p, e, GameBalance.ReaperMarkStacks); }
            }
            if (p.HasRelic(RelicLibrary.PhoenixPlume)) RelicMana(p, GameBalance.PhoenixPlumeFlame, "불새의 깃털 장식");
            // 스나이퍼 아티팩트 (2026-11-15)
            if (p.HasRelic(RelicLibrary.RustedBelt)) RelicMana(p, GameBalance.RustedBeltBullets, "녹슨 군용 탄띠");
            if (p.HasRelic(RelicLibrary.CasingCharm)) { AddLog(p.Name + " 진홍 탄피 부적"); GainAim(p, GameBalance.CasingCharmAim, null); }
            if (p.HasRelic(RelicLibrary.FrostPendant))
            {
                AddLog(p.Name + " 서리 수정 목걸이");
                foreach (EnemyCombatant e in LivingEnemies()) AddFreeze(p, e, GameBalance.FrostPendantFreeze);
            }
            if (p.HasRelic(RelicLibrary.Hourglass)) RelicMana(p, GameBalance.HourglassSoul, "사신의 모래시계");
            if (p.HasRelic(RelicLibrary.SoulLantern)) RelicMana(p, GameBalance.SoulLanternSoul, "망자의 등불");        // 3-99
            if (p.HasRelic(RelicLibrary.WarmFeather)) RelicMana(p, GameBalance.WarmFeatherFlame, "따뜻한 깃털 부적");  // 3-99
            if (p.HasRelic(RelicLibrary.IceBrooch)) RelicMana(p, GameBalance.IceBroochMana, "얼음 조각 브로치");       // 3-101
            if (p.HasRelic(RelicLibrary.NecroSkull)) RelicMana(p, p.MaxMana, "금지된 사령술의 해골");
            if (p.HasRelic(RelicLibrary.Rampart)) GiveStartBlock(p, GameBalance.RampartBlock, "견고한 성벽돌");
            if (p.HasRelic(RelicLibrary.SteelBelt))
            {
                int loss = System.Math.Min(DamageCalculator.RoundHalfUp(p.MaxHp * GameBalance.SteelBeltHpRatio), p.Hp - 1);
                if (loss > 0) { p.LoseHp(loss); AddLog(p.Name + " 무거운 강철 허리띠: HP -" + loss); }
            }
            if (p.HasRelic(RelicLibrary.Whetstone))
            {
                List<CardData> attacks = new List<CardData>();
                foreach (CardData c in p.Hand) if (c.Type == CardType.Attack && c.Cost - c.BattleDiscount > 0) attacks.Add(c);
                Shuffle(attacks);
                int n = System.Math.Min(GameBalance.WhetstoneCards, attacks.Count);
                for (int i = 0; i < n; i++)
                {
                    attacks[i].BattleDiscount += 1;
                    AddLog(p.Name + " 마력 깃든 숫돌: [" + attacks[i].Name + "] 이번 전투 에너지 -1");
                }
            }
        }
        // ---- 3-176 새 아티팩트: 전투 시작 ----
        foreach (PlayerCombatant p in Players)
        {
            if (p.IsDead) continue;
            if (p.HasRelic(RelicLibrary.OnslaughtHelm)) { ApplyStatus(p, StatusType.Vulnerable, GameBalance.OnslaughtSelfVuln); AddLog(p.Name + " 맹공의 투구: 자신 취약 " + GameBalance.OnslaughtSelfVuln); }
            if (p.HasRelic(RelicLibrary.SwiftBoots)) GiveStartBlock(p, GameBalance.SwiftBootsBlock, "신속의 부츠");
            if (p.HasRelic(RelicLibrary.EmberMatch))
            {
                AddLog(p.Name + " 잿불의 성냥");
                foreach (EnemyCombatant e in LivingEnemies()) ApplyBurn(p, e, GameBalance.EmberMatchBurn);
                RelicMana(p, GameBalance.EmberMatchFlame, "잿불의 성냥");
            }
            if (p.HasRelic(RelicLibrary.HotBrazierShard)) RelicMana(p, GameBalance.HotBrazierFlame, "뜨거운 화로 조각");
            // 전투 시작 HP 페널티 (HP 1 아래로는 안 내려감)
            int hpLoss = 0;
            if (p.HasRelic(RelicLibrary.HungryDoll)) hpLoss += GameBalance.HungryDollHpLoss;
            if (p.HasRelic(RelicLibrary.HotBrazierShard)) hpLoss += GameBalance.HotBrazierHpLoss;
            hpLoss = System.Math.Min(hpLoss, p.Hp - 1);
            if (hpLoss > 0) { p.LoseHp(hpLoss); AddLog(p.Name + " 굶주린 인형·화로 조각: HP -" + hpLoss); }
            if (p.HasRelic(RelicLibrary.LightPouch) && p.Mana > 0)
            {
                int before = p.Mana;
                p.Mana = System.Math.Max(0, p.Mana - GameBalance.LightPouchStartLoss);
                AddLog(p.Name + " 경량 탄창 파우치: 탄환 " + before + " → " + p.Mana);
            }
            if (p.HasRelic(RelicLibrary.GoldenFlask) && p.Potions.Count < GameBalance.PotionSlots)
            {
                PotionData pot = PotionLibrary.Random(rng, Players.Count);
                p.Potions.Add(pot.Id);
                AddLog(p.Name + " 연금술사의 황금 플라스크: [" + pot.Name + "]");
            }
        }
        // 공용(파티) 아티팩트
        if (PartyHas(RelicLibrary.WarBanner))
            foreach (PlayerCombatant p in Players) if (!p.IsDead) GiveStartBlock(p, GameBalance.WarBannerBlock, "전우의 깃발");
        if (PartyHas(RelicLibrary.SiegeRam))
        {
            AddLog("공성 망치: 모든 적 취약 " + GameBalance.SiegeRamVuln);
            foreach (EnemyCombatant e in LivingEnemies()) ApplyStatus(e, StatusType.Vulnerable, GameBalance.SiegeRamVuln);
        }
        CheckResult();
    }

    // 파티 중 누가 이 아티팩트를 가졌는지 (공용 아티팩트는 한 명만 가져도 모두에게 효과) - 2026-11-15
    public bool PartyHas(string id)
    {
        foreach (PlayerCombatant p in Players) if (p.HasRelic(id)) return true;
        return false;
    }

    private void RelicMana(PlayerCombatant p, int amount, string relicName)
    {
        int before = p.Mana;
        p.GainMana(amount);
        AddLog(p.Name + " " + relicName + ": " + p.ManaName + " +" + (p.Mana - before) + " (" + p.Mana + ")");
    }

    // 웨이브가 시작될 때 (첫 웨이브 포함): 은빛 화살촉
    private void OnWaveStart()
    {
        foreach (PlayerCombatant p in Players)
        {
            if (p.IsDead || !p.HasRelic(RelicLibrary.Arrowhead)) continue;
            EnemyCombatant e = RandomLivingEnemy();
            if (e == null) continue;
            AddLog(p.Name + " 은빛 화살촉");
            ApplyStatus(e, StatusType.Vulnerable, GameBalance.ArrowheadVuln);
        }
    }

    // 웨이브를 하나 클리어했을 때 (마지막 웨이브 포함): 수비대장의 식량 주머니 → 파티 전원 회복
    private void OnWaveCleared()
    {
        foreach (PlayerCombatant owner in Players)
        {
            if (owner.IsDead || !owner.HasRelic(RelicLibrary.RationPouch)) continue;
            List<string> parts = new List<string>();
            foreach (PlayerCombatant p in Players)
            {
                if (p.IsDead) continue;
                parts.Add(p.Name + " +" + p.Heal(GameBalance.RationPouchHeal));
            }
            AddLog(owner.Name + " 수비대장의 식량 주머니: HP " + string.Join(", ", parts));
        }
    }

    // [밤]이 켜짐 (그림자 습격 = 2턴(3-172), 죽음의 밤 = 전투 내내). 부서진 초승달 펜던트가 반응
    public void EnterNight(PlayerCombatant p, bool forever)
    {
        bool wasNight = p.IsNight;
        if (forever)
        {
            p.NightForever = true;
            AddLog("  ★ 죽음의 밤! 이번 전투 동안 [밤]");
        }
        else
        {
            // 3-172: 남은 턴을 2로 (이미 밤이면 다시 2턴으로 늘림, 더 줄이지는 않음)
            p.NightTurns = System.Math.Max(p.NightTurns, GameBalance.ShadowRaidNightTurns);
            AddLog("  그림자 습격: " + GameBalance.ShadowRaidNightTurns + "턴 동안 [밤]!");
        }
        if (!wasNight) NightRelics(p);
        p.BlockGainMult = BaseBlockMult(p);   // 3-176: 그림자 수확자의 반지 ([밤]이면 방어도 페널티 없음)
    }

    // [밤]이 켜질 때 (죽음의 밤 중에는 매 턴 시작) 반응하는 아티팩트 모음 (3-99)
    private void NightRelics(PlayerCombatant p)
    {
        PendantTrigger(p);
        if (p.IsDead) return;
        if (p.HasRelic(RelicLibrary.ShadowCloak))
        {
            p.GainBlock(GameBalance.ShadowCloakBlock);
            AddLog("  " + p.Name + " 그림자 망토 조각: 방어도 +" + GameBalance.ShadowCloakBlock);
        }
        if (p.HasRelic(RelicLibrary.BloodCandelabra))
        {
            AddLog("  " + p.Name + " 핏빛 촛대: " + GameBalance.BloodCandelabraDraw + "장 뽑기");
            Draw(p, GameBalance.BloodCandelabraDraw);
        }
    }

    private void PendantTrigger(PlayerCombatant p)
    {
        if (!p.HasRelic(RelicLibrary.Pendant)) return;
        EnemyCombatant e = RandomLivingEnemy();
        if (e == null) return;
        AddLog("  " + p.Name + " 부서진 초승달 펜던트");
        AddExecuteStacks(p, e, GameBalance.PendantStacks);
    }

    // 전투마다 새로 시작하는 값들 (HP와 무기 강화는 런 동안 이어짐)
    private void ResetBattleState(PlayerCombatant p)
    {
        p.DrawPile.Clear(); p.Hand.Clear(); p.DiscardPile.Clear(); p.ExhaustPile.Clear(); p.PowersInPlay.Clear();
        foreach (CardData c in p.Deck) { c.TempDiscount = 0; c.BattleDiscount = 0; c.Bound = false; }
        p.Block = 0;
        p.Statuses.Clear();
        p.HasEndedTurn = false;
        p.BlockGainMult = BaseBlockMult(p);   // 심연의 사신 낫 페널티 (3-99)
        p.DrawPenalty = 0;
        p.MarkedEnemy = null;
        p.NightTurns = 0; p.NightForever = false;
        p.PendingHalfMoons.Clear(); p.MarkUpgraded = false; p.EclipseActive = false; p.EclipseUsed = false;
        p.RetainBlockOnce = false; p.SealedCard = null; p.PendingSealPick = false; p.PendingDiscardPick = 0;
        p.ReaperStepsStacks = 0; p.FullMoonStacks = 0; p.FullMoonBonus = 0; p.RedMoonStacks = 0; p.RedMoonBonus = 0;
        p.SoulExploitStacks = 0; p.SoulExploitPer = 0; p.WasNight = false;
        p.ReflectBurn = false; p.FlameWall = false; p.FlameWallNoPenalty = false; p.GuardStanceEnergy = 0; p.GuardStanceDraw = 0;
        p.PhoenixGuardStacks = 0; p.PhoenixGuardReduction = 0f; p.SunWrathBurn = 0;
        p.PhoenixHeartReady = false; p.PhoenixHeartUsed = false; p.PhoenixHeartHeal = 0f;
        p.EternalEmberStacks = 0; p.EternalEmberRatio = 0f; p.BurningThornsStacks = 0; p.ThornsUnlimited = false;
        p.UndyingFlame = false; p.UndyingFlameMax = 0; p.FlameTrainingStacks = 0;
        p.ShockBarrier = false; p.GlacierBarrier = 0f; p.FrostPrisonStacks = 0; p.HailDamage = 0;
        p.ThunderGodActive = false; p.ThunderGodStacks = 0; p.ThunderGodBonus = 0; p.StormFieldHits.Clear();
        p.BlizzardStacks = 0; p.BlizzardEveryTurn = false; p.BlizzardTurn = 0; p.ManaReleaseStacks = 0; p.StaticFieldBlock = 0;
        // 스나이퍼·공용 카드 (2026-11-15)
        p.AimStacks = 0; p.AimedThisCard = false; p.GlovesUsed = false; p.ExecutionerGaze = 0; p.AmmoSupply = 0; p.CrimsonMag = 0; p.CrimsonCount = 0;
        p.WeakAnalysis = 0; p.FreeBulletsTurn = false; p.SmokeReduce = 0f;
        p.SteelWill = 0; p.Transcend = 0; p.BattleInstinct = 0; p.ThornArmor = 0; p.DoubleNextCard = false;
        p.BlockGainMinus = p.HasRelic(RelicLibrary.SharpShard) ? GameBalance.SharpShardBlockLoss : 0;   // 3-176 예리한 칼날 조각
        p.FlagDrawNext = 0; p.PurifyTimerUsed = 0;
        ResetTurnState(p);
    }

    // 평소 얻는 방어도 배율 (3-99): 심연의 사신 낫이면 70%, 폭풍핵 지팡이면 65% (둘 다면 곱함)
    private float BaseBlockMult(PlayerCombatant p)
    {
        float m = 1f;
        // 3-176: 수확자의 영혼등불 (첫 턴 50%), 그림자 수확자의 반지 ([밤]이 아니면 75%)
        if (p.HasRelic(RelicLibrary.ReaperEdge) && Round <= 1) m *= GameBalance.ReaperEdgeFirstBlock;
        if (p.HasRelic(RelicLibrary.ShadowReaperRing) && !p.IsNight) m *= GameBalance.ShadowRingDayBlock;
        if (p.HasRelic(RelicLibrary.AbyssalScythe)) m *= GameBalance.AbyssalBlockRatio;
        if (p.HasRelic(RelicLibrary.StormcoreStaff)) m *= GameBalance.StormcoreBlockRatio;   // 폭풍핵 지팡이 (3-101)
        return m;
    }

    // 내 턴마다 새로 시작하는 값들
    private void ResetTurnState(PlayerCombatant p)
    {
        p.CardsPlayedThisTurn = 0;
        p.AttacksPlayedThisTurn = 0;
        p.LightningPlayedThisTurn = 0;
        p.NextSkillDiscount = 0;
        p.NextLightningFree = false;
        if (p.NightTurns > 0) p.NightTurns--;   // 3-172: 내 턴 시작마다 밤 1턴 줄임 (2 → 1: 다음 턴도 밤, 1 → 0: 끝)
        p.EclipseActive = false;
        p.ReflectBurn = false;
        p.FlameWall = false;
        p.FlameWallNoPenalty = false;
        p.ShockBarrier = false;
        p.GlacierBarrier = 0f;
        p.ThornsThisTurn = 0;
        p.FrostPrisonDrawsThisTurn = 0;
        p.PendingSealPick = false;
        p.PendingDiscardPick = 0;
        p.StaticRingUsed = false;   // 정전기의 반지 (3-101)
        // 3-176
        p.RapidMagLeft = p.HasRelic(RelicLibrary.RapidMagazine) ? GameBalance.RapidMagFree : 0;
        p.FrostStaffUsed = false;
        p.CrystalOrbProcs = 0;
        p.FrostRingBlockThisTurn = 0;
    }

    // 선천성 카드(기습)를 뽑을 더미 맨 위로 → 첫 손패에 반드시 들어옴
    private void MoveInnateToTop(List<CardData> pile)
    {
        List<CardData> innate = new List<CardData>();
        for (int i = pile.Count - 1; i >= 0; i--)
        {
            if (pile[i].Innate) { innate.Add(pile[i]); pile.RemoveAt(i); }
        }
        pile.AddRange(innate);   // 맨 뒤 = 맨 위 (Draw 는 뒤에서부터 꺼냄)
    }

    // 인원수와 스테이지에 따라 몬스터를 강하게 만듭니다.
    //   HP     = 기본 × 인원 배율 × 스테이지 HP 배율
    //   방어도 = 기본 × 인원 배율 × 스테이지 공격력 배율
    //   공격력 = 기본 × 스테이지 공격력 배율 (인원으로는 안 오름)
    private void ApplyPartyScaling(List<EnemyCombatant> enemies)
    {
        int party = Clamp(Players.Count - 1, 0, GameBalance.EnemyHpScale.Length - 1);
        int stage = Clamp(Stage - 1, 0, GameBalance.StageHpScale.Length - 1);
        float fit = Stage1Fit(enemies);   // 3-123: 1스테이지만 전투 길이 맞춤 (그 외 스테이지는 1.0)

        foreach (EnemyCombatant enemy in enemies)
        {
            float hpScale = GameBalance.EnemyHpScale[party] * GameBalance.StageHpScale[stage] * fit
                          * DifficultyInfo.EnemyHp(Difficulty, enemy.Rank);   // 3-196 난이도 (등급마다 다름)
            enemy.MaxHp = DamageCalculator.RoundHalfUp(enemy.MaxHp * hpScale);
            enemy.Hp = enemy.MaxHp;
            enemy.BlockMultiplier = GameBalance.EnemyBlockScale[party] * GameBalance.StageAttackScale[stage];
            enemy.AttackMultiplier = GameBalance.StageAttackScale[stage] * DifficultyInfo.EnemyAttack(Difficulty);   // 3-196: 쉬움 -10%
        }
    }

    // 3-123: 1스테이지 전투 HP 맞춤 배율
    //   이 전투 몬스터들의 기본 HP 합이 목표 합(GameBalance.Stage1FightHp…)이 되도록 하는 배율을 돌려줌
    //   예) 일반 쥐 18 한 마리 → 58 / 18 = 3.0 배 (상한 3.0)
    //       일반 2마리 합 44   → 58 / 44 ≈ 1.32 배
    //   전투 등급은 무리 중 가장 높은 등급 (일반 + 정예 = 정예 전투)
    //   자바로 치면 list.stream().mapToInt(e -> e.maxHp).sum() 으로 합을 구하고 나눗셈 한 번 하는 것
    private float Stage1Fit(List<EnemyCombatant> enemies)
    {
        if (Stage != 1 || enemies == null || enemies.Count == 0) return 1f;
        int sum = 0;
        EnemyRank top = EnemyRank.Normal;
        foreach (EnemyCombatant e in enemies)
        {
            sum += e.MaxHp;
            // 점토 분열체는 50% 에서 남은 HP 를 2마리가 똑같이 나눠 가지므로 실제로 깎을 양이 50% 더 많음
            if (e.Tag == EnemyLibrary.T_Clay) sum += DamageCalculator.RoundHalfUp(e.MaxHp * GameBalance.ClaySplitRatio);
            if (e.Rank > top) top = e.Rank;   // enum 은 선언 순서대로 크기 비교가 됨 (Normal < Veteran < Elite < Boss)
        }
        if (sum <= 0) return 1f;

        int target;
        switch (top)
        {
            case EnemyRank.Boss: target = GameBalance.Stage1FightHpBoss; break;
            case EnemyRank.Elite: target = GameBalance.Stage1FightHpElite; break;
            case EnemyRank.Veteran: target = GameBalance.Stage1FightHpVeteran; break;
            default: target = GameBalance.Stage1FightHpNormal; break;
        }
        float f = (float)target / sum;
        if (f < GameBalance.Stage1FitMin) f = GameBalance.Stage1FitMin;
        if (f > GameBalance.Stage1FitMax) f = GameBalance.Stage1FitMax;
        return f;
    }

    private int Clamp(int value, int min, int max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    // ==================== 라운드 시작 ====================
    private void StartRound()
    {
        Round++;
        InEnemyPhase = false;
        AddLog("===== " + Round + "턴 =====");
        foreach (EnemyCombatant e in Enemies) { e.HitsThisRound = 0; e.OverloadUsed = false; }   // 3-102 딱정벌레·석상 파수꾼

        // 수호의 등불 (파티): HP 비율이 가장 낮은 아군 회복
        if (PartyHas(RelicLibrary.GuardLamp))
        {
            PlayerCombatant low = null;
            foreach (PlayerCombatant q in Players)
                if (!q.IsDead && (low == null || (float)q.Hp / q.MaxHp < (float)low.Hp / low.MaxHp)) low = q;
            if (low != null && low.Hp < low.MaxHp) AddLog("수호의 등불: " + low.Name + " HP +" + low.Heal(GameBalance.GuardLampHeal));
        }
        // 3-176 결속의 투구 (파티): HP 비율이 가장 낮은 아군 방어도 → 아래 방어도 초기화 뒤에 줌 (helmTarget 기억)
        PlayerCombatant helmTarget = null;
        if (PartyHas(RelicLibrary.UnityHelm))
        {
            foreach (PlayerCombatant q in Players)
                if (!q.IsDead && (helmTarget == null || (float)q.Hp / q.MaxHp < (float)helmTarget.Hp / helmTarget.MaxHp)) helmTarget = q;
        }
        // 3-176 결속의 횃불 (파티 보스) 페널티: 매 턴 시작 시 모두 HP -2 (HP 1 아래로는 안 내려감)
        if (PartyHas(RelicLibrary.BondTorch))
        {
            foreach (PlayerCombatant q in Players)
            {
                if (q.IsDead) continue;
                int l = System.Math.Min(GameBalance.TorchHpLoss, q.Hp - 1);
                if (l > 0) { q.LoseHp(l); AddLog("결속의 횃불: " + q.Name + " HP -" + l); }
            }
        }
        // 전술가의 지도: 엘리트·보스가 있는 전투인지
        bool bigFight = false;
        foreach (EnemyCombatant ee in Enemies) if (ee.Rank == EnemyRank.Elite || ee.IsBoss) bigFight = true;

        foreach (PlayerCombatant p in Players)
        {
            if (p.IsDead) continue;

            // 방어도: 내 턴 시작 시 0. 칠흑의 망토(밤)는 그대로, 수성의 여러 웨이브 칸은 절반(내림)
            if (p.RetainBlockOnce)
            {
                p.RetainBlockOnce = false;
                if (p.Block > 0) AddLog(p.Name + " 칠흑의 망토: 방어도 " + p.Block + " 유지");
            }
            else if (p.HasRelic(RelicLibrary.SteelBelt))
            {
                if (p.Block > 0) AddLog(p.Name + " 무거운 강철 허리띠: 방어도 " + p.Block + " 유지");
            }
            else if (KeepHalfBlock && p.Block > 0)
            {
                int kept = (int)System.Math.Floor(p.Block * GameBalance.MultiWaveBlockKeepRatio);
                AddLog(p.Name + " 방어도 " + p.Block + " → " + kept + " (절반 유지)");
                p.Block = kept;
            }
            else
            {
                p.Block = 0;
            }

            // 수호자의 낡은 방패: 지난 턴에서 남은 방어도가 있으면 +5
            if (p.Block > 0 && p.HasRelic(RelicLibrary.OldShield))
            {
                p.GainBlock(GameBalance.OldShieldBlock);
                AddLog(p.Name + " 수호자의 낡은 방패: 방어도 +" + GameBalance.OldShieldBlock);
            }
            // 서리바람 망토 (3-101): [동결]이 쌓인 적이 하나라도 있으면 방어도
            if (p.HasRelic(RelicLibrary.FrostwindCloak))
            {
                bool anyFrozen = false;
                foreach (EnemyCombatant fe in LivingEnemies()) if (fe.FreezeStacks > 0) anyFrozen = true;
                if (anyFrozen) { p.GainBlock(GameBalance.FrostwindCloakBlock); AddLog(p.Name + " 서리바람 망토: 방어도 +" + GameBalance.FrostwindCloakBlock); }
            }

            ResetTurnState(p);
            p.BlockGainMult = BaseBlockMult(p);   // 3-176: 첫 턴·[밤] 에 따라 달라지는 방어도 배율 (수확자의 영혼등불, 그림자 수확자의 반지)
            if (p == helmTarget) { p.GainBlock(GameBalance.UnityHelmBlock); AddLog("결속의 투구: " + p.Name + " 방어도 +" + GameBalance.UnityHelmBlock); }
            p.SmokeReduce = 0f;          // 연막탄은 다음 내 턴 시작까지
            p.FreeBulletsTurn = false;   // 오버드라이브는 그 턴만
            TickBurn(p);
            if (p.IsDead) continue;

            // 에너지: 3 + 보스 아티팩트 + 가드 스탠스(이번 한 번) + 마력해방(영구) + 광부의 랜턴(첫 턴)
            p.Energy = p.BaseEnergy() + p.GuardStanceEnergy + p.ManaReleaseStacks;
            if (Round == 1 && p.HasRelic(RelicLibrary.Lantern)) { p.Energy += GameBalance.LanternEnergy; AddLog(p.Name + " 광부의 랜턴: 에너지 +" + GameBalance.LanternEnergy); }
            if (Round == 1 && PartyHas(RelicLibrary.AllianceOath)) { p.Energy += GameBalance.AllianceOathEnergy; AddLog(p.Name + " 동맹의 맹세: 에너지 +" + GameBalance.AllianceOathEnergy); }
            // ---- 3-176 에너지 ----
            if (Round == 1 && p.HasRelic(RelicLibrary.EmberMatch)) { p.Energy = System.Math.Max(0, p.Energy - GameBalance.EmberMatchEnergyLoss); AddLog(p.Name + " 잿불의 성냥: 첫 턴 에너지 -" + GameBalance.EmberMatchEnergyLoss); }
            if (Round == 1 && bigFight && p.HasRelic(RelicLibrary.TacticianMap)) { p.Energy += GameBalance.TacticianEnergy; AddLog(p.Name + " 전술가의 지도: 에너지 +" + GameBalance.TacticianEnergy); }
            bool diceLost = false;
            if (p.HasRelic(RelicLibrary.GamblerDice))
            {
                if (rng.Next(2) == 0) { p.Energy += GameBalance.DiceEnergy; AddLog(p.Name + " 도박사의 주사위: 에너지 +" + GameBalance.DiceEnergy); }
                else { diceLost = true; AddLog(p.Name + " 도박사의 주사위: 꽝 (카드 -" + GameBalance.DiceDrawLoss + ")"); }
            }
            p.GuardStanceEnergy = 0;
            p.HasEndedTurn = false;

            // 지옥불의 심장 페널티: 매 내 턴 시작에 홍염 0
            if (p.HasRelic(RelicLibrary.HellfireHeart) && p.Mana > 0)
            {
                AddLog(p.Name + " 지옥불의 심장: 홍염 " + p.Mana + " → 0");
                p.Mana = 0;
            }
            // 3-176 불사조의 역린 페널티: 홍염 절반 (올림) / 불사조의 잿불 왕관 페널티: 홍염 -1
            if (p.HasRelic(RelicLibrary.PhoenixScale) && p.Mana > 1)
            {
                int half = (p.Mana + 1) / 2;
                AddLog(p.Name + " 불사조의 역린: 홍염 " + p.Mana + " → " + half);
                p.Mana = half;
            }
            if (p.HasRelic(RelicLibrary.AshCrown) && p.Mana > 0) { p.Mana = System.Math.Max(0, p.Mana - GameBalance.AshCrownFlameLoss); AddLog(p.Name + " 불사조의 잿불 왕관: 홍염 -" + GameBalance.AshCrownFlameLoss); }

            // 마나 회복: 마도사(민첩) + 마력해방 + 절대 영도의 파편
            int regen = p.ManaRegen() + p.ManaReleaseStacks * GameBalance.ManaReleaseRegen;
            if (p.Class == PlayerClass.Sniper && Round == 1) regen = 0;              // 스나이퍼 첫 턴은 시작 탄환 3 그대로
            regen += p.AmmoSupply * GameBalance.AmmoSupplyBullets;                    // 탄약보급 (매 턴)
            if (p.HasRelic(RelicLibrary.ZeroShard)) regen += GameBalance.ZeroShardRegen;
            if (p.HasRelic(RelicLibrary.TwilightRing)) regen += GameBalance.TwilightRingSoul;   // 황혼의 계약 반지 (3-99)
            if (p.HasRelic(RelicLibrary.SolarPlume)) regen += GameBalance.SolarPlumeFlame;      // 태양의 왕관 깃털 (3-99)
            // 3-176
            if (p.HasRelic(RelicLibrary.EmberCore)) regen += GameBalance.EmberCoreFlame;                                   // 꺼지지 않는 불씨핵
            if (p.Class == PlayerClass.Sniper && p.HasRelic(RelicLibrary.PrecisionWarhead)) regen -= GameBalance.WarheadRegenLoss;   // 정밀 탄두 상자 페널티
            if (p.Class == PlayerClass.FrostMage && p.HasRelic(RelicLibrary.FrostStaff)) regen -= GameBalance.FrostStaffRegenLoss;   // 서리 결정 지팡이 페널티
            bool pouchTrigger = p.Class == PlayerClass.Sniper && regen > 0 && p.HasRelic(RelicLibrary.LightPouch);       // 경량 탄창 파우치
            if (regen > 0)
            {
                int before = p.Mana;
                p.GainMana(regen);
                if (p.Mana > before) AddLog(p.Name + " " + p.ManaName + " +" + (p.Mana - before) + " (" + p.Mana + ")");
            }

            StartOfTurnPowers(p);

            // 뽑기: 5 + 가드 스탠스 - 피의 계약서 + 전령의 뿔피리(첫 턴)
            int draw = GameBalance.HandSize + p.GuardStanceDraw;
            p.GuardStanceDraw = 0;
            if (p.DrawPenalty > 0)
            {
                draw -= p.DrawPenalty;
                AddLog(p.Name + " 몬스터의 방해: 이번 턴 카드 " + p.DrawPenalty + "장 덜 뽑음");
                p.DrawPenalty = 0;
            }
            if (p.HasRelic(RelicLibrary.BloodContract)) draw -= GameBalance.BloodContractDrawLoss;
            if (Round == 1 && p.HasRelic(RelicLibrary.WarHorn)) { draw += GameBalance.WarHornDraw; AddLog(p.Name + " 전령의 뿔피리: " + GameBalance.WarHornDraw + "장 더"); }
            if (Round == 1 && PartyHas(RelicLibrary.CommanderBugle)) draw += GameBalance.CommanderBugleDraw;   // 지휘관의 나팔 (파티)
            // ---- 3-176 뽑기 ----
            if (p.HasRelic(RelicLibrary.CommanderMedal)) draw += Round == 1 ? GameBalance.MedalFirstDraw : -GameBalance.MedalLaterDrawLoss;
            if (Round == 1 && p.HasRelic(RelicLibrary.SwiftBoots)) draw += GameBalance.SwiftBootsDraw;
            if (Round == 1 && bigFight && p.HasRelic(RelicLibrary.TacticianMap)) draw += GameBalance.TacticianDraw;
            if (p.HasRelic(RelicLibrary.PurifyHourglass)) draw += GameBalance.PurifyHourglassDraw;
            if (diceLost) draw -= GameBalance.DiceDrawLoss;
            if (p.HasRelic(RelicLibrary.CommanderFlag))
            {
                if (Round == 1) draw -= GameBalance.FlagFirstDrawLoss;
                if (p.FlagDrawNext > 0) { draw += p.FlagDrawNext; AddLog(p.Name + " 작전 지휘관의 깃발: 카드 +" + p.FlagDrawNext); }
            }
            p.FlagDrawNext = 0;
            if (p.IsNight && p.HasRelic(RelicLibrary.ShadowReaperRing)) draw += GameBalance.ShadowRingDraw;
            if (pouchTrigger)
            {
                draw += GameBalance.LightPouchDraw;
                p.GainBlock(GameBalance.LightPouchBlock);
                AddLog(p.Name + " 경량 탄창 파우치: 방어도 +" + GameBalance.LightPouchBlock + ", 카드 +" + GameBalance.LightPouchDraw);
            }
            if (p.HasRelic(RelicLibrary.GamblerCoin) && rng.Next(100) < GameBalance.GamblerCoinChance) { draw += 1; AddLog(p.Name + " 도박사의 동전: 카드 +1"); }
            if (draw < 0) draw = 0;
            drawingTurnStart = true;
            Draw(p, draw);
            drawingTurnStart = false;

            // 3-176 정화의 모래시계: 손패·뽑을 더미의 방해 카드 최대 2장 소멸, 1장당 카드 +1 / 페널티 HP -1
            if (p.HasRelic(RelicLibrary.PurifyHourglass))
            {
                int removed = 0;
                for (int pass = 0; pass < 2 && removed < GameBalance.PurifyHourglassMax; pass++)
                {
                    List<CardData> pile = pass == 0 ? p.Hand : p.DrawPile;
                    for (int ci = pile.Count - 1; ci >= 0 && removed < GameBalance.PurifyHourglassMax; ci--)
                    {
                        if (!(pile[ci].Unplayable || pile[ci].StatusCard)) continue;
                        AddLog(p.Name + " 정화의 모래시계: [" + pile[ci].Name + "] 소멸");
                        pile.RemoveAt(ci);
                        removed++;
                    }
                }
                if (removed > 0) Draw(p, removed);
                int l = System.Math.Min(GameBalance.PurifyHourglassHpLoss, p.Hp - 1);
                if (l > 0) { p.LoseHp(l); AddLog(p.Name + " 정화의 모래시계: HP -" + l); }
            }
            // 3-176 희석된 정화 시계: 손패의 방해 카드 1장 소멸 + 1장 (전투당 3번)
            if (p.HasRelic(RelicLibrary.PurifyTimer) && p.PurifyTimerUsed < GameBalance.PurifyTimerUses)
            {
                int ci = p.Hand.FindIndex(hcard => hcard.Unplayable || hcard.StatusCard);   // FindIndex = 조건에 맞는 첫 번째 위치 (자바 IntStream...findFirst 와 비슷), 없으면 -1
                if (ci >= 0)
                {
                    p.PurifyTimerUsed++;
                    AddLog(p.Name + " 희석된 정화 시계: [" + p.Hand[ci].Name + "] 소멸 (" + p.PurifyTimerUsed + "/" + GameBalance.PurifyTimerUses + ")");
                    p.Hand.RemoveAt(ci);
                    Draw(p, 1);
                }
            }

            // 레드 위스퍼 페널티: 무작위 손패 1장 이번 턴 에너지 +1
            //   2026-11-15 (3-72): 낼 수 없는 카드([상처])는 빼고 고름. 상처에 걸리면 페널티가 사라져서 공짜가 됐음
            //   (아래 '초월' 과 같은 방식: 조건에 맞는 카드만 새 리스트에 모은 뒤 그중에서 무작위)
            if (p.HasRelic(RelicLibrary.RedWhisper))
            {
                List<CardData> playable = new List<CardData>();
                foreach (CardData hc in p.Hand) if (!hc.Unplayable && !hc.Sealed) playable.Add(hc);   // 3-196: 사용 금지 카드도 뺌
                if (playable.Count > 0)
                {
                    CardData rw = playable[rng.Next(playable.Count)];
                    rw.TempDiscount -= GameBalance.RedWhisperCostUp;   // TempDiscount 가 음수 = 그만큼 비싸짐 (턴 끝에 0으로 돌아감)
                    AddLog(p.Name + " 레드 위스퍼: [" + rw.Name + "] 이번 턴 에너지 +" + GameBalance.RedWhisperCostUp);
                }
            }
            // 사선의 관측경 페널티: 조준 1개당 HP -2
            if (p.HasRelic(RelicLibrary.DeadlyObserver) && p.AimStacks > 0)
            {
                int loss = p.AimStacks * GameBalance.ObserverHpPerAim;
                p.Hp = System.Math.Max(1, p.Hp - loss);
                AddLog(p.Name + " 사선의 관측경: HP -" + loss);
            }
            // 3-102 몬스터: 차원 균열의 감시자 (2턴마다 손패 1장 소멸) / 영혼 결속 마녀 (손패 1장 결속)
            TurnStartMonsterEffects(p);
            // 태양의 왕관 깃털 페널티 (3-99): 턴 시작 HP -2 (카드 효과가 아니라서 잿더미·눈물은 반응 안 함)
            if (p.HasRelic(RelicLibrary.SolarPlume))
            {
                int lostS = p.LoseHp(GameBalance.SolarPlumeHpLoss);
                AddLog(p.Name + " 태양의 왕관 깃털: HP -" + lostS);
                CheckPhoenixHeart(p);
                if (p.IsDead) { AddLog("  " + p.Name + " 쓰러짐!"); continue; }
            }
            // 고배율 거리측정기 (3-99): 턴 시작 [조준] +1
            if (p.HasRelic(RelicLibrary.Rangefinder))
            {
                AddLog(p.Name + " 고배율 거리측정기");
                GainAim(p, GameBalance.RangefinderAim, null);
            }
            // 만년설의 왕관 (3-101): 내 턴 시작 모든 적 동결
            if (p.HasRelic(RelicLibrary.GlacialCrown))
            {
                AddLog(p.Name + " 만년설의 왕관");
                foreach (EnemyCombatant ge in LivingEnemies()) AddFreeze(p, ge, GameBalance.GlacialCrownFreeze);
            }

            // 초월 (공용 파워): 무작위 손패 1장 이번 턴 에너지 0 (장수만큼)
            for (int k = 0; k < p.Transcend; k++)
            {
                List<CardData> pay = new List<CardData>();
                foreach (CardData hc in p.Hand) if (!hc.Unplayable && !hc.Sealed && EffectiveCost(p, hc) > 0) pay.Add(hc);   // 3-196
                if (pay.Count == 0) break;
                CardData pick = pay[rng.Next(pay.Count)];
                pick.TempDiscount += EffectiveCost(p, pick);
                AddLog(p.Name + " 초월: [" + pick.Name + "] 이번 턴 에너지 0");
            }

            // 과부하 축전기: 마나가 최대면 1장 뽑기 + 무작위 적 감전 스택 1
            if (p.HasRelic(RelicLibrary.Capacitor) && p.MaxMana > 0 && p.Mana >= p.MaxMana)
            {
                AddLog(p.Name + " 과부하 축전기");
                Draw(p, GameBalance.CapacitorDraw);
                EnemyCombatant e = RandomLivingEnemy();
                if (e != null) AddShock(p, e, GameBalance.CapacitorShock);
            }

            // 영혼 봉인: 봉인한 카드가 에너지 -1 로 돌아옴
            if (p.SealedCard != null)
            {
                CardData sealedCard = p.SealedCard;
                p.SealedCard = null;
                sealedCard.TempDiscount += GameBalance.SoulSealDiscount;
                if (p.Hand.Count < GameBalance.MaxHandSize) p.Hand.Add(sealedCard); else p.DiscardPile.Add(sealedCard);
                AddLog(p.Name + " 영혼 봉인 해제: [" + sealedCard.Name + "] 에너지 -" + GameBalance.SoulSealDiscount);
            }

            // 반월참 차징 발동 (지난 턴에 쓴 것)
            while (p.PendingHalfMoons.Count > 0 && Result == BattleResult.Ongoing)
            {
                int dmg = p.PendingHalfMoons[0];
                p.PendingHalfMoons.RemoveAt(0);
                AddLog(p.Name + " 차징 완료!");
                FireHalfMoon(p, dmg);
            }
        }

        CheckResult();
        TrySpawnNextWave();
    }

    // 내 턴 시작에 발동하는 파워들
    private void StartOfTurnPowers(PlayerCombatant p)
    {
        if (p.NightForever)
        {
            p.GainMana(GameBalance.DeathNightSoulPerTurn);
            AddLog(p.Name + " 죽음의 밤: 영혼 +" + GameBalance.DeathNightSoulPerTurn);
            NightRelics(p);   // 죽음의 밤 중에는 매 턴 시작에 펜던트·망토·촛대 발동
        }
        if (p.PhoenixGuardStacks > 0)
        {
            int block = (GameBalance.PGuardTurnBlock + (int)System.Math.Floor(p.Con * GameBalance.PGuardPerCon)) * p.PhoenixGuardStacks;
            p.GainBlock(block);
            AddLog(p.Name + " 피닉스의 가호: 방어도 +" + block);
        }
        if (p.EternalEmberStacks > 0 && p.Hp <= p.MaxHp * p.EternalEmberRatio)
        {
            p.GainMana(GameBalance.EternalEmberFlame * p.EternalEmberStacks);
            p.GainBlock(GameBalance.EternalEmberBlock * p.EternalEmberStacks);
            AddLog(p.Name + " 영원한 불씨: 홍염 +" + p.EternalEmberStacks + ", 방어도 +" + (GameBalance.EternalEmberBlock * p.EternalEmberStacks));
        }
        if (p.ReaperStepsStacks > 0)
        {
            EnemyCombatant e = RandomLivingEnemy();
            if (e != null)
            {
                AddLog(p.Name + " 사신의 발걸음");
                AddExecuteStacks(p, e, p.ReaperStepsStacks);
            }
        }
        if (p.BlizzardStacks > 0)
        {
            p.BlizzardTurn++;
            if (p.BlizzardEveryTurn || p.BlizzardTurn % 2 == 0)
            {
                AddLog(p.Name + " 눈보라");
                foreach (EnemyCombatant e in LivingEnemies()) AddFreeze(p, e, p.BlizzardStacks);
            }
        }
    }

    private int FindNextPlayer(int start)
    {
        for (int i = start; i < Players.Count; i++)
        {
            if (!Players[i].IsDead && !Players[i].HasEndedTurn) return i;
        }
        return -1;
    }

    // ==================== 카드 사용 ====================

    // 이 카드를 지금 쓸 수 있는지 검사. 못 쓰면 reason 에 이유를 넣어 줍니다.
    public bool CanPlay(PlayerCombatant player, CardData card, out string reason)
    {
        reason = "";
        if (Result != BattleResult.Ongoing) { reason = "전투가 끝났습니다"; return false; }
        if (player.IsDead) { reason = player.Name + "은(는) 쓰러졌습니다"; return false; }
        if (player.HasEndedTurn) { reason = player.Name + "은(는) 이미 턴을 종료했습니다"; return false; }
        if (player.PendingSealPick) { reason = "봉인할 카드를 먼저 고르세요"; return false; }
        if (player.PendingDiscardPick > 0) { reason = "버릴 카드를 먼저 고르세요"; return false; }
        if (card.Unplayable) { reason = "[" + card.Name + "] 은(는) 낼 수 없는 카드입니다"; return false; }
        if (card.Sealed) { reason = "[" + card.Name + "] 은(는) 이번 전투에서 사용 금지입니다 (지옥)"; return false; }   // 3-196
        if (EffectiveCost(player, card) > player.Energy) { reason = "에너지가 부족합니다"; return false; }
        if (Round < card.MinRound) { reason = card.MinRound + "턴부터 사용 가능"; return false; }
        if (card.Id == "eclipse" && player.EclipseUsed) { reason = "월식은 전투당 1번만 쓸 수 있습니다"; return false; }
        if (EffectiveManaCost(player, card) > player.Mana) { reason = player.ManaName + " 부족 (" + EffectiveManaCost(player, card) + " 필요)"; return false; }
        if (card.UsesAllMana && player.Mana < 1) { reason = player.ManaName + "이 없습니다"; return false; }
        return true;
    }

    // 실제로 내야 하는 에너지
    public int EffectiveCost(PlayerCombatant player, CardData card)
    {
        if (card.Id == "half_moon" && player.EclipseActive) return 0;   // 월식: 이번 턴 반월참 0
        int cost = card.Cost - card.TempDiscount - card.BattleDiscount;
        if (card.Id == "sn_shot" && player.HasRelic(RelicLibrary.TacticalGloves) && !player.GlovesUsed) return 0;   // 전술 자율 장갑: 첫 [사격] 에너지 0
        if (card.Type == CardType.Skill && player.NextSkillDiscount > 0) cost -= 1;   // 다음 스킬 N장 -1 (장마다 1씩)
        if (card.IsBayonet && player.HasRelic(RelicLibrary.BallisticModule)) cost += GameBalance.BallisticBayonetCost;   // 3-176 초정밀 탄도 모듈 페널티
        return System.Math.Max(0, cost);
    }

    // 실제로 내야 하는 마나 (이온화: 다음 [벼락] 카드 마나 0, 낙뢰 제외)
    public int EffectiveManaCost(PlayerCombatant player, CardData card)
    {
        if (card.IsLightning && player.NextLightningFree && card.Id != "lightning_fall") return 0;
        if (player.FreeBulletsTurn) return 0;   // 오버드라이브: 이번 턴 탄환 소모 0
        // 3-176: 속사 탄창 (이번 턴 탄환 카드 2장 무료), 서리 결정 지팡이 (이번 턴 첫 마나 카드 무료)
        if (card.ManaCost > 0 && player.Class == PlayerClass.Sniper && player.RapidMagLeft > 0) return 0;
        if (card.ManaCost > 0 && player.Class == PlayerClass.FrostMage && !player.FrostStaffUsed && player.HasRelic(RelicLibrary.FrostStaff)) return 0;
        int mc = card.ManaCost;
        if (mc > 0 && player.HasRelic(RelicLibrary.StormcoreStaff))   // 폭풍핵 지팡이 (3-101): 마나 75% (반올림)
            mc = DamageCalculator.RoundHalfUp(mc * GameBalance.StormcoreManaRatio);
        if (mc > 1 && player.Class == PlayerClass.BlackScythe && player.HasRelic(RelicLibrary.SoulbondHaft)) mc -= 1;   // 3-176 영혼 결속의 낫자루 (최소 1)
        return mc;
    }

    // handIndex 번째 손패 카드를 target 에게 사용. 성공하면 true
    public bool PlayCard(PlayerCombatant player, int handIndex, EnemyCombatant target)
    {
        if (handIndex < 0 || handIndex >= player.Hand.Count) return false;

        CardData card = player.Hand[handIndex];
        string reason;
        if (!CanPlay(player, card, out reason))
        {
            AddLog("사용 불가: " + reason);
            return false;
        }

        // 적 1명을 고르는 카드인데 대상이 없거나 죽었으면, 살아 있는 첫 번째 적으로
        if (card.Target == TargetType.SingleEnemy && (target == null || target.IsDead))
        {
            target = FirstLivingEnemy();
            if (target == null) return false;
        }

        // 1) 손에서 빼고 에너지·마나 지불
        player.Hand.RemoveAt(handIndex);
        int cost = EffectiveCost(player, card);
        int manaCost = EffectiveManaCost(player, card);
        player.Energy -= cost;
        if (card.Type == CardType.Skill && player.NextSkillDiscount > 0 && card.Cost - card.TempDiscount - card.BattleDiscount > 0) player.NextSkillDiscount--;
        if (card.IsLightning && player.NextLightningFree && card.Id != "lightning_fall") player.NextLightningFree = false;
        if (card.Id == "sn_shot" && player.HasRelic(RelicLibrary.TacticalGloves) && !player.GlovesUsed)
        {
            player.GlovesUsed = true;
            AddLog("  전술 자율 장갑: [사격] 에너지 0");
        }
        card.TempDiscount = 0;
        // 3-176: 무료로 낸 탄환·마나 카드 횟수 줄이기
        if (card.ManaCost > 0 && !card.UsesAllMana && !player.FreeBulletsTurn && player.Class == PlayerClass.Sniper && player.RapidMagLeft > 0) { player.RapidMagLeft--; AddLog("  속사 탄창: 탄환 소모 0 (남은 " + player.RapidMagLeft + ")"); }
        else if (card.ManaCost > 0 && !card.UsesAllMana && player.Class == PlayerClass.FrostMage && !player.FrostStaffUsed && player.HasRelic(RelicLibrary.FrostStaff)) { player.FrostStaffUsed = true; AddLog("  서리 결정 지팡이: 마나 소모 0"); }
        int manaSpent = card.UsesAllMana ? player.Mana : manaCost;
        player.Mana -= manaSpent;

        string targetName = (card.Target == TargetType.SingleEnemy) ? " → " + target.Name : "";
        AddLog(player.Name + " [" + card.Name + "] 사용" + targetName);

        // 2) 효과 부품을 순서대로 실행 (카드 성격을 기억해 두면 파워 보너스가 알아서 붙음)
        EffectContext ctx = new EffectContext();
        ctx.Battle = this;
        ctx.User = player;
        ctx.Target = (card.Target == TargetType.SingleEnemy) ? target : null;
        ctx.ManaSpent = manaSpent;
        ctx.Card = card;

        attackCardContext = card.Type == CardType.Attack;
        lightningContext = card.IsLightning;
        basicContext = card.IsBasicAttack;
        // 3-176: 카드 성격 (새 아티팩트 피해 보너스용)
        bulletAttackContext = attackCardContext && player.Class == PlayerClass.Sniper && card.ManaCost > 0;
        manaAttackContext = attackCardContext && player.Class == PlayerClass.FrostMage && card.ManaCost > 0;
        bayonetContext = card.IsBayonet;
        firstAttackContext = attackCardContext && player.AttacksPlayedThisTurn == 0;
        glacialHeartDone = false; lightLensDone = false;
        // 스나이퍼 [조준]: 공격 카드면 피해 +50% × 조준 수 (헤드샷은 대신 2배). 쓰면 조준이 사라짐
        bool aimed = card.Type == CardType.Attack && player.AimStacks > 0;
        player.AimedThisCard = aimed;
        aimExtra = aimed && !card.Id.StartsWith("sn_headshot") ? GameBalance.AimBonus * player.AimStacks : 0f;
        if (aimed) AddLog("  [조준] " + player.AimStacks + " → " + (card.Id.StartsWith("sn_headshot") ? "헤드샷 피해 2배" : "피해 +" + (int)(aimExtra * 100) + "%"));
        // 시간 왜곡 (공용): 이 카드가 2번 발동
        bool twice = player.DoubleNextCard;
        if (twice) player.DoubleNextCard = false;
        // 영겁의 화로 페널티 (3-99): 카드 효과로 얻는 방어도 60% (카드가 끝나면 원래 배율로)
        bool brazier = player.HasRelic(RelicLibrary.EternalBrazier);
        if (brazier) player.BlockGainMult = BaseBlockMult(player) * GameBalance.BrazierCardBlockRatio;
        for (int rep = 0; rep < (twice ? 2 : 1); rep++)
        {
            if (rep == 1) AddLog("  시간 왜곡: [" + card.Name + "] 한 번 더!");
            foreach (CardEffect effect in card.Effects)
            {
                effect.Apply(ctx);
            }
        }
        if (brazier) player.BlockGainMult = BaseBlockMult(player);
        if (aimed && !player.HasRelic(RelicLibrary.DeadlyObserver)) player.AimStacks = 0;   // 사선의 관측경: 조준이 사라지지 않음
        player.AimedThisCard = false;
        // 대검 코등이: [총검술] 카드마다 방어도
        if (card.IsBayonet && player.HasRelic(RelicLibrary.Crossguard))
        {
            player.GainBlock(GameBalance.CrossguardBlock);
            AddLog("  균형 잡힌 대검 코등이: 방어도 +" + GameBalance.CrossguardBlock);
        }
        // 전술 소음기 (3-99): [사격] 카드마다 방어도
        if (card.Id == "sn_shot" && player.HasRelic(RelicLibrary.Suppressor))
        {
            player.GainBlock(GameBalance.SuppressorBlock);
            AddLog("  전술 소음기: 방어도 +" + GameBalance.SuppressorBlock);
        }
        // 불씨 주머니 (3-99): 이번 턴 첫 공격 카드의 대상에게 발화 (AttacksPlayedThisTurn 은 아래에서 +1 되므로 아직 0)
        if (card.Type == CardType.Attack && player.AttacksPlayedThisTurn == 0 && player.HasRelic(RelicLibrary.EmberPouch))
        {
            EnemyCombatant bt = (ctx.Target != null && !ctx.Target.IsDead) ? ctx.Target : RandomLivingEnemy();
            if (bt != null) { AddLog("  불씨 주머니"); ApplyBurn(player, bt, GameBalance.EmberPouchBurn); }
        }
        // 정전기의 반지 (3-101): 매 턴 처음 마나를 쓴 카드 → 무작위 적 감전 스택
        if (manaSpent > 0 && player.Class == PlayerClass.FrostMage && !player.StaticRingUsed && player.HasRelic(RelicLibrary.StaticRing))
        {
            player.StaticRingUsed = true;
            EnemyCombatant se = RandomLivingEnemy();
            if (se != null) { AddLog("  정전기의 반지"); AddShock(player, se, GameBalance.StaticRingShock); }
        }
        // 원혼의 향로 (3-99): 영혼을 쓴 카드마다 무작위 적 처형 스택
        if (manaSpent > 0 && player.Class == PlayerClass.BlackScythe && player.HasRelic(RelicLibrary.SpiritCenser))
        {
            EnemyCombatant ce = RandomLivingEnemy();
            if (ce != null) { AddLog("  원혼의 향로"); AddExecuteStacks(player, ce, GameBalance.SpiritCenserStacks); }
        }
        // 화약 냄새 나는 반지: 탄환을 써서 0발이 되면 무작위 적에게 피해
        if (manaSpent > 0 && player.Class == PlayerClass.Sniper && player.Mana == 0 && player.HasRelic(RelicLibrary.GunpowderRing))
        {
            EnemyCombatant ge = RandomLivingEnemy();
            if (ge != null)
            {
                int lostG = ge.TakeAttackDamage(GameBalance.GunpowderRingDamage, false);
                ge.LastHitBy = player;
                AddLog("  화약 냄새 나는 반지: " + ge.Name + " " + GameBalance.GunpowderRingDamage + " 피해 (HP -" + lostG + ")");
                AfterEnemyHpLoss(ge);
            }
        }
        // ---- 3-176 카드를 낸 뒤 반응 ----
        if (manaSpent > 0 && player.Class == PlayerClass.BlackScythe)
        {
            if (player.HasRelic(RelicLibrary.SoulbondBlade))   // 영혼 결속의 노리개: 쓴 영혼 1당 처형 스택 1
            {
                EnemyCombatant be = (ctx.Target != null && !ctx.Target.IsDead) ? ctx.Target : RandomLivingEnemy();
                if (be != null) { AddLog("  영혼 결속의 노리개"); AddExecuteStacks(player, be, manaSpent); }
            }
            if (player.HasRelic(RelicLibrary.HungryMask)) { player.GainBlock(GameBalance.HungryMaskBlock); AddLog("  굶주린 그림자 가면: 방어도 +" + GameBalance.HungryMaskBlock); }
        }
        if (manaSpent > 0 && player.Class == PlayerClass.CosmicPhoenix && player.HasRelic(RelicLibrary.HotFlint))   // 뜨거운 불씨 부싯돌
        {
            EnemyCombatant fe = RandomLivingEnemy();
            if (fe != null)
            {
                int lostF = fe.TakeAttackDamage(GameBalance.HotFlintDamage, false);
                fe.LastHitBy = player;
                AddLog("  뜨거운 불씨 부싯돌: " + fe.Name + " " + GameBalance.HotFlintDamage + " 피해 (HP -" + lostF + ")");
                AfterEnemyHpLoss(fe);
            }
        }
        if (manaSpent > 0 && player.Class == PlayerClass.FrostMage && player.HasRelic(RelicLibrary.FrostCrystalOrb)
            && player.CrystalOrbProcs < GameBalance.CrystalOrbMaxProcs && rng.Next(100) < GameBalance.CrystalOrbChance)   // 서리 마력의 수정구
        {
            player.CrystalOrbProcs++;
            RelicMana(player, GameBalance.CrystalOrbMana, "서리 마력의 수정구");
            Draw(player, GameBalance.CrystalOrbDraw);
        }
        aimExtra = 0f;
        attackCardContext = false; lightningContext = false; basicContext = false;
        bulletAttackContext = false; manaAttackContext = false; bayonetContext = false; firstAttackContext = false;

        // 진홍의 탄창: 공격 카드 2장마다 탄환 +2, 1장 뽑기
        if (card.Type == CardType.Attack && player.CrimsonMag > 0)
        {
            player.CrimsonCount++;
            if (player.CrimsonCount >= GameBalance.CrimsonEvery)
            {
                player.CrimsonCount = 0;
                player.GainMana(GameBalance.CrimsonBullets * player.CrimsonMag);
                AddLog("  진홍의 탄창: 탄환 +" + (GameBalance.CrimsonBullets * player.CrimsonMag));
                Draw(player, GameBalance.CrimsonDraw * player.CrimsonMag);
            }
        }

        // 3) 이번 턴 기록 + 반응하는 파워
        player.CardsPlayedThisTurn++;
        // 3-176 신속의 깃털신: 3번째, 6번째… 카드마다 1장
        if (player.HasRelic(RelicLibrary.SwiftGreaves) && player.CardsPlayedThisTurn % GameBalance.SwiftGreavesEvery == 0)
        {
            AddLog("  신속의 깃털신: 1장 뽑기");
            Draw(player, 1);
        }
        if (card.Type == CardType.Attack) player.AttacksPlayedThisTurn++;
        if (card.IsLightning)
        {
            player.LightningPlayedThisTurn++;
            if (player.ThunderGodActive && player.ThunderGodStacks < GameBalance.ThunderGodMax)
            {
                player.ThunderGodStacks++;
                AddLog("  뇌신 권능: [벼락] 피해 +" + (player.ThunderGodStacks * player.ThunderGodBonus));
            }
        }
        if (manaSpent > 0 && player.Class == PlayerClass.CosmicPhoenix) SunWrath(player);   // 홍염 소모

        // 4) 사용한 카드 정리: 파워 → 전투 끝까지 유지 / 소멸 → 소멸 더미 / 그 외 → 버린 카드 더미
        if (card.Type == CardType.Power) player.PowersInPlay.Add(card);
        else if (card.Exhaust) { player.ExhaustPile.Add(card); OnPlayerCardsLost(1); }   // 망령 등불지기 (3-102)
        else player.DiscardPile.Add(card);
        card.Bound = false;   // 결속 카드를 냈으면 결속 풀림 (3-102)

        CheckResult();
        TrySpawnNextWave();   // 이 카드로 마지막 적을 잡았으면 다음 웨이브가 바로 들어옴
        return true;
    }

    // 포션 쓰기 (2026-11-15): 내 차례에만. 에너지 안 듦. 쓰면 사라짐
    // target = 회복·강화 포션을 받을 아군 (2026-11-15: 동료에게도 쓸 수 있음). null 이면 자기 자신
    //   화염·취약 포션은 원래대로 모든 적에게
    public static bool PotionTargetsAlly(string potionId)
    {
        return potionId == PotionLibrary.Heal || potionId == PotionLibrary.Power || potionId == PotionLibrary.Block;   // 3-157: 방어 포션도
    }

    // 3-157 불사조 깃털: 쓰러진 동료에게만 씀 (대상 = 쓰러진 사람)
    public static bool PotionTargetsDead(string potionId)
    {
        return potionId == PotionLibrary.Phoenix;
    }

    public bool UsePotion(PlayerCombatant player, int index, PlayerCombatant target = null)
    {
        if (Result != BattleResult.Ongoing || !CanAct(player)) return false;
        if (index < 0 || index >= player.Potions.Count) return false;
        string pid = player.Potions[index];
        if (PotionTargetsDead(pid))
        {
            // 불사조 깃털: 대상이 꼭 쓰러진 동료여야 함 (없으면 안 쓰고 남겨 둠)
            if (target == null || !target.IsDead || !Players.Contains(target)) return false;
        }
        else
        {
            if (target == null) target = player;
            if (target.IsDead || !Players.Contains(target)) return false;   // 쓰러진 동료에게는 못 씀
        }
        PotionData potion = PotionLibrary.Get(pid);
        player.Potions.RemoveAt(index);
        if (potion == null) return false;
        AddLog(player.Name + " 포션 사용: " + potion.Name + (target != player && PotionTargetsAlly(potion.Id) ? " → " + target.Name : ""));
        // 3-176 연금술사의 증류기: 회복·화염·강화·방어 포션 효과 +30% (쓰는 사람이 가졌을 때)
        float still = player.HasRelic(RelicLibrary.AlchemistStill) ? 1f + GameBalance.AlchemistStillBonus : 1f;
        if (still > 1f) AddLog("  연금술사의 증류기: 효과 +" + (int)System.Math.Round(GameBalance.AlchemistStillBonus * 100) + "%");
        switch (potion.Id)
        {
            case PotionLibrary.Heal:
                AddLog("  " + target.Name + " HP +" + target.Heal(DamageCalculator.RoundHalfUp(target.MaxHp * GameBalance.PotionHealRatio * still)));
                break;
            case PotionLibrary.Fire:
                int fireDmg = DamageCalculator.RoundHalfUp(GameBalance.PotionFireDamage * still);
                foreach (EnemyCombatant e in LivingEnemies())
                {
                    int lost = e.TakeAttackDamage(fireDmg, false, 0f);
                    e.LastHitBy = player;
                    AddLog("  " + e.Name + "에게 " + fireDmg + " 피해 (HP -" + lost + ")");
                    AfterEnemyHpLoss(e);
                }
                break;
            case PotionLibrary.Power:
                target.PotionPowerBonus += DamageCalculator.RoundHalfUp(GameBalance.PotionPowerBonus * still);
                AddLog("  " + target.Name + " 이번 전투 공격 피해 +" + target.PotionPowerBonus);
                break;
            case PotionLibrary.Vulnerable:
                foreach (EnemyCombatant e in LivingEnemies()) ApplyStatus(e, StatusType.Vulnerable, GameBalance.PotionVulnTurns);
                break;
            // ---- 3-157 새 포션 ----
            case PotionLibrary.Block:
                int potBlock = DamageCalculator.RoundHalfUp(GameBalance.PotionBlock * still);
                target.GainBlock(potBlock);
                AddLog("  " + target.Name + " 방어도 +" + potBlock);
                break;
            case PotionLibrary.Energy:
                player.Energy += GameBalance.PotionEnergy;
                AddLog("  " + player.Name + " 에너지 +" + GameBalance.PotionEnergy);
                break;
            case PotionLibrary.Swift:
                Draw(player, GameBalance.PotionDraw);
                break;
            case PotionLibrary.Weak:
                foreach (EnemyCombatant e in LivingEnemies()) ApplyStatus(e, StatusType.Weak, GameBalance.PotionWeakTurns);
                break;
            case PotionLibrary.Phoenix:
                RevivePlayer(target, GameBalance.PotionReviveRatio);
                break;
        }
        CheckResult();
        TrySpawnNextWave();
        return true;
    }

    // 3-157: 쓰러진 플레이어 부활 (불사조 깃털)
    //   HP = 최대 HP × ratio (최소 1). 방어도·상태이상은 지우고, 손에 남아 있던 카드는 버린 카드 더미로
    //   이번 턴에는 행동하지 않음 (턴 종료 상태) → 다음 플레이어 턴부터 평소처럼 에너지·카드를 받음
    void RevivePlayer(PlayerCombatant p, float ratio)
    {
        p.Hp = System.Math.Max(1, DamageCalculator.RoundHalfUp(p.MaxHp * ratio));
        p.Block = 0;
        p.Statuses.Clear();
        p.DiscardPile.AddRange(p.Hand);
        p.Hand.Clear();
        p.HasEndedTurn = true;
        fallenSeen.Remove(p);   // 3-176 전우애의 방패: 다시 쓰러지면 또 발동
        AddLog("  " + p.Name + " 부활! HP " + p.Hp + " / " + p.MaxHp + " (다음 턴부터 행동)");
    }

    // 영혼 봉인: 고른 손패를 봉인 → 다음 턴 시작에 에너지 -1 로 돌아옴
    public bool SealCard(PlayerCombatant player, int handIndex)
    {
        if (!player.PendingSealPick || handIndex < 0 || handIndex >= player.Hand.Count) return false;
        CardData card = player.Hand[handIndex];
        player.Hand.RemoveAt(handIndex);
        player.SealedCard = card;
        player.PendingSealPick = false;
        AddLog(player.Name + " [" + card.Name + "] 봉인 (다음 턴에 에너지 -" + GameBalance.SoulSealDiscount + "로 돌아옴)");
        return true;
    }

    // 전술 후퇴 (2026-11-15): 고른 손패를 버리고 탄환 +N
    public bool DiscardPick(PlayerCombatant player, int handIndex)
    {
        if (player.PendingDiscardPick <= 0 || handIndex < 0 || handIndex >= player.Hand.Count) return false;
        CardData card = player.Hand[handIndex];
        player.Hand.RemoveAt(handIndex);
        player.DiscardPile.Add(card);
        card.Bound = false;
        OnPlayerCardsLost(1);   // 망령 등불지기 (3-102)
        int bullets = player.PendingDiscardPick;
        player.PendingDiscardPick = 0;
        player.GainMana(bullets);
        AddLog(player.Name + " [" + card.Name + "] 버림 → " + player.ManaName + " +" + bullets);
        return true;
    }

    // 반월참 발동: 모든 적에게 20 피해 (강화 28), 밤이면 1.5배
    public void FireHalfMoon(PlayerCombatant player, int damage)
    {
        float bonus = player.IsNight ? GameBalance.HalfMoonNightBonus : 0f;
        string tag = player.IsNight ? " [반월참 x1.5]" : " [반월참]";
        bool before = attackCardContext;
        attackCardContext = true;
        foreach (EnemyCombatant enemy in LivingEnemies())
        {
            PlayerAttack(player, enemy, damage, DamageKind.AD, bonus, true, true, tag);
        }
        attackCardContext = before;
    }

    // 플레이어 → 적 공격 (카드 효과 부품들이 부릅니다). 반환값: 준 피해(방어도 포함 숫자)
    //   extraMultiplier: 카드 자체의 배수 보너스 (보통 0)
    //   ignoreBlock    : 방어도를 완전히 무시 (피의 검무, 화염의 창)
    public int PlayerAttack(PlayerCombatant attacker, EnemyCombatant target, int baseDamage,
                            DamageKind kind, float extraMultiplier,
                            bool addStatBonus = true, bool allowCrit = true, string tag = "", bool ignoreBlock = false)
    {
        if (target == null || target.IsDead) return 0;

        // 고정 보너스 모으기
        int flatBonus = attacker.WeaponBlade * GameBalance.BladeDamagePerLevel;   // 대장간 무기 강화 (+2 / 단계)
        flatBonus += attacker.PotionPowerBonus;                                     // 강화 포션 (이번 전투)
        string bonusText = "";
        if (attacker.NightForever) { flatBonus += GameBalance.DeathNightFlatBonus; bonusText += " [밤 +" + GameBalance.DeathNightFlatBonus + "]"; }
        if (attackCardContext)
        {
            if (attacker.FullMoonStacks > 0) flatBonus += attacker.FullMoonBonus;
            if (attacker.RedMoonBonus > 0 && attacker.Hp <= attacker.MaxHp * GameBalance.RedMoonHpRatio)
            {
                flatBonus += attacker.RedMoonBonus;
                bonusText += " [적월]";
            }
            if (basicContext && attacker.FlameTrainingStacks > 0) flatBonus += GameBalance.FlameTrainingDmg * attacker.FlameTrainingStacks;
        }
        if (lightningContext && attacker.ThunderGodStacks > 0) flatBonus += attacker.ThunderGodBonus * attacker.ThunderGodStacks;
        // 빙뢰의 마도서 (3-101): [동결]이 쌓인 적에게 [벼락] 피해 +30%
        if (lightningContext && target.FreezeStacks > 0 && attacker.HasRelic(RelicLibrary.FrostreignTome))
        {
            extraMultiplier += GameBalance.FrostreignTomeBonus;
            bonusText += " [빙뢰 +" + (int)System.Math.Round(GameBalance.FrostreignTomeBonus * 100) + "%]";
        }
        // 스나이퍼 [표식]: 1개당 +1 (누가 때려도). 처형인의 시선: 표식 있는 적에게 +
        if (target.Marks > 0)
        {
            flatBonus += target.Marks;
            bonusText += " [표식 +" + target.Marks + "]";
            if (attacker.ExecutionerGaze > 0) { flatBonus += attacker.ExecutionerGaze; bonusText += " [처형인 +" + attacker.ExecutionerGaze + "]"; }
            if (attackCardContext && attacker.HasRelic(RelicLibrary.InfraredSensor)) { extraMultiplier += GameBalance.InfraredBonus; bonusText += " [적외선 +" + (int)(GameBalance.InfraredBonus * 100) + "%]"; }
        }
        if (attackCardContext && aimExtra > 0f) extraMultiplier += aimExtra;
        // ---- 3-176 새 아티팩트: 공격 카드 피해 보너스 ----
        if (attackCardContext)
        {
            if (attacker.HasRelic(RelicLibrary.SharpShard)) flatBonus += GameBalance.SharpShardDamage;
            if (attacker.HasRelic(RelicLibrary.HungryDoll)) flatBonus += GameBalance.HungryDollDamage;
            if (firstAttackContext && attacker.HasRelic(RelicLibrary.OverheatedStone)) { flatBonus += GameBalance.OverheatedDamage; bonusText += " [과열 +" + GameBalance.OverheatedDamage + "]"; }
            if (bulletAttackContext && attacker.HasRelic(RelicLibrary.LightSuppressor)) flatBonus += GameBalance.LightSuppressorDamage;
            if (bayonetContext && attacker.HasRelic(RelicLibrary.LightSuppressor)) flatBonus -= GameBalance.LightSuppressorBayonetLoss;
            if (manaAttackContext && attacker.HasRelic(RelicLibrary.FrostShardRing)) flatBonus += GameBalance.FrostShardDamage;
            if (attacker.IsNight && attacker.HasRelic(RelicLibrary.ShadowReaperRing)) { flatBonus += GameBalance.ShadowRingDamage; bonusText += " [그림자 반지 +" + GameBalance.ShadowRingDamage + "]"; }
            if (attacker.HasRelic(RelicLibrary.OnslaughtHelm)) extraMultiplier += GameBalance.OnslaughtBonus;
            if (PartyHas(RelicLibrary.BondTorch)) extraMultiplier += GameBalance.TorchBonus;
            if (bulletAttackContext && attacker.HasRelic(RelicLibrary.PrecisionWarhead)) { extraMultiplier += GameBalance.WarheadBonus; bonusText += " [탄두 +" + (int)System.Math.Round(GameBalance.WarheadBonus * 100) + "%]"; }
            if (aimExtra > 0f && attacker.HasRelic(RelicLibrary.BallisticModule)) { extraMultiplier += GameBalance.BallisticBonus; bonusText += " [탄도 +" + (int)System.Math.Round(GameBalance.BallisticBonus * 100) + "%]"; }
            if (target.HasStatus(StatusType.Burn) && attacker.HasRelic(RelicLibrary.PhoenixScale)) { extraMultiplier += GameBalance.PhoenixScaleBonus; bonusText += " [역린 +" + (int)System.Math.Round(GameBalance.PhoenixScaleBonus * 100) + "%]"; }
            if (target.ExecuteStacks > 0 && attacker.HasRelic(RelicLibrary.ReaperEdge)) { extraMultiplier += GameBalance.ReaperEdgeBonus; bonusText += " [영혼등불 +" + (int)System.Math.Round(GameBalance.ReaperEdgeBonus * 100) + "%]"; }
            if (target.FreezeStacks > 0 && attacker.HasRelic(RelicLibrary.FrostStaff)) { extraMultiplier += GameBalance.FrostStaffBonus; bonusText += " [서리 지팡이 +" + (int)System.Math.Round(GameBalance.FrostStaffBonus * 100) + "%]"; }
        }
        if (flatBonus < 0) flatBonus = 0;

        DamageResult result = DamageCalculator.PlayerAttack(attacker, target, baseDamage, kind,
                                                            flatBonus, extraMultiplier,
                                                            addStatBonus, allowCrit, rng);
        // 3-176 날카로운 톱니 조각: 공격 카드가 방어도 25% 더 무시 (최대 100%)
        if (attackCardContext && attacker.HasRelic(RelicLibrary.JaggedShard)) result.IgnoreRatio = System.Math.Min(1f, result.IgnoreRatio + GameBalance.JaggedIgnore);
        bool wasFrozen = target.FreezeStacks > 0;   // 만년설의 심장: 맞기 전에 동결이 있었는지
        int marksBefore = target.Marks;             // 가벼운 조준경 렌즈
        // 3-102: 몬스터 쪽 피해 감소 (해골 사수 은신, 거대 껍질 외골격)
        float taken = EnemyTakenFactor(target, ref bonusText);
        if (taken < 1f) result.Damage = (int)System.Math.Floor(result.Damage * taken);
        int blockBefore = target.Block;
        int hpLost = target.TakeAttackDamage(result.Damage, ignoreBlock, result.IgnoreRatio);
        target.LastHitBy = attacker;
        OnEnemyStruck(target, hpLost);   // 3-102 딱정벌레·석상 파수꾼·외골격

        string text = "  " + target.Name + "에게 " + result.Damage + " 피해";
        if (result.IsCrit) text += " (크리티컬!)";
        if (attacker.MarkedEnemy == target) text += " [인장]";
        if (target.HasStatus(StatusType.Vulnerable)) text += " [취약]";
        if (attacker.HasStatus(StatusType.Weak)) text += " [약화]";
        text += bonusText + tag;
        if (ignoreBlock && blockBefore > 0) text += " (방어도 무시!)";
        else if (result.IgnoreRatio > 0f && blockBefore > 0) text += " (관통 " + (int)(result.IgnoreRatio * 100) + "%)";
        text += HitResultText(result.Damage, blockBefore - target.Block, hpLost);
        AddLog(text);

        // 가시 (2026-11-15): 공격 카드로 가시 몬스터를 때리면 때린 사람이 피해를 받음 (방어도로 막을 수 있음)
        if (attackCardContext && target.Thorns > 0 && !attacker.IsDead)
        {
            int lostT = attacker.TakeAttackDamage(target.Thorns, false);
            AddLog("  " + attacker.Name + " 가시에 찔림: " + target.Thorns + " 피해" + (lostT > 0 ? " (HP -" + lostT + ")" : " (방어도로 막음)"));
            if (attacker.IsDead) AddLog("  " + attacker.Name + " 쓰러짐!");
        }

        // 3-176: 만년설의 심장 (동결이 쌓인 적을 때리면 마나), 가벼운 조준경 렌즈 (표식 5 이상이면 탄환)
        if (attackCardContext && !attacker.IsDead)
        {
            if (wasFrozen && !glacialHeartDone && attacker.HasRelic(RelicLibrary.GlacialHeart)) { glacialHeartDone = true; RelicMana(attacker, GameBalance.GlacialHeartMana, "만년설의 심장"); }
            if (marksBefore >= GameBalance.LightLensMarks && !lightLensDone && attacker.HasRelic(RelicLibrary.LightLens)) { lightLensDone = true; RelicMana(attacker, GameBalance.LightLensBullets, "가벼운 조준경 렌즈"); }
        }

        // 공격 카드가 피해를 줬을 때 반응하는 파워
        if (attackCardContext && result.Damage > 0 && !target.IsDead)
        {
            if (attacker.FullMoonStacks > 0) AddExecuteStacks(attacker, target, GameBalance.FullMoonStacks * attacker.FullMoonStacks);
            if (basicContext && attacker.FlameTrainingStacks > 0) ApplyBurn(attacker, target, GameBalance.FlameTrainingBurn * attacker.FlameTrainingStacks);
        }

        AfterEnemyHpLoss(target);

        // 전투 본능 (공용 파워): 공격 카드로 처치하면 에너지 +1, 1장 뽑기
        if (attackCardContext && target.IsDead && attacker.BattleInstinct > 0 && !attacker.IsDead)
        {
            attacker.Energy += GameBalance.CInstinctEnergy * attacker.BattleInstinct;
            AddLog("  전투 본능: 에너지 +" + (GameBalance.CInstinctEnergy * attacker.BattleInstinct));
            Draw(attacker, GameBalance.CInstinctDraw * attacker.BattleInstinct);
        }
        return result.Damage;
    }

    // 스나이퍼 [표식] 걸기 (최대 10). 약점 분석 파워가 있으면 방어도·탄환
    // [조준] 얻기 (2026-11-15): 광학 조준경 부품이 있으면 대상(없으면 무작위 적)에게 [표식]
    public void GainAim(PlayerCombatant user, int amount, EnemyCombatant target)
    {
        if (amount <= 0) return;
        user.AimStacks += amount;
        AddLog("  [조준] +" + amount + " (" + user.AimStacks + ")");
        if (user.HasRelic(RelicLibrary.BrokenScope))
        {
            EnemyCombatant t = (target != null && !target.IsDead) ? target : RandomLivingEnemy();
            if (t != null) { AddLog("  광학 조준경 부품"); AddMarks(user, t, GameBalance.BrokenScopeMark); }
        }
    }

    public void AddMarks(PlayerCombatant user, EnemyCombatant target, int amount)
    {
        if (target == null || target.IsDead || amount <= 0) return;
        int before = target.Marks;
        target.Marks = System.Math.Min(GameBalance.MarkMax, target.Marks + amount);
        AddLog("  " + target.Name + "에게 [표식] +" + (target.Marks - before) + " (" + target.Marks + ")");
        if (user != null && user.WeakAnalysis > 0)
        {
            user.GainBlock(user.WeakAnalysis);
            user.GainMana(GameBalance.AnalysisBullets);
            AddLog("  약점 분석: 방어도 +" + user.WeakAnalysis + ", 탄환 +" + GameBalance.AnalysisBullets);
        }
    }

    // 상처 등 낼 수 없는 카드 없애기 (불굴의 투지·정화). handOnly = 손패만
    public int RemoveUnplayable(PlayerCombatant p, bool handOnly)
    {
        // 3-149: 낼 수 있는 방해 카드 [점액] 도 같이 없앰 (StatusCard)
        int n = p.Hand.RemoveAll(c => c.Unplayable || c.StatusCard);
        if (!handOnly)
        {
            n += p.DrawPile.RemoveAll(c => c.Unplayable || c.StatusCard);
            n += p.DiscardPile.RemoveAll(c => c.Unplayable || c.StatusCard);
        }
        if (n > 0) AddLog("  상태이상 카드 " + n + "장 제거");
        return n;
    }

    // 전투가 시작된 뒤 첫 적 차례 전에 방어도를 줌 (장비 보강, 무기 자루)
    public void GiveStartBlock(PlayerCombatant player, int amount, string reason)
    {
        if (amount <= 0 || player.IsDead) return;
        player.GainBlock(amount);
        AddLog(player.Name + " " + reason + ": 방어도 +" + amount);
    }

    public bool Roll(float chance)
    {
        return rng.NextDouble() < chance;
    }

    public int RandomInt(int maxExclusive)
    {
        return rng.Next(maxExclusive);
    }

    // 적 HP가 줄었을 때 항상 부름: 처형 조건, 보스 강화, 처치 기록
    public void AfterEnemyHpLoss(EnemyCombatant enemy)
    {
        CheckExecute(enemy);
        // 점토 분열체 (3-102): HP 50% 이하로 살아 있으면 분열 대기 (실제 분열은 FlushSplits)
        if (enemy.Tag == EnemyLibrary.T_Clay && !enemy.IsDead && !enemy.SplitPending
            && enemy.Hp <= enemy.MaxHp * GameBalance.ClaySplitRatio)
        {
            enemy.SplitPending = true;
            AddLog("  ※ " + enemy.Name + " 몸이 갈라지기 시작한다! (작은 점토체 2마리로 분열)");
        }
        if (enemy.IsDead && !enemy.DeathLogged)
        {
            enemy.DeathLogged = true;
            if (!enemy.WasExecuted) AddLog("  " + enemy.Name + " 처치!");
            OnEnemyDied(enemy);
        }
        CheckEnrage(enemy);
    }

    // 적이 쓰러졌을 때 반응하는 아티팩트 (3-99). 누가 쓰러뜨렸는지는 상관없음 (협동)
    private void OnEnemyDied(EnemyCombatant enemy)
    {
        MonsterDeathEffects(enemy);   // 3-102
        if (enemy.SelfDestructed) return;   // 자폭은 처치가 아님
        foreach (PlayerCombatant p in Players)
        {
            if (p.IsDead) continue;
            bool noSoul = p.HasRelic(RelicLibrary.SoulbondBlade);   // 3-176 영혼 결속의 노리개 페널티: 처치로 영혼을 얻지 않음
            if (!noSoul && p.HasRelic(RelicLibrary.ChippedScythe)) RelicMana(p, GameBalance.ChippedScytheSoul, "이 빠진 낫날");
            if (!noSoul && p.HasRelic(RelicLibrary.HungryDoll)) RelicMana(p, GameBalance.HungryDollSoul, "굶주린 그림자 인형");   // 3-176
            if (enemy.Marks > 0 && p.HasRelic(RelicLibrary.Rangefinder)) RelicMana(p, GameBalance.RangefinderBullets, "고배율 거리측정기");
        }
    }

    // 피해를 받은 결과를 읽기 쉬운 글자로
    private string HitResultText(int damage, int blockAbsorbed, int hpLost)
    {
        if (damage <= 0) return "";
        if (blockAbsorbed >= damage) return " → 방어도로 전부 막음";
        if (blockAbsorbed > 0) return " → 방어도가 " + blockAbsorbed + " 막고 HP -" + hpLost;
        return " (HP -" + hpLost + ")";
    }

    // ==================== 상태 이상 · 스택 ====================

    // 취약·약화 같은 턴 수 상태: 이미 걸려 있으면 더 긴 쪽으로
    public void ApplyStatus(Combatant target, StatusType status, int turns)
    {
        if (target.IsDead || turns <= 0) return;
        int newTurns = System.Math.Max(target.GetStatus(status), turns);
        target.SetStatus(status, newTurns);
        AddLog("  " + target.Name + "에게 [" + Combatant.StatusName(status) + "] " + newTurns + "턴");
    }

    // 발화 최대치 = 건 사람의 체력 스탯 (15 / 20 / 30, 폭염의 코어 +20). 사람이 없으면 30
    public int BurnCapOf(PlayerCombatant applier)
    {
        return applier != null ? applier.BurnCap() : GameBalance.BurnCapMax;
    }

    // 발화: 스택을 더함 (지옥불의 심장이면 2배)
    public void ApplyBurn(PlayerCombatant applier, Combatant target, int stacks)
    {
        if (target.IsDead || stacks <= 0) return;
        if (applier != null && stacks > 0 && applier.HasRelic(RelicLibrary.HotBrazierShard)) stacks += GameBalance.HotBrazierBurn;   // 3-176 뜨거운 화로 조각
        if (applier != null && applier.HasRelic(RelicLibrary.HellfireHeart)) stacks *= GameBalance.HellfireBurnMult;
        int cap = BurnCapOf(applier);
        int before = target.GetStatus(StatusType.Burn);
        int after = System.Math.Min(before + stacks, System.Math.Max(cap, before));   // 이미 더 높으면 깎지는 않음
        target.SetStatus(StatusType.Burn, after);
        AddLog("  " + target.Name + "에게 [발화] +" + (after - before) + " (" + after + "/" + cap + ")");
    }

    // 발화 스택을 직접 정함 (불씨 폭발 2배 등)
    public void SetBurn(Combatant target, int stacks, int cap)
    {
        int value = System.Math.Min(stacks, cap);
        target.SetStatus(StatusType.Burn, value);
        AddLog("  " + target.Name + " [발화] " + value);
    }

    // 홍염 3의 배수 보너스: 한 번에 쓴 홍염 3/6/9 → 발화 1/2/3 (+ 용암 결정 5 피해)
    public void FlameMultipleBurn(PlayerCombatant user, EnemyCombatant target, int flameSpent)
    {
        int burn = flameSpent >= 9 ? 3 : (flameSpent >= 6 ? 2 : (flameSpent >= 3 ? 1 : 0));
        if (burn <= 0 || target == null || target.IsDead) return;
        AddLog("  홍염 " + flameSpent + "개 → 추가 발화 " + burn);
        ApplyBurn(user, target, burn);
        if (user.HasRelic(RelicLibrary.LavaCrystal) && !target.IsDead)
        {
            int lost = target.TakeAttackDamage(GameBalance.LavaCrystalDamage, false);
            AddLog("  " + user.Name + " 용암 결정: " + target.Name + " " + GameBalance.LavaCrystalDamage + " 피해 (HP -" + lost + ")");
            AfterEnemyHpLoss(target);
        }
    }

    // 처형 스택 쌓기 → 바로 처형 판정
    public void AddExecuteStacks(PlayerCombatant player, EnemyCombatant enemy, int amount)
    {
        if (enemy == null || enemy.IsDead || amount <= 0) return;
        enemy.ExecuteStacks += amount;
        enemy.LastHitBy = player;
        AddLog("  " + enemy.Name + " [처형 스택] +" + amount + " (" + enemy.ExecuteStacks + ")");
        AfterEnemyHpLoss(enemy);
    }

    // 처형 조건을 채웠으면 즉시 처형
    private void CheckExecute(EnemyCombatant enemy)
    {
        int bonus = PartyHas(RelicLibrary.AbyssalScythe) ? GameBalance.AbyssalExecuteBonus : 0;   // 심연의 사신 낫 (3-99)
        if (!enemy.ExecuteReady(bonus)) return;
        enemy.WasExecuted = true;
        AddLog("  ★ " + enemy.Name + " 처형! (처형 스택 " + enemy.ExecuteStacks + " / HP " + enemy.Hp + ")");
        enemy.LoseHp(enemy.Hp);
        foreach (PlayerCombatant p in Players)
        {
            if (p.IsDead || !p.HasRelic(RelicLibrary.Ledger)) continue;
            p.MaxHp += GameBalance.LedgerMaxHp;
            p.Hp += GameBalance.LedgerMaxHp;
            AddLog("  " + p.Name + " 피 묻은 처형인 명부: 최대 HP +" + GameBalance.LedgerMaxHp + " (" + p.MaxHp + ")");
        }
        foreach (PlayerCombatant p in Players)   // 3-176 수확자의 영혼등불: 처형되면 영혼 +2
            if (!p.IsDead && p.HasRelic(RelicLibrary.ReaperEdge)) RelicMana(p, GameBalance.ReaperEdgeSoul, "수확자의 영혼등불");
    }

    // 동결 스택 → 기준에 닿으면 동상
    public void AddFreeze(PlayerCombatant player, EnemyCombatant enemy, int amount)
    {
        if (enemy == null || enemy.IsDead || amount <= 0) return;
        // 보스 빙결 면역 (동상 직후). 영구동토석이 있으면 무시
        if (enemy.FreezeImmuneTurns > 0 && !(player != null && player.HasRelic(RelicLibrary.Permafrost)))
        {
            AddLog("  " + enemy.Name + " [빙결 면역] — 동결이 쌓이지 않음");
            return;
        }
        enemy.FreezeStacks += amount;
        AddLog("  " + enemy.Name + " [동결] +" + amount + " (" + enemy.FreezeStacks + "/" + enemy.FreezeThreshold + ")");
        // 3-176 서리 결정 반지: 동결을 쌓을 때마다 방어도 +1 (한 턴 최대 6)
        if (player != null && !player.IsDead && player.HasRelic(RelicLibrary.FrostRing) && player.FrostRingBlockThisTurn < GameBalance.FrostRingMaxPerTurn)
        {
            player.FrostRingBlockThisTurn += GameBalance.FrostRingBlock;
            player.GainBlock(GameBalance.FrostRingBlock);
        }

        // 혹한의 감옥: 동결을 걸 때마다 1장 뽑기 (한 턴 최대 3장)
        if (player != null && player.FrostPrisonStacks > 0 && player.FrostPrisonDrawsThisTurn < GameBalance.FrostPrisonMaxDraw)
        {
            player.FrostPrisonDrawsThisTurn++;
            Draw(player, 1);
        }
        if (enemy.FreezeStacks >= enemy.FreezeThreshold) TriggerFrostbite(enemy, player);
    }

    // 동상 발동: 이번 적 차례 행동 취소, 스택 0, 엘리트·보스는 기준 +1, 보스는 빙결 면역, 우박 피해, 서리 맺힌 나침반
    public void TriggerFrostbite(EnemyCombatant enemy, PlayerCombatant source = null)
    {
        if (enemy.IsDead) return;
        enemy.FreezeStacks = 0;
        enemy.SetStatus(StatusType.Frostbite, 1);
        enemy.FreezeThreshold += GameBalance.FreezeNeedGrowth;   // 모든 몬스터: 터질 때마다 기준 +1
        if (enemy.IsBoss) enemy.FreezeImmuneTurns = GameBalance.BossFreezeImmuneEnemyTurns;
        AddLog("  ※ " + enemy.Name + " [동상]! 이번 행동 취소 (다음 기준 " + enemy.FreezeThreshold + ")"
               + (enemy.IsBoss ? " [빙결 면역]" : ""));
        foreach (PlayerCombatant p in Players)
        {
            if (p.IsDead) continue;
            if (p.HasRelic(RelicLibrary.FrostCompass))
            {
                p.GainMana(GameBalance.FrostCompassMana);
                AddLog("  " + p.Name + " 서리 맺힌 나침반: 마나 +" + GameBalance.FrostCompassMana);
            }
            // 3-176 만년설의 심장 페널티 / 빙결 심연의 보주
            if (p.HasRelic(RelicLibrary.GlacialHeart) && p.Mana > 0) { p.Mana = System.Math.Max(0, p.Mana - GameBalance.GlacialHeartFrostbiteLoss); AddLog("  " + p.Name + " 만년설의 심장: 마나 -" + GameBalance.GlacialHeartFrostbiteLoss); }
            if (p.HasRelic(RelicLibrary.AbyssOrb))
            {
                AddLog("  " + p.Name + " 빙결 심연의 보주: 방어도 +" + GameBalance.AbyssOrbBlock);
                p.GainBlock(GameBalance.AbyssOrbBlock);
                foreach (EnemyCombatant oe in LivingEnemies()) AddShock(p, oe, GameBalance.AbyssOrbShock);
            }
            if (p.HailDamage <= 0 || enemy.IsDead) continue;
            int dmg = p.HailDamage;
            int lost = enemy.TakeAttackDamage(dmg, false);
            AddLog("  " + p.Name + " 우박: " + enemy.Name + " " + dmg + " 피해 (HP -" + lost + ")");
            AfterEnemyHpLoss(enemy);
        }
    }

    // 감전 스택 → 4개면 감전 상태 2턴. 감전 중에는 안 쌓임
    public void AddShock(PlayerCombatant player, EnemyCombatant enemy, int amount)
    {
        if (enemy == null || enemy.IsDead || amount <= 0) return;
        if (enemy.HasStatus(StatusType.Shock)) return;
        enemy.ShockStacks += amount;
        AddLog("  " + enemy.Name + " [감전 스택] +" + amount + " (" + System.Math.Min(enemy.ShockStacks, GameBalance.ShockStacksNeeded) + "/" + GameBalance.ShockStacksNeeded + ")");
        if (player != null && player.StaticFieldBlock > 0)
        {
            int block = player.StaticFieldBlock;
            player.GainBlock(block);
            AddLog("  " + player.Name + " 정전기 역장: 방어도 +" + block);
        }
        if (enemy.ShockStacks >= GameBalance.ShockStacksNeeded)
        {
            enemy.ShockStacks = 0;
            enemy.SetStatus(StatusType.Shock, GameBalance.ShockTurns);
            AddLog("  ※ " + enemy.Name + " [감전]! " + GameBalance.ShockTurns + "턴 동안 주는 피해 -" + (int)(GameBalance.ShockReduction * 100) + "%");
        }
    }

    // 플레이어가 스스로 HP를 잃음 (어둠의 계약, 불꽃 재생). 내 턴이면 태양의 진노 발동
    public void PlayerLoseHp(PlayerCombatant p, int amount)
    {
        int lost = p.LoseHp(amount);
        AddLog("  " + p.Name + " HP -" + lost);
        CheckPhoenixHeart(p);
        if (p.IsDead) AddLog("  " + p.Name + " 쓰러짐!");
        if (lost > 0) SunWrath(p);
        if (lost > 0 && !p.IsDead && p.HasRelic(RelicLibrary.UndyingAsh))
        {
            p.GainBlock(GameBalance.UndyingAshBlock);
            AddLog("  " + p.Name + " 꺼지지 않는 잿더미: 방어도 +" + GameBalance.UndyingAshBlock);
        }
        if (lost > 0 && !p.IsDead && p.HasRelic(RelicLibrary.PhoenixTear)) RelicMana(p, GameBalance.PhoenixTearFlame, "불사조의 눈물");   // 3-99
    }

    // 태양의 진노: 내 턴에 HP를 잃거나 홍염을 쓸 때마다 모든 적 발화 2
    private void SunWrath(PlayerCombatant p)
    {
        if (p.SunWrathBurn <= 0 || InEnemyPhase || p.IsDead) return;
        AddLog("  " + p.Name + " 태양의 진노");
        foreach (EnemyCombatant e in LivingEnemies()) ApplyBurn(p, e, p.SunWrathBurn);
    }

    // 피닉스의 심장: 쓰러질 피해를 받으면 HP 1로 버티고 최대 HP 20% 회복, 홍염 7까지
    private void CheckPhoenixHeart(PlayerCombatant p)
    {
        if (!p.IsDead || !p.PhoenixHeartReady || p.PhoenixHeartUsed) return;
        p.PhoenixHeartUsed = true;
        p.PhoenixHeartReady = false;
        p.Hp = 1;
        float ratio = p.PhoenixHeartHeal > 0f ? p.PhoenixHeartHeal : GameBalance.PhoenixHeartHeal;
        int healed = p.Heal(DamageCalculator.RoundHalfUp(p.MaxHp * ratio));
        if (p.Mana < GameBalance.PhoenixHeartFlame) p.Mana = System.Math.Min(GameBalance.PhoenixHeartFlame, p.MaxMana);
        AddLog("  ★ " + p.Name + " 피닉스의 심장! HP 1로 버티고 +" + healed + ", 홍염 " + p.Mana);
    }

    // ==================== 카드 뽑기 ====================
    public void Draw(PlayerCombatant player, int count)
    {
        for (int i = 0; i < count; i++)
        {
            int index = FindDrawableIndex(player.DrawPile);

            // 뽑을 수 있는 카드가 없으면 버린 카드 더미를 섞어서 합침
            if (index < 0)
            {
                if (player.DiscardPile.Count == 0) return;
                player.DrawPile.AddRange(player.DiscardPile);
                player.DiscardPile.Clear();
                Shuffle(player.DrawPile);
                AddLog(player.Name + ": 버린 카드 더미를 섞어 뽑을 카드 더미로");
                index = FindDrawableIndex(player.DrawPile);
                if (index < 0) return;
            }

            CardData card = player.DrawPile[index];
            player.DrawPile.RemoveAt(index);
            if (player.Hand.Count >= GameBalance.MaxHandSize) player.DiscardPile.Add(card);   // 손패가 꽉 차면 바로 버림
            else player.Hand.Add(card);

            // 3-149 [공허]: 뽑으면 에너지 -N (0 아래로는 안 내려감). 손패가 꽉 차서 바로 버려져도 뽑은 것이므로 똑같이 깎임
            if (card.EnergyLossOnDraw > 0)
            {
                int lost = System.Math.Min(player.Energy, card.EnergyLossOnDraw);
                player.Energy -= lost;
                AddLog("  " + player.Name + " [" + card.Name + "] 뽑음: 에너지 -" + lost);
            }

            // 절대 영도의 파편 페널티: 카드 효과로 뽑을 때마다 HP -1 (턴 시작 뽑기는 제외)
            if (!drawingTurnStart && player.HasRelic(RelicLibrary.ZeroShard) && !player.IsDead)
            {
                player.LoseHp(GameBalance.ZeroShardHpPerDraw);
                AddLog("  " + player.Name + " 절대 영도의 파편: HP -" + GameBalance.ZeroShardHpPerDraw);
                if (player.IsDead) { AddLog("  " + player.Name + " 쓰러짐!"); return; }
            }
        }
    }

    private int FindDrawableIndex(List<CardData> pile)
    {
        for (int i = pile.Count - 1; i >= 0; i--)
        {
            if (pile[i].MinRound <= Round) return i;
        }
        return -1;
    }

    // 피셔-예이츠 셔플: 목록을 골고루 섞는 표준 방법
    private void Shuffle(List<CardData> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            CardData temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }

    // ==================== 턴 종료 ====================
    public void EndTurn(PlayerCombatant player)
    {
        if (!CanAct(player)) return;

        player.PendingSealPick = false;   // 봉인할 카드를 안 고르고 끝내면 봉인 없음
        player.PendingDiscardPick = 0;    // 버릴 카드를 안 고르고 끝내면 탄환도 없음
        // 베테랑의 예비 탄창: 남은 탄환 2발당 다음 턴 에너지 +1 (최대 2)
        if (player.Class == PlayerClass.Sniper && player.HasRelic(RelicLibrary.VeteranMag))
        {
            int en = System.Math.Min(GameBalance.VeteranMagMaxEnergy, player.Mana / GameBalance.VeteranMagBulletsPerEnergy);
            if (en > 0) { player.GuardStanceEnergy += en; AddLog(player.Name + " 베테랑의 예비 탄창: 다음 턴 에너지 +" + en); }
        }
        // 작열하는 부싯돌 (3-99): 턴 종료 때 홍염 3 이상이면 방어도
        if (player.Class == PlayerClass.CosmicPhoenix && player.Mana >= GameBalance.ScorchingFlintNeed && player.HasRelic(RelicLibrary.ScorchingFlint))
        {
            player.GainBlock(GameBalance.ScorchingFlintBlock);
            AddLog(player.Name + " 작열하는 부싯돌: 방어도 +" + GameBalance.ScorchingFlintBlock);
        }
        // ---- 3-176 턴 종료 아티팩트 (손패를 버리기 전) ----
        if (player.HasRelic(RelicLibrary.SuperheatedStone))   // 고열의 마도석: 손에 남은 카드 1장당 무작위 적 2 피해
        {
            int n = System.Math.Min(player.Hand.Count, GameBalance.SuperheatedMaxCards);
            for (int k = 0; k < n; k++)
            {
                EnemyCombatant he = RandomLivingEnemy();
                if (he == null) break;
                int lostH = he.TakeAttackDamage(GameBalance.SuperheatedDamage, false);
                he.LastHitBy = player;
                AddLog("  고열의 마도석: " + he.Name + " " + GameBalance.SuperheatedDamage + " 피해 (HP -" + lostH + ")");
                AfterEnemyHpLoss(he);
            }
        }
        if (player.HasRelic(RelicLibrary.CommanderFlag) && player.Energy > 0)   // 작전 지휘관의 깃발
        {
            player.FlagDrawNext = System.Math.Min(GameBalance.FlagMaxDraw, player.Energy);
            AddLog(player.Name + " 작전 지휘관의 깃발: 다음 턴 카드 +" + player.FlagDrawNext);
        }
        if (player.Class == PlayerClass.CosmicPhoenix && player.Mana > 0 && player.HasRelic(RelicLibrary.AshCrown))   // 불사조의 잿불 왕관
        {
            int ab = System.Math.Min(GameBalance.AshCrownMaxBlock, player.Mana * GameBalance.AshCrownBlockPerFlame);
            player.GainBlock(ab);
            AddLog(player.Name + " 불사조의 잿불 왕관: 방어도 +" + ab);
        }
        if (PartyHas(RelicLibrary.BondTorch)) { player.GainBlock(GameBalance.TorchBlock); AddLog(player.Name + " 결속의 횃불: 방어도 +" + GameBalance.TorchBlock); }
        if (player.HasRelic(RelicLibrary.OverheatedStone))   // 과열된 마력석 페널티 (HP 1 아래로는 안 내려감)
        {
            int l = System.Math.Min(GameBalance.OverheatedHpLoss, player.Hp - 1);
            if (l > 0) { player.LoseHp(l); AddLog(player.Name + " 과열된 마력석: HP -" + l); }
        }
        EndTurnMonsterCards(player);   // 3-102: [화상] 카드 피해, 결속 카드 → 마녀 강화
        // 레드 위스퍼 페널티로 비싸진 카드는 턴이 끝나면 원래대로
        foreach (CardData hc in player.Hand) if (hc.TempDiscount < 0) hc.TempDiscount = 0;
        // 3-149 휘발성 ([공허]): 손에 남은 카드는 버린 카드 더미 대신 소멸 더미로
        //   RemoveAll(조건) = 자바의 removeIf. 먼저 소멸 더미에 넣고 손패에서 뺌
        foreach (CardData hc in player.Hand) if (hc.Ethereal) { player.ExhaustPile.Add(hc); AddLog("  " + player.Name + " [" + hc.Name + "] 휘발 (소멸)"); }
        player.Hand.RemoveAll(hc => hc.Ethereal);
        player.DiscardPile.AddRange(player.Hand);
        player.Hand.Clear();
        player.HasEndedTurn = true;
        AddLog(player.Name + " 턴 종료");

        // 강철 의지 (공용 파워): 턴 종료 시 방어도
        if (player.SteelWill > 0)
        {
            player.GainBlock(player.SteelWill);
            AddLog("  강철 의지: 방어도 +" + player.SteelWill);
        }

        // 뇌전 지대: 내 턴 종료마다 장마다 무작위 적 4(강화 6) 피해 + 마나 2
        foreach (int stormDmg in player.StormFieldHits)
        {
            EnemyCombatant e = RandomLivingEnemy();
            if (e == null) break;
            bool before = lightningContext;
            lightningContext = true;
            PlayerAttack(player, e, stormDmg, DamageKind.AP, 0f, false, false, " [뇌전 지대]");
            lightningContext = before;
            player.GainMana(GameBalance.StormFieldMana);
        }

        // 꺼지지 않는 불꽃: 발화 걸린 적 1마리당 홍염 +1 (최대 2)
        if (player.UndyingFlame)
        {
            int burning = 0;
            foreach (EnemyCombatant e in LivingEnemies()) if (e.HasStatus(StatusType.Burn)) burning++;
            int gain = System.Math.Min(burning, player.UndyingFlameMax);
            if (gain > 0) { player.GainMana(gain); AddLog("  " + player.Name + " 꺼지지 않는 불꽃: 홍염 +" + gain); }
        }

        // 금지된 사령술의 해골 페널티: 내 턴 종료마다 HP -3
        if (player.HasRelic(RelicLibrary.NecroSkull))
        {
            int lost = player.LoseHp(GameBalance.SkullHpLoss);
            AddLog("  " + player.Name + " 금지된 사령술의 해골: HP -" + lost);
            CheckPhoenixHeart(player);
            if (player.IsDead) AddLog("  " + player.Name + " 쓰러짐!");
        }

        CheckResult();
        TrySpawnNextWave();
        if (Result != BattleResult.Ongoing) return;

        if (FindNextPlayer(0) == -1) EnemyPhase();   // 살아 있는 전원이 턴 종료했으면 적 차례
    }

    // ==================== 적 차례 ====================
    private void EnemyPhase()
    {
        InEnemyPhase = true;
        FlushSplits();   // 3-102
        AddLog("--- 적의 차례 ---");

        // 플레이어 쪽 턴이 끝났으므로 플레이어에게 걸린 취약·약화 1 감소
        foreach (PlayerCombatant p in Players)
        {
            DecreaseDuration(p, StatusType.Vulnerable);
            DecreaseDuration(p, StatusType.Weak);
            DecreaseDuration(p, StatusType.Frail);
        }
        // 사신의 수의 [표식]은 내 턴 동안만
        foreach (EnemyCombatant e in Enemies) e.ShroudBy = null;

        // 목록을 복사해서 돌림 (중간에 웨이브가 바뀌어도 안전하게)
        List<EnemyCombatant> acting = new List<EnemyCombatant>(Enemies);
        List<EnemyCombatant> stunUsed = new List<EnemyCombatant>();   // 이번 차례에 기절로 쉰 적 (3-102)
        foreach (EnemyCombatant enemy in acting)
        {
            if (enemy.IsDead) continue;

            // 적도 자기 턴 시작에 방어도 0. 뼈조각 수집가는 남은 방어도로 공격하므로 그대로 둠 (3-102)
            if (enemy.Tag != EnemyLibrary.T_BoneCollector) enemy.Block = 0;
            if (enemy.PendingBlock > 0)   // 투구 딱정벌레 (3-102)
            {
                enemy.GainBlock(enemy.PendingBlock);
                AddLog(enemy.Name + " 단단해진 껍질: 방어도 +" + enemy.PendingBlock);
                enemy.PendingBlock = 0;
            }
            TickBurn(enemy);
            if (enemy.IsDead) continue;

            if (enemy.HasStatus(StatusType.Frostbite))
            {
                AddLog(enemy.Name + " [" + enemy.Intent.Name + "] → 동상으로 행동하지 못함!");
            }
            else if (enemy.Stunned)   // 3-102 기절: 이번 행동을 못 하고, 다음 차례에 같은 행동
            {
                AddLog(enemy.Name + " [" + enemy.Intent.Name + "] → 기절해서 행동하지 못함!");
                stunUsed.Add(enemy);
            }
            else
            {
                DoEnemyMove(enemy);
            }

            CheckResult();
            if (Result != BattleResult.Ongoing) return;
        }

        // 적 차례 끝: 혹한의 감옥(동상 상태 적 피해), 영혼 착취
        EndOfEnemyTurnPowers();

        foreach (EnemyCombatant enemy in Enemies)
        {
            DecreaseDuration(enemy, StatusType.Vulnerable);
            DecreaseDuration(enemy, StatusType.Weak);
            DecreaseDuration(enemy, StatusType.Shock);
            enemy.SetStatus(StatusType.Frostbite, 0);
            if (enemy.Ritual > 0 && !enemy.IsDead)
            {
                enemy.Strength += enemy.Ritual;
                AddLog("  " + enemy.Name + " 의식: 힘 +" + enemy.Ritual + " (" + enemy.Strength + ")");
            }
            if (enemy.BrandTurns > 0) enemy.BrandTurns--;
            if (enemy.FreezeImmuneTurns > 0) enemy.FreezeImmuneTurns--;
            enemy.BlockLocked = false;
            enemy.HalfDamage = false;
            // 기절로 쉰 적은 다음 차례에 같은 행동 (3-102). 그 외에는 다음 행동으로
            if (stunUsed.Contains(enemy)) enemy.Stunned = false;
            else enemy.AdvanceIntent();
        }
        TickRevivals();   // 3-102: 해골 근위병·쌍둥이 석상 부활
        EnemyAttackReduction = 0f;

        CheckResult();
        TrySpawnNextWave();
        if (Result == BattleResult.Ongoing) StartRound();
    }

    private void EndOfEnemyTurnPowers()
    {
        foreach (PlayerCombatant p in Players)
        {
            if (p.IsDead) continue;
            if (p.FrostPrisonStacks > 0)
            {
                foreach (EnemyCombatant e in LivingEnemies())
                {
                    if (!e.HasStatus(StatusType.Frostbite)) continue;
                    int lost = e.LoseHp(GameBalance.FrostPrisonDamage * p.FrostPrisonStacks);
                    AddLog("  " + p.Name + " 혹한의 감옥: " + e.Name + " " + lost + " 피해");
                    AfterEnemyHpLoss(e);
                }
            }
            if (p.SoulExploitStacks > 0)
            {
                int soul = 0;
                foreach (EnemyCombatant e in LivingEnemies())
                {
                    int per = p.SoulExploitPer > 0 ? p.SoulExploitPer : GameBalance.SoulExploitPer;
                    if (e.ExecuteStacks < per) continue;
                    int dmg = (e.ExecuteStacks / per) * GameBalance.SoulExploitDamage * p.SoulExploitStacks;
                    int lost = e.LoseHp(dmg);
                    AddLog("  " + p.Name + " 영혼 착취: " + e.Name + " " + lost + " 피해");
                    AfterEnemyHpLoss(e);
                    soul++;
                }
                soul = System.Math.Min(soul, GameBalance.SoulExploitMaxSoul);
                if (soul > 0) { p.GainMana(soul); AddLog("  " + p.Name + " 영혼 +" + soul); }
            }
        }
    }

    private void DoEnemyMove(EnemyCombatant enemy)
    {
        EnemyMove move = enemy.Intent;
        AddLog(enemy.Name + " [" + move.Name + "]");

        if (move.Block > 0)
        {
            int block = enemy.ScaledBlock(move);
            if (enemy.BlockLocked) AddLog("  " + enemy.Name + " 방어도 " + block + " → 만년설로 0");
            else { enemy.GainBlock(block); AddLog("  " + enemy.Name + " 방어도 +" + block); }
        }

        // 동료에게 방어도 (감시자 무리). 살아 있는 동료가 없으면 자기에게
        if (move.AllyBlock > 0)
        {
            int block = DamageCalculator.RoundHalfUp(move.AllyBlock * enemy.BlockMultiplier);
            bool gave = false;
            foreach (EnemyCombatant ally in Enemies)
            {
                if (ally == enemy || ally.IsDead || ally.BlockLocked) continue;
                ally.GainBlock(block);
                AddLog("  " + ally.Name + " 방어도 +" + block + " (" + enemy.Name + ")");
                gave = true;
            }
            if (!gave && !enemy.BlockLocked) { enemy.GainBlock(block); AddLog("  " + enemy.Name + " 방어도 +" + block); }
        }

        if (move.Heal > 0)
        {
            int healed = enemy.Heal(DamageCalculator.RoundHalfUp(move.Heal * GameBalance.StageHpScale[Clamp(Stage - 1, 0, 3)]));
            AddLog("  " + enemy.Name + " HP +" + healed);
        }

        if (move.Damage > 0)
        {
            // 적은 살아 있는 플레이어 "전원"을 공격합니다 (슬더스2 방식). 각자 자기 방어도로 따로 막습니다.
            for (int i = 0; i < move.Hits; i++)
            {
                foreach (PlayerCombatant target in Players)
                {
                    if (target.IsDead || enemy.IsDead) continue;
                    HitPlayer(enemy, move, target);
                }
            }
        }

        // 뼈조각 수집가 (3-102): 남은 방어도를 전부 던짐
        if (move.DamageFromBlock && enemy.Block > 0) { AddLog("  " + enemy.Name + " 방어도 " + enemy.Block + " → 0"); enemy.Block = 0; }
        // 동료 힘 (3-102 고블린 대장·사냥개 조련사). 살아 있는 동료가 없으면 자기에게
        if (move.AllyStrength > 0)
        {
            bool gaveS = false;
            foreach (EnemyCombatant ally in Enemies)
            {
                if (ally == enemy || ally.IsDead) continue;
                ally.Strength += move.AllyStrength;
                AddLog("  " + ally.Name + " 힘 +" + move.AllyStrength + " (" + ally.Strength + ")");
                gaveS = true;
            }
            if (!gaveS) { enemy.Strength += move.AllyStrength; AddLog("  " + enemy.Name + " 힘 +" + move.AllyStrength); }
        }
        // 힘은 공격 뒤에 오름 (이번 공격에는 안 붙음)
        if (move.Strength > 0)
        {
            enemy.Strength += move.Strength;
            AddLog("  " + enemy.Name + " 힘 +" + move.Strength + " (" + enemy.Strength + ")");
        }
        // 가시·의식 (2026-11-15): 한 번 얻으면 전투 끝까지
        if (move.GainThorns > 0)
        {
            enemy.Thorns += move.GainThorns;
            AddLog("  " + enemy.Name + " 가시 +" + move.GainThorns + " (때리면 " + enemy.Thorns + " 피해를 돌려받음)");
        }
        if (move.GainRitual > 0)
        {
            enemy.Ritual += move.GainRitual;
            AddLog("  " + enemy.Name + " 의식 +" + move.GainRitual + " (적 차례가 끝날 때마다 힘 +" + enemy.Ritual + ")");
        }

        foreach (PlayerCombatant target in Players)
        {
            if (target.IsDead) continue;
            if (move.PlayerVulnerable > 0) ApplyStatus(target, StatusType.Vulnerable, move.PlayerVulnerable);
            if (move.PlayerWeak > 0) ApplyStatus(target, StatusType.Weak, move.PlayerWeak);
            if (move.PlayerFrail > 0) ApplyStatus(target, StatusType.Frail, move.PlayerFrail);
            if (move.PlayerBurn > 0)
            {
                // 몬스터가 거는 발화는 그냥 쌓임 (플레이어 쪽 최대치 규칙 없음)
                target.SetStatus(StatusType.Burn, target.GetStatus(StatusType.Burn) + move.PlayerBurn);
                AddLog("  " + target.Name + "에게 [발화] " + move.PlayerBurn + " (현재 " + target.GetStatus(StatusType.Burn) + ")");
            }
            if (move.AddWounds > 0)
            {
                for (int k = 0; k < move.AddWounds; k++) target.DiscardPile.Add(CardLibrary.Wound());
                AddLog("  " + target.Name + "의 버린 카드 더미에 [상처] " + move.AddWounds + "장");
            }
            if (move.PlayerDrawDown > 0)
            {
                target.DrawPenalty += move.PlayerDrawDown;
                AddLog("  " + target.Name + " 다음 턴 카드 -" + target.DrawPenalty);
            }
            if (move.AddBurnCards > 0)   // 3-102 [화상] 카드
            {
                for (int k = 0; k < move.AddBurnCards; k++) target.DiscardPile.Add(CardLibrary.BurnCard());
                AddLog("  " + target.Name + "의 버린 카드 더미에 [화상] " + move.AddBurnCards + "장");
            }
            if (move.AddSlimeCards > 0)   // 3-149 [점액] 카드
            {
                for (int k = 0; k < move.AddSlimeCards; k++) target.DiscardPile.Add(CardLibrary.SlimeCard());
                AddLog("  " + target.Name + "의 버린 카드 더미에 [점액] " + move.AddSlimeCards + "장");
            }
            if (move.AddVoidCards > 0)    // 3-149 [공허] 카드
            {
                for (int k = 0; k < move.AddVoidCards; k++) target.DiscardPile.Add(CardLibrary.VoidCard());
                AddLog("  " + target.Name + "의 버린 카드 더미에 [공허] " + move.AddVoidCards + "장");
            }
        }

        // 자폭 (3-102 불씨 정령): 행동이 끝나면 스스로 쓰러짐 (아티팩트 처치 보너스 없음)
        if (move.SelfDestruct && !enemy.IsDead)
        {
            enemy.SelfDestructed = true;
            enemy.LoseHp(enemy.Hp);
            AddLog("  " + enemy.Name + " 자폭!");
            AfterEnemyHpLoss(enemy);
        }
    }

    // 적의 공격 1회가 플레이어 1명에게
    private void HitPlayer(EnemyCombatant enemy, EnemyMove move, PlayerCombatant target)
    {
        float factor = 1f;
        List<string> notes = new List<string>();
        if (enemy.HalfDamage) { factor *= 1f - GameBalance.GlacierCutBossReduction; notes.Add("빙하절단"); }
        if (EnemyAttackReduction > 0f) { factor *= 1f - EnemyAttackReduction; notes.Add("어둠의 지배"); }
        if (target.FlameWall) { if (!target.FlameWallNoPenalty) { factor *= GameBalance.FlameWallSelfMultiplier; notes.Add("화염 장벽 x" + GameBalance.FlameWallSelfMultiplier); } }
        else if (AnyAllyFlameWall(target)) { factor *= 1f - GameBalance.FlameWallReduction; notes.Add("화염 장벽"); }
        if (target.GlacierBarrier > 0f) { factor *= 1f - target.GlacierBarrier; notes.Add("빙하 결계"); }
        if (target.PhoenixGuardStacks > 0 && enemy.HasStatus(StatusType.Burn)) { factor *= 1f - target.PhoenixGuardReduction; notes.Add("가호"); }
        if (target.SmokeReduce > 0f) { factor *= 1f - target.SmokeReduce; notes.Add("연막"); }

        int damage = DamageCalculator.EnemyAttack(enemy, move, target, factor);
        if (target.DevInvincible) { damage = 0; notes.Add("무적"); }   // 개발 도구
        int blockBefore = target.Block;
        int hpLost = target.TakeAttackDamage(damage, false);
        AddLog("  " + target.Name + "에게 " + damage + " 피해" + (notes.Count > 0 ? " (" + string.Join(", ", notes) + ")" : "")
               + HitResultText(damage, blockBefore - target.Block, hpLost));

        CheckPhoenixHeart(target);
        if (target.IsDead) AddLog("  " + target.Name + " 쓰러짐!");

        // 맞으면 반응하는 효과
        if (target.ThornArmor > 0 && !enemy.IsDead)
        {
            int lostE = enemy.TakeAttackDamage(target.ThornArmor, false);
            enemy.LastHitBy = target;
            AddLog("  가시 갑옷: " + enemy.Name + "에게 " + target.ThornArmor + " 피해 (HP -" + lostE + ")");
            AfterEnemyHpLoss(enemy);
        }
        if (target.ReflectBurn) ApplyBurn(target, enemy, GameBalance.PShieldBurn);
        if (target.ShockBarrier) AddShock(target, enemy, 1);
        if (target.BurningThornsStacks > 0 && (target.ThornsUnlimited || target.ThornsThisTurn < GameBalance.ThornsMaxPerTurn))
        {
            target.ThornsThisTurn++;
            ApplyBurn(target, enemy, GameBalance.ThornsBurn * target.BurningThornsStacks);
            target.GainMana(GameBalance.ThornsFlame);
        }
    }

    private bool AnyAllyFlameWall(PlayerCombatant target)
    {
        foreach (PlayerCombatant p in Players)
        {
            if (p != target && !p.IsDead && p.FlameWall) return true;
        }
        return false;
    }

    // 발화: 턴 시작에 스택만큼 피해(방어도 무시) → 1 감소. 낙인이면 2배, 꺼지지 않는 불꽃이면 적 발화는 안 줄어듦
    private void TickBurn(Combatant unit)
    {
        int stacks = unit.GetStatus(StatusType.Burn);
        if (stacks <= 0) return;

        EnemyCombatant enemy = unit as EnemyCombatant;
        int dmg = stacks;
        if (enemy != null && enemy.BrandTurns > 0) dmg *= 2;
        bool brazierBurn = enemy != null && PartyHas(RelicLibrary.EternalBrazier);   // 영겁의 화로 (3-99)
        if (brazierBurn) dmg = DamageCalculator.RoundHalfUp(dmg * GameBalance.BrazierBurnMult);
        int lost = unit.LoseHp(dmg);

        bool keep = enemy != null && AnyUndyingFlame();
        if (!keep) unit.SetStatus(StatusType.Burn, stacks - 1);
        AddLog("  " + unit.Name + " 발화로 " + lost + " 피해" + (enemy != null && enemy.BrandTurns > 0 ? " [낙인 x2]" : "") + (brazierBurn ? " [화로 x1.5]" : "")
               + " (발화 " + unit.GetStatus(StatusType.Burn) + ")");

        if (enemy != null) AfterEnemyHpLoss(enemy);
        else if (unit.IsDead) AddLog("  " + unit.Name + " 쓰러짐!");
    }

    private bool AnyUndyingFlame()
    {
        foreach (PlayerCombatant p in Players) if (!p.IsDead && (p.UndyingFlame || p.HasRelic(RelicLibrary.EmberCore))) return true;   // 3-176 꺼지지 않는 불씨핵
        return false;
    }

    private void DecreaseDuration(Combatant unit, StatusType status)
    {
        int turns = unit.GetStatus(status);
        if (turns > 0) unit.SetStatus(status, turns - 1);
    }

    // 보스 강화: HP가 기준 이하로 처음 내려가면 패턴을 2페이즈로 바꾸고 방어도를 얻음
    private void CheckEnrage(EnemyCombatant enemy)
    {
        if (enemy.IsDead || enemy.IsEnraged || enemy.Phase2Pattern == null) return;
        if ((float)enemy.Hp / enemy.MaxHp > enemy.EnrageHpRatio) return;

        enemy.IsEnraged = true;
        enemy.Pattern = enemy.Phase2Pattern;
        enemy.MoveIndex = 0;
        int block = DamageCalculator.RoundHalfUp(enemy.EnrageBlock * enemy.BlockMultiplier);
        enemy.GainBlock(block);
        AddLog("  ★ " + enemy.Name + " 강화! 패턴 변경, 방어도 +" + block);
    }

    // ======================================================================
    // 3-102 새 몬스터 규칙 (몬스터 통합 도감). EnemyCombatant.Tag 로 구분
    // ======================================================================

    // 전투·웨이브 시작: 외골격 크기, 부활 HP 비율, 유충 수
    private void InitSpecialEnemies(List<EnemyCombatant> list)
    {
        foreach (EnemyCombatant e in list)
        {
            if (e.Tag == EnemyLibrary.T_Shell) { e.ShellMax = DamageCalculator.RoundHalfUp(e.MaxHp * GameBalance.ShellArmorRatio); e.ShellHp = e.ShellMax; }
            if (e.Tag == EnemyLibrary.T_Twin) e.ReviveHpRatio = GameBalance.TwinReviveHpRatio;
        }
        RefreshSwarm();
    }

    // 같은 무리(GroupId)에서 Tag 가 같은 살아 있는 동료 찾기
    private EnemyCombatant FindGroupMate(EnemyCombatant self, string tag, bool alive = true)
    {
        foreach (EnemyCombatant e in Enemies)
            if (e != self && e.GroupId == self.GroupId && e.Tag == tag && e.IsDead != alive) return e;
        return null;
    }

    // 점액 유충: 살아 있는 유충 수만큼 공격력 +
    private void RefreshSwarm()
    {
        foreach (EnemyCombatant e in Enemies)
        {
            if (e.Tag != EnemyLibrary.T_Larva) continue;
            int n = 0;
            foreach (EnemyCombatant o in Enemies) if (o.Tag == EnemyLibrary.T_Larva && o.GroupId == e.GroupId && !o.IsDead) n++;
            e.SwarmBonus = n;
        }
    }

    // 플레이어 공격이 이 적에게 들어갈 때 배율 (은신·외골격)
    private float EnemyTakenFactor(EnemyCombatant target, ref string text)
    {
        float f = 1f;
        if (target.Tag == EnemyLibrary.T_SkelArcher && FindGroupMate(target, EnemyLibrary.T_SkelShield) != null)
        {
            f *= 1f - GameBalance.SkelStealthReduction;
            text += " [은신 -" + (int)System.Math.Round(GameBalance.SkelStealthReduction * 100) + "%]";
        }
        if (target.ShellHp > 0)
        {
            f *= 1f - GameBalance.ShellReduction;
            text += " [외골격 -" + (int)System.Math.Round(GameBalance.ShellReduction * 100) + "%]";
        }
        return f;
    }

    // 플레이어 공격을 맞은 뒤: 딱정벌레 껍질, 석상 과부하, 외골격 깎기
    private void OnEnemyStruck(EnemyCombatant e, int hpLost)
    {
        if (e.IsDead) return;
        if (e.Tag == EnemyLibrary.T_Beetle && e.HitsThisRound == 0)
        {
            e.PendingBlock += DamageCalculator.RoundHalfUp(GameBalance.BeetleShellBlock * e.BlockMultiplier);
            AddLog("  " + e.Name + " 껍질이 단단해짐: 다음 적 차례에 방어도 +" + e.PendingBlock);
        }
        e.HitsThisRound++;
        if (e.Tag == EnemyLibrary.T_Sentinel && !InEnemyPhase && !e.OverloadUsed && e.HitsThisRound >= GameBalance.SentinelOverloadHits)
        {
            e.OverloadUsed = true;
            e.Stunned = true;
            AddLog("  ※ " + e.Name + " 과부하! (" + GameBalance.SentinelOverloadHits + "번 맞음) 다음 행동 취소");
            ApplyStatus(e, StatusType.Vulnerable, GameBalance.SentinelOverloadVuln);
        }
        if (e.ShellHp > 0 && hpLost > 0)
        {
            e.ShellHp = System.Math.Max(0, e.ShellHp - hpLost);
            if (e.ShellHp == 0)
            {
                AddLog("  ★ " + e.Name + " 외골격 파괴! 기절 → 깨어나면 광폭화");
                e.Stunned = true;
                if (e.Phase2Pattern != null && !e.IsEnraged)
                {
                    e.IsEnraged = true;
                    e.Pattern = e.Phase2Pattern;
                    e.MoveIndex = 0;
                }
            }
        }
    }

    // 몬스터가 쓰러졌을 때의 특수 효과
    private void MonsterDeathEffects(EnemyCombatant dead)
    {
        if (dead.Tag == EnemyLibrary.T_Spore)
        {
            AddLog("  ※ " + dead.Name + " 터지며 포자를 퍼뜨림!");
            foreach (PlayerCombatant p in Players)
            {
                if (p.IsDead) continue;
                ApplyStatus(p, StatusType.Weak, GameBalance.SporeDeathWeak);
                p.DrawPile.Add(CardLibrary.SporeCard());   // 맨 뒤 = 맨 위 (Draw 는 뒤에서부터 꺼냄)
                AddLog("  " + p.Name + " 뽑을 카드 더미 맨 위에 [포자]");
            }
        }
        if (dead.Tag == EnemyLibrary.T_Handler)
        {
            EnemyCombatant beast = FindGroupMate(dead, EnemyLibrary.T_Beast);
            if (beast != null)
            {
                beast.Strength += GameBalance.BeastFrenzyStrength;
                beast.IsEnraged = true;
                AddLog("  ★ " + beast.Name + " 광폭! 주인을 잃고 날뜀 (힘 +" + GameBalance.BeastFrenzyStrength + ")");
            }
        }
        if (dead.Tag == EnemyLibrary.T_Beast)
        {
            EnemyCombatant handler = FindGroupMate(dead, EnemyLibrary.T_Handler);
            if (handler != null)
            {
                AddLog("  ★ " + handler.Name + " 공포에 질림!");
                ApplyStatus(handler, StatusType.Weak, GameBalance.HandlerFearTurns);
                ApplyStatus(handler, StatusType.Vulnerable, GameBalance.HandlerFearTurns);
            }
        }
        if (dead.Tag == EnemyLibrary.T_KingGuard && FindGroupMate(dead, EnemyLibrary.T_King) != null)
        {
            dead.ReviveTimer = GameBalance.KingReviveTurns;
            AddLog("  ※ 왕의 강령술: " + dead.Name + " 적 차례 " + dead.ReviveTimer + "번 뒤 부활");
        }
        if (dead.Tag == EnemyLibrary.T_Twin && FindGroupMate(dead, EnemyLibrary.T_Twin) != null)
        {
            dead.ReviveTimer = GameBalance.TwinReviveTurns;
            AddLog("  ※ 부활 의식: " + dead.Name + " 적 차례 " + dead.ReviveTimer + "번 뒤 부활 (함께 무너뜨려야 함)");
        }
        if (dead.Tag == EnemyLibrary.T_Larva) RefreshSwarm();
    }

    // 적 차례가 끝날 때: 부활 대기 줄이기. 부활시키는 쪽(왕·짝 석상)이 쓰러졌으면 취소
    private void TickRevivals()
    {
        foreach (EnemyCombatant e in Enemies)
        {
            if (!e.IsDead || e.ReviveTimer <= 0) continue;
            string anchorTag = e.Tag == EnemyLibrary.T_KingGuard ? EnemyLibrary.T_King : EnemyLibrary.T_Twin;
            if (FindGroupMate(e, anchorTag) == null) { e.ReviveTimer = 0; continue; }
            e.ReviveTimer--;
            if (e.ReviveTimer > 0) continue;
            e.Hp = System.Math.Max(1, DamageCalculator.RoundHalfUp(e.MaxHp * e.ReviveHpRatio));
            e.Block = 0;
            e.Statuses.Clear();
            e.ExecuteStacks = 0; e.FreezeStacks = 0; e.ShockStacks = 0; e.Marks = 0;
            e.DeathHandled = false; e.DeathLogged = false; e.WasExecuted = false;
            e.MoveIndex = 0;
            AddLog("  ★ " + e.Name + " 부활! (HP " + e.Hp + ")");
        }
    }

    // 점토 분열체 → 작은 점토체 2마리 (남은 HP 를 각각 가짐). 목록을 바꾸므로 안전한 곳에서만 부름
    private void FlushSplits()
    {
        bool changed = false;
        for (int i = 0; i < Enemies.Count; i++)
        {
            EnemyCombatant e = Enemies[i];
            if (!e.SplitPending || e.IsDead) continue;
            Enemies.RemoveAt(i);
            for (int k = 0; k < 2; k++)
            {
                EnemyCombatant c = EnemyLibrary.SmallClay(e.Hp);
                c.AttackMultiplier = e.AttackMultiplier;
                c.BlockMultiplier = e.BlockMultiplier;
                c.MoveIndex = k;
                Enemies.Insert(i, c);
            }
            foreach (PlayerCombatant p in Players) if (p.MarkedEnemy == e) p.MarkedEnemy = null;
            AddLog("  ★ " + e.Name + " 분열! 작은 점토체 2마리 (각 HP " + e.Hp + ")");
            i++;   // 넣은 두 마리 건너뛰기
            changed = true;
        }
        if (changed) EnemyListVersion++;
    }

    // 망령 등불지기: 플레이어가 카드를 소멸하거나 버릴 때마다 힘 +1
    private void OnPlayerCardsLost(int n)
    {
        foreach (EnemyCombatant e in Enemies)
        {
            if (e.IsDead || e.Tag != EnemyLibrary.T_Lantern) continue;
            e.Strength += GameBalance.LanternStrength * n;
            AddLog("  " + e.Name + " 잃어버린 카드를 먹고 힘 +" + (GameBalance.LanternStrength * n) + " (" + e.Strength + ")");
        }
    }

    // 플레이어 턴 시작(카드를 뽑은 뒤): 차원 균열 소멸, 마녀 결속
    private void TurnStartMonsterEffects(PlayerCombatant p)
    {
        foreach (EnemyCombatant e in Enemies)
        {
            if (e.IsDead || p.Hand.Count == 0) continue;
            if (e.Tag == EnemyLibrary.T_Rift && Round % GameBalance.RiftExhaustEvery == 0)
            {
                CardData gone = p.Hand[rng.Next(p.Hand.Count)];
                p.Hand.Remove(gone);
                p.ExhaustPile.Add(gone);
                AddLog("  ※ " + e.Name + "의 균열: " + p.Name + " [" + gone.Name + "] 소멸");
            }
            if (e.Tag == EnemyLibrary.T_Witch)
            {
                List<CardData> pick = new List<CardData>();
                foreach (CardData c in p.Hand) if (!c.Unplayable && !c.Bound) pick.Add(c);
                if (pick.Count == 0) continue;
                CardData b = pick[rng.Next(pick.Count)];
                b.Bound = true;
                AddLog("  ※ " + e.Name + "의 결속: " + p.Name + " [" + b.Name + "] (이번 턴에 안 내면 마녀가 강해짐)");
            }
        }
    }

    // 플레이어 턴 종료(손패를 버리기 전): [화상] 피해, 안 낸 결속 카드 → 마녀 회복·힘
    private void EndTurnMonsterCards(PlayerCombatant p)
    {
        int burns = 0;
        bool boundLeft = false;
        foreach (CardData c in p.Hand)
        {
            if (c.BurnsInHand) burns++;
            if (c.Bound) { boundLeft = true; c.Bound = false; }
        }
        if (burns > 0)
        {
            int lost = p.LoseHp(burns * GameBalance.BurnCardHpLoss);
            AddLog("  " + p.Name + " [화상] " + burns + "장: HP -" + lost);
            CheckPhoenixHeart(p);
            if (p.IsDead) AddLog("  " + p.Name + " 쓰러짐!");
        }
        if (!boundLeft) return;
        foreach (EnemyCombatant e in Enemies)
        {
            if (e.IsDead || e.Tag != EnemyLibrary.T_Witch) continue;
            int healed = e.Heal(DamageCalculator.RoundHalfUp(GameBalance.WitchBindHeal * GameBalance.StageHpScale[Clamp(Stage - 1, 0, 3)]));
            e.Strength += GameBalance.WitchBindStrength;
            AddLog("  ※ " + e.Name + " 결속된 카드를 먹음: HP +" + healed + ", 힘 +" + GameBalance.WitchBindStrength);
        }
    }

    // 화면에 이름 옆에 붙일 짧은 표시 (은신·외골격·기절·부활)
    public string EnemyBadge(EnemyCombatant e)
    {
        List<string> parts = new List<string>();
        if (e.IsDead)
        {
            if (e.ReviveTimer > 0) parts.Add("부활까지 " + e.ReviveTimer);
        }
        else
        {
            if (e.Tag == EnemyLibrary.T_SkelArcher && FindGroupMate(e, EnemyLibrary.T_SkelShield) != null) parts.Add("은신");
            if (e.ShellHp > 0) parts.Add("외골격 " + e.ShellHp);
            if (e.Stunned) parts.Add("기절");
            if (e.SplitPending) parts.Add("분열 중");
        }
        return parts.Count == 0 ? "" : " [" + string.Join(", ", parts) + "]";
    }

    // ==================== 개발 도구 (에디터·개발 빌드에서만 버튼이 보임) ====================
    // 남은 웨이브까지 전부 없애고 바로 승리
    public void DevWin()
    {
        if (Result != BattleResult.Ongoing) return;
        UpcomingWaves.Clear();
        foreach (EnemyCombatant e in Enemies) if (!e.IsDead) e.LoseHp(e.Hp);
        AddLog("[개발 도구] 즉시 승리");
        CheckResult();
    }

    // ==================== 도우미 ====================
    // 카드 효과에서 쓰는 무작위 번호 (0 ~ n-1). 전투의 rng 를 같이 씀
    public int RandomIndex(int n) { return n <= 0 ? 0 : rng.Next(n); }

    public List<EnemyCombatant> LivingEnemies()
    {
        List<EnemyCombatant> alive = new List<EnemyCombatant>();
        foreach (EnemyCombatant e in Enemies) if (!e.IsDead) alive.Add(e);
        return alive;
    }

    public EnemyCombatant RandomLivingEnemy()
    {
        List<EnemyCombatant> alive = LivingEnemies();
        return alive.Count == 0 ? null : alive[rng.Next(alive.Count)];
    }

    public EnemyCombatant FirstLivingEnemy()
    {
        foreach (EnemyCombatant enemy in Enemies)
        {
            if (!enemy.IsDead) return enemy;
        }
        return null;
    }

    private void CheckResult()
    {
        if (Result != BattleResult.Ongoing) return;

        HandleEnemyDeaths();
        CheckFallenAllies();   // 3-176 전우애의 방패

        bool allEnemiesDead = true;
        foreach (EnemyCombatant enemy in Enemies) if (!enemy.IsDead) allEnemiesDead = false;
        bool allPlayersDead = true;
        foreach (PlayerCombatant player in Players) if (!player.IsDead) allPlayersDead = false;

        if (allPlayersDead)
        {
            Result = BattleResult.Defeat;
            AddLog("패배...");
        }
        else if (allEnemiesDead && UpcomingWaves.Count == 0)
        {
            OnWaveCleared();
            Result = BattleResult.Victory;
            AddLog("승리! (" + Round + "턴)");
        }
    }

    // 3-176 전우애의 방패 (파티): 아군이 새로 쓰러지면 남은 아군 모두 방어도 +15
    //   쓰러지는 곳이 여러 군데(적 공격, 페널티, 발화…)라서, 결과를 확인할 때 "새로 쓰러진 사람" 을 한곳에서 찾음
    private void CheckFallenAllies()
    {
        foreach (PlayerCombatant p in Players)
        {
            if (!p.IsDead || fallenSeen.Contains(p)) continue;
            fallenSeen.Add(p);
            if (!PartyHas(RelicLibrary.ComradeShield)) continue;
            foreach (PlayerCombatant q in Players)
            {
                if (q.IsDead) continue;
                q.GainBlock(GameBalance.ComradeShieldBlock);
                AddLog("전우애의 방패: " + q.Name + " 방어도 +" + GameBalance.ComradeShieldBlock + " (" + p.Name + " 쓰러짐)");
            }
        }
    }

    // 지금 웨이브를 다 잡았고 다음 웨이브가 남아 있으면, 다음 웨이브 적을 들여보냄
    // ※ CheckResult 안에서 하지 않는 이유: foreach 로 Enemies 를 도는 도중에 목록을 바꾸면 C#은 오류를 냄
    //   (자바의 ConcurrentModificationException 과 같음). 그래서 안전한 곳에서만 부름
    private void TrySpawnNextWave()
    {
        FlushSplits();   // 3-102: 점토 분열 (목록을 바꿔도 안전한 곳에서)
        if (Result != BattleResult.Ongoing || UpcomingWaves.Count == 0) return;
        foreach (EnemyCombatant enemy in Enemies) if (!enemy.IsDead) return;
        bool anyPlayerAlive = false;
        foreach (PlayerCombatant player in Players) if (!player.IsDead) anyPlayerAlive = true;
        if (!anyPlayerAlive) return;

        OnWaveCleared();
        List<EnemyCombatant> next = UpcomingWaves[0];
        UpcomingWaves.RemoveAt(0);
        ApplyPartyScaling(next);
        InitSpecialEnemies(next);   // 3-102
        Enemies.Clear();
        Enemies.AddRange(next);
        RefreshSwarm();
        WaveNumber++;
        WaveVersion++;
        foreach (PlayerCombatant player in Players) player.MarkedEnemy = null;   // 인장은 쓰러진 적에게 남지 않게
        AddLog("===== 웨이브 " + WaveNumber + " / " + WaveCount + " =====");
        OnWaveStart();
    }

    // 새로 쓰러진 적 처리: 무리 분노, 죽음의 인장·사형 선고·사신의 수의 보상
    private void HandleEnemyDeaths()
    {
        foreach (EnemyCombatant dead in Enemies)
        {
            if (!dead.IsDead || dead.DeathHandled) continue;
            dead.DeathHandled = true;

            foreach (PlayerCombatant p in Players)
            {
                if (p.IsDead) continue;
                if (p.MarkedEnemy == dead)
                {
                    int soul = p.MarkUpgraded ? GameBalance.DeathMarkKillSoulUp : GameBalance.DeathMarkKillSoul;
                    int heal = p.MarkUpgraded ? GameBalance.DeathMarkKillHealUp : GameBalance.DeathMarkKillHeal;
                    p.GainMana(soul);
                    int healed = p.Heal(heal);
                    AddLog("  " + p.Name + " 죽음의 인장: 영혼 +" + soul + ", HP +" + healed);
                    p.MarkedEnemy = null;
                }
                if (dead.SentencedBy == p && dead.WasExecuted)
                {
                    int healed = p.Heal(GameBalance.SentenceHeal);
                    AddLog("  " + p.Name + " 사형 선고 집행: HP +" + healed);
                }
                if (dead.ShroudBy == p)
                {
                    p.Energy += GameBalance.ShroudEnergy;
                    p.GainMana(GameBalance.ShroudSoulGain);
                    AddLog("  " + p.Name + " 사신의 수의: 에너지 +" + GameBalance.ShroudEnergy + ", 영혼 +" + GameBalance.ShroudSoulGain + ", " + GameBalance.ShroudDraw + "장 뽑기");
                    if (!p.HasEndedTurn) Draw(p, GameBalance.ShroudDraw);
                }
            }
            dead.ShroudBy = null;

            if (dead.SquadId == 0) continue;
            foreach (EnemyCombatant ally in Enemies)
            {
                if (ally.IsDead || ally.SquadId != dead.SquadId) continue;
                ally.RageStacks++;
                ally.AttackMultiplier *= 1f + GameBalance.SquadRageBonus;
                AddLog("  ★ " + ally.Name + " 분노! (동료가 쓰러짐, 공격력 +" + (int)(GameBalance.SquadRageBonus * 100) + "%)");
            }
        }
    }
}
