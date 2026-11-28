using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(NetworkObject))]
public class StageGameManager : NetworkBehaviour
{
    [System.Serializable]
    public struct RestorableItemEntry
    {
        public string itemKey;          // 例: "Scrap", "Bat"
        public NetworkPrefabRef prefab; // 対応するプレハブ
    }

    public static StageGameManager Instance { get; private set; }

    [Header("ゲームルール設定")]
    [SerializeField] private float dayTimeLimit = 180f;
    [SerializeField] private int initialQuota = 300;
    [SerializeField] private int quotaIncrease = 200;
    [SerializeField] private int daysPerQuota = 3;

    [Header("翌日へ持ち越すアイテムのプレハブ登録")]
    [SerializeField] private List<RestorableItemEntry> restorableItems = new List<RestorableItemEntry>();

    [Header("UI表示（任意）")]
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
    private readonly HashSet<int> _restoredPlayerIds = new HashSet<int>();

    private void Awake()
    {
        Instance = this;
    }

    public override void Spawned()
    {
        _isSpawned = true;
        _restoredPlayerIds.Clear();

        if (HasStateAuthority)
        {
            if (GameProgressData.CurrentDay == 1 && GameProgressData.DeliveredAmount == 0 && GameProgressData.DaysRemainingInCycle == GameProgressData.DaysPerQuota)
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

            BroadcastProgressToAll();
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!_isSpawned || !HasStateAuthority || IsDayEnded) return;

        // 持ち越しアイテムの復元（スポーン済みのプレイヤーに配る）
        RestoreSurvivingPlayersItemsIfNeeded();

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

        int minutes = Mathf.FloorToInt(RemainingTime / 60f);
        int seconds = Mathf.FloorToInt(RemainingTime % 60f);
        string timeStr = $"残り時間: {minutes:00}:{seconds:00}";

        if (tmpTimerText != null) tmpTimerText.text = timeStr;
        if (legacyTimerText != null) legacyTimerText.text = timeStr;

        if (dayAndQuotaText != null)
        {
            dayAndQuotaText.text = $"Day {SyncedDay} (判定まであと{SyncedDaysLeftInCycle}日) | チーム納品: {SyncedDelivered} / {SyncedQuota}";
        }
    }

    /// <summary>
    /// チーム共通の納品額に加算する（ホスト専用）
    /// </summary>
    public void AddTeamScore(int amount)
    {
        if (!HasStateAuthority || IsDayEnded) return;

        GameProgressData.AddDeliveredValue(amount);
        SyncedDelivered = GameProgressData.DeliveredAmount;
        BroadcastProgressToAll();
    }

    /// <summary>
    /// 制限時間0になった瞬間の処理：
    /// ・乗車エリア内の人：生還＆持っているアイテムをそのまま保持
    /// ・乗車エリア外の人：死亡＆持っているアイテムをロスト
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

            int playerId = playerNetObj.InputAuthority.PlayerId;
            PlayerHealth health = playerSetup.GetComponentInChildren<PlayerHealth>(true);
            bool alreadyDead = (health != null && health.IsDead);
            bool isInsideVehicle = ReturnVehicleZone.Instance != null && ReturnVehicleZone.Instance.IsPlayerInside(playerSetup);

