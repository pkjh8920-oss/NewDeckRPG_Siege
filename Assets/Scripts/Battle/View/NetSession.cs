// ============================================================
// NetSession.cs  (3-236 온라인 멀티 1차)
//
// 방 코드로 다른 컴퓨터와 연결하는 통신 담당.
//   방장(Host) = 방을 만든 사람. 모든 명령의 "순서"를 정해서 모두에게 다시 보냄
//   참가자(Client) = 방 코드로 들어온 사람
//
// 연결 방법: Unity Relay (Unity 가 중간에서 이어 주는 서버, 무료 범위 안)
//   방장: Relay 에 자리를 만들고 → 방 코드(6글자)를 받음
//   참가자: 그 방 코드로 Relay 에 들어감
//   웹(itch.io)에서도 되도록 웹소켓(wss)으로 연결
//
// 동기화 방식 = "명령만 주고받기" (락스텝)
//   게임 규칙(Core)은 seed 가 같고 같은 명령을 같은 순서로 넣으면 결과가 항상 같음
//   그래서 "P2 가 손패 3번 카드를 적 0번에게 사용" 같은 짧은 글자만 보내면 됨
//   ① 누가 뭘 하면 → 방장에게 "REQ|명령" 을 보냄 (방장 자신은 바로 ②로)
//   ② 방장이 받은 순서대로 모두에게 "DO|명령" 을 보내고, 자기 화면에서도 실행
//   ③ 참가자는 "DO|명령" 을 받은 순서대로 실행 → 모든 화면이 같은 결과
//
// 자바로 치면: 소켓 서버가 받은 메시지를 모든 클라이언트에 broadcast 하는 채팅 서버와 같은 구조
// ============================================================

using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;

public class NetSession : MonoBehaviour
{
    const string MsgName = "deck";            // 우리 게임 메시지 이름 (아무 글자나 되지만 양쪽이 같아야 함)
    const string ConnectionType = "wss";      // 웹소켓 보안 연결 (웹·PC 모두 가능)

    static NetSession instance;
    NetworkManager nm;
    UnityTransport utp;
    bool registered;                          // 메시지 받는 함수를 등록했는지
    bool leaving;                             // 내가 스스로 나가는 중 (끊김 알림을 띄우지 않음)
    bool connecting;                          // 3-237: 참가자가 방장에게 연결하는 중
    float connectTimer;

    // ---- 밖에서 읽는 값 ----
    public static bool Online { get { return instance != null && instance.nm != null && instance.nm.IsListening && (instance.nm.IsHost || instance.nm.IsConnectedClient); } }
    public static bool IsHost { get { return Online && instance.nm.IsHost; } }
    public static int LocalSlot = 0;          // 나는 P 몇 번인지 (0 = P1 = 방장)
    public static int PlayerCount = 1;        // 방 인원
    public static string Code = "";           // 방 코드
    public static int LastSlot = -1;          // 3-268: 끊기기 전에 내가 몇 번 자리였는지 (다시 들어갈 때 같은 자리를 달라고 함)
    public static string LastCode = "";       // 3-249: 마지막으로 있던 방 코드 (끊긴 뒤 다시 참가할 때 입력칸에 미리 넣어 줌)
    public static bool GameStarted;           // 게임이 시작됐는지 (시작 뒤에는 새로 못 들어옴)
    public static bool Busy;                  // 연결 중 (버튼 연타 막기)
    public static bool LastFailFull;          // 3-251: 마지막 방 만들기가 "방이 꽉 차서" 실패했는지

    // 3-251 방 개수 제한: 방마다 Unity Lobby 에 "방 1개 열림"을 등록해서 셈 (방 코드는 지금처럼 Relay 코드)
    string lobbyId;
    float heartbeatTimer;

    // ---- 밖에서 넣어 주는 함수 ----
    public static System.Action<string> OnCommand;       // "DO" 로 온 명령 (모든 화면이 같은 순서로 받음)
    public static System.Action OnRoomChanged;            // 인원·내 번호가 바뀜
    public static System.Action<string> OnClosed;         // 연결이 끊김 (이유 문장)
    public static System.Action<int> OnReplay;            // 3-249 다시 들어오기: 0 = 기록 받기 시작, 1 = 기록 다 받음

