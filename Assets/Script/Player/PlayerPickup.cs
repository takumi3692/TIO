
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
            TryPickup();
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