            if (!alreadyDead && isInsideVehicle)
            {
                // 【生還】持っているアイテムをそのまま翌日へ保持する
                survivorCount++;
                List<string> heldKeys = ExtractPlayerItemKeys(playerSetup);
                GameProgressData.SetPlayerSavedItems(playerId, heldKeys);
                Debug.Log($"[生還] Player {playerId} 生還！保持アイテム数: {heldKeys.Count}");
            }
            else
            {
                // 【死亡＆ロスト】乗車エリア外なら即死させ、所持アイテムをすべて消去（ロスト）する
                GameProgressData.ClearPlayerSavedItems(playerId);
                DestroyPlayerHeldItems(playerSetup);

                if (!alreadyDead)
                {
                    RPC_KillLeftBehindPlayer(playerNetObj.Id);
                }
                Debug.Log($"[死亡・ロスト] Player {playerId} は乗車エリア外のため死亡しアイテムをロストしました");
            }
        }

        StartCoroutine(TransitionAfterDayEndRoutine(survivorCount));
    }

    private List<string> ExtractPlayerItemKeys(NetworkPlayerSetup playerSetup)
    {
        List<string> keys = new List<string>();
        PlayerInventory inv = playerSetup.GetComponentInChildren<PlayerInventory>(true);
        if (inv == null) return keys;

        FieldInfo itemsField = typeof(PlayerInventory).GetField("items", BindingFlags.NonPublic | BindingFlags.Instance);
        if (itemsField != null)
        {
            PickupItem[] items = itemsField.GetValue(inv) as PickupItem[];
            if (items != null)
            {
                foreach (var item in items)
                {
                    if (item == null) continue;
                    keys.Add(IdentifyItemKey(item.gameObject));
                }
            }
        }
        return keys;
    }

    private string IdentifyItemKey(GameObject itemObj)
    {
        TreasureItem treasure = itemObj.GetComponentInParent<TreasureItem>();
        if (treasure != null) return treasure.ItemKey;

        WeaponItem weapon = itemObj.GetComponentInParent<WeaponItem>();
        if (weapon != null) return "Bat";

        return itemObj.name.Replace("(Clone)", "").Trim();
    }

    private void DestroyPlayerHeldItems(NetworkPlayerSetup playerSetup)
    {
        NetworkPickupItem[] heldNetItems = playerSetup.GetComponentsInChildren<NetworkPickupItem>(true);
        foreach (var netItem in heldNetItems)
        {
            if (netItem != null && netItem.Object != null && netItem.Object.IsValid)
            {
                netItem.Object.Runner.Despawn(netItem.Object);
            }
        }
    }

    private void RestoreSurvivingPlayersItemsIfNeeded()
    {
        if (restorableItems == null || restorableItems.Count == 0) return;

        NetworkPlayerSetup[] allPlayers = FindObjectsByType<NetworkPlayerSetup>(FindObjectsSortMode.None);
        foreach (var playerSetup in allPlayers)
        {
            if (playerSetup.Object == null || !playerSetup.Object.IsValid) continue;

            int playerId = playerSetup.Object.InputAuthority.PlayerId;
            if (_restoredPlayerIds.Contains(playerId)) continue;

            _restoredPlayerIds.Add(playerId);
            List<string> savedKeys = GameProgressData.ConsumePlayerSavedItems(playerId);

            foreach (string key in savedKeys)
            {
                if (TryGetPrefabForKey(key, out NetworkPrefabRef prefab))
                {
                    NetworkObject spawnedItem = Runner.Spawn(prefab, playerSetup.transform.position + Vector3.up * 50f, Quaternion.identity);
                    if (spawnedItem != null)
                    {
                        RPC_GiveRestoredItemToOwner(playerSetup.Object.Id, spawnedItem.Id);
                    }
                }
            }
        }
    }

    private bool TryGetPrefabForKey(string key, out NetworkPrefabRef prefab)
    {
        foreach (var entry in restorableItems)
        {
            if (entry.itemKey == key && entry.prefab.IsValid)
            {
                prefab = entry.prefab;
                return true;
            }
        }
        prefab = default;
        return false;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_GiveRestoredItemToOwner(NetworkId playerId, NetworkId itemId)
    {
        if (Runner.TryFindObject(playerId, out NetworkObject playerNetObj) &&
            Runner.TryFindObject(itemId, out NetworkObject itemNetObj))
        {
            // そのプレイヤーの操作端末（オーナー）だけでインベントリへ追加（NetworkItemSyncが自動で全員へ同期する）
            if (!playerNetObj.HasInputAuthority) return;

            PlayerInventory inv = playerNetObj.GetComponentInChildren<PlayerInventory>(true);
            PickupItem pickup = itemNetObj.GetComponentInChildren<PickupItem>(true);
            if (inv != null && pickup != null)
            {
                inv.AddItem(pickup);
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
                health.Kill();
            }

            if (playerNetObj.HasInputAuthority && statusMessageText != null)
            {
                statusMessageText.text = "時間切れ！帰りの車に乗り遅れたため死亡し、アイテムをロストしました...";
            }
        }
    }

    private IEnumerator TransitionAfterDayEndRoutine(int survivorCount)
    {
        yield return new WaitForSeconds(3.5f);

        if (NetworkManager.Instance == null) yield break;

        if (survivorCount <= 0)
        {
            GameProgressData.SetGameOverReason("帰りの車に誰も乗れず全滅しました");
            BroadcastProgressToAll();
            yield return new WaitForSeconds(0.2f);
            NetworkManager.Instance.LoadSceneWithLoadingScreen(NetworkManager.GAMEOVER_SCENE_INDEX);
            yield break;
        }

        bool isContinue = GameProgressData.AdvanceDayAndCheckQuota(quotaIncrease);

        // シーン遷移前に最新のチーム進捗を参加者全員へ確実に同期する
        BroadcastProgressToAll();
        yield return new WaitForSeconds(0.2f);

        if (!isContinue)
        {
            NetworkManager.Instance.LoadSceneWithLoadingScreen(NetworkManager.GAMEOVER_SCENE_INDEX);
        }
        else
        {
            NetworkManager.Instance.LoadSceneWithLoadingScreen(NetworkManager.RESULT_SCENE_INDEX);
        }
    }

    private void BroadcastProgressToAll()
    {
        RPC_SyncProgressData(
            GameProgressData.CurrentDay,
            GameProgressData.CurrentQuota,
            GameProgressData.DeliveredAmount,
            GameProgressData.LastDayEarned,
            GameProgressData.DaysPerQuota,
            GameProgressData.DaysRemainingInCycle,
            GameProgressData.WasQuotaCheckDay,
            GameProgressData.PreviousQuota,
            GameProgressData.PreviousDelivered,
            GameProgressData.LastEndReason
        );
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SyncProgressData(
        int day, int quota, int delivered, int lastEarned,
        int daysPerQuota, int daysRemaining, bool wasQuotaCheck,
        int prevQuota, int prevDelivered, string endReason)
    {
        GameProgressData.ApplySyncedState(
            day, quota, delivered, lastEarned,
            daysPerQuota, daysRemaining, wasQuotaCheck,
            prevQuota, prevDelivered, endReason
        );
    }
}