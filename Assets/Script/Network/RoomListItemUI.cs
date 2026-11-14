using System;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoomListItemUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI roomNameText;
    [SerializeField] private TextMeshProUGUI playerCountText;
    [SerializeField] private Button joinButton;

    private string _roomName;
    private Action<string> _onClickJoinCallback;

    public void Setup(SessionInfo sessionInfo, Action<string> onClickJoin)
    {
        _roomName = sessionInfo.Name;
        _onClickJoinCallback = onClickJoin;

        if (roomNameText != null)
            roomNameText.text = sessionInfo.Name;

        if (playerCountText != null)
            playerCountText.text = $"{sessionInfo.PlayerCount} / {sessionInfo.MaxPlayers}";

        // 満員またはゲーム開始済みの部屋はボタンを押せないようにする
        if (joinButton != null)
        {
            joinButton.interactable = sessionInfo.IsOpen && (sessionInfo.PlayerCount < sessionInfo.MaxPlayers);
            joinButton.onClick.RemoveAllListeners();
            joinButton.onClick.AddListener(() => _onClickJoinCallback?.Invoke(_roomName));
        }
    }
}