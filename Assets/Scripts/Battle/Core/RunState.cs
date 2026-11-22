// ============================================================
// RunState.cs
// "런" 하나(처음 시작 ~ 패배 또는 클리어)의 진행 상황을 기억하는 클래스입니다.
// 화면(Unity)은 모르고 규칙만 담당합니다. (BattleSystem 과 같은 원칙)
//
// 런 동안 계속 이어지는 것 : 플레이어(HP, 덱), 스테이지, 지금 위치
// 전투마다 새로 시작하는 것: 방어도, 상태이상, 마나, 손패 (BattleSystem.StartBattle 이 정리)
//
// 새 구조
//   1~3스테이지 = 수성 (지도 안 보임). 10칸을 차례로 진행 (칸 번호는 GameBalance 에서 바꿀 수 있음)
//     1칸: 1스테이지 = 쉬운 전투 고정 / 2스테이지부터 = ???
//     2·3칸: ??? 3개 = 쉬운 1 + 어려운 1 + 비전투 1 (힌트 문장만 보고 고름)
//     4칸: 비전투 5개 중 고르기 (모닥불·대장간·보물·이벤트·상점)
//     5칸: ???
//     6칸: 무조건 전투 (1스테이지 = 쉬운 전투 / 2스테이지부터 = 어려운 전투)
//     7·8칸: ???
//     9칸: 모닥불 고정
//     10칸: 보스 (종족 수문장)
//     → 3스테이지 보스를 잡으면 지도를 얻음 (HasMap = true)
//   4스테이지 = 공격 (지도). 지금까지 만든 슬더스식 지도 그대로, 7층 보스 = 설계자
//
// 흐름
//   수성: ChooseSiege(선택지) → 전투면 CreateBattle() → 끝나면 FinishBattle()
//   지도: EnterNode(칸)       → 전투면 CreateBattle() → 끝나면 FinishBattle()
// ============================================================

using System.Collections.Generic;

// 수성 칸의 선택지 종류
public enum SiegeChoiceKind
{
    EasyFight,   // 쉬운 전투
    HardFight,   // 어려운 전투
    NonCombat,   // 비전투 (모닥불·대장간·보물·이벤트·상점)
    Boss         // 보스
}

// 지금 들어가 있는 비전투 장소 (화면이 따로 그림)
public enum RunPlace
{
    None,    // 장소 안 (선택지 또는 지도 화면)
    Camp,    // 초소 / 야영지
    Forge,   // 대장간
    Treasure,// 보물 상자 (상자를 눌러야 열림) - 2026-11-15
    Shop,    // 상점 (카드·아티팩트·포션 사기) - 2026-11-15
    BloodAltar // 3-129 피의 제단 (1·2스테이지 4칸 이벤트 자리). 맨 뒤에 붙임 (저장 번호가 안 밀리게)
}

// 상점 물건 하나 (카드·아티팩트·포션 중 하나만 들어 있음)
public class ShopItem
{
    public CardData Card;
    public RelicData Relic;
    public PotionData Potion;
    public int Price;
    public bool Sold;
    public string Name { get { return Card != null ? Card.Name : (Relic != null ? Relic.Name : (Potion != null ? Potion.Name : "")); } }
}

// 초소·야영지에서 고르는 행동
public enum CampAction
{
    Heal,     // 치료: 최대 HP 30% 회복
    Fortify,  // 장비 보강: 다음 전투 시작 시 방어도
    Scout,    // 정찰: 다음 칸 ??? 정체 미리 보기
    Mend      // 집중 치료 (멀티, 2026-11-15): 동료 1명만 크게 회복 (슬더스2 멀티의 '치유')
}

// 무기 강화 갈래
public enum WeaponBranch
{
    Blade,   // 날
    Hilt,    // 자루
    Core     // 핵
}

// 수성 칸의 선택지 하나 (화면에 카드 한 장으로 보임)
public class SiegeChoice
{
    public SiegeChoiceKind Kind;
    public MapNodeType NodeType;   // 실제 내용 (Monster/Elite/Boss 또는 Rest/Forge/Treasure/Event/Shop)
    public string Hint;            // 정찰병 힌트 문장
    public bool Hidden;            // true = "???" 로 가려서 보여 줌 (힌트만 보임)
    public string Title;           // 따로 붙인 이름 (예: "몬스터 침공"). 없으면 종류 이름
    public char Fight;             // 3-110 전투 종류: 'N' 일반 / 'V' 정예 / 'E' 엘리트 / 'W' 일반 2웨이브 (비전투·보스는 0)

    public bool IsBattle
    {
        get { return Kind != SiegeChoiceKind.NonCombat; }
    }

    // 고른 뒤(또는 가려지지 않은 칸)에 보여 줄 이름
    public string RealName
    {
        get
        {
            if (!string.IsNullOrEmpty(Title)) return Title;
            switch (Kind)
            {
                case SiegeChoiceKind.EasyFight: return Fight == 'V' ? "정예 전투" : (Fight == 'W' ? "몬스터 무리 (2웨이브)" : "쉬운 전투");
                case SiegeChoiceKind.HardFight: return "어려운 전투";
                case SiegeChoiceKind.Boss: return "보스";
                default: return RunMap.TypeName(NodeType);
            }
        }
    }
}

// 전투가 끝난 뒤 한 플레이어가 받는 보상 (보상 화면이 P1/P2 탭으로 보여 줌)
public class PlayerReward
{
    public PlayerCombatant Player;
    public int Gold;                                   // 이미 지갑에 들어감 (보여 주기용)
    public List<CardData> CardChoices = new List<CardData>();   // 남은 후보 (3-113: 4장 중 2장. 고른 카드는 여기서 빠짐)
    public bool CardDone;                              // 다 골랐거나 그만둠
    public CardData TakenCard;                         // 마지막으로 고른 카드 (안 골랐으면 null)
    public List<CardData> TakenCards = new List<CardData>();   // 3-113: 고른 카드 전부
    public int PicksLeft;                              // 3-113: 더 고를 수 있는 장 수
    public RelicData Relic;                            // 자동으로 받은 아티팩트 (엘리트·보물). 없으면 null
    public PotionData Potion;                          // 보물 상자에서 받은 포션. 없으면 null
    public int ExpGained;                              // 이번 전투 경험치
    public int LevelsGained;                           // 이번에 오른 레벨 수 (0 이면 안 오름)
    public bool Revived;                               // 이번 전투에서 쓰러졌다가 HP 1로 부활했는지 (보상 화면 표시용)
    public List<RelicData> BossChoices;                // 보스 아티팩트 후보 (보스만). 없으면 null
    public bool BossDone;
    public RelicData TakenBossRelic;

    public bool Done
    {
        get { return CardDone && (BossChoices == null || BossDone); }
    }
}

public enum RewardKind { Easy, Hard, Boss, Treasure }

public class RunState
{
    public List<PlayerCombatant> Players;
    public int Stage = 1;          // 1 ~ GameBalance.MaxStage

    // 3-196 난이도. 정하면 플레이어 경험치 배율도 같이 맞춤 (자바의 setter 안에서 다른 값도 고치는 것과 같음)
    private Difficulty difficulty = Difficulty.Normal;
    public Difficulty Difficulty
    {
        get { return difficulty; }
        set
        {
            difficulty = value;
            if (Players != null) foreach (PlayerCombatant p in Players) p.ExpScale = DifficultyInfo.Exp(value);   // 불러오기 중엔 플레이어가 아직 없을 수 있음
        }
    }
    public EnemyRank PendingTopRank = EnemyRank.Normal;   // 3-162: 지금 전투에서 가장 높은 몬스터 등급 (골드 보상용, 저장 안 함 - 전투가 끝나면 바로 씀)
    public bool HasMap;            // 지도를 가졌는지 (4스테이지부터 true)
    public RunMap Map;             // 지도 (4스테이지에서만)
    public bool IsOver;            // 런이 끝났는지 (패배 또는 클리어)
    public bool Cleared;           // 마지막 보스까지 잡았는지
    public int BattlesWon;         // 지금까지 이긴 전투 수 (기록용)
    public int EliteWins;          // 3-112: 이긴 엘리트 전투 수 (엔딩 기록)
    public int BossWins;           // 3-112: 이긴 보스 전투 수 (엔딩 기록)
    public float PlaySeconds;      // 3-112: 이 런을 한 시간 (초). 화면이 매 프레임 더함, 저장됨
    public MapNode PendingNode;    // 지도: 지금 싸우는 중인 칸 (이기면 그 칸으로 이동 완료)

    // ---- 수성 ----
    public Race[] StageRaces;      // 1~3스테이지에 침공하는 종족 (무작위 순서)
    public int[] StageBosses = new int[3];   // 1~3스테이지 보스 종류. 3-167: [0] 항상 0 (종족 수문장) / [1] 0~2 (껍질·망령·균열) / [2] 0~1 (요새의 수문장·쌍둥이 석상). EnemyLibrary.PickStageBoss
    public int Step = 1;           // 지금 고를 칸 번호 (1 ~ GameBalance.SiegeSteps)
    public List<SiegeChoice> Choices = new List<SiegeChoice>();   // 지금 칸의 선택지들
    public SiegeChoice PendingChoice;   // 지금 싸우는 중인 선택지
    public bool JustGotMap;        // 방금 3스테이지 보스를 잡아서 지도를 얻었는지 (화면 안내용)
    public string LastPlanText = "";    // 방금 시작한 전투의 웨이브 구성 (화면 안내용)
    // 이번 스테이지에서 칸마다 고른 것 (3-81, 위쪽 10칸 표시용). [칸-1] = (int)MapNodeType, 아직 안 고른 칸은 -1
    //   ??? 칸을 지나가면 그 칸 아이콘이 "?" 대신 실제로 고른 것(칼·상점·보물…)으로 바뀜
    public List<int> StepChosen = new List<int>();

    // 이 칸에서 고른 칸 종류. 아직 안 골랐으면 -1
    public int ChosenAt(int step)
    {
        int i = step - 1;
        return (i >= 0 && i < StepChosen.Count) ? StepChosen[i] : -1;
    }

    // ---- 비전투 장소 ----
    public RunPlace Place = RunPlace.None;   // 지금 들어가 있는 장소 (None 이면 선택지/지도 화면)
    public int NextBattleBlock;              // [장비 보강]: 다음 전투 시작 시 모두 받는 방어도
    private bool revealNext;                 // [정찰]: 다음 칸 ??? 를 가리지 않기
    private List<PlayerCombatant> forgedThisVisit = new List<PlayerCombatant>();   // 이번 대장간에서 강화를 끝낸 사람

    private System.Random rng;

    // ---- 보상 (전투 승리·보물 뒤에 화면이 보여 주고, 다 고르면 ClearRewards) ----
    public List<PlayerReward> Rewards = new List<PlayerReward>();
    public RewardKind LastRewardKind;

    public RunState(List<PlayerCombatant> players, int seed)
    {
        Players = players;
        rng = new System.Random(seed);

        // 종족 순서 섞기 (웨어울프·오크·좀비가 판마다 다른 순서로)
        List<Race> races = new List<Race> { Race.Werewolf, Race.Orc, Race.Zombie };
        for (int i = races.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            Race temp = races[i];
            races[i] = races[j];
            races[j] = temp;
        }
        StageRaces = races.ToArray();
        // 3-102: 스테이지 보스도 판마다 무작위 (정찰 힌트와 실제 보스가 같도록 미리 정해 둠)
        // 3-167: 스테이지마다 나올 수 있는 보스가 다름
        //   1스테이지 = 종족 수문장 (광폭한·철퇴·부패한, 종족을 따름) / 2스테이지 = 심연의 거대 껍질·잊혀진 왕의 망령·차원 균열의 감시자 중 1
        //   3스테이지 = 요새의 수문장·심판의 쌍둥이 석상(태양·달) 중 1 / 4스테이지 = 설계자 고정
        StageBosses[0] = 0;
        // 3-214: 2스테이지는 확률표대로 (거대 껍질 35 / 잊혀진 왕의 망령 30 / 차원 균열의 감시자 35). 숫자는 GameBalance.Stage2BossWeights
        //   방법: 0~(합계-1) 사이 숫자를 하나 뽑고, 앞에서부터 확률을 빼 가며 어느 칸에 떨어졌는지 봄 (자바에서 가중치 랜덤 뽑기와 같음)
        {
            int[] w = GameBalance.Stage2BossWeights;
            int total = 0;
            foreach (int x in w) total += x;
            int roll = rng.Next(total);
            int kind = 0;
            while (kind < w.Length - 1 && roll >= w[kind]) { roll -= w[kind]; kind++; }
            StageBosses[1] = kind;
        }
        StageBosses[2] = rng.Next(2);
        // 3-112: 시작 골드
        foreach (PlayerCombatant p in Players) p.Gold = GameBalance.StartGold;

        BeginStage(1);
    }

