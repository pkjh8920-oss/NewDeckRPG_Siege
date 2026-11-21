// ============================================================
// GameBalance.cs
// 게임의 "숫자"를 전부 모아 두는 파일입니다.
// 직접 플레이해 보고 밸런스를 바꾸고 싶으면 이 파일만 고치면 됩니다.
//
// const = 바뀌지 않는 상수 (자바의 static final 과 같음)
// static class = 객체를 만들지 않고 GameBalance.BaseMaxHp 처럼 바로 꺼내 쓰는 클래스
// ============================================================

public static class GameBalance
{
    // ---------------- 스탯 (2026-11-08 개편) ----------------
    // 스탯은 3개: 힘(STR) / 체력(CON) / 민첩(AGI). 한 스탯 최대 20
    //   힘   = 공격 피해 +1 / 1pt (AD·AP 상관없이)
    //   체력 = 최대 HP: AD 직업 +4 / 1pt, AP 직업 +2 / 1pt  (+ 피닉스 카드 일부, 발화 최대치)
    //   민첩 = AD: 크리티컬 + 방어도 관통 / AP: 시작 마나·턴당 마나 회복 (민첩이 크리티컬에 영향 없음, 기본 크리티컬만)
    public const int StatMax = 20;

    // ---------------- 플레이어 기본 ----------------
    public const int BaseMaxHp = 80;          // 기본 최대 HP (공통 덱 테스트용)
    public const int HpPerConAD = 4;          // AD 직업: 체력 1pt당 최대 HP +4
    public const int HpPerConAP = 3;          // AP 직업(마도사): 체력 1pt당 최대 HP +3 (3-146: 2 → 3)
    public const int HpPerConSniper = 3;      // 3-147: 스나이퍼도 체력 1pt당 +3 (AD 지만 원거리라 몸이 약한 편)
    public const int EnergyPerTurn = 3;       // 턴당 에너지(코스트)
    public const int HandSize = 5;            // 턴 시작 시 뽑는 카드 수
    public const int MaxHandSize = 10;        // 손패 최대 장수 (넘치면 버린 카드 더미로)

    // ---------------- 마나 ----------------
    public const int BaseMaxMana = 10;        // 공통 덱 테스트용 최대 마나
    public const int ManaGainBasicCard = 1;   // 기본 카드(타격/방어) 사용 시 영혼·홍염 +1
    // AP 직업(마도사): 3번째 스탯(Agi)을 화면에서 "마나"라고 부름 (AD 직업은 "민첩")
    //   시작 마나 = 2 + 민첩 / 4   (0~3 → 2, 4~7 → 3, 8~11 → 4, 12~15 → 5, 16~19 → 6, 20 → 7)
    //   턴당 회복 = 1 + 민첩 / 5   (0~4 → 1, 5~9 → 2, 10~14 → 3, 15~19 → 4, 20 → 5)
    //   첫 턴에는 시작 마나 + 회복을 한 번에 받음 (민첩 10이면 첫 턴 4 + 3 = 7, 그 뒤 매 턴 +3)
    public const int ApStartManaBase = 2;
    public const int ApStartManaStep = 4;
    public const int ApRegenBase = 1;
    public const int ApRegenStep = 5;

    // ---------------- 스탯 → 피해 ----------------
    public const int AdPerStr = 1;            // 힘 StrStep 점마다 공격 피해 +1 (AD·AP 모두)
    public const int StrStep = 2;             // 3-160: 힘 2점당 공격 피해 +1 (3-158 에서 1점당으로 바꿨다가 3-133 모의 계산대로 되돌림)
                                              //   힘 20(최대) = 공격 한 대마다 +10

    // ---------------- 크리티컬 / 방어도 관통 ----------------
    public const float CritBase = 0f;         // 기본 크리티컬 확률 0%
    public const float CritPerAgi = 0.025f;   // 민첩 1pt당 +2.5% (20 = 50%)
    public const float CritMax = 0.50f;
    public const float CritBonus = 0.5f;      // 크리티컬 시 배수에 +0.5 (1.5배)
    // 관통 = 적 방어도의 일부를 "무시" (깎는 게 아님). AD 직업만. 민첩 1pt당 3%, 최대 60%
    //   예) 민첩 10 → 30%: 방어도 10인 적은 7만 막음
    public const float PenetrationPerAgi = 0.03f;
    public const float PenetrationMax = 0.60f;

    // ---------------- 상태 이상 ----------------
    public const float VulnerableBonus = 0.5f; // 취약: 받는 피해 배수에 +0.5
    public const float WeakReduction = 0.3f;   // 약화: 주는 피해 -30%
    public const float ShockReduction = 0.2f;  // 감전: 주는 피해 -20%
    public const int ShockStacksNeeded = 4;    // 감전 스택 4개 → 감전 상태
    public const int ShockTurns = 2;           // 감전 상태 2턴 (적 차례 2번)
    // 발화: 걸린 쪽 턴 시작에 스택만큼 피해(방어도 무시) → 스택 1 감소. 최대치는 건 사람의 체력 스탯
    public const int BurnCapBase = 15;         // 체력 0~9
    public const int BurnCapMidCon = 10;
    public const int BurnCapMid = 20;          // 체력 10~19
    public const int BurnCapMax = 30;          // 체력 20
    // 동결 → 동상: 동결 스택이 기준에 닿으면 이번 적 차례 행동 취소, 스택 0
    public const int FreezeNeedNormal = 3;
    public const int FreezeNeedVeteran = 4;    // 정예 (명세에 없어서 일반과 엘리트 사이로 정함)
    public const int FreezeNeedElite = 6;     // 2026-11-09: 5 → 6
    public const int FreezeNeedBoss = 7;
    public const int FreezeNeedGrowth = 1;     // 동상이 터질 때마다 기준 +1 (2026-11-09: 엘리트·보스만 → 모든 몬스터. 같은 적을 계속 묶지 못하게)
    // 처형 (검은 낫): 조건을 채우는 순간 처형
    //   일반·정예: HP < 스택 / 엘리트: 스택 > HP × 1.2 / 보스: 스택 > HP × 1.5
    public const float ExecuteRatioElite = 1.2f;
    public const float ExecuteRatioBoss = 1.5f;

    // ---------------- 공통 카드 ----------------
    public const int StrikeCost = 1;
    public const int StrikeDamage = 6;
    public const int DefendCost = 1;
    public const int DefendBlock = 5;

    // ================= 직업 기본 =================
    // 2026-11-15 기본 HP 변경: 검은 낫 60 → 85, 피닉스 100, 마도사 45 → 65, 스나이퍼 70 (체력 스탯 보너스는 따로 더해짐)
    public const int BlackScytheMaxHp = 85;
    public const int BlackScytheMaxSoul = 20;
    public const int PhoenixMaxHp = 100;
    public const int PhoenixMaxFlame = 10;         // 홍염 최대
    public const int FrostMageMaxHp = 65;
    public const int FrostMageMaxMana = 30;

    // ================= 스나이퍼 (2026-11-15, 팀원 기획서) =================
    // 자원 = 탄환. 시작 3 / 최대 6. 남은 탄환은 다음 턴으로 이어짐 (첫 턴은 시작 탄환 그대로)
    // 민첩 5 / 10 / 15 / 20 → 최대 탄환 8 / 10 / 12 / 14 (5당 +2), 매 턴 시작 충전 +1 / +2 / +3 / +4 (5당 +1)
    // [조준]: 다음 공격 카드 피해 +50% (조준 1개당, 겹치면 +100%, +150% ...). 공격 카드를 쓰면 사라짐
    // [표식]: 적에게 쌓임 (최대 10). 표식 1개당 그 적이 받는 공격 피해 +1 (모든 아군 공격). 전투 동안 유지
    public const int SniperMaxHp = 70;
    public const int SniperMaxBullets = 6;
    public const int SniperStartBullets = 3;
    public const int SniperAgiStep = 5;            // 민첩 5마다
    public const int SniperMaxPerStep = 2;         //   최대 탄환 +2
    public const int SniperRegenPerStep = 1;       //   매 턴 충전 +1
    public const bool SniperAgiCritPen = false;    // 스나이퍼 민첩이 크리티컬·방어도 관통도 주는지 (3-80: false = 탄환만). true 로 바꾸면 예전처럼
    public const float AimBonus = 0.5f;            // 조준 1개당 피해 +50%

    // ---- 스나이퍼 아티팩트 (2026-11-15) ----
    public const int CasingCharmAim = 1;                 // (시작) 진홍 탄피 부적: 전투 시작 시 [조준] +1
    public const int RustedBeltBullets = 2;              // (일반) 녹슨 군용 탄띠: 전투 시작 시 탄환 +2
    public const int BrokenScopeMark = 1;                // (일반) 광학 조준경 부품: [조준]을 얻을 때마다 대상(없으면 무작위 적) [표식] +1
    public const int CrossguardBlock = 2;                // (일반) 균형 잡힌 대검 코등이: [총검술] 카드마다 방어도 +2
    public const int GunpowderRingDamage = 6;            // (희귀) 화약 냄새 나는 반지: 탄환이 0이 될 때마다 무작위 적 6 피해
    public const float InfraredBonus = 0.2f;             // (희귀) 암시야 적외선 센서: [표식] 있는 적에게 공격 카드 피해 +20%
    public const int VeteranMagBulletsPerEnergy = 2;     // (희귀) 베테랑의 예비 탄창: 턴 종료 시 남은 탄환 2발당 다음 턴 에너지 +1
    public const int VeteranMagMaxEnergy = 2;            //   (턴당 최대 2)
    public const int RedWhisperMult = 2;                 // (보스) 레드 위스퍼: 민첩에 따른 최대 탄환 증가 2배 / 페널티: 매 턴 무작위 손패 1장 이번 턴 에너지 +1
    public const int RedWhisperCostUp = 1;               // 레드 위스퍼 페널티로 늘어나는 에너지 (낼 수 없는 카드는 안 고름. 3-72)
    public const int ObserverHpPerAim = 2;               // (보스) 사선의 관측경: 조준이 공격에 소모되지 않음 / 페널티: 턴 시작마다 조준 1개당 HP -2
    // [총검술] 카드 (2026-11-15, 대검 코등이와 함께 쓰라고 추가)
    public const int BayonetStabDmg = 7, BayonetStabDmgUp = 10, BayonetStabBullet = 1;                         // 총검 찌르기: 탄환 +1
    public const int ButtStrikeDmg = 6, ButtStrikeDmgUp = 9, ButtStrikeWeak = 1, ButtStrikeBlock = 4, ButtStrikeBlockUp = 6;   // 개머리판 강타
    public const int BayonetChargeDmg = 12, BayonetChargeDmgUp = 16, BayonetChargeMark = 2, BayonetChargeAim = 1;  // 총검 돌격: 표식 있던 적이면 조준 +1
    public const int MarkMax = 10;

