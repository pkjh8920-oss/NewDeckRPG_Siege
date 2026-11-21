// ============================================================
// BattleScreen.cs
// 슬더스2 방식 전투 화면입니다.
//
// 사용법: 빈 게임 오브젝트에 이 스크립트를 붙이고 ▶ Play
//         (기존 BattleTest 오브젝트는 체크를 꺼서 비활성화)
//
// 규칙은 전부 BattleSystem 이 계산하고, 이 파일은 "보여 주기 + 입력 받기" 만 합니다.
//   카드를 놓으면 → battle.PlayCard(...) 호출 → Refresh() 로 화면 다시 맞추기
//
// 새 구조 (수성 + 공격)
//   1~3스테이지: ChoicePanel (??? 카드 고르기) → 전투 (웨이브 여러 번)
//   4스테이지  : MapPanel (지도) → 전투
//   웨이브가 바뀌면 적 칸만 새로 만들어서 바꿈 (WaveSwapRoutine)
//
// 화면 구성 (1920 x 1080 기준)
//   위쪽: 스테이지·턴 정보, 전투 선택, 새 전투, 기록 버튼
//   왼쪽: 플레이어 칸 / 오른쪽: 적 칸 (머리 위에 의도)
//   아래: 부채꼴 손패, 에너지 동그라미, 뽑기·버림 더미, 턴 종료 버튼
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class BattleScreen : MonoBehaviour
{
    [Header("테스트 설정")]
    public PlayerClass playerClass = PlayerClass.BlackScythe;
    public PlayerClass otherClass = PlayerClass.CosmicPhoenix;
    [Tooltip("켜면 보상 카드 전부를 처음부터 덱에 넣음 (위쪽 [새 런] 테스트용). 타이틀에서 시작한 게임은 항상 시작 덱 11장")]
    public bool addTestCards = false;
    [Range(1, 4)] public int playerCount = 1;
    [Range(1, 4)] public int stage = 1;          // 런을 몇 스테이지부터 시작할지 (테스트용)
    public int seed = 0;

    [Header("플레이어 스탯 (테스트용)")]
    [Range(0, 20)] public int str = 0;   // 힘: 모든 공격 피해 (INT 는 없앰)
    [Range(0, 20)] public int con = 0;   // 체력: 최대 HP
    [Range(0, 20)] public int agi = 0;   // 민첩: 크리티컬, 방어도 관통

    // ---------------- 규칙 ----------------
    private BattleSystem battle;
    private RunState run;                  // 런 전체 진행 (HP·덱이 다음 전투로 이어짐)
    private MapPanel mapPanel;             // 지도 화면 (4스테이지)
    private ChoicePanel choicePanel;       // 수성 화면 (1~3스테이지, ??? 카드)
    private PlacePanel placePanel;         // 초소·야영지 화면
    private ForgePanel forgePanel;         // 대장간 화면 (드워프 장인 + 모루)
    private RewardPanel rewardPanel;       // 전투 보상 / 보물 상자 화면 (카드 3장 중 1장, 아티팩트, 골드)
    private ShopPanel shopPanel;           // 상점 (2026-11-15)
    private TreasurePickPanel treasurePanel;   // 보물 상자 1인 1개 고르기 (2026-11-15)
    private LevelUpPanel levelUpPanel;     // 레벨 업 (2026-11-15)
    private EndingPanel endingPanel;       // 3-112: 4스테이지 보스를 잡은 뒤 엔딩
    private AltarPanel altarPanel;         // 3-114: 엘리트를 잡은 뒤 제물의 제단
    private TutorialPanel tutorialPanel;   // 3-130: 처음 보는 화면에서 한 번만 뜨는 안내
    private RelicBar relicBar;             // 위쪽 줄 아래: 플레이어마다 아티팩트 + 골드
    private bool bannerAfterRewards;       // 보상 화면을 닫은 뒤에 스테이지 배너를 보여 줄지
    private CompendiumPanel compendium;     // 3-180 도감 (게임에 있는 카드·아티팩트, 본 것만 공개)
    private float discoveryTimer;           // 3-180: 0.5초마다 "본 것" 훑기
    private DeckViewPanel deckView;        // 카드 목록 보기 (덱 / 뽑기 더미 / 버림 더미)
    private bool startedFromTitle;         // 타이틀에서 시작한 게임인지 (그러면 테스트 카드를 넣지 않음)
    private PauseMenu pauseMenu;           // Esc / [메뉴]: 일시정지 (계속·설정·타이틀로·종료)
    private DevPanel devPanel;             // F1: 개발자 테스트 도구 (에디터·개발 빌드에서만)
    private TitlePanel titlePanel;         // 타이틀 화면 (게임을 켜면 처음)
    private CharacterSelectPanel characterSelect;   // 캐릭터 선택 화면 (싱글플레이)
    private List<PlayerClass> runClasses;  // 이번 런의 플레이어 직업들 (싱글 1명 / 멀티 방에서 고른 사람들)
    private int shownWaveVersion;          // 화면에 그려진 적이 몇 번째 웨이브 것인지
    private int shownEnemyListVersion;     // 3-102: 웨이브 안에서 적 목록이 바뀐 것(점토 분열)을 알아채는 용도
    private bool waveSwapping;             // 다음 웨이브로 바꾸는 연출 중
    private RectTransform topBar;          // 위쪽 버튼 줄 (지도 위에도 보임)
    private int controlIndex;              // 지금 화면에서 조작하는 플레이어 번호 (동시 턴 테스트용 P1/P2 전환)
    private RectTransform playerTabs;      // P1 / P2 전환 버튼들
    private Text resultSubText;
    private Text resultButtonLabel;
    private PlayerCombatant shownPlayer;   // 지금 손패를 보여 주는 플레이어

    // ---------------- 화면 부품 ----------------
    private RectTransform root;            // 캔버스 전체
    private RectTransform stageRoot;           // 배경 + 캐릭터 (화면 흔들림은 이 상자만 흔듦)
    private float shakeTimer;
    private float shakePower;
    private bool busy;                     // 연출 중 (잠깐 입력을 막음)
    private CardData lastPlayed;           // 방금 낸 카드 (가운데에 보여 주기용)
    private Color slashColor = Color.white;

    // 필살기 연출용 화면 효과
    private Image stageDim;       // 배경만 어둡게 (캐릭터는 밝게 남음)
    private Image sceneArt;       // 스테이지 배경 그림 (1 = 성 안, 2~3 = 성 밖, 없으면 예전 밤 배경)
    private List<GameObject> plainBackground = new List<GameObject>();   // 그림이 없을 때만 보이는 예전 배경 조각들
    private Image vignette;       // 화면 가장자리 보랏빛 테두리
    private Image flash;          // 하얀 번쩍임
    private float dimTarget;      // 0 = 평소, 1 = 필살기 중
    private float dimAmount;
    private RectTransform handRoot;        // 손패 기준점 (화면 아래 가운데)
    private RectTransform enemyArea;
    private RectTransform playerArea;
    private RectTransform popupLayer;      // 떠오르는 숫자, 화살표, 안내 글
    private Text topText;
    private Text energyText;
    private Text manaText;
    // 3-120: 직업 자원 아이콘 (영혼·홍염·마나·탄환). 아이콘 안에 숫자. 그림이 없으면 예전 글자(manaText)
    private Image resIcon;
    private Text resNumber, resMax;
    private string resTip = "";
    private Text drawText;
    private Text discardText;
    private RectTransform drawPile;
    private RectTransform discardPile;
    private Button endTurnButton;
    private GameObject logPanel;
    private Text logText;
    private Text toastText;
    private float toastTimer;
    private GameObject resultPanel;
    private Text resultText;

    // 카드
    private List<CardView> handViews = new List<CardView>();
    private Dictionary<CardData, CardView> viewOf = new Dictionary<CardData, CardView>();
    private CardView hovered;
    private CardView dragging;
    private bool dragNeedsTarget;

    // 화살표 (점 여러 개로 곡선을 그림)
    private List<Image> arrowDots = new List<Image>();
    private EnemyCombatant arrowTarget;
    private Image arrowHead;

    // 적 / 플레이어 칸
    private class UnitView
    {
        public Combatant Unit;
        public RectTransform Rt;
        public Image Panel;
        public Image HpFill;
        public Text HpText;
        public Text NameText;
        public Text BlockText;
        public Text InfoText;
        public Text IntentText;
        public RectTransform IntentBox;    // 의도 상자 (위아래로 둥실둥실)
        public float IntentBaseY;   // (3-148: 상자 높이 = BattleScreen.IntentBoxH)
        public RectTransform IntentIcon;   // 의도 그림 (칼 / 방패 / 취약)
        public string IntentKey = "";      // 의도가 바뀌었을 때만 그림을 다시 그리기 위한 값
        public int LastHp;
        public int LastBlock;
        public int LastDebuff, LastBurn, LastFreeze, LastStrength;   // 3-186: 상태가 늘어나면 상태 이펙트
        public RectTransform Body;         // 그림만 들어 있는 상자 (숨쉬기·돌진·피격으로 움직임)
        public RectTransform StatusRow;    // 체력바 아래 상태 아이콘 줄 (❄ 동결, 발화, 처형 …)
        public string StatusKey = "";      // 아이콘 내용이 바뀌었을 때만 다시 그리기 위한 값
        public UnitMotion Motion;
        public float ArtHeight;
        public RectTransform HpRoot;       // 3-195: 체력바 전체 (방어도 방패도 이 안에 있음) → 여럿일 때 크기를 되돌릴 때 씀
        public Image HpFrame;              // 3-195: 체력바 틀 (방어도가 있으면 푸르게)
        public float IntroAlpha = 1f;      // 3-200: 등장 연출 중 투명도 (0 = 아직 안 나타남). 보통은 1
        public float HpBarH;               // 3-195: 체력바 높이 (상태 아이콘 줄 위치 계산용)
    }
    private List<UnitView> enemyViews = new List<UnitView>();
    private List<UnitView> playerViews = new List<UnitView>();

    // 색
    private readonly Color gold = new Color(0.95f, 0.78f, 0.35f);
    const float BlockIconSize = 58f;   // 3-171: 방어도 방패 그림 크기 (예전 파란 동그라미 46)

    // ==================== 시작 ====================
    void Start()
    {
        DiscoveryStore.Load();   // 3-180: 도감 기록 불러오기 (컴퓨터에 저장된 "본 카드·아티팩트")
        BuildCanvas();
        ShowTitle();   // 게임을 켜면 타이틀 화면부터
    }

    // 타이틀 화면 보이기 (위쪽 "타이틀" 버튼으로도 돌아옴)
    void ShowTitle()
    {
        characterSelect.Hide();
        StopAllCoroutines();
        Time.timeScale = 1f;
        relicBar.Show(null);
        if (playerTabs != null) { Destroy(playerTabs.gameObject); playerTabs = null; }   // 타이틀·캐릭터 선택에서는 P 탭 숨김
        rewardPanel.Hide();
        if (shopPanel != null) shopPanel.Hide();
        if (treasurePanel != null) treasurePanel.Hide();
        if (levelUpPanel != null) levelUpPanel.gameObject.SetActive(false);
        if (endingPanel != null) endingPanel.Hide();
        if (difficultyPanel != null) difficultyPanel.Hide();   // 3-196
        if (altarPanel != null) altarPanel.gameObject.SetActive(false);
        deckView.Hide();
        pauseMenu.Close();
        if (devPanel.gameObject.activeSelf) devPanel.Toggle();
        Discovery.Scan(run);              // 3-180: 런을 버리기 전에 마지막으로 본 것 기록
        DiscoveryStore.SaveIfDirty();
        compendium.Hide();
        run = null;   // 타이틀로 돌아가면 런은 끝 (메뉴·개발 도구가 옛 런을 건드리지 않게)
        titlePanel.DefaultClass = playerClass;
        titlePanel.Show();
    }

    // 싱글플레이: 캐릭터 선택 화면 → [플레이]
    void StartSingle()
    {
        // 상태창을 누르면 소용돌이 → 캐릭터 선택
        ScreenTransition.Play(this, () =>
        {
            titlePanel.Hide();
            characterSelect.Str = str;
            characterSelect.Con = con;
            characterSelect.Agi = agi;
            characterSelect.Show(playerClass);
        });
    }

    // 캐릭터 선택에서 [플레이]
    void PlaySingle(PlayerClass chosen)
    {
        // [플레이] → 소용돌이 → 게임 시작
        OpenDifficulty(new List<PlayerClass> { chosen }, false);   // 3-196: 바로 시작하지 않고 난이도 화면
    }

    // 멀티플레이 1: 방에서 [캐릭터 선택] → 소용돌이 → 캐릭터 선택 화면 (P1 부터 차례로)
    void OpenMultiSelect(int players, string roomCode)
    {
        ScreenTransition.Play(this, () =>
        {
            titlePanel.Hide();
            characterSelect.Str = str;
            characterSelect.Con = con;
            characterSelect.Agi = agi;
            characterSelect.ShowMulti(players, roomCode);
            lastRoomCode = roomCode;   // 3-196: 난이도 화면에서 [뒤로] 할 때 다시 씀
        });
    }

    // 멀티 캐릭터 선택에서 [뒤로] (아무도 안 고른 상태): 방 화면으로
    void BackToRoom()
    {
        ShowTitle();
        titlePanel.ReopenRoom();
    }

    // 멀티플레이 2: 모두 고른 직업들로 시작 (한 PC에서 P1/P2 를 바꿔 가며 조작)
    void StartMulti(List<PlayerClass> classes)
    {
        // 캐릭터 선택에서 [게임 시작] → 소용돌이 → 게임 시작
        OpenDifficulty(classes, true);   // 3-196: 모두 고른 뒤 방장(P1)이 난이도
    }

    // 이어하기 (2026-11-15, 3-73): 저장 파일에서 런을 불러와서 그 칸의 선택지(또는 지도) 화면부터
    //   저장은 메뉴의 [저장] 버튼으로만 (3-141, SaveFromMenu)
    void ContinueRun()
    {
        RunState loaded = SaveManager.LoadRun();
        if (loaded == null)
        {
            // 망가졌거나 옛 형식이면 지우고 타이틀 그대로 (버튼이 사라짐)
            Debug.LogWarning("[저장] 이어할 수 없는 저장이라 지웠습니다");
            titlePanel.Show();
            return;
        }
        ScreenTransition.Play(this, () =>
        {
            titlePanel.Hide();
            characterSelect.Hide();
            run = loaded;
            // 패배 뒤 [새 런] 을 누르면 같은 직업들로 다시 시작하도록 직업 목록도 맞춰 둠
            runClasses = new List<PlayerClass>();
            foreach (PlayerCombatant p in run.Players) runClasses.Add(p.Class);
            startedFromTitle = true;
            controlIndex = 0;
            battle = null;
            Debug.Log("[저장] 이어하기: " + run.Stage + "스테이지 " + run.ProgressText);
            // 레벨 업 포인트를 안 쓰고 저장했으면 레벨 업 화면부터 → 끝나면 런 화면
            levelUpPanel.Open(run.Players, () => { ShowRunScreen(true); relicBar.Show(InfoPlayer()); });
            if (levelUpPanel.IsOpen) Tutorial("levelup");   // 3-130
        });
    }

    // 새 런: 플레이어를 새로 만들고 지도부터 시작
    //   runClasses 가 없으면 (예전처럼) Inspector 의 playerCount / playerClass / otherClass 로
    void NewRun()
    {
        List<PlayerCombatant> players = new List<PlayerCombatant>();
        int count = runClasses != null ? runClasses.Count : playerCount;
        for (int i = 0; i < count; i++)
        {
            PlayerClass cls = runClasses != null ? runClasses[i] : ((i == 0) ? playerClass : otherClass);
            // 타이틀에서 시작한 게임 = 시작 덱 11장에서 보상으로 늘려 감. 테스트 카드는 Inspector 로 바로 켠 테스트에서만
            bool testCards = addTestCards && !startedFromTitle;
            players.Add(ClassLibrary.CreatePlayer(cls, "P" + (i + 1), str, con, agi, testCards));
        }
        int useSeed = (seed == 0) ? System.Environment.TickCount : seed;
        run = new RunState(players, useSeed);
        run.Difficulty = runDifficulty;   // 3-196 (BeginStage 보다 먼저 → 지도 가중치에도 반영)
        int startStage = Mathf.Clamp(stage, 1, GameBalance.MaxStage);
        if (startStage != 1) run.BeginStage(startStage);   // 테스트: 원하는 스테이지부터
        controlIndex = 0;
        battle = null;
        string races = "";
        foreach (Race r in run.StageRaces) races += EnemyLibrary.RaceName(r) + " ";
        Debug.Log("[런] seed = " + useSeed + " / 종족 순서: " + races);   // 같은 seed 를 Inspector 에 넣으면 같은 판
        ShowRunScreen(true);
    }

    // 런 화면 보이기: 수성이면 ??? 카드 화면, 4스테이지면 지도 (전투 화면은 뒤에 가려짐)
    void ShowRunScreen(bool showBanner)
    {
        StopAllCoroutines();
        ShowTip(null, null);
        ClosePotionTargets();
        vote = null;                 // 화면이 바뀌면 투표도 처음부터
        voteRolling = false;
        HideVoteLabel();
        // 받을 보상이 있으면 보상 화면을 먼저 (스테이지 배너는 보상을 닫은 뒤에)
        bool hasRewards = run.Rewards.Count > 0;
        deckView.Hide();
        if (hasRewards) { bannerAfterRewards = showBanner; showBanner = false; }
        Time.timeScale = 1f;
        dimTarget = 0f;
        busy = false;
        waveSwapping = false;
        resultPanel.SetActive(false);
        foreach (CardView v in handViews) Destroy(v.gameObject);
        handViews.Clear();
        viewOf.Clear();
        hovered = null;
        dragging = null;
        shownPlayer = null;
        HideArrow();
        placePanel.Hide();
        forgePanel.Hide();
        shopPanel.Hide();
        treasurePanel.Hide();
        if (run.Place == RunPlace.Treasure)
        {
            // 보물 상자: 상자를 눌러야 열림 → 보상 화면 (2026-11-15)
            mapPanel.Hide();
            choicePanel.Hide();
            topText.text = (run.IsFinalChapter ? "최종장" : run.Stage + "스테이지") + "   보물 상자 " + (run.TreasureChestIndex + 1) + "/" + GameBalance.TreasureChests + DiffTag();   // 3-174
            // 상자를 누르면 → 물건이 보이고 한 사람이 1개씩 고름 (TreasurePickPanel)
            if (!run.TreasureOpened) placePanel.ShowChest(run, () => { run.OpenTreasure(); ShowRunScreen(false); });
            else treasurePanel.Show(run);
        }
        else if (run.Place == RunPlace.Shop)
        {
            mapPanel.Hide();
            choicePanel.Hide();
            topText.text = (run.IsFinalChapter ? "최종장" : run.Stage + "스테이지") + "   상점" + DiffTag();
            shopPanel.Show(run, controlIndex);
        }
        else if (run.Place == RunPlace.Camp)
        {
            mapPanel.Hide();
            choicePanel.Hide();
            ShowCamp();
        }
        else if (run.Place == RunPlace.Forge)
        {
            mapPanel.Hide();
            choicePanel.Hide();
            ShowForge();
        }
        else if (run.Place == RunPlace.BloodAltar)
        {
            // 3-129 피의 제단: 제물의 제단 화면을 피의 제단 모드로 엶. 다 끝나면 다음 칸으로
            mapPanel.Hide();
            choicePanel.Hide();
            topText.text = run.Stage + "스테이지   " + RunState.BloodAltarName + DiffTag();
            if (!altarPanel.IsOpen)
            {
                altarPanel.Open(run, run.Players, () =>
                {
                    run.LeaveBloodAltar();
                    ShowRunScreen(false);
                    // 결과 알림 (아무도 못 썼으면 그 이유)
                    string msg = altarPanel.Results.Count > 0 ? string.Join("\n", altarPanel.Results.ToArray())
                               : (altarPanel.NobodyEligible
                                    ? RunState.BloodAltarName + ": 덱이 " + GameBalance.BloodAltarMinDeck + "장 이상이어야 바칠 수 있습니다"
                                    : RunState.BloodAltarName + ": 아무것도 바치지 않고 떠났다");
                    ShowRunMessage(msg);
                }, true);
            }
        }
        else if (run.IsSiege)
        {
            mapPanel.Hide();
            choicePanel.Show(run, showBanner);
            topText.text = run.Stage + "스테이지   수성   " + run.ProgressText + DiffTag();
        }
        else
        {
            choicePanel.Hide();
            mapPanel.Show(run, showBanner);
            topText.text = run.IsFinalChapter ? "최종장" : run.Stage + "스테이지   반격" + DiffTag();
            if (run.JustGotMap)
            {
                // 2스테이지를 지켜 냈음 → 이제 반격 (3스테이지: 지도를 따라 적 요새로)
                mapPanel.ShowBanner("반격의 시간이다", "성문을 열어라. 이번엔 우리가 놈들의 요새로 간다!", 3.6f);
                mapPanel.ShowMessage("수문장이 떨어뜨린 지도로 적의 요새까지 가는 길이 드러났다");
                run.JustGotMap = false;
            }
        }
        relicBar.Show(InfoPlayer());
        if (playerTabs == null || playerTabs.childCount != (run.Players.Count > 1 ? run.Players.Count : 0)) BuildPlayerTabs();
        else RefreshPlayerTabs();
        if (hasRewards) rewardPanel.Show(run);
        else rewardPanel.Hide();
        topBar.SetAsLastSibling();   // 위쪽 버튼 줄은 맨 위에
        RunScreenTutorials(hasRewards);   // 3-130: 다른 창을 다 띄운 뒤에 (안내가 맨 위로)

        // 3-141: 자동 저장 없앰 → 메뉴의 [저장] 버튼으로만 저장 (SaveFromMenu)
    }

    // 3-141: 일시정지 메뉴 [저장] 버튼. 돌려주는 글자가 버튼에 잠깐 보임
    //   저장되는 때 = 칸 고르기(수성)·지도 화면 (전투·보상·장소 안에서는 run.CanSave 가 false)
    string SaveFromMenu()
    {
        if (run == null) return "저장할 게임이 없습니다";
        if (!startedFromTitle) return "테스트 런은 저장하지 않습니다";   // Inspector 로 바로 켠 런 (진짜 저장을 덮어쓰지 않게)
        if (!run.CanSave) return "칸 고르기·지도 화면에서만 저장할 수 있습니다";
        return SaveManager.TrySave(run) ? "저장했습니다 (" + System.DateTime.Now.ToString("HH:mm") + ")" : "저장 실패";
    }

    // ---------------- 키보드: Esc = 메뉴, F1 = 개발 도구 ----------------
    void HandleKeys()
    {
        bool esc, f1;
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Keyboard kb = UnityEngine.InputSystem.Keyboard.current;
        esc = kb != null && kb.escapeKey.wasPressedThisFrame;
        f1 = kb != null && kb.f1Key.wasPressedThisFrame;
#else
        esc = Input.GetKeyDown(KeyCode.Escape);
        f1 = Input.GetKeyDown(KeyCode.F1);
#endif
        if (f1 && DevPanel.Enabled && run != null) devPanel.Toggle();
        if (!esc) return;
        // 3-180: 도감이 열려 있으면 Esc 는 도감 안에서만 (안쪽 화면 → 처음 화면 → 닫기). 타이틀 위에서 열었어도 여기서 처리
        if (compendium.IsOpen) { compendium.Back(); return; }
        if (difficultyPanel.IsOpen) return;   // 3-196: 난이도 화면에서는 Esc 무시 ([뒤로] 버튼으로)
        // 타이틀·캐릭터 선택 화면은 자기 Esc 처리를 씀
        if (titlePanel.gameObject.activeSelf || characterSelect.gameObject.activeSelf || run == null) return;
        if (pauseMenu.IsOpen) pauseMenu.Back();
        else if (deckView.IsOpen) deckView.Hide();
        else if (devPanel.gameObject.activeSelf) devPanel.Toggle();
        else pauseMenu.Open();
    }

    // 지금 전투 화면이 보이는 중인지 (지도·수성·장소 화면이 위에 없으면)
    bool mapPanelHidden()
    {
        return !mapPanel.gameObject.activeSelf && !choicePanel.gameObject.activeSelf && !placePanel.gameObject.activeSelf && !forgePanel.gameObject.activeSelf;
    }

    // 개발 도구로 뭔가 바꾼 뒤 화면 다시 맞추기
    void DevRefresh()
    {
        if (run == null) return;
        if (battle != null && mapPanelHidden() && !rewardPanel.gameObject.activeSelf) Refresh();
        else ShowRunScreen(false);
        relicBar.Show(InfoPlayer());
    }

    // 위쪽 정보 판에 보여 줄 플레이어 (2026-11-15: 지금 화면의 플레이어 한 명만)
    PlayerCombatant InfoPlayer()
    {
        if (battle != null && battle.Result == BattleResult.Ongoing && Me != null) return Me;
        if (run == null || run.Players.Count == 0) return null;
        return run.Players[Mathf.Clamp(controlIndex, 0, run.Players.Count - 1)];
    }

    // 포션 칸을 누름 (전투 중 내 차례)
    void UsePotion(int index)
    {
        PlayerCombatant p = Me;
        if (busy || p == null || battle == null || battle.Result != BattleResult.Ongoing) return;
        if (index < 0 || index >= p.Potions.Count) return;
        // 회복·강화 포션 + 살아 있는 동료가 있으면 → 누구에게 쓸지 고르기 (2026-11-15, 슬더스2 멀티처럼 동료에게도)
        int alive = 0;
        foreach (PlayerCombatant q in battle.Players) if (!q.IsDead) alive++;
        if (BattleSystem.PotionTargetsAlly(p.Potions[index]) && alive > 1) { ShowPotionTargets(p, index); return; }
        // 3-157 불사조 깃털: 쓰러진 동료를 고름 (없으면 안 쓰고 안내만)
        if (BattleSystem.PotionTargetsDead(p.Potions[index]))
        {
            List<PlayerCombatant> dead = new List<PlayerCombatant>();
            foreach (PlayerCombatant q in battle.Players) if (q.IsDead) dead.Add(q);
            if (dead.Count == 0) { Toast("쓰러진 동료가 없습니다 (불사조 깃털은 쓰러진 동료에게만)"); return; }
            if (dead.Count == 1) { UsePotionWithVfx(p, index, dead[0]); return; }
            ShowPotionTargets(p, index);
            return;
        }
        UsePotionWithVfx(p, index, null);
    }

    // 포션 사용 + 대상에게 효과 보여 주기 (3-89)
    //   쓰기 전 HP 를 적어 두고 → 포션 사용 → 바뀐 만큼 숫자와 효과를 대상 위에 띄움
    //   자바로 치면: int before = target.hp; usePotion(); showEffect(before - target.hp);
    void UsePotionWithVfx(PlayerCombatant user, int index, PlayerCombatant target)
    {
        string id = user.Potions[index];
        PlayerCombatant who = target ?? user;
        int whoHpBefore = who.Hp;
        Dictionary<EnemyCombatant, int> enemyHp = new Dictionary<EnemyCombatant, int>();
        foreach (EnemyCombatant en in battle.Enemies) if (!en.IsDead) enemyHp[en] = en.Hp;

        bool used = target == null ? battle.UsePotion(user, index) : battle.UsePotion(user, index, target);
        if (!used) return;
        Refresh();
        PotionVfx(id, who, whoHpBefore, enemyHp);
    }

    void PotionVfx(string id, PlayerCombatant who, int whoHpBefore, Dictionary<EnemyCombatant, int> enemyHp)
    {
        UnitView wv = ViewOf(who);
        switch (id)
        {
            case PotionLibrary.Heal:
                // 회복: 대상 몸에서 초록 빛이 피어오르고 "+회복량"
                if (wv != null)
                {
                    Color green = UIFactory.Hex("#7CFF9A");
                    Vfx.Rise(popupLayer, UnitPos(wv, 0.45f), green, 18);
                    Vfx.Ring(popupLayer, UnitPos(wv, 0.45f), new Color(green.r, green.g, green.b, 0.7f));
                    int healed = who.Hp - whoHpBefore;
                    Popup(UnitPos(wv, 0.85f), "+" + healed, green, 1.5f, 44);
                }
                break;
            case PotionLibrary.Power:
                // 강화: 대상 몸에서 금빛이 피어오르고 "공격 +N"
                if (wv != null)
                {
                    Color gold = UIFactory.Hex("#FFC840");
                    Vfx.Rise(popupLayer, UnitPos(wv, 0.45f), gold, 16);
                    Vfx.Ring(popupLayer, UnitPos(wv, 0.45f), new Color(gold.r, gold.g, gold.b, 0.7f));
                    Vfx.Sparks(popupLayer, UnitPos(wv, 0.6f), gold, 10);
                    Popup(UnitPos(wv, 0.85f), "공격 +" + GameBalance.PotionPowerBonus, gold, 1.4f, 38);
                }
                break;
            case PotionLibrary.Fire:
                // 화염: 맞은 적마다 불꽃이 터지고 깎인 HP 만큼 숫자
                foreach (KeyValuePair<EnemyCombatant, int> pair in enemyHp)
                {
                    UnitView ev = ViewOf(pair.Key);
                    if (ev == null) continue;
                    Vector2 pos = UnitPos(ev, 0.5f);
                    Vfx.Shards(popupLayer, pos, UIFactory.Hex("#FF7A2A"), 14, 320f, 60f);
                    Vfx.Sparks(popupLayer, pos, UIFactory.Hex("#FFD060"), 10);
                    int lost = pair.Value - pair.Key.Hp;
                    if (lost > 0) Popup(UnitPos(ev, 0.85f), "-" + lost, UIFactory.Hex("#FF8A3A"), 1.6f, 44);
                }
                break;
            // ---- 3-157 새 포션 ----
            case PotionLibrary.Block:
                if (wv != null)
                {
                    Color blue = UIFactory.Hex("#8AC8FF");
                    Vfx.Ring(popupLayer, UnitPos(wv, 0.45f), blue);
                    Vfx.Rise(popupLayer, UnitPos(wv, 0.45f), blue, 12);
                    Popup(UnitPos(wv, 0.85f), "방어도 +" + GameBalance.PotionBlock, blue, 1.4f, 38);
                }
                break;
            case PotionLibrary.Energy:
                if (wv != null)
                {
                    Color cyan = UIFactory.Hex("#5AB8FF");
                    Vfx.Sparks(popupLayer, UnitPos(wv, 0.6f), cyan, 14);
                    Popup(UnitPos(wv, 0.85f), "에너지 +" + GameBalance.PotionEnergy, cyan, 1.4f, 38);
                }
                break;
            case PotionLibrary.Swift:
                if (wv != null)
                {
                    Color cream = UIFactory.Hex("#FFF0C0");
                    Vfx.Rise(popupLayer, UnitPos(wv, 0.5f), cream, 10);
                    Popup(UnitPos(wv, 0.85f), "카드 +" + GameBalance.PotionDraw, cream, 1.4f, 38);
                }
                break;
            case PotionLibrary.Phoenix:
                if (wv != null)
                {
                    Color fire = UIFactory.Hex("#FF8A3A");
                    Vfx.Rise(popupLayer, UnitPos(wv, 0.4f), fire, 22);
                    Vfx.Ring(popupLayer, UnitPos(wv, 0.45f), fire);
                    Vfx.Sparks(popupLayer, UnitPos(wv, 0.6f), UIFactory.Hex("#FFD060"), 14);
                    Popup(UnitPos(wv, 0.85f), "부활!", fire, 1.6f, 46);
                }
                break;
            case PotionLibrary.Weak:
                foreach (KeyValuePair<EnemyCombatant, int> pair in enemyHp)
                {
                    UnitView ev = ViewOf(pair.Key);
                    if (ev == null || pair.Key.IsDead) continue;
                    Color gray = UIFactory.Hex("#B0B0C0");
                    Vfx.Ring(popupLayer, UnitPos(ev, 0.5f), gray);
                    Popup(UnitPos(ev, 0.85f), "약화", gray, 1.3f, 36);
                }
                break;
            case PotionLibrary.Vulnerable:
                // 취약: 적마다 보라 고리 + 조각, "취약"
                foreach (KeyValuePair<EnemyCombatant, int> pair in enemyHp)
                {
                    UnitView ev = ViewOf(pair.Key);
                    if (ev == null || pair.Key.IsDead) continue;
                    Color purple = UIFactory.Hex("#B070FF");
                    Vfx.Ring(popupLayer, UnitPos(ev, 0.5f), purple);
                    Vfx.Shards(popupLayer, UnitPos(ev, 0.5f), purple, 10, 220f, 50f);
                    Popup(UnitPos(ev, 0.85f), "취약", purple, 1.3f, 36);
                }
                break;
        }
    }

    // 포션 대상 고르기 창: 살아 있는 아군 버튼 + 취소
    private RectTransform potionPick;
    void ShowPotionTargets(PlayerCombatant user, int index)
    {
        ClosePotionTargets();
        PotionData potion = PotionLibrary.Get(user.Potions[index]);
        potionPick = UIFactory.MakeRect("PotionTarget", root);
        UIFactory.Stretch(potionPick);
        Image dim = UIFactory.MakeImage("Dim", potionPick, new Color(0, 0, 0, 0.55f), false);
        UIFactory.Stretch(dim.rectTransform);
        dim.raycastTarget = true;   // 뒤를 못 누르게
        Text t = UIFactory.MakeText("Title", potionPick, (potion != null ? potion.Name : "포션") + " — 누구에게 쓸까요?", 40, UIFactory.Hex("#FFD27A"), TextAnchor.MiddleCenter);
        UIFactory.Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 170), new Vector2(1200, 60));
        t.fontStyle = FontStyle.Bold;
        List<PlayerCombatant> list = new List<PlayerCombatant>();
        bool reviving = potion != null && BattleSystem.PotionTargetsDead(potion.Id);   // 3-157 불사조 깃털 = 쓰러진 동료만
        foreach (PlayerCombatant q in battle.Players) if (q.IsDead == reviving) list.Add(q);
        for (int i = 0; i < list.Count; i++)
        {
            PlayerCombatant target = list[i];
            string label = target.Name + (target == user ? " (나)" : "") + "\nHP " + target.Hp + " / " + target.MaxHp;
            Button b = UIFactory.MakeButton("T" + i, potionPick, label, UIFactory.Hex("#3A4A6A"), () =>
            {
                ClosePotionTargets();
                UsePotionWithVfx(user, index, target);   // 3-89: 대상에게 효과
            }, 24);
            UIFactory.Place((RectTransform)b.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - (list.Count - 1) / 2f) * 280, 40), new Vector2(260, 110));
        }
        Button cancel = UIFactory.MakeButton("Cancel", potionPick, "취소", UIFactory.Hex("#4A3A3A"), ClosePotionTargets, 26);
        UIFactory.Place((RectTransform)cancel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -110), new Vector2(200, 64));
        UIFactory.SkinButton(cancel, UIFactory.GothicButton);   // 3-181: 8번 공용 버튼 틀 (그림 없던 버튼)
        potionPick.SetAsLastSibling();
    }

    void ClosePotionTargets()
    {
        if (potionPick != null) Destroy(potionPick.gameObject);
        potionPick = null;
    }

    // ---------------- 카드 목록 보기 ----------------
    // 위쪽 [덱] 버튼: 열려 있으면 닫기. 전투 중이면 지금 조작하는 사람부터
    void OpenDeckView()
    {
        if (deckView.IsOpen) { deckView.Hide(); return; }
        if (run == null) return;
        int first = (battle != null && battle.Result == BattleResult.Ongoing) ? controlIndex : 0;
        deckView.ShowDecks(run.Players, first);
        topBar.SetAsLastSibling();
    }

    // 전투 중 더미 누르기: 0 = 뽑기 더미 (섞어서), 1 = 버림 더미
    void OpenPile(int which)
    {
        PlayerCombatant p = Me;
        if (p == null || battle == null || dragging != null) return;
        if (which == 0) deckView.ShowPile(p.Name + " 뽑을 카드 더미 (순서는 비밀)", p.DrawPile, p.ManaName, true);
        else deckView.ShowPile(p.Name + " 버린 카드 더미", p.DiscardPile, p.ManaName, false);
        topBar.SetAsLastSibling();
    }

    // 보상 화면에서 [계속]
    void CloseRewards()
    {
        bool elite = run.LastRewardKind == RewardKind.Hard;   // 3-114: 엘리트를 잡았으면 제단 (보상을 지우기 전에 기억)
        run.ClearRewards();
        // 레벨이 오른 사람이 있으면 레벨 업 화면 (스탯 고르기) → (엘리트면 제물의 제단) → 끝나면 런 화면
        bool banner = bannerAfterRewards;
        bannerAfterRewards = false;
        System.Action toRun = () => { ShowRunScreen(banner); relicBar.Show(InfoPlayer()); };
        levelUpPanel.Open(run.Players, () =>
        {
            if (elite)
            {
                altarPanel.Open(run, run.Players, toRun);
                if (altarPanel.IsOpen) Tutorial("elite_altar");   // 3-130
            }
            else toRun();
        });
        if (levelUpPanel.IsOpen) Tutorial("levelup");   // 3-130
    }

    // 지금 보이는 런 화면 위쪽에 잠깐 안내 문장
    void ShowRunMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        if (run.IsSiege) choicePanel.ShowMessage(message);
        else mapPanel.ShowMessage(message);
    }

    // ---------------- 초소 / 야영지 ----------------
    void ShowCamp()
    {
        topText.text = run.Stage + "스테이지   " + run.CampName + DiffTag();
        List<PlaceOption> options = new List<PlaceOption>();
        int healPct = (int)(GameBalance.RestHealRatio * 100);
        options.Add(new PlaceOption("치료", "모두 최대 HP " + healPct + "% 회복", true, () => DoCamp(CampAction.Heal)) { Art = "UI/Camp/Camp_Art_Heal" });   // 3-148: 카드 그림
        options.Add(new PlaceOption("장비 보강", "다음 전투 시작 시 모두 방어도 +" + GameBalance.FortifyBlock, true, () => DoCamp(CampAction.Fortify)) { Art = "UI/Camp/Camp_Art_Fortify" });
        // 멀티 (2026-11-15, 슬더스2 '치유'): 모두 30% 대신 동료 1명만 60% → 누구를 고칠지 다음 화면에서 고름
        if (run.Players.Count > 1)
            options.Add(new PlaceOption("집중 치료", "동료 1명만 최대 HP " + (int)(GameBalance.MendHealRatio * 100) + "% 회복 (다른 사람은 회복 없음)", true, ShowMendTargets) { Art = "UI/Camp/Camp_Art_Mend" });

        string speaker;
        string body;
        if (run.NextIsBoss)
        {
            // 보스 앞: 정찰병이 보스 힌트를 공짜로 알려 줌
            speaker = "정찰병: \"" + run.BossHint + "\"";
            body = "우두머리와 싸우기 전에 마지막으로 준비할 시간이다. 하나를 고르자.";
        }
        else
        {
            bool canScout = run.CanScout;
            if (run.IsSiege) options.Add(new PlaceOption("정찰", canScout ? "다음 칸 ??? 의 정체를 미리 보기" : "다음 칸은 이미 정해져 있어서 정찰할 필요가 없다",
                                        canScout, () => DoCamp(CampAction.Scout)) { Art = "UI/Camp/Camp_Art_Scout" });   // 3-149: 정찰 = 망원경 그림
            speaker = run.IsSiege ? "경비병: \"교대 시간입니다. 잠시 쉬었다 가시죠.\"" : "동료: \"불을 피웠어. 여기서 숨 좀 돌리자.\"";
            body = "하나를 고르자.";
        }
        placePanel.Show(run, MapNodeType.Rest, run.CampName, speaker, body, options, CampScene());   // 야영지 그림 + 모닥불을 눌러서 선택
    }

    // 3-155: 야영지 배경. 4스테이지(최종 성) 보스 앞 야영지 = 보라 수정 원형 홀 + 가운데 화로 (Backgrounds/CampFinal)
    //   나머지 초소·야영지 = 예전 야영지 그림. 새 그림이 없으면 예전 그림
    string CampScene()
    {
        if (run.IsFinalChapter && run.NextIsBoss && Resources.Load<Sprite>("Backgrounds/CampFinal") != null) return "Backgrounds/CampFinal";
        return "Backgrounds/Camp";
    }

    // 집중 치료: 누구를 고칠지 고르는 두 번째 상자
    void ShowMendTargets()
    {
        List<PlaceOption> options = new List<PlaceOption>();
        for (int i = 0; i < run.Players.Count; i++)
        {
            int index = i;
            PlayerCombatant p = run.Players[i];
            int gain = Mathf.Min(p.MaxHp - p.Hp, DamageCalculator.RoundHalfUp(p.MaxHp * GameBalance.MendHealRatio));
            bool can = !p.HasRelic(RelicLibrary.AlchemistMug);
            options.Add(new PlaceOption(p.Name + " " + ClassLibrary.ColoredClassName(p.Class), can ? "HP " + p.Hp + " / " + p.MaxHp + "  →  +" + gain : "치료 불가 (연금술 머그)", can, () => DoCamp(CampAction.Mend, index)) { Art = "UI/Party/Party_Emblem_" + p.Class });   // 3-148: 직업 문양
        }
        options.Add(new PlaceOption("뒤로", "다른 행동 고르기", true, () => { ShowCamp(); placePanel.OpenBoxNow(); }));
        placePanel.Show(run, MapNodeType.Rest, run.CampName, "누구를 집중 치료할까?", "HP 가 가장 낮은 동료를 고르는 게 보통 좋아요.", options, CampScene());
        placePanel.OpenBoxNow();
    }

    void DoCamp(CampAction action, int target = -1)
    {
        string message = run.DoCamp(action, target);
        ShowRunScreen(false);
        ShowRunMessage(message);
    }

    // ---------------- 대장간 (무기 강화) ----------------
    // 대장간: 대장간 그림 + 드워프 장인 + 모루 (ForgePanel). 강화 규칙은 RunState 가 정함
    void ShowForge()
    {
        topText.text = run.Stage + "스테이지   대장간" + DiffTag();
        forgePanel.Show(run);
    }

    // ForgePanel 에서 무기 강화를 골랐을 때 → 결과 문장을 돌려줌 (드워프가 말해 줌)
    string DoForge(PlayerCombatant player, WeaponBranch branch)
    {
        string message;
        run.UpgradeWeapon(player, branch, out message);
        return message;
    }

    // ForgePanel 에서 카드 강화를 골랐을 때
    string DoCardForge(PlayerCombatant player, int deckIndex)
    {
        string message;
        run.UpgradeCard(player, deckIndex, out message);
        return message;
    }

    void LeaveForge()
    {
        run.LeaveForge();
        ShowRunScreen(false);
    }

    // 수성 화면에서 카드를 눌렀을 때
    void OnSiegeChoiceClicked(SiegeChoice choice)
    {
        if (run.Choices.Count <= 1) { GoSiegeChoice(choice); return; }   // 고를 게 하나뿐이면 투표 없이
        CastVote(choice, choicePanel.RectOf(choice), o => GoSiegeChoice((SiegeChoice)o));
    }

    void GoSiegeChoice(SiegeChoice choice)
    {
        string message;
        bool isBattle = run.ChooseSiege(choice, out message);
        if (isBattle)
        {
            choicePanel.Hide();
            StartBattleView(run.CreateBattle());
            Toast(choice.RealName + "!   " + run.LastPlanText);
        }
        else
        {
            ShowRunScreen(false);                 // 다음 칸 선택지로 (초소·대장간이면 그 장소 화면)
            ShowRunMessage(message);
        }
    }

    // 지도에서 칸을 눌렀을 때
    void OnMapNodeClicked(MapNode node)
    {
        if (run.Map != null && run.Map.Available().Count <= 1) { GoMapNode(node); return; }   // 갈 곳이 하나뿐이면 투표 없이
        CastVote(node, mapPanel.RectOf(node), o => GoMapNode((MapNode)o));
    }

    // ---------------- 멀티 투표 (2026-11-15, 슬더스2 멀티 방식) ----------------
    //   한 PC 에서 P1 → P2 → ... 차례로 칸(카드)을 누름. 누른 칸에 P 표시가 붙음
    //   모두 누르면: 다 같으면 바로 출발, 갈리면 룰렛 (표 수만큼 확률) → 잠깐 보여 주고 출발
    //   혼자면 투표 없이 바로 출발
    private PartyVote vote;
    private Text voteLabel;
    private bool voteRolling;                       // 룰렛 연출 중 (그동안 클릭 무시)
    private readonly System.Random voteRng = new System.Random();

    void CastVote(object option, RectTransform target, System.Action<object> go)
    {
        if (run.Players.Count <= 1) { go(option); return; }
        if (voteRolling) return;
        if (vote == null) vote = new PartyVote(run.Players.Count);
        int voter = vote.NextVoter;
        if (voter < 0) return;
        vote.Cast(voter, option);
        if (target != null) AddVoteChip(target, voter, vote.CountFor(option) - 1);

        if (!vote.AllVoted)
        {
            ShowVoteLabel();
            return;
        }
        bool split;
        object pick = vote.Resolve(voteRng, out split);
        vote = null;
        if (!split) { HideVoteLabel(); go(pick); return; }
        StartCoroutine(VoteRoulette(pick, go));
    }

    // 표가 갈렸을 때: "룰렛!" 을 잠깐 보여 주고 뽑힌 곳으로
    IEnumerator VoteRoulette(object pick, System.Action<object> go)
    {
        voteRolling = true;
        string name = pick is MapNode ? RunMap.TypeName(((MapNode)pick).Type) : (((SiegeChoice)pick).Hidden ? "???" : ((SiegeChoice)pick).RealName);
        ShowVoteLabel("표가 갈렸습니다! 룰렛을 돌립니다 . . .");
        yield return new WaitForSecondsRealtime(1.0f);
        ShowVoteLabel("룰렛 결과 → " + name);
        yield return new WaitForSecondsRealtime(1.0f);
        voteRolling = false;
        HideVoteLabel();
        go(pick);
    }

    // 누른 칸(카드) 오른쪽 위에 "P1" 같은 작은 표 (같은 칸에 여러 표면 옆으로 나란히)
    //   3-149: 직업 투표 아이콘 (UI/Party/Vote_직업: 검은 낫 보라 · 피닉스 빨강 · 마도사 파랑 · 스나이퍼 노랑) 이 있으면
    //   카드 위쪽 왼쪽부터 아이콘 + 아래 "P1" + 직업색 ▼ (목업처럼 카드를 가리킴). 아이콘이 없으면 예전 색 칩
    void AddVoteChip(RectTransform target, int voter, int slot)
    {
        PlayerClass cls = run.Players[voter].Class;
        Sprite icon = Resources.Load<Sprite>("UI/Party/Vote_" + cls);
        if (icon != null)
        {
            float h = target.rect.height;
            float size = h > 200f ? 62f : 42f;   // 칸 고르기 카드 = 크게 / 지도 칸(작은 동그라미) = 작게
            RectTransform holder = UIFactory.MakeRect("Vote" + voter, target);
            UIFactory.Place(holder, new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(8 + slot * (size + 6), 4), new Vector2(size, size + size * 0.32f));
            Image img = UIFactory.MakeImage("Icon", holder, Color.white, false);
            img.sprite = icon;
            img.preserveAspect = true;
            img.raycastTarget = false;
            UIFactory.Place(img.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(size, size));
            Text t = UIFactory.MakeText("T", img.transform, "P" + (voter + 1), Mathf.RoundToInt(size * 0.36f), Color.white, TextAnchor.MiddleCenter);
            UIFactory.Stretch(t.rectTransform);
            t.font = UIFactory.TitleFont;
            t.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.95f);
            // 아래 ▼ (삼각형 그림은 위를 보니까 180도 돌림)
            Image tri = UIFactory.MakeImage("Arrow", holder, VoteColor(cls), false);
            tri.sprite = UIFactory.TriangleSprite;
            tri.raycastTarget = false;
            UIFactory.Place(tri.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0, size * 0.14f), new Vector2(size * 0.3f, size * 0.22f));
            tri.rectTransform.localEulerAngles = new Vector3(0, 0, 180f);
            return;
        }
        Image chip = UIFactory.MakeImage("Vote" + voter, target, TreasurePickPanel.PlayerColor(voter), false);
        UIFactory.Place(chip.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(10 - slot * 40, 10), new Vector2(38, 30));
        chip.raycastTarget = false;
        chip.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.9f);
        Text tt = UIFactory.MakeText("T", chip.transform, "P" + (voter + 1), 18, Color.black, TextAnchor.MiddleCenter);
        UIFactory.Stretch(tt.rectTransform);
        tt.fontStyle = FontStyle.Bold;
        tt.raycastTarget = false;
    }

    // 3-149: 투표 ▼ 색 (직업 아이콘 색과 같게)
    static Color VoteColor(PlayerClass c)
    {
        switch (c)
        {
            case PlayerClass.BlackScythe:   return UIFactory.Hex("#A050F0");   // 보라
            case PlayerClass.CosmicPhoenix: return UIFactory.Hex("#F05A2A");   // 빨강
            case PlayerClass.FrostMage:     return UIFactory.Hex("#4AA8F0");   // 파랑
            default:                        return UIFactory.Hex("#E8B830");   // 노랑 (스나이퍼)
        }
    }

    // 화면 위쪽 가운데: "P2 코스믹 피닉스 투표 차례 (1/3 투표함)"
    void ShowVoteLabel(string text = null)
    {
        if (voteLabel == null)
        {
            voteLabel = UIFactory.MakeText("VoteLabel", root, "", 32, Color.white, TextAnchor.MiddleCenter);
            UIFactory.Place(voteLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -84), new Vector2(1200, 50));
            voteLabel.fontStyle = FontStyle.Bold;
            voteLabel.supportRichText = true;
            voteLabel.raycastTarget = false;
            voteLabel.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.95f);
        }
        if (text == null && vote != null)
        {
            int next = vote.NextVoter;
            int done = 0;
            foreach (object v in vote.Votes) if (v != null) done++;
            PlayerCombatant p = run.Players[next];
            // 3-169: P 번호는 색 없음, 캐릭터 이름만 고유 색
            text = p.Name + " " + ClassLibrary.ColoredClassName(p.Class) + " 투표 차례  (" + done + " / " + run.Players.Count + ")";
        }
        voteLabel.text = text;
        voteLabel.gameObject.SetActive(true);
        voteLabel.transform.SetAsLastSibling();
    }

    void HideVoteLabel()
    {
        if (voteLabel != null) voteLabel.gameObject.SetActive(false);
    }

    void GoMapNode(MapNode node)
    {
        string message;
        bool isBattle = run.EnterNode(node, out message);
        if (isBattle)
        {
            mapPanel.Hide();
            StartBattleView(run.CreateBattle());
            Toast(run.PendingLabel + "   " + run.LastPlanText);
        }
        else
        {
            ShowRunScreen(false);                 // 지도 다시 그리기 (야영지·대장간이면 그 장소 화면)
            ShowRunMessage(message);
        }
    }

    // 결과 화면 버튼: 이겼으면 다음 칸으로(보스였으면 새 스테이지), 런이 끝났으면 새 런
    void OnResultButton()
    {
        if (battle.Result == BattleResult.Defeat)
        {
            NewRun();
            return;
        }
        bool wasBoss = run.PendingIsBoss;
        run.FinishBattle(battle);
        if (run.IsOver && run.Cleared)
        {
            // 3-112: 마지막 보스 → 엔딩 화면. [타이틀로] / [새 런]
            resultPanel.SetActive(false);
            endingPanel.Show(run, ShowTitle, () => { endingPanel.Hide(); NewRun(); });
        }
        else if (run.IsOver) NewRun();
        else ShowRunScreen(wasBoss);
    }

    // (테스트) 위쪽 버튼: 진행 위치는 그대로, 그 전투만 바로 해 봄 (HP는 런 그대로)
    // 웨이브 구성 글자는 GameBalance 의 웨이브 구성표와 같은 규칙 (N=일반, V=정예, E=엘리트, T=감시자 무리, B=수문장, A=설계자)
    static readonly string[] TestFightNames = { "쉬운 3웨이브", "어려운", "감시자 무리", "수문장", "설계자" };
    static readonly string[][] TestFightPlans =
    {
        new[] { "N1", "N2", "V1" },
        new[] { "N2", "E1" },
        new[] { "T1" },
        new[] { "B1" },
        new[] { "A1" },
    };

    void StartTestBattle(int index)
    {
        foreach (PlayerCombatant pl in run.Players)
        {
            if (pl.IsDead) pl.Hp = GameBalance.ReviveHp;
        }
        mapPanel.Hide();
        choicePanel.Hide();
        placePanel.Hide();
        forgePanel.Hide();
        StartBattleView(run.CreateTestBattle(TestFightPlans[index]));
        Toast("테스트: " + EnemyLibrary.DescribePlan(TestFightPlans[index]));
    }

    // 만들어진 전투로 전투 화면을 새로 맞춤
    void StartBattleView(BattleSystem newBattle)
    {
        battle = newBattle;
        ShowTip(null, null);
        rewardPanel.Hide();
        deckView.Hide();
        Time.timeScale = 1f;
        dimTarget = 0f;
        if (controlIndex >= battle.Players.Count) controlIndex = 0;

        // 이전 전투의 화면 정리
        StopAllCoroutines();
        busy = false;
        lastPlayed = null;
        shakeTimer = 0f;
        if (stageRoot != null) stageRoot.anchoredPosition = Vector2.zero;
        foreach (CardView v in handViews) Destroy(v.gameObject);
        handViews.Clear();
        viewOf.Clear();
        hovered = null;
        dragging = null;
        shownPlayer = null;
        resultPanel.SetActive(false);
        waveSwapping = false;
        shownWaveVersion = battle.WaveVersion;
        arrowTarget = null;

        ApplySceneArt();
        BuildUnits();
        BuildPlayerTabs();
        Refresh();
        BattleTutorials();   // 3-130
    }

    // ---------------- 3-130 튜토리얼 안내 ----------------
    // 처음 보는 것만 뜸 (이미 본 안내는 TutorialPanel 이 알아서 건너뜀)
    void Tutorial(string id)
    {
        if (tutorialPanel != null) tutorialPanel.Show(id);
    }

    // 전투 시작: 기본 조작 → 의도 → 직업 자원 → 정예/엘리트/보스
    void BattleTutorials()
    {
        if (battle == null) return;
        Tutorial("battle_basic");
        Tutorial("battle_intent");
        foreach (PlayerCombatant p in battle.Players) Tutorial("res_" + p.Class);   // 예) res_Sniper (enum 이름 그대로)
        bool veteran = false, elite = false, boss = false;
        foreach (EnemyCombatant e in battle.Enemies)
        {
            if (e.Rank == EnemyRank.Veteran) veteran = true;
            if (e.Rank == EnemyRank.Elite) elite = true;
            if (e.Rank == EnemyRank.Boss) boss = true;
        }
        if (veteran) Tutorial("battle_veteran");
        if (elite) Tutorial("battle_elite");
        if (boss) Tutorial("battle_boss");
        if (boss || elite) StartCoroutine(BossIntroFx());   // 3-188 보스 / 3-200 엘리트도
    }

    // 런 화면 (선택지·지도·장소·보상): 지금 보이는 화면에 맞는 안내
    void RunScreenTutorials(bool hasRewards)
    {
        if (run == null) return;
        if (hasRewards) { Tutorial("reward"); return; }   // 보상 화면이 위에 떠 있으면 보상 안내만
        switch (run.Place)
        {
            case RunPlace.Camp: Tutorial("camp"); return;
            case RunPlace.Forge: Tutorial("forge"); return;
            case RunPlace.Shop: Tutorial("shop"); return;
            case RunPlace.Treasure: Tutorial("treasure"); return;
            case RunPlace.BloodAltar: Tutorial("blood_altar"); return;
        }
        if (run.IsSiege)
        {
            Tutorial("siege_choice");
            if (run.Players.Count > 1) Tutorial("siege_vote");
        }
        else Tutorial("map");
    }

    // 스테이지에 맞는 배경 그림: 1스테이지 = 성 안 (성문이 뚫려 몬스터가 들어옴), 2~3스테이지 = 성 밖 (성벽 앞 들판)
    // 최종 보스 설계자와 싸울 때 = 설계자의 방 (붉은 달과 무너진 왕좌의 방) - 2026-11-09 추가
    // 3스테이지 보스(수문장) = 닫힌 요새 문, 최종장 일반 전투 = 열린 성문 안쪽 (2026-11-10)
    void ApplySceneArt()
    {
        string artName = null;
        if (IsArchitectFight()) artName = "ArchitectRoom";
        else if (run != null && run.Stage == 3 && run.PendingIsBoss) artName = "FortressGate";          // 요새 문 앞의 수문장 (2026-11-10)
        else if (run != null && run.Stage == 3)
        {
            // 3스테이지 진군 (2026-11-15): 지도 아래쪽(1~3층) = 달빛 숲, 위쪽(4층~) = 바위 고원 (요새에 가까워짐)
            int floor = run.PendingNode != null ? run.PendingNode.Floor : 0;
            artName = floor < 3 ? "MarchForest" : "MarchHighland";
        }
        else if (run != null && run.IsFinalChapter && run.PendingNode != null && run.PendingNode.Floor == 0) artName = "CastleHall";   // 최종장 첫 전투(정문 안 대전당) - 2026-11-15
        else if (run != null && run.IsFinalChapter && Resources.Load<Sprite>("Backgrounds/FinalHall") != null) artName = "FinalHall";   // 3-155: 최종장 2층~ (보스 앞 야영지 전까지) = 보라 수정 회랑
        else if (run != null && run.IsFinalChapter) artName = "FortressGateOpen";                       // 최종장: 열린 성문 안쪽 (새 그림이 없을 때)
        else if (run != null && run.Stage == 1) artName = "CastleInside";
        else if (run != null && (run.Stage == 2 || run.Stage == 3)) artName = "CastleOutside";
        Sprite sprite = artName != null ? Resources.Load<Sprite>("Backgrounds/" + artName) : null;
        // 배경마다 바닥 높이가 달라서: 그림 확대(scale)·위로 올림(dy)·캐릭터 발 높이(floor) 를 맞춤
        Vector3 layout;
        if (sprite == null || !SceneLayouts.TryGetValue(artName, out layout)) layout = new Vector3(1f, 0f, FloorY);
        sceneHolder.localScale = new Vector3(layout.x, layout.x, 1f);
        sceneHolder.anchoredPosition = new Vector2(0f, layout.y);
        currentFloorY = layout.z;
        sceneArt.sprite = sprite;
        sceneArt.enabled = sprite != null;
        // 3-95: 그림마다 가로세로 비율이 달라서(예: 새 1스테이지 성 안 그림 1024×604) 16:9 고정 대신 그림 비율로 → 찌그러지지 않음
        AspectRatioFitter fitter = sceneArt.GetComponent<AspectRatioFitter>();
        if (fitter != null && sprite != null) fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
        foreach (GameObject go in plainBackground) go.SetActive(sprite == null);
    }

    // 지금 전투에 설계자가 있는지 (적 이름에 "설계자"가 들어 있으면)
    //   자바로 치면 for-each 로 리스트를 돌면서 contains 검사하는 것과 같음
    bool IsArchitectFight()
    {
        if (battle == null || battle.Enemies == null) return false;
        foreach (EnemyCombatant e in battle.Enemies)
            if (e != null && e.Name != null && e.Name.Contains("설계자")) return true;
        return false;
    }

    // 지금 조작하는 플레이어
    PlayerCombatant Me
    {
        get { return (battle != null && controlIndex < battle.Players.Count) ? battle.Players[controlIndex] : null; }
    }

    // 동시 턴 테스트: 한 PC에서 P1·P2를 번갈아 조작하는 버튼 (네트워크가 붙으면 각자 자기 것만)
    // 파티 탭 P1~P4 (2026-11-15): 눌러서 그 사람을 봄 (위쪽 정보 판이 그 사람으로 바뀜)
    //   전투 중에는 그 사람을 조작. 지도·수성·장소 화면에서도 눌러서 동료의 HP·스탯·아티팩트·포션을 볼 수 있음
    //   탭 안: 이름 + 작은 HP 막대 + 상태 (끝 = 이번 턴 끝냄, 쓰러짐, 방N = 방어도) - 슬더스2 멀티 영상의 왼쪽 파티 목록처럼
    // 3-148: 직업별 파티 칸 그림 (UI/Party/Party_Tile_직업). 하나라도 없으면 예전 버튼 모양
    const float TileW = 112f, TileH = 122f, TileGap = 8f;
    static readonly Vector2 TileHpMin = new Vector2(0.09f, 0.075f), TileHpMax = new Vector2(0.905f, 0.178f);   // 칸 그림 속 HP 홈 (비율)
    bool UseTileArt()
    {
        if (run == null) return false;
        foreach (PlayerCombatant p in run.Players)
            if (Resources.Load<Sprite>("UI/Party/Party_Tile_" + p.Class) == null) return false;
        return true;
    }

    void BuildPlayerTabs()
    {
        if (playerTabs != null) Destroy(playerTabs.gameObject);
        playerTabs = UIFactory.MakeRect("PlayerTabs", topBar);   // 위쪽 줄 안 → 지도·수성 화면 위에도 보임
        UIFactory.Place(playerTabs, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20, -70 - RelicBar.Height - 8), new Vector2(520, 56));   // 플레이어 정보 판 바로 아래
        if (run == null || run.Players.Count < 2) return;
        if (UseTileArt()) { BuildPlayerTiles(); RefreshPlayerTabs(); return; }
        for (int i = 0; i < run.Players.Count; i++)
        {
            int index = i;
            Button b = UIFactory.MakeButton("Tab" + i, playerTabs, "", UIFactory.Hex("#2C3140"), () => SelectPlayer(index), 20);
            RectTransform brt = (RectTransform)b.transform;
            UIFactory.Place(brt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(i * 124, 0), new Vector2(118, 54));
            Text label = b.GetComponentInChildren<Text>();
            label.supportRichText = true;
            label.alignment = TextAnchor.UpperCenter;
            label.rectTransform.offsetMax = new Vector2(0, -4);
            // HP 막대 (바탕 + 채움)
            Image hpBack = UIFactory.MakeImage("HpBack", brt, new Color(0, 0, 0, 0.6f), false);
            UIFactory.Place(hpBack.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 6), new Vector2(100, 8));
            hpBack.raycastTarget = false;
            Image hpFill = UIFactory.MakeImage("HpFill", hpBack.transform, UIFactory.Hex("#D84848"), false);
            hpFill.rectTransform.anchorMin = new Vector2(0f, 0f);
            hpFill.rectTransform.anchorMax = new Vector2(1f, 1f);
            hpFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            hpFill.rectTransform.offsetMin = Vector2.zero;
            hpFill.rectTransform.offsetMax = Vector2.zero;
            hpFill.raycastTarget = false;
            // 플레이어 색 테두리 (투표·보물 상자의 P 표시와 같은 색)
            b.gameObject.AddComponent<Outline>().effectColor = TreasurePickPanel.PlayerColor(i);
        }
        RefreshPlayerTabs();
    }

    // 3-148: 파티 칸 (직업색 틀 + 위 직업 문양 + 가운데 P1 + 아래 HP 홈). 칸마다 이름이 "Tab0" 처럼 같아서 RefreshPlayerTabs 가 그대로 찾음
    void BuildPlayerTiles()
    {
        playerTabs.sizeDelta = new Vector2(run.Players.Count * (TileW + TileGap), TileH + 6);
        for (int i = 0; i < run.Players.Count; i++)
        {
            int index = i;
            PlayerCombatant p = run.Players[i];
            RectTransform tile = UIFactory.MakeRect("Tab" + i, playerTabs);
            UIFactory.Place(tile, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(i * (TileW + TileGap), 0), new Vector2(TileW, TileH));
            // 고른 사람 뒤 금빛 (RefreshPlayerTabs 가 켜고 끔)
            Image glow = UIFactory.MakeImage("Glow", tile, new Color(1f, 0.82f, 0.35f, 0.55f), false);
            glow.sprite = UIFactory.SoftGlowSprite;
            UIFactory.Stretch(glow.rectTransform, -26, -26, -26, -26);
            glow.raycastTarget = false;
            Image img = UIFactory.MakeImage("Frame", tile, Color.white, false);
            img.sprite = Resources.Load<Sprite>("UI/Party/Party_Tile_" + p.Class);
            UIFactory.Stretch(img.rectTransform);
            Button b = img.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => SelectPlayer(index));
            // 가운데 글자: 크게 "P1" + 아래 작게 상태 (끝 / 쓰러짐 / 방N)
            Text label = UIFactory.MakeText("Label", tile, "", 30, UIFactory.Hex("#F4E2BC"), TextAnchor.MiddleCenter);
            label.rectTransform.anchorMin = new Vector2(0.15f, 0.3f);
            label.rectTransform.anchorMax = new Vector2(0.85f, 0.72f);
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.font = UIFactory.TitleFont;
            label.supportRichText = true;
            label.resizeTextForBestFit = true;
            label.verticalOverflow = VerticalWrapMode.Truncate;   // 3-154: 글자 자동 축소는 세로가 Truncate 일 때만 동작 (Overflow 면 줄지 않고 칸 밖으로 넘침)
            label.resizeTextMinSize = 12;
            label.resizeTextMaxSize = 30;
            label.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
            // HP 홈 안 채움 (빨강). 이름 HpBack / HpFill 은 예전 탭과 같음
            RectTransform hpBack = UIFactory.MakeRect("HpBack", tile);
            hpBack.anchorMin = TileHpMin;
            hpBack.anchorMax = TileHpMax;
            hpBack.offsetMin = new Vector2(3, 1);
            hpBack.offsetMax = new Vector2(-3, -1);
            Image hpFill = UIFactory.MakeImage("HpFill", hpBack, Color.white, false);
            hpFill.sprite = UIFactory.MakeGradient(UIFactory.Hex("#FF5A48"), UIFactory.Hex("#A01818"));
            hpFill.rectTransform.anchorMin = Vector2.zero;
            hpFill.rectTransform.anchorMax = Vector2.one;
            hpFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            hpFill.rectTransform.offsetMin = hpFill.rectTransform.offsetMax = Vector2.zero;
            hpFill.raycastTarget = false;
        }
    }

    void SelectPlayer(int index)
    {
        if (busy || dragging != null || run == null) return;
        controlIndex = index;
        bool inBattle = battle != null && battle.Result == BattleResult.Ongoing && mapPanelHidden();
        if (inBattle) Refresh();
        else { relicBar.Show(InfoPlayer()); RefreshPlayerTabs(); }
    }

    // 턴 종료 후: 아직 턴을 안 끝낸 다른 플레이어로 자동 전환 (한 PC 테스트 편의)
    void SelectNextAvailablePlayer()
    {
        if (battle.CanAct(Me)) return;
        for (int i = 0; i < battle.Players.Count; i++)
        {
            if (battle.CanAct(battle.Players[i])) { controlIndex = i; return; }
        }
    }

    // ==================== 캔버스 만들기 ====================
    void BuildCanvas()
    {
        // 캔버스: 화면 위에 그려지는 UI 판
        GameObject canvasGo = new GameObject("BattleCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);   // 이 크기를 기준으로 자동 확대·축소
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();              // 마우스 클릭을 UI에 전달
        root = (RectTransform)canvasGo.transform;

        // 이벤트 시스템: 마우스 입력을 UI 이벤트로 바꿔 주는 장치 (씬에 1개만 필요)
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }

        stageRoot = UIFactory.MakeRect("Stage", root);
        UIFactory.Stretch(stageRoot);
        BuildStageBackground();

        // 필살기 때 배경만 어둡게 하는 판 (캐릭터보다 뒤)
        stageDim = UIFactory.MakeImage("StageDim", stageRoot, new Color(0.04f, 0f, 0.07f, 0f), false);
        UIFactory.Stretch(stageDim.rectTransform, -40, -40, -40, -40);
        stageDim.raycastTarget = false;

        // 캐릭터·몬스터가 서는 무대 (화면 전체, 왼쪽 아래가 기준)
        playerArea = UIFactory.MakeRect("PlayerArea", stageRoot);
        UIFactory.Stretch(playerArea);
        enemyArea = UIFactory.MakeRect("EnemyArea", stageRoot);
        UIFactory.Stretch(enemyArea);

        BuildTopBar();

        bottomRoot = UIFactory.MakeRect("BottomUI", root);   // 아래쪽 UI 를 담는 판 (직업이 바뀌면 안을 비우고 다시 만듦)
        UIFactory.Stretch(bottomRoot);
        BuildBottom(null);

        // 손패 기준점: 화면 아래 가운데, 크기 0 → 카드 위치 = 이 점으로부터의 거리
        handRoot = UIFactory.MakeRect("Hand", root);
        UIFactory.Place(handRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0, 175), Vector2.zero);

        // 맨 위 레이어 (화살표, 숫자 팝업, 안내)
        popupLayer = UIFactory.MakeRect("PopupLayer", root);
        UIFactory.Stretch(popupLayer);
        for (int i = 0; i < 18; i++)
        {
            Image dot = UIFactory.MakeCircle("ArrowDot" + i, popupLayer, gold);
            dot.raycastTarget = false;
            dot.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.6f);   // 점 테두리
            dot.gameObject.SetActive(false);
            arrowDots.Add(dot);
        }
        arrowHead = UIFactory.MakeImage("ArrowHead", popupLayer, gold, false);
        arrowHead.sprite = UIFactory.TriangleSprite;
        arrowHead.raycastTarget = false;
        arrowHead.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.6f);
        arrowHead.gameObject.SetActive(false);

        vignette = UIFactory.MakeImage("Vignette", popupLayer, new Color(0.38f, 0.06f, 0.62f, 0f), false);
        vignette.sprite = UIFactory.VignetteSprite;
        UIFactory.Stretch(vignette.rectTransform);
        vignette.raycastTarget = false;
        vignette.transform.SetAsFirstSibling();
        flash = UIFactory.MakeImage("Flash", popupLayer, new Color(1f, 0.95f, 0.95f, 0f), false);
        UIFactory.Stretch(flash.rectTransform);
        flash.raycastTarget = false;

        toastText = UIFactory.MakeText("Toast", popupLayer, "", 30, UIFactory.Hex("#FFD27A"), TextAnchor.MiddleCenter);
        UIFactory.Place(toastText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(900, 60));
        toastText.fontStyle = FontStyle.Bold;

        BuildLogPanel();
        BuildResultPanel();

        mapPanel = MapPanel.Create(root);
        mapPanel.OnNodeClicked = OnMapNodeClicked;
        choicePanel = ChoicePanel.Create(root);
        choicePanel.OnChoiceClicked = OnSiegeChoiceClicked;
        placePanel = PlacePanel.Create(root);
        forgePanel = ForgePanel.Create(root);
        forgePanel.OnUpgrade = DoForge;
        forgePanel.OnLeave = LeaveForge;
        forgePanel.OnCardUpgrade = DoCardForge;
        rewardPanel = RewardPanel.Create(root);
        rewardPanel.OnClose = CloseRewards;
        rewardPanel.OnHover = ShowTip;
        shopPanel = ShopPanel.Create(root);
        shopPanel.OnHover = ShowTip;
        shopPanel.OnLeave = () => { run.LeaveShop(); ShowRunScreen(false); };
        shopPanel.OnBought = () => relicBar.Show(InfoPlayer());
        treasurePanel = TreasurePickPanel.Create(root);
        treasurePanel.OnHover = ShowTip;
        treasurePanel.OnDone = () => { ShowRunScreen(false); relicBar.Show(InfoPlayer()); };
        levelUpPanel = LevelUpPanel.Create(root);
        endingPanel = EndingPanel.Create(root);   // 3-112
        altarPanel = AltarPanel.Create(root);     // 3-114
        deckView = DeckViewPanel.Create(root);
        devPanel = DevPanel.Create(root);
        devPanel.GetRun = () => run;
        devPanel.GetBattle = () => (battle != null && battle.Result == BattleResult.Ongoing && resultPanel != null && !resultPanel.activeSelf && run != null && (mapPanelHidden())) ? battle : null;
        devPanel.AfterChange = DevRefresh;
        devPanel.Toast = Toast;
        titlePanel = TitlePanel.Create(root);   // 맨 마지막에 만들어서 모든 화면 위에 덮임
        titlePanel.OnSingle = StartSingle;
        titlePanel.OnMulti = OpenMultiSelect;
        titlePanel.OnContinue = ContinueRun;   // 이어하기 (3-73)
        characterSelect = CharacterSelectPanel.Create(root);
        characterSelect.OnPlay = PlaySingle;
        characterSelect.OnBack = ShowTitle;
        characterSelect.OnPlayMulti = StartMulti;
        characterSelect.OnBackMulti = BackToRoom;
        tutorialPanel = TutorialPanel.Create(root);   // 3-130: 다른 화면들 위 (일시정지 메뉴보다는 아래)
        pauseMenu = PauseMenu.Create(root);   // 맨 마지막: 모든 화면 위
        pauseMenu.OnTitle = ShowTitle;
        pauseMenu.OnSave = SaveFromMenu;   // 3-141
        compendium = CompendiumPanel.Create(root);   // 3-180 도감 (열 때 SetAsLastSibling 으로 맨 위)
        difficultyPanel = DifficultyPanel.Create(root);   // 3-196 난이도 선택
        titlePanel.OnCompendium = () => compendium.Open();
    }

    void BuildTopBar()
    {
        // 위쪽 줄 전체를 한 상자에 담음 → 지도 화면 위에도 보이게 맨 앞으로 올릴 수 있음
        topBar = UIFactory.MakeRect("TopBarRoot", root);
        UIFactory.Stretch(topBar);

        Image bar = UIFactory.MakeImage("TopBar", topBar, new Color(0, 0, 0, 0.45f), false);
        UIFactory.Place(bar.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1920, 64));
        bar.raycastTarget = false;

        topText = UIFactory.MakeText("TopText", topBar, "", 26, Color.white, TextAnchor.MiddleLeft);
        UIFactory.Place(topText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30, -8), new Vector2(DevPanel.Enabled ? 370 : 1000, 48));
        topText.fontStyle = FontStyle.Bold;
        topText.resizeTextForBestFit = true;   // 길면 글자를 줄여서 오른쪽 버튼과 안 겹치게
        topText.verticalOverflow = VerticalWrapMode.Truncate;   // 3-154: 글자 자동 축소는 세로가 Truncate 일 때만 동작 (Overflow 면 줄지 않고 칸 밖으로 넘침)
        topText.resizeTextMinSize = 15;
        topText.resizeTextMaxSize = 26;

        // ---- 유저용 버튼 (오른쪽부터): 메뉴 / 기록 / 덱 ----
        Button menuBtn = UIFactory.MakeButton("MenuButton", topBar, "메뉴", UIFactory.Hex("#3A2C4A"), () => { if (run != null) pauseMenu.Open(); }, 20);
        UIFactory.Place((RectTransform)menuBtn.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20, -10), new Vector2(130, 44));

        Button logBtn = UIFactory.MakeButton("LogButton", topBar, "기록", UIFactory.Hex("#2C3140"), () => logPanel.SetActive(!logPanel.activeSelf), 20);
        UIFactory.Place((RectTransform)logBtn.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-170, -10), new Vector2(130, 44));

        // 덱 보기 (슬더스2 처럼 언제든 내 카드 확인)
        Button deckBtn = UIFactory.MakeButton("DeckButton", topBar, "덱", UIFactory.Hex("#4A3A6A"), OpenDeckView, 20);
        UIFactory.Place((RectTransform)deckBtn.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-320, -10), new Vector2(130, 44));

        // 3-180 도감 (덱 왼쪽). 덱 = 지금 내 카드 / 도감 = 게임에 있는 카드·아티팩트 전부 (본 것만)
        Button compBtn = UIFactory.MakeButton("CompendiumButton", topBar, "도감", UIFactory.Hex("#5A4A2C"), () => compendium.Open(), 20);
        UIFactory.Place((RectTransform)compBtn.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-470, -10), new Vector2(130, 44));

        // 3-181: 위쪽 버튼 그림 (7번 그림). 덱 = 푸른 쇠, 기록 = 청동, 메뉴 = 보라, 도감 = 초록 (보라 그림 색만 바꿈)
        //   EnvelopeParent = 버튼 네모(130×44)를 덮도록 (그림 비율 약 2.2:1 → 130×58, 위쪽 줄 64 안에 들어감)
        UIFactory.SkinButton(deckBtn, "UI/Buttons/Top_Blue", 1.0f, true);
        UIFactory.SkinButton(logBtn, "UI/Buttons/Top_Brown", 1.0f, true);
        UIFactory.SkinButton(menuBtn, "UI/Buttons/Top_Purple", 1.0f, true);
        UIFactory.SkinButton(compBtn, "UI/Buttons/Top_Green", 1.0f, true);

        // ---- 개발자용 버튼: 에디터·개발 빌드에서만 생김 (유저용 빌드에는 아예 없음) ----
        //   3-180: [도감] 자리만큼 150 씩 왼쪽으로
        if (DevPanel.Enabled)
        {
            Button devBtn = UIFactory.MakeButton("DevButton", topBar, "개발 F1", UIFactory.Hex("#24402C"), () => devPanel.Toggle(), 18);
            UIFactory.Place((RectTransform)devBtn.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-620, -10), new Vector2(130, 44));

            Button newBtn = UIFactory.MakeButton("NewRun", topBar, "새 런", UIFactory.Hex("#3A4A2C"), () => NewRun(), 20);
            UIFactory.Place((RectTransform)newBtn.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-770, -10), new Vector2(130, 44));

            // 테스트 전투 버튼들: 진행 위치는 그대로 두고 그 전투만 바로 해 봄 (HP는 런 그대로)
            for (int i = 0; i < TestFightNames.Length; i++)
            {
                int index = i;   // 람다 안에서 쓰려면 반복 변수를 복사해 둬야 함
                Button b = UIFactory.MakeButton("Enc" + i, topBar, TestFightNames[i], UIFactory.Hex("#2C3140"),
                    () => StartTestBattle(index), 17);
                UIFactory.Place((RectTransform)b.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-915 - (TestFightNames.Length - 1 - i) * 125, -10), new Vector2(118, 44));
            }
        }

        // 아티팩트 · 골드 줄 (위쪽 줄 바로 아래 왼쪽)
        relicBar = RelicBar.Create(topBar);
        relicBar.OnHover = ShowTip;
        relicBar.OnPotion = UsePotion;
        relicBar.OnToggle = RefreshPlayerTabs;   // 3-91: 접기·펼치기 → P 탭도 같이
        relicBar.Show(null);
    }

    // 2026-11-09: 직업별 UI 그림 (Resources/UI/직업이름/*.png, 예: UI/FrostMage/UI_Energy.png)
    //   UI_Energy = 에너지 마법진, UI_Draw = 뽑기 더미, UI_Discard = 버림 더미, UI_EndTurn = 턴 종료 버튼
    //   UI_HpFrame + UI_HpFill = 체력바, UI_NamePlate = 머리 위 이름표
    //   지금 조작하는 플레이어의 직업 폴더에 그림이 없으면 예전 도형으로 그림 (자바로 치면 if (image == null) 기본 모양)
    //   뽑기·버림·턴 종료 그림에는 글자가 이미 들어 있어서, 코드에서는 장수(숫자)만 작은 동그라미에 씀
    private bool pileArt;          // 뽑기·버림 그림을 쓰는지 (숫자만 표시)
    private Text endTurnWaitText;  // 턴 종료 그림 위 "대기 중" 표시
    private RectTransform bottomRoot;
    private string bottomUiKey = "?";   // 지금 아래쪽 UI 를 어느 직업 그림으로 만들었는지

    // 직업별 UI 그림 불러오기 (없으면 null)
    static Sprite ClassUI(PlayerClass cls, string file)
    {
        return Resources.Load<Sprite>("UI/" + UnitArt.PlayerArtName(cls) + "/" + file);
    }

    // 조작하는 플레이어의 직업이 바뀌었으면(멀티 탭 전환, 새 전투) 아래쪽 UI 를 다시 만듦
    void EnsureBottomUI()
    {
        PlayerCombatant p = Me;
        string key = p != null ? UnitArt.PlayerArtName(p.Class) : "";
        if (key == bottomUiKey) return;
        bottomUiKey = key;
        for (int i = bottomRoot.childCount - 1; i >= 0; i--) Destroy(bottomRoot.GetChild(i).gameObject);
        endTurnWaitText = null;
        BuildBottom(p);
    }

    void BuildBottom(PlayerCombatant owner)
    {
        System.Func<string, Sprite> art = (file) => owner != null ? ClassUI(owner.Class, file) : null;
        RectTransform ui = bottomRoot;   // 아래쪽 UI 판 (전체 화면 크기라 위치는 예전과 같음)
        // ---- 에너지 ----
        Sprite energySprite = art("UI_Energy");
        if (energySprite != null)
        {
            Image orbArt = UIFactory.MakeImage("EnergyArt", ui, Color.white, false);
            orbArt.sprite = energySprite;
            orbArt.raycastTarget = false;
            UIFactory.Place(orbArt.rectTransform, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), new Vector2(165, 212), new Vector2(180, 180));   // 3-134: 240 → 212 (뽑기 더미가 작아져서 조금 내림)
            energyText = UIFactory.MakeText("Energy", orbArt.transform, "3/3", 46, Color.white, TextAnchor.MiddleCenter);
            UIFactory.Stretch(energyText.rectTransform);
            energyText.fontStyle = FontStyle.Bold;
            energyText.gameObject.AddComponent<Outline>().effectColor = new Color(0.05f, 0.02f, 0.2f, 1f);   // 밝은 빛 위에서도 읽히게
            energyText.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2, -2);
        }
        else
        {
            Image ring = UIFactory.MakeCircle("EnergyRing", ui, UIFactory.Hex("#3A2410"));
            UIFactory.Place(ring.rectTransform, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), new Vector2(165, 212), new Vector2(150, 150));   // 3-134
            ring.raycastTarget = false;
            Image orb = UIFactory.MakeCircle("EnergyOrb", ring.transform, UIFactory.Hex("#E09A30"));
            UIFactory.Stretch(orb.rectTransform, 10, 10, 10, 10);
            orb.raycastTarget = false;
            Image orbShine = UIFactory.MakeCircle("Shine", orb.transform, new Color(1f, 1f, 1f, 0.25f));
            UIFactory.Place(orbShine.rectTransform, new Vector2(0.3f, 0.7f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34, 22));
            orbShine.raycastTarget = false;
            energyText = UIFactory.MakeText("Energy", orb.transform, "3/3", 44, Color.white, TextAnchor.MiddleCenter);
            UIFactory.Stretch(energyText.rectTransform);
            energyText.fontStyle = FontStyle.Bold;
        }

        if (energySprite != null)
        {
            // 그림 UI: 뽑기 그림이 커서 예전 자리(에너지 바로 아래)와 겹침 → 에너지 오른쪽 아래에 어두운 띠를 깔고 그 위에 글자
            Image manaPlate = UIFactory.MakeImage("ManaPlate", ui, new Color(0.04f, 0.03f, 0.1f, 0.85f));
            UIFactory.Place(manaPlate.rectTransform, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), new Vector2(305, 160), new Vector2(160, 38));
            manaPlate.raycastTarget = false;
            manaPlate.gameObject.AddComponent<Outline>().effectColor = new Color(0.55f, 0.4f, 0.9f, 0.8f);
            manaText = UIFactory.MakeText("Mana", manaPlate.transform, "", 24, UIFactory.Hex("#DCC8FF"), TextAnchor.MiddleCenter);
            UIFactory.Stretch(manaText.rectTransform);
            manaText.fontStyle = FontStyle.Bold;
            manaText.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.9f);
            manaText.resizeTextForBestFit = true;   // "홍염 12 / 30" 처럼 길어도 띠 안에 들어가게
            manaText.verticalOverflow = VerticalWrapMode.Truncate;   // 3-154: 글자 자동 축소는 세로가 Truncate 일 때만 동작 (Overflow 면 줄지 않고 칸 밖으로 넘침)
            manaText.resizeTextMinSize = 14;
            manaText.resizeTextMaxSize = 24;
        }
        else
        {
            manaText = UIFactory.MakeText("Mana", ui, "", 28, UIFactory.Hex("#C9A8FF"), TextAnchor.MiddleCenter);
            UIFactory.Place(manaText.rectTransform, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), new Vector2(170, 132), new Vector2(260, 40));
            manaText.fontStyle = FontStyle.Bold;
        }

        // ---- 3-120: 직업 자원 아이콘 (에너지 오른쪽 아래). 아이콘 한가운데 지금 개수, 그 아래 작게 "/ 최대" ----
        resIcon = UIFactory.MakeImage("Resource", ui, Color.white, false);
        UIFactory.Place(resIcon.rectTransform, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), new Vector2(305, 140), new Vector2(108, 108));   // 3-134: 에너지와 같이 조금 내림
        resIcon.preserveAspect = true;
        resIcon.raycastTarget = true;   // 마우스를 올리면 "영혼 3 / 20" 설명
        HoverRelay resHover = resIcon.gameObject.AddComponent<HoverRelay>();
        resHover.OnChange = (on) => ShowTip(on && resTip.Length > 0 ? resTip : null, on ? resIcon.rectTransform : null);
        resNumber = UIFactory.MakeText("Count", resIcon.transform, "", 44, Color.white, TextAnchor.MiddleCenter);
        UIFactory.Place(resNumber.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 6), new Vector2(108, 56));
        resNumber.fontStyle = FontStyle.Bold;
        resNumber.raycastTarget = false;
        Outline rno = resNumber.gameObject.AddComponent<Outline>();
        rno.effectColor = new Color(0f, 0f, 0f, 0.95f);
        rno.effectDistance = new Vector2(2.5f, -2.5f);
        resMax = UIFactory.MakeText("Max", resIcon.transform, "", 19, UIFactory.Hex("#F2E8D8"), TextAnchor.MiddleCenter);
        UIFactory.Place(resMax.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -30), new Vector2(108, 24));
        resMax.fontStyle = FontStyle.Bold;
        resMax.raycastTarget = false;
        resMax.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.95f);
        resIcon.gameObject.SetActive(false);

        // ---- 뽑기 더미 (왼쪽 아래) / 버림 더미 (오른쪽 아래) ----
        Sprite drawSprite = art("UI_Draw");
        Sprite discardSprite = art("UI_Discard");
        pileArt = drawSprite != null && discardSprite != null;
        if (pileArt)
        {
            drawPile = PileArt("DrawPile", drawSprite, new Vector2(0f, 0f), new Vector2(68, 66), new Vector2(112, 112), 0, out drawText, DrawBadgeAnchor(owner.Class));   // 3-134: 140 → 112
            discardPile = PileArt("DiscardPile", discardSprite, new Vector2(1f, 0f), new Vector2(-58, 68), new Vector2(92, 112), 1, out discardText, new Vector2(0.9f, 0.9f));   // 3-134: 118×144 → 92×112
        }
        else
        {
            Image dp = UIFactory.MakeImage("DrawPile", ui, UIFactory.Hex("#2A3448"));
            UIFactory.Place(dp.rectTransform, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), new Vector2(70, 70), new Vector2(90, 110));
            dp.raycastTarget = true;   // 누르면 뽑기 더미 카드 보기
            dp.gameObject.AddComponent<Button>().onClick.AddListener(() => OpenPile(0));
            drawPile = dp.rectTransform;
            drawText = UIFactory.MakeText("DrawCount", dp.transform, "0", 26, Color.white, TextAnchor.MiddleCenter);
            UIFactory.Stretch(drawText.rectTransform);

            Image dc = UIFactory.MakeImage("DiscardPile", ui, UIFactory.Hex("#3A2A2A"));
            UIFactory.Place(dc.rectTransform, new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-70, 70), new Vector2(90, 110));
            dc.raycastTarget = true;   // 누르면 버림 더미 카드 보기
            dc.gameObject.AddComponent<Button>().onClick.AddListener(() => OpenPile(1));
            discardPile = dc.rectTransform;
            discardText = UIFactory.MakeText("DiscardCount", dc.transform, "0", 26, Color.white, TextAnchor.MiddleCenter);
            UIFactory.Stretch(discardText.rectTransform);
        }

        // ---- 턴 종료 버튼 ----
        Sprite endSprite = art("UI_EndTurn");
        if (endSprite != null)
        {
            endTurnButton = UIFactory.MakeButton("EndTurn", ui, "", Color.white, OnEndTurnClicked, 30);
            Image bimg = endTurnButton.GetComponent<Image>();
            bimg.sprite = endSprite;
            bimg.type = Image.Type.Simple;   // MakeButton 은 둥근 상자(Sliced)로 만들기 때문에 그림 그대로 보이게 바꿈
            bimg.preserveAspect = true;
            UIFactory.Place((RectTransform)endTurnButton.transform, new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-150, 182), new Vector2(290, 110));   // 3-134: 220×84 → 290×110 / 3-135: 에너지·영혼 사이 높이(182)로, 버림 더미 바로 위 (손패가 10장이어도 안 겹치고, 적 상태 아이콘 줄 바로 아래)   // 버림 더미 바로 위. 3-116: 오른쪽 적의 상태 아이콘 줄을 가려서 조금 작게·낮게
            // 그림에 "턴 종료" 글자가 있으니 버튼 글자는 비워 둠. 내 차례가 아닐 때만 "대기 중" 띠를 위에 덮음
            Image waitBand = UIFactory.MakeImage("WaitBand", endTurnButton.transform, new Color(0.03f, 0.02f, 0.08f, 0.82f), false);
            UIFactory.Place(waitBand.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(190, 52));
            waitBand.raycastTarget = false;
            endTurnWaitText = UIFactory.MakeText("Wait", waitBand.transform, "대기 중", 28, UIFactory.Hex("#C8C0E0"), TextAnchor.MiddleCenter);
            UIFactory.Stretch(endTurnWaitText.rectTransform);
            waitBand.gameObject.SetActive(false);
        }
        else
        {
            endTurnButton = UIFactory.MakeButton("EndTurn", ui, "턴 종료", UIFactory.Hex("#7A3030"), OnEndTurnClicked, 30);
            UIFactory.Place((RectTransform)endTurnButton.transform, new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-180, 176), new Vector2(230, 78));   // 3-116: 215 → 176 (적 상태 아이콘 줄 아래로)
        }
    }

    // 뽑기·버림 더미 그림 + 오른쪽 아래 장수 동그라미. which: 0 = 뽑기, 1 = 버림 (누르면 그 더미 카드 보기)
    // 3-121: 뽑기 그림에 그려진 장수 동그라미(그림 속 "5") 자리 = 코드 동그라미를 그 위에 덮음 (그림 오른쪽 위, 0~1 비율)
    static Vector2 DrawBadgeAnchor(PlayerClass c)
    {
        switch (c)
        {
            case PlayerClass.Sniper: return new Vector2(0.867f, 0.822f);
            case PlayerClass.FrostMage: return new Vector2(0.842f, 0.818f);
            case PlayerClass.CosmicPhoenix: return new Vector2(0.857f, 0.832f);
            case PlayerClass.BlackScythe: return new Vector2(0.856f, 0.846f);
            default: return new Vector2(0.88f, 0.1f);
        }
    }

    RectTransform PileArt(string name, Sprite sprite, Vector2 anchor, Vector2 pos, Vector2 size, int which, out Text countText, Vector2 badgeAt)
    {
        Image img = UIFactory.MakeImage(name, bottomRoot, Color.white, false);
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = true;
        // 3-134: 예전 크기(뽑기 140×140 / 버림 118×144) 대비 비율 → 장수 동그라미 크기에 씀 (그림 비율 맞추기 전에 계산)
        float k = size.y / (which == 0 ? 140f : 144f);
        // 3-121: 칸을 그림 비율에 맞춤 (그래야 동그라미 비율 자리가 그림 위 같은 곳이 됨. 크기는 size 안에 들어가게)
        float aspect = sprite.rect.width / sprite.rect.height;
        if (aspect > size.x / size.y) size = new Vector2(size.x, size.x / aspect);
        else size = new Vector2(size.y * aspect, size.y);
        UIFactory.Place(img.rectTransform, anchor, new Vector2(0.5f, 0.5f), pos, size);
        Button b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        ColorBlock cb = b.colors;
        cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        b.colors = cb;
        b.onClick.AddListener(() => OpenPile(which));

        Image badge = UIFactory.MakeCircle("CountBadge", img.transform, UIFactory.Hex("#1A1426"));
        // 3-134: 더미 그림이 작아지면 장수 동그라미도 같은 비율로 (그림 속 숫자 동그라미를 정확히 덮게)
        //   예전 크기일 때 동그라미 40 (k 는 위에서 계산)
        UIFactory.Place(badge.rectTransform, badgeAt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f * k, 40f * k));
        badge.raycastTarget = false;
        badge.gameObject.AddComponent<Outline>().effectColor = UIFactory.Hex("#D8B060");
        countText = UIFactory.MakeText("Count", badge.transform, "0", Mathf.Max(16, Mathf.RoundToInt(24f * k)), Color.white, TextAnchor.MiddleCenter);
        UIFactory.Stretch(countText.rectTransform);
        countText.fontStyle = FontStyle.Bold;
        return img.rectTransform;
    }

    // 배경: 위는 어두운 남색, 아래로 갈수록 검게 + 바닥 띠 + 지평선
    void BuildStageBackground()
    {
        Image bg = UIFactory.MakeImage("Background", stageRoot, Color.white, false);
        bg.sprite = UIFactory.MakeGradient(UIFactory.Hex("#262A3E"), UIFactory.Hex("#0C0D14"));
        UIFactory.Stretch(bg.rectTransform, -40, -40, -40, -40);   // 흔들려도 가장자리가 안 보이게 조금 크게
        bg.raycastTarget = false;

        // 스테이지 배경 그림 (전투를 시작할 때 스테이지에 맞춰 바꿈)
        //   AspectRatioFitter(EnvelopeParent) = 화면 비율이 달라도 그림 비율은 지키면서 빈틈 없이 꽉 채움
        // 2026-11-15: 배경마다 확대·위로 올리기를 하려고 한 겹 감쌈 (sceneHolder 를 키우고 옮김)
        sceneHolder = UIFactory.MakeRect("SceneHolder", stageRoot);
        UIFactory.Stretch(sceneHolder);
        sceneArt = UIFactory.MakeImage("SceneArt", sceneHolder, Color.white, false);
        UIFactory.Stretch(sceneArt.rectTransform, -40, -40, -40, -40);
        sceneArt.raycastTarget = false;
        AspectRatioFitter fit = sceneArt.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fit.aspectRatio = 16f / 9f;
        sceneArt.enabled = false;

        // 뒤쪽 큰 달빛 (분위기용)
        Image glow = UIFactory.MakeCircle("Glow", stageRoot, new Color(0.45f, 0.5f, 0.75f, 0.10f));
        plainBackground.Add(glow.gameObject);
        UIFactory.Place(glow.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -260), new Vector2(900, 900));
        glow.raycastTarget = false;

        // 바닥 (캐릭터가 서는 곳)
        Image floor = UIFactory.MakeImage("Floor", stageRoot, Color.white, false);
        floor.sprite = UIFactory.MakeGradient(UIFactory.Hex("#2A2630"), UIFactory.Hex("#0E0D12"));
        UIFactory.Place(floor.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0, FloorY + 40), new Vector2(2000, FloorY + 80));
        floor.raycastTarget = false;
        plainBackground.Add(floor.gameObject);
        Image horizon = UIFactory.MakeImage("Horizon", stageRoot, new Color(0.6f, 0.55f, 0.7f, 0.25f), false);
        UIFactory.Place(horizon.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0, FloorY + 40), new Vector2(2000, 2));
        horizon.raycastTarget = false;
        plainBackground.Add(horizon.gameObject);

        // 떠다니는 반딧불 14개 (작은 점 + 주변의 흐린 빛)
        for (int i = 0; i < 14; i++)
        {
            Image fly = UIFactory.MakeCircle("Firefly", stageRoot, UIFactory.Hex("#D8FF8A"));
            fly.raycastTarget = false;
            float size = Random.Range(3f, 6f);
            fly.rectTransform.anchorMin = fly.rectTransform.anchorMax = Vector2.zero;
            fly.rectTransform.sizeDelta = new Vector2(size, size);
            Image halo = UIFactory.MakeCircle("Halo", fly.transform, new Color(0.85f, 1f, 0.55f, 0.10f));
            halo.raycastTarget = false;
            UIFactory.Place(halo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 6, size * 6));
            fly.gameObject.AddComponent<Firefly>();
            plainBackground.Add(fly.gameObject);   // 성 그림에는 불티가 이미 그려져 있어서 반딧불은 숨김
        }
    }

    void BuildLogPanel()
    {
        Image panel = UIFactory.MakeImage("LogPanel", root, new Color(0, 0, 0, 0.85f));
        UIFactory.Place(panel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20, -70), new Vector2(620, 640));
        logText = UIFactory.MakeText("LogText", panel.transform, "", 17, Color.white, TextAnchor.LowerLeft);
        UIFactory.Stretch(logText.rectTransform, 14, 14, 10, 10);
        logText.verticalOverflow = VerticalWrapMode.Truncate;
        logPanel = panel.gameObject;
        logPanel.SetActive(false);
    }

    // 3-157: 패배 화면 [새 런] 그림 버튼. 진 스테이지 색: 1 빨강 / 2 파랑 / 3 초록 / 4 금(보라 틀)
    private Button resultButton;
    private Color resultButtonColor;
    private Sprite resultButtonSprite;
    static readonly string[] NewRunArt = { "Red", "Blue", "Green", "Gold" };
    void SetResultButtonArt(int stage)
    {
        if (resultButton == null) return;
        Image img = resultButton.GetComponent<Image>();
        RectTransform rt = (RectTransform)resultButton.transform;
        Sprite art = stage >= 1 ? Resources.Load<Sprite>("UI/NewRun/NewRun_" + NewRunArt[Mathf.Clamp(stage, 1, 4) - 1]) : null;
        if (art != null)
        {
            UIFactory.UnskinButton(resultButton, Color.white);   // 3-181: 공용 틀을 썼었으면 벗김
            img.sprite = art;
            img.type = Image.Type.Simple;
            img.color = Color.white;
            rt.sizeDelta = new Vector2(380, 140);   // 그림 비율 (약 2.7 : 1)
            resultButtonLabel.fontSize = 44;
            resultButtonLabel.font = UIFactory.TitleFont;
            resultButtonLabel.color = UIFactory.Hex("#FFE2B0");
        }
        else
        {
            img.sprite = resultButtonSprite;
            img.type = Image.Type.Sliced;
            img.color = resultButtonColor;
            rt.sizeDelta = new Vector2(300, 80);
            resultButtonLabel.fontSize = 30;
            resultButtonLabel.font = UIFactory.DefaultFont;
            resultButtonLabel.color = Color.white;
            UIFactory.SkinButton(resultButton, UIFactory.GothicButton);   // 3-181: 8번 공용 틀 (그림이 없으면 위 네모 그대로)
        }
    }

    void BuildResultPanel()
    {
        Image dim = UIFactory.MakeImage("Result", root, new Color(0, 0, 0, 0.7f), false);
        UIFactory.Stretch(dim.rectTransform);
        resultText = UIFactory.MakeText("ResultText", dim.transform, "", 72, gold, TextAnchor.MiddleCenter);
        UIFactory.Place(resultText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(1000, 120));
        resultText.fontStyle = FontStyle.Bold;
        resultSubText = UIFactory.MakeText("ResultSub", dim.transform, "", 30, Color.white, TextAnchor.MiddleCenter);
        UIFactory.Place(resultSubText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(1200, 90));
        Button again = UIFactory.MakeButton("Again", dim.transform, "다음 전투", UIFactory.Hex("#3A4A2C"), OnResultButton, 30);
        UIFactory.Place((RectTransform)again.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -130), new Vector2(300, 80));
        resultButtonLabel = again.GetComponentInChildren<Text>();
        resultButton = again;
        resultButtonColor = again.GetComponent<Image>().color;
        resultButtonSprite = again.GetComponent<Image>().sprite;
        resultPanel = dim.gameObject;
        BuildVictoryFrame(dim.transform);   // 3-181
        resultPanel.SetActive(false);
    }

    // ==================== 3-181: 승리 창 그림 (1번 그림) ====================
    // UI/Result/Result_Victory.png (586×524) = 날개 장식 틀 + 아래 빈 버튼 판. 글자는 코드가 그림 속 자리에 얹음
    //   그림 속 자리(그림 픽셀): 제목 (64,138)-(520,274) / 설명 두 줄 (54,282)-(528,378) / 버튼 판 (86,412)-(502,514)
    //   승리 때만 이 틀을 쓰고, 패배·런 클리어는 예전 모양 (패배는 스테이지 색 [새 런] 그림 버튼이 따로 있음)
    const float VictoryW = 586f, VictoryH = 524f;
    private GameObject victoryFrame;
    private Text victoryTitle, victorySub, victoryButtonLabel;

    void BuildVictoryFrame(Transform parent)
    {
        Sprite art = Resources.Load<Sprite>("UI/Result/Result_Victory");
        if (art == null) return;   // 그림이 없으면 예전 승리 화면 그대로
        Image frame = UIFactory.MakeImage("VictoryFrame", parent, Color.white, false);
        frame.sprite = art;
        frame.preserveAspect = true;
        UIFactory.Place(frame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(VictoryW, VictoryH) * 1.15f);
        frame.raycastTarget = false;

        RectTransform t = UIFactory.ArtSpot("TitleSpot", frame.transform, VictoryW, VictoryH, 64, 138, 520, 274);
        victoryTitle = UIFactory.MakeText("Title", t, "", 72, gold, TextAnchor.MiddleCenter);
        UIFactory.Stretch(victoryTitle.rectTransform);
        victoryTitle.font = UIFactory.TitleFont;
        victoryTitle.resizeTextForBestFit = true;
        victoryTitle.verticalOverflow = VerticalWrapMode.Truncate;   // 글자 자동 축소는 Truncate 일 때만
        victoryTitle.resizeTextMinSize = 24;
        victoryTitle.resizeTextMaxSize = 80;
        Outline to = victoryTitle.gameObject.AddComponent<Outline>();
        to.effectColor = new Color(0.12f, 0.05f, 0f, 0.9f);
        to.effectDistance = new Vector2(2, -2);

        RectTransform sub = UIFactory.ArtSpot("SubSpot", frame.transform, VictoryW, VictoryH, 54, 282, 528, 378);
        victorySub = UIFactory.MakeText("Sub", sub, "", 26, UIFactory.Hex("#F0E2C0"), TextAnchor.MiddleCenter);
        UIFactory.Stretch(victorySub.rectTransform);
        victorySub.fontStyle = FontStyle.Bold;
        victorySub.resizeTextForBestFit = true;
        victorySub.verticalOverflow = VerticalWrapMode.Truncate;
        victorySub.resizeTextMinSize = 14;
        victorySub.resizeTextMaxSize = 28;
        victorySub.lineSpacing = 1.1f;

        // 버튼: 그림 속 빈 판 위에 투명 단추 + 글자
        RectTransform b = UIFactory.ArtSpot("ButtonSpot", frame.transform, VictoryW, VictoryH, 86, 412, 502, 514);
        Image hit = UIFactory.MakeImage("Hit", b, new Color(1f, 1f, 1f, 0f), false);
        UIFactory.Stretch(hit.rectTransform);
        hit.raycastTarget = true;
        Button btn = hit.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(OnResultButton);
        victoryButtonLabel = UIFactory.MakeText("Label", b, "", 34, UIFactory.Hex("#FFE2B0"), TextAnchor.MiddleCenter);
        UIFactory.Stretch(victoryButtonLabel.rectTransform);
        victoryButtonLabel.font = UIFactory.TitleFont;
        victoryButtonLabel.raycastTarget = false;
        Outline bo = victoryButtonLabel.gameObject.AddComponent<Outline>();
        bo.effectColor = new Color(0.1f, 0.04f, 0f, 0.9f);
        bo.effectDistance = new Vector2(1.5f, -1.5f);
        // 마우스를 올리면 글자가 금빛으로 밝아짐
        HoverRelay hover = hit.gameObject.AddComponent<HoverRelay>();
        hover.OnChange = (on) => victoryButtonLabel.color = on ? UIFactory.Hex("#FFFFFF") : UIFactory.Hex("#FFE2B0");

        // 3-182 연출 3단계: 창 뒤 금빛 광선 + 탄력 있는 등장 + 반짝이 (VictoryFx 가 켜질 때마다 재생)
        Image rays = UIFactory.MakeImage("VictoryRays", parent, new Color(1f, 0.82f, 0.38f, 0f), false);
        rays.sprite = VictoryFx.RaySprite;
        rays.raycastTarget = false;
        UIFactory.Place(rays.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(1500, 1500));
        rays.transform.SetSiblingIndex(frame.transform.GetSiblingIndex());   // 창 바로 뒤
        rays.gameObject.SetActive(false);
        VictoryFx fx = frame.gameObject.AddComponent<VictoryFx>();
        fx.Rays = rays;
        fx.Title = victoryTitle;
        fx.SparkLayer = parent;

        victoryFrame = frame.gameObject;
        victoryFrame.SetActive(false);
    }

    // 승리 틀을 쓸지. 켜면 예전 글자·버튼은 숨기고 같은 글자를 틀 안에 복사
    void ShowVictoryFrame(bool on)
    {
        if (victoryFrame == null) on = false;
        if (victoryFrame != null) victoryFrame.SetActive(on);
        resultText.gameObject.SetActive(!on);
        resultSubText.gameObject.SetActive(!on);
        resultButton.gameObject.SetActive(!on);
        if (!on) return;
        victoryTitle.text = resultText.text;
        victorySub.text = resultSubText.text;
        victoryButtonLabel.text = resultButtonLabel.text;
    }

    // ==================== 적 / 플레이어 (바닥 위에 선 모습) ====================
    // 플레이어 대형: (x, 바닥에서 위로 얼마나) - [0] = P1 (맨 앞)
    // 2026-11-15: 몬스터는 뒤(오른쪽)로 밀고, 맨 앞(P1)은 거의 그대로 (+40).
    //   왼쪽 끝에 몰려 있던 뒷사람(P2·P3·P4)만 앞으로 당겨서 간격을 좁힘 (마도사 HP바가 에너지 UI와 겹치지 않게)
    //   위아래 차이(뒷줄을 위로 올리는 값)도 줄여서 모두 같은 바닥에 선 것처럼 (예전 75~105 → 떠 보였음)
    private static readonly Vector2[] PartyFormation1 = { new Vector2(560, 0) };
    private static readonly Vector2[] PartyFormation2 = { new Vector2(680, 0), new Vector2(460, 22) };
    private static readonly Vector2[] PartyFormation3 = { new Vector2(760, 0), new Vector2(550, 28), new Vector2(340, 12) };
    // 4명: 앞줄 둘(P1, P3) + 뒷줄 둘(P2, P4)이 지그재그. 모두 0.8배
    private static readonly Vector2[] PartyFormation4 = { new Vector2(800, 0), new Vector2(620, 32), new Vector2(440, 10), new Vector2(260, 40) };
    // 적 무리의 가운데 x (예전 1380). 오른쪽 끝 적은 RightmostEnemyX 를 넘지 않게
    private const float EnemyCenterX = 1490f;
    private const float RightmostEnemyX = 1720f;
    // 배경별 (확대, 위로 올림, 발 높이) - 2026-11-15 배경마다 캐릭터를 그림 속 바닥에 세워 보고 정함
    //   확대·올림: 바닥이 화면 아래쪽에 있는 그림은 키우고 올려서 바닥이 손패 위로 오게 함 (아래 빈틈이 안 생기는 만큼만)
    private static readonly Dictionary<string, Vector3> SceneLayouts = new Dictionary<string, Vector3>
    {
        { "CastleInside", new Vector3(1f, 0f, 330f) },
        { "CastleOutside", new Vector3(1f, 0f, 330f) },          // 3-100: 2스테이지 밤의 성문 앞 (발 = 성문 계단 앞 흙마당)
        { "FortressGate", new Vector3(1.25f, 120f, 290f) },       // 요새 문 앞 돌바닥
        { "FortressGateOpen", new Vector3(1.25f, 120f, 290f) },
        { "MarchForest", new Vector3(1.12f, 50f, 300f) },         // 숲속 풀밭
        { "MarchHighland", new Vector3(1.12f, 50f, 300f) },       // 바위 고원의 평평한 땅
        { "ArchitectRoom", new Vector3(1.12f, 50f, 300f) },       // 왕좌의 방 돌바닥
        { "CastleHall", new Vector3(1.5f, 255f, 290f) },          // 최종장 첫 전투: 보라 수정 대전당 (바닥이 그림 아래쪽이라 크게 확대)
        { "FinalHall", new Vector3(1.55f, 285f, 290f) },          // 3-155 최종장 2층~: 보라 수정 회랑 (발 = 난간 앞 돌바닥, 그림 아래 끝이 화면 아래를 덮도록 1.55배)
    };
    private RectTransform sceneHolder;
    private float currentFloorY = FloorY;   // 지금 배경의 발 높이
    private const float FloorY = 330f;   // 캐릭터 발이 닿는 높이 (화면 아래에서부터). 2026-11-10: 420 → 330 (배경 바닥에 서도록, 손패가 내려가서 자리가 생김)

    void BuildUnits()
    {
        foreach (UnitView v in playerViews) Destroy(v.Rt.gameObject);
        playerViews.Clear();
        BuildEnemyViews();

        // 플레이어: 화면 왼쪽, P1 이 가장 앞(오른쪽). 2026-11-15: 사람 수에 따라 대형을 바꿈
        //   뒤에 선 사람은 살짝 위(멀리)에 세워서 겹치지 않고, 체력바가 왼쪽 아래 에너지 UI 와 겹치지 않게
        //   3명이면 모두 0.9배로 조금 작게
        int pn = battle.Players.Count;
        Vector2[] formation = pn >= 4 ? PartyFormation4 : pn == 3 ? PartyFormation3 : (pn == 2 ? PartyFormation2 : PartyFormation1);
        float partyScale = pn >= 4 ? 0.8f : (pn == 3 ? 0.9f : 1f);
        for (int i = 0; i < pn; i++)
        {
            // 3-195: 체력바 폭 = 옆 사람과의 간격보다 조금 좁게 (4명 180 → 168 / 3명 210 → 196 / 2명 220 → 206) → 서로 안 겹침
            UnitView v = MakeUnitView(battle.Players[i], playerArea, false, pn >= 4 ? 168f : (pn == 3 ? 196f : (pn == 2 ? 206f : HpBarW)));
            Vector2 spot = formation[Mathf.Min(i, formation.Length - 1)];
            UIFactory.Place(v.Rt, Vector2.zero, new Vector2(0.5f, 0f), new Vector2(spot.x, currentFloorY + spot.y), new Vector2(260, 420));
            v.Rt.localScale = new Vector3(partyScale, partyScale, 1f);
            // 3-195: 몸은 0.8·0.9 배로 작아져도 체력바·방어도·HP 숫자는 원래 크기 (작아서 안 보이던 문제)
            if (v.HpRoot != null && partyScale < 1f)
            {
                float inv = 1f / partyScale;
                v.HpRoot.localScale = new Vector3(inv, inv, 1f);
                // 체력바가 커진 만큼 상태 아이콘 줄을 아래로
                v.StatusRow.anchoredPosition = new Vector2(v.StatusRow.anchoredPosition.x, -22f - v.HpBarH * inv - 4f);
            }
            playerViews.Add(v);
        }
        // 그리는 순서: 위(멀리)에 선 사람부터 → 앞사람이 뒷사람을 가림
        List<UnitView> byDepth = new List<UnitView>(playerViews);
        byDepth.Sort((a, b) => a.Rt.anchoredPosition.y.CompareTo(b.Rt.anchoredPosition.y));   // 낮은(앞) 사람이 먼저 → SetAsFirstSibling 으로 맨 뒤로 보내므로 결국 마지막
        foreach (UnitView v in byDepth) v.Rt.SetAsFirstSibling();
    }

    // 적 칸만 새로 만듦 (전투 시작, 웨이브가 바뀔 때)
    void BuildEnemyViews()
    {
        foreach (UnitView v in enemyViews) Destroy(v.Rt.gameObject);
        enemyViews.Clear();

        // 적: 화면 오른쪽에 나란히 (가운데 x = 1400). 3마리면 간격을 조금 좁힘
        shownEnemyListVersion = battle.EnemyListVersion;
        int n = battle.Enemies.Count;
        float gap = n >= 5 ? 205f : (n >= 4 ? 235f : (n >= 3 ? 270f : 300f));   // 3-102: 무리 몬스터(4~5마리)는 더 촘촘히
        for (int i = 0; i < n; i++)
        {
            UnitView v = MakeUnitView(battle.Enemies[i], enemyArea, true, Mathf.Min(HpBarW, gap - 24f));   // 3-149: 몬스터가 많으면 옆 칸과 안 겹치게
            // 무리 전체를 오른쪽으로 밀되, 오른쪽 끝 적이 화면 밖/턴 종료 버튼 쪽으로 나가면 무리째 왼쪽으로 당김
            float center = Mathf.Min(EnemyCenterX, RightmostEnemyX - (n - 1) / 2f * gap);
            float x = center + (i - (n - 1) / 2f) * gap;
            UIFactory.Place(v.Rt, Vector2.zero, new Vector2(0.5f, 0f), new Vector2(x, currentFloorY), new Vector2(280, 420));
            // 3-106: 4마리 이상이면 의도 칸(원래 폭 300)이 옆 칸과 겹침 → 간격에 맞게 줄이고, 글자가 길면 작게
            if (n >= 4 && v.IntentBox != null)
            {
                v.IntentBox.sizeDelta = new Vector2(gap - 8f, IntentBoxH);
                v.IntentText.resizeTextForBestFit = true;
                v.IntentText.verticalOverflow = VerticalWrapMode.Truncate;   // 3-154: 글자 자동 축소는 세로가 Truncate 일 때만 동작 (Overflow 면 줄지 않고 칸 밖으로 넘침)
                v.IntentText.resizeTextMinSize = 11;
                v.IntentText.resizeTextMaxSize = 21;
            }
            enemyViews.Add(v);
        }
    }

    // 다음 웨이브: 쓰러진 적을 잠깐 보여 준 뒤 새 적으로 바꿈
    IEnumerator WaveSwapRoutine()
    {
        waveSwapping = true;
        busy = true;
        yield return new WaitForSeconds(0.6f);
        arrowTarget = null;
        BuildEnemyViews();
        shownWaveVersion = battle.WaveVersion;
        waveSwapping = false;
        busy = false;
        Refresh();
        Toast("웨이브 " + battle.WaveNumber + " / " + battle.WaveCount + "  ·  적이 몰려온다!");
        Shake(0.3f, 12f);
    }

    // hpWidth (3-149): 새 체력바 폭. 사람·몬스터 수가 많으면 BattleScreen 이 줄여서 넘김
    UnitView MakeUnitView(Combatant unit, RectTransform parent, bool isEnemy, float hpWidth = HpBarW)
    {
        UnitView v = new UnitView();
        v.Unit = unit;
        v.Rt = UIFactory.MakeRect(unit.Name, parent);
        v.LastHp = unit.Hp;
        v.LastBlock = unit.Block;
        ReadStatus(unit, out v.LastDebuff, out v.LastBurn, out v.LastFreeze, out v.LastStrength);   // 3-186

        // 대상으로 지정됐을 때 빛나는 바닥 원 (평소에는 투명)
        v.Panel = UIFactory.MakeCircle("TargetGlow", v.Rt, new Color(1f, 0.6f, 0.2f, 0f));
        UIFactory.Place(v.Panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(260, 60));
        v.Panel.raycastTarget = false;

        // 발밑 그림자
        Image shadow = UIFactory.MakeCircle("Shadow", v.Rt, new Color(0, 0, 0, 0.45f));
        UIFactory.Place(shadow.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(200, 34));
        shadow.raycastTarget = false;

        // 그림 상자: 기준점이 발밑(아래 가운데). 숨쉬기·돌진·피격은 이 상자만 움직임
        v.Body = UIFactory.MakeRect("Body", v.Rt);
        UIFactory.Place(v.Body, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(260, 420));

        // 그림 (Resources/Art 에 파일이 있으면 그 그림, 없으면 임시 도형)
        float artHeight;
        RectTransform weapon = null;
        RectTransform cape = null;
        if (isEnemy)
        {
            artHeight = UnitArt.Build(v.Body, UnitArt.EnemyArtName(unit.Name), false, Color.white);
        }
        else
        {
            PlayerCombatant pl = (PlayerCombatant)unit;
            Color tint;
            switch (pl.Class)
            {
                case PlayerClass.CosmicPhoenix: tint = UIFactory.Hex("#B5562A"); break;
                case PlayerClass.FrostMage: tint = UIFactory.Hex("#4A86B8"); break;
                case PlayerClass.Sniper: tint = UIFactory.Hex("#B08A3A"); break;
                default: tint = UIFactory.Hex("#3A3446"); break;
            }
            artHeight = UnitArt.Build(v.Body, UnitArt.PlayerArtName(pl.Class), true, tint, out weapon, out cape);
        }
        v.ArtHeight = artHeight;
        v.Motion = v.Body.gameObject.AddComponent<UnitMotion>();
        v.Motion.Facing = isEnemy ? -1f : 1f;
        v.Motion.Weapon = weapon;   // 무기 그림이 있으면 따로 흔들고 휘두름
        v.Motion.Cape = cape;       // 망토 그림이 있으면 따로 펄럭임

        // 이름 (그림 바로 위)
        v.NameText = UIFactory.MakeText("Name", v.Rt, unit.Name, 24, Color.white, TextAnchor.MiddleCenter);
        UIFactory.Place(v.NameText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, artHeight + 6), new Vector2(320, 32));
        v.NameText.fontStyle = FontStyle.Bold;
        Outline nameOutline = v.NameText.gameObject.AddComponent<Outline>();   // 글자 테두리 (배경에서 잘 보이게)
        nameOutline.effectColor = new Color(0, 0, 0, 0.8f);

        // 3-169: 마도사(제피르)만 쓰던 "이름표 그림"은 없앰 (그림은 Assets/Art_Old/UI_FrostMage_예전 에 보관)
        //   → 이제 4캐릭터 모두 위의 NameText 한 줄("P1 제피르")로 같은 글꼴·크기로 씀. 캐릭터 이름만 고유 색

        if (isEnemy)
        {
            // 머리 위 의도
            Image intentBox = UIFactory.MakeImage("Intent", v.Rt, new Color(0.05f, 0.04f, 0.06f, 0.85f));
            UIFactory.Place(intentBox.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, artHeight + 44), new Vector2(300, IntentBoxH));
            intentBox.raycastTarget = false;
            v.IntentBox = intentBox.rectTransform;
            v.IntentBaseY = artHeight + 44;
            v.IntentIcon = UIFactory.MakeRect("IntentIcon", intentBox.transform);
            UIFactory.Place(v.IntentIcon, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(30, 0), new Vector2(44, 44));
            v.IntentText = UIFactory.MakeText("IntentText", intentBox.transform, "", 21, UIFactory.Hex("#FF9A7A"), TextAnchor.MiddleCenter);
            UIFactory.Stretch(v.IntentText.rectTransform, 56, 8, 3, 3);
            v.IntentText.fontStyle = FontStyle.Bold;
            // 3-148: 긴 의도("끈적한 점액: 공격 2 + 방어도 15 + 약화 부여 (전원)")가 상자 밖으로 넘치던 것 → 늘 글자 크기 자동 축소
            v.IntentText.resizeTextForBestFit = true;
            v.IntentText.verticalOverflow = VerticalWrapMode.Truncate;   // 3-154: 글자 자동 축소는 세로가 Truncate 일 때만 동작 (Overflow 면 줄지 않고 칸 밖으로 넘침)
            v.IntentText.resizeTextMinSize = 12;
            v.IntentText.resizeTextMaxSize = 21;
        }

        // 체력바 (발밑 아래)
        //   3-149: 새 체력바 그림 (UI/HpBar_Frame: 양 끝 보석 + 어두운 홈) 이 있으면 모든 캐릭터·몬스터가 이 체력바
        //     가운데만 늘어나는 9칸 자르기(Sliced) 그림이라 폭을 바꿔도 양 끝 보석은 그대로. 홈 안을 빨간 채움이 HP 비율만큼
        //   없으면 예전 체력바 (직업 그림 UI_HpFrame + UI_HpFill, 또는 빨간 막대)
        Sprite newBar = Resources.Load<Sprite>("UI/HpBar_Frame");
        float barW = hpWidth, barH = hpWidth < 230f ? HpBarH - 6f : HpBarH;
        bool hpArt = false;   // 예전 직업 체력바 그림을 쓰는지 (아래 방어도 방패 자리에 씀)
        if (newBar != null)
        {
            Image hpRoot = UIFactory.MakeImage("HpBack", v.Rt, new Color(0, 0, 0, 0), false);
            UIFactory.Place(hpRoot.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(10, -22), new Vector2(barW, barH));
            hpRoot.raycastTarget = false;
            // 그림 픽셀(높이 54) → 화면 높이 barH 로 같은 비율 (pixelsPerUnitMultiplier = 그림 픽셀 / 화면 칸)
            float k = barH / HpBarArtH;
            RectTransform inner = UIFactory.MakeRect("HpInner", hpRoot.transform);
            UIFactory.Stretch(inner, HpBarInL * k, HpBarInR * k, HpBarInT * k, HpBarInB * k);
            // 3-154: 틀 그림의 홈을 투명하게 바꿈 → 홈 = 어두운 빈 칸(잃은 HP) 위에 빨간 채움(남은 HP). 틀은 맨 위
            //   (예전엔 틀 그림의 홈이 불투명한 검정이라 그 아래 빨간 채움이 가려져서 HP 가 줄어드는 게 안 보였음)
            Image empty = UIFactory.MakeImage("HpEmpty", inner, Color.white, false);
            empty.sprite = UIFactory.MakeGradient(UIFactory.Hex("#2A0E12"), UIFactory.Hex("#12060A"));
            UIFactory.Stretch(empty.rectTransform, -2, -2, -2, -2);   // 틀 가장자리 아래로 살짝 겹쳐 틈이 안 보이게
            empty.raycastTarget = false;
            v.HpFill = UIFactory.MakeImage("HpFill", inner, Color.white, false);
            v.HpFill.sprite = UIFactory.MakeGradient(UIFactory.Hex("#F0505A"), UIFactory.Hex("#A01822"));   // 위 밝은 빨강 → 아래 진한 빨강
            v.HpFill.rectTransform.anchorMin = Vector2.zero;
            v.HpFill.rectTransform.anchorMax = Vector2.one;
            v.HpFill.rectTransform.offsetMin = v.HpFill.rectTransform.offsetMax = Vector2.zero;   // 비율은 anchorMax.x 로 (Refresh)
            v.HpFill.raycastTarget = false;
            Image frame = UIFactory.MakeImage("HpFrame", hpRoot.transform, Color.white, false);
            frame.sprite = newBar;
            frame.type = Image.Type.Sliced;
            frame.pixelsPerUnitMultiplier = HpBarArtH / barH;
            UIFactory.Stretch(frame.rectTransform);
            frame.raycastTarget = false;
            v.HpRoot = hpRoot.rectTransform;
            v.HpFrame = frame;
            v.HpBarH = barH;
            // 3-195: HP 숫자를 조금 크게 (0.45 → 0.52) + 테두리 두껍게
            v.HpText = UIFactory.MakeText("HpText", hpRoot.transform, "", Mathf.RoundToInt(barH * 0.52f), Color.white, TextAnchor.MiddleCenter);
            UIFactory.Stretch(v.HpText.rectTransform, 0, 0, 0, 2);
            v.HpText.fontStyle = FontStyle.Bold;
            Outline ho = v.HpText.gameObject.AddComponent<Outline>();
            ho.effectColor = new Color(0, 0, 0, 1f);
            ho.effectDistance = new Vector2(1.6f, -1.6f);
        }
        else
        {
            //   그림(Resources/UI/직업이름/UI_HpFrame + UI_HpFill)이 있으면: 장식 틀 + 안쪽 빨간 천을 HP 비율만큼 왼쪽부터 채움 (Image.Type.Filled)
            //   없으면 예전처럼 채움의 오른쪽 끝(anchorMax.x)을 줄여서 표현
            PlayerCombatant hpOwner = isEnemy ? null : unit as PlayerCombatant;   // 몬스터는 예전 체력바
            Sprite hpFrameSprite = hpOwner != null ? ClassUI(hpOwner.Class, "UI_HpFrame") : null;
            Sprite hpFillSprite = hpOwner != null ? ClassUI(hpOwner.Class, "UI_HpFill") : null;
            hpArt = hpFrameSprite != null && hpFillSprite != null;
            Image hpBack = UIFactory.MakeImage("HpBack", v.Rt, hpArt ? new Color(0, 0, 0, 0) : UIFactory.Hex("#1A0C0E"));
            UIFactory.Place(hpBack.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(10, -22), hpArt ? new Vector2(300, 26) : new Vector2(220, 26));
            hpBack.raycastTarget = false;
            if (hpArt)
            {
                // 틀 그림의 안쪽 빈칸 = 좌우 8.5%, 위아래 19% 안쪽 (그림에서 잰 값)
                Vector2 inMin = new Vector2(0.085f, 0.19f), inMax = new Vector2(0.915f, 0.81f);
                Image inner = UIFactory.MakeImage("HpEmpty", hpBack.transform, UIFactory.Hex("#140809"), false);
                inner.rectTransform.anchorMin = inMin; inner.rectTransform.anchorMax = inMax;
                inner.rectTransform.offsetMin = inner.rectTransform.offsetMax = Vector2.zero;
                inner.raycastTarget = false;
                v.HpFill = UIFactory.MakeImage("HpFill", hpBack.transform, Color.white, false);
                v.HpFill.sprite = hpFillSprite;
                v.HpFill.type = Image.Type.Filled;
                v.HpFill.fillMethod = Image.FillMethod.Horizontal;
                v.HpFill.fillOrigin = (int)Image.OriginHorizontal.Left;
                v.HpFill.rectTransform.anchorMin = inMin; v.HpFill.rectTransform.anchorMax = inMax;
                v.HpFill.rectTransform.offsetMin = v.HpFill.rectTransform.offsetMax = Vector2.zero;
                v.HpFill.raycastTarget = false;
                Image frame = UIFactory.MakeImage("HpFrame", hpBack.transform, Color.white, false);
                frame.sprite = hpFrameSprite;
                UIFactory.Stretch(frame.rectTransform);
                frame.raycastTarget = false;
            }
            else
            {
                v.HpFill = UIFactory.MakeImage("HpFill", hpBack.transform, UIFactory.Hex("#D2404C"));
                v.HpFill.rectTransform.anchorMin = Vector2.zero;
                v.HpFill.rectTransform.anchorMax = Vector2.one;
                v.HpFill.rectTransform.offsetMin = new Vector2(3, 3);
                v.HpFill.rectTransform.offsetMax = new Vector2(-3, -3);
                v.HpFill.raycastTarget = false;
            }
            v.HpText = UIFactory.MakeText("HpText", hpBack.transform, "", 18, Color.white, TextAnchor.MiddleCenter);
            UIFactory.Stretch(v.HpText.rectTransform);
            v.HpText.fontStyle = FontStyle.Bold;
            v.HpText.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.9f);
        }
        bool newHp = newBar != null;
        float hpBottom = newHp ? 22f + barH : 48f;   // 체력바 아래 끝 (발밑 기준 아래로)

        // 방어도 방패 (체력바 왼쪽 동그라미, 0이면 숨김)
        // 3-195: 새 체력바면 방패를 체력바 안(왼쪽 보석 위)에 넣음 → 체력바 밖으로 안 튀어나와 옆 사람 체력바와 안 겹치고, 체력바와 같이 커짐
        Image shield = UIFactory.MakeCircle("Shield", (newHp && v.HpRoot != null) ? (Transform)v.HpRoot : v.Rt, UIFactory.Hex("#3C78C8"));
        if (newHp && v.HpRoot != null)
            UIFactory.Place(shield.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(4, 0), new Vector2(46, 46));
        else
            UIFactory.Place(shield.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(hpArt ? -158 : -112, -35), new Vector2(46, 46));
        shield.raycastTarget = false;
        v.BlockText = UIFactory.MakeText("Block", shield.transform, "", 22, Color.white, TextAnchor.MiddleCenter);
        UIFactory.Stretch(v.BlockText.rectTransform);
        v.BlockText.fontStyle = FontStyle.Bold;
        // 3-171: 방어도 그림 (Icons/Block_Shield = 강철 방패 동그라미, 숫자 칸은 비워 둠) → 파란 동그라미 대신 그림 + 숫자는 코드가 씀
        //   숫자: 하늘색 + 검은 테두리 (방패 위에서도 잘 보이게). 그림이 없으면 예전 파란 동그라미 그대로
        Sprite blockArt = Resources.Load<Sprite>("Icons/Block_Shield");
        if (blockArt != null)
        {
            shield.sprite = blockArt;
            shield.type = Image.Type.Simple;
            shield.preserveAspect = true;
            shield.color = Color.white;
            shield.rectTransform.sizeDelta = new Vector2(BlockIconSize, BlockIconSize);
            if (!newHp) shield.rectTransform.anchoredPosition += new Vector2(-(BlockIconSize - 46f) / 2f, 0f);   // 커진 만큼 왼쪽으로 → 체력바를 안 가림
            v.BlockText.fontSize = 28;
            v.BlockText.color = Color.white;               // 3-195: 하늘색 → 흰색 (더 또렷하게)
            v.BlockText.resizeTextForBestFit = true;      // 세 자리(100 이상)도 방패 안에 들어가게 자동 축소
            v.BlockText.resizeTextMinSize = 12;
            v.BlockText.resizeTextMaxSize = 28;
            v.BlockText.verticalOverflow = VerticalWrapMode.Truncate;
            UIFactory.Stretch(v.BlockText.rectTransform, 12, 12, 10, 8);   // 방패 안쪽만 (가장자리 테두리는 피함)
            Outline blockOutline = v.BlockText.gameObject.AddComponent<Outline>();
            blockOutline.effectColor = new Color(0.0f, 0.1f, 0.35f, 1f);   // 3-195: 진한 파랑 테두리 두껍게
            blockOutline.effectDistance = new Vector2(2f, -2f);
        }

        // 상태 아이콘 줄 (체력바 바로 아래). 마우스를 올리면 설명
        v.StatusRow = UIFactory.MakeRect("StatusRow", v.Rt);
        UIFactory.Place(v.StatusRow, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(10, newHp ? -hpBottom - 4f : -56f), new Vector2(320, StatusIcons.Size));   // 3-149: 새 체력바 바로 아래

        // 효과 글자 (아이콘 줄 아래, 플레이어의 파워 이름 등)
        v.InfoText = UIFactory.MakeText("Info", v.Rt, "", 17, UIFactory.Hex("#E0D8C4"), TextAnchor.UpperCenter);
        UIFactory.Place(v.InfoText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0, -100), new Vector2(340, 70));
        v.InfoText.raycastTarget = false;
        v.InfoText.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
        return v;
    }

    // ==================== 화면 다시 맞추기 ====================
    // 카드를 쓰거나 턴을 넘긴 뒤 부르면, 규칙 상태(battle)에 맞게 모든 글자와 카드를 다시 맞춤
    void Refresh()
    {
        PlayerCombatant active = battle.CanAct(Me) ? Me : null;   // 지금 조작 가능한 플레이어 (턴 종료했으면 null)

        // 위쪽 글: 스테이지·칸·턴 + 아직 턴 종료 안 한 사람
        List<string> waiting = new List<string>();
        foreach (PlayerCombatant pl in battle.Players) if (battle.CanAct(pl)) waiting.Add(pl.Name);
        string who = (battle.Players.Count > 1 && waiting.Count > 0) ? "   |   진행 중: " + string.Join(", ", waiting) : "";
        string where = (run.PendingNode != null || run.PendingChoice != null)
            ? run.ProgressText + " " + run.PendingLabel
            : "테스트 전투";
        string wave = battle.WaveCount > 1 ? "   웨이브 " + battle.WaveNumber + "/" + battle.WaveCount : "";
        topText.text = run.Stage + "스테이지 " + where + wave + "   " + battle.Round + "턴" + who + DiffTag();

        // 웨이브가 바뀌었으면 적 칸을 새로 만들기 (쓰러진 적을 잠깐 보여 준 뒤)
        if (battle.WaveVersion != shownWaveVersion && !waveSwapping)
        {
            StartCoroutine(WaveSwapRoutine());
        }
        else if (battle.EnemyListVersion != shownEnemyListVersion && !waveSwapping)
        {
            // 3-102 점토 분열: 웨이브 안내 없이 적 칸만 다시 만듦
            arrowTarget = null;
            BuildEnemyViews();
        }

        foreach (UnitView v in enemyViews) RefreshUnit(v, true);
        foreach (UnitView v in playerViews) RefreshUnit(v, false);
        relicBar.Show(InfoPlayer(), Me != null && battle.Result == BattleResult.Ongoing && battle.CanAct(Me) && !busy);

        EnsureBottomUI();
        PlayerCombatant p = Me;
        if (p != null)
        {
            energyText.text = p.Energy + "/" + p.BaseEnergy();
            // 3-120: 직업 자원 아이콘이 있으면 아이콘 안에 숫자, 없으면 예전 글자
            Sprite resSprite = p.MaxMana > 0 ? ResourceSprite(p.Class) : null;
            bool useIcon = resSprite != null;
            resIcon.gameObject.SetActive(useIcon);
            if (useIcon)
            {
                if (resIcon.sprite != resSprite) resIcon.sprite = resSprite;
                resNumber.text = p.Mana.ToString();
                resMax.text = "/ " + p.MaxMana;
                resTip = p.ManaName + " " + p.Mana + " / " + p.MaxMana;
            }
            manaText.text = (!useIcon && p.MaxMana > 0) ? p.ManaName + " " + p.Mana + " / " + p.MaxMana : "";   // 자원이 없으면 숨김
            if (manaText.transform.parent != bottomRoot) manaText.transform.parent.gameObject.SetActive(!useIcon && p.MaxMana > 0);   // 아이콘을 쓰거나 자원이 없으면 띠도 숨김
            drawText.text = pileArt ? p.DrawPile.Count.ToString() : "뽑기\n" + p.DrawPile.Count;
            discardText.text = pileArt ? p.DiscardPile.Count.ToString() : "버림\n" + p.DiscardPile.Count;
        }

        endTurnButton.interactable = (active != null && !busy);
        bool notMyTurn = !(active != null || Me == null);   // (위에 waiting 이라는 이름이 이미 있어서 다른 이름)
        if (endTurnWaitText != null) endTurnWaitText.transform.parent.gameObject.SetActive(notMyTurn);   // 그림 버튼: "대기 중" 띠
        else endTurnButton.GetComponentInChildren<Text>().text = notMyTurn ? "대기 중" : "턴 종료";
        RefreshPlayerTabs();
        SyncHand();

        // 기록: 마지막 28줄
        int start = Mathf.Max(0, battle.Log.Count - 28);
        logText.text = string.Join("\n", battle.Log.GetRange(start, battle.Log.Count - start));

        if (battle.Result != BattleResult.Ongoing)
        {
            bool boss = run.PendingIsBoss;
            bool lastBoss = (battle.Result == BattleResult.Victory) && boss && run.Stage == GameBalance.MaxStage;
            // 런이 끝났으면 저장을 지움 (3-73): 진 판을 이어하기로 되살릴 수 없게
            if (battle.Result == BattleResult.Defeat || lastBoss) SaveManager.Delete();
            if (battle.Result == BattleResult.Defeat)
            {
                resultText.text = "패배...";
                resultSubText.text = "런 종료: " + run.Stage + "스테이지 " + run.ProgressText + " (" + run.BattlesWon + "승)";
                resultButtonLabel.text = "새 런";
                SetResultButtonArt(run.Stage);   // 3-157
                ShowVictoryFrame(false);
                SoundManager.Play("Defeat");   // 3-190
            }
            else if (lastBoss)
            {
                resultText.text = "런 클리어!";
                resultSubText.text = "마지막 보스를 쓰러뜨렸습니다 (" + (run.BattlesWon + 1) + "승)";
                resultButtonLabel.text = "엔딩 보기";   // 3-112
                SetResultButtonArt(-1);
                ShowVictoryFrame(false);
                SoundManager.Play("Victory");   // 3-190
            }
            else
            {
                resultText.text = "승리!  (" + battle.Round + "턴)";
                string hp = "";
                foreach (PlayerCombatant pl in battle.Players)
                {
                    hp += pl.Name + " HP " + Mathf.Max(pl.Hp, GameBalance.ReviveHp) + "/" + pl.MaxHp + "   ";
                }
                string next;
                bool toFinal = boss && run.Stage + 1 >= GameBalance.MaxStage;   // 요새 문의 수문장을 쓰러뜨림
                if (run.PendingGivesMap) next = "성을 지켜 냈다! 수문장이 적의 요새로 가는 지도를 떨어뜨렸다.  다음: " + (run.Stage + 1) + "스테이지, 반격";
                else if (toFinal) next = "수문장이 쓰러지고 요새의 문이 열렸다!  다음: 최종장";
                else if (boss) next = "다음: " + (run.Stage + 1) + "스테이지";
                else next = "HP는 다음 칸으로 이어집니다";
                resultSubText.text = hp + "\n" + next;
                resultButtonLabel.text = run.PendingGivesMap ? "반격 개시" : (toFinal ? "성문 안으로" : (boss ? "다음 스테이지" : (run.IsSiege ? "다음 칸으로" : "지도로")));
                SetResultButtonArt(-1);   // 3-157: 승리 버튼은 예전 모양
                ShowVictoryFrame(true);   // 3-181: 승리 창 그림 (그림이 없으면 예전 모양)
                SoundManager.Play("Victory");   // 3-190
            }
            resultPanel.SetActive(true);
        }
    }

    void RefreshUnit(UnitView v, bool isEnemy)
    {
        Combatant u = v.Unit;

        // 숫자 팝업: 지난번보다 HP나 방어도가 바뀌었으면 떠오르는 숫자
        int hpDiff = u.Hp - v.LastHp;
        int blockDiff = u.Block - v.LastBlock;
        PlayUnitEffects(v, isEnemy, hpDiff, blockDiff);
        v.LastHp = u.Hp;
        v.LastBlock = u.Block;
        // 3-186 상태 이펙트: 약화·취약·손상 / 발화 / 동결·동상 / 힘 이 늘어났으면
        int deb, brn, frz, str;
        ReadStatus(u, out deb, out brn, out frz, out str);
        if (!u.IsDead)
        {
            Vector2 sb = UnitPos(v, 0.55f);
            if (deb > v.LastDebuff) { Vfx.Status(popupLayer, "Fx_Debuff", sb, v.ArtHeight); SoundManager.Play("Debuff"); }
            if (brn > v.LastBurn) { Vfx.Status(popupLayer, "Fx_Burn", UnitPos(v, 0.35f), v.ArtHeight); SoundManager.Play("Burn", "Fire"); }
            if (frz > v.LastFreeze) { Vfx.Status(popupLayer, "Fx_Freeze", UnitPos(v, 0.25f), v.ArtHeight); SoundManager.Play("Freeze", "Ice"); }
            if (str > v.LastStrength) { Vfx.Status(popupLayer, "Fx_PowerUp", sb, v.ArtHeight); SoundManager.Play("PowerUp"); }
        }
        v.LastDebuff = deb; v.LastBurn = brn; v.LastFreeze = frz; v.LastStrength = str;
        v.Motion.Dead = u.IsDead;

        float ratio = u.MaxHp > 0 ? (float)u.Hp / u.MaxHp : 0f;
        if (v.HpFill.type == Image.Type.Filled) v.HpFill.fillAmount = Mathf.Clamp01(ratio);   // 그림 체력바
        else v.HpFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
        v.HpText.text = u.Hp + " / " + u.MaxHp;
        v.BlockText.text = u.Block.ToString();
        v.BlockText.transform.parent.gameObject.SetActive(u.Block > 0);   // 방어도 0이면 방패 숨김
        // 3-195: 방어도가 있으면 체력바 틀이 푸르게 (슬더스처럼 한눈에 "방어 중" 이 보이게)
        if (v.HpFrame != null) v.HpFrame.color = u.Block > 0 ? UIFactory.Hex("#8FC8FF") : Color.white;

        if (isEnemy)
        {
            EnemyCombatant e = (EnemyCombatant)u;
            v.NameText.text = e.Name + (e.IsEnraged ? " ★" : "") + (e.RageStacks > 0 && !e.IsDead ? " (분노)" : "") + battle.EnemyBadge(e);
            string intent = "-";
            if (!e.IsDead)
            {
                intent = e.IntentText();
                if (battle.Players.Count > 1 && e.Intent.Damage > 0) intent += " (전원)";
            }
            v.IntentText.text = intent;
            string key = e.IsDead ? "dead" : e.Intent.Name + "/" + e.Intent.Damage + "/" + e.Intent.Block + "/" + e.Intent.PlayerVulnerable + "/" + e.Intent.PlayerWeak + "/" + e.Intent.AllyBlock + "/" + e.Intent.Strength + "/" + e.Intent.Heal + "/" + EnemyCombatant.IsDebuffMove(e.Intent) + "/" + e.Intent.GainThorns + "/" + e.Intent.GainRitual + "/" + e.Strength;
            if (key != v.IntentKey)
            {
                v.IntentKey = key;
                BuildIntentIcon(v.IntentIcon, e.IsDead ? null : e.Intent);
            }
            SetStatusIcons(v, StatusIcons.ForEnemy(e, battle.Players));   // 상태·스택·표식은 전부 아이콘으로
            v.InfoText.text = "";
            v.Panel.color = (e == arrowTarget && !e.IsDead) ? new Color(1f, 0.55f, 0.15f, 0.55f) : new Color(1f, 0.55f, 0.15f, 0f);
            if (e.IsDead) v.IntentText.text = e.ReviveTimer > 0 ? "부활까지 " + e.ReviveTimer + "턴" : "격파";
            SetFigureAlpha(v, e.IsDead ? 0.12f : v.IntroAlpha);   // 3-200: 등장 연출 중이면 그 투명도
        }
        else
        {
            PlayerCombatant pl = (PlayerCombatant)u;
            // 3-169: "P1" 은 색 없이 흰색 그대로, 캐릭터 이름만 그 캐릭터 고유 색 (예: "P1 <color=#6CC8FF>제피르</color>")
            v.NameText.text = pl.Name + " " + ClassLibrary.ColoredClassName(pl.Class) + (pl.HasEndedTurn && !pl.IsDead && battle.Result == BattleResult.Ongoing ? " (턴 종료)" : "");
            SetStatusIcons(v, StatusIcons.ForPlayer(pl));   // 취약·약화·발화·밤·차징 … 은 아이콘
            v.InfoText.text = EffectText(pl);                // 켜 둔 파워 이름은 글자로
            v.Motion.Charging = pl.PendingHalfMoon > 0 && !pl.IsDead && battle.Result == BattleResult.Ongoing;   // 월광섬 차징 중
            v.Motion.NightAura = pl.IsNight && battle.Result == BattleResult.Ongoing;                         // 죽음의 밤: 붉은 기운
            v.Motion.Guarding = pl.Block > 0 && !pl.IsDead && battle.Result == BattleResult.Ongoing;              // 방어도가 있으면 수비 자세 (그림이 있는 캐릭터만)
            bool isActive = (pl == Me);   // 지금 조작 중인 플레이어
            v.NameText.color = Color.white;   // 3-169: "P1" 글자는 항상 흰색 (내 차례 표시는 아래 파란 판 Panel 이 함)
            v.Panel.color = isActive ? new Color(0.4f, 0.8f, 1f, 0.35f) : new Color(0.4f, 0.8f, 1f, 0f);
            SetFigureAlpha(v, pl.IsDead ? 0.25f : 1f);
        }
    }

    // 상태 아이콘 줄: 내용이 바뀌었을 때만 다시 그림 (매 프레임 새로 만들면 마우스 올림이 끊김)
    void SetStatusIcons(UnitView v, List<StatusBadge> badges)
    {
        string key = StatusIcons.Key(badges);
        if (key == v.StatusKey) return;
        v.StatusKey = key;
        StatusIcons.Draw(v.StatusRow, badges, ShowTip);
        // 아이콘이 2줄이면 아래 글자를 조금 내림
        float rows = Mathf.Max(1, Mathf.CeilToInt(badges.Count / 7f));
        v.InfoText.rectTransform.anchoredPosition = new Vector2(0, -56 - rows * (StatusIcons.Size + StatusIcons.Gap) - 4);
    }

    // ---------------- 말풍선 (아이콘·아티팩트에 마우스를 올리면) ----------------
    private Image tipBox;
    private Text tipText;

    // 3-120: 직업 자원 아이콘 (Resources/UI/Res_이름.png). 한 번 읽으면 기억 (자바의 HashMap 캐시와 같음)
    static readonly Dictionary<PlayerClass, Sprite> resSprites = new Dictionary<PlayerClass, Sprite>();
    static Sprite ResourceSprite(PlayerClass c)
    {
        Sprite s;
        if (resSprites.TryGetValue(c, out s)) return s;
        string name = c == PlayerClass.BlackScythe ? "Soul" : c == PlayerClass.CosmicPhoenix ? "Flame" : c == PlayerClass.FrostMage ? "Mana" : c == PlayerClass.Sniper ? "Bullet" : "";
        s = name.Length > 0 ? Resources.Load<Sprite>("UI/Res_" + name) : null;
        resSprites[c] = s;
        return s;
    }

    public void ShowTip(string text, RectTransform anchor)
    {
        if (tipBox == null)
        {
            tipBox = UIFactory.MakeImage("TipBox", root, new Color(0.06f, 0.05f, 0.08f, 0.95f));
            tipBox.raycastTarget = false;
            tipBox.gameObject.AddComponent<Outline>().effectColor = UIFactory.Hex("#C8A05A");
            tipText = UIFactory.MakeText("TipText", tipBox.transform, "", 20, UIFactory.Hex("#F0E8D8"), TextAnchor.UpperLeft);
            UIFactory.Stretch(tipText.rectTransform, 14, 14, 10, 10);
            tipText.raycastTarget = false;
        }
        if (string.IsNullOrEmpty(text) || anchor == null) { tipBox.gameObject.SetActive(false); return; }
        tipBox.gameObject.SetActive(true);
        tipBox.transform.SetAsLastSibling();
        tipText.text = text;
        // 3-154: 상자 크기를 실제 글 크기에 맞춤
        //   예전: 줄 수(\n 개수) × 27 → 긴 줄이 자동 줄바꿈되면 그 줄은 안 세서 글이 상자 밖으로 넘쳤음 (부서진 초승달 펜던트 설명)
        //   지금: 폭을 먼저 정하고(짧은 글은 좁게, 길면 최대 TipMaxW), 그 폭에서 줄바꿈된 진짜 높이(preferredHeight)를 씀
        //   자바 Swing 으로 치면 JTextArea 의 getPreferredSize() 로 크기를 잡는 것
        const float TipMaxW = 440f, TipPadX = 14f, TipPadY = 10f;
        float oneLineW = tipText.preferredWidth;                         // 줄바꿈 없이 한 줄로 썼을 때 폭 (\n 은 그대로 줄바꿈)
        float w = Mathf.Clamp(oneLineW + TipPadX * 2 + 4f, 160f, TipMaxW);
        tipBox.rectTransform.sizeDelta = new Vector2(w, 100f);           // 폭을 먼저 정해야 그 폭으로 줄바꿈한 높이가 나옴
        float textH = tipText.cachedTextGeneratorForLayout.GetPreferredHeight(text, tipText.GetGenerationSettings(new Vector2(w - TipPadX * 2, 0f))) / tipText.pixelsPerUnit;
        Vector2 size = new Vector2(w, textH + TipPadY * 2 + 6f);
        // 아이콘 위치(화면 좌표) → 캔버스 좌표. 아이콘 바로 위에, 화면 밖으로 안 나가게
        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, RectTransformUtility.WorldToScreenPoint(null, anchor.position), null, out local);
        Vector2 half = root.rect.size * 0.5f;
        float x = Mathf.Clamp(local.x, -half.x + size.x / 2 + 10, half.x - size.x / 2 - 10);
        float y = Mathf.Min(local.y + 30 + size.y / 2, half.y - size.y / 2 - 70);
        y = Mathf.Max(y, -half.y + size.y / 2 + 10);   // 3-154: 아래로도 화면 밖으로 안 나가게
        UIFactory.Place(tipBox.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, y), size);
    }

    // 3-149: 새 체력바 크기와 그림 속 홈 자리 (UI/HpBar_Frame.png 308×54 픽셀: 홈 = 왼쪽 42, 오른쪽 42, 위 16, 아래 14 안쪽)
    const float HpBarW = 280f, HpBarH = 46f;
    const float HpBarArtH = 54f, HpBarInL = 42f, HpBarInR = 42f, HpBarInT = 16f, HpBarInB = 14f;

    // 3-148: 의도 상자 높이 52 → 60 (글자가 2~3줄이 돼도 들어가게)
    const float IntentBoxH = 60f;
    const float IntentIconSize = 44f, IntentIconStep = 30f;   // 그림 아이콘 크기 / 여러 개일 때 옆으로 띄우는 거리 (조금 겹침)

    // 의도 그림: 공격 = 빨간 칼, 방어 = 파란 방패, 취약 = 보라 물방울 (여러 개면 겹쳐서 작게)
    void BuildIntentIcon(RectTransform holder, EnemyMove move)
    {
        for (int i = holder.childCount - 1; i >= 0; i--) Destroy(holder.GetChild(i).gameObject);
        if (move == null) return;

        List<string> kinds = new List<string>();
        if (move.Damage > 0) kinds.Add("attack");
        if (move.Block > 0 || move.AllyBlock > 0) kinds.Add("block");
        if (EnemyCombatant.IsDebuffMove(move)) kinds.Add("debuff");   // 취약·약화·손상·발화·상처·카드 덜 뽑기
        // 3-149: 강화(힘·의식·가시·동료 힘) 와 회복을 나눔. 동료 힘(AllyStrength)만 하는 행동은 예전엔 아이콘이 안 나왔음
        bool artIcons = Resources.Load<Sprite>("Icons/Intent_Attack") != null;
        if (move.Strength > 0 || move.GainThorns > 0 || move.GainRitual > 0 || move.AllyStrength > 0) kinds.Add("buff");
        if (move.Heal > 0)
        {
            if (artIcons && Resources.Load<Sprite>("Icons/Intent_Heal") != null) kinds.Add("heal");
            else if (!kinds.Contains("buff")) kinds.Add("buff");   // 회복 그림이 없으면 예전처럼 강화 아이콘
        }

        // 3-148: 그림 아이콘 (Icons/Intent_Attack · Intent_Block · Intent_Debuff) 이 있으면 왼쪽부터 나란히 → 글자는 아이콘 오른쪽부터
        //   3-149: 강화 = Intent_Buff (주먹), 회복 = Intent_Heal (날개). 그림이 없으면 초록 화살표
        Sprite atk = Resources.Load<Sprite>("Icons/Intent_Attack");
        if (atk != null)
        {
            float width = IntentIconSize + Mathf.Max(0, kinds.Count - 1) * IntentIconStep;
            UIFactory.Place(holder, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6, 0), new Vector2(width, IntentIconSize));
            Text text = holder.parent.Find("IntentText") != null ? holder.parent.Find("IntentText").GetComponent<Text>() : null;
            if (text != null) text.rectTransform.offsetMin = new Vector2(kinds.Count > 0 ? 6 + width + 4 : 10, text.rectTransform.offsetMin.y);
            for (int k = 0; k < kinds.Count; k++)
            {
                Sprite sp = Resources.Load<Sprite>("Icons/Intent_" + IntentIconName(kinds[k]));
                RectTransform icon = UIFactory.MakeRect(kinds[k], holder);
                UIFactory.Place(icon, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(k * IntentIconStep, 0), new Vector2(IntentIconSize, IntentIconSize));
                if (sp != null)
                {
                    Image img = UIFactory.MakeImage("Art", icon, Color.white, false);
                    img.sprite = sp;
                    img.preserveAspect = true;
                    img.raycastTarget = false;
                    UIFactory.Stretch(img.rectTransform);
                }
                else DrawBuffArrow(icon);
            }
            return;
        }

        float scale = kinds.Count > 1 ? 0.7f : 1f;
        for (int k = 0; k < kinds.Count; k++)
        {
            RectTransform icon = UIFactory.MakeRect(kinds[k], holder);
            float x = (k - (kinds.Count - 1) / 2f) * 20f;   // 여러 개면 옆으로 조금씩 겹침
            UIFactory.Place(icon, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0), new Vector2(44, 44));
            icon.localScale = new Vector3(scale, scale, 1f);

            if (kinds[k] == "attack")
            {
                Color c = UIFactory.Hex("#FF5A4A");
                Image blade = UIFactory.MakeImage("Blade", icon, c);
                UIFactory.Place(blade.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(4, 4), new Vector2(9, 40));
                blade.rectTransform.localEulerAngles = new Vector3(0, 0, -45f);
                Image guard = UIFactory.MakeImage("Guard", icon, UIFactory.Hex("#E0C080"));
                UIFactory.Place(guard.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-8, -8), new Vector2(22, 6));
                guard.rectTransform.localEulerAngles = new Vector3(0, 0, -45f);
                foreach (Image img in icon.GetComponentsInChildren<Image>()) img.raycastTarget = false;
            }
            else if (kinds[k] == "block")
            {
                Color c = UIFactory.Hex("#5AA0FF");
                Image top = UIFactory.MakeImage("ShieldTop", icon, c);
                UIFactory.Place(top.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 6), new Vector2(30, 22));
                Image tip = UIFactory.MakeImage("ShieldTip", icon, c);
                UIFactory.Place(tip.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -6), new Vector2(21, 21));
                tip.rectTransform.localEulerAngles = new Vector3(0, 0, 45f);
                foreach (Image img in icon.GetComponentsInChildren<Image>()) img.raycastTarget = false;
            }
            else if (kinds[k] == "buff")
            {
                // 강화(힘·회복): 초록 위쪽 화살표
                Color c = UIFactory.Hex("#6AE07A");
                Image stem = UIFactory.MakeImage("Stem", icon, c);
                UIFactory.Place(stem.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -6), new Vector2(10, 24));
                Image head = UIFactory.MakeImage("Head", icon, c);
                UIFactory.Place(head.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 8), new Vector2(20, 20));
                head.rectTransform.localEulerAngles = new Vector3(0, 0, 45f);
                stem.raycastTarget = false;
                head.raycastTarget = false;
            }
            else
            {
                Image drop = UIFactory.MakeCircle("Drop", icon, UIFactory.Hex("#B060FF"));
                UIFactory.Place(drop.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -4), new Vector2(26, 26));
                Image tip = UIFactory.MakeImage("DropTip", icon, UIFactory.Hex("#B060FF"));
                UIFactory.Place(tip.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 7), new Vector2(16, 16));
                tip.rectTransform.localEulerAngles = new Vector3(0, 0, 45f);
                drop.raycastTarget = false;
                tip.raycastTarget = false;
            }
        }
    }

    // 3-149: 의도 종류 → 그림 이름 (Icons/Intent_이름)
    static string IntentIconName(string kind)
    {
        switch (kind)
        {
            case "attack": return "Attack";
            case "block": return "Block";
            case "debuff": return "Debuff";
            case "heal": return "Heal";
            default: return "Buff";
        }
    }

    // 강화(힘·회복) 아이콘: 초록 위쪽 화살표 (3-148: 그림 아이콘을 쓸 때도 이건 그려서 씀)
    static void DrawBuffArrow(RectTransform icon)
    {
        Color c = UIFactory.Hex("#6AE07A");
        Image stem = UIFactory.MakeImage("Stem", icon, c);
        UIFactory.Place(stem.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -6), new Vector2(10, 24));
        Image head = UIFactory.MakeImage("Head", icon, c);
        UIFactory.Place(head.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 8), new Vector2(20, 20));
        head.rectTransform.localEulerAngles = new Vector3(0, 0, 45f);
        stem.raycastTarget = false;
        head.raycastTarget = false;
    }

    // P1~P4 탭: 보고 있는 사람 = 금색, 이번 턴 끝냄 = "끝" + 어둡게, 쓰러짐. HP 막대 길이 맞추기
    void RefreshPlayerTabs()
    {
        if (playerTabs == null || run == null) return;
        // 3-91: 정보 판을 접으면 P1~P4 탭도 숨김 (보상 화면 위쪽 탭 등 다른 화면을 가리지 않게). 펼치면 다시 보임
        playerTabs.gameObject.SetActive(!RelicBar.Collapsed);
        bool inBattle = battle != null && battle.Result == BattleResult.Ongoing;
        for (int i = 0; i < playerTabs.childCount && i < run.Players.Count; i++)
        {
            Transform tab = playerTabs.GetChild(i);
            Image img = tab.GetComponent<Image>();
            PlayerCombatant pl = run.Players[i];
            bool ended = inBattle && !pl.IsDead && pl.HasEndedTurn;
            string mark = pl.IsDead ? " <color=#FF6060><size=16>쓰러짐</size></color>" : (ended ? " <color=#7CFF7C><size=16>끝</size></color>" : "");   // (✓ 같은 기호는 글꼴에 없을 수 있어서 글자로)
            string block = inBattle && pl.Block > 0 ? " <color=#8AC8FF><size=16>방" + pl.Block + "</size></color>" : "";
            Text label = tab.GetComponentInChildren<Text>();
            if (img == null)
            {
                // 3-148: 그림 칸 - 고른 사람 = 금빛 + 밝게, 끝냄·쓰러짐 = 어둡게. 상태는 P1 아래 줄에
                Transform glow = tab.Find("Glow");
                if (glow != null) glow.gameObject.SetActive(i == controlIndex);
                Image frame = tab.Find("Frame").GetComponent<Image>();
                frame.color = (pl.IsDead || ended) ? new Color(0.45f, 0.45f, 0.45f, 1f) : (i == controlIndex ? Color.white : new Color(0.82f, 0.82f, 0.82f, 1f));
                string state = (pl.IsDead ? "<color=#FF6060>쓰러짐</color>" : (ended ? "<color=#7CFF7C>끝</color>" : "")) + (block.Length > 0 ? (pl.IsDead || ended ? " " : "") + "<color=#8AC8FF>방" + pl.Block + "</color>" : "");
                if (label != null) label.text = pl.Name + (state.Length > 0 ? "\n<size=15>" + state + "</size>" : "");
            }
            else
            {
                if (i == controlIndex) img.color = UIFactory.Hex("#9A7A2A");
                else if (pl.IsDead || ended) img.color = UIFactory.Hex("#1A1C24");
                else img.color = UIFactory.Hex("#2C3140");
                if (label != null) label.text = pl.Name + mark + block;
            }
            Transform back = tab.Find("HpBack");
            if (back != null)
            {
                RectTransform fill = (RectTransform)back.Find("HpFill");
                float r = pl.MaxHp > 0 ? Mathf.Clamp01((float)pl.Hp / pl.MaxHp) : 0f;
                if (fill != null) fill.anchorMax = new Vector2(r, 1f);
            }
        }
    }

    // HP·방어도가 바뀐 만큼 숫자, 이펙트, 흔들림을 보여 줌
    void PlayUnitEffects(UnitView v, bool isEnemy, int hpDiff, int blockDiff)
    {
        Vector2 center = UnitPos(v, 0.5f);   // 몸통 가운데 (popupLayer 좌표)

        if (hpDiff < 0)
        {
            int dmg = -hpDiff;
            float power = Mathf.Clamp(dmg / 10f, 0.6f, 2.2f);   // 피해가 클수록 크게
            // 3-182 연출 1단계: 큰 피해(20 이상)는 금빛 숫자로 더 크게 튀어나옴
            bool heavy = dmg >= GameBalance.FxHeavyHit;
            Color numColor = isEnemy ? (heavy ? UIFactory.Hex("#FFD24A") : Color.white) : UIFactory.Hex("#FF5A5A");
            Popup(UnitPos(v, 0.8f), dmg.ToString(), numColor, 1.4f + power * 0.4f + (heavy ? 0.5f : 0f), 46 + Mathf.Min(dmg, 40) + (heavy ? 10 : 0));
            v.Motion.Hit(power, UIFactory.Hex("#FF7070"));
            Vfx.Sparks(popupLayer, center, isEnemy ? UIFactory.Hex("#FFE08A") : UIFactory.Hex("#FF6060"), 6 + Mathf.Min(dmg / 2, 10));
            if (isEnemy) Vfx.Slash(popupLayer, center, slashColor);
            if (!isEnemy || dmg >= 15) Shake(0.25f, isEnemy ? 8f + dmg * 0.3f : 10f + dmg * 0.4f);
            PlayImpact(v, isEnemy, dmg, center);   // 3-182: 히트스톱·번쩍임·충격 고리·처치 연출
            // 3-190 소리: 적이 맞음 = Hit (큰 피해 HitHeavy) / 플레이어가 맞음 = PlayerHit / 쓰러짐 = Kill
            if (v.Unit != null && v.Unit.IsDead) SoundManager.Play(isEnemy ? "Kill" : "PlayerDown", "Kill", "HitHeavy", "Hit");
            else if (isEnemy) SoundManager.Play(heavy ? "HitHeavy" : "Hit", "Hit");
            else SoundManager.Play("PlayerHit", "Hit");
            if (!isEnemy) PlayEnemyStyleOn(v);      // 3-186: 몬스터 공격 그림 (둔기·화살·할퀴기·어둠·독·불)
        }
        else if (blockDiff < 0 && v.LastBlock > 0)
        {
            if (!isEnemy) v.Motion.Defend();   // 방어도로 막아 냄 → 수비 자세
            // 방어도가 다 막음: 파란 불똥 + 작은 흔들림
            Popup(UnitPos(v, 0.8f), "막음", UIFactory.Hex("#8FC8FF"), 1.3f, 34);
            SoundManager.Play("BlockHit", "Block");   // 3-190 방어도로 막음 (쇠 부딪히는 소리)
            if (!isEnemy) PlayEnemyStyleOn(v);   // 3-186
            v.Motion.Hit(0.5f, UIFactory.Hex("#8FC8FF"));
            Vfx.Sparks(popupLayer, center, UIFactory.Hex("#8FC8FF"), 6);
            if (isEnemy) Vfx.Slash(popupLayer, center, slashColor);
        }

        if (hpDiff > 0 && v.LastHp <= 0)
            Vfx.Big(popupLayer, "Fx_Revive", UnitPos(v, 0.45f), v.ArtHeight * 1.2f, 1.2f);   // 3-188: 쓰러졌다가 일어남 (동료의 휘장 등)
        if (hpDiff > 0)
        {
            Popup(UnitPos(v, 0.8f), "+" + hpDiff, UIFactory.Hex("#7CFF8A"), 1.4f, 40);
            Vfx.Status(popupLayer, "Fx_Heal", UnitPos(v, 0.45f), v.ArtHeight);   // 3-186 회복 그림
            SoundManager.Play("Heal");   // 3-190
            Vfx.Rise(popupLayer, center, UIFactory.Hex("#7CFF8A"), 10);
        }
        if (blockDiff > 0)
        {
            if (!isEnemy) v.Motion.Defend();   // 방어도를 얻음 → 수비 자세
            Popup(UnitPos(v, 0.8f), "방어도 +" + blockDiff, UIFactory.Hex("#8FC8FF"), 1.3f, 34);
            SoundManager.Play("Block");   // 3-190
            Vfx.Ring(popupLayer, center, UIFactory.Hex("#8FC8FF"));
            // 3-184: 그림 방패 (이그니스 = 불꽃 고리, 나머지 플레이어 = 푸른 방패 구)
            // 3-200: 몬스터도 방패. 일반·정예·엘리트 = 플레이어와 같은 푸른 방패 구 / 보스 = 붉은 룬 방어막 (발밑에서 덮음)
            if (!isEnemy)
            {
                PlayerCombatant bp = v.Unit as PlayerCombatant;
                Vfx.Guard(popupLayer, center, bp != null && bp.Class == PlayerClass.CosmicPhoenix, v.ArtHeight);
            }
            else
            {
                EnemyCombatant be = v.Unit as EnemyCombatant;
                if (be != null && be.Rank == EnemyRank.Boss)
                {
                    float sh = v.ArtHeight * 0.95f;   // 방어막 그림 높이 (가로는 그림 비율대로 약 1.5배)
                    VfxPart dome = Vfx.Art(popupLayer, "Fx_BossShield", UnitPos(v, 0f) + new Vector2(0f, sh * 0.47f), sh, 0f, 0.8f, 0.7f, 1f, 1.03f, 0.25f, 0.55f);
                    if (dome == null) Vfx.Guard(popupLayer, center, false, v.ArtHeight);   // 그림이 없으면 보통 방패
                }
                else Vfx.Guard(popupLayer, center, false, v.ArtHeight);
            }
        }
    }

    // 캐릭터 발밑에서 그림 높이 × heightRatio 만큼 위 (popupLayer 좌표)
    Vector2 UnitPos(UnitView v, float heightRatio)
    {
        Vector3 p = popupLayer.InverseTransformPoint(v.Rt.position);
        return new Vector2(p.x, p.y + v.ArtHeight * heightRatio);
    }

    UnitView ViewOf(Combatant unit)
    {
        foreach (UnitView v in playerViews) if (v.Unit == unit) return v;
        foreach (UnitView v in enemyViews) if (v.Unit == unit) return v;
        return null;
    }

    // ==================== 3-182 연출 1단계: 타격감 ====================
    // 맞는 순간을 "묵직하게" 느끼게 하는 4가지
    //   1) 히트스톱: 아주 잠깐(0.04~0.12초) 게임 전체를 거의 멈춤 → 칼이 "박히는" 느낌 (격투 게임에서 많이 씀)
    //   2) 하얀 번쩍임: 큰 피해일 때 화면 전체가 살짝 하얗게
    //   3) 충격 고리: 맞은 자리에서 하얀 고리가 퍼짐
    //   4) 처치: 적이 쓰러지는 순간 더 긴 멈춤 + 큰 흔들림 + 검은 파편·불똥이 터짐
    // 숫자(몇 이상이면 큰 피해 등)는 GameBalance 의 Fx… 값
    private bool hitStopping;

    void PlayImpact(UnitView v, bool isEnemy, int dmg, Vector2 center)
    {
        bool killed = v.Unit != null && v.Unit.IsDead;
        float stop = 0f;
        if (killed && isEnemy) stop = GameBalance.FxHitStopKill;
        else if (dmg >= GameBalance.FxHeavyHit) stop = GameBalance.FxHitStopHeavy;
        else if (dmg >= GameBalance.FxMediumHit) stop = GameBalance.FxHitStopMedium;
        if (stop > 0f) StartCoroutine(HitStop(stop));

        if (dmg >= GameBalance.FxMediumHit)
        {
            Vfx.Ring(popupLayer, center, new Color(1f, 1f, 1f, 0.75f));   // 충격 고리
            Vfx.Burst(popupLayer, center, 200f + Mathf.Min(dmg, 40) * 3f, Color.white);   // 3-186: 빛 터짐 5장 프레임
        }
        if (dmg >= GameBalance.FxHeavyHit)
        {
            Color fc = flash.color;
            if (fc.a < 0.22f) flash.color = new Color(1f, 0.97f, 0.9f, 0.22f);   // 하얀 번쩍임 (Update 가 알아서 서서히 사라지게 함)
        }
        if (killed && isEnemy)
        {
            Vfx.DeathSmoke(popupLayer, UnitPos(v, 0.45f), v.ArtHeight);   // 3-184: 보라·검은 연기
            Shake(0.35f, 18f);
            Vfx.Shards(popupLayer, center, UIFactory.Hex("#0C0612"), 16, 420f, 180f);   // 검은 파편
            Vfx.Sparks(popupLayer, center, UIFactory.Hex("#FFC86A"), 18);               // 불똥
            Vfx.Ring(popupLayer, center, slashColor);
        }
    }

    // ==================== 3-186: 상태 숫자 읽기 · 몬스터 공격 이펙트 ====================
    // 약화+취약+손상 (턴 수 합) / 발화 스택 / 동결 스택+동상 / 힘 (몬스터만)
    static void ReadStatus(Combatant u, out int debuff, out int burn, out int freeze, out int strength)
    {
        debuff = u.GetStatus(StatusType.Weak) + u.GetStatus(StatusType.Vulnerable) + u.GetStatus(StatusType.Frail);
        burn = u.GetStatus(StatusType.Burn);
        freeze = u.GetStatus(StatusType.Frostbite);
        strength = 0;
        EnemyCombatant e = u as EnemyCombatant;
        if (e != null) { freeze += e.FreezeStacks; strength = e.Strength; }
    }

    // 적 턴에 공격한 몬스터들의 공격 종류. 플레이어가 맞은 순서대로 하나씩 꺼내 씀 (누가 누구를 쳤는지는 화면이 몰라서 차례대로)
    private List<Vfx.EnemyStyle> enemyStyles = new List<Vfx.EnemyStyle>();
    private int enemyStyleIndex;

    // ==================== 3-188: 필살기 카드 큰 이펙트 ====================
    // 카드 아이디로 정함. 공격·스킬·파워 모두 (적이 안 맞아도 나옴)
    //   적 쪽 가운데 = 살아 있는 적들의 가운데, 대상 = 맞은 적 (없으면 화살표로 고른 적)
    void PlayCardSpecialFx(PlayerCombatant p, CardData card, List<EnemyCombatant> hit, EnemyCombatant target)
    {
        UnitView pv = ViewOf(p);
        List<UnitView> tvs = new List<UnitView>();
        foreach (EnemyCombatant en in hit) { UnitView tv = ViewOf(en); if (tv != null) tvs.Add(tv); }
        if (tvs.Count == 0 && target != null) { UnitView tv = ViewOf(target); if (tv != null) tvs.Add(tv); }
        Vector2 enemyMid = Vector2.zero;
        int n = 0;
        foreach (UnitView ev in enemyViews) if (ev.Unit != null && !ev.Unit.IsDead) { enemyMid += UnitPos(ev, 0.5f); n++; }
        if (n > 0) enemyMid /= n; else enemyMid = new Vector2(400f, 0f);
        Vector2 me = pv != null ? UnitPos(pv, 0.5f) : new Vector2(-400f, 0f);

        switch (card.Id)
        {
            // 카인
            case "death_night": case "red_moon": case "full_moon":
                Vfx.Big(popupLayer, "Fx_BloodMoon", new Vector2((me.x + enemyMid.x) * 0.5f, me.y + 330f), 300f, 1.4f);   // 하늘에 붉은 달
                break;
            case "soul_harvest": case "absorb": case "soul_exploit":
                Vfx.Big(popupLayer, "Fx_SoulVortex", me, 300f, 1.0f, -200f);   // 영혼이 몸으로 빨려 들어옴 (돌기)
                break;
            case "death_sentence": case "death_mark":
                foreach (UnitView tv in tvs) Vfx.Big(popupLayer, "Fx_SkullSigil", UnitPos(tv, 0.55f), 230f, 0.9f, 60f);
                break;
            // 이그니스
            case "phoenix_nova":
                Vfx.Big(popupLayer, "Fx_PhoenixNova", enemyMid + new Vector2(0, 40f), 420f, 1.1f);
                Shake(0.4f, 20f);
                break;
            case "meteor":
                foreach (UnitView tv in tvs) Vfx.Meteor(popupLayer, UnitPos(tv, 0.45f));
                if (tvs.Count == 0) Vfx.Meteor(popupLayer, enemyMid);
                break;
            case "sunspot": case "sun_wrath":
                foreach (UnitView tv in tvs) Vfx.Big(popupLayer, "Fx_SunFlare", UnitPos(tv, 0.5f), 260f, 0.8f, 40f);
                if (tvs.Count == 0) Vfx.Big(popupLayer, "Fx_SunFlare", enemyMid, 320f, 0.8f, 40f);
                break;
            // 제피르
            case "absolute_zero": case "blizzard":
                Vfx.Big(popupLayer, "Fx_Blizzard", enemyMid, 420f, 1.1f, -150f);
                break;
            case "chain_lightning":
                Vfx.Big(popupLayer, "Fx_ChainLightning", enemyMid, 230f, 0.5f);
                break;
            case "mana_release": case "limit_break":
                Vfx.Big(popupLayer, "Fx_ManaBurst", me, 360f, 0.9f);
                break;
            // 실비아
            case "sn_headshot": case "sn_doom":
                foreach (UnitView tv in tvs) Vfx.Crosshair(popupLayer, UnitPos(tv, 0.7f));
                break;
            case "sn_grenade":
                foreach (UnitView tv in tvs) Vfx.Art(popupLayer, "Fx_Grenade", UnitPos(tv, 0.45f), 280f, 0f, 0.7f, 0.3f, 1f, 1.15f, 0.25f, 0.45f);
                break;
            case "sn_smoke":
                Vfx.Big(popupLayer, "Fx_SmokeScreen", me + new Vector2(60f, -40f), 300f, 1.4f);
                break;
            // 공용
            case "c_apocalypse":
                foreach (UnitView ev in enemyViews)
                    if (ev.Unit != null) Vfx.Big(popupLayer, "Fx_Apocalypse", UnitPos(ev, 0.45f), 380f, 0.9f);
                flash.color = new Color(1f, 0.95f, 0.8f, 0.4f);
                Shake(0.45f, 22f);
                break;

            // ==================== 3-197: 카드별 이펙트 ====================
            // 카인
            case "dark_night_slash":   // 흑야 참격: 맞은 적마다 보라 달빛 가로 베기
                foreach (UnitView tv in tvs) Vfx.Art(popupLayer, "Fx_NightSlash", UnitPos(tv, 0.5f), 300f, 0f, 0.5f, 0.6f, 1.05f, 1.12f, 0.2f, 0.45f);
                break;
            case "shadow_raid":        // 그림자 습격: 내 자리에서 적 쪽으로 그림자 잔상이 달려감
                foreach (UnitView tv in tvs) Vfx.Fly(popupLayer, "Fx_ShadowDash", me, UnitPos(tv, 0.45f), 200f, 0.28f);
                break;
            case "blood_dance": case "reaper_waltz":   // 3-198 피의 검무·사신의 윤무: 붉은 칼날이 원을 그리며 돎
                foreach (UnitView tv in tvs) Vfx.Big(popupLayer, "Fx_BloodDance", UnitPos(tv, 0.5f), 280f, 0.7f, -360f);
                break;
            // 이그니스
            case "flame_lance":        // 화염의 창: 불꽃 창이 날아가 꽂힘
                foreach (UnitView tv in tvs) FlyAndBurst("Fx_FlameLance", me + new Vector2(80f, 0f), UnitPos(tv, 0.5f), 170f);
                break;
            case "phoenix_pierce":     // 피닉스 피어스: 깃털 꼬리 불새 부리가 꿰뚫음
                foreach (UnitView tv in tvs) FlyAndBurst("Fx_PhoenixPierce", me + new Vector2(80f, 0f), UnitPos(tv, 0.5f), 190f);
                break;
            case "blue_flame":         // 푸른 불꽃: 대상 자리에서 푸른 불길이 확 터짐
                foreach (UnitView tv in tvs) Vfx.Big(popupLayer, "Fx_BlueFlame", UnitPos(tv, 0.45f), 270f, 0.8f);
                if (tvs.Count == 0) Vfx.Big(popupLayer, "Fx_BlueFlame", me, 270f, 0.8f);
                break;
            case "brand":              // 낙인: 대상 몸에 불꽃 문양이 돌며 새겨짐
                foreach (UnitView tv in tvs) Vfx.Big(popupLayer, "Fx_Brand", UnitPos(tv, 0.5f), 220f, 0.9f, 50f);
                break;
            // 제피르
            case "thunderbolt":        // 천둥 벼락: 먹구름에서 큰 벼락이 내리꽂힘
                foreach (UnitView tv in tvs) Vfx.Art(popupLayer, "Fx_Thunderbolt", UnitPos(tv, 0.75f), 440f, 0f, 0.55f, 0.95f, 1f, 1.03f, 0.15f, 0.5f);
                Shake(0.25f, 14f);
                break;
            case "plasma_bolt":        // 플라즈마 볼트: 보라·파란 에너지 구슬이 날아감
                foreach (UnitView tv in tvs) FlyAndBurst("Fx_PlasmaBolt", me + new Vector2(80f, 0f), UnitPos(tv, 0.5f), 170f);
                break;
            case "hail":               // 우박: 적 쪽 하늘에서 얼음덩이가 비스듬히 쏟아짐
            {
                VfxPart h = Vfx.Art(popupLayer, "Fx_Hail", enemyMid + new Vector2(-60f, 120f), 400f, 0f, 0.8f, 0.95f, 1f, 1f, 0.2f, 0.6f);
                if (h != null) h.Velocity = new Vector2(120f, -160f);   // 오른쪽 아래로 떨어지는 느낌
                break;
            }
            case "ice_spike":          // 얼음 송곳: 얼음 창이 날아가 꽂힘
                foreach (UnitView tv in tvs) FlyAndBurst("Fx_IceSpike", me + new Vector2(80f, 0f), UnitPos(tv, 0.5f), 160f);
                break;
            case "glacier_cut":        // 빙하절단: 거대한 얼음 칼날로 베기
                foreach (UnitView tv in tvs) Vfx.Art(popupLayer, "Fx_GlacierCut", UnitPos(tv, 0.5f), 300f, 0f, 0.5f, 0.6f, 1.05f, 1.12f, 0.2f, 0.45f);
                break;
            // 실비아 (총구 = 몸 높이 0.74, 몸 가운데보다 키의 0.88배 앞)
            case "sn_incendiary":      // 소이탄: 맞은 자리에서 불길이 튐
                foreach (UnitView tv in tvs) Vfx.Art(popupLayer, "Fx_Incendiary", UnitPos(tv, 0.45f), 280f, Random.Range(-10f, 10f), 0.7f, 0.3f, 1f, 1.12f, 0.25f, 0.5f);
                break;
            case "sn_spray": case "sn_rapid":   // 난사·속사: 총구에서 탄 줄기 여러 개
                foreach (UnitView tv in tvs) Vfx.Fly(popupLayer, "Fx_Spray", Muzzle(pv, me), UnitPos(tv, 0.5f), 170f, 0.2f);
                break;
            case "sn_pierce":          // 관통탄: 흰 충격파를 두른 탄환
                foreach (UnitView tv in tvs) FlyAndBurst("Fx_PierceShot", Muzzle(pv, me), UnitPos(tv, 0.5f), 130f, 0.2f);
                break;
            case "sn_chain_trap":      // 3-198 사슬 함정: 대상 발밑에서 가시 덫이 철컥 닫힘 (던지는 자세는 원래대로)
                foreach (UnitView tv in tvs) Vfx.Art(popupLayer, "Fx_ChainTrap", UnitPos(tv, 0.25f), 230f, 0f, 0.8f, 1.2f, 0.95f, 1f, 0.15f, 0.6f);
                break;
            case "sn_red_flash":       // 붉은 섬광: 던진 섬광탄이 적마다 붉게 번쩍 (던지는 자세는 아래 사격 쪽에서)
                //   3-198: 전용 그림 Fx_RedFlash (그림이 없으면 예전처럼 빛 터짐을 붉게)
                foreach (UnitView tv in tvs)
                    if (Vfx.Art(popupLayer, "Fx_RedFlash", UnitPos(tv, 0.5f), 300f, 0f, 0.6f, 0.3f, 1f, 1.15f, 0.2f, 0.45f) == null)
                        Vfx.Burst(popupLayer, UnitPos(tv, 0.5f), 260f, UIFactory.Hex("#FF4A3A"));
                flash.color = new Color(1f, 0.25f, 0.2f, 0.3f);
                break;
            // 공용
            case "c_time_warp":        // 시간 왜곡: 내 몸에 황금 시계가 거꾸로 돎
                Vfx.Big(popupLayer, "Fx_TimeWarp", me, 300f, 1.1f, 70f);
                break;
            case "c_transcend":        // 초월: 빛의 날개가 피어오름
            {
                VfxPart tr = Vfx.Big(popupLayer, "Fx_Transcend", me + new Vector2(0f, 30f), 340f, 1.2f);
                if (tr != null) tr.Velocity = new Vector2(0f, 40f);
                break;
            }
            case "c_purify":           // 정화: 흰 빛이 어두운 안개를 씻어 냄
                Vfx.Big(popupLayer, "Fx_Purify", me, 280f, 1.0f);
                break;
            default:
                // 파워 카드 공용: 따로 정한 이펙트가 없는 파워 카드 = 발밑에서 여러 색 기운이 솟음
                if (card.Type == CardType.Power && pv != null)
                    Vfx.Art(popupLayer, "Fx_PowerAwaken", UnitPos(pv, 0.4f), pv.ArtHeight * 1.2f, 0f, 1.0f, 0.5f, 1f, 1.05f, 0.25f, 0.55f);
                break;
        }
    }

    // 3-197: 실비아 총구 자리 (그림이 없으면 몸 가운데)
    Vector2 Muzzle(UnitView pv, Vector2 fallback)
    {
        return pv != null ? UnitPos(pv, 0.74f) + new Vector2(pv.ArtHeight * 0.88f, 0f) : fallback;
    }

    // 3-197: 그림이 날아가서 → 닿을 때 작은 빛 터짐
    void FlyAndBurst(string name, Vector2 from, Vector2 to, float height, float flight = 0.3f)
    {
        Vfx.Fly(popupLayer, name, from, to, height, flight);
        StartCoroutine(BurstLater(to, flight));
    }
    IEnumerator BurstLater(Vector2 pos, float delay)
    {
        yield return new WaitForSeconds(delay);
        Vfx.Burst(popupLayer, pos, 200f, Color.white);
    }

    // 3-197: 자기만의 날아가는 그림·베기가 있는 카드 = 직업 기본 이펙트(초승달·불새·총 빛줄기 등)를 겹쳐 보여 주지 않음
    static bool HasOwnAttackFx(string id)
    {
        switch (id)
        {
            case "dark_night_slash": case "shadow_raid": case "blood_dance": case "reaper_waltz":
            case "flame_lance": case "phoenix_pierce":
            case "thunderbolt": case "plasma_bolt": case "ice_spike": case "glacier_cut":
            case "sn_spray": case "sn_rapid": case "sn_pierce": case "sn_red_flash":
                return true;
        }
        return false;
    }

    // 3-188: 턴 시작 = 살아 있는 플레이어 몸에 푸른 에너지 구슬 (에너지 회복)
    void PlayTurnStartFx()
    {
        SoundManager.Play("TurnStart");   // 3-190
        foreach (UnitView v in playerViews)
            if (v.Unit != null && !v.Unit.IsDead) Vfx.Art(popupLayer, "Fx_EnergyOrb", UnitPos(v, 0.5f), 170f, 0f, 0.7f, 0.4f, 1f, 1.05f, 0.25f, 0.5f);
    }

    // 3-188 보스 등장 → 3-200: 보스·엘리트 등장 연출
    //   ① 전투가 시작되면 보스·엘리트를 바로 숨김 (투명도 0)
    //   ② 0.2초 뒤 발밑에서 붉은 기운이 땅을 뚫고 솟음 (Fx_Emerge, 보스는 크게 + 흔들림)
    //   ③ 0.45초 뒤 몬스터가 0.35초 동안 서서히 나타남. 보스는 이때 붉은 폭발(Fx_BossAura)도
    //   자바로 치면: 코루틴(IEnumerator) = 중간중간 "몇 초 쉬었다가 이어서" 하는 함수. yield return = 여기서 잠깐 멈춤
    IEnumerator BossIntroFx()
    {
        List<UnitView> intro = new List<UnitView>();
        foreach (UnitView v in enemyViews)
        {
            EnemyCombatant e = v.Unit as EnemyCombatant;
            if (e == null || e.IsDead || (e.Rank != EnemyRank.Boss && e.Rank != EnemyRank.Elite)) continue;
            intro.Add(v);
            v.IntroAlpha = 0f;
            SetFigureAlpha(v, 0f);
        }
        if (intro.Count == 0) yield break;

        yield return new WaitForSeconds(GameBalance.FxIntroDelay);
        bool anyBoss = false;
        foreach (UnitView v in intro)
        {
            bool isBoss = ((EnemyCombatant)v.Unit).Rank == EnemyRank.Boss;
            anyBoss |= isBoss;
            float h = v.ArtHeight * (isBoss ? 1.35f : 1.05f);   // 솟는 기운 높이 (보스가 더 큼)
            Vfx.Art(popupLayer, "Fx_Emerge", UnitPos(v, 0f) + new Vector2(0f, h * 0.42f), h, 0f, 1.3f, 0.6f, 1f, 1.08f, 0.3f, 0.6f);
        }
        SoundManager.Play(anyBoss ? "BossAppear" : "EliteAppear", "BossAppear");   // 3-190 (엘리트 소리가 없으면 보스 소리)
        Shake(anyBoss ? 0.5f : 0.3f, anyBoss ? 14f : 8f);

        yield return new WaitForSeconds(GameBalance.FxIntroReveal);
        foreach (UnitView v in intro)
            if (((EnemyCombatant)v.Unit).Rank == EnemyRank.Boss)
                Vfx.Big(popupLayer, "Fx_BossAura", UnitPos(v, 0.45f), v.ArtHeight * 1.1f, 1.3f);
        float t = 0f;
        while (t < GameBalance.FxIntroFade)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / GameBalance.FxIntroFade);
            foreach (UnitView v in intro) { v.IntroAlpha = a; if (!v.Unit.IsDead) SetFigureAlpha(v, a); }
            yield return null;   // 다음 프레임까지 기다림
        }
        foreach (UnitView v in intro) { v.IntroAlpha = 1f; if (!v.Unit.IsDead) SetFigureAlpha(v, 1f); }
    }
    private float drawFxCooldown;   // 카드 뽑기 이펙트가 한꺼번에 여러 번 겹치지 않게

    // ==================== 3-184: 직업별 그림 이펙트 ====================
    // 공격 카드로 적이 맞았을 때만. 맞은 적마다 (여러 명 공격이면 여러 번)
    //   제피르 = 벼락 카드는 마법진 + 번개 / 나머지는 얼음 결정
    //   카인   = 보라 초승달 (필살기·어둠의 지배·월식은 X자)
    //   실비아 = 총구에서 빛줄기 + 폭발 (총검술은 카인처럼 베기 대신 폭발만)
    //   이그니스 = 불새가 날아가 부딪힘
    void PlayClassFx(PlayerCombatant p, CardData card, List<EnemyCombatant> hit)
    {
        if (card.Type != CardType.Attack || hit.Count == 0) return;
        UnitView pv = ViewOf(p);
        string id = card.Id;
        bool big = id == "dark_dominion" || id == "eclipse" || id == "half_moon" || id == "execution";
        // 3-190 소리: 직업 공격 (Slash 낫 / Fire 불 / Lightning·Ice 마법 / Gun 총)
        switch (p.Class)
        {
            case PlayerClass.BlackScythe: SoundManager.Play("Slash", "Hit"); break;
            case PlayerClass.CosmicPhoenix: SoundManager.Play("Fire", "Hit"); break;
            case PlayerClass.FrostMage: SoundManager.Play(card.IsLightning ? "Lightning" : "Ice", "Hit"); break;
            case PlayerClass.Sniper: SoundManager.Play(card.IsBayonet ? "Slash" : "Gun", "Hit"); break;
        }
        if (HasOwnAttackFx(id)) return;   // 3-197: 카드 전용 이펙트가 대신 나옴 (소리는 위에서 이미 냄)
        foreach (EnemyCombatant en in hit)
        {
            UnitView tv = ViewOf(en);
            if (tv == null) continue;
            Vector2 tp = UnitPos(tv, 0.5f);
            switch (p.Class)
            {
                case PlayerClass.FrostMage:
                    if (card.IsLightning) Vfx.ZephyrLightning(popupLayer, UnitPos(tv, 0f));
                    else Vfx.ZephyrIce(popupLayer, UnitPos(tv, 0.3f));
                    break;
                case PlayerClass.BlackScythe:
                    Vfx.KainSlash(popupLayer, tp, big);
                    break;
                case PlayerClass.Sniper:
                    if (card.IsBayonet || pv == null) Vfx.Art(popupLayer, "Fx_Explosion", tp, 170f, Random.Range(0f, 360f), 0.4f, 0.3f, 0.9f, 1f);
                    else Vfx.SylviaShot(popupLayer, UnitPos(pv, 0.74f) + new Vector2(pv.ArtHeight * 0.88f, 0f), tp);
                    break;
                case PlayerClass.CosmicPhoenix:
                    if (pv != null) Vfx.IgnisPhoenix(popupLayer, UnitPos(pv, 0.55f) + new Vector2(80f, 0f), tp);
                    break;
            }
        }
    }

    // 히트스톱: Time.timeScale(게임 전체 속도)을 잠깐 0.05 로. 기다리는 건 실제 시간(Realtime)으로
    //   메뉴(일시정지)가 timeScale 을 0 으로 바꿔 놨으면 건드리지 않음
    IEnumerator HitStop(float seconds)
    {
        if (hitStopping || Time.timeScale < 0.99f) yield break;
        hitStopping = true;
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(seconds);
        if (Mathf.Approximately(Time.timeScale, 0.05f)) Time.timeScale = 1f;   // 그 사이에 메뉴가 멈췄으면 그대로 둠
        hitStopping = false;
    }

    // 화면 흔들기 (더 센 흔들림이 오면 그걸로 덮어씀)
    void Shake(float time, float power)
    {
        if (power >= shakePower * (shakeTimer > 0f ? 1f : 0f))
        {
            shakeTimer = time;
            shakePower = Mathf.Min(power, 28f);
        }
    }

    // 쓰러진 캐릭터는 그림을 흐리게
    void SetFigureAlpha(UnitView v, float alpha)
    {
        CanvasGroup g = v.Rt.GetComponent<CanvasGroup>();
        if (g == null) g = v.Rt.gameObject.AddComponent<CanvasGroup>();
        g.alpha = alpha;
        g.blocksRaycasts = false;
    }

    string EffectText(PlayerCombatant p)
    {
        return p.EffectText();   // 규칙 쪽(PlayerCombatant)이 만들어 줌
    }

    // ==================== 손패 맞추기 ====================
    void SyncHand()
    {
        PlayerCombatant p = (battle.Result == BattleResult.Ongoing) ? Me : null;

        // 조작하는 플레이어가 바뀌었으면 손패를 전부 치움
        if (p != shownPlayer)
        {
            foreach (CardView v in handViews) v.Leave(LocalOf(discardPile), 0.3f);
            handViews.Clear();
            viewOf.Clear();
            hovered = null;
            shownPlayer = p;
        }
        if (p == null) return;

        // 손에서 사라진 카드 → 버림 더미(또는 소멸·파워면 가운데에서 작아지며 사라짐)로
        for (int i = handViews.Count - 1; i >= 0; i--)
        {
            CardView v = handViews[i];
            if (!p.Hand.Contains(v.Data))
            {
                bool toDiscard = p.DiscardPile.Contains(v.Data);
                Vector2 dest = toDiscard ? LocalOf(discardPile) : CardView.ShowcasePos;
                if (v.Data == lastPlayed) v.CastColor = slashColor;   // 3-182: 카드 사용 빛 = 직업 색 (검은 낫 보라, 피닉스 주황, 마도사 하늘, 스나이퍼 진홍)
                v.Leave(dest, toDiscard ? 0.3f : 0.05f, v.Data == lastPlayed);   // 방금 낸 카드는 가운데에 보여 준 뒤
                handViews.RemoveAt(i);
                viewOf.Remove(v.Data);
                if (hovered == v) hovered = null;
            }
        }

        // 새로 들어온 카드 → 뽑기 더미에서 날아옴
        foreach (CardData data in p.Hand)
        {
            if (viewOf.ContainsKey(data)) continue;
            GameObject go = new GameObject("Card_" + data.Name, typeof(RectTransform));
            go.transform.SetParent(handRoot, false);
            CardView v = go.AddComponent<CardView>();
            v.Build(data, this, p.ManaName);
            Vector2 from = LocalOf(drawPile);
            v.Rt.anchoredPosition = from;
            if (Time.time >= drawFxCooldown)   // 3-188: 카드 뽑기 = 뽑기 더미에서 푸른 소용돌이 (0.25초에 한 번만)
            {
                drawFxCooldown = Time.time + 0.25f;
                SoundManager.Play("CardDraw");   // 3-190
                VfxPart sw = Vfx.Art(handRoot, "Fx_DrawSwirl", from, 120f, 0f, 0.5f, 0.4f, 1f, 1.1f, 0.25f, 0.4f);
                if (sw != null) sw.Spin = -300f;
            }
            v.Rt.localScale = new Vector3(0.3f, 0.3f, 1f);
            v.TargetPos = from;
            handViews.Add(v);
            viewOf[data] = v;
        }

        // 화면 카드 순서를 실제 손패 순서와 같게
        handViews.Sort((a, b) => p.Hand.IndexOf(a.Data).CompareTo(p.Hand.IndexOf(b.Data)));

        // 코스트 색 (쓸 수 있으면 흰색, 부족하면 빨간색)
        foreach (CardView v in handViews)
        {
            int cost = battle.EffectiveCost(p, v.Data);
            bool manaOk = v.Data.UsesAllMana ? p.Mana >= 1 : p.Mana >= battle.EffectiveManaCost(p, v.Data);
            string reason;
            v.SetCostState(cost, cost <= p.Energy, manaOk, !busy && battle.CanPlay(p, v.Data, out reason));
        }
    }

    // 화면 부품의 위치를 손패 기준점 좌표로 바꿈
    Vector2 LocalOf(RectTransform target)
    {
        Vector3 local = handRoot.InverseTransformPoint(target.position);
        return new Vector2(local.x, local.y);
    }

    // ==================== 매 프레임: 부채꼴 배치 ====================
    // 3-180: 게임을 끌 때 도감 기록 저장 (0.5초 안에 본 것까지)
    void OnApplicationQuit()
    {
        Discovery.Scan(run);
        DiscoveryStore.SaveIfDirty();
    }

    // ==================== 3-196 난이도 ====================
    private DifficultyPanel difficultyPanel;
    private Difficulty runDifficulty = Difficulty.Normal;
    private string lastRoomCode = "";   // 다음에 시작할 런의 난이도 (패배 뒤 [새 런] 도 같은 난이도)

    // 위쪽 글자 끝에 붙는 난이도 표시 (보통이면 안 붙음). 예) "2스테이지   수성   칸 3/10   [지옥]"
    string DiffTag()
    {
        if (run == null || run.Difficulty == Difficulty.Normal) return "";
        string[] col = { "#9BE07A", "#F0E6D2", "#FFC86A", "#FF8A4A", "#FF4A4A" };
        return "   <color=" + col[(int)run.Difficulty] + ">[" + DifficultyInfo.Name(run.Difficulty) + "]</color>";
    }

    // 캐릭터 선택이 끝나면 난이도 화면 → [시작] 이면 런 시작 / [뒤로] 면 캐릭터 선택으로
    void OpenDifficulty(List<PlayerClass> classes, bool multi)
    {
        characterSelect.Hide();
        difficultyPanel.Show(classes.Count, runDifficulty, (d) =>
        {
            ScreenTransition.Play(this, () =>
            {
                difficultyPanel.Hide();
                titlePanel.Hide();
                runDifficulty = d;
                runClasses = classes;
                startedFromTitle = true;
                NewRun();
            });
        }, () =>
        {
            difficultyPanel.Hide();
            if (multi) characterSelect.ShowMulti(classes.Count, lastRoomCode);
            else characterSelect.Show(classes[0]);
        });
    }

    // ==================== 3-190 사운드: 지금 화면에 맞는 배경음 ====================
    // 매 프레임 "지금 어떤 곡이어야 하는지"만 알려 줌. 곡이 바뀔 때만 SoundManager 가 크로스페이드
    //   Title(타이틀·캐릭터 선택) / Battle(전투) / Boss(보스 전투) / Shop(상점) / Rest(야영지·대장간) / Map(지도·선택지) / Ending(엔딩)
    //   전투 결과 창이 떠 있는 동안은 배경음을 끄고 승리·패배 효과음만
    void UpdateBgm()
    {
        string want;
        if (endingPanel != null && endingPanel.gameObject.activeSelf) want = "Ending";
        else if (titlePanel.gameObject.activeSelf || characterSelect.gameObject.activeSelf || difficultyPanel.IsOpen || run == null) want = "Title";
        else if (resultPanel.activeSelf) want = "";
        else if (battle != null && battle.Result == BattleResult.Ongoing && mapPanelHidden())
        {
            bool boss = false;
            foreach (EnemyCombatant e in battle.Enemies) if (e.Rank == EnemyRank.Boss) boss = true;
            want = boss ? "Boss" : "Battle";
        }
        else if (run.Place == RunPlace.Shop) want = "Shop";
        else if (run.Place == RunPlace.Camp || run.Place == RunPlace.Forge) want = "Rest";
        else want = "Map";
        SoundManager.Bgm(want);
    }

    void Update()
    {
        UpdateBgm();   // 3-190
        // 3-112: 런을 한 시간 (엔딩 기록). 게임 속도와 상관없이 실제 시간
        if (run != null && !run.IsOver) run.PlaySeconds += Time.unscaledDeltaTime;
        // 3-180 도감: 0.5초마다 지금 화면에 나온 카드·아티팩트를 "본 것"으로 기록 (새로 본 게 있을 때만 저장)
        discoveryTimer -= Time.unscaledDeltaTime;
        if (discoveryTimer <= 0f)
        {
            discoveryTimer = 0.5f;
            Discovery.Scan(run);
            DiscoveryStore.SaveIfDirty();
        }
        HandleKeys();
        LayoutHand();

        // 적 의도 상자: 둥실둥실 (적마다 박자가 조금씩 다르게)
        for (int i = 0; i < enemyViews.Count; i++)
        {
            UnitView ev = enemyViews[i];
            if (ev.IntentBox == null) continue;
            float bob = Mathf.Sin(Time.time * 2.5f + i * 1.3f) * 5f;
            ev.IntentBox.anchoredPosition = new Vector2(0, ev.IntentBaseY + bob);
        }

        // 필살기 화면 효과: 어둡게 / 보랏빛 테두리 / 번쩍임이 부드럽게 따라감 (히트스톱 중에도 움직이게 unscaled)
        float udt = Time.unscaledDeltaTime;
        dimAmount = Mathf.MoveTowards(dimAmount, dimTarget, udt * 5f);
        stageDim.color = new Color(0.04f, 0f, 0.07f, 0.78f * dimAmount);   // 검은 빛 (살짝 보라)
        vignette.color = new Color(0.38f, 0.06f, 0.62f, 0.7f * dimAmount);  // 보랏빛 테두리
        Color fc = flash.color;
        fc.a = Mathf.MoveTowards(fc.a, 0f, udt * 4f);
        flash.color = fc;

        // 화면 흔들림: 남은 시간에 비례해서 점점 약해짐
        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.deltaTime;
            float k = Mathf.Max(0f, shakeTimer) / 0.25f;
            stageRoot.anchoredPosition = Random.insideUnitCircle * shakePower * k;
            if (shakeTimer <= 0f) { stageRoot.anchoredPosition = Vector2.zero; shakePower = 0f; }
        }

        if (toastTimer > 0f)
        {
            toastTimer -= Time.deltaTime;
            Color c = toastText.color;
            c.a = Mathf.Clamp01(toastTimer / 0.5f);
            toastText.color = c;
        }
    }

    // 2026-11-10: 슬더스2 처럼 평소엔 손패가 화면 아래로 내려가 있고(이름·그림 정도만 보임),
    //   마우스를 올린 카드만 위로 올라와 전체가 보임 → 배경 바닥과 캐릭터가 가려지지 않음
    private const float HandRestY = -150f;

    void LayoutHand()
    {
        int n = handViews.Count;
        if (n == 0) return;

        // 카드가 많을수록 간격을 좁힘 (최대 폭 약 1050. 3-135: 턴 종료를 버림 더미 위로 올려서 다시 1050)
        float spacing = (n > 1) ? Mathf.Min(175f, 1050f / (n - 1)) : 0f;
        int hoverIndex = (hovered != null && dragging == null) ? handViews.IndexOf(hovered) : -1;

        for (int i = 0; i < n; i++)
        {
            CardView v = handViews[i];
            if (v == dragging) continue;   // 드래그 중인 카드는 드래그 코드가 위치를 정함

            float offset = i - (n - 1) / 2f;            // 가운데가 0, 왼쪽이 음수
            float x = offset * spacing;
            float y = HandRestY - offset * offset * 5f; // 평소엔 화면 아래로 반쯤 내려가 있음 + 양쪽 끝으로 갈수록 살짝 아래로 (부채꼴)
            float angle = -offset * 4f;                 // 양쪽 끝으로 갈수록 기울기
            float scale = 1f;

            if (hoverIndex >= 0)
            {
                if (i == hoverIndex)
                {
                    y = 95f;          // 위로 올라와서
                    angle = 0f;       // 똑바로 서고
                    scale = 1.3f;     // 커짐
                }
                else
                {
                    x += (i < hoverIndex) ? -70f : 70f;   // 옆 카드는 비켜남
                }
            }

            v.TargetPos = new Vector2(x, y);
            v.TargetAngle = angle;
            v.TargetScale = scale;
            v.transform.SetSiblingIndex(i);
        }

        // 위로 올라온 카드와 드래그 중인 카드는 맨 앞에 그리기
        if (hoverIndex >= 0) hovered.transform.SetAsLastSibling();
        if (dragging != null) dragging.transform.SetAsLastSibling();
    }

    // ==================== 카드 입력 ====================
    public void OnCardHover(CardView card, bool enter)
    {
        if (dragging != null) return;
        if (enter) hovered = card;
        else if (hovered == card) hovered = null;
    }

    public void OnCardBeginDrag(CardView card, PointerEventData e)
    {
        PlayerCombatant p = Me;
        if (busy || p == null || battle.Result != BattleResult.Ongoing) return;

        string reason;
        // 영혼 봉인: 봉인할 카드를 고르는 중이면 어떤 카드든 끌어올릴 수 있음 (사용이 아니라 봉인)
        if (!p.PendingSealPick && p.PendingDiscardPick <= 0 && !battle.CanPlay(p, card.Data, out reason))
        {
            Toast(reason);
            return;
        }

        dragging = card;
        hovered = null;
        dragNeedsTarget = !p.PendingSealPick && p.PendingDiscardPick <= 0 && (card.Data.Target == TargetType.SingleEnemy);

        if (dragNeedsTarget)
        {
            // 대상이 필요한 카드: 가운데 위로 올라와서 화살표로 적을 가리킴
            card.TargetPos = new Vector2(0, 170);
            card.TargetAngle = 0f;
            card.TargetScale = 1.2f;
        }
        OnCardDrag(card, e);
    }

    public void OnCardDrag(CardView card, PointerEventData e)
    {
        if (dragging != card) return;

        if (dragNeedsTarget)
        {
            EnemyCombatant target = EnemyUnder(e.position);
            if (target != arrowTarget)
            {
                arrowTarget = target;
                foreach (UnitView v in enemyViews) RefreshUnit(v, true);   // 대상 강조 색 바꾸기
            }
            DrawArrow(card, e.position, target != null);
        }
        else
        {
            // 대상이 없는 카드: 마우스를 그대로 따라감
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(handRoot, e.position, null, out local);
            card.Snap(local);
            card.TargetAngle = 0f;
            card.TargetScale = 1.1f;
        }
    }

    public void OnCardEndDrag(CardView card, PointerEventData e)
    {
        if (dragging != card) return;
        PlayerCombatant p = Me;
        dragging = null;
        HideArrow();
        EnemyCombatant target = arrowTarget;
        arrowTarget = null;

        if (p == null) { Refresh(); return; }
        int index = p.Hand.IndexOf(card.Data);

        // 영혼 봉인: 위로 끌어올린 카드를 봉인
        if (p.PendingSealPick)
        {
            Vector2 sealLocal;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(handRoot, e.position, null, out sealLocal);
            if (sealLocal.y > 250f && index >= 0 && battle.SealCard(p, index)) Toast("[" + card.Data.Name + "] 봉인! 다음 턴에 에너지 -1로 돌아옴");
            Refresh();
            return;
        }

        // 전술 후퇴: 위로 끌어올린 카드를 버림 → 탄환
        if (p.PendingDiscardPick > 0)
        {
            Vector2 dLocal;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(handRoot, e.position, null, out dLocal);
            int gain = p.PendingDiscardPick;
            if (dLocal.y > 250f && index >= 0 && battle.DiscardPick(p, index)) Toast("[" + card.Data.Name + "] 버림! " + p.ManaName + " +" + gain);
            Refresh();
            return;
        }

        // 필살기 연출 판단용: 카드를 쓰기 전 적 HP 기억
        Dictionary<EnemyCombatant, int> hpBefore = new Dictionary<EnemyCombatant, int>();
        foreach (EnemyCombatant en in battle.Enemies) hpBefore[en] = en.Hp;

        bool played = false;
        if (dragNeedsTarget)
        {
            if (target != null && index >= 0) played = battle.PlayCard(p, index, target);
        }
        else
        {
            // 손패 영역보다 위(기준점에서 250 이상)로 끌어올렸으면 사용
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(handRoot, e.position, null, out local);
            if (local.y > 250f && index >= 0) played = battle.PlayCard(p, index, null);
        }

        if (!played) { Refresh(); return; }   // 안 썼으면 카드는 원래 자리로 돌아감

        // 썼으면: 카드는 바로 가운데로 → 공격 카드면 캐릭터가 돌진 → 돌진이 닿을 때쯤 피해 표시
        lastPlayed = card.Data;
        if (p.PendingSealPick) Toast("봉인할 카드를 위로 끌어올리세요");
        if (p.PendingDiscardPick > 0) Toast("버릴 카드를 위로 끌어올리세요 (탄환 +" + p.PendingDiscardPick + ")");
        switch (p.Class)   // 공격 이펙트 색 (직업마다)
        {
            case PlayerClass.CosmicPhoenix: slashColor = UIFactory.Hex("#FF8A30"); break;
            case PlayerClass.FrostMage: slashColor = UIFactory.Hex("#8FD8FF"); break;
            case PlayerClass.Sniper: slashColor = UIFactory.Hex("#FF4A3A"); break;   // 스나이퍼: 진홍 탄흔
            default: slashColor = UIFactory.Hex("#9A4DFF"); break;   // 검은 낫: 보랏빛
        }
        if (p.IsNight) slashColor = UIFactory.Hex("#C0206A");   // 죽음의 밤 동안: 붉은 기운이 섞인 베기
        SyncHand();

        // ---- 필살기 연출 (웹툰 느낌) ----
        List<EnemyCombatant> hitEnemies = new List<EnemyCombatant>();
        // 카드를 쓰기 전에 있던 적 기준으로 봄 (이 카드로 웨이브가 바뀌면 battle.Enemies 는 이미 새 적이라서)
        foreach (KeyValuePair<EnemyCombatant, int> pair in hpBefore)
        {
            if (pair.Key.Hp < pair.Value) hitEnemies.Add(pair.Key);
        }
        string id = card.Data.Id;
        // 3-136: 처형 연출을 끈 동안은 "처형됐는지" 계산도 주석으로 막음 (안 쓰는 변수 경고 CS0219 없애기)
        //   처형 연출을 다시 켜려면 아래 3줄과 special 조건의 "|| executed" 주석을 함께 풀면 됨
        // bool executed = false;
        // foreach (KeyValuePair<EnemyCombatant, int> pair in hpBefore)
        //     if (pair.Value > 0 && pair.Key.WasExecuted) executed = true;
        PlayCardSpecialFx(p, card.Data, hitEnemies, target);   // 3-188: 필살기 카드 큰 이펙트
        PlayClassFx(p, card.Data, hitEnemies);   // 3-184: 직업별 그림 이펙트 (번개·베기·사격·불새)

        bool special = (id == "half_moon" && hitEnemies.Count > 0)   // 월식으로 바로 터진 반월참
                    // || executed                                    // 3-129: 처형 연출은 일단 끔 (처형 규칙은 그대로, 보통 공격 연출로 보임)
                    || id == "dark_dominion"                          // 어둠의 지배
                    || id == "eclipse";                               // 월식 발동
        if (special)
        {
            int poseKind = id == "half_moon" ? 6 : (id == "eclipse" ? 3 : 2);   // 반월참 = 기습 자세, 월식 = 차징 자세
            StartCoroutine(SpecialRoutine(p, hitEnemies, hitEnemies.Count == 0, null, poseKind));   // 3-129: "처형" 글자 연출 끔
            return;
        }

        // 반월참을 차징만 한 경우: 돌진하지 않고 차징 자세만 (터질 때 기습 자세로 가름)
        if (id == "half_moon")
        {
            UnitView mv = ViewOf(p);
            if (mv != null) Vfx.Rise(popupLayer, UnitPos(mv, 0.5f), UIFactory.Hex("#A65CFF"), 8);
            Refresh();
            return;
        }

        // 죽음의 밤: 죽음의 밤 자세 + 붉은·보랏빛·검은 기운
        if (id == "death_night")
        {
            UnitView nv = ViewOf(p);
            if (nv != null)
            {
                nv.Motion.NightCast();
                Vector2 nb = UnitPos(nv, 0.5f);
                Vfx.Shards(popupLayer, nb, UIFactory.Hex("#D0183A"), 18, 380f, 60f);   // 붉은 기운
                Vfx.Shards(popupLayer, nb, UIFactory.Hex("#9A3BFF"), 12, 320f, 60f);   // 보랏빛
                Vfx.Shards(popupLayer, nb, UIFactory.Hex("#0C0612"), 10, 280f, 60f);   // 검은 빛
            }
            Refresh();
            return;
        }

        // 적에게 거는 스킬 (죽음의 인장 등): 저주 자세 + 대상에게 보랏빛·검은 기운
        if (id == "mana_amp" || id == "mana_rebuild" || id == "mana_release")
        {
            // 마도사 마력 집중 (3-77): 마력 증폭·마력 재구성·마력해방 = 제자리에서 마력을 모으는 자세 + 푸른 기운이 피어오름
            UnitView mc = ViewOf(p);
            if (mc != null)
            {
                mc.Motion.ManaCharge();
                Vfx.Rise(popupLayer, UnitPos(mc, 0.5f), UIFactory.Hex("#5CC8FF"), 10);
            }
            Refresh();
            return;
        }

        if (card.Data.IsThrow)
        {
            // 던지는 카드 (3-74, 연막탄·사슬 함정): 제자리에서 투척 자세 + 앞쪽에 회색 연기 조각
            //   사슬 함정처럼 적에게 쓰는 카드는 맞은 적에게 고리 효과도 (아래 '적에게 거는 스킬' 보다 먼저 확인해야 함)
            UnitView thrower = ViewOf(p);
            if (thrower != null)
            {
                thrower.Motion.Throw();
                Vfx.Shards(popupLayer, UnitPos(thrower, 0.6f) + new Vector2(170f, 30f), UIFactory.Hex("#A8A8A8"), 12, 240f, 50f);
            }
            UnitView hitView = target != null ? ViewOf(target) : null;
            if (hitView != null) Vfx.Ring(popupLayer, UnitPos(hitView, 0.5f), UIFactory.Hex("#C8C8C8"));
            StartCoroutine(RefreshAfter(0.12f));
            return;
        }

        if (card.Data.Type == CardType.Skill && card.Data.Target != TargetType.Self)
        {
            UnitView cv = ViewOf(p);
            if (cv != null) cv.Motion.Curse();
            List<EnemyCombatant> cursed = new List<EnemyCombatant>();
            if (target != null) cursed.Add(target);
            else foreach (EnemyCombatant ce in battle.Enemies) if (!ce.IsDead) cursed.Add(ce);
            foreach (EnemyCombatant ce in cursed)
            {
                UnitView tv = ViewOf(ce);
                if (tv == null) continue;
                Vector2 tp = UnitPos(tv, 0.5f);
                Vfx.Ring(popupLayer, tp, slashColor);   // 캐릭터 색 (검은 낫 = 보라)
                Vfx.Shards(popupLayer, tp, UIFactory.Hex("#0C0612"), 10, 260f, 60f);
            }
            Refresh();
            return;
        }

        if (id == "soul_harvest")
        {
            // 영혼 수확: 수확 자세 + 몸 주변에서 보랏빛·검은 기운이 피어오름
            UnitView hv = ViewOf(p);
            if (hv != null)
            {
                hv.Motion.Harvest();
                Vector2 body = UnitPos(hv, 0.5f);
                Vfx.Rise(popupLayer, body, UIFactory.Hex("#A65CFF"), 10);
                Vfx.Shards(popupLayer, body, UIFactory.Hex("#0C0612"), 10, 260f, 60f);
            }
            Refresh();
            return;
        }

        if (card.Data.Type == CardType.Attack)
        {
            UnitView pv = ViewOf(p);
            if (pv != null && p.Class == PlayerClass.FrostMage)
            {
                // 마도사 (2026-11-15): 돌진하지 않고 제자리에서 마법. [벼락] 카드 = 전기 자세, 나머지 공격 = 얼음 자세
                //   3-75: [절대영도] · [낙뢰] 는 전용 자세 (그림이 없으면 보통 얼음·전기 자세)
                if (id == "absolute_zero") pv.Motion.CastAbsoluteZero();
                else if (id == "lightning_fall") pv.Motion.CastLightningFall();
                else if (card.Data.IsLightning) pv.Motion.CastLightning(); else pv.Motion.CastIce();
            }
            else if (pv != null && p.Class == PlayerClass.Sniper)
            {
                // 스나이퍼 (3-90): [총검술] = 앞으로 뛰어들며 총검 휘두르기 / 나머지 공격 = 제자리 조준 사격 + 총구 불꽃
                if (card.Data.IsBayonet) pv.Motion.Lunge(false, 15);
                else if (id == "sn_red_flash")
                {
                    // 3-197: 붉은 섬광 = 연막탄과 같은 투척 자세 + 앞쪽에 붉은 조각 (섬광탄을 던지는 느낌)
                    pv.Motion.Throw();
                    Vfx.Shards(popupLayer, UnitPos(pv, 0.6f) + new Vector2(170f, 30f), UIFactory.Hex("#FF5A4A"), 12, 240f, 50f);
                }
                else
                {
                    pv.Motion.Shoot();
                    // 총구 자리: 조준 그림에서 총끝은 몸 가운데보다 몸 높이의 약 0.88배 앞, 발에서 0.74 높이
                    Vector2 muzzle = UnitPos(pv, 0.74f) + new Vector2(pv.ArtHeight * 0.88f, 0f);
                    Vfx.Sparks(popupLayer, muzzle, UIFactory.Hex("#FFD27A"), 10);
                    Vfx.Shards(popupLayer, muzzle, UIFactory.Hex("#FF8A3A"), 6, 260f, 20f);
                }
            }
            else if (pv != null) pv.Motion.Lunge(id == "ambush" || id == "rush");   // 기습(검은 낫)·돌진(피닉스)이면 특별 돌진 자세 (_Ambush 그림)
            StartCoroutine(RefreshAfter(0.12f));
        }
        else
        {
            Refresh();
        }
    }

    // 필살기 연출: 어두워짐 → 보랏빛·검은 기운 + 낫 치켜듦 → 내려찍기(잔상) → 빛기둥 + 초승달 + 번쩍 + 히트스톱 → 원래대로
    IEnumerator SpecialRoutine(PlayerCombatant attacker, List<EnemyCombatant> targets, bool auraOnly, string bigText, int poseKind = 2)
    {
        busy = true;
        UnitView av = ViewOf(attacker);
        Color violet = UIFactory.Hex("#9A3BFF");      // 보랏빛 (예전 붉은색 #E0102A 대신)
        Color darkGlow = UIFactory.Hex("#0C0612");    // 검은 빛
        dimTarget = 1f;
        if (av != null)
        {
            av.Motion.PlaySpecial(poseKind);
            Vector2 body = UnitPos(av, 0.5f);
            Vfx.Shards(popupLayer, body, violet, 22, 420f, 60f);                     // 보랏빛 기운
            Vfx.Shards(popupLayer, body, darkGlow, 18, 360f, 50f);                   // 검은 깃털
            if (attacker.IsNight) Vfx.Shards(popupLayer, body, UIFactory.Hex("#D0183A"), 14, 380f, 60f);   // 죽음의 밤: 붉은 기운
        }

        // 치켜드는 동안 기운이 한 번 더 뿜어짐
        yield return new WaitForSeconds(UnitMotion.SpecialImpact * 0.5f);
        if (av != null) { Vfx.Shards(popupLayer, UnitPos(av, 0.6f), violet, 14, 300f, 40f); Vfx.Shards(popupLayer, UnitPos(av, 0.6f), darkGlow, 8, 260f, 40f); }
        yield return new WaitForSeconds(UnitMotion.SpecialImpact * 0.5f);

        // 내려찍는 순간: 잔상 3개
        Image bodyImg = (av != null) ? FindBodyImage(av) : null;
        for (int i = 0; i < 3; i++)
        {
            if (bodyImg != null) Vfx.Ghost(av.Rt, bodyImg, (i == 1 ? new Color(0.05f, 0f, 0.1f, 0.5f) : new Color(0.6f, 0.25f, 1f, 0.55f - i * 0.12f)), 0.35f + i * 0.08f);
            yield return new WaitForSeconds(0.03f);
        }

        if (auraOnly)
        {
            // 적월: 대상 없이 보랏빛 달빛만 크게
            flash.color = new Color(0.55f, 0.35f, 0.85f, 0.45f);
            Shake(0.3f, 14f);
            if (av != null)
            {
                Vfx.Ring(popupLayer, UnitPos(av, 0.6f), violet);
                Vfx.Pillar(popupLayer, UnitPos(av, 0.3f), violet);
                Vfx.Shards(popupLayer, UnitPos(av, 0.5f), darkGlow, 16, 420f, 60f);
            }
        }
        else
        {
            foreach (EnemyCombatant e in targets)
            {
                UnitView ev = ViewOf(e);
                if (ev == null) continue;
                Vector2 c = UnitPos(ev, 0.5f);
                Vfx.Pillar(popupLayer, c, violet);
                Vfx.Crescent(popupLayer, c, violet, 420f, Random.Range(-35f, -15f));
                Vfx.Shards(popupLayer, c, UIFactory.Hex("#D9A8FF"), 12, 520f, 20f);   // 연보라 결정
                Vfx.Shards(popupLayer, c, violet, 16, 600f, 20f);
                Vfx.Shards(popupLayer, c, darkGlow, 12, 480f, 20f);   // 검은 파편
                if (attacker.IsNight) Vfx.Shards(popupLayer, c, UIFactory.Hex("#D0183A"), 14, 540f, 20f);   // 죽음의 밤: 붉은 기운 추가
            }
            flash.color = new Color(0.9f, 0.85f, 1f, 0.6f);
            Shake(0.35f, 26f);
        }

        Refresh();   // 이 순간 피해 숫자·HP 반영
        if (bigText != null && targets.Count > 0)
        {
            UnitView tv = ViewOf(targets[0]);
            if (tv != null) Popup(UnitPos(tv, 1.05f), bigText, UIFactory.Hex("#B866FF"), 2.4f, 72);
        }

        // 히트스톱: 아주 잠깐 거의 멈춤 (Time.timeScale 은 게임 전체 속도)
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(0.1f);
        Time.timeScale = 1f;

        yield return new WaitForSeconds(0.35f);
        dimTarget = 0f;
        yield return new WaitForSeconds(0.2f);
        busy = false;
        Refresh();
    }

    // 이 화면이 꺼질 때 게임 속도를 꼭 원래대로 (히트스톱 도중에 꺼져도 안전하게)
    void OnDisable()
    {
        Time.timeScale = 1f;
    }

    // 캐릭터 몸 그림 (Body 안의 "Sprite") 찾기
    Image FindBodyImage(UnitView v)
    {
        // 공격 자세가 보이는 중이면 그 그림으로 잔상을 만듦
        Transform sp = v.Body.Find("SpecialPose");
        if (sp != null && sp.GetComponent<Image>().enabled) return sp.GetComponent<Image>();
        Transform chg = v.Body.Find("ChargePose");
        if (chg != null && chg.GetComponent<Image>().enabled) return chg.GetComponent<Image>();
        Transform amb = v.Body.Find("AmbushPose");
        if (amb != null && amb.GetComponent<Image>().enabled) return amb.GetComponent<Image>();
        Transform pose = v.Body.Find("AttackPose");
        if (pose != null && pose.GetComponent<Image>().enabled) return pose.GetComponent<Image>();
        Transform t = v.Body.Find("Sprite");
        return t != null ? t.GetComponent<Image>() : null;
    }

    // delay 초 뒤에 화면을 맞춤 (그동안은 입력을 막음)
    IEnumerator RefreshAfter(float delay)
    {
        busy = true;
        yield return new WaitForSeconds(delay);
        busy = false;
        Refresh();
    }

    // 적 차례 연출: 공격하는 적이 차례로 돌진 → 맞는 순간 화면 맞춤
    // 월광섬 발동 연출: 차징하던 사람이 기습 자세로 앞으로 가르고, 적들 위에 보랏빛 초승달
    IEnumerator MoonlightFireRoutine(List<PlayerCombatant> fired)
    {
        busy = true;
        yield return new WaitForSeconds(0.25f);
        foreach (PlayerCombatant fp in fired)
        {
            UnitView fv = ViewOf(fp);
            if (fv != null) fv.Motion.Lunge(true);
            yield return new WaitForSeconds(0.1f);
            Color c = fp.IsNight ? UIFactory.Hex("#C0206A") : UIFactory.Hex("#9A3BFF");   // 죽음의 밤이면 붉은 보라
            foreach (UnitView ev in enemyViews)
            {
                Vector2 ep = UnitPos(ev, 0.5f);
                Vfx.Crescent(popupLayer, ep, c, 360f, Random.Range(-35f, -15f));
                Vfx.Shards(popupLayer, ep, UIFactory.Hex("#0C0612"), 8, 420f, 20f);
            }
            flash.color = new Color(0.8f, 0.7f, 1f, 0.35f);
            Shake(0.25f, 16f);
            yield return new WaitForSeconds(0.3f);
        }
        busy = false;
        Refresh();
    }

    void PlayEnemyStyleOn(UnitView v)
    {
        if (enemyStyles.Count == 0) return;   // 적 턴이 아닐 때 (가시·발화 등 자기 피해) 는 안 나옴
        Vfx.EnemyStyle st = enemyStyles[enemyStyleIndex % enemyStyles.Count];
        enemyStyleIndex++;
        Vfx.EnemyHit(popupLayer, st, UnitPos(v, 0.5f), UnitPos(v, 0f), v.ArtHeight);
    }

    IEnumerator EnemyTurnRoutine(List<EnemyCombatant> attackers, List<PlayerCombatant> fired)
    {
        busy = true;
        // 3-186: 이번 적 턴의 공격 종류 모아 두기 (아래 Refresh 에서 플레이어가 맞을 때 하나씩)
        enemyStyles.Clear();
        enemyStyleIndex = 0;
        foreach (EnemyCombatant ea in attackers) enemyStyles.Add(Vfx.StyleOf(UnitArt.EnemyArtName(ea.Name)));
        Toast("적 턴");   // 슬더스2 처럼 차례가 바뀔 때 큰 글씨
        SoundManager.Play("EnemyTurn");   // 3-190
        yield return new WaitForSeconds(0.35f);
        foreach (EnemyCombatant e in attackers)
        {
            UnitView ev = ViewOf(e);
            if (ev != null) ev.Motion.Lunge();
            yield return new WaitForSeconds(0.12f);
        }
        yield return new WaitForSeconds(0.05f);
        busy = false;
        controlIndex = 0;
        SelectNextAvailablePlayer();   // 새 턴: P1부터
        Refresh();
        enemyStyles.Clear();   // 3-186: 적 턴 끝
        if (battle.Result == BattleResult.Ongoing) { Toast("내 턴  ·  " + battle.Round + "턴"); PlayTurnStartFx(); }   // 3-188 에너지 구슬
        if (fired.Count > 0) yield return StartCoroutine(MoonlightFireRoutine(fired));
    }

    // 마우스 아래에 있는 살아 있는 적
    EnemyCombatant EnemyUnder(Vector2 screenPos)
    {
        foreach (UnitView v in enemyViews)
        {
            if (v.Unit.IsDead) continue;
            if (RectTransformUtility.RectangleContainsScreenPoint(v.Rt, screenPos, null)) return (EnemyCombatant)v.Unit;
        }
        return null;
    }

    void OnEndTurnClicked()
    {
        PlayerCombatant p = Me;
        if (busy || !battle.CanAct(p)) return;

        // 적이 움직이기 전에 "누가 공격할 예정인지" 기억해 둠
        List<EnemyCombatant> attackers = new List<EnemyCombatant>();
        foreach (EnemyCombatant e in battle.Enemies)
        {
            if (!e.IsDead && e.Intent.Damage > 0) attackers.Add(e);
        }
        int roundBefore = battle.Round;
        lastPlayed = null;
        // 월광섬을 차징 중인 사람 기억 (다음 턴 시작에 터지면 기습 자세로 가르는 연출)
        List<PlayerCombatant> charging = new List<PlayerCombatant>();
        foreach (PlayerCombatant cp in battle.Players) if (cp.PendingHalfMoon > 0 && !cp.IsDead) charging.Add(cp);
        slashColor = UIFactory.Hex("#FF7A30");   // 적 차례에 적이 받는 피해는 발화·반사 같은 불 피해

        battle.EndTurn(p);

        // 라운드가 넘어갔거나 전투가 끝났다면 = 적이 행동했다
        bool enemyActed = battle.Round != roundBefore || battle.Result != BattleResult.Ongoing;
        List<PlayerCombatant> fired = new List<PlayerCombatant>();
        if (enemyActed) foreach (PlayerCombatant cp in charging) if (cp.PendingHalfMoon == 0 && !cp.IsDead) fired.Add(cp);
        if (enemyActed && attackers.Count > 0)
        {
            StartCoroutine(EnemyTurnRoutine(attackers, fired));
        }
        else if (enemyActed)
        {
            controlIndex = 0;
            SelectNextAvailablePlayer();
            Refresh();
            if (battle.Result == BattleResult.Ongoing) { Toast("내 턴  ·  " + battle.Round + "턴"); PlayTurnStartFx(); }   // 3-188 에너지 구슬
            if (fired.Count > 0) StartCoroutine(MoonlightFireRoutine(fired));
        }
        else
        {
            // 다른 플레이어가 아직 턴 중 → 그 플레이어로 전환 (한 PC 테스트)
            SelectNextAvailablePlayer();
            Refresh();
            if (Me != p) Toast(Me.Name + " 조작 중");
        }
    }

    // ==================== 화살표 ====================
    // 카드 위쪽에서 마우스까지 휘어진 곡선(2차 베지어)을 점 16개로 그림
    void DrawArrow(CardView card, Vector2 screenPos, bool onTarget)
    {
        Vector3 startWorld = card.Rt.TransformPoint(new Vector3(0, CardView.Height / 2f, 0));
        Vector3 s3 = popupLayer.InverseTransformPoint(startWorld);
        Vector2 start = new Vector2(s3.x, s3.y);
        Vector2 end;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(popupLayer, screenPos, null, out end);
        Vector2 control = new Vector2(start.x, end.y + 120f);   // 위로 볼록하게 휘는 기준점

        Color c = onTarget ? UIFactory.Hex("#FF5A3A") : gold;
        for (int i = 0; i < arrowDots.Count; i++)
        {
            float t = (i + 1f) / arrowDots.Count;
            // 베지어 공식: (1-t)²·시작 + 2(1-t)t·기준 + t²·끝
            Vector2 pos = (1 - t) * (1 - t) * start + 2 * (1 - t) * t * control + t * t * end;
            Image dot = arrowDots[i];
            dot.gameObject.SetActive(true);
            dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            dot.rectTransform.anchoredPosition = pos;
            float size = Mathf.Lerp(12f, 26f, t);
            dot.rectTransform.sizeDelta = new Vector2(size, size);
            dot.color = c;
        }

        // 화살촉: 곡선 끝에서 진행 방향(접선)으로 회전
        Vector2 tangent = 2f * (end - control);   // 베지어를 t=1 에서 미분한 값
        float angle = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg - 90f;   // 삼각형이 위(90도)를 보고 있으므로 -90
        arrowHead.gameObject.SetActive(true);
        arrowHead.rectTransform.anchorMin = arrowHead.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        arrowHead.rectTransform.anchoredPosition = end;
        arrowHead.rectTransform.sizeDelta = new Vector2(54, 54);
        arrowHead.rectTransform.localEulerAngles = new Vector3(0, 0, angle);
        arrowHead.color = c;
        arrowDots[arrowDots.Count - 1].gameObject.SetActive(false);   // 마지막 점 자리는 화살촉이 차지
    }

    void HideArrow()
    {
        foreach (Image d in arrowDots) d.gameObject.SetActive(false);
        if (arrowHead != null) arrowHead.gameObject.SetActive(false);
    }

    // ==================== 떠오르는 숫자 / 안내 ====================
    void Popup(Vector2 pos, string text, Color color, float popScale = 1.6f, int fontSize = 44)
    {
        Text t = UIFactory.MakeText("Popup", popupLayer, text, fontSize, color, TextAnchor.MiddleCenter);
        t.fontStyle = FontStyle.Bold;
        t.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.9f);
        t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        t.rectTransform.sizeDelta = new Vector2(400, fontSize + 20);
        t.rectTransform.anchoredPosition = pos + new Vector2(Random.Range(-30f, 30f), 0f);
        t.gameObject.AddComponent<FloatingText>().PopScale = popScale;
    }

    void Toast(string message)
    {
        toastText.text = message;
        toastTimer = 1.6f;
        Color c = toastText.color;
        c.a = 1f;
        toastText.color = c;
    }
}
