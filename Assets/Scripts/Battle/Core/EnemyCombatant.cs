// ============================================================
// EnemyCombatant.cs
// 몬스터 한 마리와, 몬스터의 행동 하나(EnemyMove)
//
// 몬스터는 행동 목록(Pattern)을 순서대로 반복합니다.
// 지금 할 행동 = "의도(Intent)" → 화면에 미리 보여 줍니다 (슬더스2와 같음).
//
// 몬스터가 들고 있는 "쌓이는 값" (플레이어 카드가 쌓음)
//   처형 스택 (검은 낫) : 조건을 채우는 순간 즉시 처형
//                         일반·정예 = HP < 스택,  엘리트 = 스택 > HP × 1.2,  보스 = 스택 > HP × 1.5
//   동결 스택 (마도사)  : 기준(일반 3 / 정예 4 / 엘리트 5 / 보스 7)에 닿으면 [동상] → 이번 적 차례 행동 취소
//                         터지면 스택 0. 엘리트·보스는 터질 때마다 기준 +1
//   감전 스택 (마도사)  : 4개 모이면 [감전] 2턴 (주는 피해 -20%), 스택 0. 감전 중에는 안 쌓임
// ============================================================

using System.Collections.Generic;

// 몬스터 등급: 처형 조건, 동결 기준이 다름
public enum EnemyRank
{
    Normal,   // 일반 (슬라임, 박쥐)
    Veteran,  // 정예 (병정, 골렘)
    Elite,    // 엘리트 (감시자, 감시자 무리)
    Boss      // 보스 (수문장, 설계자)
}

// 몬스터 행동 하나. 공격, 방어도, 약화·취약, 강화, 회복을 섞을 수 있음
public class EnemyMove
{
    public string Name;
    public int Damage;           // 1회 피해 (0이면 공격 안 함)
    public int Hits;             // 공격 횟수
    public int Block;            // 얻는 방어도
    public int PlayerVulnerable; // 플레이어 전원에게 거는 취약 턴 수
    public int PlayerWeak;       // 플레이어 전원에게 거는 약화 턴 수
    public int AllyBlock;        // 동료 전원에게 주는 방어도 (동료가 없으면 자기에게)
    public int Strength;         // 힘: 이 행동 뒤로 공격 1회당 피해가 영구히 +Strength
    public int Heal;             // 자기 HP 회복
    // ---- 2026-11-15 슬더스2 처럼 다양한 디버프·버프 ----
    public int PlayerFrail;      // 플레이어 전원에게 손상 (얻는 방어도 -25%) 턴 수
    public int PlayerBurn;       // 플레이어 전원에게 발화 스택 (내 턴 시작에 그만큼 피해, 1씩 줄어듦)
    public int AddWounds;        // 플레이어마다 [상처] 카드를 버린 카드 더미에 N장 (쓸 수 없는 카드, 전투 끝나면 사라짐)
    public int PlayerDrawDown;   // 플레이어 전원 다음 턴 뽑는 카드 -N
    public int GainThorns;       // 자기에게 가시 +N (공격 카드로 때린 사람이 N 피해) - 영구
    public int GainRitual;       // 자기에게 의식 +N (적 차례가 끝날 때마다 힘 +N) - 영구
    // ---- 3-102 새 몬스터 ----
    public int AllyStrength;     // 살아 있는 동료 전원 힘 +N (고블린 대장, 사냥개 조련사)
    public int AddBurnCards;     // 플레이어마다 [화상] 카드를 버린 카드 더미에 N장
    public int AddSlimeCards;    // 3-149: 플레이어마다 [점액] 카드를 버린 카드 더미에 N장
    public int AddVoidCards;     // 3-149: 플레이어마다 [공허] 카드를 버린 카드 더미에 N장
    public bool SelfDestruct;    // 이 행동 뒤 스스로 쓰러짐 (자폭 불씨 정령)
    public bool DamageFromBlock; // 피해 = 지금 남은 방어도 (최소 Damage). 쓰고 나면 방어도 0 (뼈조각 수집가)
    // 만들 때 new EnemyMove(...) { PlayerWeak = 1, Heal = 5 } 처럼 씀 (C# 객체 초기화 문법)