    // 기본 카드
    public const int SnStrikeDmg = 6, SnStrikeDmgUp = 9, SnStrikeBullet = 1;   // 3-128: 5/8 → 6/9 (스나이퍼 초반 화력 보강)
    public const int SnDefendBlock = 5, SnDefendBlockUp = 8, SnDefendBullet = 1;
    // 공격 (강화 = 숫자 증가)
    public const int ShotDmg = 10, ShotDmgUp = 14,   /* 3-128: 8/11 → 10/14 */ ShotMark = 1, ShotMarkUp = 2, ShotBullet = 1;
    public const int HeadshotDmg = 16, HeadshotDmgUp = 22, HeadshotKillBullets = 6, HeadshotBullet = 3, HeadshotCost = 2;
    public const int IncendiaryDmg = 5, IncendiaryDmgUp = 8, IncendiaryWeak = 1, IncendiaryWeakUp = 2, IncendiaryMark = 1, IncendiaryBullet = 1;
    public const int APRoundDmg = 11, APRoundDmgUp = 16, APRoundBullet = 3;
    public const int SprayBase = 6, SprayBaseUp = 8, SprayPerBullet = 3, SprayPerBulletUp = 4, SprayCost = 2;
    public const int DoomDmg = 35, DoomDmgUp = 45, DoomPerMark = 3, DoomPerMarkUp = 5, DoomBullet = 6, DoomCost = 3;
    public const int RedFlashDmg = 15, RedFlashDmgUp = 20, RedFlashWeak = 1, RedFlashWeakUp = 2, RedFlashBullet = 4;
    public const int PierceShotDmg = 13, PierceShotDmgUp = 17, PierceShotBullet = 1;
    public const int RapidDmg = 4, RapidDmgUp = 6, RapidHits = 2, RapidBullet = 2;
    public const int VitalDmg = 6, VitalDmgUp = 9, VitalMark = 2, VitalMarkUp = 3, VitalVuln = 2, VitalBullet = 1;
    public const int SuppressDmg = 10, SuppressDmgUp = 14, SuppressMark = 2, SuppressMarkUp = 3, SuppressBullet = 2;
    public const int GrenadeDmg = 12, GrenadeDmgUp = 16, GrenadeVuln = 1, GrenadeVulnUp = 2, GrenadeBullet = 3, GrenadeCost = 2;
    // 스킬
    public const int SmokeBlock = 7, SmokeBlockUp = 10; public const float SmokeReduce = 0.1f, SmokeReduceUp = 0.15f;
    public const int PreciseAim = 1, PreciseAimBullets = 2, PreciseAimBulletsUp = 3;
    public const int RetreatBlock = 10, RetreatBlockUp = 14, RetreatBullets = 3, RetreatBulletsUp = 4, RetreatCost = 2;
    public const int StealthBlock = 14, StealthBlockUp = 19, StealthBullet = 1, StealthCost = 2;
    public const int OverdriveDraw = 3, OverdriveDrawUp = 4;
    public const int ChainTrapWeak = 1, ChainTrapWeakUp = 2, ChainTrapMark = 2, ChainTrapBlock = 5, ChainTrapBlockUp = 8;
    public const int ResupplyEnergy = 1, ResupplyEnergyUp = 2;
    public const int FullReloadBullets = 4, FullReloadBulletsUp = 6, FullReloadDraw = 1;
    public const int LaserAim = 1, LaserAimUp = 2, LaserMark = 2, LaserMarkUp = 3, LaserBullet = 1;
    // 파워
    public const int GazeBonus = 4, GazeBullet = 1, GazeCost = 2, GazeCostUp = 1;
    public const int AmmoSupplyBullets = 2, AmmoSupplyCost = 1, AmmoSupplyCostUp = 0;
    public const int CrimsonEvery = 2, CrimsonBullets = 2, CrimsonDraw = 1, CrimsonCost = 2, CrimsonCostUp = 1;
    public const int AnalysisBlock = 2, AnalysisBlockUp = 3, AnalysisInstantBlockUp = 4, AnalysisBullets = 1, AnalysisBullet = 2;

    // ================= 공용 카드 (2026-11-15: 모든 직업이 보상·상점·보물 상자에서 얻음) =================
    public const int CAmbushDmg = 5, CAmbushDmgUp = 8;
    public const int CJabDmg = 7, CJabDmgUp = 10, CJabWeak = 1, CJabWeakUp = 2;
    public const int CComboDmg = 8, CComboDmgUp = 10, CComboPerCard = 2, CComboPerCardUp = 3;
    public const int CShieldBashDmg = 10, CShieldBashDmgUp = 14, CShieldBashVuln = 2, CShieldBashVulnUp = 3, CShieldBashCost = 2;
    public const int CDoubleCutDmg = 4, CDoubleCutDmgUp = 6, CDoubleCutHits = 2;
    public const int CBreakDmg = 7, CBreakDmgUp = 11;
    public const int CLootDmg = 9, CLootDmgUp = 12, CLootGold = 20, CLootGoldUp = 30;
    public const int CApocalypseDmg = 28, CApocalypseDmgUp = 35, CApocalypsePerExhaust = 3, CApocalypsePerExhaustUp = 4, CApocalypseCost = 3;
    public const int CEmergencyBlock = 5, CEmergencyBlockUp = 8;
    public const int CFirstAidHeal = 8, CFirstAidHealUp = 12;
    public const int CRegroupBlock = 6, CRegroupBlockUp = 9, CRegroupDraw = 1;
    public const int CExposeVuln = 1;
    public const int CFocusRes = 2;
    public const int COpportunityDraw = 2, COpportunityDrawUp = 3, COpportunityEnergy = 1;
    public const int CIndomitableBlock = 18, CIndomitableBlockUp = 24, CIndomitableCost = 2;
    public const int CTimeWarpCost = 2, CTimeWarpCostUp = 1;
    public const int CRefitBlock = 8, CRefitBlockUp = 11, CRefitDraw = 1;
    public const int CSteelWillBlock = 4, CSteelWillBlockUp = 6;
    public const int CTranscendCost = 3, CTranscendCostUp = 2;
    public const int CInstinctEnergy = 1, CInstinctDraw = 1, CInstinctCostUp = 0;
    public const int CPotentialCostUp = 0;
    public const int CThornArmorBlock = 7, CThornArmorBlockUp = 10, CThornArmorDmg = 3, CThornArmorDmgUp = 5;
    public const int TreasureCardChance = 25;      // 보물 상자 물건 하나가 공용 카드일 확률 %
    public const int CommonCardChance = 25;        // 전투 보상·상점 카드 한 칸이 공용 카드일 확률 % (나머지는 직업 카드)

    // ================= 카드 숫자 (2026-11-09 등급·강화 최종본 + Claude 수정) =================
    // 한 줄 = 카드 한 장. "Up" 이 붙은 값 = 강화했을 때 (타격+ 처럼 이름 뒤에 + 가 붙음)
    //   예) SStrikeDmg = 6, SStrikeDmgUp = 9  →  타격 6 피해, 타격+ 9 피해
    // 순서: 에너지(Cost), 자원(영혼·홍염·마나) 비용, 그 다음 효과 숫자
    // 등급(일반/희귀/영웅/전설)은 CardLibrary 의 각 카드에 적혀 있음

    // ---- 검은 낫: 기본 카드 (시작 덱 전용) ----
    public const int SStrikeDmg = 6, SStrikeDmgUp = 9, SStrikeSoul = 1;                      // 타격
    public const int SDefendBlock = 5, SDefendBlockUp = 8, SDefendSoul = 1;                  // 방어
    public const int ReaperSlashCost = 1, ReaperSlashDmg = 7, ReaperSlashDmgUp = 10, ReaperSlashHeal = 3, ReaperSlashHealUp = 4; // 사신 베기
    public const int VeilCost = 1, VeilBlock = 8, VeilBlockUp = 11, VeilSoul = 2, VeilSoulUp = 3;   // 장막
    // ---- 검은 낫: 공격 ----
    public const int SoulReleaseCost = 0, SoulReleaseDmg = 4, SoulReleaseDmgUp = 6, SoulReleasePerSoul = 2, SoulReleasePerSoulUp = 3, SoulReleaseHeal = 1; // 영혼 방출 [일반]
    public const int SoulCleaveCost = 1, SoulCleaveDmg = 9, SoulCleaveDmgUp = 13, SoulCleaveSoul = 4;   // 영혼 가르기 [일반]
    public const int NightRaidCost = 1, NightRaidDmg = 7, NightRaidDmgUp = 10, NightRaidSoul = 2;       // 야습 [일반] (밤 2배)
    public const int MoonlightCost = 1, MoonlightDmg = 8, MoonlightDmgUp = 11, MoonlightStacks = 4, MoonlightStacksUp = 6; // 월광섬 [일반]
    public const int ShadowRaidCost = 1, ShadowRaidDmg = 7, ShadowRaidDmgUp = 10, ShadowRaidSoul = 5;   // 그림자 습격 [일반] (Claude: 다시 넣음). 3-172: 영혼 2 → 5
    public const int ShadowRaidNightTurns = 2;   // 3-172: 그림자 습격의 [밤] 지속 턴 (이번 턴 포함. 2 = 이번 턴 + 다음 턴)
    public const int AmbushCost = 0, AmbushDmg = 9, AmbushSoul = 2;                                     // 기습 [희귀] (강화: 소멸 없음)
    public const int BloodDanceCost = 2, BloodDanceSoul = 2, BloodDanceDmg = 18, BloodDanceDmgUp = 24;  // 피의 검무 [희귀] (Claude: 강화 = 피해 24)
    public const int ReaperWaltzCost = 1, ReaperWaltzSoul = 1, ReaperWaltzDmg = 4, ReaperWaltzDmgUp = 6, ReaperWaltzHits = 2, ReaperWaltzNightHits = 4; // 사신의 윤무 [희귀]
    public const int DarkSlashCost = 2, DarkSlashDmg = 14, DarkSlashDmgUp = 18, DarkSlashSoul = 2, DarkSlashSoulUp = 3; // 흑야 참격 [희귀]
    public const int HalfMoonCost = 2, HalfMoonDamage = 20, HalfMoonDamageUp = 28;                     // 반월참 [영웅] (밤이면 1.5배)
    public const float HalfMoonNightBonus = 0.5f;
    public const int SentenceCost = 2, SentenceSoul = 3, SentenceDmg = 10, SentenceBaseStacks = 10, SentenceBaseStacksUp = 15, SentenceStacksPerAgi = 2, SentenceHeal = 7; // 사형 선고 [영웅]
    public const int HarvestScytheCost = 0, HarvestScytheSoul = 4, HarvestScytheDmg = 7;                // 수확의 낫 [영웅]
    public const float HarvestScytheMultiplier = 1.5f, HarvestScytheMultiplierUp = 2f;
    // ---- 검은 낫: 스킬 ----
    public const int AbsorbCost = 1, AbsorbSoul = 4, AbsorbHeal = 1, AbsorbHealUp = 3;                 // 흡수 [일반] (취약2 약화1)
    public const int BlackCloakCost = 1, BlackCloakSoul = 1, BlackCloakBlock = 10, BlackCloakBlockUp = 14; // 칠흑의 망토 [일반] (Claude: 희귀 → 일반)
    public const int ShroudCost = 1, ShroudSoul = 1, ShroudEnergy = 2, ShroudDraw = 3, ShroudSoulGain = 4; // 사신의 수의 [희귀] (강화: 소멸 없음)
    public const int SoulHarvestCost = 0, SoulHarvestSoul = 3, SoulHarvestSoulUp = 5, SoulHarvestEnergy = 1, SoulHarvestDraw = 1, SoulHarvestDrawUp = 2; // 영혼 수확 [영웅]
    public const int SoulSealCost = 1, SoulSealCostUp = 0, SoulSealDiscount = 1;                      // 영혼 봉인 [영웅]
    public const int DeathMarkCost = 0, DeathMarkSoul = 4, DeathMarkKillSoul = 3, DeathMarkKillSoulUp = 5, DeathMarkKillHeal = 4, DeathMarkKillHealUp = 8; // 죽음의 인장 [전설]
    public const float DeathMarkBonus = 1.0f;      // 죽음의 인장: 내 공격 배수 +1.0 (2배)
    public const int DarkPactCost = 0, DarkPactEnergy = 2, DarkPactDraw = 4, DarkPactHpLoss = 4, DarkPactSoul = 2; // 어둠의 계약 [전설] (강화: HP 잃지 않음)
    public const int DarkDominionCost = 2, DarkDominionSoul = 3, DarkDominionStacks = 7, DarkDominionStacksUp = 12, DarkDominionDraw = 1; // 어둠의 지배 [전설]
    public const float DarkDominionReduction = 0.4f;
    // ---- 검은 낫: 파워 ----
    public const int ReaperStepsCost = 1, ReaperStepsStacks = 3, ReaperStepsStacksUp = 5;             // 사신의 발걸음 [희귀] (무작위 적 1명)
    public const int FullMoonCost = 1, FullMoonSoul = 1, FullMoonStacks = 2, FullMoonBonus = 1, FullMoonBonusUp = 2; // 만월 [희귀]
    public const int RedMoonCost = 0, RedMoonSoul = 2, RedMoonBonus = 2, RedMoonBonusUp = 4;          // 적월 [희귀]
    public const float RedMoonHpRatio = 0.6f;
    public const int SoulExploitCost = 1, SoulExploitSoul = 2, SoulExploitPer = 3, SoulExploitPerUp = 2, SoulExploitDamage = 2, SoulExploitMaxSoul = 3; // 영혼 착취 [영웅]
    public const int DeathNightCost = 2, DeathNightCostUp = 1, DeathNightFlatBonus = 3, DeathNightBlock = 5, DeathNightSoulPerTurn = 1; // 죽음의 밤 [전설]
    public const int EclipseCost = 3, EclipseCostUp = 2, EclipseSoul = 3;                             // 월식 [전설] (원래 손패는 버림)