    // 마지막 스테이지(최종장)인지
    public bool IsFinalChapter
    {
        get { return Stage >= GameBalance.MaxStage; }
    }

    // 지도가 안 보이는 수성 스테이지인지
    public bool IsSiege
    {
        get { return !HasMap; }
    }

    // 지금 스테이지에 침공하는 종족 (4스테이지는 종족 없음 → 첫 종족을 임시로)
    public Race CurrentRace
    {
        get
        {
            int i = Stage - 1;
            if (i < 0) i = 0;
            if (i >= StageRaces.Length) i = 0;
            return StageRaces[i];
        }
    }

    // 스테이지 시작. 테스트용으로 화면에서 원하는 스테이지부터 시작할 때도 부름
    public void BeginStage(int stage)
    {
        Stage = stage;
        HasMap = stage >= GameBalance.MapStage;
        RunMap.RestName = "초소";   // 3-156: 지도 스테이지(3·4)도 "야영지" 대신 "초소" (이름을 하나로)
        Place = RunPlace.None;
        PendingNode = null;
        PendingChoice = null;
        if (HasMap)
        {
            MapGenerator.CurrentDifficulty = Difficulty;   // 3-196: 엘리트·모닥불 가중치
            Map = stage >= GameBalance.MaxStage ? MapGenerator.FinalChapter(rng) : MapGenerator.Generate(rng);   // 최종장 = 정해진 길
            Choices.Clear();
        }
        else
        {
            Map = null;
            Step = 1;
            StepChosen.Clear();   // 새 스테이지 = 고른 기록 처음부터 (3-81)
            MakeChoices();
        }
    }

    // 지도: 지금 층 (화면 표시용, 1부터). 출발 전이면 0
    public int FloorNumber
    {
        get
        {
            if (Map == null) return 0;
            MapNode n = PendingNode ?? Map.Current;   // ?? = 왼쪽이 null 이면 오른쪽 (자바엔 없는 문법)
            return n == null ? 0 : n.Floor + 1;
        }
    }

    // 위쪽 글자용: "칸 3/7" 또는 "4층"
    public string ProgressText
    {
        get { return IsSiege ? "칸 " + Step + "/" + GameBalance.SiegeSteps : FloorNumber + "층"; }
    }

    // 지금 싸우는 전투의 이름 ("쉬운 전투", "엘리트" ...). 테스트 전투면 "테스트 전투"
    public string PendingLabel
    {
        get
        {
            if (PendingChoice != null) return PendingChoice.RealName;
            if (PendingNode != null) return RunMap.TypeName(PendingNode.Type);
            return "테스트 전투";
        }
    }

    // ==================== 수성: 선택지 만들기 ====================
    void MakeChoices()
    {
        Choices.Clear();
        if (Step >= GameBalance.SiegeSteps)
        {
            Choices.Add(MakeChoice(SiegeChoiceKind.Boss, MapNodeType.Boss, false));
        }
        else if (Step == GameBalance.SiegeRestStep)
        {
            Choices.Add(MakeChoice(SiegeChoiceKind.NonCombat, MapNodeType.Rest, false));
        }
        else if (Step == GameBalance.NonCombatStep)
        {
            // 비전투 5개 전부 보여 주고 하나 고르기
            foreach (MapNodeType t in NonCombatTypes)
            {
                if (t == MapNodeType.Rest && DifficultyInfo.FewerRests(Difficulty)) continue;   // 3-196 매우 어려움~: 초소 선택 없음
                SiegeChoice c = MakeChoice(SiegeChoiceKind.NonCombat, t, false);
                // 3-129: 1·2스테이지는 [이벤트] 자리에 피의 제단
                if (t == MapNodeType.Event && IsBloodAltarStage)
                {
                    c.Title = BloodAltarName;
                    c.Hint = "카드 " + GameBalance.BloodAltarSacrifice + "장을 바치면 직업 카드 1장 + 유물 1개\n대가: 최대 HP -" + (int)(GameBalance.BloodAltarHpLoss * 100 + 0.5f) + "% (영구)";
                }
                Choices.Add(c);
            }
        }
        else
        {
            // 3-110: 칸별 구성표 (GameBalance.Stage1StepPlan / Stage2StepPlan)
            //   "V" = 정예 고정 칸 / "NV?" = ??? 칸 (일반 전투 + 정예 전투 + 비전투, 자리 섞음)
            string code = StepCode(Step);
            bool hidden = code.EndsWith("?");
            foreach (char ch in code)
                if (ch == 'N' || ch == 'V' || ch == 'E' || ch == 'W') Choices.Add(MakeFight(ch, hidden));
            if (Choices.Count == 0) Choices.Add(MakeFight('N', false));   // 표가 비어 있으면 일반 전투
            if (hidden)
            {
                // 3-125: "?" 개수만큼 비전투 칸. 같은 종류가 두 번 나오지 않게 이미 나온 종류는 뺌
                List<MapNodeType> used = new List<MapNodeType>();
                foreach (char ch in code)
                {
                    if (ch != '?') continue;
                    MapNodeType t = PickNonCombat(used);
                    used.Add(t);
                    Choices.Add(MakeChoice(SiegeChoiceKind.NonCombat, t, true));
                }
                for (int i = Choices.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    SiegeChoice temp = Choices[i];
                    Choices[i] = Choices[j];
                    Choices[j] = temp;
                }
            }
        }

        // 지난 칸에서 [정찰]을 했으면 이번 칸은 가리지 않음
        if (revealNext)
        {
            foreach (SiegeChoice c in Choices) c.Hidden = false;
            revealNext = false;
        }
    }

    // 이번 스테이지 칸별 구성표에서 step 칸의 글자 (3-110). 수성(1·2스테이지)만
    public string StepCode(int step)
    {
        string[] plan = Stage == 1 ? GameBalance.Stage1StepPlan : GameBalance.Stage2StepPlan;
        string code = (step >= 1 && step <= plan.Length) ? plan[step - 1] : "-";
        // 3-196 어려움~: 엘리트 2배 → 3칸 정예 고정 = 엘리트 고정, 5칸 정예 자리 = 엘리트 (엘리트가 3칸부터 나옴)
        if (DifficultyInfo.MoreElites(Difficulty) && (step == GameBalance.SiegeVeteranStep || step == 5))
        {
            int v = code.IndexOf('V');
            if (v >= 0) code = code.Substring(0, v) + "E" + code.Substring(v + 1);
        }
        return code;
    }

    // 전투 선택지 하나 (3-110). 고정 칸이면 이름을 붙임 (??? 칸은 가려져 있다가 정찰·선택 뒤 RealName 으로 보임)
    SiegeChoice MakeFight(char fight, bool hidden)
    {
        SiegeChoice c;
        if (fight == 'E')
        {
            c = MakeChoice(SiegeChoiceKind.HardFight, MapNodeType.Elite, hidden);
            if (!hidden) c.Title = "피할 수 없는 침공 (엘리트)";
        }
        else
        {
            c = MakeChoice(SiegeChoiceKind.EasyFight, fight == 'V' ? MapNodeType.Veteran : MapNodeType.Monster, hidden);
            if (!hidden) c.Title = fight == 'V' ? "정예 몬스터 침공" : (fight == 'W' ? "몬스터 무리 (2웨이브)" : "몬스터 침공");
        }
        c.Fight = fight;
        c.Hint = SiegeHints.PickFight(fight, rng);   // 3-125: 일반·정예·엘리트가 헷갈리게 섞인 정찰병 힌트
        return c;
    }

    // 이 칸이 ??? (가려진 선택지) 칸인지
    bool StepIsHidden(int step)
    {
        if (step >= GameBalance.SiegeSteps) return false;
        if (step == GameBalance.SiegeRestStep) return false;
        if (step == GameBalance.NonCombatStep) return false;
        return StepCode(step).EndsWith("?");   // 3-110: 구성표에서 ? 가 붙은 칸만 가림
    }

    // 6칸 무조건 전투가 어려운 전투(엘리트)인지
    public bool ForcedFightIsHard
    {
        get { return Stage >= GameBalance.ForcedHardFromStage; }
    }

    // 비전투 종류. 순서는 GameBalance.NonCombatWeights 와 같아야 함
    public static readonly MapNodeType[] NonCombatTypes =
    {
        MapNodeType.Rest, MapNodeType.Forge, MapNodeType.Treasure, MapNodeType.Event, MapNodeType.Shop
    };

    // 비전투 종류 하나를 확률표(NonCombatWeights)대로 뽑음. exclude 에 든 종류는 확률 0 으로 봄 (3-125)
    //   자바로 치면 가중치 배열에서 제외할 것만 0 으로 두고 룰렛 뽑기
    MapNodeType PickNonCombat(List<MapNodeType> exclude = null)
    {
        // 3-196 매우 어려움~: ??? 칸에도 초소가 안 나옴
        if (DifficultyInfo.FewerRests(Difficulty))
        {
            exclude = exclude != null ? new List<MapNodeType>(exclude) : new List<MapNodeType>();
            exclude.Add(MapNodeType.Rest);
        }
        int[] w = GameBalance.NonCombatWeights;
        int total = 0;
        for (int i = 0; i < w.Length; i++)
            if (exclude == null || !exclude.Contains(NonCombatTypes[i])) total += w[i];
        if (total <= 0) return MapNodeType.Rest;
        int r = rng.Next(total);
        for (int i = 0; i < w.Length; i++)
        {
            if (exclude != null && exclude.Contains(NonCombatTypes[i])) continue;
            if (r < w[i]) return NonCombatTypes[i];
            r -= w[i];
        }
        return MapNodeType.Rest;
    }

    SiegeChoice MakeChoice(SiegeChoiceKind kind, MapNodeType type, bool hidden)
    {
        SiegeChoice c = new SiegeChoice();
        c.Kind = kind;
        c.NodeType = type;
        c.Hidden = hidden;
        c.Hint = SiegeHints.Pick(kind, type, rng);
        return c;
    }

    // ==================== 수성: 선택지 고르기 ====================
    // 전투면 true (화면이 CreateBattle 을 불러야 함). 비전투는 여기서 바로 처리하고 결과 문장을 message 로
    public bool ChooseSiege(SiegeChoice choice, out string message)
    {
        message = "";
        if (!IsSiege || !Choices.Contains(choice)) { message = "고를 수 없는 칸입니다"; return false; }

        // 고른 기록 (3-81): 리스트를 칸 수만큼 늘린 뒤 이번 칸 자리에 저장
        while (StepChosen.Count < Step) StepChosen.Add(-1);
        StepChosen[Step - 1] = (int)choice.NodeType;

        if (choice.IsBattle)
        {
            PendingChoice = choice;
            return true;
        }

        if (EnterPlace(choice.NodeType)) return false;   // 초소·대장간: 장소 화면으로 (칸은 장소를 나갈 때 넘어감)

        message = ApplyNonCombat(choice.NodeType);
        AdvanceStep();
        return false;
    }

