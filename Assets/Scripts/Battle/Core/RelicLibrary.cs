// ============================================================
// RelicLibrary.cs
// 아티팩트(유물) 목록 (2026-11-09 아티팩트 기획서 + Claude 수정본)
//
// 아티팩트 = "가지고 있으면 계속 효과가 나는 물건". 런 동안 유지됨
// 이 파일에는 이름·등급·설명만 있음. 실제 효과는 규칙 쪽에서 아이디로 확인함
//   예) if (p.HasRelic(RelicLibrary.Hourglass)) p.GainMana(3);   (BattleSystem 의 전투 시작)
//   → 새 아티팩트 = 여기 한 줄 + 효과가 나는 곳에 if 한 줄
// 아이디를 상수(const string)로 둔 이유: 글자를 잘못 쓰면 컴파일 오류가 나서 바로 알 수 있음
//   (자바의 public static final String 과 같음)
// ============================================================

using System.Collections.Generic;
using B = GameBalance;

public enum RelicRarity
{
    Starter,   // 직업 시작 아티팩트 (런 시작에 자동)
    Common,    // 일반
    Special,   // 특별 (직업 전용. 보상 확률은 일반과 같음)
    Rare,      // 희귀
    Hero,      // 영웅
    Boss,      // 보스 (에너지 +1 + 페널티, 보스 처치 시 3개 중 1개)
    Party      // 공용 (2026-11-15): 파티 전원에게 효과. 보물 상자에서만 나옴. 파티에 1개씩만
}

public class RelicData
{
    public string Id;
    public string Name;
    public RelicRarity Rarity;
    public PlayerClass ClassOnly;   // Common 이면 모든 직업. 아니면 그 직업만
    public string Description;      // 효과
    public string Penalty;          // 페널티 (없으면 빈 글자)
    // 3-176: 보스 아티팩트가 "매 턴 에너지 +1" 을 주는지. 기본 = 보스면 true. '보스 승격' 아티팩트는 { GivesEnergy = false }
    //   (C# 객체 초기화 문법: new RelicData(...) { GivesEnergy = false } = 만든 직후 필드 하나를 바꿈. 자바라면 생성 뒤 setter 호출)
    public bool GivesEnergy;
    public bool MultiOnly;          // 3-176: 멀티(2명 이상)에서만 나옴 (결속의 횃불)

    public RelicData(string id, string name, RelicRarity rarity, PlayerClass classOnly, string description, string penalty = "")
    {
        Id = id;
        Name = name;
        Rarity = rarity;
        ClassOnly = classOnly;
        Description = description;
        Penalty = penalty;
        GivesEnergy = rarity == RelicRarity.Boss;
    }

    public bool HasPenalty { get { return Penalty.Length > 0; } }

    public string RarityName()
    {
        switch (Rarity)
        {
            case RelicRarity.Starter: return "시작";
            case RelicRarity.Common: return "일반";
            case RelicRarity.Special: return "특별";
            case RelicRarity.Rare: return "희귀";
            case RelicRarity.Hero: return "영웅";
            case RelicRarity.Party: return "공용";
            default: return "보스";
        }
    }

    // 화면 설명 한 덩어리 (효과 + 페널티)
    public string FullText()
    {
        return Description + (HasPenalty ? "\n페널티: " + Penalty : "");
    }
}

public static class RelicLibrary
{
    // ---- 아이디 ----
    // 시작
    public const string ReaperMark = "reaper_mark";
    public const string PhoenixPlume = "phoenix_plume";
    public const string FrostPendant = "frost_pendant";
    // 공용
    public const string RationPouch = "ration_pouch";
    public const string Rampart = "rampart_stone";
    public const string WarHorn = "war_horn";
    public const string GuardHelm = "guard_helm";
    public const string Arrowhead = "silver_arrowhead";
    public const string Whetstone = "whetstone";
    public const string Lantern = "miner_lantern";
    public const string SteelBelt = "steel_belt";
    public const string OldShield = "old_shield";
    public const string AlchemistMug = "alchemist_mug";
    // 보스
    public const string TyrantCrown = "tyrant_crown";
    public const string BloodContract = "blood_contract";
    public const string IronKey = "iron_key";
    // 검은 낫
    public const string Ledger = "exec_ledger";
    public const string Hourglass = "reaper_hourglass";
    public const string Pendant = "crescent_pendant";
    public const string NecroSkull = "necro_skull";
    public const string SoulLantern = "soul_lantern";            // 3-99 추가 (아티팩트 표 v2)
    public const string ChippedScythe = "chipped_scythe";
    public const string ShadowCloak = "shadow_cloak";
    public const string SpiritCenser = "spirit_censer";
    public const string BloodCandelabra = "blood_candelabra";
    public const string AbyssalScythe = "abyssal_scythe";        // 보스
    public const string TwilightRing = "twilight_ring";          // 보스
    // 피닉스
    public const string UndyingAsh = "undying_ash";
    public const string LavaCrystal = "lava_crystal";
    public const string BlazingCore = "blazing_core";
    public const string HellfireHeart = "hellfire_heart";
    public const string WarmFeather = "warm_feather";            // 3-99 추가
    public const string EmberPouch = "ember_pouch";
    public const string AshSalve = "ash_salve";
    public const string PhoenixTear = "phoenix_tear";
    public const string ScorchingFlint = "scorching_flint";
    public const string SolarPlume = "solar_plume";              // 보스
    public const string EternalBrazier = "eternal_brazier";      // 보스
    // 마도사
    public const string FrostCompass = "frost_compass";
    public const string Capacitor = "overload_capacitor";
    public const string Permafrost = "permafrost";
    public const string ZeroShard = "zero_shard";
    public const string IceBrooch = "ice_brooch";                // 3-101 추가 (아티팩트 표 v3)
    public const string StaticRing = "static_ring";
    public const string FrostwindCloak = "frostwind_cloak";
    public const string FrostreignTome = "frostreign_tome";
    public const string GlacialCrown = "glacial_crown";          // 보스
    public const string StormcoreStaff = "stormcore_staff";      // 보스
    // 스나이퍼 (2026-11-15)
    public const string CasingCharm = "casing_charm";
    public const string RustedBelt = "rusted_ammo_belt";
    public const string BrokenScope = "broken_scope";
    public const string Crossguard = "balanced_crossguard";
    public const string GunpowderRing = "gunpowder_ring";
    public const string InfraredSensor = "infrared_sensor";
    public const string VeteranMag = "veteran_magazine";
    public const string RedWhisper = "red_whisper";
    public const string DeadlyObserver = "deadly_observer";
    public const string TacticalGloves = "tactical_gloves";
    public const string Suppressor = "tactical_suppressor";      // 3-99 추가
    public const string Rangefinder = "rangefinder";