    // ---- 코스믹 피닉스: 기본 카드 ----
    public const int PStrikeDmg = 6, PStrikeDmgUp = 9, PStrikeFlame = 1;                     // 찌르기
    public const int PDefendBlock = 5, PDefendBlockUp = 8, PDefendFlame = 1;                 // 방어
    public const int PierceCost = 1, PierceDmg = 7, PierceDmgUp = 10, PierceBurn = 2, PierceBurnUp = 3;   // 피닉스 피어스
    public const int PShieldCost = 1, PShieldFlame = 1, PShieldBlock = 10, PShieldBlockUp = 14, PShieldBurn = 2; // 피닉스의 방패
    // ---- 피닉스: 공격 ----
    public const int AshCost = 0, AshDmgPerFlame = 3, AshDmgPerFlameUp = 4, AshBlockPerFlame = 2, AshBlockPerFlameUp = 3; // 재의 소멸 [일반] (한 번에 쓴 홍염 3/6/9 → 발화 1/2/3)
    public const int RushCost = 1, RushDmg = 8, RushBurn = 1, RushWeak = 1, RushWeakUp = 2;            // 돌진 [일반]
    public const int GuardBreakCost = 1, GuardBreakDmg = 6, GuardBreakVuln = 1, GuardBreakVulnUp = 2;  // 가드 브레이크 [일반]
    public const int SunspotCost = 2, SunspotFlame = 1, SunspotDmg = 14;                              // 흑점 폭발 [희귀]
    public const float SunspotBurnMult = 1f, SunspotBurnMultUp = 1.5f;
    public const int FlameLanceCost = 2, FlameLanceCostUp = 1, FlameLanceDmg = 14, FlameLanceBurn = 3; // 화염의 창 [희귀]
    public const int BrandCost = 1, BrandFlame = 1, BrandDmg = 10, BrandTurns = 1, BrandTurnsUp = 2;  // 낙인 [희귀]
    public const int BlueFlameCost = 2;                                                               // 푸른 불꽃 [영웅]
    public const float BlueFlameRatio = 0.25f, BlueFlameRatioUp = 0.35f;
    public const int NovaCost = 2, NovaDmg = 7, NovaBurn = 4, NovaBurnUp = 6;                         // 파멸의 피닉스 노바 [전설]
    public const float NovaPerCon = 1.5f, NovaPerConUp = 2f;
    public const int MeteorCost = 3, MeteorDmg = 17;                                                  // 유성 낙하 [전설] (취약1)
    public const float MeteorRatio = 0.15f, MeteorRatioUp = 0.25f;
    // ---- 피닉스: 스킬 ----
    public const int SamsaraCost = 1, SamsaraBlock = 8, SamsaraBlockUp = 11;                          // 윤회의 불꽃 [일반]
    public const float SamsaraPerCon = 0.5f, SamsaraPerConUp = 1f;
    public const int FeatherCost = 0, FeatherDraw = 3, FeatherDrawUp = 4, FeatherFlame = 2;           // 피닉스의 깃털 [희귀]
    public const int EmberBurstCost = 1, EmberBurstCostUp = 0, EmberBurstFlame = 1;                   // 불씨 폭발 [희귀] (최대치 = 내 발화 최대치)
    public const int FlameRegenCost = 0, FlameRegenHpLoss = 7, FlameRegenHpLossUp = 4, FlameRegenEnergy = 1, FlameRegenFlame = 3; // 불꽃 재생 [희귀] (Claude: 전설 → 희귀)
    public const int HeatCost = 0, HeatFlame = 2, HeatDraw = 2, HeatSkills = 1, HeatSkillsUp = 2;     // 열기 응축 [영웅] (다음 스킬 N장 -1)
    public const int GuardStanceCost = 2, GuardStanceBlock = 12, GuardStanceBlockUp = 16, GuardStanceEnergy = 1, GuardStanceDraw = 1; // 가드 스탠스 [영웅]
    public const int FlameWallCost = 2, FlameWallBlock = 10, FlameWallPerCon = 2;                     // 화염 장벽 [영웅] (Claude: 전설 → 영웅, 강화: 내 1.5배 없음)
    public const float FlameWallReduction = 0.4f;  // 아군 받는 피해 -40%
    public const float FlameWallSelfMultiplier = 1.5f;
    // ---- 피닉스: 파워 ----
    public const int SunWrathCost = 1, SunWrathBurn = 2, SunWrathBurnUp = 3;                          // 태양의 진노 [희귀]
    public const int ThornsCost = 1, ThornsFlame = 1, ThornsBurn = 1, ThornsMaxPerTurn = 3;           // 작열하는 가시 [희귀] (강화: 횟수 제한 없음)
    public const int FlameTrainingCost = 1, FlameTrainingCostUp = 0, FlameTrainingDmg = 2, FlameTrainingBurn = 1, FlameTrainingMax = 2; // 화염 단련 [일반] (Claude: 희귀 → 일반)
    public const int EternalEmberCost = 1, EternalEmberFlame = 1, EternalEmberBlock = 2;              // 영원한 불씨 [영웅]
    public const float EternalEmberHpRatio = 0.3f, EternalEmberHpRatioUp = 0.5f;
    public const int PGuardCost = 2, PGuardBlock = 7, PGuardTurnBlock = 3;                            // 피닉스의 가호 [전설]
    public const float PGuardPerCon = 0.5f, PhoenixGuardReduction = 0.1f, PhoenixGuardReductionUp = 0.2f;
    public const int PHeartCost = 3, PHeartFlame = 2, PhoenixHeartFlame = 7;                          // 피닉스의 심장 [전설]
    public const float PhoenixHeartHeal = 0.2f, PhoenixHeartHealUp = 0.4f;
    public const int UndyingFlameCost = 2, UndyingFlameMaxFlame = 2, UndyingFlameMaxFlameUp = 4;      // 꺼지지 않는 불꽃 [전설]

    // ---- 마도사: 기본 카드 ---- (2026-11-09 플레이 피드백: 마나가 2턴 만에 30/30 → 기본 카드 마나 획득 감소)
    public const int IceBulletCost = 1, IceBulletDmg = 7, IceBulletDmgUp = 10, IceBulletFreeze = 1, IceBulletMana = 1, IceBulletManaUp = 2; // 빙결 탄환
    public const int SparkCost = 0, SparkDmg = 4, SparkDmgUp = 6, SparkShock = 1, SparkMana = 2, SparkManaUp = 3; // 스파크 [벼락]
    public const int MDefendBlock = 5, MDefendBlockUp = 8, MDefendMana = 1, MDefendManaUp = 2;        // 방어
    public const int IceVeilCost = 0, IceVeilMana = 4, IceVeilBlock = 10, IceVeilBlockUp = 14;        // 얼음 장막
    // ---- 마도사: 공격 ----
    public const int ColdWaveCost = 1, ColdWaveMana = 1, ColdWaveDmg = 7, ColdWaveDmgUp = 10;         // 한파 [일반]
    public const int IcicleCost = 1, IcicleMana = 3, IcicleDmg = 4, IcicleDmgUp = 6, IcicleHits = 4, IcicleMaxFreeze = 2; // 고드름 [일반] (무작위 적)
    public const int PlasmaCost = 1, PlasmaDmg = 8, PlasmaDmgUp = 12, PlasmaShock = 2, PlasmaMana = 6; // 플라즈마 볼트 [일반]
    public const int MThunderStrikeCost = 1, MThunderStrikeDmg = 8, MThunderStrikeChain = 4, MThunderStrikeChainUp = 7, MThunderStrikeMana = 3; // 뇌전 일격 [일반] [벼락]
    public const int StaticCost = 0, StaticMana = 4, StaticManaUp = 2, StaticDmg = 6;                 // 정전기 방출 [일반]
    public const int CurrentArrowCost = 1, CurrentArrowMana = 2, CurrentArrowDmg = 10, CurrentArrowDmgUp = 15, CurrentArrowShock = 2, CurrentArrowGain = 6; // 전류 화살 [희귀]
    public const int ChainLightningCost = 1, ChainLightningMana = 3, ChainLightningDmg = 6, ChainLightningDmgUp = 8, ChainLightningHits = 3; // 연쇄 번개 [희귀] (감전 있으면 4회)
    public const int IceSpikeCost = 2, IceSpikeMana = 6, IceSpikeDmg = 16, IceSpikeBonus = 10, IceSpikeBonusUp = 16; // 얼음 송곳 [영웅]
    public const int ShatterCost = 2, ShatterMana = 8, ShatterDmg = 14, ShatterPerStack = 8, ShatterPerStackUp = 12; // 빙결 파쇄 [영웅]
    public const int ThunderboltCost = 2, ThunderboltMana = 10, ThunderboltDmg = 24, ThunderboltDmgUp = 32, ThunderboltPerCard = 6, ThunderboltShock = 2; // 천둥 벼락 [영웅]
    public const int GlacierCutCost = 1, GlacierCutMana = 12, GlacierCutManaUp = 8, GlacierCutDmg = 20; // 빙하절단 [전설]
    public const float GlacierCutBossReduction = 0.5f;   // 보스는 동상 대신 이번 적 차례 피해 -50%
    public const int EternalSnowCost = 0, EternalSnowMana = 15, EternalSnowManaUp = 10, EternalSnowDmg = 15; // 만년설 [전설]
    public const int AbsZeroCost = 3, AbsZeroCostUp = 2, AbsZeroMana = 18, AbsZeroDmg = 40;           // 절대영도 [전설] (취약2)
    public const int LightningFallCost = 2, LightningFallDmg = 15, LightningFallPerMana = 3, LightningFallPerManaUp = 4; // 낙뢰 [전설]
    // ---- 마도사: 스킬 ----
    public const int CurrentBarrierCost = 1, CurrentBarrierBlock = 7, CurrentBarrierBlockUp = 10;     // 전류 방벽 [일반]
    public const int WeakSpotCost = 1, WeakSpotTurns = 1, WeakSpotTurnsUp = 2;                        // 약점 포착 [일반]
    public const int ManaAmpCost = 1, ManaAmpMana = 10, ManaAmpManaUp = 15, ManaAmpBlock = 7;         // 마력 증폭 [일반] (Claude: 영웅 → 일반, 소멸)
    public const int ManaShieldCost = 0, ManaShieldMana = 5, ManaShieldBlock = 6, ManaShieldBlockUp = 10; // 마나 실드 [희귀]
    public const int IonizeCost = 1, IonizeShock = 1, IonizeShockUp = 2;                              // 이온화 [희귀]
    public const int ManaCycleCost = 0, ManaCycleNeed = 3, ManaCycleMana = 5, ManaCycleEnergy = 1, ManaCycleEnergyUp = 2; // 마나 순환 [희귀] (Claude: 전설 → 희귀)
    public const int RebuildCost = 1, RebuildMana = 5, RebuildBlock = 8, RebuildBlockUp = 12, RebuildGain = 10, RebuildGainUp = 15; // 마력 재구성 [영웅]
    public const int LimitBreakCost = 0, LimitBreakMana = 15, LimitBreakBlock = 15, LimitBreakBlockUp = 20; // 한계 돌파 [영웅] (Claude: 전설 → 영웅)
    public const float LimitBreakHp = 0.4f;
    public const int GlacierBarrierCost = 2, GlacierBarrierMana = 10, GlacierBarrierBlock = 20, GlacierBarrierBlockUp = 30; // 빙하 결계 [영웅] (Claude: 전설 → 영웅)
    public const float GlacierBarrierReduction = 0.2f, GlacierBarrierReductionUp = 0.3f;
    public const int CryoCost = 2, CryoMana = 5, CryoGain = 15, CryoGainUp = 25;                      // 냉동 수면 [영웅] (Claude: 전설 → 영웅, 소멸)
    public const float CryoHeal = 0.1f;
    // ---- 마도사: 파워 ----
    public const int HailCost = 1, HailMana = 3, HailDamage = 10, HailDamageUp = 15;                  // 우박 [희귀]
    public const int StormFieldCost = 1, StormFieldMana3 = 3, StormFieldDamage = 4, StormFieldDamageUp = 6, StormFieldMana = 2; // 뇌전 지대 [희귀]
    public const int BlizzardCost = 1, BlizzardMana = 2;                                              // 눈보라 [희귀] (강화: 매 턴)
    public const int StaticFieldCost = 1, StaticFieldMana = 4, StaticFieldBlock = 3, StaticFieldBlockUp = 5; // 정전기 역장 [희귀]
    public const int FrostPrisonCost = 2, FrostPrisonCostUp = 1, FrostPrisonMana = 4, FrostPrisonDamage = 4; // 혹한의 감옥 [영웅]
    public const int FrostPrisonMaxDraw = 3;       // 혹한의 감옥 뽑기: 한 턴 최대 3장 (무한 뽑기 방지, Claude 가 정함)
    public const int ThunderGodCost = 2, ThunderGodMana = 6, ThunderGodBonus = 2, ThunderGodBonusUp = 3, ThunderGodMax = 4; // 뇌신 권능 [영웅]
    public const int ManaReleaseCost = 2, ManaReleaseCostUp = 1, ManaReleaseMana = 15, ManaReleaseRegen = 3; // 마력해방 [전설]

