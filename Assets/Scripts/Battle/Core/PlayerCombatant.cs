// ============================================================
// PlayerCombatant.cs
// 플레이어 한 명의 전투 상태: 스탯, 에너지, 마나, 카드 더미(뽑을/손패/버린/소멸)
//
// 협동 게임이므로 BattleSystem 은 이 클래스를 "목록(List)" 으로 여러 개 가집니다.
//
// 스탯 (2026-11-08 개편, 최대 20)
//   힘(STR)  : 공격 피해 +1 / 1pt (AD·AP 상관없이)
//   체력(CON): 최대 HP  AD 직업 +4 / 1pt,  AP 직업 +2 / 1pt
//   민첩(AGI): AD 직업 = 크리티컬 + 방어도 관통
//              AP 직업 = 시작 마나·턴당 마나 회복 (민첩은 크리티컬·관통에 영향 없음)
//              스나이퍼 = 최대 탄환·턴당 충전만 (3-80: 크리티컬·관통 없음, GameBalance.SniperAgiCritPen)
// ============================================================

using System.Collections.Generic;

public class PlayerCombatant : Combatant
{
    public int Str;   // 힘
    public int Con;   // 체력
    public int Agi;   // 민첩

    public PlayerClass Class;         // 어떤 클래스인지 (ClassLibrary 가 넣어 줌)
    public bool IsAP;                 // true = 마법 직업 (마도사). 체력·민첩 효과가 다름

    // 화면에 보여 줄 3번째 스탯 이름. 값은 같은 Agi 지만 AP 직업(마도사)은 "마나", AD 직업은 "민첩"
    //   (자바로 치면 getter 메서드: public String getAgiName() { return isAP ? "마나" : "민첩"; })
    public string AgiName { get { return IsAP ? "마나" : "민첩"; } }

    // 대장간 무기 강화 단계 (0 ~ GameBalance.WeaponMaxLevel). 런 동안 계속 유지
    public int WeaponBlade;   // 날: 모든 공격 피해 +
    public int WeaponHilt;    // 자루: 최대 HP +, 전투 시작 방어도 +
    public int WeaponCore;    // 핵: 최대 마나 +, 3단계에서 턴 시작 마나 회복

    public int Energy;
    public int Mana;
    public int Gold;                   // 골드 (런 동안 유지). 상점이 생기면 씀

    // 아티팩트 (런 동안 유지). 효과는 BattleSystem·RunState 가 HasRelic("아이디") 로 확인
    public List<RelicData> Relics = new List<RelicData>();
    public List<string> Potions = new List<string>();   // 포션 ID (PotionLibrary). 최대 GameBalance.PotionSlots 개
    public int PotionPowerBonus;                         // 강화 포션: 이번 전투 공격 피해 + (전투 시작 때 0)

    // 레벨 (2026-11-15)
    public int Level = GameBalance.StartLevel;   // 3-164: 0 에서 시작 (첫 전투 승리 = 1레벨)
    public int Exp;            // 지금 레벨에서 모은 경험치
    public int StatPoints;     // 아직 안 쓴 스탯 포인트 (레벨 업 화면에서 씀)
    // 3-196: 난이도 배율 (쉬움 0.85 / 어려움~ 1.15). RunState.Difficulty 를 정할 때 같이 넣음. 반올림, 최소 1
    public float ExpScale = 1f;
    public int ExpToNext { get { return Level >= GameBalance.MaxLevel ? 0 : System.Math.Max(1, DamageCalculator.RoundHalfUp(GameBalance.ExpToNext[Level] * ExpScale)); } }   // 3-164: 레벨 0 부터라 [Level]
    // 카드 보상 보정 장치: 일반 카드가 나올 때마다 +1 (영웅 확률 +1%p), 영웅·전설이 나오면 0
    public int CardPity;
    public int BaseMaxMana = GameBalance.BaseMaxMana; // 클래스마다 다름 (영혼 20, 홍염 10, 마나 30)
    public float BaseCrit = GameBalance.CritBase;
    public string ManaName = "마나";   // 검은 낫: 영혼, 피닉스: 홍염, 마도사: 마나
    public bool BasicCardsGiveMana = true;

