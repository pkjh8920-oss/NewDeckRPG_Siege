// ============================================================
// PartyVote.cs
// 멀티 투표 (2026-11-15, 슬더스2 멀티 방식)
//   지도 칸 / 수성 선택지 카드를 고를 때 파티원이 한 명씩 표를 던짐
//   모두 던지면: 다 같은 곳이면 바로 그곳, 갈리면 "룰렛" = 표 수만큼 확률 (3표 중 2표면 2/3 확률)
//
// UnityEngine 을 쓰지 않는 규칙 클래스 (화면은 BattleScreen 이 그림)
// 자바로 치면: Object[] votes 배열 하나를 들고 있는 평범한 클래스
// ============================================================

using System.Collections.Generic;

public class PartyVote
{
    // Votes[사람 번호] = 그 사람이 고른 것 (MapNode 또는 SiegeChoice). null = 아직 안 던짐
    public readonly object[] Votes;

    public PartyVote(int players)
    {
        Votes = new object[players];
    }

    // 다음에 투표할 사람 번호 (모두 던졌으면 -1). P1 → P2 → P3 → P4 순서
    public int NextVoter
    {
        get
        {
            for (int i = 0; i < Votes.Length; i++) if (Votes[i] == null) return i;
            return -1;
        }
    }

    public bool AllVoted { get { return NextVoter < 0; } }

    public void Cast(int player, object option)
    {
        if (player < 0 || player >= Votes.Length || option == null) return;
        Votes[player] = option;
    }

    // 이 선택지에 몇 표가 들어왔는지
    public int CountFor(object option)
    {
        int n = 0;
        foreach (object v in Votes) if (v == option) n++;
        return n;
    }

    // 결과 정하기. split = 표가 갈렸는지 (갈렸으면 화면에서 룰렛 연출)
    //   "무작위로 한 사람을 뽑아서 그 사람 표" = 표 수에 비례한 확률과 같음
    public object Resolve(System.Random rng, out bool split)
    {
        List<object> cast = new List<object>();
        foreach (object v in Votes) if (v != null) cast.Add(v);
        split = false;
        if (cast.Count == 0) return null;
        foreach (object v in cast) if (v != cast[0]) split = true;
        return cast[rng.Next(cast.Count)];
    }
}
