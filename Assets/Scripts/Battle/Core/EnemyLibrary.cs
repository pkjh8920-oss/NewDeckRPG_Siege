// ============================================================
// EnemyLibrary.cs
// 몬스터와 전투 편성(어떤 몬스터들이 함께 나오는지) 목록입니다.
// 숫자는 GameBalance 에서 가져옵니다.
//
// 등급 (새 구조)
//   일반 = 균열 슬라임, 연쇄 박쥐
//   정예 = 오염된 병정, 철갑 골렘
//   엘리트 = 엘리트 감시자 / 까다로운 엘리트 = 감시자 무리 (3마리가 한 팀)
//   보스 = 수문장 (1~3스테이지), 설계자 (4스테이지)
//
// 종족 (웨어울프족 / 오크족 / 좀비족)
//   아직 종족 몬스터가 없어서, 지금은 어느 종족이든 위 몬스터가 "임시"로 나옴.
//   종족 몬스터를 만들면 아래 CreateNormal / CreateVeteran / CreateElite / CreateStageBoss 의
//   switch 에 종족별로 한 줄씩 추가하면 됨 (다른 파일은 안 고쳐도 됨)
// ============================================================

using System.Collections.Generic;

// 1~3스테이지에 침공하는 종족 (스테이지마다 무작위 순서)
public enum Race
{
    Werewolf,  // 웨어울프족
    Orc,       // 오크족
    Zombie     // 좀비족
}

public static class EnemyLibrary
{
    // EnemyMove(이름, 1회 피해, 횟수, 방어도, 취약) { PlayerWeak = 약화, Strength = 힘, Heal = 회복 }
    // 몬스터마다 "싸우는 방식"이 다르게 (2026-11-08)
    //   슬라임 = 약화를 거는 탱커 / 박쥐 = 연타 + 흡혈 / 병정 = 함성으로 점점 세짐 / 골렘 = 방어도 + 과열
    //   감시자 = 취약·약화로 약하게 만든 뒤 광선 / 감시자 무리 = 역할 분담
    //   수문장은 종족마다 다름 (웨어울프 = 연타·힘, 오크 = 한 방·방패, 좀비 = 약화·회복)
    //   설계자 = 최종 보스. 2페이즈에서 연타와 자가 복구

    public static EnemyCombatant RiftSlime()
    {
        List<EnemyMove> pattern = new List<EnemyMove>();
        pattern.Add(new EnemyMove("몸통 박치기", GameBalance.SlimeTackle, 1, 0));
        pattern.Add(new EnemyMove("끈적한 점액", GameBalance.SlimeGooHit, 1, GameBalance.SlimeGooBlock) { PlayerWeak = GameBalance.EnemyWeakTurns, AddSlimeCards = GameBalance.SlimeGooCards });   // 3-149: + [점액] 카드
        return new EnemyCombatant("균열 슬라임", GameBalance.SlimeHp, pattern, EnemyRank.Normal);
    }

    public static EnemyCombatant ChainBat()
    {
        List<EnemyMove> pattern = new List<EnemyMove>();
        pattern.Add(new EnemyMove("연쇄 물기", GameBalance.BatBite, GameBalance.BatBiteHits, 0));
        pattern.Add(new EnemyMove("흡혈", GameBalance.BatDrain, 1, 0) { Heal = GameBalance.BatDrainHeal });
        pattern.Add(new EnemyMove("날개 치기", GameBalance.BatWing, 1, 0));
        return new EnemyCombatant("연쇄 박쥐", GameBalance.BatHp, pattern, EnemyRank.Normal);
    }

    public static EnemyCombatant CorruptedSoldier()
    {
        List<EnemyMove> pattern = new List<EnemyMove>();
        pattern.Add(new EnemyMove("베기", GameBalance.SoldierSlash, 1, 0));
        pattern.Add(new EnemyMove("방패 들기", GameBalance.SoldierShieldHit, 1, GameBalance.SoldierShieldBlock));
        pattern.Add(new EnemyMove("오염된 함성", 0, 0, 0) { Strength = GameBalance.SoldierShout });
        pattern.Add(new EnemyMove("강타", GameBalance.SoldierSmash, 1, 0));
        return new EnemyCombatant("오염된 병정", GameBalance.SoldierHp, pattern, EnemyRank.Veteran);
    }

    public static EnemyCombatant IronGolem()
    {
        List<EnemyMove> pattern = new List<EnemyMove>();
        pattern.Add(new EnemyMove("강철화", 0, 0, GameBalance.GolemHarden));
        pattern.Add(new EnemyMove("철권", GameBalance.GolemPunch, 1, 0));
        pattern.Add(new EnemyMove("과열", 0, 0, GameBalance.GolemOverheatBlock) { Strength = GameBalance.GolemOverheat });
        pattern.Add(new EnemyMove("철벽 반격", GameBalance.GolemGuardHit, 1, GameBalance.GolemGuardBlock));
        return new EnemyCombatant("철갑 골렘", GameBalance.GolemHp, pattern, EnemyRank.Veteran);
    }

    public static EnemyCombatant Watcher()
    {
        List<EnemyMove> pattern = new List<EnemyMove>();
        pattern.Add(new EnemyMove("응시", 0, 0, GameBalance.WatcherGazeBlock, GameBalance.EnemyVulnerableTurns));
        pattern.Add(new EnemyMove("광선", GameBalance.WatcherBeam, 1, 0));
        pattern.Add(new EnemyMove("분석", GameBalance.WatcherScan, 1, 0) { PlayerWeak = GameBalance.EnemyWeakTurns });
        pattern.Add(new EnemyMove("이중 광선", GameBalance.WatcherDoubleBeam, 2, 0));
        return new EnemyCombatant("엘리트 감시자", GameBalance.WatcherHp, pattern, EnemyRank.Elite);
    }

    // ==================== 새 몬스터 (2026-11-15) ====================
    // 슬더스2 처럼 "무엇을 조심해야 하는지"가 몬스터마다 다르게
    //   역병 쥐 = 상처 카드로 손패 방해 / 불씨 임프 = 발화(시간이 지날수록 아픔) / 그물 거미 = 카드 덜 뽑기 + 손상
    //   광신도 = 의식으로 매 턴 힘이 오름 → 빨리 잡아야 함
    //   오크 투석병 = 손상 + 연타 / 가시 갑주병 = 가시 (공격 카드로 때리면 아픔 → 한 방 큰 공격이 좋음)
    //   역병 주술사(엘리트) = 상처·발화·의식 / 그림자 암살자(엘리트) = 연타 + 카드 덜 뽑기 + 손상
    public static EnemyCombatant PlagueRat()
    {
        List<EnemyMove> p = new List<EnemyMove>();
        p.Add(new EnemyMove("갉아먹기", GameBalance.RatGnaw, GameBalance.RatGnawHits, 0));
        p.Add(new EnemyMove("역병 물기", GameBalance.RatBite, 1, 0) { AddWounds = GameBalance.RatWounds });
        return new EnemyCombatant("역병 쥐", GameBalance.RatHp, p, EnemyRank.Normal);
    }