    // 초소·대장간이면 그 장소에 들어감 (true). 아니면 false
    bool EnterPlace(MapNodeType type)
    {
        if (type == MapNodeType.Rest) { Place = RunPlace.Camp; return true; }
        if (type == MapNodeType.Forge) { Place = RunPlace.Forge; forgedThisVisit.Clear(); return true; }
        if (type == MapNodeType.Treasure) { Place = RunPlace.Treasure; TreasureChestIndex = 0; MakeTreasure(); return true; }   // 3-174: 첫 번째 상자부터
        if (type == MapNodeType.Shop) { Place = RunPlace.Shop; MakeShop(); return true; }
        if (type == MapNodeType.Event && IsBloodAltarStage) { Place = RunPlace.BloodAltar; return true; }   // 3-129
        return false;
    }

    // 장소에서 나감: 수성이면 다음 칸으로 (지도는 이미 그 칸으로 옮겨져 있음)
    void LeavePlace()
    {
        Place = RunPlace.None;
        if (IsSiege) AdvanceStep();
    }

    // ==================== 초소 / 야영지 ====================
    public string CampName
    {
        get { return RunMap.RestName; }
    }

    // 다음 칸이 보스인지 (보스 앞 초소 = 정찰병이 보스 힌트를 줌)
    public bool NextIsBoss
    {
        get
        {
            if (IsSiege) return Step + 1 >= GameBalance.SiegeSteps;
            if (Map == null || Map.Current == null) return false;
            foreach (MapNode n in Map.Current.Next) if (n.Type == MapNodeType.Boss) return true;
            return false;
        }
    }

    public string BossHint
    {
        get { return Stage <= 3 ? EnemyLibrary.StageBossHint(CurrentRace, CurrentBossKind, Stage) : EnemyLibrary.ArchitectHint(); }
    }

    // 이번 스테이지 보스 종류 (3-102). 4스테이지는 설계자 고정이라 0
    public int CurrentBossKind
    {
        get
        {
            int i = Stage - 1;
            return (i >= 0 && i < StageBosses.Length) ? StageBosses[i] : 0;
        }
    }

    // [정찰]을 할 수 있는지: 수성이고, 다음 칸이 ??? 칸일 때만 의미가 있음
    public bool CanScout
    {
        get { return IsSiege && !NextIsBoss && StepIsHidden(Step + 1); }
    }

    // 초소에서 행동 하나. 결과 문장을 돌려주고 초소를 나감
    public string DoCamp(CampAction action, int target = -1)
    {
        string message;
        switch (action)
        {
            case CampAction.Mend:
                // 집중 치료: 고른 동료 1명만 최대 HP 60% 회복 (다른 사람은 회복 없음)
                if (target < 0 || target >= Players.Count) return "치료할 동료를 고르세요";
                PlayerCombatant m = Players[target];
                if (m.HasRelic(RelicLibrary.AlchemistMug)) message = m.Name + "은(는) 치료할 수 없습니다 (연금술 머그)";
                else
                {
                    int got = m.Heal(DamageCalculator.RoundHalfUp(m.MaxHp * GameBalance.MendHealRatio));
                    message = CampName + "에서 " + m.Name + "을(를) 집중 치료했습니다: HP +" + got;
                }
                break;
            case CampAction.Heal:
                List<string> parts = new List<string>();
                foreach (PlayerCombatant p in Players)
                {
                    if (p.HasRelic(RelicLibrary.AlchemistMug)) { parts.Add(p.Name + " 치료 불가 (연금술 머그)"); continue; }
                    int amount = DamageCalculator.RoundHalfUp(p.MaxHp * DifficultyInfo.RestHeal(Difficulty));   // 3-196: 쉬움 40%
                    int healed = p.Heal(amount);
                    parts.Add(p.Name + " +" + healed);
                }
                message = CampName + "에서 치료했습니다: " + string.Join(", ", parts);
                break;
            case CampAction.Fortify:
                NextBattleBlock += GameBalance.FortifyBlock;
                message = "장비를 보강했습니다: 다음 전투 시작 시 모두 방어도 +" + NextBattleBlock;
                break;
            default:
                revealNext = true;
                message = "정찰병을 보냈습니다: 다음 칸의 정체가 드러납니다";
                break;
        }
        LeavePlace();
        return message;
    }

    // ==================== 대장간 (무기 강화) ====================
    public static int WeaponLevel(PlayerCombatant p, WeaponBranch b)
    {
        switch (b)
        {
            case WeaponBranch.Blade: return p.WeaponBlade;
            case WeaponBranch.Hilt: return p.WeaponHilt;
            default: return p.WeaponCore;
        }
    }

    public static string BranchName(WeaponBranch b)
    {
        switch (b)
        {
            case WeaponBranch.Blade: return "무기 강화";
            case WeaponBranch.Hilt: return "갑옷 강화";
            default: return "체력 강화";
        }
    }

    // 한 단계 올렸을 때의 효과 설명
    public static string BranchEffect(WeaponBranch b, int nextLevel)
    {
        switch (b)
        {
            case WeaponBranch.Blade: return "모든 공격 피해 +" + GameBalance.BladeDamagePerLevel;
            case WeaponBranch.Hilt: return "전투 시작 방어도 +" + GameBalance.HiltStartBlockPerLevel;
            default: return "최대 HP +" + GameBalance.VitalMaxHpPerLevel;
        }
    }

    // 이 플레이어가 이번 대장간에서 아직 강화할 수 있는지
    public bool CanForge(PlayerCombatant p)
    {
        return Place == RunPlace.Forge && !forgedThisVisit.Contains(p);
    }

    public bool UpgradeWeapon(PlayerCombatant p, WeaponBranch b, out string message)
    {
        message = "";
        if (!CanForge(p)) { message = p.Name + "은(는) 이번엔 이미 강화했습니다"; return false; }
        int level = WeaponLevel(p, b);
        if (level >= GameBalance.WeaponMaxLevel) { message = BranchName(b) + "은(는) 이미 최대입니다"; return false; }

        switch (b)
        {
            case WeaponBranch.Blade: p.WeaponBlade++; break;      // 무기 강화
            case WeaponBranch.Hilt: p.WeaponHilt++; break;        // 갑옷 강화 (전투 시작 때 방어도로 쓰임)
            default:                                              // 체력 강화
                p.WeaponCore++;
                p.MaxHp += GameBalance.VitalMaxHpPerLevel;
                p.Hp += GameBalance.VitalMaxHpPerLevel;   // 늘어난 만큼 바로 채움
                break;
        }
        forgedThisVisit.Add(p);
        message = p.Name + " [" + BranchName(b) + "] " + (level + 1) + "단계: " + BranchEffect(b, level + 1);
        return true;
    }

    // 카드 강화: 덱의 deckIndex 번째 카드를 강화판으로 바꿈 (무기 강화와 합쳐서 한 사람당 한 번)
    public bool UpgradeCard(PlayerCombatant p, int deckIndex, out string message)
    {
        message = "";
        if (!CanForge(p)) { message = p.Name + "은(는) 이번엔 이미 강화했습니다"; return false; }
        if (deckIndex < 0 || deckIndex >= p.Deck.Count) { message = "카드를 찾을 수 없습니다"; return false; }
        CardData card = p.Deck[deckIndex];
        if (!card.CanUpgrade) { message = card.Name + "은(는) 더 강화할 수 없습니다"; return false; }
        CardData upgraded = card.MakeUpgraded();
        p.Deck[deckIndex] = upgraded;
        forgedThisVisit.Add(p);
        message = p.Name + " [" + card.Name + "] → [" + upgraded.Name + "]";
        return true;
    }

    // 강화할 수 있는 카드가 덱에 있는지
    public static bool HasUpgradableCard(PlayerCombatant p)
    {
        foreach (CardData c in p.Deck) if (c.CanUpgrade) return true;
        return false;
    }

    // 대장간을 나감 (강화를 안 해도 나갈 수 있음)
    // ==================== 상점 (2026-11-15) ====================
    // 플레이어마다 따로 물건을 깔아 둠 (자기 직업 카드). 골드도 각자
    public Dictionary<PlayerCombatant, List<ShopItem>> ShopStock = new Dictionary<PlayerCombatant, List<ShopItem>>();

    void MakeShop()
    {
        ShopStock.Clear();
        foreach (PlayerCombatant p in Players)
        {
            List<ShopItem> items = new List<ShopItem>();
            foreach (CardData c in RollCards(p, RewardKind.Hard, GameBalance.ShopCards))
            {
                int tier = System.Math.Max(0, System.Math.Min(GameBalance.ShopCardPrice.Length - 1, (int)c.Rarity - 1));
                int cardPrice = ShopPrice(GameBalance.ShopCardPrice[tier]);
                if (p.HasRelic(RelicLibrary.GoldenChalice)) cardPrice = DamageCalculator.RoundHalfUp(cardPrice * GameBalance.ChaliceCardPrice);   // 3-176 황금 성배 페널티
                items.Add(new ShopItem { Card = c, Price = cardPrice });
            }
            List<string> relicIds = new List<string>();
            for (int i = 0; i < GameBalance.ShopRelics; i++)
            {
                RelicData r = null;
                for (int t = 0; t < 10 && (r == null || relicIds.Contains(r.Id)); t++) r = RollRelic(p);
                if (r == null || relicIds.Contains(r.Id)) continue;
                relicIds.Add(r.Id);
                int tier = r.Rarity == RelicRarity.Hero ? 2 : (r.Rarity == RelicRarity.Rare ? 1 : 0);
                items.Add(new ShopItem { Relic = r, Price = ShopPrice(GameBalance.ShopRelicPrice[tier]) });
            }
            // 3-176 연금술사의 황금 플라스크 페널티: 그 사람 상점에는 포션이 안 나옴
            if (!p.HasRelic(RelicLibrary.GoldenFlask))
                for (int i = 0; i < GameBalance.ShopPotions; i++)
                    items.Add(new ShopItem { Potion = PotionLibrary.Random(rng, Players.Count), Price = ShopPrice(GameBalance.ShopPotionPrice) });
            ShopStock[p] = items;
        }
    }

    // 사기. 결과 문장을 돌려줌
    public string Buy(PlayerCombatant p, ShopItem item)
    {
        if (item == null || item.Sold) return "이미 팔린 물건입니다";
        if (p.Gold < item.Price) return "골드가 부족합니다 (" + p.Gold + " / " + item.Price + ")";
        if (item.Potion != null && p.Potions.Count >= GameBalance.PotionSlots) return "포션 칸이 가득 찼습니다";
        if (item.Relic != null && p.HasRelic(item.Relic.Id)) return "이미 가진 아티팩트입니다";
        p.Gold -= item.Price;
        item.Sold = true;
        if (item.Card != null) p.Deck.Add(item.Card);
        else if (item.Relic != null) GiveRelic(p, item.Relic);
        else if (item.Potion != null) p.Potions.Add(item.Potion.Id);
        return item.Name + " 구입! (남은 골드 " + p.Gold + ")";
    }

    // ==================== 3-163: 아티팩트 팔기 ====================
    // 팔 수 있는지: 시작(직업) · 공용(파티) 아티팩트는 못 팖
    public static bool CanSellRelic(RelicData r)
    {
        return r != null && r.Rarity != RelicRarity.Starter && r.Rarity != RelicRarity.Party;
    }

    // 파는 값 = 상점 가격(등급 · 스테이지) × RelicSellRatio
    public int RelicSellPrice(RelicData r)
    {
        int tier = (r.Rarity == RelicRarity.Hero || r.Rarity == RelicRarity.Boss) ? 2 : (r.Rarity == RelicRarity.Rare ? 1 : 0);
        return DamageCalculator.RoundHalfUp(ShopPrice(GameBalance.ShopRelicPrice[tier]) * GameBalance.RelicSellRatio);
    }

