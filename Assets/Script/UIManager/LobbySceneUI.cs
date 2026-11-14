using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbySceneUI : MonoBehaviour
{
    [Header("Lobby UI Elements")]
    [SerializeField] private TextMeshProUGUI roomNameText;
    [SerializeField] private TextMeshProUGUI playerListText;
    [SerializeField] private TextMeshProUGUI statusMessageText; // 「ホストの開始を待っています...」等
    [SerializeField] private Button startGameButton; // ホストのみ表示される開始ボタン
    [SerializeField] private Button leaveLobbyButton; // タイトルへ戻るボタン

    private void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        startGameButton.onClick.AddListener(OnClickStartGame);
        leaveLobbyButton.onClick.AddListener(OnClickLeaveLobby);

        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnPlayerListChanged += RefreshLobbyUI;
        }

        // シーン開始直後に現在の部屋情報とプレイヤー一覧を描画
        RefreshLobbyUI();
    }

    private void OnDestroy()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnPlayerListChanged -= RefreshLobbyUI;
        }
    }

    private void RefreshLobbyUI()
    {
        if (NetworkManager.Instance == null || NetworkManager.Instance.Runner == null) return;

        var runner = NetworkManager.Instance.Runner;
        if (!runner.IsRunning) return;

        // 1. 部屋名の表示
        if (roomNameText != null && runner.SessionInfo.IsValid)
        {
            roomNameText.text = $"部屋名: {runner.SessionInfo.Name}";
        }

        // 2. ホストかクライアントかで「ゲームスタート」ボタンの表示を切り替え
        bool isHost = runner.IsServer;
        startGameButton.gameObject.SetActive(isHost);

        if (statusMessageText != null)
        {
            statusMessageText.text = isHost
                ? "メンバーが揃ったらゲームを開始してください"
                : "ホストがゲームを開始するのを待っています...";
        }

        // 3. 参加メンバー一覧の更新
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        int count = 0;

        foreach (var player in runner.ActivePlayers)
        {
            count++;
            string youTag = (player == runner.LocalPlayer) ? " (あなた)" : "";
            sb.AppendLine($"・Player {player.PlayerId}{youTag}");
        }

        if (playerListText != null)
        {
            playerListText.text = $"参加メンバー ({count}/4人)\n\n{sb.ToString()}";
        }
    }

    private void OnClickStartGame()
    {
        startGameButton.interactable = false;
        // 全員一斉に StageScene (Index 2) へ移動！
        NetworkManager.Instance.LoadStageScene();
    }

    private async void OnClickLeaveLobby()
    {
        leaveLobbyButton.interactable = false;
        await NetworkManager.Instance.LeaveSessionAndReturnToTitle();
    }
}