    // vulnerable = 0 처럼 "= 값" 이 붙은 매개변수는 생략 가능 (자바라면 생성자를 여러 개 만들어야 함)
    public EnemyMove(string name, int damage, int hits, int block, int vulnerable = 0)
    {
        Name = name;
        Damage = damage;
        Hits = hits;
        Block = block;
        PlayerVulnerable = vulnerable;
    }
}

public class EnemyCombatant : Combatant
{
    public List<EnemyMove> Pattern = new List<EnemyMove>();
    public int MoveIndex;                 // 지금 몇 번째 행동인지
    public float BlockMultiplier = 1f;    // 인원수 × 스테이지 스케일링
    public float AttackMultiplier = 1f;   // 스테이지 스케일링 (인원수로는 안 오름)
    public EnemyRank Rank = EnemyRank.Normal;
    public bool IsBoss => Rank == EnemyRank.Boss;
    public bool IsElite => Rank == EnemyRank.Elite;
    public int Strength;                  // 힘 (행동으로 쌓임). 공격 1회당 +Strength
    public int Marks;                     // [표식] (스나이퍼, 최대 10): 1개당 이 적이 받는 공격 피해 +1. 전투 동안 유지
    public int Thorns;                    // 가시: 공격 카드로 이 몬스터를 때리면 때린 사람이 Thorns 만큼 피해 (2026-11-15)
    public int Ritual;                    // 의식: 적 차례가 끝날 때마다 힘 +Ritual (2026-11-15, 슬더스 광신도)

    // ---- 3-102 새 몬스터용 ----
    public string Tag = "";               // 특수 규칙이 있는 몬스터 구분 (EnemyLibrary 의 T_ 상수)
    public int GroupId;                   // 함께 나온 무리 번호 (감시자 무리의 SquadId 와 달리 분노 없음)
    public int HitsThisRound;             // 이번 플레이어 차례에 공격 카드로 맞은 횟수 (딱정벌레·석상 파수꾼)
    public int PendingBlock;              // 다음 적 차례 시작에 얻을 방어도 (투구 딱정벌레)
    public bool Stunned;                  // 기절: 다음 행동 1번 취소 (과부하, 외골격 파괴)
    public bool OverloadUsed;             // 이번 차례에 과부하가 이미 터졌는지
    public int ShellHp, ShellMax;         // 외골격 (심연의 거대 껍질). 0 이면 없음
    public int ReviveTimer;               // 쓰러진 뒤 부활까지 남은 적 차례 수 (0 = 부활 안 함)
    public float ReviveHpRatio = 1f;      // 부활할 때 HP 비율
    public int SwarmBonus;                // 점액 유충: 살아 있는 유충 수만큼 공격력 +
    public bool SelfDestructed;           // 자폭으로 쓰러졌는지 (아티팩트 처치 보너스 없음)
    public bool SplitPending;             // 점토 분열체: 분열 대기 중

    // 강화 페이즈 (보스용). Phase2Pattern 이 null 이면 강화 없음
    public List<EnemyMove> Phase2Pattern;
    public float EnrageHpRatio;           // 0.5 = HP 50% 이하에서 강화
    public int EnrageBlock;               // 강화 순간 얻는 방어도
    public bool IsEnraged;

    // 무리 엘리트용: 같은 SquadId 끼리 한 팀. 동료가 쓰러지면 남은 놈의 공격력이 오름
    public int SquadId;
    public int RageStacks;
    public bool DeathHandled;             // 쓰러진 것을 이미 처리했는지 (분노·처치 효과가 두 번 걸리지 않게)
    public bool DeathLogged;              // "처치!" 기록을 이미 남겼는지

    // ---- 검은 낫 ----
    public int ExecuteStacks;             // 처형 스택
    public bool WasExecuted;              // 처형으로 쓰러졌는지 (화면 연출용)
    public PlayerCombatant SentencedBy;   // 사형 선고를 건 사람 (이 적이 처형되면 그 사람 HP 회복)
    public PlayerCombatant ShroudBy;      // 사신의 수의 [표식]을 건 사람 (이번 턴 안에 쓰러지면 보상)
    public PlayerCombatant LastHitBy;     // 마지막으로 이 적을 때린/스택을 쌓은 사람 (처형 기록용)

