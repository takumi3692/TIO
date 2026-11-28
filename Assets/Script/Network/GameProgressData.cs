using System.Collections.Generic;
using UnityEngine;

public static class GameProgressData
{
    public static int CurrentDay { get; private set; } = 1;
    public static int CurrentQuota { get; private set; } = 300;
    public static int DeliveredAmount { get; private set; } = 0;
    public static int LastDayEarned { get; private set; } = 0;

    // 何日ごとにノルマ判定を行うか（3なら3日ごとにチーム合計額で判定）
    public static int DaysPerQuota { get; private set; } = 3;
    public static int DaysRemainingInCycle { get; private set; } = 3;

    // リザルト・ゲームオーバー表示用
    public static bool WasQuotaCheckDay { get; private set; } = false;
    public static int PreviousQuota { get; private set; } = 300;
    public static int PreviousDelivered { get; private set; } = 0;
    public static string LastEndReason { get; private set; } = "";

    // 生還したプレイヤーの持ち越しアイテム（PlayerId -> アイテムキー一覧）
    private static readonly Dictionary<int, List<string>> _savedInventories = new Dictionary<int, List<string>>();

    public static void ResetNewGame(int initialQuota = 300, int daysPerQuota = 3)
    {
        CurrentDay = 1;
        CurrentQuota = initialQuota;
        DeliveredAmount = 0;
        LastDayEarned = 0;
        DaysPerQuota = Mathf.Max(1, daysPerQuota);
        DaysRemainingInCycle = DaysPerQuota;
        WasQuotaCheckDay = false;
        PreviousQuota = initialQuota;
        PreviousDelivered = 0;
        LastEndReason = "";
        _savedInventories.Clear();
    }

    public static void BeginDay()
    {
        LastDayEarned = 0;
        LastEndReason = "";
    }

    public static void AddDeliveredValue(int amount)
    {
        DeliveredAmount += amount;
        LastDayEarned += amount;
    }

    public static void SetPlayerSavedItems(int playerId, List<string> itemKeys)
    {
        _savedInventories[playerId] = new List<string>(itemKeys);
    }

    public static void ClearPlayerSavedItems(int playerId)
    {
        _savedInventories.Remove(playerId);
    }

    public static List<string> ConsumePlayerSavedItems(int playerId)
    {
        if (_savedInventories.TryGetValue(playerId, out List<string> items))
        {
            _savedInventories.Remove(playerId);
            return items;
        }
        return new List<string>();
    }

    /// <summary>
    /// ホストから全クライアントへ進行データを同期する
    /// </summary>
    public static void ApplySyncedState(
        int day, int quota, int delivered, int lastEarned,
        int daysPerQuota, int daysRemaining, bool wasQuotaCheck,
        int prevQuota, int prevDelivered, string endReason)
    {
        CurrentDay = day;
        CurrentQuota = quota;
        DeliveredAmount = delivered;
        LastDayEarned = lastEarned;
        DaysPerQuota = daysPerQuota;
        DaysRemainingInCycle = daysRemaining;
        WasQuotaCheckDay = wasQuotaCheck;
        PreviousQuota = prevQuota;
        PreviousDelivered = prevDelivered;
        LastEndReason = endReason;
    }

    public static void SetGameOverReason(string reason)
    {
        LastEndReason = reason;
    }

    /// <summary>
    /// 1日終了時の進行判定。ゲーム続行なら true、ノルマ未達でゲームオーバーなら false を返す
    /// </summary>
    public static bool AdvanceDayAndCheckQuota(int quotaIncreasePerCycle = 200)
    {
        PreviousQuota = CurrentQuota;
        PreviousDelivered = DeliveredAmount;
        DaysRemainingInCycle--;

        if (DaysRemainingInCycle <= 0)
        {
            WasQuotaCheckDay = true;

            if (DeliveredAmount < CurrentQuota)
            {
                LastEndReason = $"チームの合計納品額が目標に届きませんでした ({DeliveredAmount} / {CurrentQuota})";
                return false;
            }

            // チーム全員の合計でノルマ達成 -> 次のサイクルへ
            DeliveredAmount = 0;
            CurrentQuota += quotaIncreasePerCycle;
            DaysRemainingInCycle = DaysPerQuota;
        }
        else
        {
            WasQuotaCheckDay = false;
        }

        CurrentDay++;
        return true;
    }
}