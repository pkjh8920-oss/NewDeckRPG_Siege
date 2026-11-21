// ============================================================
// Difficulty.cs  (3-196 난이도)
// 난이도 5단계와 단계별 배율. 숫자는 전부 GameBalance 의 Easy… Hard… VeryHard… Hell… 값
//
//   쉬움       : 몬스터 체력 -15% · 공격력 -10% · 휴식 회복 40% · 골드 +20% · 필요 경험치 -15%
//   보통       : 지금 그대로 (기준)
//   어려움     : 엘리트 2배 자주 (수성은 3칸부터 엘리트) · 골드 -15% · 필요 경험치 +15%
//   매우 어려움: + 휴식처 감소 (모닥불 절반, 초소 선택 제외) · 일반 몬스터 체력 +10% · 정예·엘리트 체력 +15%
//   지옥       : + 보스 체력 +20% · 전투마다 카드 사용 금지 (스테이지 수만큼, 덱의 1/4 까지) · 상점 가격 +20%
//
// 자바로 치면 enum Difficulty + static 메서드만 있는 DifficultyInfo 유틸 클래스
//   (d >= Difficulty.Hard) 처럼 순서로 비교 = 자바의 d.ordinal() >= HARD.ordinal()
// 이 파일은 UnityEngine 을 쓰지 않습니다.
// ============================================================

public enum Difficulty
{
    Easy,      // 쉬움
    Normal,    // 보통 (기본)
    Hard,      // 어려움
    VeryHard,  // 매우 어려움
    Hell       // 지옥
}

public static class DifficultyInfo
{
    public static string Name(Difficulty d)
    {
        switch (d)
        {
            case Difficulty.Easy: return "쉬움";
            case Difficulty.Hard: return "어려움";
            case Difficulty.VeryHard: return "매우 어려움";
            case Difficulty.Hell: return "지옥";
            default: return "보통";
        }
    }

    // 난이도 화면 설명 (줄마다 하나)
    public static string Description(Difficulty d)
    {
        switch (d)
        {
            case Difficulty.Easy:
                return "몬스터 체력 -15%\n몬스터 공격력 -10%\n휴식 회복 30% → 40%\n골드 획득 +20%\n레벨업 조금 빠름 (필요 경험치 -15%)";
            case Difficulty.Hard:
                return "엘리트가 2배 자주 나옴 (수성 엘리트는 3칸부터)\n골드 획득 -15%\n레벨업 조금 느림 (필요 경험치 +15%)";
            case Difficulty.VeryHard:
                return "[어려움] 효과 모두 +\n휴식처 감소 (모닥불 절반, 초소 선택 없음)\n일반 몬스터 체력 +10%\n정예·엘리트 체력 +15%";
            case Difficulty.Hell:
                return "[매우 어려움] 효과 모두 +\n보스 체력 +20%\n전투마다 카드 사용 금지 (1스테이지 1장 ~ 4스테이지 4장)\n상점 가격 +20%";
            default:
                return "기본 난이도\n처음 하는 분께 추천합니다.";
        }
    }

    // 몬스터 체력 배율 (등급마다 다름)
    public static float EnemyHp(Difficulty d, EnemyRank rank)
    {
        if (d == Difficulty.Easy) return GameBalance.EasyEnemyHp;
        float m = 1f;
        if (d >= Difficulty.VeryHard)
        {
            if (rank == EnemyRank.Normal) m *= GameBalance.VeryHardNormalHp;
            else if (rank == EnemyRank.Veteran || rank == EnemyRank.Elite) m *= GameBalance.VeryHardEliteHp;
        }
        if (d >= Difficulty.Hell && rank == EnemyRank.Boss) m *= GameBalance.HellBossHp;
        return m;
    }

    public static float EnemyAttack(Difficulty d) { return d == Difficulty.Easy ? GameBalance.EasyEnemyAttack : 1f; }
    public static float RestHeal(Difficulty d) { return d == Difficulty.Easy ? GameBalance.EasyRestHeal : GameBalance.RestHealRatio; }
    public static float Gold(Difficulty d) { return d == Difficulty.Easy ? GameBalance.EasyGold : (d >= Difficulty.Hard ? GameBalance.HardGold : 1f); }
    public static float Exp(Difficulty d) { return d == Difficulty.Easy ? GameBalance.EasyExp : (d >= Difficulty.Hard ? GameBalance.HardExp : 1f); }
    public static float ShopPrice(Difficulty d) { return d >= Difficulty.Hell ? GameBalance.HellShopPrice : 1f; }
    public static bool MoreElites(Difficulty d) { return d >= Difficulty.Hard; }
    public static bool FewerRests(Difficulty d) { return d >= Difficulty.VeryHard; }

    // 지옥: 이번 전투에서 사용 금지할 카드 수 = 스테이지 수, 단 덱의 1/4 까지
    public static int SealCount(Difficulty d, int stage, int deckCount)
    {
        if (d < Difficulty.Hell) return 0;
        return System.Math.Max(0, System.Math.Min(stage, deckCount / GameBalance.HellSealDeckDivide));
    }
}