    // 팔기. 결과 문장을 돌려줌. 얻을 때 바로 바뀐 것(최대 HP)은 되돌림 (GiveRelic 의 반대)
    public string SellRelic(PlayerCombatant p, RelicData r)
    {
        if (Place != RunPlace.Shop) return "상점에서만 팔 수 있습니다";
        if (r == null || !p.Relics.Contains(r)) return "가지고 있지 않은 아티팩트입니다";
        if (!CanSellRelic(r)) return r.Name + " 은(는) 팔 수 없습니다 (시작·공용 아티팩트)";
        int price = RelicSellPrice(r);
        p.Relics.Remove(r);
        if (r.Id == RelicLibrary.GuardHelm)
        {
            p.MaxHp = System.Math.Max(1, p.MaxHp - GameBalance.GuardHelmHp);
            if (p.Hp > p.MaxHp) p.Hp = p.MaxHp;
        }
        if (r.Id == RelicLibrary.TwilightRing)
        {
            // 얻을 때 최대 HP × 20% 를 잃었음 → 지금 최대 HP 에서 거꾸로 계산해 되돌림 (예: 80 → 100). 회복은 안 함
            int before = DamageCalculator.RoundHalfUp(p.MaxHp / (1f - GameBalance.TwilightRingHpLoss));
            p.MaxHp = System.Math.Max(p.MaxHp, before);
        }
        if (r.Id == RelicLibrary.EmberCore)   // 3-176: 얻을 때 잃은 최대 HP 10% 되돌림 (회복은 안 함)
        {
            int before = DamageCalculator.RoundHalfUp(p.MaxHp / (1f - GameBalance.EmberCoreHpLoss));
            p.MaxHp = System.Math.Max(p.MaxHp, before);
        }
        p.Gold += price;
        return r.Name + " 판매! +" + price + " 골드 (남은 골드 " + p.Gold + ")";
    }

    public void LeaveShop()
    {
        if (Place != RunPlace.Shop) return;
        ShopStock.Clear();
        LeavePlace();
    }

    // ==================== 레벨 (2026-11-15) ====================
    // 경험치를 주고, 레벨이 오르면 스탯 포인트를 줌. 오른 레벨 수를 돌려줌
    public static int GainExp(PlayerCombatant p, int amount)
    {
        int ups = 0;
        p.Exp += amount;
        while (p.Level < GameBalance.MaxLevel && p.Exp >= p.ExpToNext)
        {
            p.Exp -= p.ExpToNext;
            p.Level++;
            p.StatPoints += GameBalance.StatPointsPerLevel;
            ups++;
        }
        if (p.Level >= GameBalance.MaxLevel) p.Exp = 0;
        return ups;
    }

    // 레벨 업 화면에서 스탯 1 올리기 (0 = 힘, 1 = 체력, 2 = 민첩). 포인트가 없거나 최대면 false
    public static bool SpendStat(PlayerCombatant p, int which)
    {
        if (p.StatPoints <= 0) return false;
        int before = which == 0 ? p.Str : (which == 1 ? p.Con : p.Agi);
        if (before >= GameBalance.StatMax) return false;
        DevAddStat(p, which);
        p.StatPoints--;
        return true;
    }

    // 모든 스탯이 최대라 더 쓸 데가 없으면 남은 포인트를 버림
    public static void DiscardUnusablePoints(PlayerCombatant p)
    {
        if (p.Str >= GameBalance.StatMax && p.Con >= GameBalance.StatMax && p.Agi >= GameBalance.StatMax) p.StatPoints = 0;
    }

    // 보물 상자를 눌러서 엶 → 보상(아티팩트 또는 포션) 만들고 장소에서 나감
    // ==================== 보물 상자 (2026-11-15: 슬더스2 멀티처럼 한 사람이 1개씩 고름) ====================
    //   1) 상자에 들어가면 MakeTreasure: 물건 (인원 + 1)개를 만듦 (아티팩트·포션 섞어서)
    //   2) 상자를 누르면 OpenTreasure: 물건이 보임
    //   3) 사람마다 PickTreasure 로 1개 고름 (못 받는 물건은 고를 수 없음)
    //   4) 모두 고르면 ResolveTreasure: 같은 걸 고른 사람끼리 주사위 → 1명만 받고, 나머지는 남은 물건 중 무작위
    public List<ShopItem> TreasureItems = new List<ShopItem>();   // 상자 안 물건 (가격은 0, 상점 물건 칸을 같이 씀)
    public int[] TreasurePicks = new int[0];                       // TreasurePicks[사람 번호] = 고른 물건 번호 (-1 = 아직)
    public bool TreasureOpened;
    // 3-174: 상자 2개 (GameBalance.TreasureChests). 지금 몇 번째 상자인지 (0 = 첫 번째)
    //   첫 상자를 다 나누면 같은 보물 칸에서 두 번째 상자를 새로 만듦 → 다시 눌러서 열고 1개씩 고름
    public int TreasureChestIndex;
    public bool IsLastTreasureChest => TreasureChestIndex >= GameBalance.TreasureChests - 1;

    void MakeTreasure()
    {
        TreasureItems.Clear();
        TreasureOpened = false;
        TreasurePicks = new int[Players.Count];
        for (int i = 0; i < TreasurePicks.Length; i++) TreasurePicks[i] = -1;

        int count = Players.Count + GameBalance.TreasureExtraChoices;
        bool triedParty = false;
        for (int k = 0; k < count; k++)
        {
            ShopItem item = null;
            // 공용(파티) 아티팩트는 상자 하나에 최대 1개 (70%)
            if (!triedParty)
            {
                triedParty = true;
                if (rng.Next(100) < GameBalance.TreasurePartyRelicChance)
                {
                    RelicData pr = RollPartyRelic();
                    if (pr != null) item = new ShopItem { Relic = pr };
                }
            }
            // 반은 포션, 반은 아티팩트 (아티팩트는 무작위 한 사람의 직업 기준 → 다른 직업 전용이 섞일 수 있음)
            // 공용 카드 (2026-11-15): 상자 물건 하나가 공용 카드일 확률
            if (item == null && rng.Next(100) < GameBalance.TreasureCardChance)
            {
                List<CardData> commons = CardLibrary.RemoveMultiOnly(CardLibrary.CommonPoolCards(), Players.Count);
                if (commons.Count > 0)   // 다 빠져서 0장이면 카드는 건너뛰고 아래 포션·아티팩트로 넘어감
                {
                    CardData c = commons[rng.Next(commons.Count)];
                    bool dup = false;
                    foreach (ShopItem it in TreasureItems) if (it.Card != null && it.Card.Id == c.Id) dup = true;
                    if (!dup) item = new ShopItem { Card = c };
                }
            }
            if (item == null && rng.Next(100) < GameBalance.TreasurePotionChance)
            {
                item = new ShopItem { Potion = PotionLibrary.Random(rng, Players.Count) };
            }
            for (int tries = 0; item == null && tries < 8; tries++)
            {
                RelicData r = RollRelic(Players[rng.Next(Players.Count)]);
                if (r != null && !TreasureHasRelic(r.Id)) item = new ShopItem { Relic = r };
            }
            if (item == null) item = new ShopItem { Potion = PotionLibrary.Random(rng, Players.Count) };
            TreasureItems.Add(item);
        }
        Shuffle(TreasureItems);
    }

    bool TreasureHasRelic(string id)
    {
        foreach (ShopItem it in TreasureItems) if (it.Relic != null && it.Relic.Id == id) return true;
        return false;
    }

    // 상자를 누름 → 물건이 보임
    public void OpenTreasure()
    {
        if (Place != RunPlace.Treasure) return;
        TreasureOpened = true;
    }

    // 이 사람이 이 물건을 받을 수 있는지 (이미 가진 아티팩트·다른 직업 전용·포션 칸이 가득이면 X)
    public bool CanTakeTreasure(PlayerCombatant p, ShopItem item)
    {
        if (item == null || p == null) return false;
        if (item.Card != null) return true;   // 공용 카드는 누구나
        if (item.Relic != null)
        {
            RelicData r = item.Relic;
            if (p.HasRelic(r.Id)) return false;
            if (r.ClassOnly != PlayerClass.Common && r.ClassOnly != p.Class) return false;
            if (r.Rarity == RelicRarity.Party && PartyHas(r.Id)) return false;
            return true;
        }
        return p.Potions.Count < GameBalance.PotionSlots;
    }

    // 받을 수 있는 물건이 하나라도 있는지 (없으면 고르지 않고 골드를 받음)
    public bool CanTakeAnyTreasure(PlayerCombatant p)
    {
        foreach (ShopItem it in TreasureItems) if (CanTakeTreasure(p, it)) return true;
        return false;
    }

    public void PickTreasure(int player, int item)
    {
        if (player < 0 || player >= TreasurePicks.Length || item < 0 || item >= TreasureItems.Count) return;
        if (!CanTakeTreasure(Players[player], TreasureItems[item])) return;
        TreasurePicks[player] = item;
    }

    // 모두 골랐는지 (받을 게 없는 사람은 건너뜀)
    public bool AllTreasurePicked
    {
        get
        {
            for (int i = 0; i < Players.Count; i++)
                if (TreasurePicks[i] < 0 && CanTakeAnyTreasure(Players[i])) return false;
            return true;
        }
    }

    // 나눠 주기. 결과 문장들을 돌려주고 보물 칸을 나감
    public List<string> ResolveTreasure()
    {
        List<string> lines = new List<string>();
        bool[] taken = new bool[TreasureItems.Count];
        List<int> losers = new List<int>();

        // 1) 물건마다 고른 사람들 중 1명 (여러 명이면 주사위)
        for (int j = 0; j < TreasureItems.Count; j++)
        {
            List<int> who = new List<int>();
            for (int i = 0; i < Players.Count; i++) if (TreasurePicks[i] == j) who.Add(i);
            if (who.Count == 0) continue;
            int win = who[rng.Next(who.Count)];
            GiveTreasure(Players[win], TreasureItems[j]);
            taken[j] = true;
            if (who.Count > 1)
            {
                List<string> names = new List<string>();
                foreach (int i in who) names.Add(Players[i].Name);
                lines.Add("[" + TreasureItems[j].Name + "] 을(를) " + string.Join(", ", names) + " 이(가) 같이 골라서 주사위! → " + Players[win].Name + " 승리");
                foreach (int i in who) if (i != win) losers.Add(i);
            }
            else lines.Add(Players[win].Name + " : " + TreasureItems[j].Name);
        }

        // 2) 주사위에서 진 사람: 남은 물건 중 받을 수 있는 것 무작위
        foreach (int i in losers)
        {
            List<int> left = new List<int>();
            for (int j = 0; j < TreasureItems.Count; j++) if (!taken[j] && CanTakeTreasure(Players[i], TreasureItems[j])) left.Add(j);
            if (left.Count > 0)
            {
                int j = left[rng.Next(left.Count)];
                GiveTreasure(Players[i], TreasureItems[j]);
                taken[j] = true;
                lines.Add(Players[i].Name + " : 대신 " + TreasureItems[j].Name);
            }
            else
            {
                Players[i].Gold += GameBalance.TreasureFallbackGold;
                lines.Add(Players[i].Name + " : 남은 물건이 없어서 골드 +" + GameBalance.TreasureFallbackGold);
            }
        }

        // 3) 받을 수 있는 게 하나도 없던 사람
        for (int i = 0; i < Players.Count; i++)
        {
            if (TreasurePicks[i] >= 0 || CanTakeAnyTreasure(Players[i])) continue;
            Players[i].Gold += GameBalance.TreasureFallbackGold;
            lines.Add(Players[i].Name + " : 받을 수 있는 물건이 없어서 골드 +" + GameBalance.TreasureFallbackGold);
        }

        TreasureItems.Clear();
        TreasurePicks = new int[0];
        TreasureOpened = false;
        // 3-174: 상자가 더 남았으면 같은 칸에서 다음 상자를 만듦 (닫힌 상자부터 다시). 마지막 상자면 칸을 나감
        if (!IsLastTreasureChest)
        {
            TreasureChestIndex++;
            MakeTreasure();
        }
        else LeavePlace();
        return lines;
    }

