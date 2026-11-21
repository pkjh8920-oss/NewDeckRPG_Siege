// ============================================================
// ClassLibrary.cs
// 클래스(캐릭터) 목록과, 클래스별 플레이어를 만드는 곳입니다.
//
// 클래스마다 다른 것: 기본 HP, 기본 최대 마나, 마나 이름, 기본 크리티컬, 기본 덱
// 새 클래스를 추가할 때는
//   1) PlayerClass 에 한 줄 추가
//   2) CreatePlayer 의 switch 에 case 추가
//   3) CardLibrary 에 카드와 기본 덱 추가
// ============================================================

using System.Collections.Generic;

// 유니티 Inspector 드롭다운에도 그대로 보입니다.
// 순서를 바꾸면 이미 저장된 Inspector 값이 다른 클래스로 바뀌니, 새 클래스는 항상 맨 아래에 추가하세요.
public enum PlayerClass
{
    Common,        // 타격/방어만 (비교용)
    BlackScythe,   // 검은 낫
    CosmicPhoenix, // 코스믹 피닉스
    FrostMage,     // 마도사 (명세서 8-6)
    Sniper = 5     // 스나이퍼 (2026-11-15). 자원 = 탄환
    // 4번 자리는 예전 벼락(Thunder). 지웠으니 다음 새 클래스를 넣을 때 "= 5" 처럼 번호를 정해 주면 안전함
}

public static class ClassLibrary
{
    // 3-169: 캐릭터 고유 색 (이름 글자에 씀). Core 는 UnityEngine 을 안 써서 "#RRGGBB" 글자로 둠 → View 에서 UIFactory.Hex 로 바꿈
    //   투표 아이콘 색과 같은 계열: 카인 보라 / 이그니스 주황빛 빨강 / 제피르 하늘 / 실비아 금빛 노랑
    public static string ClassColorHex(PlayerClass playerClass)
    {
        switch (playerClass)
        {
            case PlayerClass.BlackScythe: return "#C08CFF";
            case PlayerClass.CosmicPhoenix: return "#FF7A50";
            case PlayerClass.FrostMage: return "#6CC8FF";
            case PlayerClass.Sniper: return "#FFD25A";
            default: return "#FFFFFF";
        }
    }

    // 3-169: 색이 칠해진 캐릭터 이름 → "<color=#C08CFF>카인</color>"
    //   uGUI Text 의 리치 텍스트(기본 켜짐)가 <color> 태그를 읽어서 그 부분만 색을 바꿔 줌
    //   (Java 로 치면 HTML 문자열 "<font color=...>카인</font>" 을 JLabel 에 넣는 것과 같은 방식)
    //   "P1" 같은 플레이어 번호는 여기 안 들어감 → 번호는 흰색 그대로
    public static string ColoredClassName(PlayerClass playerClass)
    {
        return "<color=" + ClassColorHex(playerClass) + ">" + ClassName(playerClass) + "</color>";
    }

    public static string ClassName(PlayerClass playerClass)
    {
        switch (playerClass)
        {
            case PlayerClass.BlackScythe: return "카인";            // 3-168: 검은 낫 → 카인
            case PlayerClass.CosmicPhoenix: return "이그니스";      // 3-168: 코스믹 피닉스 → 이그니스
            case PlayerClass.FrostMage: return "제피르";          // 3-168: 마도사 → 제피르
            case PlayerClass.Sniper: return "실비아";             // 3-168: 스나이퍼 → 실비아
            default: return "공통";
        }
    }

    // 직업별 기본 무기 이름 (대장간에서 강화). 피닉스·마도사는 가안
    public static string WeaponName(PlayerClass playerClass)
    {
        switch (playerClass)
        {
            case PlayerClass.BlackScythe: return "사신의 낫";
            case PlayerClass.CosmicPhoenix: return "불꽃 깃털창";
            case PlayerClass.FrostMage: return "서리 지팡이";
            case PlayerClass.Sniper: return "황혼의 저격총";
            default: return "낡은 검";
        }
    }

    // addTestCards: 그 직업의 보상 카드 전부를 덱에 추가 (테스트용. 끄면 시작 덱 10장만)
    public static PlayerCombatant CreatePlayer(PlayerClass playerClass, string name,
                                               int str, int con, int agi,
                                               bool addTestCards)
    {
        int baseHp;
        List<CardData> deck;
        PlayerCombatant player;

        switch (playerClass)
        {
            case PlayerClass.BlackScythe:
                baseHp = GameBalance.BlackScytheMaxHp;
                deck = CardLibrary.BlackScytheStarterDeck();
                if (addTestCards) deck.AddRange(CardLibrary.BlackScytheTestCards());
                break;

            case PlayerClass.CosmicPhoenix:
                baseHp = GameBalance.PhoenixMaxHp;
                deck = CardLibrary.PhoenixStarterDeck();
                if (addTestCards) deck.AddRange(CardLibrary.PhoenixTestCards());
                break;

            case PlayerClass.FrostMage:
                baseHp = GameBalance.FrostMageMaxHp;
                deck = CardLibrary.FrostMageStarterDeck();
                if (addTestCards) deck.AddRange(CardLibrary.FrostMageTestCards());
                break;

            case PlayerClass.Sniper:
                baseHp = GameBalance.SniperMaxHp;
                deck = CardLibrary.SniperStarterDeck();
                if (addTestCards) deck.AddRange(CardLibrary.SniperTestCards());
                break;

            default:
                baseHp = GameBalance.BaseMaxHp;
                deck = CardLibrary.CommonStarterDeck();
                break;
        }

        if (addTestCards) deck.AddRange(CardLibrary.CommonTestCards());

        bool isAP = playerClass == PlayerClass.FrostMage;   // 마법 직업: 체력 1pt당 HP +3 (3-146), 민첩 → 마나
        player = new PlayerCombatant(name, baseHp, str, con, agi, deck, isAP);
        player.Class = playerClass;
        // 3-147: 직업이 정해진 뒤 체력 HP 를 다시 계산 (생성자에선 직업을 몰라 스나이퍼도 +4 로 계산됨)
        player.MaxHp = baseHp + player.Con * player.HpPerCon;
        player.Hp = player.MaxHp;

        // 자원 이름과 최대치 (생성자 뒤에 넣어야 MaxMana 계산에 반영됨)
        switch (playerClass)
        {
            case PlayerClass.BlackScythe:
                player.ManaName = "영혼";
                player.BaseMaxMana = GameBalance.BlackScytheMaxSoul;   // 0/20
                break;

            case PlayerClass.CosmicPhoenix:
                player.ManaName = "홍염";
                player.BaseMaxMana = GameBalance.PhoenixMaxFlame;      // 0/10
                break;

            case PlayerClass.FrostMage:
                player.ManaName = "마나";
                player.BaseMaxMana = GameBalance.FrostMageMaxMana;     // 30 (민첩으로 시작 마나·턴당 회복)
                break;

            case PlayerClass.Sniper:
                player.ManaName = "탄환";
                player.BaseMaxMana = GameBalance.SniperMaxBullets;     // 시작 3 / 최대 6 (민첩 5당 최대 +1)
                break;
        }

        // 직업 시작 아티팩트 (낡은 사신의 표식 / 불새의 깃털 장식 / 서리 수정 목걸이)
        string starter = RelicLibrary.StarterFor(playerClass);
        if (starter != null) player.Relics.Add(RelicLibrary.Get(starter));

        return player;
    }
}
