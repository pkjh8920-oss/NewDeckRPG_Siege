// ============================================================
// MapData.cs
// 지도 규칙 (화면은 모름): 칸(MapNode), 지도(RunMap), 지도 만들기(MapGenerator)
//
// 지도 모양
//   아래(1층)에서 위(7층 보스)로 올라감. 한 층에 칸이 2~4개.
//   한 칸에서 위층의 "바로 위 / 왼쪽 위 / 오른쪽 위" 칸으로만 갈 수 있음.
//
// 만드는 방법 (슬더스 방식)
//   1) 1층의 무작위 칸에서 시작하는 길 4개를 위로 한 칸씩 그림 (왼위/위/오른위 중 무작위)
//      - 길끼리 X자로 엇갈리지 않게 함 → 지도가 깔끔
//      - 같은 칸을 지나가는 길은 자연스럽게 합쳐짐 → 갈림길이 생김
//   2) 6층 칸들은 모두 7층 보스와 연결
//   3) 세로줄마다 "길의 성격"(위험/평온/보통)을 정하고, 성격에 따라 칸 종류를 뽑음
//      → 오른쪽은 몬스터 길, 왼쪽은 보물·이벤트 길 같은 차이가 생김
//   4) 규칙: 1층 = 전부 몬스터 / 엘리트는 3층부터 / 6층 = 모닥불 /
//            엘리트·모닥불·상점·보물·대장간은 같은 종류가 바로 이어서 두 번 나오지 않음
//   5) 1층만 싸우고 보스까지 가는 길이 최소 1개 (없으면 다시 만듦)
//
// 새 구조에서는 4스테이지(공격)에서만 씀. 몬스터 칸 = 쉬운 전투, 엘리트 칸 = 어려운 전투, 보스 = 설계자
//
// 자바와 비교: List<MapNode> = ArrayList<MapNode>, HashSet<int> = HashSet<Integer>
// ============================================================

using System.Collections.Generic;

public enum MapNodeType
{
    Monster,   // 일반 전투
    Elite,     // 엘리트 전투
    Rest,      // 모닥불
    Treasure,  // 보물상자
    Event,     // 이벤트 (?)
    Shop,      // 상점
    Forge,     // 대장간
    Boss,      // 보스
    Veteran    // 정예 전투 (3-111). 맨 뒤에 붙여서 옛 저장의 칸 번호가 안 바뀜
}

public enum PathTheme
{
    Normal,  // 보통
    Danger,  // 위험: 몬스터·엘리트가 많음
    Calm     // 평온: 이벤트·보물·상점이 많음
}

// 지도의 칸 하나
public class MapNode
{
    public int Floor;                  // 0부터 (화면에는 +1 해서 "1층")
    public int Column;                 // 0 ~ MapColumns-1
    public MapNodeType Type;
    public PathTheme Theme;
    public bool Visited;               // 지나온 칸
    public float SpotX = -1f, SpotY = -1f;   // 그림 지도(최종장)에서의 자리 = 그림 안 비율. -1 이면 없음 (보통 양피지 지도)
    public List<MapNode> Next = new List<MapNode>();   // 위층으로 이어진 칸들
    public List<MapNode> Prev = new List<MapNode>();   // 아래층에서 이어진 칸들

    public bool IsBattle
    {
        get { return Type == MapNodeType.Monster || Type == MapNodeType.Veteran || Type == MapNodeType.Elite || Type == MapNodeType.Boss; }
    }
}

// 한 스테이지의 지도
public class RunMap
{
    public List<MapNode>[] Floors;     // Floors[층] = 그 층의 칸들 (왼쪽부터)
    public MapNode Boss;
    public MapNode Current;            // 지금 서 있는 칸 (null = 아직 출발 전)
    public PathTheme[] ColumnThemes;   // 세로줄마다 길의 성격

    // 지금 갈 수 있는 칸들: 출발 전이면 1층 전부, 아니면 지금 칸에서 이어진 위층 칸
    public List<MapNode> Available()
    {
        if (Current == null) return Floors[0];
        return Current.Next;
    }

    public bool CanMoveTo(MapNode node)
    {
        return Available().Contains(node);
    }

    public void MoveTo(MapNode node)
    {
        node.Visited = true;
        Current = node;
    }

    // 모든 칸 (화면에서 한꺼번에 그릴 때)
    public List<MapNode> AllNodes()
    {
        List<MapNode> all = new List<MapNode>();
        foreach (List<MapNode> floor in Floors) all.AddRange(floor);
        return all;
    }