    // ---- 보스 빙결 면역 ----
    // 보스가 [동상]이 되면 그 뒤 다음 적 차례가 끝날 때까지 동결이 안 쌓임 (= 다음 내 턴 동안 면역)
    //   숫자 2 = 적 차례가 2번 끝나면 풀림 (동상으로 쉬는 차례 + 다음 차례)
    public const int BossFreezeImmuneEnemyTurns = 2;

    // ================= 보상 (2026-11-09) =================
    // 골드 (플레이어마다 따로). 최소 ~ 최대 사이 무작위
    // 3-162: 골드는 전투에 나온 몬스터 중 가장 높은 등급으로 정함 (일반 / 정예 / 엘리트 / 보스)
    //   예전: 쉬움 15~25 / 어려움 30~40 / 보스 80~100 (정예와 엘리트가 같았음)
    public const int GoldEasyMin = 18, GoldEasyMax = 26;           // 일반 몬스터만
    public const int GoldVeteranMin = 28, GoldVeteranMax = 38;     // 정예가 섞인 전투
    public const int GoldHardMin = 42, GoldHardMax = 52;           // 엘리트
    public const int GoldBossMin = 95, GoldBossMax = 115;          // 보스
    // 스테이지가 올라갈수록 골드도 조금씩 (1스테이지 ×1 → 4스테이지 ×1.45). 상점 가격도 같이 오름 (아래 ShopPriceScale)
    public static readonly float[] StageGoldScale = { 1.0f, 1.15f, 1.3f, 1.45f };
    // 상점 기준 가격 (상점 UI가 들어오면 사용). 일반 / 희귀 / 영웅 / 전설
    public static readonly int[] ShopCardPrice = { 50, 75, 110, 160 };
    // 3-162: 스테이지별 상점 가격 배율. 초반(1스테이지)은 25% 싸게 → 카드 38~ / 아티팩트 105~ / 포션 34
    public static readonly float[] ShopPriceScale = { 0.75f, 0.9f, 1.0f, 1.1f };
    // 상점 (2026-11-15): 카드 5장 · 아티팩트 2개 · 포션 2개 (플레이어마다 따로)
    public const int ShopCards = 5, ShopRelics = 2, ShopPotions = 2;
    public static readonly int[] ShopRelicPrice = { 140, 190, 250 };   // 일반, 희귀, 영웅
    public const int ShopPotionPrice = 45;
    // 3-163: 아티팩트 팔기 (상점에서 판매대로 끌어다 놓기). 값 = 그 등급 상점 가격(스테이지 배율 포함)의 절반
    //   시작 아티팩트·공용 아티팩트는 못 팖. 보스 아티팩트는 영웅 가격 기준
    public const float RelicSellRatio = 0.5f;

    // 공용(파티) 아티팩트 (2026-11-15) - 보물 상자 전용
    public const int TreasurePartyRelicChance = 70;   // 보물 상자에서 아티팩트가 나올 때 공용일 확률 %
    public const int WarBannerBlock = 6, MedicBagHeal = 5, CommanderBugleDraw = 1, AllianceOathEnergy = 1;
    public const int SiegeRamVuln = 1, SupplyWagonGold = 8, GuardLampHeal = 3, BondStoneHp = 6;

    // 카드 보상: 3장 중 1장 (건너뛰기 가능). 등급 확률 % — 순서: 일반, 희귀, 영웅, 전설 (합 100)
    public const int CardRewardChoices = 4;        // 3-113: 3 → 4 (공용 카드가 섞여도 직업 카드를 고를 수 있게)
    public const int CardRewardPicks = 2;          // 3-113: 그중 최대 2장까지 고름 (한 장만 고르고 그만둬도 됨)
    public const int CardRewardCommonSlots = 1;    // 3-113: 보상 4장 중 공용 카드 칸 수 (고정). 나머지 3장은 직업 카드

    // 3-114 제물의 제단: 엘리트를 잡으면 보상 뒤에 나옴 (안 해도 됨)
    //   덱에서 AltarSacrifice 장을 바치면 → 직업 전용 카드 10장이 나오고 → 그중 원하는 1장을 얻음
    // 3-127: 공용 카드는 안 나옴. 10장은 "한 장씩 따로" 등급을 뽑음 (좋은 등급일수록 낮은 확률)
    //   기대값 (10장 기준): 일반 7.5 / 희귀 2 / 영웅 0.4 / 전설 0.1 → 보통 "일반 8 + 희귀 2" 정도
    //   그 등급 카드가 다 떨어지면 (같은 카드는 두 번 안 나옴) 한 단계 위 등급에서 뽑음
    public const int AltarSacrifice = 3;           // 바치는 카드 수
    public const int AltarMinDeck = 8;             // 덱이 이 장 수 이상일 때만 (바치고 나서도 5장 + 1장은 남게)
    public const int AltarOfferCount = 10;         // 3-127: 보여 주는 카드 수 (위 5 + 아래 5)
    public const int AltarOfferCols = 5;           // 3-127: 한 줄에 5장
    public static readonly int[] AltarCardOdds = { 75, 20, 4, 1 };

    // 3-129 피의 제단: 1·2스테이지 4칸(비전투 고르기)의 [이벤트] 자리 (이벤트가 준비될 때까지)
    //   덱에서 2장을 바치면 → 직업 카드 3장(희귀 이상) 중 1장 + 유물 1개를 얻고 → 대가로 최대 HP 가 영구히 10% 줄어듦
    //   안 하고 [떠나기] 도 됨. 멀티면 사람마다 따로 고름
    public const int BloodAltarStages = 2;          // 1·2스테이지(수성)만
    public const int BloodAltarSacrifice = 2;       // 바치는 카드 수
    public const int BloodAltarOfferCount = 3;      // 보여 주는 직업 카드 수
    public const int BloodAltarMinDeck = 7;         // 덱이 이 장 수 이상일 때만 (바치고 나서도 5장 + 1장)
    public static readonly int[] BloodAltarCardOdds = { 0, 70, 25, 5 };   // 카드 한 장 등급 % : 일반, 희귀, 영웅, 전설 (희귀 이상 보장)
    public const float BloodAltarHpLoss = 0.10f;    // 최대 HP -10% (반올림, 최소 1)   // 3-127: 카드 한 장의 등급 확률 % : 일반, 희귀, 영웅, 전설
    public static readonly int[] CardOddsEasy = { 60, 30, 9, 1 };
    public static readonly int[] CardOddsHard = { 35, 40, 20, 5 };
    public static readonly int[] CardOddsBoss = { 0, 0, 70, 30 };
    // 보정 장치: 일반 카드가 나올 때마다 영웅 확률 +1%p (최대 +15). 영웅·전설이 나오면 0
    public const int CardPityStep = 1;
    public const int CardPityMax = 15;

    // 아티팩트 등급 확률 % (엘리트·보물) — 순서: 일반(특별 포함), 희귀, 영웅
    public static readonly int[] RelicOdds = { 50, 33, 17 };
    // 3-175: 정예 전투 승리 시 아티팩트 (사람마다 따로 굴림). 영웅은 안 나옴 → 엘리트(확정 1개, 영웅 포함)와 차이를 둠
    public const int VeteranRelicChance = 35;                     // 정예를 이기면 아티팩트가 나올 확률 %
    public static readonly int[] VeteranRelicOdds = { 60, 40 };   // 나오면 등급: 일반(특별 포함) 60 / 희귀 40

    // ================= 아티팩트 숫자 (RelicLibrary 의 효과가 씀) =================
    // 시작 아티팩트
    public const int ReaperMarkStacks = 4;         // 낡은 사신의 표식: 전투 시작 무작위 적 처형 스택
    public const int PhoenixPlumeFlame = 3;        // 불새의 깃털 장식: 전투 시작 홍염
    public const int FrostPendantFreeze = 1;       // 서리 수정 목걸이: 전투 시작 모든 적 동결
    // 공용
    public const int RationPouchHeal = 3;          // 수비대장의 식량 주머니: 웨이브 클리어마다 파티 전원 HP
    public const int RampartBlock = 8;             // 견고한 성벽돌: 전투 시작 방어도
    public const int WarHornDraw = 2;              // 전령의 뿔피리: 첫 턴 추가 뽑기
    public const int GuardHelmHp = 8;              // 수성 병사의 투구: 최대 HP
    public const int ArrowheadVuln = 1;            // 은빛 화살촉: 웨이브 시작 무작위 적 취약
    public const int WhetstoneCards = 2;           // 마력 깃든 숫돌: 첫 손패 공격 카드 N장 이번 전투 에너지 -1
    public const int LanternEnergy = 1;            // 광부의 랜턴: 첫 턴 에너지
    public const int OldShieldBlock = 5;           // 수호자의 낡은 방패: 남은 방어도가 있으면 +5
    public const float SteelBeltHpRatio = 0.05f;   // 3-163: 15% → 5% (페널티가 너무 커서). 무거운 강철 허리띠 페널티: 전투 시작 최대 HP 5% 잃음 (HP 1 아래로는 안 내려감)
    // 보스
    public const int BossRelicEnergy = 1;          // 보스 아티팩트 3개 공통: 턴 시작 에너지 +1
    public const float CrownGoldRatio = 0.5f;      // 폭군의 왕관 페널티: 골드 50%
    public const int BloodContractDrawLoss = 1;    // 피의 계약서 페널티: 턴 시작 뽑기 -1
    public const int IronKeyChoices = 2;           // 봉인된 성문 열쇠 페널티: 카드 보상 2장 중
    public const int IronKeyPicks = 1;             //   3-113: 그중 1장만
    // 검은 낫
    public const int LedgerMaxHp = 1;              // 피 묻은 처형인 명부: 처형마다 최대 HP +1
    public const int HourglassSoul = 3;            // 사신의 모래시계: 전투 시작 영혼
    public const int PendantStacks = 5;            // 부서진 초승달 펜던트: 밤이 켜지면 무작위 적 처형 스택
    public const int SkullHpLoss = 3;              // 금지된 사령술의 해골 페널티: 내 턴 종료마다 HP
    // 피닉스
    public const int UndyingAshBlock = 4;          // 꺼지지 않는 잿더미: 카드로 HP를 잃을 때마다 방어도
    public const int LavaCrystalDamage = 5;        // 용암 결정: 홍염 3배수 발화가 붙을 때 그 적에게 피해
    public const int BlazingCoreCap = 20;          // 폭염의 코어: 발화 최대치 +20
    public const int HellfireBurnMult = 2;         // 지옥불의 심장: 내가 거는 발화 스택 x2 (페널티: 턴 시작 홍염 0)
    // 마도사
    public const int FrostCompassMana = 5;         // 서리 맺힌 나침반: 동상이 터질 때마다 마나
    public const int CapacitorDraw = 1;            // 과부하 축전기: 턴 시작 마나가 최대면 뽑기 + 감전
    public const int CapacitorShock = 1;
    public const int ZeroShardMana = 15;           // 절대 영도의 파편: 최대 마나 +15, 턴당 마나 +2
    public const int ZeroShardRegen = 2;
    public const int ZeroShardHpPerDraw = 1;       //   페널티: 카드 효과로 1장 뽑을 때마다 HP -1 (턴 시작 뽑기는 제외)