    public bool HasEndedTurn;          // 이번 턴에 "턴 종료" 를 눌렀는지
    public bool DevInvincible;         // (개발 도구) 무적: 적 공격 피해 0

    // ---- 이번 턴 기록 (턴 시작에 0) ----
    public int CardsPlayedThisTurn;    // 이번 턴에 쓴 카드 수 (마나 순환)
    public int AttacksPlayedThisTurn;  // 이번 턴에 쓴 공격 카드 수 (그림자 습격)
    public int LightningPlayedThisTurn;// 이번 턴에 쓴 [벼락] 카드 수 (천둥 벼락)
    public int NextSkillDiscount;      // 이번 턴 다음 스킬 카드 N장 에너지 -1 (열기 응축, 마나 실드). 장수로 쌓임
    public bool NextLightningFree;     // 이번 턴 다음 [벼락] 카드 마나 0 (이온화, 낙뢰 제외)
    public bool PendingSealPick;       // 영혼 봉인: 봉인할 손패를 고르는 중
    public int PendingDiscardPick;     // 전술 후퇴: 버릴 손패를 고르는 중 (고르면 탄환 +이 값). 0 이면 고르는 중 아님

    // ---- 검은 낫 ----
    public EnemyCombatant MarkedEnemy; // 죽음의 인장: 내 공격 2배 (한 명만)
    // 3-172: 그림자 습격의 [밤] 남은 턴 (이번 턴 포함). 내 턴 시작마다 1씩 줄고 0 이 되면 밤 끝
    //   예전 bool NightThisTurn(이번 턴만) → int 로 바꿈. 자바로 치면 boolean 플래그를 int 카운터로 바꾼 것
    public int NightTurns;
    public bool NightThisTurn => NightTurns > 0;   // 예전 이름 그대로 읽을 수 있게 (읽기 전용 속성 = 자바의 getter)
    public bool NightForever;          // 죽음의 밤: 이번 전투 동안 [밤]
    public bool IsNight => NightThisTurn || NightForever;
    public List<int> PendingHalfMoons = new List<int>();   // 반월참: 차징 중인 반월참의 피해 (강화판은 28). 다음 내 턴 시작에 발동
    public int PendingHalfMoon => PendingHalfMoons.Count; // 차징 중인 장수 (화면용)
    public bool MarkUpgraded;          // 죽음의 인장+ 로 걸었는지 (처치 보상이 커짐)
    public bool EclipseActive;         // 월식: 이번 턴 반월참 에너지 0 + 즉시 발동
    public bool EclipseUsed;           // 월식: 전투당 1번
    public bool RetainBlockOnce;       // 칠흑의 망토(밤): 다음 내 턴 시작에 방어도가 사라지지 않음
    public CardData SealedCard;        // 영혼 봉인: 다음 턴 시작에 손으로 돌아올 카드
    public int ReaperStepsStacks;      // 사신의 발걸음: 매 턴 시작 무작위 적 1명에게 처형 스택 (장마다 3, 강화 5 를 더한 값)
    public int FullMoonStacks;         // 만월 장수: 공격 카드가 피해를 줄 때마다 처형 스택 +2 × 장수
    public int FullMoonBonus;          // 만월 피해 보너스 합계 (1, 강화 2)
    public int RedMoonStacks;          // 적월 장수 (화면용)
    public int RedMoonBonus;           // 적월: HP 60% 이하면 공격 카드 피해 + 이 값 (2, 강화 4 를 더한 값)
    public int SoulExploitStacks;      // 영혼 착취 장수
    public int SoulExploitPer;         // 영혼 착취: 처형 스택 몇 개당 피해 2 (3, 강화 2). 0 = 없음
    public bool WasNight;              // 지난번에 확인했을 때 [밤]이었는지 (부서진 초승달 펜던트: 밤이 "켜질 때")

