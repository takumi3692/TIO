
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PlayerInventory : MonoBehaviour
{
    [Header("Inventory Settings")]
    [SerializeField] private int maxSlots = 5;
    [SerializeField] private Transform holdPoint;
    [SerializeField] private Camera playerCamera;

    [Header("Hold Position")]
    [SerializeField] private Vector3 holdPosition =
        new Vector3(0.3f, -0.25f, 1.0f);

    [SerializeField] private Vector3 holdRotation =
        new Vector3(0f, 0f, 0f);

    [Header("Throw Settings")]
    [SerializeField] private float throwForce = 8f;

    [Header("Slot UI")]
    [SerializeField] private Image[] slotImages;

    [SerializeField] private Color emptySlotColor =
        new Color32(70, 70, 70, 255);

    [SerializeField] private Color filledSlotColor =
    new Color32(80, 170, 90, 255);

    [SerializeField] private Color selectedEmptyColor =
    new Color32(100, 160, 220, 255);

    [SerializeField] private Color selectedFilledColor =
        new Color32(240, 180, 50, 255);

    private PickupItem[] items;
    private int selectedSlot = 0;

    public int SelectedSlot => selectedSlot;
    public int MaxSlots => maxSlots;

    private void Awake()
    {
        items = new PickupItem[maxSlots];

        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();

        if (holdPoint == null && playerCamera != null)
        {
            GameObject point = new GameObject("HoldPoint");
            holdPoint = point.transform;
            holdPoint.SetParent(playerCamera.transform, false);
        }

        RefreshAllSlotVisuals();
    }

    private void Update()
    {
        HandleSlotInput();
        HandleItemInput();
    }

    public bool AddItem(PickupItem item)
    {
        if (item == null) return false;

        for (int i = 0; i < maxSlots; i++)
        {
            if (items[i] == null)
            {
                item.Store();
                items[i] = item;

                if (i == selectedSlot)
                    ShowSelectedItem();
                else
                {
                    item.transform.SetParent(transform);
                    item.gameObject.SetActive(false);
                }

                UpdateSlotVisual(i);

                Debug.Log("アイテムを取得しました。Slot: " + (i + 1));
                return true;
            }
        }

        Debug.Log("インベントリがいっぱいです！");
        return false;
    }

    private void HandleSlotInput()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
            SelectSlot(0);

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
            SelectSlot(1);

        if (Keyboard.current.digit3Key.wasPressedThisFrame)
            SelectSlot(2);

        if (Keyboard.current.digit4Key.wasPressedThisFrame)
            SelectSlot(3);

        if (Keyboard.current.digit5Key.wasPressedThisFrame)
            SelectSlot(4);
    }

    public void SelectSlot(int index)
    {
        if (index < 0 || index >= maxSlots) return;

        if (items[selectedSlot] != null)
        {
            items[selectedSlot].transform.SetParent(transform);
            items[selectedSlot].gameObject.SetActive(false);
        }

        selectedSlot = index;
        ShowSelectedItem();
        RefreshAllSlotVisuals();
    }

    private void ShowSelectedItem()
    {
        PickupItem item = items[selectedSlot];

        if (item == null) return;

        item.gameObject.SetActive(true);
        item.transform.SetParent(holdPoint);

        item.transform.localPosition = holdPosition;
        item.transform.localRotation =
            Quaternion.Euler(holdRotation);
    }

    private void HandleItemInput()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.gKey.wasPressedThisFrame)
            DropItem();

        if (Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame)
            ThrowItem();
    }

    public void DropItem()
    {
        ReleaseSelected(Vector3.zero);
    }

    public void ThrowItem()
    {
        if (playerCamera == null) return;

        Vector3 force = playerCamera.transform.forward * throwForce;
        ReleaseSelected(force);
    }

    private void ReleaseSelected(Vector3 force)
    {
        PickupItem item = items[selectedSlot];

        if (item == null) return;

        Vector3 releasePosition = holdPoint.position +
            playerCamera.transform.forward * 0.5f;

        item.transform.position = releasePosition;

        items[selectedSlot] = null;

        UpdateSlotVisual(selectedSlot);

        item.Release(force);

        Debug.Log("アイテムを手放しました。");
    }

    public bool IsFull()
    {
        foreach (PickupItem item in items)
        {
            if (item == null) return false;
        }

        return true;
    }

    // すべてのスロットの色を更新
    private void RefreshAllSlotVisuals()
    {
        if (slotImages == null) return;

        for (int i = 0; i < slotImages.Length; i++)
        {
            UpdateSlotVisual(i);
        }
    }

    // 指定したスロットの色を更新
    private void UpdateSlotVisual(int index)
    {
        if (slotImages == null) return;
        if (index < 0 || index >= slotImages.Length) return;
        if (slotImages[index] == null) return;

        bool isSelected = index == selectedSlot;
        bool hasItem = items[index] != null;

        if (isSelected)
        {
            slotImages[index].color = hasItem
                ? selectedFilledColor
                : selectedEmptyColor;
        }
        else
        {
            slotImages[index].color = hasItem
                ? filledSlotColor
                : emptySlotColor;
        }
}
}