    // 쉬는 곳 이름: 3-156 부터 모든 스테이지 "초소" (예전: 지도 스테이지 = "야영지")
    // RunState.BeginStage 가 스테이지가 바뀔 때 바꿔 줌 (static = 모든 곳에서 같은 값을 씀)
    public static string RestName = "초소";

    public static string TypeName(MapNodeType type)
    {
        switch (type)
        {
            case MapNodeType.Monster: return "몬스터";
            case MapNodeType.Veteran: return "정예";
            case MapNodeType.Elite: return "엘리트";
            case MapNodeType.Rest: return RestName;
            case MapNodeType.Treasure: return "보물상자";
            case MapNodeType.Event: return "이벤트";
            case MapNodeType.Shop: return "상점";
            case MapNodeType.Forge: return "대장간";
            default: return "보스";
        }
    }

    public static string Description(MapNodeType type)
    {
        switch (type)
        {
            case MapNodeType.Monster: return "쉬운 전투: 일반 몬스터와 싸웁니다.";
            case MapNodeType.Veteran: return "정예 전투: 훈련된 정예 몬스터와 싸웁니다. 일반보다 단단하고 아픕니다. 이기면 " + GameBalance.VeteranRelicChance + "% 확률로 아티팩트.";   // 3-175
            case MapNodeType.Elite: return "어려운 전투: 엘리트와 싸웁니다. 위험하지만 보상이 좋습니다. (보상은 아직 없음)";
            case MapNodeType.Rest: return "하나 고르기: 치료(최대 HP " + (int)(GameBalance.RestHealRatio * 100) + "%) / 장비 보강(다음 전투 방어도 +" + GameBalance.FortifyBlock + ") / 정찰(다음 칸 미리 보기). 보스 앞에서는 정찰병이 보스 힌트를 알려 줍니다.";
            case MapNodeType.Treasure: return "보물상자 " + GameBalance.TreasureChests + "개. 상자마다 아티팩트·포션·카드 중 1개씩.";   // 3-174
            case MapNodeType.Event: return "무슨 일이 일어날지 모릅니다. (준비 중: 5단계)";
            case MapNodeType.Shop: return "카드·아티팩트·포션을 골드로 삽니다.";
            case MapNodeType.Forge: return "드워프 대장장이. 카드 강화 또는 장비 강화(무기·갑옷·체력 중 하나).";
            default: return "이 스테이지의 보스. 3스테이지 = 요새 문을 지키는 수문장 / 최종장 = 설계자";
        }
    }

    public static string ThemeName(PathTheme theme)
    {
        switch (theme)
        {
            case PathTheme.Danger: return "위험한 길";
            case PathTheme.Calm: return "평온한 길";
            default: return "보통 길";
        }
    }
}

public static class MapGenerator
{
    // 다시 만드는 조건 (최대 GameBalance.MapGenerateTries 번)
    //   1) 길이 너무 많이 합쳐져서 칸이 적음 (갈림길이 거의 없음)
    //   2) 1층만 싸우고 보스까지 가는 길이 없음 (MapRequireSafePath 가 켜져 있을 때)
    // 최종장(4스테이지): 성 그림 위의 갈림길 지도
    //   2026-11-15: 매번 무작위 (칸 종류와 옆길 연결). 칸 자리는 성 그림에 맞춘 4줄 × 6층 (3-84: 5층 → 6층, 보스까지 7줄)
    //   0층 정문(정예) → 1~4층 무작위 → 5층 = 전부 초소(보스 바로 앞) → 설계자
    //   3-111: 정예 칸(Veteran)이 따로 생김 → 몬스터 칸은 일반 전투, 정예 칸은 정예 전투
    static readonly float[] FinalLaneX = { 0.27f, 0.41f, 0.585f, 0.74f };
    // 층마다 세로 위치: 정문(0.88) ~ 초소(0.30) 를 층 수로 고르게 나눔 (3-84: 예전 5층 고정값 0.88 ~ 0.36 → 6층)
    static float FinalFloorY(int floor)
    {
        int last = GameBalance.FinalFloors - 1;
        return GameBalance.FinalBottomY - (GameBalance.FinalBottomY - GameBalance.FinalTopY) * floor / last;
    }
    // 1~4층 칸 종류 가중치: 일반, 엘리트, 보물, 대장간, 초소, 정예 (3-111: 정예 칸 추가, 예전 일반 42 를 일반 20 + 정예 22 로)
    static readonly MapNodeType[] FinalTypes = { MapNodeType.Monster, MapNodeType.Elite, MapNodeType.Treasure, MapNodeType.Forge, MapNodeType.Rest, MapNodeType.Veteran };
    static readonly int[] FinalWeights = { 20, 22, 16, 10, 10, 22 };

