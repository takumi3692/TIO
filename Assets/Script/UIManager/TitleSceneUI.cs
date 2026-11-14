using System.Collections.Generic;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TitleSceneUI : MonoBehaviour
{
    [Header("Panels (タイトル画面の2パネル)")]
    [SerializeField] private GameObject titlePanel;
    [SerializeField] private GameObject serverSelectPanel;

    [Header("TitlePanel UI")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button openServerSelectButton;
    [SerializeField] private Button quitGameButton;

    [Header("ServerSelectPanel UI")]
    [SerializeField] private Transform roomListContainer; // ScrollViewのContent
    [SerializeField] private RoomListItemUI roomListItemPrefab;
    [SerializeField] private TextMeshProUGUI serverStatusText;
    [SerializeField] private Button serverBackButton;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        hostButton.onClick.AddListener(OnClickHost);
        openServerSelectButton.onClick.AddListener(OnClickOpenServerSelect);
        quitGameButton.onClick.AddListener(OnClickQuitGame);
        serverBackButton.onClick.AddListener(OnClickBackToTitle);

        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnSessionListChanged += UpdateServerListUI;
        }

        ShowPanel(titlePanel);
    }

    private void OnDestroy()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnSessionListChanged -= UpdateServerListUI;
        }
    }

    private void ShowPanel(GameObject targetPanel)
    {
        titlePanel.SetActive(targetPanel == titlePanel);
        serverSelectPanel.SetActive(targetPanel == serverSelectPanel);
    }

    private async void OnClickHost()
    {
        SetTitleButtonsInteractable(false);

        // 部屋名を生成して作成（完了すると自動で LobbyScene に遷移します）
        string autoRoomName = $"Room_{Random.Range(1000, 9999)}";
        await NetworkManager.Instance.CreateHostAndGoToLobby(autoRoomName);

        SetTitleButtonsInteractable(true);
    }

    private async void OnClickOpenServerSelect()
    {
        SetTitleButtonsInteractable(false);
        ShowPanel(serverSelectPanel);

        ClearRoomList();
        if (serverStatusText != null) serverStatusText.text = "部屋を検索中...";

        await NetworkManager.Instance.JoinLobbyForServerList();

        SetTitleButtonsInteractable(true);
    }

    private void OnClickQuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void UpdateServerListUI(List<SessionInfo> sessionList)
    {
        ClearRoomList();

        if (sessionList.Count == 0)
        {
            if (serverStatusText != null) serverStatusText.text = "現在参加できる部屋がありません";
            return;
        }

        if (serverStatusText != null) serverStatusText.text = $"{sessionList.Count} 件の部屋が見つかりました";

        foreach (var session in sessionList)
        {
            RoomListItemUI item = Instantiate(roomListItemPrefab, roomListContainer);
            item.Setup(session, OnClickJoinRoomFromList);
        }
    }

    private async void OnClickJoinRoomFromList(string roomName)
    {
        if (serverStatusText != null) serverStatusText.text = $"{roomName} に参加中...";

        // 参加完了するとホストがいる LobbyScene に自動で遷移します
        await NetworkManager.Instance.JoinRoomAndGoToLobby(roomName);
    }

    private void ClearRoomList()
    {
        foreach (Transform child in roomListContainer)
        {
            Destroy(child.gameObject);
        }
    }

    private async void OnClickBackToTitle()
    {
        await NetworkManager.Instance.LeaveLobbySearch();
        ShowPanel(titlePanel);
    }

    private void SetTitleButtonsInteractable(bool interactable)
    {
        hostButton.interactable = interactable;
        openServerSelectButton.interactable = interactable;
        quitGameButton.interactable = interactable;
    }
}