    void GiveTreasure(PlayerCombatant p, ShopItem item)
    {
        if (item.Card != null) p.Deck.Add(item.Card);
        else if (item.Relic != null) GiveRelic(p, item.Relic);
        else if (item.Potion != null && p.Potions.Count < GameBalance.PotionSlots) p.Potions.Add(item.Potion.Id);
    }

    public void LeaveForge()
    {
        if (Place != RunPlace.Forge) return;
        LeavePlace();
    }

    void AdvanceStep()
    {
        Step++;
        MakeChoices();
    }

    // 비전투 칸 효과 (수성과 지도가 같이 씀). 결과 문장을 돌려줌
    string ApplyNonCombat(MapNodeType type)
    {
        // 초소·대장간은 EnterPlace 가 따로 처리
        if (type == MapNodeType.Treasure)
        {
            MakeRewards(RewardKind.Treasure);
            return "보물 상자를 열었다!";
        }
        // 이벤트·상점은 아직 준비 중
        return RunMap.TypeName(type) + ": 준비 중 (아직 아무 일도 일어나지 않습니다)";
    }

    // ==================== 지도 (4스테이지) ====================
    // 지도에서 칸을 골랐을 때. 전투 칸이면 true (화면이 CreateBattle 을 불러야 함)
    public bool EnterNode(MapNode node, out string message)
    {
        message = "";
        if (Map == null || !Map.CanMoveTo(node)) { message = "갈 수 없는 칸입니다"; return false; }

        if (node.IsBattle)
        {
            PendingNode = node;   // 이길 때까지는 "이동 중"
            return true;
        }

        Map.MoveTo(node);
        if (EnterPlace(node.Type)) return false;   // 야영지·대장간: 장소 화면으로
        message = ApplyNonCombat(node.Type);
        return false;
    }

    // ==================== 전투 만들기 ====================
    // 고른 칸(수성 선택지 또는 지도 칸)의 전투를 만들어서 시작 상태로 돌려줌
    public BattleSystem CreateBattle()
    {
        string[] plan;
        if (PendingChoice != null)
        {
            if (PendingChoice.Kind == SiegeChoiceKind.Boss) plan = new[] { "B1" };
            else if (PendingChoice.Kind == SiegeChoiceKind.HardFight) plan = PickPlan(Stage == 1 ? GameBalance.Stage1HardFights : GameBalance.HardFights);
            else if (PendingChoice.Fight == 'W') plan = PickPlan(GameBalance.TwoWaveFights);   // 일반 2웨이브 (3-110: 2스테이지 5칸)
            else if (PendingChoice.Fight == 'V') plan = PickPlan(Stage == 1 ? GameBalance.Stage1VeteranFights : GameBalance.VeteranFights);   // 정예 전투
            else plan = PickPlan(Stage == 1 ? GameBalance.Stage1EasyFights : GameBalance.EasyFights);
        }
        else if (PendingNode != null)
        {
            if (PendingNode.Type == MapNodeType.Boss) plan = new[] { IsFinalChapter ? "A1" : "B1" };   // 3스테이지 = 수문장, 최종장 = 설계자
            else if (PendingNode.Type == MapNodeType.Elite) plan = PickPlan(GameBalance.MapHardFights);
            else if (PendingNode.Type == MapNodeType.Veteran) plan = PickPlan(IsFinalChapter ? GameBalance.FinalVeteranFights : GameBalance.MapVeteranFights);   // 3-111 정예 칸
            else plan = PickPlan(GameBalance.MapEasyFights);   // 몬스터 칸 = 일반 전투 (3-111: 4스테이지도)
        }
        else
        {
            plan = PickPlan(GameBalance.EasyFights);
        }
        return CreateBattleFromPlan(plan, PendingLabel);
    }

    // (테스트용) 웨이브 구성을 직접 정해서 전투. 진행 위치는 안 바뀜
    public BattleSystem CreateTestBattle(string[] plan)
    {
        PendingNode = null;
        PendingChoice = null;
        return CreateBattleFromPlan(plan, "테스트 전투");
    }

    // 전투 시작 방어도: 장비 보강(한 번 쓰면 사라짐) + 무기 자루(매 전투)
    void ApplyStartBlocks(BattleSystem battle)
    {
        foreach (PlayerCombatant p in Players)
        {
            battle.GiveStartBlock(p, NextBattleBlock, "장비 보강");
            battle.GiveStartBlock(p, p.WeaponHilt * GameBalance.HiltStartBlockPerLevel, "갑옷 강화");
        }
        NextBattleBlock = 0;
    }

    // ==================== 3-196 지옥: 사용 금지 ====================
    // 전투가 시작될 때 플레이어마다 덱에서 무작위 N장 (N = 스테이지 수, 덱의 1/4 까지). 방해 카드는 고르지 않음
    //   카드에 Sealed = true → CanPlay 가 막고, 화면은 붉은 사슬로 보여 줌. 전투가 끝나면 FinishBattle 에서 풀림
    void ApplySeals(BattleSystem battle)
    {
        ClearSeals();
        foreach (PlayerCombatant p in Players)
        {
            List<CardData> pool = new List<CardData>();
            foreach (CardData c in p.Deck) if (!c.Unplayable && !c.StatusCard) pool.Add(c);
            int n = DifficultyInfo.SealCount(Difficulty, Stage, pool.Count);
            List<string> names = new List<string>();
            for (int k = 0; k < n && pool.Count > 0; k++)
            {
                int i = rng.Next(pool.Count);
                pool[i].Sealed = true;
                names.Add(pool[i].Name);
                pool.RemoveAt(i);
            }
            if (names.Count > 0) battle.AddLog("[지옥] " + p.Name + " 사용 금지: " + string.Join(", ", names));
        }
    }

    public void ClearSeals()
    {
        foreach (PlayerCombatant p in Players)
            foreach (CardData c in p.Deck) c.Sealed = false;
    }

    string[] PickPlan(string[][] table)
    {
        return table[rng.Next(table.Length)];
    }

    BattleSystem CreateBattleFromPlan(string[] plan, string label)
    {
        List<List<EnemyCombatant>> waves = EnemyLibrary.BuildWaves(plan, CurrentRace, rng, CurrentBossKind, Stage);
        // 2026-11-15: 3스테이지 보스 = 요새의 수문장 (그림: 예전 설계자 그림 → UnitArt "FortressLord")
        if (Stage == 3)
            foreach (List<EnemyCombatant> w in waves)
                foreach (EnemyCombatant e in w)
                    if (e.Name.Contains("수문장") && !e.Name.StartsWith("요새")) e.Name = "요새의 " + e.Name;
        // 3-162: 이 전투에서 가장 높은 몬스터 등급 (골드 보상에 씀)
        PendingTopRank = EnemyRank.Normal;
        foreach (List<EnemyCombatant> w in waves)
            foreach (EnemyCombatant e in w)
                if (e.Rank > PendingTopRank) PendingTopRank = e.Rank;
        BattleSystem battle = new BattleSystem(Players, waves, rng.Next());
        battle.Stage = Stage;
        battle.Difficulty = Difficulty;   // 3-196: 몬스터 체력·공격력
        ApplySeals(battle);               // 3-196 지옥: 사용 금지 카드
        // 방어도 절반 유지 = 수성(1~3스테이지) + 웨이브 2번 이상
        battle.KeepHalfBlock = IsSiege && waves.Count >= 2;
        LastPlanText = EnemyLibrary.DescribePlan(plan);
        string where = IsSiege ? ProgressText + " " + label + " (" + EnemyLibrary.RaceName(CurrentRace) + ")" : ProgressText + " " + label;
        battle.AddLog("[런] " + Stage + "스테이지 " + where + " / 구성: " + LastPlanText);
        battle.StartBattle();
        ApplyStartBlocks(battle);
        return battle;
    }

    // ==================== 전투가 끝난 뒤 ====================
    // 한 번 부름. 이기면 다음 칸으로 (보스면 다음 스테이지), 지면 런 종료
    public void FinishBattle(BattleSystem battle)
    {
        JustGotMap = false;
        ClearSeals();   // 3-196: 사용 금지는 그 전투에서만
        if (battle.Result == BattleResult.Defeat)
        {
            IsOver = true;
            return;
        }
        if (battle.Result != BattleResult.Victory) return;   // 아직 진행 중이면 아무것도 안 함

        BattlesWon++;
        // 3-112 엔딩 기록: 보스 / 엘리트 승리 수
        if (PendingIsBoss) BossWins++;
        else if ((PendingChoice != null && PendingChoice.Kind == SiegeChoiceKind.HardFight) || (PendingNode != null && PendingNode.Type == MapNodeType.Elite)) EliteWins++;

        // 쓰러졌던 플레이어는 HP 1로 부활 (슬더스2 멀티 방식). 보상 화면에 "부활" 이라고 보여 주려고 기억해 둠
        List<PlayerCombatant> revived = new List<PlayerCombatant>();
        foreach (PlayerCombatant player in Players)
        {
            if (player.IsDead)
            {
                player.Hp = GameBalance.ReviveHp;
                // 3-176 전우애의 문장 (파티): HP 1 대신 최대 HP 25%
                if (PartyHas(RelicLibrary.ComradeCrest)) player.Hp = System.Math.Max(GameBalance.ReviveHp, DamageCalculator.RoundHalfUp(player.MaxHp * GameBalance.ComradeCrestRevive));
                revived.Add(player);
            }
        }

        // 보상 (테스트 전투는 없음, 마지막 보스는 런이 끝나므로 없음)
        bool lastBoss = PendingIsBoss && Stage >= GameBalance.MaxStage;
        if ((PendingChoice != null || PendingNode != null) && !lastBoss)
        {
            RewardKind kind = RewardKind.Easy;
            if (PendingIsBoss) kind = RewardKind.Boss;
            else if (PendingChoice != null && PendingChoice.Kind == SiegeChoiceKind.HardFight) kind = RewardKind.Hard;
            else if (PendingNode != null && PendingNode.Type == MapNodeType.Elite) kind = RewardKind.Hard;
            // 3-175: 정예 전투인지 (수성 ??? 의 'V' 카드, 지도·최종장의 정예 칸). 보상 종류는 그대로 일반(Easy) + 아티팩트 기회만 더함
            bool veteran = kind == RewardKind.Easy &&
                ((PendingChoice != null && PendingChoice.Fight == 'V') || (PendingNode != null && PendingNode.Type == MapNodeType.Veteran));
            MakeRewards(kind, veteran);
            foreach (PlayerReward r in Rewards) if (revived.Contains(r.Player)) r.Revived = true;
        }

        if (PendingChoice != null)
        {
            SiegeChoice c = PendingChoice;
            PendingChoice = null;
            if (c.Kind == SiegeChoiceKind.Boss) StageCleared();
            else AdvanceStep();
            return;
        }

        MapNode node = PendingNode;
        PendingNode = null;
        if (node == null) return;   // 테스트 전투: 진행 위치 그대로

        Map.MoveTo(node);
        if (node.Type == MapNodeType.Boss) StageCleared();
    }

    void StageCleared()
    {
        if (Stage >= GameBalance.MaxStage)
        {
            Cleared = true;
            IsOver = true;
            return;
        }
        bool hadMap = HasMap;
        BeginStage(Stage + 1);
        JustGotMap = !hadMap && HasMap;   // 수성 마지막(2스테이지) 보스 → 지도 획득
    }

    // ==================== 개발 도구 (에디터·개발 빌드에서만 버튼이 보임) ====================
    // 수성: 지금 칸을 건너뛰고 다음 칸 선택지로 (보스 칸이면 다음 스테이지로)
    public void DevSkipStep()
    {
        Place = RunPlace.None;
        PendingChoice = null;
        PendingNode = null;
        if (!IsSiege) return;
        if (Step >= GameBalance.SiegeSteps) StageCleared();
        else AdvanceStep();
    }

    // 원하는 스테이지의 처음으로
    public void DevGoToStage(int stage)
    {
        Place = RunPlace.None;
        PendingChoice = null;
        PendingNode = null;
        BeginStage(System.Math.Max(1, System.Math.Min(stage, GameBalance.MaxStage)));
    }