    public static EnemyCombatant EmberImp()
    {
        List<EnemyMove> p = new List<EnemyMove>();
        p.Add(new EnemyMove("불씨 던지기", GameBalance.ImpEmber, 1, 0) { PlayerBurn = GameBalance.ImpEmberBurn });
        p.Add(new EnemyMove("날갯짓", 0, 0, GameBalance.ImpFlap));
        p.Add(new EnemyMove("화염 숨결", GameBalance.ImpBreath, 1, 0));
        return new EnemyCombatant("불씨 임프", GameBalance.ImpHp, p, EnemyRank.Normal);
    }

    public static EnemyCombatant WebSpider()
    {
        List<EnemyMove> p = new List<EnemyMove>();
        p.Add(new EnemyMove("거미줄", 0, 0, 0) { PlayerWeak = GameBalance.EnemyWeakTurns, PlayerDrawDown = 1 });
        p.Add(new EnemyMove("독니", GameBalance.SpiderFang, 1, 0));
        p.Add(new EnemyMove("덮치기", GameBalance.SpiderPounce, 1, 0) { PlayerFrail = GameBalance.SpiderFrail });
        return new EnemyCombatant("그물 거미", GameBalance.SpiderHp, p, EnemyRank.Normal);
    }

    public static EnemyCombatant Cultist()
    {
        List<EnemyMove> p = new List<EnemyMove>();
        p.Add(new EnemyMove("검은 의식", 0, 0, 0) { GainRitual = GameBalance.CultistRitual });
        p.Add(new EnemyMove("단검", GameBalance.CultistDagger, 1, 0));
        p.Add(new EnemyMove("단검", GameBalance.CultistDagger, 1, 0));
        p.Add(new EnemyMove("단검", GameBalance.CultistDagger, 1, 0));
        return new EnemyCombatant("광신도", GameBalance.CultistHp, p, EnemyRank.Normal);
    }

    public static EnemyCombatant OrcSlinger()
    {
        List<EnemyMove> p = new List<EnemyMove>();
        p.Add(new EnemyMove("바위 던지기", GameBalance.SlingerRock, 1, 0) { PlayerFrail = GameBalance.SlingerFrail });
        p.Add(new EnemyMove("방패 세우기", 0, 0, GameBalance.SlingerShield));
        p.Add(new EnemyMove("돌 세례", GameBalance.SlingerVolley, GameBalance.SlingerVolleyHits, 0));
        return new EnemyCombatant("오크 투석병", GameBalance.SlingerHp, p, EnemyRank.Veteran);
    }

    public static EnemyCombatant ThornKnight()
    {
        List<EnemyMove> p = new List<EnemyMove>();
        p.Add(new EnemyMove("가시 돋우기", 0, 0, GameBalance.ThornKnightBlock) { GainThorns = GameBalance.ThornKnightThorns });
        p.Add(new EnemyMove("가시 찌르기", GameBalance.ThornKnightStab, 1, 0));
        p.Add(new EnemyMove("돌진", GameBalance.ThornKnightCharge, 1, 0));
        return new EnemyCombatant("가시 갑주병", GameBalance.ThornKnightHp, p, EnemyRank.Veteran);
    }

    public static EnemyCombatant PlagueShaman()
    {
        List<EnemyMove> p = new List<EnemyMove>();
        p.Add(new EnemyMove("저주의 주문", 0, 0, 0, GameBalance.EnemyVulnerableTurns) { AddWounds = GameBalance.ShamanCurseWounds });
        p.Add(new EnemyMove("역병 화살", GameBalance.ShamanArrow, 2, 0) { PlayerBurn = GameBalance.ShamanArrowBurn });
        p.Add(new EnemyMove("어둠의 의식", 0, 0, GameBalance.ShamanRitualBlock) { GainRitual = GameBalance.ShamanRitual });
        p.Add(new EnemyMove("영혼 흡수", GameBalance.ShamanDrain, 1, 0) { Heal = GameBalance.ShamanDrainHeal });
        return new EnemyCombatant("역병 주술사", GameBalance.ShamanHp, p, EnemyRank.Elite);
    }

    public static EnemyCombatant ShadowAssassin()
    {
        List<EnemyMove> p = new List<EnemyMove>();
        p.Add(new EnemyMove("연속 찌르기", GameBalance.AssassinStab, GameBalance.AssassinStabHits, 0));
        p.Add(new EnemyMove("연막", 0, 0, GameBalance.AssassinSmoke) { PlayerWeak = GameBalance.EnemyWeakTurns, PlayerDrawDown = 1 });
        p.Add(new EnemyMove("급소 찌르기", GameBalance.AssassinVital, 1, 0) { PlayerFrail = GameBalance.AssassinVitalFrail });
        return new EnemyCombatant("그림자 암살자", GameBalance.AssassinHp, p, EnemyRank.Elite);
    }

    // 보스 공통: 2페이즈 붙이기
    static EnemyCombatant MakeBoss(string name, int hp, List<EnemyMove> phase1, List<EnemyMove> phase2, int enrageBlock, float ratio)
    {
        EnemyCombatant boss = new EnemyCombatant(name, hp, phase1, EnemyRank.Boss);
        boss.Phase2Pattern = phase2;
        boss.EnrageHpRatio = ratio;
        boss.EnrageBlock = enrageBlock;
        return boss;
    }

    // 웨어울프족 수문장: 연타 + 포효로 힘이 계속 오름 → 오래 끌수록 위험
    public static EnemyCombatant WolfGatekeeper()
    {
        List<EnemyMove> p1 = new List<EnemyMove>();
        p1.Add(new EnemyMove("할퀴기", GameBalance.WolfClaw, 3, 0));
        p1.Add(new EnemyMove("포효", 0, 0, 0, GameBalance.EnemyVulnerableTurns) { Strength = GameBalance.WolfHowlStrength });
        p1.Add(new EnemyMove("물어뜯기", GameBalance.WolfBite, 1, 0) { Heal = GameBalance.WolfBiteHeal });
        List<EnemyMove> p2 = new List<EnemyMove>();
        p2.Add(new EnemyMove("광란", GameBalance.WolfFrenzy, 4, 0));
        p2.Add(new EnemyMove("피의 포효", 0, 0, 0) { Strength = GameBalance.WolfBloodHowl });
        p2.Add(new EnemyMove("사냥", GameBalance.WolfHunt, 1, 0));
        return MakeBoss("광폭한 수문장", GameBalance.WolfBossHp, p1, p2, GameBalance.WolfEnrageBlock, GameBalance.BossEnrageRatio);
    }