    // ---- 피닉스 ----
    public int BrandTurns;                // 낙인: 남은 적 차례 수 동안 받는 발화 피해 2배 (낙인 1, 낙인+ 2)

    // ---- 마도사 ----
    public int FreezeStacks;              // 동결 스택
    public int FreezeThreshold;           // 동상까지 필요한 스택 (등급으로 시작값, 엘리트·보스는 터질 때마다 +1)
    public int ShockStacks;               // 감전 스택 (4개면 감전 상태)
    public bool BlockLocked;              // 만년설: 이번 적 차례 동안 방어도 0 고정
    public bool HalfDamage;               // 빙하절단(보스): 이번 적 차례 공격 피해 50% 감소
    public int FreezeImmuneTurns;         // 보스 빙결 면역: 남은 적 차례 수 (0 이면 면역 아님)

    // 스킬·특수 행동인지 (순수한 공격이 아닌 것)
    public static bool IsSpecialMove(EnemyMove move)
    {
        return move.Damage <= 0 || move.Block > 0 || move.PlayerVulnerable > 0 || move.PlayerWeak > 0
            || move.AllyBlock > 0 || move.Strength > 0 || move.Heal > 0
            || IsDebuffMove(move) || move.GainThorns > 0 || move.GainRitual > 0 || move.AllyStrength > 0 || move.SelfDestruct;
    }

    // 플레이어에게 나쁜 효과를 거는 행동인지 (의도 그림의 보라 물방울)
    public static bool IsDebuffMove(EnemyMove move)
    {
        return move.PlayerVulnerable > 0 || move.PlayerWeak > 0 || move.PlayerFrail > 0 || move.PlayerBurn > 0
            || move.AddWounds > 0 || move.PlayerDrawDown > 0 || move.AddBurnCards > 0
            || move.AddSlimeCards > 0 || move.AddVoidCards > 0;   // 3-149
    }

    public EnemyCombatant(string name, int maxHp, List<EnemyMove> pattern, EnemyRank rank = EnemyRank.Normal)
    {
        Name = name;
        MaxHp = maxHp;
        Hp = maxHp;
        Pattern = pattern;
        MoveIndex = 0;
        SetRank(rank);
    }

    public void SetRank(EnemyRank rank)
    {
        Rank = rank;
        switch (rank)
        {
            case EnemyRank.Veteran: FreezeThreshold = GameBalance.FreezeNeedVeteran; break;
            case EnemyRank.Elite: FreezeThreshold = GameBalance.FreezeNeedElite; break;
            case EnemyRank.Boss: FreezeThreshold = GameBalance.FreezeNeedBoss; break;
            default: FreezeThreshold = GameBalance.FreezeNeedNormal; break;
        }
    }

    // 현재 의도 (다음 적 턴에 할 행동)
    public EnemyMove Intent
    {
        get { return Pattern[MoveIndex % Pattern.Count]; }  // % 로 목록 끝에 가면 처음으로
    }

    public void AdvanceIntent()
    {
        MoveIndex++;
    }

    public int ScaledBlock(EnemyMove move)
    {
        return DamageCalculator.RoundHalfUp(move.Block * BlockMultiplier);
    }

    // 스테이지 배율 + 힘이 적용된 1회 공격력 (취약·약화 같은 상태는 DamageCalculator 가 따로 계산)
    public int ScaledDamage(EnemyMove move)
    {
        if (move.Damage <= 0) return 0;
        // 뼈조각 수집가 (3-102): 남은 방어도만큼 (방어도는 이미 배율이 붙어 있어서 공격 배율은 안 곱함)
        if (move.DamageFromBlock) return System.Math.Max(DamageCalculator.RoundHalfUp(move.Damage * AttackMultiplier), Block + Strength);
        return DamageCalculator.RoundHalfUp((move.Damage + Strength + SwarmBonus) * AttackMultiplier);
    }

