
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerPickup : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private PlayerInventory inventory;

    [Header("Pickup Settings")]
    [SerializeField] private float pickupDistance = 3f;
    [SerializeField] private LayerMask itemLayer = ~0;

    [Header("Nearby Pickup")]
    [SerializeField] private float nearbyPickupDistance = 1.5f;

    private void Awake()
    {
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();

        if (inventory == null)
            inventory = GetComponent<PlayerInventory>();
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            // まず近くのバットを探す
            if (!TryPickupNearbyBat())
            {
                // バットが近くになければ従来のRaycast方式
                TryPickup();
            }
        }
    }

    private bool TryPickupNearbyBat()
    {
        if (inventory == null) return false;

        Collider[] colliders = Physics.OverlapSphere(
            transform.position,
            nearbyPickupDistance,
            itemLayer,
            QueryTriggerInteraction.Collide
        );

        BatProximityPickup nearestBat = null;
        float nearestDistance = float.MaxValue;

        foreach (Collider col in colliders)
        {
            BatProximityPickup bat =
                col.GetComponentInParent<BatProximityPickup>();

            if (bat == null) continue;

            float distance = Vector3.Distance(
                transform.position,
                bat.transform.position
            );

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestBat = bat;
            }
        }

        if (nearestBat == null) return false;

        if (inventory.IsFull())
        {
            Debug.Log("インベントリがいっぱいです！");
            return true;
        }

        PickupItem item =
            nearestBat.GetComponent<PickupItem>();

        if (item == null) return false;

        bool success = inventory.AddItem(item);

        if (success)
        {
            nearestBat.enabled = false;
            Debug.Log("バットを取得しました！");
        }

        return true;
    }

    private void TryPickup()
    {
        if (playerCamera == null || inventory == null)
            return;

        if (inventory.IsFull())
        {
            Debug.Log("インベントリがいっぱいです！");
            return;
        }

        Ray ray = playerCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f)
        );

        if (Physics.Raycast(
            ray,
            out RaycastHit hit,
            pickupDistance,
            itemLayer,
            QueryTriggerInteraction.Ignore))
        {
            PickupItem item =
                hit.collider.GetComponentInParent<PickupItem>();

            if (item != null)
                inventory.AddItem(item);
        }
    }
}
