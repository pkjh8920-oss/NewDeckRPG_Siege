// ============================================================
// CardData.cs
// 카드 한 장의 "정보" 입니다.
//
// 핵심 아이디어: 카드 = 작은 효과 부품(CardEffect)들의 목록
//   타격  = [피해 6] + [마나 +1]
//   방어  = [방어도 5] + [마나 +1]
// 새 카드를 만들 때 새 클래스를 만들 필요 없이, 부품만 골라 끼우면 됩니다.
//
// 등급 (2026-11-09): 기본 / 일반 / 희귀 / 영웅 / 전설
//   기본 = 시작 덱 전용 (보상으로 안 나옴). 나머지는 전투 보상 확률과 카드 테두리 색에 씀
// 강화: 카드마다 "강화판을 만드는 함수"(UpgradeTo)를 들고 있음
//   대장간에서 강화하면 덱의 카드를 UpgradeTo() 로 만든 새 카드로 바꿔 끼움
//   자바로 치면 Supplier<CardData> 를 필드로 들고 있는 것과 같음
// ============================================================

using System.Collections.Generic;

public enum CardType
{
    Attack, // 공격
    Skill,  // 스킬
    Power   // 파워
}

public enum TargetType
{
    SingleEnemy, // 적 1명 선택
    AllEnemies,  // 적 전체 (선택 필요 없음)
    Self         // 나 자신 (선택 필요 없음)
}

public enum DamageKind
{
    AD, // 물리: 힘 보너스, 크리티컬 O, 방어도 관통 O (민첩 × 3% 무시)
    AP  // 마법: 힘 보너스, 크리티컬은 기본값만 (민첩 영향 X), 방어도 관통 X
}

// 카드 등급. 순서가 GameBalance 의 확률표(일반, 희귀, 영웅, 전설)와 맞물려 있으니 바꾸지 마세요
public enum CardRarity
{
    Basic,   // 기본 (시작 덱 전용)
    Common,  // 일반
    Rare,    // 희귀
    Hero,    // 영웅
    Legend   // 전설
}

public class CardData
{
    public string Id;          // 코드에서 구분하는 이름 (영어). 강화해도 같음
    public string Name;        // 화면에 보이는 이름 (강화하면 뒤에 +)
    public CardType Type;
    public int Cost;           // 에너지 소모량
    public int ManaCost;       // 마나(클래스 고유 자원) 소모량. 기본 0
    public bool UsesAllMana;   // true 면 가진 마나를 전부 씀 (최소 1 필요). 영혼 방출 등
    public int MinRound;       // 이 턴부터 손에 나옴 (그 전에는 뽑히지 않음, 0 이면 제한 없음)
    public TargetType Target;
    public string Description; // 카드 설명 글
    public bool Exhaust;       // true 면 사용 후 소멸 (버린 카드 더미로 가지 않음)
    public bool Sealed;        // 3-196 지옥 난이도 "사용 금지" (붉은 사슬). 전투가 시작될 때 무작위로 걸리고 전투가 끝나면 풀림
    public bool Bound;         // 영혼 결속 마녀가 "결속"을 건 카드 (3-102). 이번 턴에 안 내면 마녀가 회복·강화. 턴 끝에 풀림
    public bool BurnsInHand;   // [화상] 카드 (3-102): 턴 종료 때 손에 있으면 HP 를 잃음
    public bool Unplayable;    // true 면 낼 수 없는 카드 (몬스터가 넣는 [상처]. 2026-11-15)
    public bool StatusCard;    // 3-149: 몬스터가 넣는 방해 카드 (상처·화상·포자·점액·공허). 불굴의 투지·정화가 없애는 대상 (점액은 낼 수 있어도 방해 카드)
    public bool Ethereal;      // 휘발성 (3-149 [공허]): 턴이 끝날 때 손에 남아 있으면 소멸 (버린 카드 더미로 안 감)
    public int EnergyLossOnDraw; // 3-149 [공허]: 이 카드를 뽑으면 에너지 -N (0 이면 없음)
    public bool Innate;        // 선천성: 전투 첫 손패에 반드시 들어옴 (기습)
    public bool IsLightning;   // [벼락] 카드 (마도사): 뇌신 권능·천둥 벼락·이온화가 반응
    public bool IsBasicAttack; // 기본 공격 카드 (타격·찌르기·빙결 탄환·스파크): 화염 단련이 반응
    public bool IsBayonet;     // [총검술] 카드 (스나이퍼 근접): 균형 잡힌 대검 코등이가 반응 (2026-11-15)
    public bool IsCharge;      // 차징 카드 (반월참): 화면에서 차징 자세
    public bool IsThrow;       // 던지는 카드 (연막탄·사슬 함정): 화면에서 투척 자세 (2026-11-15, 3-74). 규칙에는 영향 없음
    public bool MultiOnly;     // 멀티 전용 카드: 혼자 할 때는 보상·상점·보물·무한한 잠재력에 안 나옴 (2026-11-15, 3-71)
                               //   예) 화염 장벽 = 동료가 받는 피해를 줄이는 카드라 혼자면 쓸모가 거의 없음
    public int TempDiscount;   // 다음에 쓸 때만 에너지 -N (영혼 봉인). 쓰면 0으로
    public int BattleDiscount; // 이번 전투 동안 에너지 -N (마력 깃든 숫돌). 전투가 시작될 때 0으로
    public List<CardEffect> Effects = new List<CardEffect>();

    // ---- 등급 · 강화 ----
    public CardRarity Rarity = CardRarity.Common;
    public bool Upgraded;                    // 강화된 카드인지 (한 장은 한 번만 강화)
    public System.Func<CardData> UpgradeTo;  // 강화판을 새로 만드는 함수 (CardLibrary 가 넣어 줌)

    // params CardEffect[] effects : 효과를 원하는 개수만큼 쉼표로 넘길 수 있게 해 줌
    // (자바의 CardEffect... effects 가변 인자와 같습니다)
    public CardData(string id, string name, CardType type, int cost, TargetType target,
                    string description, params CardEffect[] effects)
    {
        Id = id;
        Name = name;
        Type = type;
        Cost = cost;
        Target = target;
        Description = description;
        Effects.AddRange(effects);
    }

    public bool CanUpgrade
    {
        get { return !Upgraded && UpgradeTo != null; }
    }

    // 강화판 카드를 새로 만들어 돌려줌 (이 카드는 그대로). 강화할 수 없으면 null
    public CardData MakeUpgraded()
    {
        return CanUpgrade ? UpgradeTo() : null;
    }

    public string TypeName()
    {
        switch (Type)
        {
            case CardType.Attack: return "공격";
            case CardType.Skill: return "스킬";
            case CardType.Power: return "파워";
            default: return Type.ToString();
        }
    }

    public static string RarityName(CardRarity rarity)
    {
        switch (rarity)
        {
            case CardRarity.Basic: return "기본";
            case CardRarity.Common: return "일반";
            case CardRarity.Rare: return "희귀";
            case CardRarity.Hero: return "영웅";
            default: return "전설";
        }
    }
}