    // 처형 조건을 채웠는지
    // bonus (3-99): 심연의 사신 낫이 있으면 처형 스택을 그만큼 많게 쳐서 판정 (실제 스택 수는 그대로)
    public bool ExecuteReady(int bonus = 0)
    {
        if (IsDead || ExecuteStacks <= 0) return false;
        int stacks = ExecuteStacks + bonus;
        switch (Rank)
        {
            case EnemyRank.Boss: return stacks > Hp * GameBalance.ExecuteRatioBoss;
            case EnemyRank.Elite: return stacks > Hp * GameBalance.ExecuteRatioElite;
            default: return Hp < stacks;
        }
    }

    // 화면 표시용 의도 글자. 예) "연쇄 물기: 공격 3x2"
    public string IntentText()
    {
        EnemyMove move = Intent;
        List<string> parts = new List<string>();

        if (move.Damage > 0)
        {
            string attack = "공격 " + ScaledDamage(move);
            if (move.Hits > 1) attack += "x" + move.Hits;
            parts.Add(attack);
        }
        if (move.Block > 0) parts.Add("방어도 " + ScaledBlock(move));
        if (move.AllyBlock > 0) parts.Add("동료 방어도 " + DamageCalculator.RoundHalfUp(move.AllyBlock * BlockMultiplier));
        if (move.PlayerVulnerable > 0) parts.Add("취약 부여");
        if (move.PlayerWeak > 0) parts.Add("약화 부여");
        if (move.PlayerFrail > 0) parts.Add("손상 부여");
        if (move.PlayerBurn > 0) parts.Add("발화 " + move.PlayerBurn);
        if (move.AddWounds > 0) parts.Add("상처 카드 " + move.AddWounds + "장");
        if (move.PlayerDrawDown > 0) parts.Add("다음 턴 카드 -" + move.PlayerDrawDown);
        if (move.GainThorns > 0) parts.Add("가시 +" + move.GainThorns);
        if (move.GainRitual > 0) parts.Add("의식 +" + move.GainRitual);
        if (move.Strength > 0) parts.Add("힘 +" + move.Strength);
        if (move.Heal > 0) parts.Add("회복 " + move.Heal);
        if (move.AllyStrength > 0) parts.Add("동료 힘 +" + move.AllyStrength);
        if (move.AddBurnCards > 0) parts.Add("화상 카드 " + move.AddBurnCards + "장");
        if (move.AddSlimeCards > 0) parts.Add("점액 카드 " + move.AddSlimeCards + "장");   // 3-149
        if (move.AddVoidCards > 0) parts.Add("공허 카드 " + move.AddVoidCards + "장");
        if (move.DamageFromBlock) parts.Add("(남은 방어도만큼)");
        if (move.SelfDestruct) parts.Add("자폭!");
        if (Stunned) return "기절 — 이번 행동 취소";
        return move.Name + ": " + string.Join(" + ", parts);
    }

    // 상태 글자 + 쌓인 스택 (스택은 1 이상일 때만 보임)
    //   예) "취약 1, 처형 12, 동결 2/5, 감전 3/4"
    public override string StatusText()
    {
        List<string> parts = new List<string>();
        string baseText = base.StatusText();
        if (baseText.Length > 0) parts.Add(baseText);
        if (ExecuteStacks > 0) parts.Add("처형 " + ExecuteStacks);
        if (FreezeStacks > 0) parts.Add("동결 " + FreezeStacks + "/" + FreezeThreshold);
        if (ShockStacks > 0) parts.Add("감전 " + ShockStacks + "/" + GameBalance.ShockStacksNeeded);
        if (Strength > 0) parts.Add("힘 " + Strength);
        if (Thorns > 0) parts.Add("가시 " + Thorns);
        if (Marks > 0) parts.Add("표식 " + Marks);
        if (Ritual > 0) parts.Add("의식 " + Ritual);
        if (ShellHp > 0) parts.Add("외골격 " + ShellHp);
        if (Stunned) parts.Add("기절");
        if (IsDead && ReviveTimer > 0) parts.Add("부활까지 " + ReviveTimer);
        return string.Join(", ", parts);
    }
}
