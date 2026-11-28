using Fusion;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class DeliveryZone : MonoBehaviour
{
    private BoxCollider _boxCollider;

    private void Awake()
    {
        _boxCollider = GetComponent<BoxCollider>();
        _boxCollider.isTrigger = true;
    }

    private void FixedUpdate()
    {
        if (StageGameManager.Instance == null || !StageGameManager.Instance.HasStateAuthority) return;
        if (StageGameManager.Instance.IsDayEnded || _boxCollider == null) return;

        Vector3 worldCenter = transform.TransformPoint(_boxCollider.center);
        Vector3 lossy = transform.lossyScale;
        Vector3 halfExtents = new Vector3(
            Mathf.Abs(_boxCollider.size.x * lossy.x) * 0.5f,
            Mathf.Abs(_boxCollider.size.y * lossy.y) * 0.5f,
            Mathf.Abs(_boxCollider.size.z * lossy.z) * 0.5f
        );

        Collider[] hits = Physics.OverlapBox(
            worldCenter,
            halfExtents,
            transform.rotation,
            Physics.AllLayers,
            QueryTriggerInteraction.Collide
        );

        foreach (var hit in hits)
        {
            TreasureItem treasure = hit.GetComponentInParent<TreasureItem>();
            if (treasure == null) continue;

            NetworkPickupItem netPickup = treasure.GetComponent<NetworkPickupItem>();
            if (netPickup == null || !netPickup.IsReady || netPickup.IsHeld) continue;

            NetworkObject netObj = netPickup.Object;
            if (netObj != null && netObj.IsValid)
            {
                int itemValue = treasure.Value;
                StageGameManager.Instance.AddTeamScore(itemValue);
                netObj.Runner.Despawn(netObj);
                Debug.Log($"[チーム納品] {treasure.TreasureName} (+{itemValue}) を納品！");
            }
        }
    }
}