    // 공용 (파티 전원 효과, 보물 상자 전용) - 2026-11-15
    public const string WarBanner = "war_banner";
    public const string MedicBag = "medic_bag";
    public const string CommanderBugle = "commander_bugle";
    public const string AllianceOath = "alliance_oath";
    public const string SiegeRam = "siege_ram";
    public const string SupplyWagon = "supply_wagon";
    public const string GuardLamp = "guard_lamp";
    public const string BondStone = "bond_stone";

    // ---- 3-176 새 아티팩트 (아티팩트 표 v6). 아이디 = 아이콘 파일 이름에서 "Relic_" 을 뺀 것 ----
    public const string CommanderMedal = "commander_medal";      // 공용 보스
    public const string GoldenChalice = "golden_chalice";
    public const string OnslaughtHelm = "onslaught_helm";
    public const string PurifyHourglass = "purify_hourglass";
    public const string GamblerDice = "gambler_dice";
    public const string CommanderFlag = "commander_flag";
    public const string GoldenFlask = "golden_flask";
    public const string BondTorch = "bond_torch";                // 파티 보스 (멀티)
    public const string SwiftBoots = "swift_boots";              // 공용 일반·희귀·영웅
    public const string AlchemistStill = "alchemist_still";
    public const string TacticianMap = "tactician_map";
    public const string SharpShard = "sharp_shard";
    public const string OverheatedStone = "overheated_stone";
    public const string SwiftGreaves = "swift_greaves";
    public const string JaggedShard = "jagged_shard";
    public const string SuperheatedStone = "superheated_stone";
    public const string PurifyTimer = "purify_timer";
    public const string GamblerCoin = "gambler_coin";
    public const string ComradeShield = "comrade_shield";        // 파티
    public const string UnityHelm = "unity_helm";
    public const string ComradeCrest = "comrade_crest";
    public const string SoulbondHaft = "soulbond_haft";          // 검은 낫
    public const string ReaperEdge = "reaper_edge";
    public const string HungryDoll = "hungry_doll";
    public const string SoulbondBlade = "soulbond_blade";
    public const string ShadowReaperRing = "shadow_reaper_ring";
    public const string HungryMask = "hungry_mask";
    public const string PhoenixScale = "phoenix_scale";          // 피닉스
    public const string EmberMatch = "ember_match";
    public const string HotBrazierShard = "hot_brazier_shard";
    public const string AshCrown = "ash_crown";
    public const string EmberCore = "ember_core";
    public const string HotFlint = "hot_flint";
    public const string PrecisionWarhead = "precision_warhead";  // 스나이퍼
    public const string RapidMagazine = "rapid_magazine";
    public const string LightSuppressor = "light_suppressor";
    public const string BallisticModule = "ballistic_module";
    public const string LightPouch = "light_pouch";
    public const string LightLens = "light_lens";
    public const string GlacialHeart = "glacial_heart";          // 마도사
    public const string FrostStaff = "frost_staff";
    public const string FrostShardRing = "frost_shard_ring";
    public const string AbyssOrb = "abyss_orb";
    public const string FrostCrystalOrb = "frost_crystal_orb";
    public const string FrostRing = "frost_ring";