    // 3-249 다시 들어오기 (방장만 씀)
    //   게임 시작(START)부터 모든 명령을 적어 둠 → 끊겼던 사람이 코드로 다시 들어오면 이 기록을 통째로 보냄
    //   받은 쪽은 처음부터 빠르게 다시 계산 → 지금 상태와 똑같아짐 (같은 seed + 같은 명령 순서 = 같은 결과)
    public static readonly List<string> Log = new List<string>();
    static bool logging;
    readonly HashSet<int> awaySlots = new HashSet<int>();
    readonly HashSet<ulong> pendingRejoin = new HashSet<ulong>();   // 3-268: 다시 들어왔는데 아직 자리를 안 정한 사람   // 게임 중에 끊긴 자리 (다시 들어올 수 있는 자리)

    // 방장만: 자리 번호 → 연결 번호 (자리 0 = 방장 자신)
    readonly List<ulong> slots = new List<ulong>();

    // 3-242: ▶ 를 다시 누르면 통신 상태도 처음으로 (지난 플레이의 "연결 중" 표시가 남아서 방을 못 만드는 일 방지)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
        LocalSlot = 0;
        LastSlot = -1;
        PlayerCount = 1;
        Code = "";
        GameStarted = false;
        Busy = false;
        OnCommand = null;
        OnRoomChanged = null;
        OnClosed = null;
        OnReplay = null;
        Log.Clear();
        logging = false;
    }

    static NetSession Ensure()
    {
        if (instance != null) return instance;
        GameObject go = new GameObject("NetSession");
        go.SetActive(false);   // 설정을 다 넣은 뒤에 켜야 NetworkManager 가 빈 설정으로 시작하지 않음
        instance = go.AddComponent<NetSession>();
        instance.utp = go.AddComponent<UnityTransport>();
        instance.nm = go.AddComponent<NetworkManager>();
        if (instance.nm.NetworkConfig == null) instance.nm.NetworkConfig = new NetworkConfig();
        instance.nm.NetworkConfig.NetworkTransport = instance.utp;
        instance.nm.NetworkConfig.EnableSceneManagement = false;
        instance.utp.DisconnectTimeoutMS = 10000;   // 3-237: 창을 그냥 닫아도 10초 안에 "나갔다"고 알아챔 (기본 30초)   // 장면을 다시 불러오지 않게 (우리는 장면 하나만 씀)
        instance.nm.OnClientConnectedCallback += instance.HandleConnected;
        instance.nm.OnClientDisconnectCallback += instance.HandleDisconnected;
        instance.nm.OnTransportFailure += instance.HandleTransportFailure;
        go.SetActive(true);
        DontDestroyOnLoad(go);
        return instance;
    }

    // Unity 서비스 시작 + 익명 로그인 (계정 없이 자동, 무료)
    static async Task SignIn()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized) await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    // ==================== 방 만들기 (방장) ====================
    //   done(성공했는지, 방 코드 또는 실패 이유)
    public static async void Host(System.Action<bool, string> done)
    {
        if (Busy) return;
        Busy = true;
        LastFailFull = false;
        NetSession s = Ensure();
        try
        {
            await SignIn();
            // 3-251: 지금 열린 방이 너무 많으면 만들지 않음 (Lobby 가 안 되면 그냥 만듦)
            int open = await CountOpenRooms();
            if (open >= GameBalance.NetMaxRooms)
            {
                Busy = false;
                LastFailFull = true;
                done(false, "지금은 방이 꽉 찼어요 (최대 " + GameBalance.NetMaxRooms + "개). 잠시 후 다시 시도해 주세요");
                return;
            }
            Allocation a = await RelayService.Instance.CreateAllocationAsync(GameBalance.NetMaxPlayers - 1);   // 방장 빼고 3명
            string code = await RelayService.Instance.GetJoinCodeAsync(a.AllocationId);
            s.utp.SetRelayServerData(AllocationUtils.ToRelayServerData(a, ConnectionType));
            s.utp.UseWebSockets = true;
            s.leaving = false;
            if (!s.nm.StartHost()) { Busy = false; done(false, "방을 열지 못했습니다"); return; }
            s.Register();
            s.slots.Clear();
            s.slots.Add(s.nm.LocalClientId);
            LocalSlot = 0;
            PlayerCount = 1;
            Code = code;
            GameStarted = false;
            Busy = false;
            s.RegisterRoom();   // 3-251: 방 1개 열림 등록 (기다리지 않음)
            done(true, code);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[온라인] 방 만들기 실패: " + e);
            Busy = false;
            done(false, "온라인 연결 실패 (인터넷·Unity 서비스 연결을 확인하세요)");
        }
    }

    // ==================== 방 코드로 참가 ====================
    public static async void Join(string code, System.Action<bool, string> done)
    {
        if (Busy) return;
        Busy = true;
        NetSession s = Ensure();
        try
        {
            await SignIn();
            JoinAllocation a = await RelayService.Instance.JoinAllocationAsync(code);
            s.utp.SetRelayServerData(AllocationUtils.ToRelayServerData(a, ConnectionType));
            s.utp.UseWebSockets = true;
            s.leaving = false;
            if (!s.nm.StartClient()) { Busy = false; done(false, "방에 들어가지 못했습니다"); return; }
            s.Register();
            s.connecting = true;
            s.connectTimer = 15f;
            Code = code;
            LocalSlot = -1;          // 방장이 "SLOT|번호" 를 보내 주면 정해짐
            PlayerCount = 0;
            GameStarted = false;
            Busy = false;
            done(true, code);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[온라인] 참가 실패: " + e);
            Busy = false;
            done(false, "[" + code + "] 방을 찾지 못했습니다 (코드를 확인하세요)");
        }
    }

    // ---------------- 3-251 방 개수 세기 (Unity Lobby) ----------------
    // 열린 방 수. Lobby 가 안 되면(설정 안 함·오류) -1 → 제한 없이 만듦
    static async Task<int> CountOpenRooms()
    {
        try
        {
            QueryResponse r = await LobbyService.Instance.QueryLobbiesAsync(new QueryLobbiesOptions { Count = 100 });
            return r.Results != null ? r.Results.Count : 0;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[온라인] 방 개수를 못 셌어요 (제한 없이 만듭니다): " + e.Message);
            return -1;
        }
    }

    async void RegisterRoom()
    {
        try
        {
            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync("DeckRPG", GameBalance.NetMaxPlayers, new CreateLobbyOptions { IsPrivate = false });
            if (nm == null || !nm.IsHost) { await LobbyService.Instance.DeleteLobbyAsync(lobby.Id); return; }   // 그새 방을 닫았으면 바로 지움
            lobbyId = lobby.Id;
            heartbeatTimer = GameBalance.NetLobbyHeartbeat;
        }
        catch (System.Exception e) { Debug.LogWarning("[온라인] 방 등록 실패 (게임은 그대로 됨): " + e.Message); }
    }

    async void UnregisterRoom()
    {
        string id = lobbyId;
        lobbyId = null;
        if (string.IsNullOrEmpty(id)) return;
        try { await LobbyService.Instance.DeleteLobbyAsync(id); }
        catch (System.Exception e) { Debug.LogWarning("[온라인] 방 등록 지우기 실패 (30초 뒤 저절로 지워짐): " + e.Message); }
    }

    // 방장: 15초마다 "아직 열려 있음" 알림 (안 보내면 30초 뒤 Lobby 가 저절로 지움 → 비정상 종료에도 안전)
    async void Heartbeat()
    {
        try { if (!string.IsNullOrEmpty(lobbyId)) await LobbyService.Instance.SendHeartbeatPingAsync(lobbyId); }
        catch (System.Exception e) { Debug.LogWarning("[온라인] 방 알림 실패: " + e.Message); }
    }

    // 나가기 (방장이 나가면 방이 사라짐)
    public static void Leave()
    {
        if (instance == null || instance.nm == null) return;
        instance.UnregisterRoom();   // 3-251
        instance.leaving = true;
        instance.connecting = false;
        if (instance.nm.IsListening) instance.nm.Shutdown();
        instance.registered = false;
        instance.slots.Clear();
        instance.awaySlots.Clear();
        instance.pendingRejoin.Clear();
        Log.Clear();
        logging = false;
        if (!string.IsNullOrEmpty(Code)) LastCode = Code;
        if (LocalSlot > 0 && GameStarted) LastSlot = LocalSlot;   // 3-268: 게임 중에 끊긴 참가자 → 다시 들어갈 때 이 자리를 달라고 함
        LocalSlot = 0;
        PlayerCount = 1;
        Code = "";
        GameStarted = false;
        Busy = false;
    }

    // ==================== 명령 보내기 ====================
    // 게임 명령 (모두가 같은 순서로 실행해야 하는 것). 방장이면 바로 모두에게, 참가자면 방장에게 부탁
    public static void Send(string command)
    {
        if (!Online) return;
        if (instance.nm.IsHost) instance.Broadcast(command);
        else instance.SendTo(NetworkManager.ServerClientId, "REQ|" + command);
    }

    // 방장: 모두에게 "DO|명령" + 내 화면에서도 실행
    void Broadcast(string command)
    {
        if (command.StartsWith("START|")) { Log.Clear(); logging = true; awaySlots.Clear(); }
        if (logging) Log.Add(command);
        foreach (ulong id in nm.ConnectedClientsIds)
            if (id != nm.LocalClientId) SendTo(id, "DO|" + command);
        if (OnCommand != null) OnCommand(command);
    }

    void SendTo(ulong clientId, string text)
    {
        using (FastBufferWriter w = new FastBufferWriter(256, Allocator.Temp, 65536))
        {
            w.WriteValueSafe(text);
            nm.CustomMessagingManager.SendNamedMessage(MsgName, clientId, w, NetworkDelivery.ReliableFragmentedSequenced);
        }
    }

    void Register()
    {
        if (registered) return;
        nm.CustomMessagingManager.RegisterNamedMessageHandler(MsgName, HandleMessage);
        registered = true;
    }

    // ==================== 받기 ====================
    void HandleMessage(ulong sender, FastBufferReader reader)
    {
        string text;
        reader.ReadValueSafe(out text);
        int bar = text.IndexOf('|');
        string head = bar < 0 ? text : text.Substring(0, bar);
        string body = bar < 0 ? "" : text.Substring(bar + 1);

        if (nm.IsHost)
        {
            // 방장은 참가자의 부탁만 받음 → 순서를 정해서 모두에게
            if (head == "REQ") Broadcast(body);
            else if (head == "WANT")   // 3-268: 다시 들어온 사람이 "나는 원래 P몇 이었다" 고 알려 줌
            {
                int want;
                if (pendingRejoin.Contains(sender) && int.TryParse(body, out want)) AssignRejoin(sender, want);
            }
            return;
        }
        switch (head)
        {
            case "DO":
                if (OnCommand != null) OnCommand(body);
                break;
            case "SLOT":
                connecting = false;   // 방장이 번호를 보내 줌 = 연결 완료
                LocalSlot = int.Parse(body);
                if (OnRoomChanged != null) OnRoomChanged();
                break;
            case "ROOM":
                PlayerCount = int.Parse(body);
                if (OnRoomChanged != null) OnRoomChanged();
                break;
            case "FULL":
                Close("방이 꽉 찼거나 이미 게임이 시작됐습니다");
                break;
            case "RPLB":   // 3-249: 다시 들어오기 기록 시작
                connecting = false;
                if (OnReplay != null) OnReplay(0);
                break;
            case "RPLC":   // 기록 한 묶음 (줄바꿈으로 나뉜 명령들)
                if (OnCommand != null) foreach (string c in body.Split('\n')) if (c.Length > 0) OnCommand(c);
                break;
            case "RPLE":   // 기록 끝
                if (OnReplay != null) OnReplay(1);
                break;
        }
    }

    // 방장: 누가 들어옴
    void HandleConnected(ulong clientId)
    {
        // 3-268 참가자: 방장에게 연결됐으면 "예전 자리" 를 알려 줌 (같은 방에 다시 들어온 경우만)
        if (!nm.IsHost)
        {
            if (clientId == nm.LocalClientId && LastSlot > 0 && LastCode == Code)
                SendTo(NetworkManager.ServerClientId, "WANT|" + LastSlot);
            return;
        }
        if (clientId == nm.LocalClientId) return;
        // 3-249: 게임 중인데 끊긴 자리가 있으면 → 그 자리로 다시 들어오기
        //   3-268: 3·4명일 때 두 명 이상 끊기면 엉뚱한 자리(남의 캐릭터)로 들어갈 수 있었음
        //          → 잠깐(1초) 기다려서 "WANT|예전 자리" 가 오면 그 자리, 안 오면 가장 앞 번호 자리
        if (GameStarted && logging && awaySlots.Count > 0)
        {
            pendingRejoin.Add(clientId);
            StartCoroutine(RejoinTimeout(clientId));
            return;
        }
        if (GameStarted || slots.Count >= GameBalance.NetMaxPlayers)
        {
            SendTo(clientId, "FULL");
            StartCoroutine(KickLater(clientId));
            return;
        }
        slots.Add(clientId);
        SendSlots();
    }

    // 3-268: 1초 안에 WANT 가 안 오면 (새로 온 사람 등) 가장 앞 번호 빈자리로
    System.Collections.IEnumerator RejoinTimeout(ulong clientId)
    {
        yield return new WaitForSecondsRealtime(1f);
        if (pendingRejoin.Contains(clientId)) AssignRejoin(clientId, -1);
    }

    // 다시 들어온 사람에게 자리를 정해 줌 (want = 원하는 자리, 끊긴 자리가 아니면 가장 앞 번호)
    void AssignRejoin(ulong clientId, int want)
    {
        if (!pendingRejoin.Remove(clientId)) return;   // 이미 정함
        if (awaySlots.Count == 0)
        {
            // 그새 다른 사람이 마지막 자리를 가져감 → 들어올 자리가 없음
            SendTo(clientId, "FULL");
            StartCoroutine(KickLater(clientId));
            return;
        }
        int slot;
        if (awaySlots.Contains(want)) slot = want;
        else { slot = int.MaxValue; foreach (int a in awaySlots) if (a < slot) slot = a; }
        awaySlots.Remove(slot);
        if (slot < slots.Count) slots[slot] = clientId;
        SendTo(clientId, "SLOT|" + slot);
        SendTo(clientId, "ROOM|" + slots.Count);
        SendReplay(clientId);
        Broadcast("BACK|" + slot);   // 모두에게 (다시 들어온 사람도 기록 다음에 받음): 그 자리 자동 진행 끄기
    }

    // 기록을 묶음(약 1500글자)으로 나눠서 보냄 (한 번에 너무 크면 못 보냄)
    void SendReplay(ulong clientId)
    {
        SendTo(clientId, "RPLB");
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (string c in Log)
        {
            if (sb.Length > 0 && sb.Length + c.Length > 1500) { SendTo(clientId, "RPLC|" + sb); sb.Length = 0; }
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(c);
        }
        if (sb.Length > 0) SendTo(clientId, "RPLC|" + sb);
        SendTo(clientId, "RPLE");
    }

    System.Collections.IEnumerator KickLater(ulong clientId)
    {
        yield return new WaitForSecondsRealtime(0.5f);   // FULL 글자가 먼저 도착하게 잠깐 기다림
        if (nm != null && nm.IsHost) nm.DisconnectClient(clientId);
    }

    // 모두에게 자기 번호와 인원 알려 주기
    void SendSlots()
    {
        PlayerCount = slots.Count;
        for (int i = 1; i < slots.Count; i++)
        {
            SendTo(slots[i], "SLOT|" + i);
            SendTo(slots[i], "ROOM|" + slots.Count);
        }
        if (OnRoomChanged != null) OnRoomChanged();
    }

    // 누가 나감 (방장: 참가자가 나감 / 참가자: 내가 끊김)
    void HandleDisconnected(ulong clientId)
    {
        if (nm.IsHost)
        {
            pendingRejoin.Remove(clientId);   // 3-268: 자리를 정하기 전에 다시 끊김 → 그 사람 몫 자리를 잡아 두지 않음
            int slot = slots.IndexOf(clientId);
            if (slot < 0) return;
            if (!GameStarted)
            {
                // 방에서 기다리는 중: 자리를 당겨서 다시 번호 매김
                slots.RemoveAt(slot);
                SendSlots();
            }
            else
            {
                // 게임 중에 나감 → 남은 사람끼리 계속 (3-249: 같은 코드로 다시 들어오면 이 자리로)
                if (logging) awaySlots.Add(slot);
                Broadcast("LEFT|" + slot);
            }
            return;
        }
        // 참가자 쪽: 방장과 연결이 끊김
        if (!leaving) Close("방장과 연결이 끊겼습니다");
    }

    // 3-237: 참가자가 방장에게 연결을 못 하면 (실패 알림이 안 올 때도 있어서 직접 확인)
    void Update()
    {
        // 3-251: 방장이면 방 등록이 살아 있게 알림
        if (!string.IsNullOrEmpty(lobbyId))
        {
            heartbeatTimer -= Time.unscaledDeltaTime;
            if (heartbeatTimer <= 0f) { heartbeatTimer = GameBalance.NetLobbyHeartbeat; Heartbeat(); }
        }
        if (!connecting || leaving || nm == null) return;
        connectTimer -= Time.unscaledDeltaTime;
        bool dropped = !nm.IsListening && !nm.ShutdownInProgress;
        if (dropped || connectTimer <= 0f)
        {
            connecting = false;
            Close("방장에게 연결하지 못했습니다 (방장이 방을 닫았거나 통신이 막혔어요)");
        }
    }

    void HandleTransportFailure()
    {
        if (!leaving) Close("통신 오류로 연결이 끊겼습니다");
    }

    void Close(string reason)
    {
        Leave();
        if (OnClosed != null) OnClosed(reason);
    }
}