    // ---------------- 3-99 추가 아티팩트 (아티팩트 표 v2) ----------------
    // 검은 낫
    public const int SoulLanternSoul = 1;          // 망자의 등불: 전투 시작 영혼 +1
    public const int ChippedScytheSoul = 1;        // 이 빠진 낫날: 적이 쓰러질 때마다 영혼 +1
    public const int ShadowCloakBlock = 4;         // 그림자 망토 조각: [밤]이 켜질 때 (죽음의 밤 중엔 매 턴 시작) 방어도 +4
    public const int SpiritCenserStacks = 2;       // 원혼의 향로: 영혼을 쓸 때마다 무작위 적 처형 스택 +2
    public const int BloodCandelabraDraw = 1;      // 핏빛 촛대: [밤]이 켜질 때 (죽음의 밤 중엔 매 턴 시작) 1장 뽑기
    public const int AbyssalExecuteBonus = 5;      // (보스) 심연의 사신 낫: 처형 판정 때 처형 스택을 +5 로 계산
    public const float AbyssalBlockRatio = 0.7f;   //   페널티: 얻는 방어도 70% 만 (-30%, 내림)
    public const int TwilightRingSoul = 1;         // (보스) 황혼의 계약 반지: 매 턴 영혼 +1
    public const float TwilightRingHpLoss = 0.2f;  //   페널티: 얻는 즉시 최대 HP -20% (반올림)
    // 피닉스
    public const int WarmFeatherFlame = 2;         // 따뜻한 깃털 부적: 전투 시작 홍염 +2
    public const int EmberPouchBurn = 2;           // 불씨 주머니: 매 턴 첫 공격 카드의 대상에게 발화 2
    public const float AshSalveRatio = 0.15f;      // 화산재 연고: 전투 승리 시 잃은 HP 의 15% 회복 (반올림)
    public const int PhoenixTearFlame = 1;         // 불사조의 눈물: 카드 효과로 HP 를 잃을 때마다 홍염 +1
    public const int ScorchingFlintNeed = 3;       // 작열하는 부싯돌: 턴 종료 때 홍염 3 이상이면
    public const int ScorchingFlintBlock = 6;      //   방어도 +6
    public const int SolarPlumeFlame = 2;          // (보스) 태양의 왕관 깃털: 턴 시작 홍염 +2
    public const int SolarPlumeHpLoss = 2;         //   페널티: 턴 시작 HP -2
    public const float BrazierBurnMult = 1.5f;     // (보스) 영겁의 화로: 적 발화 피해 x1.5 (반올림)
    public const float BrazierCardBlockRatio = 0.6f; //   페널티: 카드로 얻는 방어도 60% 만 (-40%, 내림)
    // 스나이퍼
    public const int SuppressorBlock = 3;          // 전술 소음기: [사격] 카드를 쓸 때마다 방어도 +3
    public const int RangefinderAim = 1;           // 고배율 거리측정기: 턴 시작 [조준] +1
    public const int RangefinderBullets = 1;       //   [표식] 있는 적이 쓰러지면 탄환 +1
    // 마도사 (3-101, 아티팩트 표 v3)
    public const int IceBroochMana = 10;           // 얼음 조각 브로치: 전투 시작 마나 +10
    public const int StaticRingShock = 1;          // 정전기의 반지: 매 턴 처음 마나를 쓰는 카드 → 무작위 적 감전 스택 1
    public const int FrostwindCloakBlock = 4;      // 서리바람 망토: 턴 시작 때 [동결] 쌓인 적이 있으면 방어도 +4
    public const float FrostreignTomeBonus = 0.3f; // 빙뢰의 마도서: [동결] 쌓인 적에게 [벼락] 카드 피해 +30%
    public const int GlacialCrownFreeze = 1;       // (보스) 만년설의 왕관: 턴 시작 모든 적 동결 1
    public const int GlacialCrownManaLoss = 15;    //   페널티: 최대 마나 -15
    public const float StormcoreManaRatio = 0.75f; // (보스) 폭풍핵 지팡이: 마나 소모 75% (-25%, 반올림)
    public const float StormcoreBlockRatio = 0.65f;//   페널티: 얻는 방어도 65% 만 (-35%, 내림)

    // ---------------- 스테이지별 몬스터 스케일링 (초안) ----------------
    // 배열 칸 번호 = 스테이지 - 1  (0번 칸 = 1스테이지 ... 3번 칸 = 4스테이지)
    // 보스 전 예상 레벨: 1스테이지 Lv.3, 2스테이지 Lv.5~6, 3스테이지 Lv.8, 4스테이지 Lv.10 기준으로 잡은 추정치
    // 플레이어 HP도 CON으로 늘어나므로 공격력도 함께 올림. 방어도는 공격력과 같은 배율
    public const int MaxStage = 4;
    public static readonly float[] StageHpScale = { 1.0f, 1.8f, 2.6f, 3.4f };   // 3-160: 3-133 값으로 되돌림 (힘 2점당 +1 과 짝). 3-159 의 1 / 2 / 3 / 4 는 힘 1점당 +1 일 때 쓴 것
    // 2026-11-09 플레이 피드백 (힘·민첩 20 으로도 1스테이지 끝에 HP 30/60): 1스테이지 공격력 1.0 → 0.75
    //   예) 슬라임 7 → 5, 병정 강타 13 → 10, 감시자 광선 15 → 11, 오크 수문장 지진 26 → 20
    //   방어도도 이 값을 따라감 (몬스터 방어도도 25% 줄어듦)
    public static readonly float[] StageAttackScale = { 0.75f, 1.3f, 1.6f, 1.9f };

    // ---------------- 지도 ----------------
    // 한 스테이지 = 7층. 1~6층은 무작위 칸, 7층은 보스 1칸
    public const int MapFloors = 8;           // 보스 층 포함 (3-83: 7 → 8, 보스 빼고 7층)
    public const int MapColumns = 5;          // 가로 칸 수 (0~4)
    public const int MapPaths = 6;            // 아래에서 위로 그리는 길 수 (겹치는 칸은 합쳐짐). 3-83: 4 → 6 (슬더스와 같은 수)
    public const int MapMinNodes = 20;        // 보스 빼고 칸이 이보다 적으면 지도를 다시 만듦. 3-83: 14 → 20
    public const int MapMinBranches = 7;      // 위로 가는 길이 2개 이상인 칸(갈림길)이 이보다 적으면 다시 만듦 (3-83)
    public const int MapMinStarts = 3;        // 1층 출발 칸이 이보다 적으면 다시 만듦 (3-83)

    // ---- 최종장(4스테이지) 성 지도 (3-84) ----
    public const int FinalFloors = 6;          // 보스 빼고 층 수: 정문 1 + 무작위 4 + 초소 1 (보스까지 7줄). 3-84: 5 → 6
    public const int FinalEliteMin = 2, FinalEliteMax = 5;        // 엘리트 칸 수 범위 (벗어나면 다시 만듦). 3-84: 최대 4 → 5 (칸이 늘어서)
    public const int FinalTreasureMin = 1, FinalTreasureMax = 4;  // 보물 칸 수 범위. 3-84: 최대 3 → 4
    public const float FinalTopY = 0.30f, FinalBottomY = 0.88f;   // 성 그림 위 칸 자리: 맨 위층(초소)·맨 아래층(정문)의 세로 위치 (그림 높이 비율)
    public const int EliteMinFloor = 3;       // 엘리트는 3층부터
    public const int VeteranMinFloor = 2;     // 3-111: 정예 칸은 2층부터 (1층은 일반 몬스터)
    public const bool RestBeforeBoss = true;  // 보스 바로 아래층(6층)은 전부 모닥불 (슬더스 기본). false 면 무작위
    public const float RestHealRatio = 0.3f;  // 초소·야영지 [치료]: 최대 HP의 30% 회복
    public const float MendHealRatio = 0.6f;  // 2026-11-15 멀티 [집중 치료]: 동료 1명만 최대 HP의 60% 회복 (모두 30% 대신)

    // ---------------- 초소 (1~3스테이지) / 야영지 (4스테이지) ----------------
    // 슬더스 모닥불 대신. 들어가면 하나 고르기: 치료 / 장비 보강 / 정찰
    //   보스 바로 앞 초소에서는 정찰병이 보스 힌트를 공짜로 알려 주고, 정찰 대신 치료·보강 중 고름
    public const int FortifyBlock = 12;       // [장비 보강]: 다음 전투 시작 시 모두 방어도 +12

    // ---------------- 대장간: 강화 (3-123 에서 무기·갑옷·체력으로 바뀜) ----------------
    // 갈래 3개 × 최대 3단계. 한 번 방문에 플레이어마다 한 갈래를 한 단계 올림 (카드 강화와 합쳐서 한 번)
    //   무기 강화 = 모든 공격 피해 +2 / 단계          (저장 칸 이름은 예전 그대로 WeaponBlade)
    //     (3-124: +3 → +2. 모든 공격 카드에 붙고 배율도 받아서 +3 은 카드 강화보다 너무 셌음)
    //   갑옷 강화 = 전투 시작 방어도 +3 / 단계        (저장 칸 WeaponHilt)
    //   체력 강화 = 최대 HP +5 / 단계 (올릴 때 HP 도 +5) (저장 칸 WeaponCore)
    //   ※ 저장 칸 이름을 안 바꾼 이유: 예전 세이브를 그대로 읽기 위해 (자바로 치면 직렬화 필드명 유지)
    //   ※ 예전 [핵] 의 최대 마나 +2 · 마나 회복 +1 은 없어짐
    public const int WeaponMaxLevel = 3;
    public const int BladeDamagePerLevel = 2;      // 무기 강화: 공격 피해 +2 (최대 3번 → +6)
    public const int HiltStartBlockPerLevel = 3;   // 갑옷 강화: 전투 시작 방어도 +3
    public const int VitalMaxHpPerLevel = 5;       // 체력 강화: 최대 HP +5

    // ---------------- 3-123: 1스테이지 전투 길이 맞추기 ----------------
    // 시작 덱 기준 한 턴 피해 (단일 대상, 스탯 0, 몬스터 방어도 무시, 4000판 모의 평균)
    //   검은 낫 18.7 / 피닉스 17.5 / 마도사 15.2 / 스나이퍼 13.8
    // 목표 턴: 일반 3~5 / 정예 4~6 / 엘리트 6~8 / 보스 8~10
    // → 1인 기준 "전투 전체 HP 합" 목표 (몬스터 방어도 몫 약 8% 를 빼고 잡음)
    //   몬스터가 몇 마리든 합이 이 값이 되도록 1스테이지에서만 HP 를 늘리거나 줄임 (인원 배율은 그 위에 곱함)
    //   전투 등급 = 그 전투에서 가장 높은 등급 (일반+정예 = 정예 전투)
    public const int Stage1FightHpNormal = 58;
    public const int Stage1FightHpVeteran = 74;
    public const int Stage1FightHpElite = 102;
    public const int Stage1FightHpBoss = 140;
    public const float Stage1FitMin = 0.6f;     // 너무 크게 바뀌지 않게 제한
    public const float Stage1FitMax = 3.0f;


    // ================= 새 구조: 1~3스테이지 수성 / 4스테이지 공격 =================
    // 지도가 보이는 스테이지 (2스테이지 보스를 잡으면 지도를 얻음 → 3스테이지부터 지도)
    //   2026-11-10: 4 → 3. 3스테이지 = 지도를 따라 적 요새로 진군, 보스 = 요새 문을 지키는 수문장
    //               4스테이지 = "최종장" (문이 열린 성 안, 길이 정해진 짧은 지도)
    public const int MapStage = 3;

    // ---------------- 수성 (1~3스테이지): 10칸 (2026-10-30, 7칸 → 10칸) ----------------
    // 칸 번호는 1부터 셈 (1칸 ~ 10칸)
    //   1 쉬운 전투(1스테이지만 고정) / 2·3 ??? / 4 비전투 고르기 / 5 ??? / 6 피할 수 없는 침공
    //   7·8 ??? / 9 모닥불 고정 / 10 보스
    public const int SiegeSteps = 10;         // 10칸째 = 보스
    public const int SiegeFixedEasyStep = 1;  // 1칸 = 쉬운 전투 고정. 단, 1스테이지만 (시작 덱으로 엘리트는 무리)
                                              //        2스테이지부터 1칸은 ??? 3개
    public const int NonCombatStep = 4;       // 4칸 = 비전투 5개 중 고르기
    public const int SiegeForcedFightStep = 7; // 7칸 = 엘리트 고정 (2026-11-15: 6 → 7, 1스테이지부터 엘리트)
    public const int SiegeTwoWaveStep = 2;    // (3-108부터 안 씀) 예전 2칸 2웨이브. 지금은 2스테이지 5칸 Stage2MidFightStep
    public const int TwoWaveStage = 2;        // 3-107: 2웨이브는 2스테이지에서만 (1·3·4스테이지는 웨이브 없음)
    // 3-108: 2스테이지는 3칸 정예 고정 대신 "중반(5칸)에 딱 한 번" 고정 전투 → 2웨이브 일반 전투 또는 정예 전투 (반반)
    //   2스테이지 2칸·3칸은 보통 ??? 칸. 1스테이지는 그대로 3칸 정예
    public const int Stage2MidFightStep = 5;       // (3-110부터 안 씀 → 아래 칸별 구성표)
    public const int MidFightTwoWaveChance = 50;   // (3-110부터 안 씀)