    // ---- 피닉스 ----
    public bool ReflectBurn;           // 피닉스의 방패: 이번 적 차례에 나를 때린 적에게 발화 2
    public bool FlameWall;             // 화염 장벽: 이번 적 차례에 아군 피해 -40%, 나는 1.5배
    public bool FlameWallNoPenalty;    // 화염 장벽+: 나는 1.5배 없음
    public int GuardStanceEnergy;      // 가드 스탠스: 다음 턴 시작 에너지 +
    // ---- 스나이퍼 (2026-11-15) ----
    public int AimStacks;              // [조준] 개수: 다음 공격 카드 피해 +50% × 개수
    public bool StaticRingUsed;        // 정전기의 반지 (3-101): 이번 턴에 이미 발동했는지
    // ---- 3-176 새 아티팩트용 (턴·전투마다 다시 셈) ----
    public int RapidMagLeft;           // 속사 탄창: 이번 턴 탄환 소모 0 남은 장수
    public bool FrostStaffUsed;        // 서리 결정 지팡이: 이번 턴 첫 마나 카드 무료를 썼는지
    public int CrystalOrbProcs;        // 서리 마력의 수정구: 이번 턴 발동 횟수
    public int FrostRingBlockThisTurn; // 서리 결정 반지: 이번 턴 얻은 방어도
    public int FlagDrawNext;           // 작전 지휘관의 깃발: 다음 턴 카드 +N
    public int PurifyTimerUsed;        // 희석된 정화 시계: 이번 전투 사용 횟수
    public bool GlovesUsed;            // 전술 자율 장갑: 이번 전투에 [사격] 에너지 0 을 이미 썼는지
    public bool AimedThisCard;         // 지금 쓰는 공격 카드가 조준을 받았는지 (헤드샷 2배 판정)
    public int ExecutionerGaze;        // 처형인의 시선: 표식 있는 적에게 공격 피해 +
    public int AmmoSupply;             // 탄약보급: 매 턴 탄환 +2 × 장수
    public int CrimsonMag;             // 진홍의 탄창: 공격 2장마다 탄환·드로우
    public int CrimsonCount;
    public int WeakAnalysis;           // 약점 분석: 표식을 걸 때마다 방어도·탄환
    public bool FreeBulletsTurn;       // 오버드라이브: 이번 턴 탄환 소모 0
    public float SmokeReduce;          // 연막탄: 다음 내 턴까지 받는 피해 감소 비율
    // ---- 공용 카드 파워 (2026-11-15) ----
    public int SteelWill;              // 강철 의지: 턴 종료 시 방어도
    public int Transcend;              // 초월: 턴 시작 시 무작위 손패 1장 이번 턴 에너지 0
    public int BattleInstinct;         // 전투 본능: 공격 카드로 처치 시 에너지 +1, 1장 뽑기
    public int ThornArmor;             // 가시 갑옷: 공격받을 때마다 공격자에게 피해
    public bool DoubleNextCard;        // 시간 왜곡: 다음 카드 2번 발동
    public int DrawPenalty;            // 몬스터 거미줄·연막 등: 다음 턴 시작 뽑기 - (2026-11-15)
    public int GuardStanceDraw;        // 가드 스탠스: 다음 턴 시작 뽑기 +
    public int PhoenixGuardStacks;     // 피닉스의 가호
    public float PhoenixGuardReduction;// 피닉스의 가호 피해 감소 (0.1, 강화 0.2)
    public int SunWrathBurn;           // 태양의 진노: 모든 적에게 거는 발화 (2, 강화 3 을 더한 값)
    public bool PhoenixHeartReady;     // 피닉스의 심장 (전투당 1회)
    public bool PhoenixHeartUsed;
    public float PhoenixHeartHeal;     // 피닉스의 심장 회복 비율 (0.2, 강화 0.4)
    public int EternalEmberStacks;     // 영원한 불씨
    public float EternalEmberRatio;    // 영원한 불씨 발동 HP 비율 (0.3, 강화 0.5)
    public int BurningThornsStacks;    // 작열하는 가시
    public bool ThornsUnlimited;       // 작열하는 가시+: 턴당 횟수 제한 없음
    public int ThornsThisTurn;         // 작열하는 가시: 이번 적 차례 발동 횟수 (최대 3)
    public bool UndyingFlame;          // 꺼지지 않는 불꽃: 적 발화가 줄지 않음
    public int UndyingFlameMax;        // 꺼지지 않는 불꽃: 턴 종료 홍염 최대 (2, 강화 4)
    public int FlameTrainingStacks;    // 화염 단련 (최대 2)