    // 오크족 수문장: 큰 한 방 + 두꺼운 방패 → 관통·방어도 무시 카드가 빛남
    public static EnemyCombatant OrcGatekeeper()
    {
        List<EnemyMove> p1 = new List<EnemyMove>();
        p1.Add(new EnemyMove("철퇴 내려찍기", GameBalance.OrcSmash, 1, 0));
        p1.Add(new EnemyMove("방패벽", 0, 0, GameBalance.OrcShieldWall));
        p1.Add(new EnemyMove("전쟁 함성", GameBalance.OrcWarCryHit, 1, 0) { PlayerWeak = GameBalance.EnemyWeakTurns, Strength = GameBalance.OrcWarCryStrength });
        List<EnemyMove> p2 = new List<EnemyMove>();
        p2.Add(new EnemyMove("대지 분쇄", GameBalance.OrcQuake, 1, 0));
        p2.Add(new EnemyMove("철벽 반격", GameBalance.OrcCounterHit, 1, GameBalance.OrcCounterBlock));
        p2.Add(new EnemyMove("광분", GameBalance.OrcRampage, 3, 0));
        return MakeBoss("철퇴 수문장", GameBalance.OrcBossHp, p1, p2, GameBalance.OrcEnrageBlock, GameBalance.BossEnrageRatio);
    }

    // 좀비족 수문장: 약화·취약 + 회복 → 회복 턴에 몰아치기
    public static EnemyCombatant ZombieGatekeeper()
    {
        List<EnemyMove> p1 = new List<EnemyMove>();
        p1.Add(new EnemyMove("썩은 손톱", GameBalance.ZombieClaw, 1, 0) { PlayerWeak = GameBalance.EnemyWeakTurns });
        p1.Add(new EnemyMove("부패의 숨결", 0, 0, 0, GameBalance.EnemyVulnerableTurns) { PlayerWeak = GameBalance.EnemyWeakTurns });
        p1.Add(new EnemyMove("시체 먹기", 0, 0, GameBalance.ZombieFeastBlock) { Heal = GameBalance.ZombieFeastHeal });
        List<EnemyMove> p2 = new List<EnemyMove>();
        p2.Add(new EnemyMove("역병 폭발", GameBalance.ZombiePlague, 1, 0, GameBalance.EnemyVulnerableTurns));
        p2.Add(new EnemyMove("재생", 0, 0, 0) { Heal = GameBalance.ZombieRegenHeal, Strength = GameBalance.ZombieRegenStrength });
        p2.Add(new EnemyMove("무덤의 손아귀", GameBalance.ZombieGrasp, 3, 0));
        return MakeBoss("부패한 수문장", GameBalance.ZombieBossHp, p1, p2, GameBalance.ZombieEnrageBlock, GameBalance.BossEnrageRatio);
    }

    // ======================================================================
    // 새 구조: 등급별 몬스터 + 웨이브 만들기
    // ======================================================================

    public static string RaceName(Race race)
    {
        switch (race)
        {
            case Race.Werewolf: return "웨어울프족";
            case Race.Orc: return "오크족";
            default: return "좀비족";
        }
    }

    // 일반: 6종 중 무작위 (2026-11-15: 쥐·임프·거미·광신도 추가)
    public static EnemyCombatant CreateNormal(Race race, System.Random rng)
    {
        switch (rng.Next(6))
        {
            case 0: return RiftSlime();
            case 1: return ChainBat();
            case 2: return PlagueRat();
            case 3: return EmberImp();
            case 4: return WebSpider();
            default: return Cultist();
        }
    }

    // 정예: 4종 중 무작위 (2026-11-15: 오크 투석병·가시 갑주병 추가)
    public static EnemyCombatant CreateVeteran(Race race, System.Random rng)
    {
        switch (rng.Next(4))
        {
            case 0: return CorruptedSoldier();
            case 1: return IronGolem();
            case 2: return OrcSlinger();
            default: return ThornKnight();
        }
    }

    // 엘리트: 3종 중 무작위 (2026-11-15: 역병 주술사·그림자 암살자 추가)
    public static EnemyCombatant CreateElite(Race race, System.Random rng)
    {
        switch (rng.Next(3))
        {
            case 0: return Watcher();
            case 1: return PlagueShaman();
            default: return ShadowAssassin();
        }
    }

    // 스테이지 보스 (1~3스테이지 = 종족의 수문장. 지금은 전부 수문장)
    public static EnemyCombatant CreateStageBoss(Race race, System.Random rng)
    {
        switch (race)
        {
            case Race.Werewolf: return WolfGatekeeper();
            case Race.Orc: return OrcGatekeeper();
            default: return ZombieGatekeeper();
        }
    }

    // 보스 앞 초소에서 정찰병이 알려 주는 보스 힌트 (보스 패턴과 맞춰 둘 것)
    public static string StageBossHint(Race race)
    {
        switch (race)
        {
            case Race.Werewolf:
                return "몬스터들의 우두머리는 광폭한 수문장입니다.\n"
                     + "할퀴기를 연달아 휘두르고, 포효할 때마다 더 강해집니다.\n"
                     + "오래 끌면 위험합니다. 체력이 절반이 되면 광란에 빠집니다.";
            case Race.Orc:
                return "몬스터들의 우두머리는 철퇴 수문장입니다.\n"
                     + "한 방이 무겁고 방패벽으로 단단히 버팁니다.\n"
                     + "방어도를 뚫는 공격을 준비하세요. 절반이 되면 대지를 부숩니다.";
            default:
                return "몬스터들의 우두머리는 부패한 수문장입니다.\n"
                     + "썩은 숨결로 우리를 약하게 만들고 시체를 먹어 회복합니다.\n"
                     + "회복하는 틈에 몰아치세요. 절반이 되면 역병을 터뜨립니다.";
        }
    }

    public static string ArchitectHint()
    {
        return "소굴 가장 깊은 곳에 모든 침공을 설계한 자가 있습니다.\n"
             + "구조를 보강할 때마다 강해지고, 오류를 주입해 우리를 약하게 만듭니다.\n"
             + "체력이 절반이 되면 붕괴 연쇄와 자가 복구를 번갈아 씁니다.";
    }