    // ---------------- 3-110 수성 칸별 구성 (1칸 ~ 10칸) ----------------
    // 글자: N = 일반 전투, V = 정예 전투, E = 엘리트 전투, W = 일반 2웨이브 전투
    //   글자만 있으면 그 전투 고정 칸 (예: "V" = 정예 고정)
    //   끝에 ? 가 붙으면 ??? 칸: 글자마다 전투 카드 1장 + "?" 하나마다 비전투 1장을 섞어서 가려 둠
    //     예) "NV?" = 일반·정예·비전투 3장 / "VE??" = 정예·엘리트·비전투 2장 (비전투 2장은 서로 다른 종류)
    //   "-" = 특수 칸 (4칸 비전투 고르기, 9칸 모닥불, 10칸 보스 — 따로 처리)
    // 3-125: ??? 칸 속 엘리트는 1스테이지 8칸, 2스테이지 5칸부터만 나옴
    //   1스테이지: 7칸 엘리트 고정은 그대로 (3-126 에서 되돌림). 8칸은 4장 (전투 3 : 비전투 1)
    //   2스테이지: 5칸 = 2웨이브·정예·엘리트 + 비전투 1 / 6칸 = 전투 3 + 비전투 1 / 7칸 엘리트 고정 / 8칸 = 정예·엘리트 + 비전투 2
    public static readonly string[] Stage1StepPlan = { "N", "NN?", "V", "-", "NV?", "NV?", "E", "NVE?", "-", "-" };
    public static readonly string[] Stage2StepPlan = { "N", "NV?", "V", "-", "WVE?", "NVE?", "E", "VE??", "-", "-" };
    public const int SiegeVeteranStep = 3;    // 3칸 = 정예 몬스터 고정
    public const int ForcedHardFromStage = 1;  //        7칸 엘리트는 1스테이지부터
    public const int SiegeRestStep = 9;       // 9칸 = 모닥불 고정 (보스 바로 앞)
    public const int EliteFromStep = 5;       // ??? 칸의 엘리트(어려운 전투)는 5칸부터. 2·3칸(2스테이지부터는 1칸도)은 쉬운 2 + 비전투 1
    // 나머지 칸(2, 3, 5, 7, 8 + 2스테이지부터 1) = ??? 3개: 쉬운 1 + 어려운 1 + 비전투 1 (자리만 섞음)
    //   단 5칸 전(1·2·3칸)은 어려운 자리가 쉬운 전투 → 엘리트는 5칸부터 (1스테이지 6칸 침공도 엘리트 아님)

    // ??? 비전투 칸이 무엇일지 확률 (합계 100). 순서: 모닥불, 대장간, 보물, 이벤트, 상점
    public static readonly int[] NonCombatWeights = { 30, 20, 25, 0, 25 };   // 2026-11-15: 이벤트 삭제 (0) → 나머지에 나눔

    // 포션 (2026-11-15)
    public const int PotionSlots = 2;                 // 한 사람이 들 수 있는 포션 수
    public const int TreasurePotionChance = 50;       // 보물 상자: 포션이 나올 확률 % (아니면 아티팩트)
    // 보물 상자 고르기 (2026-11-15, 슬더스2 멀티 방식): 상자 안 물건 = 인원 + 1개. 한 사람이 1개씩 고름
    //   같은 걸 여러 명이 고르면 주사위로 1명만 받고, 나머지는 남은 물건 중 무작위
    public const int TreasureExtraChoices = 1;
    public const int TreasureChests = 2;              // 3-174: 보물 칸 하나에 상자 2개. 상자마다 한 사람이 1개씩 → 한 사람당 2개 받음
    public const int PartyRelicMinPlayers = 2;        // 3-174: 공용(파티) 아티팩트는 이 인원 이상(멀티)일 때만 나옴
    public const int StartGold = 100;                 // 3-112: 런을 시작할 때 갖고 시작하는 골드 (1인당)
    public const int TreasureFallbackGold = 30;       // 받을 수 있는 물건이 하나도 없으면 대신 골드
    public const float PotionHealRatio = 0.25f;
    public const int PotionFireDamage = 15, PotionPowerBonus = 3, PotionVulnTurns = 2;
    // 3-157 새 포션 5종
    public const int PotionBlock = 12;             // 방어 포션: 아군 1명 방어도 +12
    public const int PotionEnergy = 2;             // 에너지 포션: 이번 턴 에너지 +2
    public const int PotionDraw = 3;               // 신속 포션: 카드 3장 뽑기
    public const int PotionWeakTurns = 2;          // 약화 포션: 모든 적 약화 2턴
    public const float PotionReviveRatio = 0.3f;   // 불사조 깃털: 쓰러진 동료를 최대 HP 30% 로 부활 (멀티 전용)
    public const int PotionCommonWeight = 3, PotionRareWeight = 1;   // 포션이 나올 때 무게: 보통 3 : 희귀(불사조 깃털) 1

    // 레벨 (2026-11-15): 경험치 = 쉬운 전투 1 / 어려운 전투 2 / 보스 4. 레벨이 오르면 스탯 포인트 +4 를 골라서 올림
    public const int ExpEasy = 1, ExpHard = 2, ExpBoss = 4;
    public const int MaxLevel = 10;
    public const int StatPointsPerLevel = 4;
    // 3-164: 레벨 0 에서 시작 → 첫 전투(1칸)를 이기면 바로 1레벨 (경험치 1) → 10레벨(최대)까지 10번 오름 × 4점 = 스탯 40점
    // 다음 레벨까지 필요한 경험치 ([0] = 0→1레벨, [1] = 1→2레벨 …). 합계 33 (예전 32 + 첫 레벨 1)
    //   누적 (3-166): 1레벨 1 / 2레벨 3 / 3레벨 6 / 4레벨 9 / 5레벨 13 / 6레벨 17 / 7레벨 21 / 8레벨 25 / 9레벨 29 / 10레벨 33
    //   1·2스테이지 한 번에 얻는 경험치: 최소 8 (선택 전투 다 피함) ~ 보통 11 ~ 최대 15 (??? 에서 어려운 전투만)
    //   3스테이지 (3-84 층 +1): 최소 약 7 ~ 보통 약 10 ~ 최대 13 / 4스테이지 층마다 1 (엘리트 2)
    //   3-165 목표: 적당히 싸우면 4스테이지 1~2층에서 10레벨 (합계 33)
    //   → 보통(11+11+10 = 32) = 4스테이지 1층 / 조금 덜 싸움(30~31) = 2~3층 / 다 싸움(43) = 3스테이지 중간 / 거의 다 피함(23) = 4스테이지 보스 앞 8레벨
    // 3-166: { 1,2,2,3,3,4,4,4,5,5 } → { 1,2,3,3,4,4,4,4,4,4 } (합계 33 그대로). 앞쪽을 조금 느리게, 뒤쪽을 고르게
    //   모의 계산 20000판 (??? 는 무작위로 고름, 3스테이지 지도는 전투 약 60%): 평균 레벨 1스테이지 끝 4.1 / 2스테이지 끝 7.0 / 3스테이지 끝 9.2
    //   10레벨이 되는 곳: 3스테이지 안 28% / 4스테이지 1층 16% / 2층 13% / 3층 11% / 4~5층 16% / 보스 앞 못 찍음 15% → 가운데값 = 4스테이지 2층
    public static readonly int[] ExpToNext = { 1, 2, 3, 3, 4, 4, 4, 4, 4, 4 };
    public const int StartLevel = 0;   // 3-164: 런 시작 레벨

    // ---------------- 전투 칸의 웨이브 구성표 ----------------
    // 칸 하나에 웨이브가 여러 번 이어짐. 적을 다 잡으면 다음 웨이브가 바로 들어옴
    // (HP·손패·마나·에너지는 그대로 이어짐). 보상은 칸이 끝날 때 한 번
    // 글자 뜻: "N2" = 일반 2마리, "V1" = 정예 1마리, "E1" = 엘리트 1마리,
    //          "T1" = 까다로운 엘리트(무리), "B1" = 스테이지 보스, "A1" = 설계자
    // 목록 중 하나를 무작위로 뽑음. 줄을 추가하거나 고치면 바로 반영됨
    // 1스테이지 전용 (2026-11-09 플레이 피드백) — 슬더스처럼 초반은 "약한 조합", 대신 우리 게임은 수성이라
    //   한꺼번에 3마리가 아니라 1~2마리씩 "웨이브로 이어서" 몰려옴. 3마리 조합·감시자 무리는 2스테이지부터
    // 2026-11-15: 슬더스2 처럼 한 번에 나오는 무리 (웨이브 X). "N1+V1" = 한 웨이브에 일반 1 + 정예 1
    //   1스테이지는 한 번에 최대 2마리
    public static readonly string[][] Stage1EasyFights =
    {
        new[] { "N1" },
        new[] { "N2" },
        new[] { "N2" },
    };
    // 3-107: 1스테이지는 웨이브 없음 (2칸도 쉬운 전투 표를 씀). Stage1TwoWaveFights 는 지움
    // 3칸: 정예
    public static readonly string[][] Stage1VeteranFights =
    {
        new[] { "V1" },
        new[] { "N1+V1" },
    };
    public static readonly string[][] Stage1HardFights =
    {
        new[] { "E1" },
        new[] { "N1+E1" },
    };
    // 2·3스테이지
    // 3-110: "일반 전투" 는 일반 몬스터만 (정예가 섞인 "N2+V1" 은 정예 전투 표로)
    public static readonly string[][] EasyFights =
    {
        new[] { "N2" },
        new[] { "N3" },
        new[] { "N1" },
    };
    // 3-107: 웨이브는 2스테이지 2칸의 "일반 몬스터" 전투에만, 최대 2웨이브 (정예가 섞인 2웨이브 "N2 → V1" 은 지움)
    public static readonly string[][] TwoWaveFights =
    {
        new[] { "N2", "N1" },
        new[] { "N1", "N2" },
    };
    public static readonly string[][] VeteranFights =
    {
        new[] { "V2" },
        new[] { "N1+V1" },
        new[] { "N2+V1" },
    };
    public static readonly string[][] HardFights =
    {
        new[] { "E1" },
        new[] { "N1+E1" },
        new[] { "V1+E1" },
        new[] { "T1" },
    };
    // 3·4스테이지(지도) 전투는 웨이브 1번. 3-111: 몬스터 칸 = 일반만, 정예 칸 = 정예 전투 표
    public static readonly string[][] MapEasyFights =
    {
        new[] { "N2" },
        new[] { "N3" },
        new[] { "N1" },
    };
    // 3스테이지 정예 칸 (3-111)
    public static readonly string[][] MapVeteranFights =
    {
        new[] { "V1" },
        new[] { "N1+V1" },
        new[] { "V2" },
    };
    // 최종장(4스테이지) 첫 칸 "정예" 전투
    public static readonly string[][] FinalVeteranFights =
    {
        new[] { "V2" },          // 정예 2마리
        new[] { "N1+V1" },       // 일반 1 + 정예 1 (3-107: 4스테이지는 웨이브 없음 → 예전 "정예 1 → 정예 1" 2웨이브 대신)
    };

    public static readonly string[][] MapHardFights =
    {
        new[] { "E1" },
        new[] { "T1" },
    };

    // ---------------- 방어도 (수성의 여러 웨이브 칸) ----------------
    // 웨이브가 2번 이상인 칸에서는 내 턴 시작에 방어도가 0이 아니라 절반만 남음 (내림: 3→1, 5→2)
    // 웨이브 1번 칸과 4스테이지는 지금처럼 0
    public const float MultiWaveBlockKeepRatio = 0.5f;

    // ---------------- 4스테이지 지도 ----------------
    // 잘 고르면 1층만 싸우고 보스까지 갈 수 있는 길이 최소 1개 생기게 (없으면 지도를 다시 만듦)
    public const bool MapRequireSafePath = true;
    public const int MapGenerateTries = 60;   // 조건에 맞는 지도를 만들 때까지 다시 해 보는 최대 횟수

