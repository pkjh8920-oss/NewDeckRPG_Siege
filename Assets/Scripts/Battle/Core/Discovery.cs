// ============================================================
// Discovery.cs  (3-180 도감)
// "한 번이라도 본 카드·아티팩트" 기록 → 도감에서 본 것만 공개 (슬더스 방식, 안 본 것은 ???)
//
// 본 것으로 치는 때 (화면에 한 번이라도 나온 것):
//   카드      = 내 덱·손패·더미, 전투 보상 후보, 상점, 보물 상자, 제단 후보, 몬스터가 넣은 방해 카드
//   아티팩트  = 가진 것, 보스 보상 후보, 상점, 보물 상자, 엘리트·보물 보상
//
// 이 파일은 Core 라서 UnityEngine 을 안 씀 → "저장"은 화면 쪽 DiscoveryStore(PlayerPrefs)가 함
//   Core 는 아이디 목록만 들고 있고, 새로 본 게 생기면 Dirty = true 로 알려 줌
//   (자바로 치면 static HashSet<String> 두 개를 가진 유틸 클래스 + "바뀜" 깃발)
//
// HashSet = 같은 값을 두 번 넣어도 하나만 남는 묶음. Add 가 true 면 "처음 넣음" (자바의 HashSet.add 와 같음)
// ============================================================

using System.Collections.Generic;

public static class Discovery
{
    private static readonly HashSet<string> cards = new HashSet<string>();
    private static readonly HashSet<string> relics = new HashSet<string>();

    // 새로 본 게 생겼는지. 화면 쪽이 저장하고 나서 false 로 돌림
    public static bool Dirty;

    // 개발자용 "도감 전부 보기" (저장 안 함, 이번 실행 동안만). 켜면 안 본 것도 다 보임
    public static bool RevealAll;

    public static bool CardSeen(string id) { return RevealAll || (id != null && cards.Contains(id)); }
    public static bool RelicSeen(string id) { return RevealAll || (id != null && relics.Contains(id)); }

    public static void SeeCard(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (cards.Add(id)) Dirty = true;   // 처음 본 카드면 저장 필요
    }

    public static void SeeRelic(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (relics.Add(id)) Dirty = true;
    }

    static void SeeCards(List<CardData> list)
    {
        if (list == null) return;
        foreach (CardData c in list) if (c != null) SeeCard(c.Id);
    }

    static void SeeItems(List<ShopItem> list)
    {
        if (list == null) return;
        foreach (ShopItem it in list)
        {
            if (it == null) continue;
            if (it.Card != null) SeeCard(it.Card.Id);
            if (it.Relic != null) SeeRelic(it.Relic.Id);
        }
    }

    // 지금 런에서 화면에 나와 있을 수 있는 것들을 한 번에 훑어서 기록
    //   화면 쪽(BattleScreen)이 0.5초마다 부름. 목록이 작아서(수백 개) 아주 가벼움
    //   여기저기 "본 순간"마다 코드를 끼워 넣는 것보다 빠뜨릴 일이 적음
    public static void Scan(RunState run)
    {
        if (run == null) return;
        foreach (PlayerCombatant p in run.Players)
        {
            SeeCards(p.Deck);
            SeeCards(p.Hand);
            SeeCards(p.DrawPile);
            SeeCards(p.DiscardPile);
            SeeCards(p.ExhaustPile);
            SeeCards(p.PowersInPlay);
            foreach (RelicData r in p.Relics) SeeRelic(r.Id);
        }
        foreach (PlayerReward rw in run.Rewards)
        {
            SeeCards(rw.CardChoices);
            SeeCards(rw.TakenCards);
            if (rw.Relic != null) SeeRelic(rw.Relic.Id);
            if (rw.BossChoices != null) foreach (RelicData r in rw.BossChoices) SeeRelic(r.Id);
        }
        foreach (KeyValuePair<PlayerCombatant, List<ShopItem>> kv in run.ShopStock) SeeItems(kv.Value);
        SeeItems(run.TreasureItems);
    }

    // ---------------- 저장용 글자 바꾸기 ----------------
    // "id1,id2,id3" 처럼 쉼표로 이은 한 줄 (아이디에는 쉼표가 없음)
    public static string CardsText() { return string.Join(",", new List<string>(cards).ToArray()); }
    public static string RelicsText() { return string.Join(",", new List<string>(relics).ToArray()); }

    public static void Load(string cardText, string relicText)
    {
        cards.Clear();
        relics.Clear();
        AddAll(cards, cardText);
        AddAll(relics, relicText);
        Dirty = false;
    }

    static void AddAll(HashSet<string> set, string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        foreach (string id in text.Split(','))
            if (id.Length > 0) set.Add(id);
    }

    // 개발자용: 기록 전부 지우기 (도감을 처음 상태로)
    public static void Clear()
    {
        cards.Clear();
        relics.Clear();
        Dirty = true;
    }

    // ---------------- 도감에 보여 줄 목록 ----------------
    // 직업 카드 = 시작 덱 카드(같은 카드는 1장만) + 보상으로 나오는 그 직업 카드
    public static List<CardData> ClassCards(PlayerClass pc)
    {
        List<CardData> list = new List<CardData>();
        HashSet<string> ids = new HashSet<string>();
        List<CardData> starter;
        switch (pc)
        {
            case PlayerClass.BlackScythe: starter = CardLibrary.BlackScytheStarterDeck(); break;
            case PlayerClass.CosmicPhoenix: starter = CardLibrary.PhoenixStarterDeck(); break;
            case PlayerClass.FrostMage: starter = CardLibrary.FrostMageStarterDeck(); break;
            case PlayerClass.Sniper: starter = CardLibrary.SniperStarterDeck(); break;
            default: starter = new List<CardData>(); break;
        }
        foreach (CardData c in starter) if (ids.Add(c.Id)) list.Add(c);
        foreach (CardData c in CardLibrary.ClassPool(pc)) if (ids.Add(c.Id)) list.Add(c);
        return list;
    }

    // 몬스터가 넣는 방해 카드 (상처·화상·점액·공허·포자)
    public static List<CardData> StatusCards()
    {
        return new List<CardData> { CardLibrary.Wound(), CardLibrary.BurnCard(), CardLibrary.SlimeCard(), CardLibrary.VoidCard(), CardLibrary.SporeCard() };
    }

    // 아티팩트가 나오는 곳 (도감 설명용). 규칙(RunState)과 같은 내용을 글로 적은 것
    public static string RelicSource(RelicData r)
    {
        switch (r.Rarity)
        {
            case RelicRarity.Starter: return "그 캐릭터로 시작하면 처음부터 가지고 있음";
            case RelicRarity.Common:
            case RelicRarity.Special: return "엘리트 전투 · 보물 상자 · 상점 · 피의 제단 · 정예 전투(" + GameBalance.VeteranRelicChance + "%)";
            case RelicRarity.Rare: return "엘리트 전투 · 보물 상자 · 상점 · 피의 제단 · 정예 전투(" + GameBalance.VeteranRelicChance + "%)";
            case RelicRarity.Hero: return "엘리트 전투 · 보물 상자 · 상점 · 피의 제단";
            case RelicRarity.Boss: return "보스를 쓰러뜨리면 3개 중 1개 선택";
            case RelicRarity.Party: return "보물 상자 (멀티 전용, 파티에 1개씩)";
            default: return "";
        }
    }
}