    // 까다로운 엘리트: 감시자 무리. 체력을 나눠 가진 3마리가 역할을 나눠 싸움
    // squadId = 같은 무리끼리 같은 번호 (한 웨이브에 무리가 둘이면 번호가 달라야 함)
    public static List<EnemyCombatant> WatcherSquad(int squadId)
    {
        List<EnemyCombatant> squad = new List<EnemyCombatant>();

        List<EnemyMove> attacker = new List<EnemyMove>();
        attacker.Add(new EnemyMove("광선", GameBalance.SquadBeam, 1, 0));
        attacker.Add(new EnemyMove("집중 광선", GameBalance.SquadFocusBeam, 1, 0));
        squad.Add(new EnemyCombatant("공격의 감시자", GameBalance.SquadEyeHp, attacker));

        List<EnemyMove> guard = new List<EnemyMove>();
        guard.Add(new EnemyMove("수호의 시선", 0, 0, 0) { AllyBlock = GameBalance.SquadAllyBlock });
        guard.Add(new EnemyMove("약한 광선", GameBalance.SquadWeakBeam, 1, 0));
        squad.Add(new EnemyCombatant("수호의 감시자", GameBalance.SquadEyeHp, guard));

        List<EnemyMove> curse = new List<EnemyMove>();
        curse.Add(new EnemyMove("약한 광선", GameBalance.SquadWeakBeam, 1, 0));
        curse.Add(new EnemyMove("저주의 시선", 0, 0, 0, GameBalance.EnemyVulnerableTurns) { PlayerWeak = GameBalance.EnemyWeakTurns });
        squad.Add(new EnemyCombatant("저주의 감시자", GameBalance.SquadEyeHp, curse));

        foreach (EnemyCombatant e in squad) { e.SquadId = squadId; e.SetRank(EnemyRank.Elite); }
        // 번갈아 행동: 시작 위치를 다르게 해서 셋이 같은 턴에 같은 걸 하지 않게
        squad[1].MoveIndex = 0;   // 수호: 첫 턴에 방어도부터
        squad[2].MoveIndex = 1;   // 저주: 첫 턴에 취약부터
        return squad;
    }

    // 4스테이지 최종 보스: 설계자
    public static EnemyCombatant Architect()
    {
        List<EnemyMove> p1 = new List<EnemyMove>();
        p1.Add(new EnemyMove("설계 수정", GameBalance.ArchitectStrike, 1, 0));
        p1.Add(new EnemyMove("구조 보강", 0, 0, GameBalance.ArchitectWallBlock) { Strength = GameBalance.ArchitectWallStrength });
        p1.Add(new EnemyMove("오류 주입", GameBalance.ArchitectPressure, 1, 0, GameBalance.EnemyVulnerableTurns) { PlayerWeak = GameBalance.EnemyWeakTurns });
        List<EnemyMove> p2 = new List<EnemyMove>();
        p2.Add(new EnemyMove("재설계", GameBalance.ArchitectRageStrike, 1, 0));
        p2.Add(new EnemyMove("붕괴 연쇄", GameBalance.ArchitectFlurry, GameBalance.ArchitectFlurryHits, 0));
        p2.Add(new EnemyMove("자가 복구", 0, 0, GameBalance.ArchitectRepairBlock) { Heal = GameBalance.ArchitectRepairHeal });
        p2.Add(new EnemyMove("오류 주입", GameBalance.ArchitectPressure, 1, 0, GameBalance.EnemyVulnerableTurns) { PlayerWeak = GameBalance.EnemyWeakTurns });
        return MakeBoss("보스 설계자", GameBalance.ArchitectHp, p1, p2, GameBalance.ArchitectEnrageBlock, GameBalance.ArchitectEnrageRatio);
    }


    // ======================================================================
    // 3-102 새 몬스터 (몬스터 통합 도감). 특수 규칙은 Tag 로 구분하고 BattleSystem 이 처리
    //   무리 몬스터(2~4마리)는 한 번에 List 로 돌려줌 → 웨이브 슬롯 하나를 차지
    // ======================================================================
    public const string T_Beetle = "beetle", T_Spore = "spore", T_Clay = "clay", T_Lantern = "lantern", T_Larva = "larva",
                        T_BoneCollector = "bone", T_SkelArcher = "skel_archer", T_SkelShield = "skel_shield",
                        T_Sentinel = "sentinel", T_Witch = "witch", T_Handler = "handler", T_Beast = "beast",
                        T_Shell = "shell", T_King = "king", T_KingGuard = "king_guard", T_Rift = "rift", T_Twin = "twin";
    static int nextGroupId = 1000;   // 무리 번호 (전투마다 겹치지만 않으면 됨)

    static EnemyCombatant Tagged(EnemyCombatant e, string tag, int group = 0) { e.Tag = tag; e.GroupId = group; return e; }

    // ---- 일반 ----
    public static EnemyCombatant HornBeetle()
    {
        List<EnemyMove> p = new List<EnemyMove>();
        p.Add(new EnemyMove("뿔 박치기", GameBalance.BeetleRam, 1, 0));
        p.Add(new EnemyMove("껍질 웅크리기", GameBalance.BeetleCurlHit, 1, GameBalance.BeetleCurlBlock));
        return Tagged(new EnemyCombatant("투구 딱정벌레", GameBalance.BeetleHp, p, EnemyRank.Normal), T_Beetle);
    }

    public static EnemyCombatant Sporeling()
    {
        List<EnemyMove> p = new List<EnemyMove>();
        p.Add(new EnemyMove("포자 뿌리기", GameBalance.SporeSpray, 1, 0) { PlayerWeak = GameBalance.EnemyWeakTurns });
        p.Add(new EnemyMove("기생 물기", GameBalance.SporeBite, 1, 0));
        return Tagged(new EnemyCombatant("기생 포자충", GameBalance.SporeHp, p, EnemyRank.Normal), T_Spore);
    }

    public static EnemyCombatant ClaySplitter()
    {
        List<EnemyMove> p = new List<EnemyMove>();
        p.Add(new EnemyMove("진흙 주먹", GameBalance.ClayPunch, 1, 0));
        p.Add(new EnemyMove("굳히기", 0, 0, GameBalance.ClayHarden));
        return Tagged(new EnemyCombatant("점토 분열체", GameBalance.ClayHp, p, EnemyRank.Normal), T_Clay);
    }

    // 분열로 생기는 작은 점토체 (HP 는 분열 순간의 남은 HP)
    public static EnemyCombatant SmallClay(int hp)
    {
        List<EnemyMove> p = new List<EnemyMove>();
        p.Add(new EnemyMove("진흙 튀기기", GameBalance.ClaySmallHit, 1, 0));
        p.Add(new EnemyMove("진흙 튀기기", GameBalance.ClaySmallHit, 1, 0) { PlayerWeak = GameBalance.EnemyWeakTurns });
        return new EnemyCombatant("작은 점토체", System.Math.Max(1, hp), p, EnemyRank.Normal);
    }