    // 길의 성격: 지도를 만들 때 세로줄마다 하나씩 정해짐 → 같은 줄을 따라가면 비슷한 칸이 많이 나옴
    //   위험 = 몬스터·엘리트가 많음 (보상은 나중에 더 좋게)
    //   평온 = 이벤트·보물·상점이 많음
    //   보통 = 골고루
    // 칸 종류별 가중치 순서: 몬스터, 엘리트, 모닥불, 보물, 이벤트, 상점, 대장간, 보스(항상 0), 정예
    // 2026-11-15: 이벤트(5번째) 삭제 = 0, 그 몫을 몬스터·모닥불·보물·대장간에 나눔
    // 3-111: 정예(9번째) 추가. 몬스터 몫에서 나눔 (보통 52 → 40 + 12, 위험 60 → 44 + 16, 평온 22 → 18 + 4)
    public static readonly int[] WeightsNormal = { 40, 10, 12, 12, 0, 6, 8, 0, 12 };
    public static readonly int[] WeightsDanger = { 44, 25, 8, 3, 0, 2, 2, 0, 16 };
    public static readonly int[] WeightsCalm   = { 18, 3, 16, 35, 0, 10, 14, 0, 4 };

    public const int ReviveHp = 1;   // 전투 중 쓰러진 플레이어는 승리 후 HP 1로 부활

    // ---------------- 인원수별 몬스터 스케일링 ----------------
    // 배열 칸 번호 = 인원수 - 1  (0번 칸 = 1인, 1번 칸 = 2인 ...)
    // 적은 플레이어 "전원"을 공격하므로 공격력은 올리지 않습니다 (슬더스2 방식).
    // 대신 때리는 사람이 늘어난 만큼 체력과 방어도를 올립니다.
    // 협동이 너무 쉬우면 체력을 2.2배처럼 조금 더 올려 보세요.
    public static readonly float[] EnemyHpScale = { 1.0f, 2.0f, 3.0f, 4.0f };
    public static readonly float[] EnemyBlockScale = { 1.0f, 2.0f, 3.0f, 4.0f };

    // ---------------- 몬스터 (1인, 1스테이지 기준) — 2026-11-08 재조정 ----------------
    // 기준: 플레이어 1명이 한 턴에 약 20~30 피해 → 일반 1마리 1~2턴, 정예 2~3턴, 엘리트 4~5턴, 보스 8턴 안팎
    //       웨이브가 이어지는 칸은 합계로 생각: 쉬운 칸(일반 2 → 정예 1) ≈ 90, 어려운 칸 ≈ 130~150
    // 적이 플레이어에게 거는 취약은 "2" 로 넣어야 다음 적 차례 공격에 적용됨 (플레이어 취약은 적 차례 시작에 1 감소)
    // 플레이어에게 거는 약화는 "1" = 다음 내 턴 한 번
    public const int EnemyVulnerableTurns = 2;
    public const int EnemyWeakTurns = 1;

    // 균열 슬라임 (일반): 몸통 박치기 ↔ 끈적한 점액(약화)
    public const int SlimeHp = 24;
    public const int SlimeTackle = 7;
    public const int SlimeGooHit = 3;
    public const int SlimeGooBlock = 5;
    // 연쇄 박쥐 (일반): 연쇄 물기 → 흡혈 → 날개 치기
    public const int BatHp = 20;
    public const int BatBite = 3;
    public const int BatBiteHits = 2;
    public const int BatDrain = 5;
    public const int BatDrainHeal = 4;
    public const int BatWing = 6;
    // 오염된 병정 (정예): 베기 → 방패 들기 → 오염된 함성(힘) → 강타
    public const int SoldierHp = 44;
    public const int SoldierSlash = 9;
    public const int SoldierShieldHit = 4;
    public const int SoldierShieldBlock = 8;
    public const int SoldierShout = 2;         // 힘 +2
    public const int SoldierSmash = 13;
    // 철갑 골렘 (정예): 강철화 → 철권 → 과열(힘+방어) → 철벽 반격
    public const int GolemHp = 40;
    public const int GolemHarden = 12;
    public const int GolemPunch = 11;
    public const int GolemOverheat = 2;        // 힘 +2
    public const int GolemOverheatBlock = 5;
    public const int GolemGuardHit = 6;
    public const int GolemGuardBlock = 8;

    // 엘리트 감시자: 응시(방어+취약) → 광선 → 분석(약화) → 이중 광선
    public const int WatcherHp = 85;
    public const int WatcherGazeBlock = 10;
    public const int WatcherBeam = 15;
    public const int WatcherScan = 7;
    public const int WatcherDoubleBeam = 8;    // x2

    // ---------------- 새 몬스터 (2026-11-15, 슬더스2 처럼 여러 방식 + 디버프) ----------------
    // 손상(Frail): 얻는 방어도 -25%. 상처: 쓸 수 없는 카드가 버린 더미에 들어와서 손패를 막음
    public const float FrailReduction = 0.25f;
    // 일반
    public const int RatHp = 18, RatGnaw = 4, RatGnawHits = 2, RatBite = 6, RatWounds = 1;                 // 역병 쥐: 연타 / 물기 + 상처 1장
    public const int ImpHp = 20, ImpEmber = 4, ImpEmberBurn = 3, ImpFlap = 6, ImpBreath = 8;                // 불씨 임프: 피해 + 발화 / 방어 / 숨결
    public const int SpiderHp = 22, SpiderFang = 7, SpiderPounce = 5, SpiderFrail = 1;                      // 그물 거미: 거미줄(약화 + 카드 -1) / 독니 / 덮치기 + 손상
    public const int CultistHp = 26, CultistRitual = 2, CultistDagger = 5;                                  // 광신도: 의식(매 턴 힘 +2) 뒤 단검만
    // 정예
    public const int SlingerHp = 42, SlingerRock = 10, SlingerFrail = 2, SlingerShield = 10, SlingerVolley = 4, SlingerVolleyHits = 3;   // 오크 투석병
    public const int ThornKnightHp = 46, ThornKnightThorns = 3, ThornKnightBlock = 8, ThornKnightStab = 9, ThornKnightCharge = 13;       // 가시 갑주병
    // 엘리트
    public const int ShamanHp = 80, ShamanCurseWounds = 2, ShamanArrow = 6, ShamanArrowBurn = 2, ShamanRitual = 2, ShamanRitualBlock = 12, ShamanDrain = 12, ShamanDrainHeal = 8;   // 역병 주술사
    public const int AssassinHp = 75, AssassinStab = 3, AssassinStabHits = 4, AssassinSmoke = 14, AssassinVital = 16, AssassinVitalFrail = 2;                                       // 그림자 암살자
    // 까다로운 엘리트: 감시자 무리 (3마리)
    public const int SquadEyeHp = 30;
    public const int SquadBeam = 8;
    public const int SquadFocusBeam = 12;
    public const int SquadAllyBlock = 8;
    public const int SquadWeakBeam = 4;
    public const float SquadRageBonus = 0.3f;  // 동료가 쓰러질 때마다 남은 놈 공격력 +30%

    // ---------------- 3-102 새 몬스터 (몬스터 통합 도감) ----------------
    // HP 는 도감 그대로(1인·1스테이지). 공격 숫자는 기존 몬스터와 비슷한 세기로 정함
    public const int MaxWaveEnemies = 5;       // 한 웨이브 최대 몬스터 수 (무리 몬스터가 슬롯 하나를 차지. 넘으면 1마리짜리로 다시 뽑음)
    public const int BurnCardHpLoss = 2;       // [화상] 카드: 턴 종료 때 손에 있으면 HP -2
    public const int SlimeCardCost = 1;        // 3-149 [점액] 카드: 에너지 1 을 내면 소멸
    public const int SlimeGooCards = 1;        // 3-149 균열 슬라임 "끈적한 점액": 플레이어마다 [점액] 1장 (버린 카드 더미)
    public const int VoidCardEnergyLoss = 1;   // 3-149 [공허] 카드: 뽑으면 에너지 -1
    public const int MoonVoidCards = 1;        // 3-149 달의 석상 "월광 저주": 플레이어마다 [공허] 1장 (버린 카드 더미)
    // 일반
    public const int BeetleHp = 22, BeetleRam = 8, BeetleCurlHit = 5, BeetleCurlBlock = 6, BeetleShellBlock = 5;   // 투구 딱정벌레: 매 턴 처음 맞으면 다음 적 차례에 방어도 +5
    public const int SporeHp = 16, SporeSpray = 4, SporeBite = 7, SporeDeathWeak = 1;                              // 기생 포자충: 쓰러지면 모두 약화 1 + 뽑을 더미 맨 위에 [포자]
    public const int ClayHp = 32, ClayPunch = 9, ClayHarden = 8, ClaySmallHit = 5;                                  // 점토 분열체: HP 50% 이하면 작은 점토체 2마리로 분열
    public const float ClaySplitRatio = 0.5f;
    public const int LanternHp = 19, LanternFire = 6, LanternSwing = 3, LanternSwingHits = 2, LanternStrength = 1;  // 망령 등불지기: 카드를 소멸·버릴 때마다 힘 +1
    public const int EmberSpiritHp = 10, EmberSpark = 3, EmberBlast = 14, EmberBlastBurnCards = 1;                  // 자폭 불씨 정령 (3마리): 3번째 행동에 자폭
    public const int LarvaHp = 7, LarvaBite = 1;                                                                    // 점액 유충 (4마리): 물기 1 + 살아 있는 유충 수
    public const int ApprenticeHp = 8, ApprenticeDagger = 3, ApprenticeDaggerHits = 2, ApprenticeShield = 5, ApprenticeHex = 3;   // 광신도 수련생 (3명: 단검·방패·주술)
    // 정예
    public const int BoneHp = 45, BoneArmorBlock = 14, BoneArmorHit = 5, BoneStormMin = 6;                          // 뼈조각 수집가: 남은 방어도만큼 공격 (최소 6)
    public const int SkelArcherHp = 24, SkelShieldHp = 26, SkelVolley = 5, SkelVolleyHits = 2, SkelAimedShot = 11,
                     SkelShieldAlly = 6, SkelShieldBash = 7;                                                        // 해골 사수 & 방패병
    public const float SkelStealthReduction = 0.5f;                                                                 //   방패병이 살아 있으면 사수가 받는 공격 피해 -50%
    public const int GoblinChiefHp = 18, GoblinSpearHp = 16, GoblinNetHp = 16, GoblinChiefSlash = 7, GoblinChiefRally = 2,
                     GoblinJavelin = 6, GoblinJavelinFrail = 1, GoblinNet = 3, GoblinStab = 6;                      // 고블린 약탈대 (대장은 동료 힘 +2)
    // 엘리트
    public const int SentinelHp = 88, SentinelFist = 13, SentinelGuardBlock = 14, SentinelGuardHit = 6, SentinelSlam = 8, SentinelSlamHits = 2;
    public const int SentinelOverloadHits = 4, SentinelOverloadVuln = 2;                                            // 고대 석상 파수꾼: 한 턴에 4번 맞으면 과부하 (다음 행동 취소 + 취약 2)
    public const int WitchHp = 78, WitchArrow = 9, WitchDrain = 7, WitchDrainHits = 2, WitchVeilBlock = 12;
    public const int WitchBindHeal = 8, WitchBindStrength = 2;                                                     // 영혼 결속 마녀: 결속 카드를 안 내면 회복 8 + 힘 2
    public const int HandlerHp = 42, BeastHp = 44, HandlerWhip = 8, HandlerCommand = 2, HandlerTrap = 5,
                     BeastBite = 10, BeastClaw = 4, BeastClawHits = 3;
    public const int BeastFrenzyStrength = 5, HandlerFearTurns = 3;                                                 // 조련사가 쓰러지면 맹수 힘 +5 / 맹수가 쓰러지면 조련사 약화·취약 3
    // 보스 (1~2스테이지)
    public const int ShellBossHp = 185, ShellClaw = 12, ShellCurlBlock = 15, ShellAcid = 6, ShellAcidFrail = 1,
                     ShellRageClaw = 9, ShellRageClawHits = 2, ShellRoarStrength = 3, ShellShard = 18;
    public const float ShellArmorRatio = 0.35f;                                                                     // 심연의 거대 껍질: 외골격 = 최대 HP 의 35% (이만큼 HP 를 깎으면 파괴)
    public const float ShellReduction = 0.5f;                                                                       //   외골격이 있는 동안 받는 공격 피해 -50%
    public const int KingHp = 175, KingGuardHp = 15, KingStrike = 13, KingHarvest = 6, KingHarvestHits = 2, KingHarvestHeal = 10,
                     GuardSpear = 6, GuardAllyBlock = 5;
    public const int KingReviveTurns = 2;                                                                           // 잊혀진 왕의 망령: 근위병이 쓰러지면 적 차례 2번 뒤 부활 (왕이 살아 있을 때)
    // 보스 (3스테이지)
    public const int RiftBossHp = 190, RiftBeam = 14, RiftGazeHit = 5, RiftGazeHits = 3, RiftStormHit = 7, RiftStormHits = 3,
                     RiftGateBlock = 20, RiftGateStrength = 3, RiftCollapse = 22, RiftExhaustEvery = 2;             // 차원 균열의 감시자: 2턴마다 손패 1장 소멸
    public const int TwinStatueHp = 105, SunBeam = 12, SunBurnCards = 1, SunScorch = 6, SunScorchBurn = 3,
                     MoonVeilAlly = 10, MoonVeilBlock = 8, MoonCurse = 5;
    public const int TwinReviveTurns = 2;                                                                           // 심판의 쌍둥이 석상: 한쪽이 쓰러지면 적 차례 2번 뒤 부활
    public const float TwinReviveHpRatio = 0.5f;                                                                    //   부활 HP = 최대 HP 의 50%