    // ---- 마도사 ----
    public bool ShockBarrier;          // 전류 방벽: 이번 적 차례에 나를 때린 적에게 감전 스택 1
    public float GlacierBarrier;       // 빙하 결계: 이번 적 차례에 받는 피해 감소 비율 (0.2, 강화 0.3). 0 = 없음
    public int FrostPrisonStacks;      // 혹한의 감옥
    public int FrostPrisonDrawsThisTurn;
    public int HailDamage;             // 우박: 적이 동상이 될 때마다 주는 피해 (10, 강화 15 를 더한 값)
    public bool ThunderGodActive;      // 뇌신 권능을 썼는지
    public int ThunderGodStacks;       // 뇌신 권능: [벼락] 카드를 쓴 횟수 (최대 4) → [벼락] 피해 +보너스 × 횟수
    public int ThunderGodBonus;        // 뇌신 권능: 1회당 피해 (2, 강화 3)
    public List<int> StormFieldHits = new List<int>();   // 뇌전 지대: 장마다 턴 종료 피해 (4, 강화 6)
    public int StormFieldStacks => StormFieldHits.Count;
    public int BlizzardStacks;         // 눈보라
    public bool BlizzardEveryTurn;     // 눈보라+: 2턴마다 → 매 턴
    public int BlizzardTurn;           // 눈보라: 2턴마다 세는 값
    public int ManaReleaseStacks;      // 마력해방
    public int StaticFieldBlock;       // 정전기 역장: 감전 스택을 걸 때마다 방어도 (3, 강화 5 를 더한 값)

    // 이번 전투에서 사용한 파워 카드 (파워는 한 번 쓰면 전투 끝까지 유지되고 덱으로 돌아가지 않음)
    public List<CardData> PowersInPlay = new List<CardData>();

    // 카드 더미 4개
    public List<CardData> DrawPile = new List<CardData>();     // 뽑을 카드 더미
    public List<CardData> Hand = new List<CardData>();         // 손패
    public List<CardData> DiscardPile = new List<CardData>();  // 버린 카드 더미
    public List<CardData> ExhaustPile = new List<CardData>();  // 소멸된 카드

    // 전투 시작 전 덱 (전투가 끝나도 남는 원본 목록)
    public List<CardData> Deck = new List<CardData>();

    // 생성자: 자바와 똑같이 클래스 이름과 같은 메서드
    // 3-147: 체력 1점당 최대 HP. 마도사·스나이퍼 +3 / 검은 낫·피닉스 +4
    //   자바로 치면 getter: public int getHpPerCon() { ... }
    public int HpPerCon
    {
        get
        {
            if (IsAP) return GameBalance.HpPerConAP;
            if (Class == PlayerClass.Sniper) return GameBalance.HpPerConSniper;
            return GameBalance.HpPerConAD;
        }
    }

    public PlayerCombatant(string name, int baseMaxHp, int str, int con, int agi, List<CardData> deck, bool isAP = false)
    {
        Name = name;
        Str = ClampStat(str);
        Con = ClampStat(con);
        Agi = ClampStat(agi);
        IsAP = isAP;

        MaxHp = baseMaxHp + Con * (isAP ? GameBalance.HpPerConAP : GameBalance.HpPerConAD);
        Hp = MaxHp;
        Mana = 0;
        Deck = new List<CardData>(deck);   // 받은 목록을 복사해서 보관
    }

    public static int ClampStat(int value)
    {
        if (value < 0) return 0;
        if (value > GameBalance.StatMax) return GameBalance.StatMax;
        return value;
    }