    // 스탯 +1 (체력이면 최대 HP 도 같이)
    public static void DevAddStat(PlayerCombatant p, int which)
    {
        if (which == 0) p.Str = PlayerCombatant.ClampStat(p.Str + 1);
        else if (which == 2) p.Agi = PlayerCombatant.ClampStat(p.Agi + 1);
        else if (p.Con < GameBalance.StatMax)
        {
            p.Con++;
            int hp = p.HpPerCon;   // 3-147: 직업별 (마도사·스나이퍼 3 / 나머지 4)
            p.MaxHp += hp;
            p.Hp += hp;
        }
    }

    // ==================== 보상 ====================
    // 전투(쉬움/어려움/보스) 또는 보물 칸의 보상을 플레이어마다 만듦
    //   골드는 바로 지갑에, 엘리트·보물 아티팩트는 바로 가방에. 카드·보스 아티팩트는 화면에서 고름
    // 3-162: 전투 골드 = 가장 높은 몬스터 등급의 범위에서 무작위 × 스테이지 배율 (보물 칸 = 0)
    //   자바로 치면 switch 로 범위를 고르고 Math.round(gold * scale)
    int BattleGold(RewardKind kind)
    {
        if (kind == RewardKind.Treasure) return 0;
        EnemyRank rank = PendingTopRank;
        if (kind == RewardKind.Boss) rank = EnemyRank.Boss;
        else if (kind == RewardKind.Hard && rank < EnemyRank.Elite) rank = EnemyRank.Elite;   // 엘리트 칸인데 엘리트 등급이 없는 무리 (안전장치)
        int gold;
        switch (rank)
        {
            case EnemyRank.Boss:    gold = RandomRange(GameBalance.GoldBossMin, GameBalance.GoldBossMax); break;
            case EnemyRank.Elite:   gold = RandomRange(GameBalance.GoldHardMin, GameBalance.GoldHardMax); break;
            case EnemyRank.Veteran: gold = RandomRange(GameBalance.GoldVeteranMin, GameBalance.GoldVeteranMax); break;
            default:                gold = RandomRange(GameBalance.GoldEasyMin, GameBalance.GoldEasyMax); break;
        }
        float scale = GameBalance.StageGoldScale[System.Math.Max(0, System.Math.Min(GameBalance.StageGoldScale.Length - 1, Stage - 1))];
        return DamageCalculator.RoundHalfUp(gold * scale);
    }

    // 3-162: 스테이지별 상점 가격
    int ShopPrice(int basePrice)
    {
        float scale = GameBalance.ShopPriceScale[System.Math.Max(0, System.Math.Min(GameBalance.ShopPriceScale.Length - 1, Stage - 1))];
        scale *= DifficultyInfo.ShopPrice(Difficulty);   // 3-196: 지옥 +20%
        return DamageCalculator.RoundHalfUp(basePrice * scale);
    }

    // veteran = 정예 전투 승리 (3-175): 사람마다 35% 로 일반·희귀 아티팩트 1개
    //   = 뒤에 붙인 매개변수 기본값 (C# 의 "선택 매개변수". 자바라면 MakeRewards(kind) 를 하나 더 오버로드하는 것과 같음)
    public void MakeRewards(RewardKind kind, bool veteran = false)
    {
        Rewards.Clear();
        LastRewardKind = kind;
        foreach (PlayerCombatant p in Players)
        {
            PlayerReward r = new PlayerReward();
            r.Player = p;

            // 골드
            int gold = BattleGold(kind);   // 3-162: 몬스터 등급 + 스테이지
            if (gold > 0 && p.HasRelic(RelicLibrary.TyrantCrown)) gold = DamageCalculator.RoundHalfUp(gold * GameBalance.CrownGoldRatio);
            if (gold > 0 && p.HasRelic(RelicLibrary.GoldenChalice)) gold = DamageCalculator.RoundHalfUp(gold * (1f + GameBalance.ChaliceGoldBonus));   // 3-176 황금 성배
            if (gold > 0) gold = DamageCalculator.RoundHalfUp(gold * DifficultyInfo.Gold(Difficulty));   // 3-196: 쉬움 +20% / 어려움~ -15%
            r.Gold = gold;
            p.Gold += gold;

            // 경험치 (전투만)
            int exp = kind == RewardKind.Easy ? GameBalance.ExpEasy : (kind == RewardKind.Hard ? GameBalance.ExpHard : (kind == RewardKind.Boss ? GameBalance.ExpBoss : 0));
            if (exp > 0) { r.ExpGained = exp; r.LevelsGained = GainExp(p, exp); }

            // 공용 아티팩트: 전투에서 이기면 (보급 마차 = 골드, 야전 의무병 가방 = 회복)
            if (exp > 0 && !p.IsDead)
            {
                if (PartyHas(RelicLibrary.SupplyWagon)) { p.Gold += GameBalance.SupplyWagonGold; r.Gold += GameBalance.SupplyWagonGold; }
                if (p.HasRelic(RelicLibrary.AshSalve))   // 화산재 연고 (3-99): 잃은 HP 의 15%
                    p.Heal(DamageCalculator.RoundHalfUp((p.MaxHp - p.Hp) * GameBalance.AshSalveRatio));
                if (PartyHas(RelicLibrary.MedicBag)) p.Heal(GameBalance.MedicBagHeal);
            }

            // 카드 3장 중 1장 (보물 칸은 카드 없음)
            if (kind != RewardKind.Treasure)
            {
                bool key = p.HasRelic(RelicLibrary.IronKey);
                int count = key ? GameBalance.IronKeyChoices : GameBalance.CardRewardChoices;
                r.PicksLeft = key ? GameBalance.IronKeyPicks : GameBalance.CardRewardPicks;
                // 3-113: 공용 카드는 정해진 칸 수만큼 (열쇠로 2장일 때는 1장만 공용이면 직업 카드가 1장뿐이라 공용 0)
                r.CardChoices = RollCards(p, kind, count, key ? 0 : GameBalance.CardRewardCommonSlots);
                if (p.HasRelic(RelicLibrary.AlchemistMug))
                {
                    for (int i = 0; i < r.CardChoices.Count; i++)
                    {
                        CardData up = r.CardChoices[i].MakeUpgraded();
                        if (up != null) r.CardChoices[i] = up;
                    }
                }
            }
            r.CardDone = r.CardChoices.Count == 0;

            // 아티팩트: 어려운 전투·보물 = 1개 바로 받음, 보스 = 보스 아티팩트 중 고르기
            if (kind == RewardKind.Treasure)
            {
                // 보물 상자 (2026-11-15): 아티팩트 또는 포션 중 무작위. 포션 칸이 가득이면 아티팩트, 아티팩트가 없으면 포션
                bool potionFree = p.Potions.Count < GameBalance.PotionSlots;
                RelicData relic = null;
                bool wantPotion = potionFree && rng.Next(100) < GameBalance.TreasurePotionChance;
                if (!wantPotion)
                {
                    relic = rng.Next(100) < GameBalance.TreasurePartyRelicChance ? RollPartyRelic() : null;   // 공용(파티) 아티팩트는 보물 상자에서만
                    if (relic == null) relic = RollRelic(p);
                }
                if (relic != null) { GiveRelic(p, relic); r.Relic = relic; }
                else if (potionFree)
                {
                    PotionData potion = PotionLibrary.Random(rng, Players.Count);
                    p.Potions.Add(potion.Id);
                    r.Potion = potion;
                }
            }
            else if (kind == RewardKind.Hard)
            {
                RelicData relic = RollRelic(p);
                if (relic != null) { GiveRelic(p, relic); r.Relic = relic; }
            }
            else if (veteran && rng.Next(100) < GameBalance.VeteranRelicChance)
            {
                // 3-175: 정예 승리 → 35% 로 일반(특별 포함) 60 / 희귀 40. 화면은 엘리트와 같이 "아티팩트 획득!" 상자
                RelicData relic = RollVeteranRelic(p);
                if (relic != null) { GiveRelic(p, relic); r.Relic = relic; }
            }
            else if (kind == RewardKind.Boss)
            {
                List<RelicData> choices = RelicLibrary.Available(p, RelicRarity.Boss);
                // 3-176: 결속의 횃불(파티 보스)은 멀티에서만, 파티에 1개만
                choices.RemoveAll(rc => rc.MultiOnly && (Players.Count < GameBalance.PartyRelicMinPlayers || PartyHas(rc.Id)));
                Shuffle(choices);
                if (choices.Count > 3) choices.RemoveRange(3, choices.Count - 3);
                // 보스 아티팩트를 다 가졌으면 영웅 아티팩트로 채움
                List<RelicData> heroes = RelicLibrary.Available(p, RelicRarity.Hero);
                Shuffle(heroes);
                for (int i = 0; choices.Count < 3 && i < heroes.Count; i++) choices.Add(heroes[i]);
                if (choices.Count > 0) r.BossChoices = choices;
            }
            Rewards.Add(r);
        }
    }

    public void TakeRewardCard(PlayerReward r, int index)
    {
        if (r.CardDone || index < 0 || index >= r.CardChoices.Count) return;
        r.TakenCard = r.CardChoices[index];
        r.Player.Deck.Add(r.TakenCard);
        r.TakenCards.Add(r.TakenCard);
        r.CardChoices.RemoveAt(index);   // 3-113: 고른 카드는 후보에서 빠지고, 남은 카드 중에서 더 고를 수 있음
        r.PicksLeft--;
        if (r.PicksLeft <= 0 || r.CardChoices.Count == 0) r.CardDone = true;
    }

    public void SkipRewardCard(PlayerReward r)
    {
        r.CardDone = true;
    }

    public void TakeBossRelic(PlayerReward r, int index)
    {
        if (r.BossChoices == null || r.BossDone || index < 0 || index >= r.BossChoices.Count) return;
        r.TakenBossRelic = r.BossChoices[index];
        GiveRelic(r.Player, r.TakenBossRelic);
        r.BossDone = true;
    }

    public void SkipBossRelic(PlayerReward r)
    {
        r.BossDone = true;
    }

    public bool AllRewardsDone
    {
        get
        {
            foreach (PlayerReward r in Rewards) if (!r.Done) return false;
            return true;
        }
    }

    // ==================== 3-114 제물의 제단 (엘리트 보상 뒤) ====================
    // 이 사람이 제단을 쓸 수 있는지 (덱이 충분히 많을 때만)
    public bool AltarAvailable(PlayerCombatant p)
    {
        return p.Deck.Count >= GameBalance.AltarMinDeck;
    }

    // 3-127: 제단이 보여 줄 카드 10장 (직업 전용 카드만, 공용 X)
    //   1) 직업 카드를 등급별 주머니 4개로 나눔 (혼자면 멀티 전용 빼기)
    //   2) 10번 반복: 등급을 AltarCardOdds 로 뽑고 → 그 주머니에서 무작위 1장을 꺼냄 (꺼낸 카드는 주머니에서 빠짐 = 중복 없음)
    //      그 주머니가 비었으면 한 단계 위 등급 → 맨 위까지 비었으면 아래 등급으로 찾음
    //   3) 일반 → 희귀 → 영웅 → 전설 순서로 정렬해서 돌려줌 (위 줄에 일반, 아래 줄 끝에 좋은 카드)
    //   자바로 치면 Map<Rarity, List<Card>> 를 만들어 두고 Collections.shuffle 뒤 remove(0) 하는 것과 같음
    public List<CardData> AltarOffer(PlayerCombatant p)
    {
        return RollClassOffer(p, GameBalance.AltarOfferCount, GameBalance.AltarCardOdds);
    }