    // 전체 목록 (static readonly = 프로그램이 시작될 때 한 번 만들어짐. 자바의 static final List)
    public static readonly List<RelicData> All = new List<RelicData>
    {
        // ---------------- 시작 (직업별 1개) ----------------
        new RelicData(ReaperMark, "낡은 사신의 표식", RelicRarity.Starter, PlayerClass.BlackScythe,
            "전투 시작 시 무작위 적 1명에게 처형 스택 " + B.ReaperMarkStacks),
        new RelicData(PhoenixPlume, "불새의 깃털 장식", RelicRarity.Starter, PlayerClass.CosmicPhoenix,
            "전투 시작 시 홍염 +" + B.PhoenixPlumeFlame),
        new RelicData(FrostPendant, "서리 수정 목걸이", RelicRarity.Starter, PlayerClass.FrostMage,
            "전투 시작 시 모든 적에게 동결 " + B.FrostPendantFreeze),
        new RelicData(CasingCharm, "진홍 탄피 부적", RelicRarity.Starter, PlayerClass.Sniper,
            "전투 시작 시 [조준] +" + B.CasingCharmAim),

        // ---------------- 공용 (파티 전원, 보물 상자 전용) ----------------
        new RelicData(WarBanner, "전우의 깃발", RelicRarity.Party, PlayerClass.Common,
            "[파티] 전투 시작 시 모두 방어도 +" + B.WarBannerBlock),
        new RelicData(MedicBag, "야전 의무병 가방", RelicRarity.Party, PlayerClass.Common,
            "[파티] 전투에서 이기면 모두 HP +" + B.MedicBagHeal),
        new RelicData(CommanderBugle, "지휘관의 나팔", RelicRarity.Party, PlayerClass.Common,
            "[파티] 첫 턴에 모두 카드 +" + B.CommanderBugleDraw + "장"),
        new RelicData(AllianceOath, "동맹의 맹세", RelicRarity.Party, PlayerClass.Common,
            "[파티] 첫 턴에 모두 에너지 +" + B.AllianceOathEnergy),
        new RelicData(SiegeRam, "공성 망치", RelicRarity.Party, PlayerClass.Common,
            "[파티] 전투 시작 시 모든 적 취약 " + B.SiegeRamVuln),
        new RelicData(SupplyWagon, "보급 마차", RelicRarity.Party, PlayerClass.Common,
            "[파티] 전투에서 이기면 모두 골드 +" + B.SupplyWagonGold),
        new RelicData(GuardLamp, "수호의 등불", RelicRarity.Party, PlayerClass.Common,
            "[파티] 매 턴 시작 시 HP 비율이 가장 낮은 아군 HP +" + B.GuardLampHeal),
        new RelicData(BondStone, "결속의 돌", RelicRarity.Party, PlayerClass.Common,
            "[파티] 얻는 즉시 모두 최대 HP +" + B.BondStoneHp),

        // ---------------- 공용 (일반 아티팩트: 어느 직업이나) ----------------
        new RelicData(RationPouch, "수비대장의 식량 주머니", RelicRarity.Common, PlayerClass.Common,
            "웨이브를 하나 클리어할 때마다 파티 전원 HP +" + B.RationPouchHeal),
        new RelicData(Rampart, "견고한 성벽돌", RelicRarity.Common, PlayerClass.Common,
            "전투 시작 시 방어도 +" + B.RampartBlock),
        new RelicData(WarHorn, "전령의 뿔피리", RelicRarity.Common, PlayerClass.Common,
            "전투 첫 턴에 카드 " + B.WarHornDraw + "장 더 뽑기"),
        new RelicData(GuardHelm, "수성 병사의 투구", RelicRarity.Common, PlayerClass.Common,
            "얻는 즉시 최대 HP +" + B.GuardHelmHp),
        new RelicData(Arrowhead, "은빛 화살촉", RelicRarity.Common, PlayerClass.Common,
            "웨이브가 시작될 때마다 무작위 적 1명 취약 " + B.ArrowheadVuln),
        new RelicData(Whetstone, "마력 깃든 숫돌", RelicRarity.Rare, PlayerClass.Common,
            "전투 시작 시 첫 손패의 무작위 공격 카드 " + B.WhetstoneCards + "장\n이번 전투 동안 에너지 -1"),
        new RelicData(Lantern, "광부의 랜턴", RelicRarity.Rare, PlayerClass.Common,
            "전투 첫 턴 에너지 +" + B.LanternEnergy),
        new RelicData(SteelBelt, "무거운 강철 허리띠", RelicRarity.Rare, PlayerClass.Common,
            "내 턴 시작 시 남은 방어도가 사라지지 않고 그대로 이어짐",
            "전투 시작 시 최대 HP의 " + (int)(B.SteelBeltHpRatio * 100) + "%를 잃음 (HP 1 아래로는 안 내려감)"),
        new RelicData(OldShield, "수호자의 낡은 방패", RelicRarity.Hero, PlayerClass.Common,
            "내 턴 시작 시 지난 턴에서 남은 방어도가 있으면 방어도 +" + B.OldShieldBlock),
        new RelicData(AlchemistMug, "탐욕의 연금술 머그", RelicRarity.Hero, PlayerClass.Common,
            "보상으로 얻는 카드가 전부 강화된 상태로 나옴",
            "초소에서 치료를 받을 수 없음"),

        // ---------------- 보스 (에너지 +1 + 페널티) ----------------
        new RelicData(TyrantCrown, "폭군의 왕관", RelicRarity.Boss, PlayerClass.Common,
            "매 턴 에너지 +" + B.BossRelicEnergy,
            "전투 승리 골드 " + (int)(B.CrownGoldRatio * 100) + "%만 받음"),
        new RelicData(BloodContract, "피의 계약서", RelicRarity.Boss, PlayerClass.Common,
            "매 턴 에너지 +" + B.BossRelicEnergy,
            "턴 시작에 뽑는 카드 -" + B.BloodContractDrawLoss),
        new RelicData(IronKey, "봉인된 성문 열쇠", RelicRarity.Boss, PlayerClass.Common,
            "매 턴 에너지 +" + B.BossRelicEnergy,
            "카드 보상이 " + B.CardRewardChoices + "장 중 " + B.CardRewardPicks + "장 → " + B.IronKeyChoices + "장 중 " + B.IronKeyPicks + "장"),

        // ---------------- 검은 낫 전용 ----------------
        new RelicData(Ledger, "피 묻은 처형인 명부", RelicRarity.Special, PlayerClass.BlackScythe,
            "적이 처형될 때마다 최대 HP +" + B.LedgerMaxHp),
        new RelicData(Hourglass, "사신의 모래시계", RelicRarity.Rare, PlayerClass.BlackScythe,
            "전투를 영혼 " + B.HourglassSoul + "개로 시작"),
        new RelicData(Pendant, "부서진 초승달 펜던트", RelicRarity.Hero, PlayerClass.BlackScythe,
            "[밤]이 켜질 때마다 (죽음의 밤 중에는 매 턴 시작)\n무작위 적 1명에게 처형 스택 " + B.PendantStacks),
        new RelicData(NecroSkull, "금지된 사령술의 해골", RelicRarity.Hero, PlayerClass.BlackScythe,
            "전투를 영혼 최대치로 시작",
            "내 턴 종료마다 HP -" + B.SkullHpLoss),
        // 3-99 추가 (아티팩트 표 v2)
        new RelicData(SoulLantern, "망자의 등불", RelicRarity.Common, PlayerClass.BlackScythe,
            "전투 시작 시 영혼 +" + B.SoulLanternSoul),
        new RelicData(ChippedScythe, "이 빠진 낫날", RelicRarity.Common, PlayerClass.BlackScythe,
            "적이 쓰러질 때마다 영혼 +" + B.ChippedScytheSoul),
        new RelicData(ShadowCloak, "그림자 망토 조각", RelicRarity.Common, PlayerClass.BlackScythe,
            "[밤]이 켜질 때마다 (죽음의 밤 중에는 매 턴 시작)\n방어도 +" + B.ShadowCloakBlock),
        new RelicData(SpiritCenser, "원혼의 향로", RelicRarity.Rare, PlayerClass.BlackScythe,
            "영혼을 쓰는 카드를 낼 때마다\n무작위 적 1명에게 처형 스택 " + B.SpiritCenserStacks),
        new RelicData(BloodCandelabra, "핏빛 촛대", RelicRarity.Rare, PlayerClass.BlackScythe,
            "[밤]이 켜질 때마다 (죽음의 밤 중에는 매 턴 시작)\n카드 " + B.BloodCandelabraDraw + "장 뽑기"),
        new RelicData(AbyssalScythe, "심연의 사신 낫", RelicRarity.Boss, PlayerClass.BlackScythe,
            "매 턴 에너지 +" + B.BossRelicEnergy + "\n처형 판정 때 처형 스택을 +" + B.AbyssalExecuteBonus + " 많게 계산",
            "얻는 방어도 -" + (int)System.Math.Round((1f - B.AbyssalBlockRatio) * 100) + "%"),
        new RelicData(TwilightRing, "황혼의 계약 반지", RelicRarity.Boss, PlayerClass.BlackScythe,
            "매 턴 에너지 +" + B.BossRelicEnergy + "\n매 턴 시작 시 영혼 +" + B.TwilightRingSoul,
            "얻는 즉시 최대 HP -" + (int)System.Math.Round(B.TwilightRingHpLoss * 100) + "%"),

        // ---------------- 피닉스 전용 ----------------
        new RelicData(UndyingAsh, "꺼지지 않는 잿더미", RelicRarity.Special, PlayerClass.CosmicPhoenix,
            "카드 효과로 HP를 잃을 때마다 방어도 +" + B.UndyingAshBlock),
        new RelicData(LavaCrystal, "용암 결정", RelicRarity.Rare, PlayerClass.CosmicPhoenix,
            "한 번에 쓴 홍염이 3의 배수라 추가 발화가 붙을 때\n그 적에게 " + B.LavaCrystalDamage + " 피해"),
        new RelicData(BlazingCore, "폭염의 코어", RelicRarity.Hero, PlayerClass.CosmicPhoenix,
            "내 발화 최대치 +" + B.BlazingCoreCap + " (15→35, 20→40, 30→50)"),
        new RelicData(HellfireHeart, "지옥불의 심장", RelicRarity.Hero, PlayerClass.CosmicPhoenix,
            "내가 거는 발화 스택 " + B.HellfireBurnMult + "배",
            "매 내 턴 시작에 홍염 0"),
        // 3-99 추가 (아티팩트 표 v2)
        new RelicData(WarmFeather, "따뜻한 깃털 부적", RelicRarity.Common, PlayerClass.CosmicPhoenix,
            "전투 시작 시 홍염 +" + B.WarmFeatherFlame),
        new RelicData(EmberPouch, "불씨 주머니", RelicRarity.Common, PlayerClass.CosmicPhoenix,
            "매 턴 처음 내는 공격 카드의 대상에게 발화 " + B.EmberPouchBurn + "\n(대상이 없으면 무작위 적)"),
        new RelicData(AshSalve, "화산재 연고", RelicRarity.Common, PlayerClass.CosmicPhoenix,
            "전투에서 이기면 잃은 HP의 " + (int)System.Math.Round(B.AshSalveRatio * 100) + "% 회복"),
        new RelicData(PhoenixTear, "불사조의 눈물", RelicRarity.Rare, PlayerClass.CosmicPhoenix,
            "카드 효과로 HP를 잃을 때마다 홍염 +" + B.PhoenixTearFlame),
        new RelicData(ScorchingFlint, "작열하는 부싯돌", RelicRarity.Rare, PlayerClass.CosmicPhoenix,
            "턴 종료 시 홍염이 " + B.ScorchingFlintNeed + " 이상이면 방어도 +" + B.ScorchingFlintBlock),
        new RelicData(SolarPlume, "태양의 왕관 깃털", RelicRarity.Boss, PlayerClass.CosmicPhoenix,
            "매 턴 에너지 +" + B.BossRelicEnergy + "\n매 턴 시작 시 홍염 +" + B.SolarPlumeFlame,
            "매 턴 시작 시 HP -" + B.SolarPlumeHpLoss),
        new RelicData(EternalBrazier, "영겁의 화로", RelicRarity.Boss, PlayerClass.CosmicPhoenix,
            "매 턴 에너지 +" + B.BossRelicEnergy + "\n적이 받는 발화 피해 +" + (int)System.Math.Round((B.BrazierBurnMult - 1f) * 100) + "%",
            "카드로 얻는 방어도 -" + (int)System.Math.Round((1f - B.BrazierCardBlockRatio) * 100) + "%"),

        // ---------------- 마도사 전용 ----------------
        new RelicData(FrostCompass, "서리 맺힌 나침반", RelicRarity.Special, PlayerClass.FrostMage,
            "적이 [동상]이 될 때마다 마나 +" + B.FrostCompassMana),
        new RelicData(Capacitor, "과부하 축전기", RelicRarity.Rare, PlayerClass.FrostMage,
            "턴 시작 시 마나가 최대면 " + B.CapacitorDraw + "장 뽑기 + 무작위 적 감전 스택 " + B.CapacitorShock),
        new RelicData(Permafrost, "영구동토석", RelicRarity.Hero, PlayerClass.FrostMage,
            "보스의 [빙결 면역]을 무시하고 동결을 쌓음"),
        new RelicData(ZeroShard, "절대 영도의 파편", RelicRarity.Hero, PlayerClass.FrostMage,
            "최대 마나 +" + B.ZeroShardMana + ", 매 턴 마나 +" + B.ZeroShardRegen,
            "카드 효과로 1장 뽑을 때마다 HP -" + B.ZeroShardHpPerDraw + " (턴 시작 뽑기는 제외)"),
        // 3-101 추가 (아티팩트 표 v3)
        new RelicData(IceBrooch, "얼음 조각 브로치", RelicRarity.Common, PlayerClass.FrostMage,
            "전투 시작 시 마나 +" + B.IceBroochMana),
        new RelicData(StaticRing, "정전기의 반지", RelicRarity.Common, PlayerClass.FrostMage,
            "매 턴 처음 마나를 쓰는 카드를 낼 때\n무작위 적 1명에게 감전 스택 " + B.StaticRingShock),
        new RelicData(FrostwindCloak, "서리바람 망토", RelicRarity.Common, PlayerClass.FrostMage,
            "내 턴 시작 시 [동결]이 쌓인 적이 있으면 방어도 +" + B.FrostwindCloakBlock),
        new RelicData(FrostreignTome, "빙뢰의 마도서", RelicRarity.Rare, PlayerClass.FrostMage,
            "[동결]이 쌓인 적에게 [벼락] 카드 피해 +" + (int)System.Math.Round(B.FrostreignTomeBonus * 100) + "%"),
        new RelicData(GlacialCrown, "만년설의 왕관", RelicRarity.Boss, PlayerClass.FrostMage,
            "매 턴 에너지 +" + B.BossRelicEnergy + "\n내 턴 시작 시 모든 적에게 동결 " + B.GlacialCrownFreeze,
            "최대 마나 -" + B.GlacialCrownManaLoss),
        new RelicData(StormcoreStaff, "폭풍핵 지팡이", RelicRarity.Boss, PlayerClass.FrostMage,
            "매 턴 에너지 +" + B.BossRelicEnergy + "\n카드의 마나 소모 -" + (int)System.Math.Round((1f - B.StormcoreManaRatio) * 100) + "%",
            "얻는 방어도 -" + (int)System.Math.Round((1f - B.StormcoreBlockRatio) * 100) + "%"),

        // ---------------- 스나이퍼 전용 (2026-11-15) ----------------
        new RelicData(RustedBelt, "녹슨 군용 탄띠", RelicRarity.Common, PlayerClass.Sniper,
            "전투 시작 시 탄환 +" + B.RustedBeltBullets),
        new RelicData(BrokenScope, "광학 조준경 부품", RelicRarity.Common, PlayerClass.Sniper,
            "[조준]을 얻을 때마다 대상에게 [표식] " + B.BrokenScopeMark + "\n(대상이 없으면 무작위 적)"),
        new RelicData(Crossguard, "균형 잡힌 대검 코등이", RelicRarity.Common, PlayerClass.Sniper,
            "[총검술] 카드를 쓸 때마다 방어도 +" + B.CrossguardBlock),
        new RelicData(GunpowderRing, "화약 냄새 나는 반지", RelicRarity.Rare, PlayerClass.Sniper,
            "탄환이 0발이 될 때마다 무작위 적 1명에게 " + B.GunpowderRingDamage + " 피해"),
        new RelicData(InfraredSensor, "암시야 적외선 센서", RelicRarity.Rare, PlayerClass.Sniper,
            "[표식]이 있는 적에게 공격 카드 피해 +" + (int)(B.InfraredBonus * 100) + "%"),
        new RelicData(VeteranMag, "베테랑의 예비 탄창", RelicRarity.Rare, PlayerClass.Sniper,
            "턴 종료 시 남은 탄환 " + B.VeteranMagBulletsPerEnergy + "발당 다음 턴 에너지 +1 (최대 " + B.VeteranMagMaxEnergy + ")"),
        // 2026-11-15 (3-71): 스나이퍼 보스 2개도 공용 보스처럼 "매 턴 에너지 +1" 을 받음.
        //   실제 +1 은 원래부터 PlayerCombatant.BaseEnergy() 가 "등급이 Boss 인 아티팩트" 를 보고 줬음.
        //   설명에만 빠져 있어서 첫 줄에 적어 줌 (규칙은 그대로, 글만 바뀜)
        new RelicData(RedWhisper, "레드 위스퍼", RelicRarity.Boss, PlayerClass.Sniper,
            "매 턴 에너지 +" + B.BossRelicEnergy + "\n민첩에 따른 최대 탄환 증가량 " + B.RedWhisperMult + "배",
            "매 턴 시작 시 무작위 손패 1장 이번 턴 에너지 +" + B.RedWhisperCostUp + "\n(낼 수 없는 카드는 안 고름)"),
        new RelicData(DeadlyObserver, "사선(死線)의 관측경", RelicRarity.Boss, PlayerClass.Sniper,
            "매 턴 에너지 +" + B.BossRelicEnergy + "\n공격 카드를 써도 [조준]이 사라지지 않음",
            "매 턴 시작 시 [조준] 1개당 HP -" + B.ObserverHpPerAim),
        new RelicData(TacticalGloves, "전술 자율 장갑", RelicRarity.Special, PlayerClass.Sniper,
            "전투에서 처음 쓰는 [사격] 카드 에너지 0"),
        // 3-99 추가 (아티팩트 표 v2)
        new RelicData(Suppressor, "전술 소음기", RelicRarity.Hero, PlayerClass.Sniper,
            "[사격] 카드를 쓸 때마다 방어도 +" + B.SuppressorBlock),
        new RelicData(Rangefinder, "고배율 거리측정기", RelicRarity.Hero, PlayerClass.Sniper,
            "매 턴 시작 시 [조준] +" + B.RangefinderAim + "\n[표식]이 있는 적이 쓰러지면 탄환 +" + B.RangefinderBullets),
        // ================= 3-176 새 아티팩트 45개 (아티팩트 표 v6, Claude 가 겹치는 것·없는 규칙을 조정) =================
        // ---- 공용 보스 (에너지 +1) ----
        new RelicData(CommanderMedal, "작전 지휘관의 훈장", RelicRarity.Boss, PlayerClass.Common,
            "매 턴 에너지 +" + B.BossRelicEnergy + "\n전투 첫 턴 카드 +" + B.MedalFirstDraw + "장",
            "2턴부터 턴 시작에 뽑는 카드 -" + B.MedalLaterDrawLoss),
        new RelicData(GoldenChalice, "황금 성배", RelicRarity.Boss, PlayerClass.Common,
            "매 턴 에너지 +" + B.BossRelicEnergy + "\n전투 승리 골드 +" + (int)System.Math.Round(B.ChaliceGoldBonus * 100) + "%",
            "상점 카드 가격 +" + (int)System.Math.Round((B.ChaliceCardPrice - 1f) * 100) + "%"),
        new RelicData(OnslaughtHelm, "맹공의 투구", RelicRarity.Boss, PlayerClass.Common,
            "매 턴 에너지 +" + B.BossRelicEnergy + "\n공격 카드 피해 +" + (int)System.Math.Round(B.OnslaughtBonus * 100) + "%",
            "전투 시작 시 자신에게 취약 " + B.OnslaughtSelfVuln),
        new RelicData(CommanderFlag, "작전 지휘관의 깃발", RelicRarity.Boss, PlayerClass.Common,
            "매 턴 에너지 +" + B.BossRelicEnergy + "\n턴 종료 시 남은 에너지 1당 다음 턴 카드 +1장 (최대 " + B.FlagMaxDraw + ")",
            "전투 첫 턴 카드 -" + B.FlagFirstDrawLoss + "장"),
        // ---- 공용 보스 (보스 승격: 에너지 없음) ----
        new RelicData(PurifyHourglass, "정화의 모래시계", RelicRarity.Boss, PlayerClass.Common,
            "매 턴 카드 +" + B.PurifyHourglassDraw + "장\n턴 시작 시 손패·뽑을 더미의 방해 카드 최대 " + B.PurifyHourglassMax + "장 소멸, 1장당 카드 +1장",
            "매 턴 시작 시 HP -" + B.PurifyHourglassHpLoss) { GivesEnergy = false },
        new RelicData(GamblerDice, "도박사의 주사위", RelicRarity.Boss, PlayerClass.Common,
            "매 턴 시작 시 50% 확률로 에너지 +" + B.DiceEnergy,
            "나머지 50%는 이번 턴 카드 -" + B.DiceDrawLoss + "장") { GivesEnergy = false },
        new RelicData(GoldenFlask, "연금술사의 황금 플라스크", RelicRarity.Boss, PlayerClass.Common,
            "전투 시작 시 빈 포션 칸에 무작위 포션 1개",
            "상점에서 포션을 살 수 없음 (상점에 포션이 안 나옴)") { GivesEnergy = false },
        new RelicData(BondTorch, "결속의 횃불", RelicRarity.Boss, PlayerClass.Common,
            "[파티] 모두 공격 카드 피해 +" + (int)System.Math.Round(B.TorchBonus * 100) + "%\n각자 턴 종료 시 방어도 +" + B.TorchBlock,
            "매 턴 시작 시 모두 HP -" + B.TorchHpLoss) { GivesEnergy = false, MultiOnly = true },
        // ---- 공용 일반 · 희귀 · 영웅 ----
        new RelicData(SharpShard, "예리한 칼날 조각", RelicRarity.Common, PlayerClass.Common,
            "공격 카드 피해 +" + B.SharpShardDamage,
            "방어도를 얻을 때마다 1 적게 얻음"),
        new RelicData(JaggedShard, "날카로운 톱니 조각", RelicRarity.Common, PlayerClass.Common,
            "공격 카드가 적 방어도의 " + (int)System.Math.Round(B.JaggedIgnore * 100) + "%를 무시"),
        new RelicData(SwiftBoots, "신속의 부츠", RelicRarity.Rare, PlayerClass.Common,
            "전투 첫 턴 카드 +" + B.SwiftBootsDraw + "장\n전투 시작 시 방어도 +" + B.SwiftBootsBlock),
        new RelicData(OverheatedStone, "과열된 마력석", RelicRarity.Rare, PlayerClass.Common,
            "매 턴 처음 내는 공격 카드 피해 +" + B.OverheatedDamage,
            "내 턴 종료마다 HP -" + B.OverheatedHpLoss),
        new RelicData(SwiftGreaves, "신속의 깃털신", RelicRarity.Rare, PlayerClass.Common,
            "한 턴에 " + B.SwiftGreavesEvery + "번째, " + (B.SwiftGreavesEvery * 2) + "번째… 카드를 낼 때마다 1장 뽑기"),
        new RelicData(SuperheatedStone, "고열의 마도석", RelicRarity.Rare, PlayerClass.Common,
            "턴 종료 시 손에 남은 카드 1장당 무작위 적에게 " + B.SuperheatedDamage + " 피해 (최대 " + B.SuperheatedMaxCards + "장)"),
        new RelicData(PurifyTimer, "희석된 정화 시계", RelicRarity.Rare, PlayerClass.Common,
            "턴 시작 시 손패의 방해 카드 1장 소멸 + 1장 뽑기 (전투당 " + B.PurifyTimerUses + "번)"),
        new RelicData(GamblerCoin, "도박사의 동전", RelicRarity.Rare, PlayerClass.Common,
            "매 턴 시작 시 " + B.GamblerCoinChance + "% 확률로 카드 +1장"),
        new RelicData(AlchemistStill, "연금술사의 증류기", RelicRarity.Hero, PlayerClass.Common,
            "회복·화염·강화·방어 포션 효과 +" + (int)System.Math.Round(B.AlchemistStillBonus * 100) + "%"),
        new RelicData(TacticianMap, "전술가의 지도", RelicRarity.Hero, PlayerClass.Common,
            "엘리트·보스 전투 첫 턴 에너지 +" + B.TacticianEnergy + ", 카드 +" + B.TacticianDraw + "장"),
        // ---- 파티 (보물 상자, 멀티 전용) ----
        new RelicData(ComradeShield, "전우애의 방패", RelicRarity.Party, PlayerClass.Common,
            "[파티] 아군이 쓰러질 때 남은 아군 모두 방어도 +" + B.ComradeShieldBlock),
        new RelicData(UnityHelm, "결속의 투구", RelicRarity.Party, PlayerClass.Common,
            "[파티] 얻는 즉시 모두 최대 HP +" + B.UnityHelmHp + "\n매 턴 시작 시 HP 비율이 가장 낮은 아군 방어도 +" + B.UnityHelmBlock),
        new RelicData(ComradeCrest, "전우애의 문장", RelicRarity.Party, PlayerClass.Common,
            "[파티] 쓰러진 아군이 전투 후 HP 1 대신 최대 HP " + (int)System.Math.Round(B.ComradeCrestRevive * 100) + "%로 부활"),
        // ---- 검은 낫 ----
        new RelicData(HungryDoll, "굶주린 그림자 인형", RelicRarity.Common, PlayerClass.BlackScythe,
            "공격 카드 피해 +" + B.HungryDollDamage + "\n적이 쓰러질 때마다 영혼 +" + B.HungryDollSoul,
            "전투 시작 시 HP -" + B.HungryDollHpLoss),
        new RelicData(HungryMask, "굶주린 그림자 가면", RelicRarity.Common, PlayerClass.BlackScythe,
            "영혼을 쓰는 카드를 낼 때마다 방어도 +" + B.HungryMaskBlock,
            "최대 영혼 -" + B.HungryMaskMaxLoss),
        new RelicData(SoulbondHaft, "영혼 결속의 낫자루", RelicRarity.Boss, PlayerClass.BlackScythe,
            "매 턴 에너지 +" + B.BossRelicEnergy + "\n영혼을 쓰는 카드의 영혼 소모 -1 (최소 1)",
            "최대 영혼 -" + B.SoulbondHaftMaxLoss),
        new RelicData(SoulbondBlade, "영혼 결속의 노리개", RelicRarity.Boss, PlayerClass.BlackScythe,
            "매 턴 에너지 +" + B.BossRelicEnergy + "\n영혼을 쓰면 쓴 영혼 1개당 대상(없으면 무작위 적)에게 처형 스택 1",
            "적이 쓰러져도 영혼을 얻지 않음"),
        new RelicData(ReaperEdge, "수확자의 영혼등불", RelicRarity.Boss, PlayerClass.BlackScythe,
            "처형 스택이 있는 적에게 공격 카드 피해 +" + (int)System.Math.Round(B.ReaperEdgeBonus * 100) + "%\n적이 처형될 때마다 영혼 +" + B.ReaperEdgeSoul,
            "전투 첫 턴 얻는 방어도 -" + (int)System.Math.Round((1f - B.ReaperEdgeFirstBlock) * 100) + "%") { GivesEnergy = false },
        new RelicData(ShadowReaperRing, "그림자 수확자의 반지", RelicRarity.Boss, PlayerClass.BlackScythe,
            "[밤]일 때 공격 카드 피해 +" + B.ShadowRingDamage + ", 턴 시작 카드 +" + B.ShadowRingDraw + "장",
            "[밤]이 아닐 때 얻는 방어도 -" + (int)System.Math.Round((1f - B.ShadowRingDayBlock) * 100) + "%") { GivesEnergy = false },
        // ---- 피닉스 ----
        new RelicData(HotBrazierShard, "뜨거운 화로 조각", RelicRarity.Common, PlayerClass.CosmicPhoenix,
            "내가 발화를 걸 때마다 발화 +" + B.HotBrazierBurn + "\n전투 시작 시 홍염 +" + B.HotBrazierFlame,
            "전투 시작 시 HP -" + B.HotBrazierHpLoss),
        new RelicData(HotFlint, "뜨거운 불씨 부싯돌", RelicRarity.Common, PlayerClass.CosmicPhoenix,
            "홍염을 쓰는 카드를 낼 때마다 무작위 적에게 " + B.HotFlintDamage + " 피해"),
        new RelicData(PhoenixScale, "불사조의 역린", RelicRarity.Boss, PlayerClass.CosmicPhoenix,
            "매 턴 에너지 +" + B.BossRelicEnergy + "\n발화가 걸린 적에게 공격 카드 피해 +" + (int)System.Math.Round(B.PhoenixScaleBonus * 100) + "%",
            "내 턴 시작 시 홍염이 절반으로 (올림)"),
        new RelicData(AshCrown, "불사조의 잿불 왕관", RelicRarity.Boss, PlayerClass.CosmicPhoenix,
            "매 턴 에너지 +" + B.BossRelicEnergy + "\n턴 종료 시 남은 홍염 1당 방어도 +" + B.AshCrownBlockPerFlame + " (최대 " + B.AshCrownMaxBlock + ")",
            "내 턴 시작 시 홍염 -" + B.AshCrownFlameLoss),
        new RelicData(EmberMatch, "잿불의 성냥", RelicRarity.Boss, PlayerClass.CosmicPhoenix,
            "전투 시작 시 모든 적에게 발화 " + B.EmberMatchBurn + ", 홍염 +" + B.EmberMatchFlame,
            "전투 첫 턴 에너지 -" + B.EmberMatchEnergyLoss) { GivesEnergy = false },
        new RelicData(EmberCore, "꺼지지 않는 불씨핵", RelicRarity.Boss, PlayerClass.CosmicPhoenix,
            "매 턴 시작 시 홍염 +" + B.EmberCoreFlame + "\n적의 발화가 줄어들지 않음",
            "얻는 즉시 최대 HP -" + (int)System.Math.Round(B.EmberCoreHpLoss * 100) + "%") { GivesEnergy = false },
        // ---- 스나이퍼 ----
        new RelicData(LightSuppressor, "가벼운 소음기 부품", RelicRarity.Common, PlayerClass.Sniper,
            "탄환을 쓰는 공격 카드 피해 +" + B.LightSuppressorDamage,
            "[총검술] 카드 피해 -" + B.LightSuppressorBayonetLoss),
        new RelicData(LightLens, "가벼운 조준경 렌즈", RelicRarity.Common, PlayerClass.Sniper,
            "[표식]이 " + B.LightLensMarks + " 이상인 적을 공격 카드로 때리면 탄환 +" + B.LightLensBullets + " (카드당 1번)"),
        new RelicData(PrecisionWarhead, "정밀 탄두 상자", RelicRarity.Boss, PlayerClass.Sniper,
            "매 턴 에너지 +" + B.BossRelicEnergy + "\n탄환을 쓰는 공격 카드 피해 +" + (int)System.Math.Round(B.WarheadBonus * 100) + "%",
            "매 턴 탄환 충전 -" + B.WarheadRegenLoss),
        new RelicData(BallisticModule, "초정밀 탄도 모듈", RelicRarity.Boss, PlayerClass.Sniper,
            "매 턴 에너지 +" + B.BossRelicEnergy + "\n[조준]으로 강해진 공격 피해 +" + (int)System.Math.Round(B.BallisticBonus * 100) + "% 더",
            "[총검술] 카드 에너지 +" + B.BallisticBayonetCost),
        new RelicData(RapidMagazine, "속사 탄창", RelicRarity.Boss, PlayerClass.Sniper,
            "매 턴 처음 내는 탄환 카드 " + B.RapidMagFree + "장은 탄환 소모 0",
            "최대 탄환 -" + B.RapidMagMaxLoss) { GivesEnergy = false },
        new RelicData(LightPouch, "경량 탄창 파우치", RelicRarity.Boss, PlayerClass.Sniper,
            "턴 시작에 탄환이 충전될 때 방어도 +" + B.LightPouchBlock + ", 카드 +" + B.LightPouchDraw + "장",
            "전투 시작 시 탄환 -" + B.LightPouchStartLoss) { GivesEnergy = false },
        // ---- 마도사 ----
        new RelicData(FrostShardRing, "서리 파편 반지", RelicRarity.Common, PlayerClass.FrostMage,
            "마나를 쓰는 공격 카드 피해 +" + B.FrostShardDamage,
            "최대 마나 -" + B.FrostShardMaxLoss),
        new RelicData(FrostRing, "서리 결정 반지", RelicRarity.Common, PlayerClass.FrostMage,
            "적에게 [동결]을 쌓을 때마다 방어도 +" + B.FrostRingBlock + " (한 턴 최대 " + B.FrostRingMaxPerTurn + ")"),
        new RelicData(GlacialHeart, "만년설의 심장", RelicRarity.Boss, PlayerClass.FrostMage,
            "매 턴 에너지 +" + B.BossRelicEnergy + "\n공격 카드로 [동결]이 쌓인 적을 때리면 마나 +" + B.GlacialHeartMana + " (카드당 1번)",
            "적이 [동상]이 될 때마다 마나 -" + B.GlacialHeartFrostbiteLoss),
        new RelicData(AbyssOrb, "빙결 심연의 보주", RelicRarity.Boss, PlayerClass.FrostMage,
            "매 턴 에너지 +" + B.BossRelicEnergy + "\n적이 [동상]이 될 때마다 모든 적 감전 스택 " + B.AbyssOrbShock + ", 방어도 +" + B.AbyssOrbBlock,
            "최대 마나 -" + B.AbyssOrbMaxLoss),
        new RelicData(FrostStaff, "서리 결정 지팡이", RelicRarity.Boss, PlayerClass.FrostMage,
            "매 턴 처음 내는 마나 카드 마나 소모 0\n[동결]이 쌓인 적에게 공격 카드 피해 +" + (int)System.Math.Round(B.FrostStaffBonus * 100) + "%",
            "매 턴 마나 회복 -" + B.FrostStaffRegenLoss) { GivesEnergy = false },
        new RelicData(FrostCrystalOrb, "서리 마력의 수정구", RelicRarity.Boss, PlayerClass.FrostMage,
            "마나를 쓰는 카드를 낼 때 " + B.CrystalOrbChance + "% 확률로 마나 +" + B.CrystalOrbMana + ", 1장 뽑기 (한 턴 최대 " + B.CrystalOrbMaxProcs + "번)",
            "최대 마나 -" + B.CrystalOrbMaxLoss) { GivesEnergy = false },
    };

    public static RelicData Get(string id)
    {
        foreach (RelicData r in All) if (r.Id == id) return r;
        return null;
    }

    public static string StarterFor(PlayerClass playerClass)
    {
        switch (playerClass)
        {
            case PlayerClass.BlackScythe: return ReaperMark;
            case PlayerClass.CosmicPhoenix: return PhoenixPlume;
            case PlayerClass.FrostMage: return FrostPendant;
            case PlayerClass.Sniper: return CasingCharm;
            default: return null;
        }
    }

    // 이 플레이어가 새로 얻을 수 있는 아티팩트 (그 등급, 내 직업용, 아직 없는 것)
    public static List<RelicData> Available(PlayerCombatant p, RelicRarity rarity)
    {
        List<RelicData> list = new List<RelicData>();
        foreach (RelicData r in All)
        {
            bool sameTier = r.Rarity == rarity || (rarity == RelicRarity.Common && r.Rarity == RelicRarity.Special);
            if (!sameTier) continue;
            if (r.ClassOnly != PlayerClass.Common && r.ClassOnly != p.Class) continue;
            if (p.HasRelic(r.Id)) continue;
            list.Add(r);
        }
        return list;
    }
}