    public static RunMap FinalChapter(System.Random rng)
    {
        for (int attempt = 0; attempt < 50; attempt++)
        {
            RunMap map = TryFinalChapter(rng);
            if (map != null) return map;
        }
        return TryFinalChapter(rng, true);
    }

    static RunMap TryFinalChapter(System.Random rng, bool force = false)
    {
        const int Lanes = 4;
        int Floors = GameBalance.FinalFloors;   // 3-84: 5 → 6 (GameBalance)
        RunMap map = new RunMap();
        map.Floors = new List<MapNode>[Floors + 1];
        map.ColumnThemes = new PathTheme[GameBalance.MapColumns];
        MapNode[,] grid = new MapNode[Floors, Lanes];
        int elites = 0, treasures = 0;
        for (int f = 0; f < Floors; f++)
        {
            map.Floors[f] = new List<MapNode>();
            for (int lane = 0; lane < Lanes; lane++)
            {
                if (f == 0 && lane != 1) continue;   // 정문은 한 칸
                MapNodeType type;
                if (f == 0) type = MapNodeType.Veteran;              // 정문 = 정예 (3-111)
                else if (f == Floors - 1) type = MapNodeType.Rest;   // 보스 바로 앞 = 전부 초소
                else
                {
                    type = FinalTypes[WeightedIndex(FinalWeights, rng)];
                    MapNodeType below = grid[f - 1, lane] != null ? grid[f - 1, lane].Type : MapNodeType.Monster;
                    if (type != MapNodeType.Monster && type != MapNodeType.Veteran && type == below) type = MapNodeType.Monster;     // 같은 특수 칸 연속 X
                    if (f == Floors - 2 && type == MapNodeType.Rest) type = MapNodeType.Monster;      // 초소층 바로 아래는 초소 X
                }
                if (type == MapNodeType.Elite) elites++;
                if (type == MapNodeType.Treasure) treasures++;
                MapNode n = new MapNode { Floor = f, Column = lane, Type = type, Theme = PathTheme.Normal };
                n.SpotX = f == 0 ? 0.5f : FinalLaneX[lane];
                n.SpotY = FinalFloorY(f);
                grid[f, lane] = n;
                map.Floors[f].Add(n);
            }
        }
        if (!force && (elites < GameBalance.FinalEliteMin || elites > GameBalance.FinalEliteMax
                       || treasures < GameBalance.FinalTreasureMin || treasures > GameBalance.FinalTreasureMax)) return null;

        // 연결: 정문 → 1층 전부. 그 위로는 같은 줄은 항상 + 옆 줄로 가끔 (선끼리 X 자로 엇갈리지 않게)
        for (int lane = 0; lane < Lanes; lane++) Link(grid[0, 1], grid[1, lane]);
        for (int f = 1; f < Floors - 1; f++)
        {
            bool[] rightLink = new bool[Lanes];
            for (int lane = 0; lane < Lanes; lane++)
            {
                Link(grid[f, lane], grid[f + 1, lane]);
                if (lane + 1 < Lanes && rng.Next(100) < 35) { Link(grid[f, lane], grid[f + 1, lane + 1]); rightLink[lane] = true; }
            }
            for (int lane = 1; lane < Lanes; lane++)
                if (!rightLink[lane - 1] && rng.Next(100) < 30) Link(grid[f, lane], grid[f + 1, lane - 1]);   // 반대 방향과 엇갈리지 않을 때만
        }

        MapNode boss = new MapNode { Floor = Floors, Column = 2, Type = MapNodeType.Boss, Theme = PathTheme.Normal, SpotX = 0.5f, SpotY = 0.13f };
        map.Floors[Floors] = new List<MapNode> { boss };
        foreach (MapNode n in map.Floors[Floors - 1]) Link(n, boss);
        map.Boss = boss;
        return map;
    }

    static void Link(MapNode a, MapNode b)
    {
        if (a == null || b == null || a.Next.Contains(b)) return;
        a.Next.Add(b);
        b.Prev.Add(a);
    }

    // 3-196: 난이도 (어려움~ 엘리트 가중치 ×2, 매우 어려움~ 모닥불 가중치 ÷2). 부르는 쪽이 정해 줌
    public static Difficulty CurrentDifficulty = Difficulty.Normal;

