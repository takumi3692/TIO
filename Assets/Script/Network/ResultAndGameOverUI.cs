using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResultAndGameOverUI : MonoBehaviour
{
    [Header("画面モード設定（Result画面ならfalse、GameOver画面ならtrue）")]
    [SerializeField] private bool isGameOverScreen = false;

    [Header("UI要素")]
    [SerializeField] private TMP_Text resultSummaryText;
    [SerializeField] private Button nextDayButton;
    [SerializeField] private Button returnToTitleButton;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        bool isHost = NetworkManager.Instance != null &&
                      NetworkManager.Instance.Runner != null &&
                      NetworkManager.Instance.Runner.IsServer;

        if (resultSummaryText != null)
        {
            if (isGameOverScreen)
            {
                string reasonLine = string.IsNullOrEmpty(GameProgressData.LastEndReason)
                    ? ""
                    : $"理由: {GameProgressData.LastEndReason}\n\n";

                resultSummaryText.text =
                    $"GAME OVER\n\n" +
                    reasonLine +
                    $"到達日数: Day {GameProgressData.CurrentDay}\n" +
                    $"チーム最終納品額: {GameProgressData.DeliveredAmount} / 目標 {GameProgressData.CurrentQuota}";
            }
            else if (GameProgressData.WasQuotaCheckDay)
            {
                resultSummaryText.text =
                    $"チームノルマ達成！クリア！\n\n" +
                    $"達成納品額: {GameProgressData.PreviousDelivered} / {GameProgressData.PreviousQuota}\n" +
                    $"次の目標（Day {GameProgressData.CurrentDay}〜）: {GameProgressData.CurrentQuota}";
            }
            else
            {
                resultSummaryText.text =
                    $"Day {GameProgressData.CurrentDay - 1} 帰還成功！\n\n" +
                    $"本日のチーム獲得額: +{GameProgressData.LastDayEarned}\n" +
                    $"チーム累計納品額: {GameProgressData.DeliveredAmount} / 目標 {GameProgressData.CurrentQuota}\n" +
                    $"ノルマ判定日まであと {GameProgressData.DaysRemainingInCycle} 日";
            }
        }

        if (nextDayButton != null)
        {
            nextDayButton.gameObject.SetActive(!isGameOverScreen && isHost);
            nextDayButton.onClick.AddListener(OnClickNextDay);
        }

        if (returnToTitleButton != null)
        {
            returnToTitleButton.onClick.AddListener(OnClickReturnToTitle);
        }
    }

    private void OnClickNextDay()
    {
        nextDayButton.interactable = false;
        NetworkManager.Instance.LoadSceneWithLoadingScreen(NetworkManager.STAGE_SCENE_INDEX);
    }

    private async void OnClickReturnToTitle()
    {
        returnToTitleButton.interactable = false;
        GameProgressData.ResetNewGame();
        if (NetworkManager.Instance != null)
        {
            await NetworkManager.Instance.LeaveSessionAndReturnToTitle();
        }
    }
}