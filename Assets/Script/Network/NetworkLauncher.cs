using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem; // 先頭に追加

public class NetworkLauncher : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("Network Prefabs")]
    [SerializeField] private NetworkPrefabRef playerPrefab; // インスペクターからPlayerPrefabを設定

    private NetworkRunner _runner;
    private Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new Dictionary<PlayerRef, NetworkObject>();
    private float _yaw;
    private float _pitch;

    public async Task StartGame(GameMode mode, string roomName)
    {
        if (_runner == null)
        {
            _runner = gameObject.AddComponent<NetworkRunner>();
        }

        _runner.ProvideInput = true;
        _runner.AddCallbacks(this);

        var activeScene = SceneManager.GetActiveScene();
        var sceneInfo = new NetworkSceneInfo();
        if (activeScene.buildIndex >= 0)
        {
            sceneInfo.AddSceneRef(SceneRef.FromIndex(activeScene.buildIndex), LoadSceneMode.Single);
        }

        var result = await _runner.StartGame(new StartGameArgs()
        {
            GameMode = mode,
            SessionName = roomName,
            PlayerCount = 4,
            Scene = sceneInfo,
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        });

        if (!result.Ok)
        {
            Debug.LogError($"[Fusion] 接続失敗: {result.ShutdownReason}");
        }
    }

    private void OnGUI()
    {
        if (_runner == null)
        {
            if (GUI.Button(new Rect(30, 30, 180, 45), "Host で部屋作成"))
            {
                _ = StartGame(GameMode.Host, "CoopRoom1");
            }

            if (GUI.Button(new Rect(30, 85, 180, 45), "Client で参加"))
            {
                _ = StartGame(GameMode.Client, "CoopRoom1");
            }
        }
    }

    #region INetworkRunnerCallbacks 実装
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        // ホストのみが全プレイヤーのキャラを生成する権限を持つ
        if (runner.IsServer)
        {
            // スポーン位置をプレイヤーIDに応じて少しずつずらす
            Vector3 spawnPosition = new Vector3((player.RawEncoded % 4) * 2.0f, 1.0f, 0);

            // ネットワークオブジェクトのスポーン
            NetworkObject networkPlayerObject = runner.Spawn(playerPrefab, spawnPosition, Quaternion.identity, player);

            // 退室時の削除管理用に保持
            _spawnedCharacters.Add(player, networkPlayerObject);
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        // 退室したプレイヤーのキャラを削除
        if (runner.IsServer && _spawnedCharacters.TryGetValue(player, out NetworkObject networkObject))
        {
            runner.Despawn(networkObject);
            _spawnedCharacters.Remove(player);
        }
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        var data = new NetworkInputData();

        // 1. 移動入力 (WASD)
        if (Keyboard.current != null)
        {
            Vector3 direction = Vector3.zero;

            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
                direction.z += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
                direction.z -= 1f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                direction.x -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                direction.x += 1f;

            data.direction = direction;
            data.jump = Keyboard.current.spaceKey.isPressed;
        }

        // 2. 視点入力 (マウスの回転積算)
        if (Mouse.current != null && Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue() * 0.15f;
            _yaw += mouseDelta.x;
            _pitch -= mouseDelta.y;
            _pitch = Mathf.Clamp(_pitch, -89f, 89f); // 上下角度の制限
        }

        data.yaw = _yaw;
        data.pitch = _pitch;

        input.Set(data);
    }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ReadOnlySpan<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    #endregion
}