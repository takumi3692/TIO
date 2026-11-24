using System.Collections;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(NetworkObject))]
public class StageGameManager : NetworkBehaviour
{
    public static StageGameManager Instance { get; private set; }

    [Header("ゲームルール設定")]
    [SerializeField] private float dayTimeLimit = 180f;        // 1日の制限時間（秒）
    [SerializeField] private int initialQuota = 300;           // 最初の目標ノルマ
    [SerializeField] private int quotaIncrease = 200;          // ノルマ達成ごとの上昇額
    [SerializeField] private int daysPerQuota = 3;             // 何日ごとにノルマ判定するか（ミメシス方式は3）

    [Header("UI表示（任意・未設定でも動作します）")]
    [SerializeField] private TMP_Text tmpTimerText;
    [SerializeField] private Text legacyTimerText;
    [SerializeField] private TMP_Text dayAndQuotaText;
    [SerializeField] private TMP_Text statusMessageText;

    [Networked] public float RemainingTime { get; set; }
    [Networked] public int SyncedDay { get; set; }
    [Networked] public int SyncedQuota { get; set; }
    [Networked] public int SyncedDelivered { get; set; }
    [Networked] public int SyncedDaysLeftInCycle { get; set; }
    [Networked] public NetworkBool IsDayEnded { get; set; }

    private bool _isSpawned = false;

    private void Awake()
    {
        Instance = this;
    }

    public override void Spawned()
    {
        _isSpawned = true;

        if (HasStateAuthority)
        {
            // Day 1 の初回開始時なら新規ゲームとして初期化
            if (GameProgressData.CurrentDay == 1 && GameProgressData.DeliveredAmount == 0)
            {
                GameProgressData.ResetNewGame(initialQuota, daysPerQuota);
            }

            GameProgressData.BeginDay();

            RemainingTime = dayTimeLimit;
            SyncedDay = GameProgressData.CurrentDay;
            SyncedQuota = GameProgressData.CurrentQuota;
            SyncedDelivered = GameProgressData.DeliveredAmount;
            SyncedDaysLeftInCycle = GameProgressData.DaysRemainingInCycle;
            IsDayEnded = false;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!_isSpawned || !HasStateAuthority || IsDayEnded) return;

        RemainingTime -= Runner.DeltaTime;

        if (RemainingTime <= 0f)
        {
            RemainingTime = 0f;
            EndDaySequence();
        }
    }

    private void Update()
    {
        if (!_isSpawned) return;

        // 残り時間UIの更新
        int minutes = Mathf.FloorToInt(RemainingTime / 60f);
        int seconds = Mathf.FloorToInt(RemainingTime % 60f);
        string timeStr = $"残り時間: {minutes:00}:{seconds:00}";

        if (tmpTimerText != null) tmpTimerText.text = timeStr;
        if (legacyTimerText != null) legacyTimerText.text = timeStr;

        // 日数・ノルマUIの更新
        if (dayAndQuotaText != null)
        {
            dayAndQuotaText.text = $"Day {SyncedDay} (判定まであと{SyncedDaysLeftInCycle}日) | 納品: {SyncedDelivered} / {SyncedQuota}";
        }
    }

    /// <summary>
    /// 納品エリアから呼ばれるスコア加算（ホスト専用）
    /// </summary>
    public void AddScore(int amount)
    {
        if (!HasStateAuthority || IsDayEnded) return;

        GameProgressData.AddDeliveredValue(amount);
        SyncedDelivered = GameProgressData.DeliveredAmount;
    }

    /// <summary>
    /// 制限時間終了時の強制帰還・未乗車即死シーケンス
    /// </summary>
    public void EndDaySequence()
    {
        if (!HasStateAuthority || IsDayEnded) return;
        IsDayEnded = true;

        NetworkPlayerSetup[] allPlayers = FindObjectsByType<NetworkPlayerSetup>(FindObjectsSortMode.None);
        int survivorCount = 0;

        foreach (var playerSetup in allPlayers)
        {
            NetworkObject playerNetObj = playerSetup.Object;
            if (playerNetObj == null || !playerNetObj.IsValid) continue;

            PlayerHealth health = playerSetup.GetComponentInChildren<PlayerHealth>(true);
            bool alreadyDead = (health != null && health.IsDead);
            bool isInsideVehicle = ReturnVehicleZone.Instance != null && ReturnVehicleZone.Instance.IsPlayerInside(playerNetObj);

            if (!alreadyDead && isInsideVehicle)
            {
                survivorCount++;

                // 車内に乗って生還したプレイヤーが手に持っているお宝も自動で納品加算する
                CollectHeldTreasuresFromSurvivor(playerSetup);
            }
            else if (!alreadyDead && !isInsideVehicle)
            {
                // 帰りの車に乗れていなかったプレイヤーを全画面で即死させる
                RPC_KillLeftBehindPlayer(playerNetObj.Id);
            }
        }

        StartCoroutine(TransitionAfterDayEndRoutine(survivorCount));
    }

    private void CollectHeldTreasuresFromSurvivor(NetworkPlayerSetup survivor)
    {
        TreasureItem[] heldTreasures = survivor.GetComponentsInChildren<TreasureItem>(true);
        foreach (var treasure in heldTreasures)
        {
            NetworkPickupItem netPickup = treasure.GetComponent<NetworkPickupItem>();
            if (netPickup != null && netPickup.Object != null && netPickup.Object.IsValid)
            {
                GameProgressData.AddDeliveredValue(treasure.Value);
                SyncedDelivered = GameProgressData.DeliveredAmount;
                netPickup.Object.Runner.Despawn(netPickup.Object);
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_KillLeftBehindPlayer(NetworkId playerId)
    {
        if (Runner.TryFindObject(playerId, out NetworkObject playerNetObj))
        {
            PlayerHealth health = playerNetObj.GetComponentInChildren<PlayerHealth>(true);
            if (health != null && !health.IsDead)
            {
                // 友達が実装した Kill() メソッドで即死させる
                health.Kill();
            }

            if (playerNetObj.HasInputAuthority && statusMessageText != null)
            {
                statusMessageText.text = "時間切れ！帰りの車に乗り遅れたため死亡しました...";
            }
        }
    }

    private IEnumerator TransitionAfterDayEndRoutine(int survivorCount)
    {
        // 即死演出と最終納品額を確認できるよう3.5秒待機
        yield return new WaitForSeconds(3.5f);

        if (NetworkManager.Instance == null) yield break;

        // 全員死亡（車に生還者が1人もいなかった）場合はゲームオーバーへ
        if (survivorCount <= 0)
        {
            NetworkManager.Instance.LoadSceneWithLoadingScreen(NetworkManager.GAMEOVER_SCENE_INDEX);
            yield break;
        }

        // 日数を進め、判定日であればノルマ達成チェックを行う
        bool isContinue = GameProgressData.AdvanceDayAndCheckQuota(quotaIncrease);

        if (!isContinue)
        {
            // ノルマ未達 -> 8,Gameover シーンへ
            NetworkManager.Instance.LoadSceneWithLoadingScreen(NetworkManager.GAMEOVER_SCENE_INDEX);
        }
        else
        {
            // 生還＆継続 -> 6,Result シーンへ
            NetworkManager.Instance.LoadSceneWithLoadingScreen(NetworkManager.RESULT_SCENE_INDEX);
        }
    }
}