    // 최대 마나 = 클래스 기본값 (+ 유물·민첩 보정)
    public int MaxMana
    {
        get
        {
            int max = BaseMaxMana;   // 3-123: 예전 무기 [핵] 의 최대 마나 보너스는 없어짐
            if (Class == PlayerClass.Sniper)
            {
                int agiBonus = Agi / GameBalance.SniperAgiStep * GameBalance.SniperMaxPerStep;   // 스나이퍼: 민첩 5당 최대 탄환 +2
                if (HasRelic(RelicLibrary.RedWhisper)) agiBonus *= GameBalance.RedWhisperMult;    // 레드 위스퍼: 2배
                max += agiBonus;
            }
            if (HasRelic(RelicLibrary.ZeroShard)) max += GameBalance.ZeroShardMana;   // 절대 영도의 파편
            // 3-176: 최대 자원이 줄어드는 새 아티팩트 페널티
            if (HasRelic(RelicLibrary.SoulbondHaft)) max -= GameBalance.SoulbondHaftMaxLoss;
            if (HasRelic(RelicLibrary.HungryMask)) max -= GameBalance.HungryMaskMaxLoss;
            if (HasRelic(RelicLibrary.RapidMagazine)) max -= GameBalance.RapidMagMaxLoss;
            if (HasRelic(RelicLibrary.FrostShardRing)) max -= GameBalance.FrostShardMaxLoss;
            if (HasRelic(RelicLibrary.AbyssOrb)) max -= GameBalance.AbyssOrbMaxLoss;
            if (HasRelic(RelicLibrary.FrostCrystalOrb)) max -= GameBalance.CrystalOrbMaxLoss;
            if (HasRelic(RelicLibrary.GlacialCrown)) max = System.Math.Max(1, max - GameBalance.GlacialCrownManaLoss);   // 만년설의 왕관 페널티 (3-101)
            return System.Math.Max(1, max);   // 3-176: 페널티가 겹쳐도 최소 1
        }
    }

    public void GainMana(int amount)
    {
        Mana += amount;
        if (Mana > MaxMana) Mana = MaxMana;
        if (Mana < 0) Mana = 0;
    }

    // AP 직업 시작 마나: 민첩 0~3 → 2, 4~7 → 3, 8~11 → 4, 12~15 → 5, 16~19 → 6, 20 → 7
    //   공식: 2 + 민첩 / 4 (나눗셈은 소수점 버림. 자바의 int 나눗셈과 같음)
    public int StartMana()
    {
        if (Class == PlayerClass.Sniper) return GameBalance.SniperStartBullets;   // 스나이퍼: 탄환 3으로 시작
        if (!IsAP) return 0;
        return GameBalance.ApStartManaBase + Agi / GameBalance.ApStartManaStep;
    }

    // AP 직업 턴당 마나 회복: 민첩 0~4 → 1, 5~9 → 2, 10~14 → 3, 15~19 → 4, 20 → 5
    //   공식: 1 + 민첩 / 5
    public int ManaRegen()
    {
        if (Class == PlayerClass.Sniper) return Agi / GameBalance.SniperAgiStep * GameBalance.SniperRegenPerStep;   // 스나이퍼: 민첩 5당 매 턴 충전 +1
        if (!IsAP) return 0;
        return GameBalance.ApRegenBase + Agi / GameBalance.ApRegenStep;
    }

    // 크리티컬 확률 = 기본 크리티컬 + 민첩 × 2.5%, 최대 50%
    //   AP 직업은 민첩이 크리티컬에 영향을 주지 않음 → 기본 크리티컬만 (지금은 0%, 나중에 유물 등으로 올릴 수 있음)
    // 민첩이 크리티컬·관통을 주는 직업인지: 마도사(AP) 아님 + 스나이퍼 아님 (3-80)
    //   자바로 치면 boolean agiGivesCritPen() { return !isAP && (cls != SNIPER || SNIPER_AGI_CRIT_PEN); }
    public bool AgiGivesCritPen
    {
        get { return !IsAP && (Class != PlayerClass.Sniper || GameBalance.SniperAgiCritPen); }
    }

    public override float CritChance()
    {
        float fromAgi = AgiGivesCritPen ? Agi * GameBalance.CritPerAgi : 0f;
        float chance = BaseCrit + fromAgi;
        return System.Math.Min(chance, GameBalance.CritMax);
    }

    // 방어도 관통 비율 = 민첩 × 3%, 최대 60%. AD 직업만 (AP는 0)
    //   적 방어도의 이 비율만큼은 없는 것처럼 계산 (방어도를 깎는 게 아니라 "무시")
    public override float PenetrationRatio()
    {
        if (!AgiGivesCritPen) return 0f;   // 마도사·스나이퍼는 관통 없음 (3-80)
        float ratio = Agi * GameBalance.PenetrationPerAgi;
        return System.Math.Min(ratio, GameBalance.PenetrationMax);
    }

