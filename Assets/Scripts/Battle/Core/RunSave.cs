// ============================================================
// RunSave.cs  (2026-11-15, 3-73 이어하기)
// 런 하나를 "저장할 수 있는 글자 덩어리" 로 바꾸기 위한 데이터 상자들입니다.
//
// 왜 따로 만들었나?
//   RunState 안에는 CardData(효과 함수가 들어 있음), MapNode(서로를 가리키는 연결),
//   System.Random 같은 "글자로 바로 바꿀 수 없는 것" 이 섞여 있음.
//   그래서 저장할 때는 숫자·글자·아이디만 남긴 이 상자로 옮겨 담고 (RunState.ToSave),
//   불러올 때는 아이디로 카드·아티팩트를 다시 만들어서 RunState 를 새로 조립함 (RunState.FromSave).
//
// 자바로 치면
//   RunSave = 필드만 있는 DTO 클래스 (getter/setter 없는 POJO)
//   [System.Serializable] = 자바의 implements Serializable 과 같은 표시
//     → 유니티 JsonUtility 가 이 표시가 있는 클래스의 public 필드를 JSON 으로 바꿔 줌
//
// 규칙(Core) 이라서 UnityEngine 은 쓰지 않음. 실제 파일 쓰기·JSON 변환은 View/SaveManager.cs 가 함
//
// 언제 저장하나?  3-141 부터 일시정지 메뉴의 [저장] 을 눌렀을 때만 (칸 고르기·지도 화면에서만 됨, SaveManager.TrySave)
//   → 전투·상점·보물·보상 화면 도중에 끄면, 그 칸을 고르기 직전부터 다시 시작
//   (슬더스도 비슷하게 "방에 들어가기 전" 상태로 돌아감)
// ============================================================

using System.Collections.Generic;

[System.Serializable]
public class RunSave
{
    public int Version = RunSave.CurrentVersion;   // 저장 형식 번호. 형식이 바뀌면 올려서 옛 저장을 안전하게 버림
    public const int CurrentVersion = 1;

    public int Seed;              // 불러온 뒤 쓸 새 무작위 씨앗 (System.Random 은 상태를 저장할 수 없어서 새로 만듦)
    public int Stage;
    public int Difficulty = 1;    // 3-196 난이도 (0 쉬움 ~ 4 지옥). 옛 저장엔 없음 → 1 (보통)
    public bool HasMap;
    public int BattlesWon;
    public int EliteWins, BossWins;   // 3-112 엔딩 기록 (옛 저장엔 없음 → 0)
    public float PlaySeconds;
    public int[] StageRaces;      // Race 를 숫자로 (enum → int)
    public int[] StageBosses;     // 3-102: 스테이지 보스 종류 (옛 저장엔 없음 → null → 0 으로 읽음)
    public int Step;
    public int NextBattleBlock;   // 장비 보강
    public bool RevealNext;       // 정찰
    public List<int> StepChosen = new List<int>();   // 칸마다 고른 것 (3-81, 위쪽 10칸 아이콘). 옛 저장에는 없어도 빈 목록으로 읽힘
    public string SavedAt = "";   // 저장한 시각 (타이틀 버튼에 보여 주기용, View 가 넣음)

    public List<ChoiceSave> Choices = new List<ChoiceSave>();
    public MapSave Map = new MapSave();     // 지도가 없으면 Nodes 가 비어 있음 (JsonUtility 는 null 을 못 씀)
    public List<PlayerSave> Players = new List<PlayerSave>();

    // 타이틀 버튼에 쓸 짧은 설명. 예) "싱글 · 2스테이지 칸 4/10"
    public string Summary()
    {
        string who = Players.Count > 1 ? Players.Count + "인 멀티" : "싱글";
        string where;
        if (Stage >= GameBalance.MaxStage) where = "최종장";
        else if (HasMap) where = Stage + "스테이지 반격";
        else where = Stage + "스테이지 칸 " + Step + "/" + GameBalance.SiegeSteps;
        return who + " · " + where;
    }
}

// 수성 ??? 카드 하나
[System.Serializable]
public class ChoiceSave
{
    public int Kind;       // SiegeChoiceKind
    public int NodeType;   // MapNodeType
    public string Hint = "";
    public bool Hidden;
    public string Title = "";
    public int Fight;      // 3-110: 'N' 'V' 'E' 'W' (char 를 숫자로). 옛 저장엔 0
}

// 지도 전체. 칸들을 한 줄로 늘어놓고, 연결은 "몇 번째 칸" 번호로 저장
//   (MapNode 끼리 서로를 가리키는 참조는 JSON 으로 못 바꾸니까 번호로 바꿈.
//    자바로 치면 객체 그래프를 int 인덱스 배열로 펴서 저장하는 것)
[System.Serializable]
public class MapSave
{
    public int FloorCount;
    public List<NodeSave> Nodes = new List<NodeSave>();
    public int BossIndex = -1;
    public int CurrentIndex = -1;    // -1 = 아직 출발 전
    public int[] ColumnThemes = new int[0];
}

[System.Serializable]
public class NodeSave
{
    public int Floor, Column, Type, Theme;
    public bool Visited;
    public float SpotX = -1f, SpotY = -1f;
    public int[] Next = new int[0];   // 이어진 위층 칸들의 번호 (MapSave.Nodes 안에서 몇 번째인지)
}

[System.Serializable]
public class PlayerSave
{
    public int Class;      // PlayerClass
    public string Name = "";
    public int Str, Con, Agi;
    public int Hp, MaxHp;
    public int Gold;
    public int Level, Exp, StatPoints, CardPity;
    public int WeaponBlade, WeaponHilt, WeaponCore;
    public List<string> Relics = new List<string>();    // 아티팩트 아이디
    public List<string> Potions = new List<string>();   // 포션 아이디
    public List<CardSave> Deck = new List<CardSave>();
}

[System.Serializable]
public class CardSave
{
    public string Id = "";
    public bool Upgraded;
}