    public static EnemyCombatant LanternKeeper()
    {
        List<EnemyMove> p = new List<EnemyMove>();
        p.Add(new EnemyMove("망령불", GameBalance.LanternFire, 1, 0));
        p.Add(new EnemyMove("등불 흔들기", GameBalance.LanternSwing, GameBalance.LanternSwingHits, 0));
        return Tagged(new EnemyCombatant("망령 등불지기", GameBalance.LanternHp, p, EnemyRank.Normal), T_Lantern);
    }

    // 자폭 불씨 정령단 (3마리): 불꽃 튀기기 → 달아오름 → 자폭 (큰 피해 + 화상 카드)
    public static List<EnemyCombatant> EmberSpirits()
    {
        List<EnemyCombatant> list = new List<EnemyCombatant>();
        int g = nextGroupId++;
        for (int i = 0; i < 3; i++)
        {
            List<EnemyMove> p = new List<EnemyMove>();
            p.Add(new EnemyMove("불꽃 튀기기", GameBalance.EmberSpark, 1, 0));
            p.Add(new EnemyMove("달아오름 (다음 차례 자폭)", 0, 0, 0));
            p.Add(new EnemyMove("자폭", GameBalance.EmberBlast, 1, 0) { SelfDestruct = true, AddBurnCards = GameBalance.EmberBlastBurnCards });
            list.Add(Tagged(new EnemyCombatant("불씨 정령", GameBalance.EmberSpiritHp, p, EnemyRank.Normal), "", g));
        }
        return list;
    }

    // 점액 유충 무리 (4마리): 살아 있는 유충 수만큼 물기가 세짐 → 광역기로 한꺼번에
    public static List<EnemyCombatant> LarvaSwarm()
    {
        List<EnemyCombatant> list = new List<EnemyCombatant>();
        int g = nextGroupId++;
        for (int i = 0; i < 4; i++)
        {
            List<EnemyMove> p = new List<EnemyMove>();
            p.Add(new EnemyMove("떼 지어 물기", GameBalance.LarvaBite, 1, 0));
            list.Add(Tagged(new EnemyCombatant("점액 유충", GameBalance.LarvaHp, p, EnemyRank.Normal), T_Larva, g));
        }
        return list;
    }

    // 광신도 수련생들 (3명): 단검 1 + 방패 1 + 주술 1  (3-140: 4명 → 3명, 단검 수련생 1명 뺌)
    public static List<EnemyCombatant> Apprentices()
    {
        List<EnemyCombatant> list = new List<EnemyCombatant>();
        int g = nextGroupId++;
        for (int i = 0; i < 1; i++)   // 3-140: 2 → 1
        {
            List<EnemyMove> d = new List<EnemyMove>();
            d.Add(new EnemyMove("단검 난도질", GameBalance.ApprenticeDagger, GameBalance.ApprenticeDaggerHits, 0));
            list.Add(Tagged(new EnemyCombatant("단검 수련생", GameBalance.ApprenticeHp, d, EnemyRank.Normal), "", g));
        }
        List<EnemyMove> s = new List<EnemyMove>();
        s.Add(new EnemyMove("방패 진형", 0, 0, 0) { AllyBlock = GameBalance.ApprenticeShield });
        s.Add(new EnemyMove("방패 밀치기", GameBalance.ApprenticeDagger, 1, 0));
        list.Insert(1, Tagged(new EnemyCombatant("방패 수련생", GameBalance.ApprenticeHp, s, EnemyRank.Normal), "", g));
        List<EnemyMove> h = new List<EnemyMove>();
        h.Add(new EnemyMove("저주 주문", GameBalance.ApprenticeHex, 1, 0) { PlayerWeak = GameBalance.EnemyWeakTurns });
        h.Add(new EnemyMove("저주 주문", GameBalance.ApprenticeHex, 1, 0) { PlayerFrail = 1 });
        list.Add(Tagged(new EnemyCombatant("주술 수련생", GameBalance.ApprenticeHp, h, EnemyRank.Normal), "", g));
        return list;
    }

    // ---- 정예 ----
    public static EnemyCombatant BoneCollector()
    {
        List<EnemyMove> p = new List<EnemyMove>();
        p.Add(new EnemyMove("뼈 갑옷", GameBalance.BoneArmorHit, 1, GameBalance.BoneArmorBlock));
        p.Add(new EnemyMove("뼈 폭풍", GameBalance.BoneStormMin, 1, 0) { DamageFromBlock = true });
        return Tagged(new EnemyCombatant("뼈조각 수집가", GameBalance.BoneHp, p, EnemyRank.Veteran), T_BoneCollector);
    }

    // 해골 사수 & 방패병: 방패병이 살아 있으면 사수가 받는 공격 피해 -50% (은신)
    public static List<EnemyCombatant> SkeletonPair()
    {
        int g = nextGroupId++;
        List<EnemyMove> s = new List<EnemyMove>();
        s.Add(new EnemyMove("방패 들기", 0, 0, GameBalance.SkelShieldAlly) { AllyBlock = GameBalance.SkelShieldAlly });
        s.Add(new EnemyMove("방패 밀치기", GameBalance.SkelShieldBash, 1, 0));
        List<EnemyMove> a = new List<EnemyMove>();
        a.Add(new EnemyMove("연속 사격", GameBalance.SkelVolley, GameBalance.SkelVolleyHits, 0));
        a.Add(new EnemyMove("조준 사격", GameBalance.SkelAimedShot, 1, 0));
        List<EnemyCombatant> list = new List<EnemyCombatant>();
        list.Add(Tagged(new EnemyCombatant("해골 방패병", GameBalance.SkelShieldHp, s, EnemyRank.Veteran), T_SkelShield, g));
        list.Add(Tagged(new EnemyCombatant("해골 사수", GameBalance.SkelArcherHp, a, EnemyRank.Veteran), T_SkelArcher, g));
        return list;
    }

