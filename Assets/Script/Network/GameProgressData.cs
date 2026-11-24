using UnityEngine;

public static class GameProgressData
{
    public static int CurrentDay { get; private set; } = 1;
    public static int CurrentQuota { get; private set; } = 300;
    public static int DeliveredAmount { get; private set; } = 0;
    public static int LastDayEarned { get; private set; } = 0;

    // 何日ごとにノルマ判定を行うか（例：3なら3日ごとに判定、1なら毎日判定）
    public static int DaysPerQuota { get; private set; } = 3;
    public static int DaysRemainingInCycle { get; private set; } = 3;

    // リザルト画面表示用の記録
    public static bool WasQuotaCheckDay { get; private set; } = false;
    public static int PreviousQuota { get; private set; } = 300;
    public static int PreviousDelivered { get; private set; } = 0;

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
    }

    public static void BeginDay()
    {
        LastDayEarned = 0;
    }

    public static void AddDeliveredValue(int amount)
    {
        DeliveredAmount += amount;
        LastDayEarned += amount;
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
                // ノルマ未達 -> ゲームオーバー
                return false;
            }

            // ノルマ達成 -> 次のノルマサイクルへ
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