    // 직업 카드 count 장 (중복 없음). 한 장마다 odds(일반·희귀·영웅·전설 %)로 등급을 따로 뽑음
    //   확률이 0 인 등급은 빈 주머니로 칠 때도 내려가지 않음 (피의 제단 = 희귀 이상 보장)
    List<CardData> RollClassOffer(PlayerCombatant p, int count, int[] w)
    {
        CardRarity[] tiers = { CardRarity.Common, CardRarity.Rare, CardRarity.Hero, CardRarity.Legend };
        List<CardData>[] bags = new List<CardData>[tiers.Length];
        for (int t = 0; t < tiers.Length; t++) bags[t] = new List<CardData>();
        foreach (CardData c in CardLibrary.RemoveMultiOnly(CardLibrary.ClassPool(p.Class), Players.Count))
            for (int t = 0; t < tiers.Length; t++)
                if (c.Rarity == tiers[t]) bags[t].Add(c);

        int total = 0;
        foreach (int x in w) total += x;

        List<CardData> offer = new List<CardData>();
        for (int k = 0; k < count; k++)
        {
            // 등급 뽑기 (룰렛)
            int r = rng.Next(System.Math.Max(1, total));
            int pick = 0;
            for (int i = 0; i < w.Length; i++) { if (r < w[i]) { pick = i; break; } r -= w[i]; }

            // 비어 있으면 위 등급 → 그래도 없으면 아래 등급
            int use = -1;
            for (int t = pick; t < tiers.Length && use < 0; t++) if (bags[t].Count > 0) use = t;
            for (int t = pick - 1; t >= 0 && use < 0; t--) if (bags[t].Count > 0 && w[t] > 0) use = t;   // 확률 0 등급으로는 안 내려감
            if (use < 0) break;   // 직업 카드를 다 꺼냄 (10장보다 적게 나올 수 있음)

            int idx = rng.Next(bags[use].Count);
            offer.Add(bags[use][idx]);
            bags[use].RemoveAt(idx);
        }
        offer.Sort((a, b) => ((int)a.Rarity).CompareTo((int)b.Rarity));   // 등급 순 (자바의 Comparator)
        return offer;
    }

    // ==================== 3-129 피의 제단 (1·2스테이지 4칸) ====================
    public const string BloodAltarName = "피의 제단";

    // 이번 스테이지가 피의 제단을 쓰는 스테이지인지 (수성 1·2스테이지)
    public bool IsBloodAltarStage
    {
        get { return IsSiege && Stage <= GameBalance.BloodAltarStages; }
    }

    public bool BloodAltarAvailable(PlayerCombatant p)
    {
        return p.Deck.Count >= GameBalance.BloodAltarMinDeck;
    }

    // 이 사람이 바치면 잃을 최대 HP (반올림, 최소 1)
    public int BloodAltarHpCost(PlayerCombatant p)
    {
        return System.Math.Max(1, DamageCalculator.RoundHalfUp(p.MaxHp * GameBalance.BloodAltarHpLoss));
    }

    // 직업 카드 3장 (희귀 이상). 뽑는 방법은 제물의 제단과 같고 확률표만 다름
    public List<CardData> BloodAltarOffer(PlayerCombatant p)
    {
        return RollClassOffer(p, GameBalance.BloodAltarOfferCount, GameBalance.BloodAltarCardOdds);
    }

    // 바친 카드 빼기 + 고른 카드 넣기 + 유물 1개 + 최대 HP 감소. 결과 문장을 돌려줌
    public string BloodAltarExchange(PlayerCombatant p, List<CardData> sacrificed, CardData gained)
    {
        AltarExchange(p, sacrificed, gained);
        RelicData relic = RollRelic(p);
        GiveRelic(p, relic);
        int loss = BloodAltarHpCost(p);
        p.MaxHp = System.Math.Max(1, p.MaxHp - loss);
        if (p.Hp > p.MaxHp) p.Hp = p.MaxHp;
        return p.Name + ": [" + (gained != null ? gained.Name : "-") + "]" + (relic != null ? " + 유물 [" + relic.Name + "]" : "") + "  /  최대 HP -" + loss;
    }

    // 피의 제단을 나감 → 다음 칸 (화면이 부름)
    public void LeaveBloodAltar()
    {
        if (Place == RunPlace.BloodAltar) LeavePlace();
    }

    // 바친 카드를 덱에서 빼고, 고른 카드를 넣음
    public void AltarExchange(PlayerCombatant p, List<CardData> sacrificed, CardData gained)
    {
        foreach (CardData c in sacrificed) p.Deck.Remove(c);   // 같은 이름 카드가 여러 장이어도 고른 그 한 장만 (참조로 지움)
        if (gained != null) p.Deck.Add(gained);
    }

    public void ClearRewards()
    {
        Rewards.Clear();
    }

    // 아티팩트를 가방에 넣음 + 얻는 즉시 효과 (수성 병사의 투구)
    // 파티 중 누가 이 아티팩트를 가졌는지 (공용 아티팩트는 한 명만 가져도 모두에게 효과)
    public bool PartyHas(string id)
    {
        foreach (PlayerCombatant p in Players) if (p.HasRelic(id)) return true;
        return false;
    }

    // 보물 상자용 공용 아티팩트 뽑기 (파티에 아직 없는 것). 없으면 null
    RelicData RollPartyRelic()
    {
        // 3-174: 공용(파티) 아티팩트는 멀티 전용. 싱글이면 null → 부르는 쪽이 일반 아티팩트·포션·카드로 채움
        if (Players.Count < GameBalance.PartyRelicMinPlayers) return null;
        List<RelicData> list = new List<RelicData>();
        foreach (RelicData r in RelicLibrary.All)
            if (r.Rarity == RelicRarity.Party && !PartyHas(r.Id)) list.Add(r);
        if (list.Count == 0) return null;
        return list[rng.Next(list.Count)];
    }

    public void GiveRelic(PlayerCombatant p, RelicData relic)
    {
        if (relic == null || p.HasRelic(relic.Id)) return;
        p.Relics.Add(relic);
        if (relic.Id == RelicLibrary.BondStone)
        {
            foreach (PlayerCombatant q in Players) { q.MaxHp += GameBalance.BondStoneHp; q.Hp += GameBalance.BondStoneHp; }
        }
        if (relic.Id == RelicLibrary.GuardHelm)
        {
            p.MaxHp += GameBalance.GuardHelmHp;
            p.Hp += GameBalance.GuardHelmHp;
        }
        if (relic.Id == RelicLibrary.UnityHelm)   // 3-176 결속의 투구 (파티): 모두 최대 HP +5
        {
            foreach (PlayerCombatant q in Players) { q.MaxHp += GameBalance.UnityHelmHp; q.Hp += GameBalance.UnityHelmHp; }
        }
        if (relic.Id == RelicLibrary.EmberCore)   // 3-176 꺼지지 않는 불씨핵 페널티: 최대 HP -10%
        {
            int lossE = DamageCalculator.RoundHalfUp(p.MaxHp * GameBalance.EmberCoreHpLoss);
            p.MaxHp = System.Math.Max(1, p.MaxHp - lossE);
            if (p.Hp > p.MaxHp) p.Hp = p.MaxHp;
        }
        if (relic.Id == RelicLibrary.TwilightRing)   // 황혼의 계약 반지 페널티 (3-99): 최대 HP -20%
        {
            int loss = DamageCalculator.RoundHalfUp(p.MaxHp * GameBalance.TwilightRingHpLoss);
            p.MaxHp = System.Math.Max(1, p.MaxHp - loss);
            if (p.Hp > p.MaxHp) p.Hp = p.MaxHp;
        }
    }

    // 카드 count 장 뽑기 (같은 카드 중복 없음). 등급은 확률표 + 보정 장치
    // fixedCommon (3-113): 0 이상이면 공용 카드를 정확히 그 장 수만 (전투 보상), -1 이면 예전처럼 칸마다 확률 (상점)
    List<CardData> RollCards(PlayerCombatant p, RewardKind kind, int count, int fixedCommon = -1)
    {
        // 2026-11-15: 한 칸마다 CommonCardChance% 확률로 공용 카드 풀에서, 아니면 직업 카드 풀에서
        // 2026-11-15 (3-71): 혼자면 멀티 전용 카드 빼기 (상점도 이 함수를 써서 같이 적용됨)
        List<CardData> classPool = CardLibrary.RemoveMultiOnly(CardLibrary.ClassPool(p.Class), Players.Count);
        List<CardData> commonPool = CardLibrary.RemoveMultiOnly(CardLibrary.CommonPoolCards(), Players.Count);
        List<CardData> result = new List<CardData>();
        int[] odds = kind == RewardKind.Boss ? GameBalance.CardOddsBoss : (kind == RewardKind.Hard ? GameBalance.CardOddsHard : GameBalance.CardOddsEasy);

        for (int k = 0; k < count && (classPool.Count > 0 || commonPool.Count > 0); k++)
        {
            bool wantCommon = fixedCommon >= 0 ? k >= count - fixedCommon : rng.Next(100) < GameBalance.CommonCardChance;   // 고정이면 마지막 칸들이 공용
            List<CardData> pool = (wantCommon && commonPool.Count > 0) || classPool.Count == 0 ? commonPool : classPool;
            CardRarity want = RollCardRarity(odds, kind == RewardKind.Boss ? 0 : p.CardPity);
            List<CardData> same = new List<CardData>();
            foreach (CardData c in pool) if (c.Rarity == want) same.Add(c);
            if (same.Count == 0) same = pool;   // 그 등급이 다 떨어졌으면 아무거나
            CardData pick = same[rng.Next(same.Count)];
            pool.Remove(pick);
            result.Add(pick);

            // 보정 장치: 일반이 나오면 영웅 확률 +1%p, 영웅·전설이 나오면 0
            if (kind != RewardKind.Boss)
            {
                if (pick.Rarity == CardRarity.Common) p.CardPity = System.Math.Min(p.CardPity + GameBalance.CardPityStep, GameBalance.CardPityMax);
                else if (pick.Rarity == CardRarity.Hero || pick.Rarity == CardRarity.Legend) p.CardPity = 0;
            }
        }
        if (fixedCommon > 0) Shuffle(result);   // 공용 카드가 늘 오른쪽 끝에 있지 않게 자리 섞기
        return result;
    }

    // odds = { 일반, 희귀, 영웅, 전설 }. pity 만큼 일반 → 영웅으로 옮김
    CardRarity RollCardRarity(int[] odds, int pity)
    {
        int common = System.Math.Max(0, odds[0] - pity);
        int hero = odds[2] + (odds[0] - common);
        int[] w = { common, odds[1], hero, odds[3] };
        CardRarity[] tiers = { CardRarity.Common, CardRarity.Rare, CardRarity.Hero, CardRarity.Legend };
        int total = 0;
        foreach (int x in w) total += x;
        if (total <= 0) return CardRarity.Common;
        int r = rng.Next(total);
        for (int i = 0; i < w.Length; i++)
        {
            if (r < w[i]) return tiers[i];
            r -= w[i];
        }
        return CardRarity.Common;
    }

    // 엘리트·보물 아티팩트 1개 (일반 50 / 희귀 33 / 영웅 17). 그 등급이 다 떨어졌으면 다른 등급에서
    // 3-175: 정예 보상용. 일반(특별 포함) / 희귀 중 하나 (영웅 없음). 뽑힌 등급에 받을 게 없으면 다른 쪽, 둘 다 없으면 null
    RelicData RollVeteranRelic(PlayerCombatant p)
    {
        RelicRarity[] tiers = { RelicRarity.Common, RelicRarity.Rare };
        int[] w = GameBalance.VeteranRelicOdds;
        int first = rng.Next(w[0] + w[1]) < w[0] ? 0 : 1;
        for (int k = 0; k < tiers.Length; k++)
        {
            List<RelicData> list = RelicLibrary.Available(p, tiers[(first + k) % tiers.Length]);
            if (list.Count > 0) return list[rng.Next(list.Count)];
        }
        return null;
    }

