// ============================================================
// SiegeHints.cs
// 수성(1·2스테이지) ??? 칸에 붙는 정찰병 힌트 문장
//
// 3-125 바뀐 점
//   [전투] 일반 / 정예 / 엘리트 중 무엇인지 "헷갈리게" 만듦
//     - 문장마다 "어떤 등급에서 나올 수 있는지" 표시를 붙여 둠 (아래 FightHint)
//     - 한 등급에서만 나오는 문장은 없음 → 문장을 외워도 딱 하나로 못 맞힘
//     - 대신 두 등급 중 하나라는 정도는 짐작할 수 있음 (예: "땅이 울린다" = 정예 아니면 엘리트)
//   [비전투] 대장간·보물·이벤트·초소·상점은 "작은 힌트"만 줌 (소리·냄새·흔적 정도)
//     - 종류마다 문장 5개. 대놓고 "상점입니다" 라고 하지 않음
//
// 문장을 추가하고 싶으면 배열에 한 줄만 넣으면 됨
// ============================================================

public static class SiegeHints
{
    // 전투 힌트 한 줄: 문장 + 이 문장이 나올 수 있는 등급
    //   자바로 치면 record FightHint(String text, boolean normal, boolean veteran, boolean elite)
    struct FightHint
    {
        public string Text;
        public bool N, V, E;   // 일반 / 정예 / 엘리트 전투에서 나올 수 있는지
        public FightHint(string text, bool n, bool v, bool e) { Text = text; N = n; V = v; E = e; }
    }

    static readonly FightHint[] Fights =
    {
        // 셋 다 가능 (완전히 모름)
        new FightHint("무언가 다가오고 있습니다.", true, true, true),
        new FightHint("짐승 냄새가 바람에 실려 옵니다.", true, true, true),
        new FightHint("횃불이 하나둘 꺼졌습니다.", true, true, true),
        new FightHint("발자국이 어지럽게 찍혀 있습니다.", true, true, true),
        new FightHint("성벽 너머에서 비명이 들렸습니다.", true, true, true),

        // 일반 또는 정예
        new FightHint("풀숲이 바스락거립니다.", true, true, false),
        new FightHint("작은 무리의 발소리가 들립니다.", true, true, false),
        new FightHint("쇠붙이가 달그락거리는 소리가 납니다.", true, true, false),
        new FightHint("낮게 으르렁거리는 소리가 몇 번 들립니다.", true, true, false),

        // 정예 또는 엘리트
        new FightHint("땅이 낮게 울립니다.", false, true, true),
        new FightHint("발소리가 줄을 맞춰 다가옵니다.", false, true, true),
        new FightHint("경비병들이 겁에 질려 물러났습니다.", false, true, true),
        new FightHint("저 멀리 적의 깃발이 보입니다.", false, true, true),

        // 일반 또는 엘리트 (가장 헷갈리는 묶음)
        new FightHint("주위가 이상할 만큼 조용합니다.", true, false, true),
        new FightHint("새들이 한꺼번에 날아올랐습니다.", true, false, true),
        new FightHint("커다란 그림자가 스쳐 지나갔습니다.", true, false, true),
    };

    // 전투 힌트 하나를 뽑음. fight = 'N' 일반 / 'V' 정예 / 'E' 엘리트 / 'W' 2웨이브(일반 취급)
    public static string PickFight(char fight, System.Random rng)
    {
        // 1) 이 등급에서 나올 수 있는 문장만 모음 (자바의 stream().filter())
        System.Collections.Generic.List<string> pool = new System.Collections.Generic.List<string>();
        foreach (FightHint h in Fights)
        {
            bool ok = fight == 'E' ? h.E : (fight == 'V' ? h.V : h.N);
            if (ok) pool.Add(h.Text);
        }
        if (pool.Count == 0) pool.Add("무언가 다가오고 있습니다.");
        // 2) 그중 하나를 무작위로
        return "정찰병: \"" + pool[rng.Next(pool.Count)] + "\"";
    }

    // ---------------- 비전투: 작은 힌트만 ----------------
    // 초소 (휴식)
    public static readonly string[] Rest =
    {
        "멀리 모닥불 연기가 가늘게 피어오릅니다.",
        "누군가 낮게 노래를 흥얼거립니다.",
        "아군의 깃발이 바람에 펄럭입니다.",
        "따뜻한 수프 냄새가 납니다.",
        "고요합니다. 발소리 하나 들리지 않습니다.",
    };

    public static readonly string[] Forge =
    {
        "규칙적인 쇳소리가 울립니다.",
        "쇠 달구는 냄새가 납니다.",
        "굴뚝에서 검은 연기가 오릅니다.",
        "누군가 투덜거리며 무언가를 두드립니다.",
        "바닥에 쇳가루가 흩어져 있습니다.",
    };

    public static readonly string[] Shop =
    {
        "작은 방울 소리가 들립니다.",
        "바퀴 자국이 길게 이어져 있습니다.",
        "누군가 동전을 세는 소리가 납니다.",
        "향신료 냄새가 바람에 실려 옵니다.",
        "등불 하나가 흔들리며 다가옵니다.",
    };

    public static readonly string[] Event =
    {
        "낯선 목소리가 들립니다.",
        "길 위에 누군가 서 있습니다.",
        "이상한 노랫소리가 들렸다가 끊겼습니다.",
        "길가에 처음 보는 표식이 새겨져 있습니다.",
        "누군가 이쪽을 지켜보는 느낌이 듭니다.",
    };

    public static readonly string[] Treasure =
    {
        "무언가 반짝입니다.",
        "버려진 수레가 보입니다.",
        "흙 속에 무언가 묻혀 있습니다.",
        "무너진 벽 틈으로 빛이 새어 나옵니다.",
        "까마귀들이 한곳을 맴돌고 있습니다.",
    };

    public static readonly string[] Boss =
    {
        "침공의 우두머리가 모습을 드러냈습니다.",
    };

    // 종류에 맞는 문장 하나를 무작위로 (전투 칸은 RunState.MakeFight 에서 PickFight 로 다시 덮어씀)
    public static string Pick(SiegeChoiceKind kind, MapNodeType type, System.Random rng)
    {
        if (kind == SiegeChoiceKind.EasyFight) return PickFight('N', rng);
        if (kind == SiegeChoiceKind.HardFight) return PickFight('E', rng);

        string[] list;
        if (kind == SiegeChoiceKind.Boss) list = Boss;
        else
        {
            switch (type)
            {
                case MapNodeType.Forge: list = Forge; break;
                case MapNodeType.Shop: list = Shop; break;
                case MapNodeType.Event: list = Event; break;
                case MapNodeType.Treasure: list = Treasure; break;
                default: list = Rest; break;
            }
        }
        return "정찰병: \"" + list[rng.Next(list.Length)] + "\"";
    }
}