    // ---------------- 스테이지 보스 (종족마다 다른 수문장) ----------------
    // 모두 HP 50% 이하에서 2페이즈 (패턴 변경 + 방어도)
    // 웨어울프족: 광폭한 수문장 — 연타 + 힘이 계속 오름 (빨리 잡아야 함)
    public const int WolfBossHp = 170;
    public const int WolfClaw = 5;             // x3
    public const int WolfHowlStrength = 2;
    public const int WolfBite = 14;
    public const int WolfBiteHeal = 8;
    public const int WolfEnrageBlock = 15;
    public const int WolfFrenzy = 6;           // x4
    public const int WolfBloodHowl = 3;
    public const int WolfHunt = 22;
    // 오크족: 철퇴 수문장 — 큰 한 방 + 두꺼운 방패 (방어도 관통·무시 카드가 빛남)
    public const int OrcBossHp = 180;
    public const int OrcSmash = 18;
    public const int OrcShieldWall = 22;
    public const int OrcWarCryHit = 6;
    public const int OrcWarCryStrength = 2;
    public const int OrcEnrageBlock = 25;
    public const int OrcQuake = 26;
    public const int OrcCounterHit = 12;
    public const int OrcCounterBlock = 18;
    public const int OrcRampage = 8;           // x3
    // 좀비족: 부패한 수문장 — 약화·취약 + 회복 (오래 끌면 불리)
    public const int ZombieBossHp = 165;
    public const int ZombieClaw = 11;
    public const int ZombieFeastHeal = 15;
    public const int ZombieFeastBlock = 10;
    public const int ZombieEnrageBlock = 15;
    public const int ZombiePlague = 15;
    public const int ZombieRegenHeal = 20;
    public const int ZombieRegenStrength = 2;
    public const int ZombieGrasp = 6;          // x3

    // ---------------- 최종 보스: 설계자 (4스테이지) ----------------
    // 1페이즈: 설계 수정 → 구조 보강(방어+힘) → 오류 주입(취약+약화)
    // 2페이즈: 재설계 → 붕괴 연쇄 → 자가 복구 → 오류 주입
    public const int ArchitectHp = 210;
    public const float ArchitectEnrageRatio = 0.5f;
    public const int ArchitectEnrageBlock = 25;
    public const int ArchitectStrike = 14;
    public const int ArchitectWallBlock = 20;
    public const int ArchitectWallStrength = 2;
    public const int ArchitectPressure = 8;
    public const int ArchitectRageStrike = 20;
    public const int ArchitectFlurry = 6;       // x4
    public const int ArchitectFlurryHits = 4;
    public const int ArchitectRepairHeal = 25;
    public const int ArchitectRepairBlock = 15;
    public const float BossEnrageRatio = 0.5f;  // 스테이지 보스 공통

    // ================= 3-176: 새 아티팩트 45개 (아티팩트 표 v6) =================
    // 보스 아티팩트 중 "매 턴 에너지 +1" 이 적힌 것만 에너지를 줌 (RelicData.GivesEnergy). '보스 승격' 은 에너지 없이 효과가 큼
    // --- 공용 보스 ---
    public const int MedalFirstDraw = 2, MedalLaterDrawLoss = 1;            // 작전 지휘관의 훈장
    public const float ChaliceGoldBonus = 0.35f, ChaliceCardPrice = 1.2f;   // 황금 성배
    public const float OnslaughtBonus = 0.20f; public const int OnslaughtSelfVuln = 2;   // 맹공의 투구
    public const int PurifyHourglassDraw = 1, PurifyHourglassMax = 2, PurifyHourglassHpLoss = 1;   // 정화의 모래시계
    public const int DiceEnergy = 2, DiceDrawLoss = 1;                      // 도박사의 주사위 (50%)
    public const int FlagMaxDraw = 2, FlagFirstDrawLoss = 1;                // 작전 지휘관의 깃발
    public const float TorchBonus = 0.15f; public const int TorchBlock = 5, TorchHpLoss = 2;   // 결속의 횃불 (파티 보스, 멀티)
    // --- 공용 일반·희귀·영웅 ---
    public const int SwiftBootsDraw = 1, SwiftBootsBlock = 4;               // 신속의 부츠
    public const float AlchemistStillBonus = 0.30f;                         // 연금술사의 증류기
    public const int TacticianEnergy = 2, TacticianDraw = 2;                // 전술가의 지도 (엘리트·보스 첫 턴)
    public const int SharpShardDamage = 2, SharpShardBlockLoss = 1;         // 예리한 칼날 조각
    public const int OverheatedDamage = 5, OverheatedHpLoss = 1;            // 과열된 마력석
    public const int SwiftGreavesEvery = 3;                                 // 신속의 깃털신: 3장째마다 1장
    public const float JaggedIgnore = 0.25f;                                // 날카로운 톱니 조각
    public const int SuperheatedDamage = 2, SuperheatedMaxCards = 4;        // 고열의 마도석
    public const int PurifyTimerUses = 3;                                   // 희석된 정화 시계 (전투당)
    public const int GamblerCoinChance = 50;                                // 도박사의 동전
    // --- 파티 ---
    public const int ComradeShieldBlock = 15;                               // 전우애의 방패
    public const int UnityHelmHp = 5, UnityHelmBlock = 5;                   // 결속의 투구
    public const float ComradeCrestRevive = 0.25f;                          // 전우애의 문장
    // --- 검은 낫 ---
    public const int SoulbondHaftMaxLoss = 5;                               // 영혼 결속의 낫자루
    public const float ReaperEdgeBonus = 0.25f; public const int ReaperEdgeSoul = 2; public const float ReaperEdgeFirstBlock = 0.5f;   // 수확자의 낫끝
    public const int HungryDollDamage = 2, HungryDollSoul = 1, HungryDollHpLoss = 3;   // 굶주린 그림자 인형
    public const int ShadowRingDamage = 4, ShadowRingDraw = 1; public const float ShadowRingDayBlock = 0.75f;   // 그림자 수확자의 반지
    public const int HungryMaskBlock = 3, HungryMaskMaxLoss = 3;            // 굶주린 그림자 가면
    // --- 피닉스 ---
    public const float PhoenixScaleBonus = 0.20f;                           // 불사조의 역린
    public const int EmberMatchBurn = 5, EmberMatchFlame = 2, EmberMatchEnergyLoss = 1;   // 잿불의 성냥
    public const int HotBrazierBurn = 1, HotBrazierFlame = 1, HotBrazierHpLoss = 3;       // 뜨거운 화로 조각
    public const int AshCrownBlockPerFlame = 2, AshCrownMaxBlock = 12, AshCrownFlameLoss = 1;   // 불사조의 잿불 왕관
    public const int EmberCoreFlame = 2; public const float EmberCoreHpLoss = 0.10f;      // 꺼지지 않는 불씨핵
    public const int HotFlintDamage = 3;                                    // 뜨거운 불씨 부싯돌
    // --- 스나이퍼 ---
    public const float WarheadBonus = 0.30f; public const int WarheadRegenLoss = 1;       // 정밀 탄두 상자
    public const int RapidMagFree = 2, RapidMagMaxLoss = 1;                 // 속사 탄창
    public const int LightSuppressorDamage = 2, LightSuppressorBayonetLoss = 1;   // 가벼운 소음기 부품
    public const float BallisticBonus = 0.25f; public const int BallisticBayonetCost = 1;   // 초정밀 탄도 모듈
    public const int LightPouchBlock = 6, LightPouchDraw = 1, LightPouchStartLoss = 1;    // 경량 탄창 파우치
    public const int LightLensMarks = 5, LightLensBullets = 1;              // 가벼운 조준경 렌즈
    // --- 마도사 ---
    public const int GlacialHeartMana = 3, GlacialHeartFrostbiteLoss = 2;   // 만년설의 심장
    public const float FrostStaffBonus = 0.20f; public const int FrostStaffRegenLoss = 2;   // 서리 결정 지팡이
    public const int FrostShardDamage = 2, FrostShardMaxLoss = 3;           // 서리 파편 반지
    public const int AbyssOrbShock = 1, AbyssOrbBlock = 5, AbyssOrbMaxLoss = 10;           // 빙결 심연의 보주
    public const int CrystalOrbChance = 50, CrystalOrbMana = 5, CrystalOrbDraw = 1, CrystalOrbMaxProcs = 2, CrystalOrbMaxLoss = 10;   // 서리 마력의 수정구
    public const int FrostRingBlock = 1, FrostRingMaxPerTurn = 6;           // 서리 결정 반지

    // ---------------- 3-182 전투 연출 (화면 효과만, 규칙과 상관없음) ----------------
    public const int FxMediumHit = 10;                 // 이 피해 이상이면 짧은 히트스톱 + 충격 고리
    public const int FxHeavyHit = 20;                  // 이 피해 이상이면 금빛 큰 숫자 + 하얀 번쩍임 + 조금 긴 히트스톱
    public const float FxHitStopMedium = 0.04f;        // 히트스톱 시간 (초, 실제 시간)
    public const float FxHitStopHeavy = 0.07f;
    public const float FxHitStopKill = 0.12f;          // 적을 쓰러뜨린 순간
    public const float FxCardShowTime = 0.45f;         // 낸 카드를 가운데에 보여 주는 시간 (원래 0.45 그대로)
    public const float FxVictoryIntro = 0.45f;         // 승리 창이 커지며 나타나는 시간

    // ---------------- 3-196 난이도 (쉬움 · 보통 · 어려움 · 매우 어려움 · 지옥) ----------------
    // 보통 = 지금 밸런스 그대로. 어려움부터는 아래 단계 효과를 모두 포함 (쉬움은 따로)
    public const float EasyEnemyHp = 0.85f;            // 쉬움: 몬스터 체력 -15%
    public const float EasyEnemyAttack = 0.9f;         // 쉬움: 몬스터 공격력 -10%
    public const float EasyRestHeal = 0.4f;            // 쉬움: 휴식 회복 30% → 40%
    public const float EasyGold = 1.2f;                // 쉬움: 골드 +20%
    public const float EasyExp = 0.85f;                // 쉬움: 다음 레벨까지 필요 경험치 -15%
    public const float HardGold = 0.85f;               // 어려움~: 골드 -15%
    public const float HardExp = 1.15f;                // 어려움~: 필요 경험치 +15%
    public const int HardEliteWeight = 2;              // 어려움~: 지도 엘리트 칸 가중치 ×2 (수성은 3칸·5칸 정예 → 엘리트)
    public const int VeryHardRestDivide = 2;           // 매우 어려움~: 지도 모닥불 가중치 ÷2, 수성 [초소] 선택지 없음 (9칸 보스 앞 초소는 그대로)
    public const float VeryHardNormalHp = 1.1f;        // 매우 어려움~: 일반 몬스터 체력 +10%
    public const float VeryHardEliteHp = 1.15f;        // 매우 어려움~: 정예·엘리트 체력 +15% (보스는 안 오름)
    public const float HellBossHp = 1.2f;              // 지옥: 보스 체력 +20%
    public const float HellShopPrice = 1.2f;           // 지옥: 상점 가격 +20%
    public const int HellSealDeckDivide = 4;           // 지옥: 전투마다 사용 금지 카드 = 스테이지 수 (1~4장), 단 덱의 1/4 까지

    // ==================== 3-200 보스·엘리트 등장 연출 (초) ====================
    public const float FxIntroDelay = 0.2f;    // 전투 시작 → 땅에서 붉은 기운이 솟을 때까지
    public const float FxIntroReveal = 0.45f;  // 기운이 솟고 → 몬스터가 나타나기 시작할 때까지
    public const float FxIntroFade = 0.35f;    // 몬스터가 투명 → 다 보일 때까지
}