    // 고블린 약탈대: 투창병(손상) + 대장(동료 힘) + 그물병(카드 덜 뽑기)
    public static List<EnemyCombatant> GoblinRaiders()
    {
        int g = nextGroupId++;
        List<EnemyMove> sp = new List<EnemyMove>();
        sp.Add(new EnemyMove("투창", GameBalance.GoblinJavelin, 1, 0) { PlayerFrail = GameBalance.GoblinJavelinFrail });
        sp.Add(new EnemyMove("투창", GameBalance.GoblinJavelin, 1, 0));
        List<EnemyMove> ch = new List<EnemyMove>();
        ch.Add(new EnemyMove("약탈의 함성", 0, 0, 0) { AllyStrength = GameBalance.GoblinChiefRally });
        ch.Add(new EnemyMove("칼질", GameBalance.GoblinChiefSlash, 1, 0));
        ch.Add(new EnemyMove("칼질", GameBalance.GoblinChiefSlash, 1, 0));
        List<EnemyMove> nt = new List<EnemyMove>();
        nt.Add(new EnemyMove("그물 던지기", GameBalance.GoblinNet, 1, 0) { PlayerDrawDown = 1 });
        nt.Add(new EnemyMove("찌르기", GameBalance.GoblinStab, 1, 0));
        List<EnemyCombatant> list = new List<EnemyCombatant>();
        list.Add(Tagged(new EnemyCombatant("고블린 투창병", GameBalance.GoblinSpearHp, sp, EnemyRank.Veteran), "", g));
        list.Add(Tagged(new EnemyCombatant("고블린 대장", GameBalance.GoblinChiefHp, ch, EnemyRank.Veteran), "", g));
        list.Add(Tagged(new EnemyCombatant("고블린 그물병", GameBalance.GoblinNetHp, nt, EnemyRank.Veteran), "", g));
        return list;
    }

    // ---- 엘리트 ----
    public static EnemyCombatant StoneSentinel()
    {
        List<EnemyMove> p = new List<EnemyMove>();
        p.Add(new EnemyMove("돌주먹", GameBalance.SentinelFist, 1, 0));
        p.Add(new EnemyMove("석화 방어", GameBalance.SentinelGuardHit, 1, GameBalance.SentinelGuardBlock));
        p.Add(new EnemyMove("대지 강타", GameBalance.SentinelSlam, GameBalance.SentinelSlamHits, 0));
        return Tagged(new EnemyCombatant("고대 석상 파수꾼", GameBalance.SentinelHp, p, EnemyRank.Elite), T_Sentinel);
    }

    public static EnemyCombatant SoulWitch()
    {
        List<EnemyMove> p = new List<EnemyMove>();
        p.Add(new EnemyMove("저주 화살", GameBalance.WitchArrow, 1, 0) { PlayerWeak = GameBalance.EnemyWeakTurns });
        p.Add(new EnemyMove("영혼 흡수", GameBalance.WitchDrain, GameBalance.WitchDrainHits, 0));
        p.Add(new EnemyMove("어둠 장막", 0, 0, GameBalance.WitchVeilBlock));
        return Tagged(new EnemyCombatant("영혼 결속 마녀", GameBalance.WitchHp, p, EnemyRank.Elite), T_Witch);
    }

    // 사냥개 조련사 & 맹수: 조련사를 먼저 잡으면 맹수 광폭(힘 +5), 맹수를 먼저 잡으면 조련사 공포(약화·취약)
    public static List<EnemyCombatant> HoundPair()
    {
        int g = nextGroupId++;
        List<EnemyMove> h = new List<EnemyMove>();
        h.Add(new EnemyMove("채찍", GameBalance.HandlerWhip, 1, 0));
        h.Add(new EnemyMove("사냥 지휘", 0, 0, 0) { AllyStrength = GameBalance.HandlerCommand });
        h.Add(new EnemyMove("덫 놓기", GameBalance.HandlerTrap, 1, 0) { PlayerWeak = GameBalance.EnemyWeakTurns });
        List<EnemyMove> b = new List<EnemyMove>();
        b.Add(new EnemyMove("물어뜯기", GameBalance.BeastBite, 1, 0));
        b.Add(new EnemyMove("할퀴기", GameBalance.BeastClaw, GameBalance.BeastClawHits, 0));
        List<EnemyCombatant> list = new List<EnemyCombatant>();
        list.Add(Tagged(new EnemyCombatant("사냥개 조련사", GameBalance.HandlerHp, h, EnemyRank.Elite), T_Handler, g));
        list.Add(Tagged(new EnemyCombatant("굶주린 맹수", GameBalance.BeastHp, b, EnemyRank.Elite), T_Beast, g));
        return list;
    }

    // ---- 보스 ----
    // 심연의 거대 껍질: 외골격(받는 피해 -50%)을 깨면 기절 → 광폭화(2페이즈)
    public static EnemyCombatant AbyssShell()
    {
        List<EnemyMove> p1 = new List<EnemyMove>();
        p1.Add(new EnemyMove("집게 내려찍기", GameBalance.ShellClaw, 1, 0));
        p1.Add(new EnemyMove("껍질 웅크리기", 0, 0, GameBalance.ShellCurlBlock));
        p1.Add(new EnemyMove("산성 분사", GameBalance.ShellAcid, 1, 0) { PlayerFrail = GameBalance.ShellAcidFrail });
        List<EnemyMove> p2 = new List<EnemyMove>();
        p2.Add(new EnemyMove("광폭 집게", GameBalance.ShellRageClaw, GameBalance.ShellRageClawHits, 0));
        p2.Add(new EnemyMove("심연의 포효", 0, 0, 0, GameBalance.EnemyVulnerableTurns) { Strength = GameBalance.ShellRoarStrength });
        p2.Add(new EnemyMove("껍질 파편", GameBalance.ShellShard, 1, 0));
        EnemyCombatant boss = MakeBoss("심연의 거대 껍질", GameBalance.ShellBossHp, p1, p2, 0, 0f);   // 비율 0 = HP 로는 강화 안 됨 (외골격이 깨질 때 강화)
        return Tagged(boss, T_Shell);
    }

    // 잊혀진 왕의 망령 + 해골 근위병 2: 근위병은 왕이 살아 있는 동안 적 차례 2번 뒤 부활
    public static List<EnemyCombatant> ForgottenKing()
    {
        int g = nextGroupId++;
        List<EnemyMove> p1 = new List<EnemyMove>();
        p1.Add(new EnemyMove("망령의 검", GameBalance.KingStrike, 1, 0));
        p1.Add(new EnemyMove("저주의 왕관", 0, 0, 0, GameBalance.EnemyVulnerableTurns) { PlayerWeak = GameBalance.EnemyWeakTurns });
        p1.Add(new EnemyMove("영혼 수확", GameBalance.KingHarvest, GameBalance.KingHarvestHits, 0) { Heal = GameBalance.KingHarvestHeal });
        List<EnemyMove> p2 = new List<EnemyMove>();
        p2.Add(new EnemyMove("왕의 분노", GameBalance.KingStrike + 5, 1, 0));
        p2.Add(new EnemyMove("영혼 수확", GameBalance.KingHarvest, GameBalance.KingHarvestHits + 1, 0) { Heal = GameBalance.KingHarvestHeal });
        p2.Add(new EnemyMove("저주의 왕관", 0, 0, 10, GameBalance.EnemyVulnerableTurns) { PlayerWeak = GameBalance.EnemyWeakTurns });
        EnemyCombatant king = Tagged(MakeBoss("잊혀진 왕의 망령", GameBalance.KingHp, p1, p2, 15, GameBalance.BossEnrageRatio), T_King, g);
        List<EnemyCombatant> list = new List<EnemyCombatant>();
        for (int i = 0; i < 2; i++)
        {
            List<EnemyMove> gp = new List<EnemyMove>();
            gp.Add(new EnemyMove("창 찌르기", GameBalance.GuardSpear, 1, 0));
            gp.Add(new EnemyMove("호위 대형", 0, 0, 0) { AllyBlock = GameBalance.GuardAllyBlock });
            EnemyCombatant guard = Tagged(new EnemyCombatant("해골 근위병", GameBalance.KingGuardHp, gp, EnemyRank.Normal), T_KingGuard, g);
            guard.MoveIndex = i;   // 둘이 번갈아
            list.Add(guard);
        }
        list.Insert(1, king);   // 근위병 - 왕 - 근위병
        return list;
    }