    public static RunMap Generate(System.Random rng)
    {
        RunMap map = null;
        for (int i = 0; i < GameBalance.MapGenerateTries; i++)
        {
            map = GenerateOnce(rng);
            bool enoughNodes = map.AllNodes().Count - 1 >= GameBalance.MapMinNodes;   // -1 = 보스 칸 빼기
            bool safeOk = !GameBalance.MapRequireSafePath || HasSafePath(map);
            // 3-83: 갈림길(위로 가는 길 2개 이상인 칸)과 출발 칸이 충분한지 → 고를 게 없는 지도를 다시 만듦
            bool branchesOk = CountBranches(map) >= GameBalance.MapMinBranches;
            bool startsOk = map.Floors[0].Count >= GameBalance.MapMinStarts;
            if (enoughNodes && safeOk && branchesOk && startsOk) break;
        }
        return map;
    }

    // 갈림길 수: 보스 바로 아래층을 빼고, 위층으로 가는 길이 2개 이상인 칸 (3-83)
    //   (보스 아래층은 모두 보스로만 가니까 셀 필요 없음)
    public static int CountBranches(RunMap map)
    {
        int count = 0;
        for (int f = 0; f < map.Floors.Length - 2; f++)
            foreach (MapNode n in map.Floors[f])
                if (n.Next.Count >= 2) count++;
        return count;
    }

    // 1층(전부 몬스터)에서 출발해서, 2층부터는 싸움 없는 칸만 밟고 보스까지 갈 수 있는 길이 있는지
    public static bool HasSafePath(RunMap map)
    {
        foreach (MapNode start in map.Floors[0])
        {
            if (SafeFrom(start)) return true;
        }
        return false;
    }

    // n 다음 칸부터 싸움 없이 보스까지 갈 수 있는지 (재귀: 자기 자신을 다시 부르는 메서드)
    static bool SafeFrom(MapNode n)
    {
        foreach (MapNode next in n.Next)
        {
            if (next.Type == MapNodeType.Boss) return true;
            if (!next.IsBattle && SafeFrom(next)) return true;
        }
        return false;
    }

    static RunMap GenerateOnce(System.Random rng)
    {
        int floors = GameBalance.MapFloors - 1;   // 보스를 뺀 층 수 (6)
        int cols = GameBalance.MapColumns;
        MapNode[,] grid = new MapNode[floors, cols];
        HashSet<int> edges = new HashSet<int>();  // 층*100 + 출발줄*10 + 도착줄

        // ---- 1) 길 그리기 ----
        int firstStart = rng.Next(cols);
        for (int p = 0; p < GameBalance.MapPaths; p++)
        {
            int col = rng.Next(cols);
            if (p == 1)
            {
                // 두 번째 길은 첫 번째와 다른 곳에서 시작 (출발지가 최소 2곳)
                while (col == firstStart) col = rng.Next(cols);
            }
            if (p == 0) col = firstStart;
            GetOrCreate(grid, 0, col);

            for (int f = 0; f < floors - 1; f++)
            {
                List<int> options = new List<int> { col - 1, col, col + 1 };
                Shuffle(options, rng);
                int next = -1;
                foreach (int c in options)
                {
                    if (c < 0 || c >= cols) continue;
                    if (Crosses(edges, f, col, c)) continue;
                    next = c;
                    break;
                }
                if (next == -1) next = col;   // 엇갈리지 않는 곳이 없으면 바로 위로
                edges.Add(f * 100 + col * 10 + next);
                GetOrCreate(grid, f + 1, next);
                col = next;
            }
        }

        // ---- 2) 칸 연결 ----
        foreach (int e in edges)
        {
            int f = e / 100, from = (e / 10) % 10, to = e % 10;
            MapNode a = grid[f, from];
            MapNode b = grid[f + 1, to];
            if (!a.Next.Contains(b)) { a.Next.Add(b); b.Prev.Add(a); }
        }

        RunMap map = new RunMap();
        map.Floors = new List<MapNode>[GameBalance.MapFloors];
        for (int f = 0; f < floors; f++)
        {
            map.Floors[f] = new List<MapNode>();
            for (int c = 0; c < cols; c++)
            {
                if (grid[f, c] != null) map.Floors[f].Add(grid[f, c]);
            }
            map.Floors[f].Sort((x, y) => x.Column.CompareTo(y.Column));
        }

        map.Boss = new MapNode();
        map.Boss.Floor = floors;
        map.Boss.Column = cols / 2;
        map.Boss.Type = MapNodeType.Boss;
        map.Floors[floors] = new List<MapNode> { map.Boss };
        foreach (MapNode n in map.Floors[floors - 1])
        {
            n.Next.Add(map.Boss);
            map.Boss.Prev.Add(n);
        }

        // ---- 3) 길의 성격: 위험 1개 이상, 평온 1개 이상, 나머지 무작위 ----
        List<PathTheme> themes = new List<PathTheme> { PathTheme.Danger, PathTheme.Calm };
        while (themes.Count < cols)
        {
            int r = rng.Next(3);
            themes.Add(r == 0 ? PathTheme.Danger : (r == 1 ? PathTheme.Calm : PathTheme.Normal));
        }
        Shuffle(themes, rng);
        map.ColumnThemes = themes.ToArray();

        // ---- 4) 칸 종류 정하기 (아래층부터) ----
        for (int f = 0; f < floors; f++)
        {
            foreach (MapNode n in map.Floors[f])
            {
                n.Theme = map.ColumnThemes[n.Column];
                n.Type = PickType(n, floors, rng);
            }
        }
        return map;
    }

