using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class NetworkManager : MonoBehaviour, INetworkRunnerCallbacks
{
    public static NetworkManager Instance { get; private set; }

    [Header("Network Prefabs")]
    [SerializeField] private NetworkPrefabRef playerPrefab;

    // シーンのBuild Index定義
    public const int TITLE_SCENE_INDEX = 0;
    public const int LOBBY_SCENE_INDEX = 1;
    public const int STAGE_SCENE_INDEX = 2;

    public NetworkRunner Runner { get; private set; }
    private GameObject _runnerObject; // Runner専用の独立オブジェクト
    private Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new Dictionary<PlayerRef, NetworkObject>();

    // UI側へ状態変化を通知するためのイベント
    public event Action<List<SessionInfo>> OnSessionListChanged;
    public event Action OnPlayerListChanged;

    // 視点回転用
    private float _yaw;
    private float _pitch;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 既存のRunner専用オブジェクトを安全にシャットダウン・破棄する
    /// </summary>
    private async Task ResetRunnerAsync()
    {
        if (Runner != null)
        {
            Runner.RemoveCallbacks(this);
            await Runner.Shutdown(destroyGameObject: true);
            Runner = null;
        }

        if (_runnerObject != null)
        {
            Destroy(_runnerObject);
            _runnerObject = null;
        }

        _spawnedCharacters.Clear();
    }

    /// <summary>
    /// 新しい専用GameObject上に真っさらなNetworkRunnerを生成する
    /// </summary>
    private NetworkRunner CreateNewRunner()
    {
        _runnerObject = new GameObject("SessionRunner");
        DontDestroyOnLoad(_runnerObject);

        var newRunner = _runnerObject.AddComponent<NetworkRunner>();
        newRunner.AddCallbacks(this);
        return newRunner;
    }

    /// <summary>
    /// サーバー一覧を取得するためにロビーへ接続する（「参加する」ボタン用）
    /// </summary>
    public async Task JoinLobbyForServerList()
    {
        await ResetRunnerAsync();

        Runner = CreateNewRunner();

        var result = await Runner.JoinSessionLobby(SessionLobby.ClientServer);
        if (!result.Ok)
        {
            Debug.LogError($"[Fusion] ロビー接続失敗: {result.ShutdownReason}");
        }
    }

    /// <summary>
    /// ホストとして部屋を作成し、自動で LobbyScene (Index 1) へ遷移する
    /// </summary>
    public async Task CreateHostAndGoToLobby(string roomName)
    {
        await ResetRunnerAsync();

        Runner = CreateNewRunner();
        Runner.ProvideInput = true;

        var sceneInfo = new NetworkSceneInfo();
        sceneInfo.AddSceneRef(SceneRef.FromIndex(LOBBY_SCENE_INDEX), LoadSceneMode.Single);

        var sceneManager = _runnerObject.AddComponent<NetworkSceneManagerDefault>();

        var result = await Runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.Host,
            SessionName = roomName,
            PlayerCount = 4,
            Scene = sceneInfo,
            SceneManager = sceneManager
        });

        if (!result.Ok)
        {
            Debug.LogError($"[Fusion] ホスト作成失敗: {result.ShutdownReason}");
        }
    }

    /// <summary>
    /// クライアントとして部屋に参加する（ホストがいる LobbyScene へ自動で同期遷移する）
    /// </summary>
    public async Task JoinRoomAndGoToLobby(string roomName)
    {
        await ResetRunnerAsync();

        Runner = CreateNewRunner();
        Runner.ProvideInput = true;

        var sceneManager = _runnerObject.AddComponent<NetworkSceneManagerDefault>();

        var result = await Runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.Client,
            SessionName = roomName,
            SceneManager = sceneManager
        });

        if (!result.Ok)
        {
            Debug.LogError($"[Fusion] 部屋参加失敗: {result.ShutdownReason}");
        }
    }

    /// <summary>
    /// ロビー画面から「戻る」を押したとき、通信を切断して TitleScene (Index 0) に戻る
    /// </summary>
    public async Task LeaveSessionAndReturnToTitle()
    {
        await ResetRunnerAsync();
        SceneManager.LoadScene(TITLE_SCENE_INDEX);
    }

    /// <summary>
    /// サーバー選択画面からロビー検索だけ解除してタイトルパネルに戻る用
    /// </summary>
    public async Task LeaveLobbySearch()
    {
        await ResetRunnerAsync();
    }

    /// <summary>
    /// ホストが呼び出し、全プレイヤーを一括で StageScene (Index 2) へ遷移させる
    /// </summary>
    public void LoadStageScene()
    {
        if (Runner != null && Runner.IsServer)
        {
            Runner.SessionInfo.IsOpen = false;
            Runner.LoadScene(SceneRef.FromIndex(STAGE_SCENE_INDEX), LoadSceneMode.Single);
        }
    }

    #region INetworkRunnerCallbacks 実装

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        OnSessionListChanged?.Invoke(sessionList);
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        OnPlayerListChanged?.Invoke();

        if (runner.IsServer && SceneManager.GetActiveScene().buildIndex == STAGE_SCENE_INDEX)
        {
            SpawnPlayerCharacter(runner, player);
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        OnPlayerListChanged?.Invoke();

        if (runner.IsServer && _spawnedCharacters.TryGetValue(player, out NetworkObject networkObject))
        {
            runner.Despawn(networkObject);
            _spawnedCharacters.Remove(player);
        }
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        int currentBuildIndex = SceneManager.GetActiveScene().buildIndex;

        if (currentBuildIndex == LOBBY_SCENE_INDEX)
        {
            OnPlayerListChanged?.Invoke();
        }
        else if (runner.IsServer && currentBuildIndex == STAGE_SCENE_INDEX)
        {
            foreach (var player in runner.ActivePlayers)
            {
                SpawnPlayerCharacter(runner, player);
            }
        }
    }

    private void SpawnPlayerCharacter(NetworkRunner runner, PlayerRef player)
    {
        if (!_spawnedCharacters.ContainsKey(player))
        {
            Vector3 spawnPos = new Vector3((player.RawEncoded % 4) * 2.0f, 1.0f, 0);
            NetworkObject networkPlayerObj = runner.Spawn(playerPrefab, spawnPos, Quaternion.identity, player);
            _spawnedCharacters.Add(player, networkPlayerObj);
        }
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        var data = new NetworkInputData();

        if (SceneManager.GetActiveScene().buildIndex == STAGE_SCENE_INDEX)
        {
            if (Keyboard.current != null)
            {
                Vector3 direction = Vector3.zero;
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) direction.z += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) direction.z -= 1f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) direction.x -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) direction.x += 1f;

                data.direction = direction;
                data.jump = Keyboard.current.spaceKey.isPressed;
            }

            if (Mouse.current != null && Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 mouseDelta = Mouse.current.delta.ReadValue() * 0.15f;
                _yaw += mouseDelta.x;
                _pitch -= mouseDelta.y;
                _pitch = Mathf.Clamp(_pitch, -89f, 89f);
            }

            data.yaw = _yaw;
            data.pitch = _pitch;
        }

        input.Set(data);
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ReadOnlySpan<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    #endregion
}