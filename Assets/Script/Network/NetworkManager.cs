using System;
using System.Collections;
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

    // Build Settings の順番と一致させる定数
    public const int TITLE_SCENE_INDEX = 0;    // 1,Title
    public const int LOBBY_SCENE_INDEX = 1;    // 2,Lobby
    public const int LOAD_SCENE_INDEX = 2;     // 3,Load
    public const int BASE_SCENE_INDEX = 3;     // 4,Base
    public const int STAGE_SCENE_INDEX = 4;    // 5,Stage
    public const int RESULT_SCENE_INDEX = 5;   // 6,Result
    public const int BOSS_SCENE_INDEX = 6;     // 7,Boss
    public const int GAMEOVER_SCENE_INDEX = 7; // 8,Gameover

    public NetworkRunner Runner { get; private set; }
    private GameObject _runnerObject;
    private Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new Dictionary<PlayerRef, NetworkObject>();

    // ロード画面を経由した後の「本当の行き先」を記憶する変数
    private int _nextTargetSceneIndex = -1;

    // 今回のマッチで選ばれたスタート地点エリアと割り当て済み人数
    private SpawnArea _selectedSpawnArea;
    private int _spawnedPlayerCount = 0;

    public event Action<List<SessionInfo>> OnSessionListChanged;
    public event Action OnPlayerListChanged;

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
        _nextTargetSceneIndex = -1;
    }

    private NetworkRunner CreateNewRunner()
    {
        _runnerObject = new GameObject("SessionRunner");
        DontDestroyOnLoad(_runnerObject);

        var newRunner = _runnerObject.AddComponent<NetworkRunner>();
        newRunner.AddCallbacks(this);
        return newRunner;
    }

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

    public async Task LeaveSessionAndReturnToTitle()
    {
        await ResetRunnerAsync();
        SceneManager.LoadScene(TITLE_SCENE_INDEX);
    }

    public async Task LeaveLobbySearch()
    {
        await ResetRunnerAsync();
    }

    /// <summary>
    /// 【重要】3,Load シーンを経由して指定したシーンへ全員で移動する（ホスト専用）
    /// 例: NetworkManager.Instance.LoadSceneWithLoadingScreen(NetworkManager.BASE_SCENE_INDEX);
    /// </summary>
    public void LoadSceneWithLoadingScreen(int targetSceneIndex)
    {
        if (Runner != null && Runner.IsServer)
        {
            // ロビーからの出発時は途中参加を締め切る
            if (SceneManager.GetActiveScene().buildIndex == LOBBY_SCENE_INDEX)
            {
                Runner.SessionInfo.IsOpen = false;
            }

            // 本当の行き先を保存し、まずは全員で 3,Load (Index 2) へ移動する
            _nextTargetSceneIndex = targetSceneIndex;
            _spawnedCharacters.Clear();
            Runner.LoadScene(SceneRef.FromIndex(LOAD_SCENE_INDEX), LoadSceneMode.Single);
        }
    }

    /// <summary>
    /// プレイヤーキャラクターを出現させるべきプレイ用シーンかどうかを判定
    /// </summary>
    private bool IsGameplayScene(int buildIndex)
    {
        return buildIndex == BASE_SCENE_INDEX ||
               buildIndex == STAGE_SCENE_INDEX ||
               buildIndex == BOSS_SCENE_INDEX;
    }

    #region INetworkRunnerCallbacks 実装

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        OnSessionListChanged?.Invoke(sessionList);
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        OnPlayerListChanged?.Invoke();

        int currentIndex = SceneManager.GetActiveScene().buildIndex;
        if (runner.IsServer && IsGameplayScene(currentIndex))
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

        // 1. ロビー画面に到着した場合
        if (currentBuildIndex == LOBBY_SCENE_INDEX)
        {
            OnPlayerListChanged?.Invoke();
        }
        // 2. ロード中継画面 (3,Load) に到着した場合
        else if (currentBuildIndex == LOAD_SCENE_INDEX)
        {
            if (runner.IsServer && _nextTargetSceneIndex >= 0)
            {
                // ロード画面を確実に描画させてから、本番シーンの読み込みを開始する
                StartCoroutine(ProceedToTargetSceneRoutine());
            }
        }
        // 3. ゲームプレイ用シーン (Base / Stage / Boss) に到着した場合
        else if (runner.IsServer && IsGameplayScene(currentBuildIndex))
        {
            _spawnedCharacters.Clear();
            _spawnedPlayerCount = 0;

            // シーン内の全SpawnAreaを取得し、ランダムに1つ決定する
            SpawnArea[] areas = FindObjectsByType<SpawnArea>(FindObjectsSortMode.None);
            _selectedSpawnArea = (areas.Length > 0)
                ? areas[UnityEngine.Random.Range(0, areas.Length)]
                : null;

            if (_selectedSpawnArea != null)
            {
                Debug.Log($"[Spawn] スタート地点に「{_selectedSpawnArea.name}」が選ばれました");
            }

            foreach (var player in runner.ActivePlayers)
            {
                SpawnPlayerCharacter(runner, player);
            }
        }
    }

    private IEnumerator ProceedToTargetSceneRoutine()
    {
        // ロード画面のUIが表示されるよう少し待機（0.5秒）
        yield return new WaitForSeconds(0.5f);

        int destination = _nextTargetSceneIndex;
        _nextTargetSceneIndex = -1;

        if (Runner != null && Runner.IsServer && destination >= 0)
        {
            Runner.LoadScene(SceneRef.FromIndex(destination), LoadSceneMode.Single);
        }
    }

    private void SpawnPlayerCharacter(NetworkRunner runner, PlayerRef player)
    {
        if (!_spawnedCharacters.ContainsKey(player))
        {
            Vector3 spawnPos;
            Quaternion spawnRot;

            if (_selectedSpawnArea != null)
            {
                var transformData = _selectedSpawnArea.GetSpawnTransform(_spawnedPlayerCount);
                spawnPos = transformData.position;
                spawnRot = transformData.rotation;
            }
            else
            {
                spawnPos = new Vector3((_spawnedPlayerCount % 4) * 2.0f, 1.0f, 0);
                spawnRot = Quaternion.identity;
            }

            NetworkObject networkPlayerObj = runner.Spawn(
                playerPrefab,
                spawnPos,
                spawnRot,
                player,
                onBeforeSpawned: (runner, obj) =>
                {
                    var setup = obj.GetComponent<NetworkPlayerSetup>();
                    if (setup != null)
                    {
                        setup.InitializeSpawnTransform(spawnPos, spawnRot);
                    }
                }
            );

            _spawnedCharacters.Add(player, networkPlayerObj);
            _spawnedPlayerCount++;
        }
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        var data = new NetworkInputData();

        // プレイ用シーンにいる時だけ操作入力を受け付ける（Load画面では動かない）
        if (IsGameplayScene(SceneManager.GetActiveScene().buildIndex))
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