    static MapNode GetOrCreate(MapNode[,] grid, int floor, int col)
    {
        if (grid[floor, col] == null)
        {
            MapNode n = new MapNode();
            n.Floor = floor;
            n.Column = col;
            grid[floor, col] = n;
        }
        return grid[floor, col];
    }

    // floor 층에서 from → to 로 가는 선이 이미 있는 선과 X자로 엇갈리는지
    static bool Crosses(HashSet<int> edges, int floor, int from, int to)
    {
        foreach (int e in edges)
        {
            if (e / 100 != floor) continue;
            int a = (e / 10) % 10, b = e % 10;
            if ((from < a && to > b) || (from > a && to < b)) return true;
        }
        return false;
    }

    static MapNodeType PickType(MapNode n, int floors, System.Random rng)
    {
        int f = n.Floor;
        if (f == 0) return MapNodeType.Monster;                                  // 1층 = 몬스터
        if (GameBalance.RestBeforeBoss && f == floors - 1) return MapNodeType.Rest;  // 보스 아래층 = 모닥불

        int[] weights = n.Theme == PathTheme.Danger ? GameBalance.WeightsDanger
                      : (n.Theme == PathTheme.Calm ? GameBalance.WeightsCalm : GameBalance.WeightsNormal);
        // 3-196 난이도: 원본 배열은 그대로 두고 복사본만 바꿈 (자바의 array.clone())
        if (DifficultyInfo.MoreElites(CurrentDifficulty) || DifficultyInfo.FewerRests(CurrentDifficulty))
        {
            weights = (int[])weights.Clone();
            if (DifficultyInfo.MoreElites(CurrentDifficulty)) weights[(int)MapNodeType.Elite] *= GameBalance.HardEliteWeight;
            if (DifficultyInfo.FewerRests(CurrentDifficulty)) weights[(int)MapNodeType.Rest] /= GameBalance.VeryHardRestDivide;
        }

        for (int tryCount = 0; tryCount < 30; tryCount++)
        {
            MapNodeType t = (MapNodeType)WeightedIndex(weights, rng);
            if (t == MapNodeType.Elite && f + 1 < GameBalance.EliteMinFloor) continue;            // 엘리트는 3층부터
            if (t == MapNodeType.Veteran && f + 1 < GameBalance.VeteranMinFloor) continue;        // 정예는 2층부터 (3-111)
            if (t == MapNodeType.Rest && GameBalance.RestBeforeBoss && f == floors - 2) continue;   // 모닥불층 바로 아래는 모닥불 X
            if (t != MapNodeType.Monster && t != MapNodeType.Veteran && t != MapNodeType.Event && SameAsParent(n, t)) continue; // 특수 칸 연속 X
            return t;
        }
        return MapNodeType.Monster;
    }

    static bool SameAsParent(MapNode n, MapNodeType t)
    {
        foreach (MapNode p in n.Prev)
        {
            if (p.Type == t) return true;
        }
        return false;
    }

    // 가중치 배열에서 하나 뽑기. 예) {45, 10, ...} 이면 0번이 45/전체 확률
    static int WeightedIndex(int[] weights, System.Random rng)
    {
        int total = 0;
        foreach (int w in weights) total += w;
        int r = rng.Next(total);
        for (int i = 0; i < weights.Length; i++)
        {
            if (r < weights[i]) return i;
            r -= weights[i];
        }
        return 0;
    }

    static void Shuffle<T>(List<T> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
}