    // 차원 균열의 감시자: 2턴마다 손패 1장 소멸
    public static EnemyCombatant RiftWatcher()
    {
        List<EnemyMove> p1 = new List<EnemyMove>();
        p1.Add(new EnemyMove("균열 광선", GameBalance.RiftBeam, 1, 0));
        p1.Add(new EnemyMove("차원 왜곡", 0, 0, 0, GameBalance.EnemyVulnerableTurns) { PlayerWeak = GameBalance.EnemyWeakTurns });
        p1.Add(new EnemyMove("공허 응시", GameBalance.RiftGazeHit, GameBalance.RiftGazeHits, 0));
        List<EnemyMove> p2 = new List<EnemyMove>();
        p2.Add(new EnemyMove("균열 폭풍", GameBalance.RiftStormHit, GameBalance.RiftStormHits, 0));
        p2.Add(new EnemyMove("공허의 문", 0, 0, GameBalance.RiftGateBlock) { Strength = GameBalance.RiftGateStrength });
        p2.Add(new EnemyMove("붕괴 광선", GameBalance.RiftCollapse, 1, 0));
        return Tagged(MakeBoss("차원 균열의 감시자", GameBalance.RiftBossHp, p1, p2, 20, GameBalance.BossEnrageRatio), T_Rift);
    }

    // 심판의 쌍둥이 석상: 태양(공격·화상) + 달(방어·약화). 한쪽이 쓰러지면 적 차례 2번 뒤 HP 50% 로 부활
    public static List<EnemyCombatant> TwinStatues()
    {
        int g = nextGroupId++;
        List<EnemyMove> sun = new List<EnemyMove>();
        sun.Add(new EnemyMove("태양 광선", GameBalance.SunBeam, 1, 0) { AddBurnCards = GameBalance.SunBurnCards });
        sun.Add(new EnemyMove("작열", GameBalance.SunScorch, 1, 0) { PlayerBurn = GameBalance.SunScorchBurn });
        List<EnemyMove> moon = new List<EnemyMove>();
        moon.Add(new EnemyMove("달빛 장막", 0, 0, GameBalance.MoonVeilBlock) { AllyBlock = GameBalance.MoonVeilAlly });
        moon.Add(new EnemyMove("월광 저주", GameBalance.MoonCurse, 1, 0) { PlayerWeak = GameBalance.EnemyWeakTurns, AddVoidCards = GameBalance.MoonVoidCards });   // 3-149: + [공허] 카드
        List<EnemyCombatant> list = new List<EnemyCombatant>();
        list.Add(Tagged(new EnemyCombatant("태양의 석상", GameBalance.TwinStatueHp, sun, EnemyRank.Boss), T_Twin, g));
        list.Add(Tagged(new EnemyCombatant("달의 석상", GameBalance.TwinStatueHp, moon, EnemyRank.Boss), T_Twin, g));
        return list;
    }

    // ---- 등급별 뽑기 (무리는 List 하나로) ----
    // room = 이 웨이브에 더 들어갈 수 있는 몬스터 수. 무리가 안 들어가면 1마리짜리로 다시 뽑음
    static List<EnemyCombatant> One(EnemyCombatant e) { return new List<EnemyCombatant> { e }; }

    public static List<EnemyCombatant> PickNormal(Race race, System.Random rng, int room)
    {
        for (int tries = 0; tries < 20; tries++)
        {
            List<EnemyCombatant> pick;
            switch (rng.Next(12))   // 3-141: 점액 유충 뺌 (13 → 12). 유충 코드·그림은 남겨 둠 (다시 켜려면 case 하나 추가)
            {
                case 0: pick = One(RiftSlime()); break;
                case 1: pick = One(ChainBat()); break;
                case 2: pick = One(PlagueRat()); break;
                case 3: pick = One(EmberImp()); break;
                case 4: pick = One(WebSpider()); break;
                case 5: pick = One(Cultist()); break;
                case 6: pick = One(HornBeetle()); break;
                case 7: pick = One(Sporeling()); break;
                case 8: pick = One(ClaySplitter()); break;
                case 9: pick = One(LanternKeeper()); break;
                case 10: pick = EmberSpirits(); break;
                // case 11: pick = LarvaSwarm(); break;   // 3-141: 점액 유충 (일단 안 나옴)
                default: pick = Apprentices(); break;
            }
            if (pick.Count <= room) return pick;
        }
        return One(RiftSlime());
    }

    public static List<EnemyCombatant> PickVeteran(Race race, System.Random rng, int room)
    {
        for (int tries = 0; tries < 20; tries++)
        {
            List<EnemyCombatant> pick;
            switch (rng.Next(7))
            {
                case 0: pick = One(CorruptedSoldier()); break;
                case 1: pick = One(IronGolem()); break;
                case 2: pick = One(OrcSlinger()); break;
                case 3: pick = One(ThornKnight()); break;
                case 4: pick = One(BoneCollector()); break;
                case 5: pick = SkeletonPair(); break;
                default: pick = GoblinRaiders(); break;
            }
            if (pick.Count <= room) return pick;
        }
        return One(IronGolem());
    }

    public static List<EnemyCombatant> PickElite(Race race, System.Random rng, int room)
    {
        for (int tries = 0; tries < 20; tries++)
        {
            List<EnemyCombatant> pick;
            switch (rng.Next(6))
            {
                case 0: pick = One(Watcher()); break;
                case 1: pick = One(PlagueShaman()); break;
                case 2: pick = One(ShadowAssassin()); break;
                case 3: pick = One(StoneSentinel()); break;
                case 4: pick = One(SoulWitch()); break;
                default: pick = HoundPair(); break;
            }
            if (pick.Count <= room) return pick;
        }
        return One(Watcher());
    }

