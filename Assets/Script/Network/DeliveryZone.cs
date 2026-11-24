using Fusion;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DeliveryZone : MonoBehaviour
{
    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerStay(Collider other)
    {
        if (StageGameManager.Instance == null || !StageGameManager.Instance.HasStateAuthority) return;
        if (StageGameManager.Instance.IsDayEnded) return;

        TreasureItem treasure = other.GetComponentInParent<TreasureItem>();
        if (treasure == null) return;

        NetworkPickupItem netPickup = treasure.GetComponent<NetworkPickupItem>();
        if (netPickup == null || !netPickup.IsReady) return;

        // 誰かが手に持っている最中は回収しない（手放した瞬間に納品）
        if (netPickup.IsHeld) return;

        NetworkObject netObj = netPickup.Object;
        if (netObj != null && netObj.IsValid)
        {
            int itemValue = treasure.Value;
            StageGameManager.Instance.AddScore(itemValue);
            netObj.Runner.Despawn(netObj);
            Debug.Log($"[納品] {treasure.TreasureName} (+{itemValue}) を納品しました！");
        }
    }
}