    // 화면 표시용: 지금 켜져 있는 효과를 짧게 (예: "밤, 반월참 차징 1, 만월 1")
    public string EffectText()
    {
        List<string> parts = new List<string>();
        // 밤·반월참 차징·방어도 유지·봉인은 화면에서 아이콘으로 보여 줌 (StatusIcons)
        if (EclipseActive) parts.Add("월식");
        if (PendingSealPick) parts.Add("봉인할 카드 고르기");
        if (PendingDiscardPick > 0) parts.Add("버릴 카드 고르기");
        if (ReaperStepsStacks > 0) parts.Add("발걸음 " + ReaperStepsStacks);
        if (FullMoonStacks > 0) parts.Add("만월 +" + FullMoonBonus);
        if (RedMoonStacks > 0) parts.Add("적월" + (Hp <= MaxHp * GameBalance.RedMoonHpRatio ? " +" + RedMoonBonus : ""));
        if (SoulExploitStacks > 0) parts.Add("영혼 착취");
        if (ReflectBurn) parts.Add("방패 반사");
        if (FlameWall) parts.Add("화염 장벽");
        if (PhoenixGuardStacks > 0) parts.Add("가호");
        if (SunWrathBurn > 0) parts.Add("태양의 진노");
        if (PhoenixHeartReady) parts.Add("심장");
        if (EternalEmberStacks > 0) parts.Add("영원한 불씨");
        if (BurningThornsStacks > 0) parts.Add("가시");
        if (UndyingFlame) parts.Add("꺼지지 않는 불꽃");
        if (FlameTrainingStacks > 0) parts.Add("단련 " + FlameTrainingStacks);
        if (ShockBarrier) parts.Add("전류 방벽");
        if (GlacierBarrier > 0f) parts.Add("빙하 결계");
        if (FrostPrisonStacks > 0) parts.Add("혹한의 감옥");
        if (HailDamage > 0) parts.Add("우박");
        if (ThunderGodActive) parts.Add("뇌신 +" + ThunderGodStacks * ThunderGodBonus);
        if (StormFieldStacks > 0) parts.Add("뇌전 지대");
        if (BlizzardStacks > 0) parts.Add("눈보라");
        if (ManaReleaseStacks > 0) parts.Add("마력해방");
        if (StaticFieldBlock > 0) parts.Add("정전기 역장");
        if (NextSkillDiscount > 0) parts.Add("다음 스킬 -1" + (NextSkillDiscount > 1 ? " x" + NextSkillDiscount : ""));
        if (NextLightningFree) parts.Add("다음 [벼락] 마나 0");
        return string.Join(", ", parts);
    }

    // 발화 최대치 (내가 거는 발화): 체력 0~9 → 15, 10~19 → 20, 20 → 30. 폭염의 코어가 있으면 +20
    public int BurnCap()
    {
        int cap;
        if (Con >= GameBalance.StatMax) cap = GameBalance.BurnCapMax;
        else if (Con >= GameBalance.BurnCapMidCon) cap = GameBalance.BurnCapMid;
        else cap = GameBalance.BurnCapBase;
        if (HasRelic(RelicLibrary.BlazingCore)) cap += GameBalance.BlazingCoreCap;
        return cap;
    }

    // ---------------- 아티팩트 ----------------
    // 이 아티팩트를 가지고 있는지 (아이디로 찾음). 자바의 list.stream().anyMatch(...) 와 같은 일
    public bool HasRelic(string id)
    {
        foreach (RelicData r in Relics) if (r.Id == id) return true;
        return false;
    }

    // 턴 시작 에너지: 3 + 보스 아티팩트(+1). 가드 스탠스·마력해방 같은 카드 효과는 BattleSystem 이 따로 더함
    public int BaseEnergy()
    {
        int energy = GameBalance.EnergyPerTurn;
        // 3-176: "매 턴 에너지 +1" 이 적힌 보스 아티팩트만 (GivesEnergy). '보스 승격' 아티팩트는 에너지 없음
        foreach (RelicData r in Relics) if (r.Rarity == RelicRarity.Boss && r.GivesEnergy) energy += GameBalance.BossRelicEnergy;
        return energy;
    }
}