    // 스테이지 보스 (3-167: 스테이지마다 정해진 보스만)
    //   1스테이지: 종족 수문장 (광폭한 / 철퇴 / 부패한)
    //   2스테이지: bossKind 0 심연의 거대 껍질 / 1 잊혀진 왕의 망령 / 2 차원 균열의 감시자
    //   3스테이지: bossKind 0 요새의 수문장 (종족 수문장 + "요새의" 이름, RunState 가 붙임) / 1 심판의 쌍둥이 석상 (태양·달)
    //   4스테이지는 설계자 (여기 말고 따로)
    //   자바로 치면 stage 로 switch 한 뒤 그 안에서 bossKind 로 또 switch
    public static List<EnemyCombatant> PickStageBoss(Race race, System.Random rng, int bossKind, int stage)
    {
        if (stage == 2)
        {
            if (bossKind == 1) return ForgottenKing();
            if (bossKind == 2) return One(RiftWatcher());
            return One(AbyssShell());
        }
        if (stage == 3 && bossKind == 1) return TwinStatues();
        return One(CreateStageBoss(race, rng));
    }

    public static string StageBossHint(Race race, int bossKind, int stage)
    {
        // 3-167: PickStageBoss 와 같은 짝으로
        if (stage == 2 && bossKind == 2)
            return "성벽 너머 갈라진 균열에서 차원 균열의 감시자가 깨어났습니다.\n"
                 + "두 턴마다 우리 손에 든 카드 한 장을 허공으로 지워 버립니다.\n"
                 + "덱이 바닥나기 전에 끝내세요. 체력이 절반이 되면 균열이 폭주합니다.";
        if (stage == 3 && bossKind == 1)
            return "요새 문 앞에 심판의 쌍둥이 석상이 서 있습니다.\n"
                 + "태양의 석상은 불태우고, 달의 석상은 지키고 약하게 만듭니다.\n"
                 + "한쪽만 부수면 두 턴 뒤 다시 일어납니다. 둘을 함께 무너뜨리세요.";
        if (stage == 2 && bossKind == 0)
            return "이번 침공의 우두머리는 심연의 거대 껍질입니다.\n"
                 + "단단한 외골격이 받는 피해를 절반으로 줄입니다.\n"
                 + "외골격을 깨면 잠시 기절하지만, 그 뒤로는 미쳐 날뜁니다.";
        if (stage == 2 && bossKind == 1)
            return "이번 침공의 우두머리는 잊혀진 왕의 망령입니다.\n"
                 + "해골 근위병 둘이 곁을 지키고, 쓰러져도 두 턴 뒤 강령술로 일어납니다.\n"
                 + "근위병에 매달리지 말고 왕을 노리세요.";
        return StageBossHint(race);
    }

    // 웨이브 구성표 한 줄을 실제 몬스터로 만듦
    //   plan 예) { "N2", "V1" } → 1웨이브: 일반 2마리, 2웨이브: 정예 1마리
    //   돌려주는 값: 웨이브 목록 (웨이브 하나 = 몬스터 목록)
    // 자바로 치면 List<List<EnemyCombatant>> = ArrayList<ArrayList<...>>
    // 3-102: 무리 몬스터가 생겨서 N/V/E 슬롯 하나가 여러 마리일 수 있음 (한 웨이브 최대 MaxWaveEnemies 마리)
    // 3-106: 무리는 그 웨이브에 슬롯이 하나뿐일 때만 (예: "N1" → 유충 4마리 가능, "N1+E1" → 1마리짜리만)
    //   bossKind / stage = 스테이지 보스 종류 (RunState.StageBosses)
    public static List<List<EnemyCombatant>> BuildWaves(string[] plan, Race race, System.Random rng, int bossKind = 0, int stage = 1)
    {
        List<List<EnemyCombatant>> waves = new List<List<EnemyCombatant>>();
        int squadId = 1;
        foreach (string slot in plan)
        {
            List<EnemyCombatant> wave = new List<EnemyCombatant>();
            // "N1+V1" = 한 웨이브에 여러 종류 (2026-11-15)
            string[] parts = slot.Split('+');
            int slotsLeft = 0;
            foreach (string part in parts) slotsLeft += (part.Length > 1) ? int.Parse(part.Substring(1)) : 1;
            int totalSlots = slotsLeft;
            foreach (string part in parts)
            {
            char kind = part[0];
            int count = (part.Length > 1) ? int.Parse(part.Substring(1)) : 1;
            for (int i = 0; i < count; i++)
            {
                slotsLeft--;
                // 3-106: 무리 몬스터(2~4마리)는 "혼자 나오는 칸"(N1, V1, E1)에서만. "N2", "N1+E1" 처럼 다른 몬스터와
                //   같이 나오는 칸은 1마리짜리만 → 1스테이지 "한 번에 최대 2마리" 규칙이 지켜짐
                int room = (totalSlots == 1) ? GameBalance.MaxWaveEnemies : 1;
                switch (kind)
                {
                    case 'N': wave.AddRange(PickNormal(race, rng, room)); break;
                    case 'V': wave.AddRange(PickVeteran(race, rng, room)); break;
                    case 'E': wave.AddRange(PickElite(race, rng, room)); break;
                    case 'T': wave.AddRange(WatcherSquad(squadId++)); break;
                    case 'B': wave.AddRange(PickStageBoss(race, rng, bossKind, stage)); break;
                    case 'A': wave.Add(Architect()); break;
                    default: wave.Add(RiftSlime()); break;
                }
            }
            }
            waves.Add(wave);
        }
        return waves;
    }

    // 웨이브 구성표 한 줄을 읽기 쉬운 글자로. 예) { "N2", "V1" } → "일반 2 → 정예 1"
    public static string DescribePlan(string[] plan)
    {
        List<string> parts = new List<string>();
        foreach (string slot in plan)
        {
            List<string> group = new List<string>();
            foreach (string part in slot.Split('+'))
            {
                string count = part.Length > 1 ? part.Substring(1) : "1";
                switch (part[0])
                {
                    case 'N': group.Add("일반 " + count); break;
                    case 'V': group.Add("정예 " + count); break;
                    case 'E': group.Add("엘리트 " + count); break;
                    case 'T': group.Add("감시자 무리"); break;
                    case 'B': group.Add("보스"); break;
                    case 'A': group.Add("설계자"); break;
                }
            }
            parts.Add(string.Join(" + ", group));
        }
        return string.Join(" → ", parts);
    }
}
