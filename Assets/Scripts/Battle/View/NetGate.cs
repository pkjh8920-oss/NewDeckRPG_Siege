// ============================================================
// NetGate.cs  (3-244 온라인 멀티 2·3차)
//
// 보상·레벨업·보물·상점·대장간·제단 화면이 온라인 명령을 보낼 때 쓰는 작은 문.
//   화면(패널)은 BattleScreen 을 모르니까, 이 static 클래스 하나를 통해서
//   "온라인이면 명령으로 보내고, 아니면 바로 실행" 을 똑같이 처리함
//
// 쓰는 법 (패널 안에서):
//   if (NetGate.Intercept("RWD|0|C|2")) return;   ← 온라인이면 보내고 끝. "DO" 로 돌아오면 그때 진짜 실행
//   ...한 PC 일 때와 "DO" 로 돌아왔을 때 하는 진짜 일...
//
// 값은 BattleScreen 이 매 프레임 맞춰 줌 (Active, Me, IsHost, Away) + 명령을 실행하는 동안 Executing = true
// 자바로 치면: 여러 클래스가 같이 쓰는 static 유틸 + 콜백(Consumer<String>) 하나
// ============================================================

using System.Collections.Generic;

public static class NetGate
{
    public static bool Active;                 // 온라인 게임을 하는 중
    public static int Me;                      // 내 자리 번호 (0 = P1)
    public static bool IsHost;                 // 내가 방장인지
    public static bool Executing;              // "DO" 명령을 실행하는 중 (이때는 다시 보내지 않고 진짜 실행)
    public static readonly HashSet<int> Away = new HashSet<int>();   // 게임 중에 나간 사람
    public static System.Action<string> Sender;                      // 명령 보내기 (BattleScreen 이 넣어 줌)

    // 온라인이면 명령을 보내고 true (= 부른 쪽은 여기서 멈춤). 한 PC 이거나 실행 중이면 false (= 바로 실행)
    public static bool Intercept(string command)
    {
        if (!Active || Executing) return false;
        if (Sender != null) Sender(command);
        return true;
    }

    // 이 자리(slot)를 내 화면에서 조작해도 되는지 (한 PC 면 누구든, 온라인이면 나만)
    public static bool CanControl(int slot)
    {
        return !Active || slot == Me;
    }

    // ▶ 를 다시 누르면 처음으로 (3-242 와 같은 이유)
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Active = false; Me = 0; IsHost = false; Executing = false; Away.Clear(); Sender = null;
    }

    public static bool IsAway(int slot)
    {
        return Active && Away.Contains(slot);
    }
}