    RelicData RollRelic(PlayerCombatant p)
    {
        RelicRarity[] tiers = { RelicRarity.Common, RelicRarity.Rare, RelicRarity.Hero };
        int[] w = GameBalance.RelicOdds;
        int total = 0;
        foreach (int x in w) total += x;
        int roll = rng.Next(total);
        int first = 0;
        for (int i = 0; i < w.Length; i++)
        {
            if (roll < w[i]) { first = i; break; }
            roll -= w[i];
        }
        for (int k = 0; k < tiers.Length; k++)
        {
            List<RelicData> list = RelicLibrary.Available(p, tiers[(first + k) % tiers.Length]);
            if (list.Count > 0) return list[rng.Next(list.Count)];
        }
        return null;
    }

    int RandomRange(int min, int max)
    {
        return min + rng.Next(max - min + 1);
    }

    void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }

    // ==================== 이어하기: 저장 / 불러오기 (2026-11-15, 3-73) ====================
    // 저장해도 되는 순간인지: 선택지·지도 화면에 있고, 싸우는 중·장소 안·보상 고르는 중이 아닐 때
    //   (그 사이 상태까지 저장하면 복잡해지고 오류가 나기 쉬워서, 칸과 칸 사이에서만 저장함)
    public bool CanSave
    {
        get { return !IsOver && Place == RunPlace.None && PendingChoice == null && PendingNode == null && Rewards.Count == 0; }
    }

    // 지금 런 → 저장 상자 (RunSave). 글자로 바꾸는 건 화면 쪽 SaveManager 가 함
    public RunSave ToSave()
    {
        RunSave s = new RunSave();
        s.Seed = rng.Next();   // 불러온 뒤 쓸 새 씨앗 (같은 저장을 불러오면 같은 씨앗 → 같은 전개)
        s.Stage = Stage;
        s.Difficulty = (int)Difficulty;   // 3-196
        s.HasMap = HasMap;
        s.BattlesWon = BattlesWon;
        s.EliteWins = EliteWins; s.BossWins = BossWins; s.PlaySeconds = PlaySeconds;   // 3-112
        s.StageRaces = new int[StageRaces.Length];
        for (int i = 0; i < StageRaces.Length; i++) s.StageRaces[i] = (int)StageRaces[i];   // enum → int (자바의 ordinal() 비슷)
        s.StageBosses = (int[])StageBosses.Clone();   // 3-102
        s.Step = Step;
        s.NextBattleBlock = NextBattleBlock;
        s.RevealNext = revealNext;
        s.StepChosen = new List<int>(StepChosen);

        foreach (SiegeChoice c in Choices)
            s.Choices.Add(new ChoiceSave { Kind = (int)c.Kind, NodeType = (int)c.NodeType, Hint = c.Hint ?? "", Hidden = c.Hidden, Title = c.Title ?? "", Fight = (int)c.Fight });

        if (Map != null)
        {
            // 1) 모든 칸을 한 줄로 펴고, 칸 → 번호 표를 만듦 (Dictionary = 자바의 HashMap)
            List<MapNode> all = Map.AllNodes();
            Dictionary<MapNode, int> index = new Dictionary<MapNode, int>();
            for (int i = 0; i < all.Count; i++) index[all[i]] = i;
            // 2) 칸마다 내용 + 이어진 칸 번호
            s.Map.FloorCount = Map.Floors.Length;
            foreach (MapNode n in all)
            {
                NodeSave ns = new NodeSave { Floor = n.Floor, Column = n.Column, Type = (int)n.Type, Theme = (int)n.Theme, Visited = n.Visited, SpotX = n.SpotX, SpotY = n.SpotY };
                List<int> next = new List<int>();
                foreach (MapNode m in n.Next) if (index.ContainsKey(m)) next.Add(index[m]);
                ns.Next = next.ToArray();
                s.Map.Nodes.Add(ns);
            }
            s.Map.BossIndex = Map.Boss != null && index.ContainsKey(Map.Boss) ? index[Map.Boss] : -1;
            s.Map.CurrentIndex = Map.Current != null && index.ContainsKey(Map.Current) ? index[Map.Current] : -1;
            int themes = Map.ColumnThemes != null ? Map.ColumnThemes.Length : 0;
            s.Map.ColumnThemes = new int[themes];
            for (int i = 0; i < themes; i++) s.Map.ColumnThemes[i] = (int)Map.ColumnThemes[i];
        }

        foreach (PlayerCombatant p in Players)
        {
            PlayerSave ps = new PlayerSave
            {
                Class = (int)p.Class, Name = p.Name, Str = p.Str, Con = p.Con, Agi = p.Agi,
                Hp = p.Hp, MaxHp = p.MaxHp, Gold = p.Gold,
                Level = p.Level, Exp = p.Exp, StatPoints = p.StatPoints, CardPity = p.CardPity,
                WeaponBlade = p.WeaponBlade, WeaponHilt = p.WeaponHilt, WeaponCore = p.WeaponCore,
            };
            foreach (RelicData r in p.Relics) ps.Relics.Add(r.Id);
            ps.Potions.AddRange(p.Potions);
            foreach (CardData c in p.Deck) ps.Deck.Add(new CardSave { Id = c.Id, Upgraded = c.Upgraded });
            s.Players.Add(ps);
        }
        return s;
    }

    // 생성자 없이 빈 런을 만들 때만 씀 (FromSave 전용). private = 바깥에서는 못 부름
    private RunState() { }

    // 저장 상자 → 런. 불러올 수 없는 저장(형식이 다름, 사람 없음)이면 null
    //   없어진 카드·아티팩트 아이디는 조용히 건너뜀 (게임이 바뀌어도 멈추지 않게)
    public static RunState FromSave(RunSave s)
    {
        if (s == null || s.Version != RunSave.CurrentVersion || s.Players == null || s.Players.Count == 0) return null;

        RunState run = new RunState();
        run.rng = new System.Random(s.Seed);
        run.Stage = s.Stage;
        run.Difficulty = (Difficulty)System.Math.Max(0, System.Math.Min(4, s.Difficulty));   // 3-196 (옛 저장 = 1 보통)
        run.HasMap = s.HasMap;
        RunMap.RestName = "초소";   // 3-156
        run.BattlesWon = s.BattlesWon;
        run.EliteWins = s.EliteWins; run.BossWins = s.BossWins; run.PlaySeconds = s.PlaySeconds;   // 3-112 (옛 저장은 0)
        run.StageRaces = new Race[s.StageRaces != null ? s.StageRaces.Length : 0];
        for (int i = 0; i < run.StageRaces.Length; i++) run.StageRaces[i] = (Race)s.StageRaces[i];   // int → enum
        if (run.StageRaces.Length == 0) run.StageRaces = new[] { Race.Werewolf, Race.Orc, Race.Zombie };
        // 3-102: 옛 저장에는 보스 종류가 없음 → 0 (종족 수문장)
        run.StageBosses = new int[3];
        if (s.StageBosses != null) for (int i = 0; i < s.StageBosses.Length && i < 3; i++) run.StageBosses[i] = s.StageBosses[i];
        run.Step = s.Step;
        run.NextBattleBlock = s.NextBattleBlock;
        run.revealNext = s.RevealNext;
        if (s.StepChosen != null) run.StepChosen = new List<int>(s.StepChosen);

        foreach (ChoiceSave c in s.Choices)
        {
            SiegeChoice sc = new SiegeChoice { Kind = (SiegeChoiceKind)c.Kind, NodeType = (MapNodeType)c.NodeType, Hint = c.Hint, Hidden = c.Hidden, Title = string.IsNullOrEmpty(c.Title) ? null : c.Title, Fight = (char)c.Fight };
            // 옛 저장(3-110 전)에는 Fight 가 없음 → 이름으로 짐작
            if (sc.Fight == 0 && sc.Title != null && sc.Title.Contains("정예")) sc.Fight = 'V';
            if (sc.Fight == 0 && sc.Title != null && sc.Title.Contains("2웨이브")) sc.Fight = 'W';
            run.Choices.Add(sc);
        }

        if (s.Map != null && s.Map.Nodes != null && s.Map.Nodes.Count > 0)
        {
            RunMap map = new RunMap();
            map.Floors = new List<MapNode>[s.Map.FloorCount];
            for (int f = 0; f < map.Floors.Length; f++) map.Floors[f] = new List<MapNode>();
            // 1) 칸부터 전부 만들고  2) 번호를 보고 연결 (위층 Next, 아래층 Prev 둘 다)
            List<MapNode> nodes = new List<MapNode>();
            foreach (NodeSave ns in s.Map.Nodes)
            {
                MapNode n = new MapNode { Floor = ns.Floor, Column = ns.Column, Type = (MapNodeType)ns.Type, Theme = (PathTheme)ns.Theme, Visited = ns.Visited, SpotX = ns.SpotX, SpotY = ns.SpotY };
                nodes.Add(n);
                if (n.Floor >= 0 && n.Floor < map.Floors.Length) map.Floors[n.Floor].Add(n);
            }
            for (int i = 0; i < nodes.Count; i++)
                foreach (int k in s.Map.Nodes[i].Next)
                    if (k >= 0 && k < nodes.Count) { nodes[i].Next.Add(nodes[k]); nodes[k].Prev.Add(nodes[i]); }
            map.Boss = s.Map.BossIndex >= 0 && s.Map.BossIndex < nodes.Count ? nodes[s.Map.BossIndex] : null;
            map.Current = s.Map.CurrentIndex >= 0 && s.Map.CurrentIndex < nodes.Count ? nodes[s.Map.CurrentIndex] : null;
            map.ColumnThemes = new PathTheme[s.Map.ColumnThemes != null ? s.Map.ColumnThemes.Length : 0];
            for (int i = 0; i < map.ColumnThemes.Length; i++) map.ColumnThemes[i] = (PathTheme)s.Map.ColumnThemes[i];
            run.Map = map;
        }

        run.Players = new List<PlayerCombatant>();
        foreach (PlayerSave ps in s.Players)
        {
            // 직업 기본값(자원 이름·최대치·AP 여부)은 CreatePlayer 가 넣어 주고, 나머지는 저장값으로 덮어씀
            PlayerCombatant p = ClassLibrary.CreatePlayer((PlayerClass)ps.Class, ps.Name, ps.Str, ps.Con, ps.Agi, false);
            p.MaxHp = ps.MaxHp;
            p.Hp = System.Math.Max(1, System.Math.Min(ps.Hp, ps.MaxHp));
            p.Gold = ps.Gold;
            p.Level = ps.Level;
            p.Exp = ps.Exp;
            p.StatPoints = ps.StatPoints;
            p.CardPity = ps.CardPity;
            p.WeaponBlade = ps.WeaponBlade;
            p.WeaponHilt = ps.WeaponHilt;
            p.WeaponCore = ps.WeaponCore;
            // 아티팩트: 시작 아티팩트까지 저장되어 있으니 비우고 다시 넣음 (얻을 때 효과(최대 HP+)는 MaxHp 에 이미 들어 있음)
            p.Relics.Clear();
            foreach (string id in ps.Relics) { RelicData r = RelicLibrary.Get(id); if (r != null) p.Relics.Add(r); }
            p.Potions.Clear();
            foreach (string id in ps.Potions) if (PotionLibrary.Get(id) != null) p.Potions.Add(id);
            p.Deck.Clear();
            foreach (CardSave cs in ps.Deck) { CardData c = CardLibrary.FromId(cs.Id, cs.Upgraded); if (c != null) p.Deck.Add(c); }
            run.Players.Add(p);
        }
        run.Difficulty = run.Difficulty;   // 3-196: 플레이어가 생긴 뒤 경험치 배율을 다시 맞춤
        return run;
    }

    // 지금 싸우는 전투가 보스인지 (결과 화면 문구용)
    public bool PendingIsBoss
    {
        get
        {
            if (PendingChoice != null) return PendingChoice.Kind == SiegeChoiceKind.Boss;
            return PendingNode != null && PendingNode.Type == MapNodeType.Boss;
        }
    }

    // 이 보스를 잡으면 지도를 얻는지 (수성 마지막 스테이지 보스)
    public bool PendingGivesMap
    {
        get { return PendingIsBoss && IsSiege && Stage + 1 >= GameBalance.MapStage && Stage < GameBalance.MaxStage; }
    }
}
