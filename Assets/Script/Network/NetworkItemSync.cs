using System.Collections.Generic;
using System.Reflection;
using Fusion;
using UnityEngine;
using UnityEngine.UI;

public class NetworkItemSync : NetworkBehaviour
{
    [Header("他人から見た時にお宝を持つ位置（体の右手付近）")]
    [SerializeField] private Vector3 thirdPersonHoldOffset = new Vector3(0.35f, 1.1f, 0.45f);

    private PlayerInventory _inventory;
    private int _lastSelectedSlot = 0;

    private Dictionary<NetworkId, int> _heldItems = new Dictionary<NetworkId, int>();
    private Dictionary<NetworkId, NetworkPickupItem> _heldItemRefs = new Dictionary<NetworkId, NetworkPickupItem>();

    public override void Spawned()
    {
        _inventory = GetComponentInChildren<PlayerInventory>(true);
        if (_inventory == null) return;

        if (!HasInputAuthority)
        {
            // 他人の分身キャラが自分の画面下のスロットUI色を変えてしまわないよう参照を外す
            FieldInfo slotImagesField = typeof(PlayerInventory).GetField("slotImages", BindingFlags.NonPublic | BindingFlags.Instance);
            if (slotImagesField != null)
            {
                slotImagesField.SetValue(_inventory, null);
            }

            // 他人のカメラは非表示のため、体の右手付近にお宝を表示する用の持ち位置を設定
            GameObject tpHold = new GameObject("ThirdPersonHoldPoint");
            tpHold.transform.SetParent(_inventory.transform, false);
            tpHold.transform.localPosition = thirdPersonHoldOffset;

            FieldInfo holdPointField = typeof(PlayerInventory).GetField("holdPoint", BindingFlags.NonPublic | BindingFlags.Instance);
            if (holdPointField != null)
            {
                holdPointField.SetValue(_inventory, tpHold.transform);
            }
        }
        else
        {
            // 自分のキャラでスロットUIが未割り当てなら、シーン内のInventoryCanvasから自動接続
            FieldInfo slotImagesField = typeof(PlayerInventory).GetField("slotImages", BindingFlags.NonPublic | BindingFlags.Instance);
            if (slotImagesField != null)
            {
                Image[] currentSlots = slotImagesField.GetValue(_inventory) as Image[];
                if (currentSlots == null || currentSlots.Length == 0)
                {
                    GameObject canvasObj = GameObject.Find("InventoryCanvas");
                    if (canvasObj != null)
                    {
                        List<Image> foundSlots = new List<Image>();
                        foreach (var img in canvasObj.GetComponentsInChildren<Image>(true))
                        {
                            if (img.gameObject != canvasObj && foundSlots.Count < _inventory.MaxSlots)
                            {
                                foundSlots.Add(img);
                            }
                        }
                        if (foundSlots.Count > 0)
                        {
                            slotImagesField.SetValue(_inventory, foundSlots.ToArray());
                        }
                    }
                }
            }
        }
    }

    private void LateUpdate()
    {
        if (!HasInputAuthority || _inventory == null || Object == null || !Object.IsValid) return;

        // 1. スロット切り替えの検知
        if (_inventory.SelectedSlot != _lastSelectedSlot)
        {
            _lastSelectedSlot = _inventory.SelectedSlot;
            RPC_SyncSelectSlot(_lastSelectedSlot);
        }

        // 2. 新しく拾ったお宝・武器の検知
        PickupItem[] currentChildren = _inventory.GetComponentsInChildren<PickupItem>(true);
        HashSet<NetworkId> currentIds = new HashSet<NetworkId>();

        foreach (var item in currentChildren)
        {
            NetworkPickupItem netItem = item.GetComponent<NetworkPickupItem>();
            if (netItem == null || netItem.Object == null || !netItem.Object.IsValid) continue;

            NetworkId id = netItem.Object.Id;
            currentIds.Add(id);

            if (!_heldItems.ContainsKey(id))
            {
                if (netItem.IsHeld)
                {
                    // 他の人が先に拾っていた場合は即座に手放す（二重取得防止）
                    _inventory.DropItem();
                }
                else
                {
                    _heldItems[id] = _inventory.SelectedSlot;
                    _heldItemRefs[id] = netItem;
                    RPC_RequestPickupToServer(id);
                }
            }
        }

        // 3. 手放した（ドロップ・投擲した）お宝・武器の検知
        List<NetworkId> releasedIds = new List<NetworkId>();
        foreach (var kvp in _heldItems)
        {
            if (!currentIds.Contains(kvp.Key))
            {
                releasedIds.Add(kvp.Key);
            }
        }

        foreach (var id in releasedIds)
        {
            int slotIndex = _heldItems[id];
            NetworkPickupItem netItem = _heldItemRefs[id];

            _heldItems.Remove(id);
            _heldItemRefs.Remove(id);

            if (netItem != null)
            {
                // バットの近接拾得コンポーネントがOFFになっていたら再有効化
                UnityEngine.Behaviour batPickup = netItem.GetComponent("BatProximityPickup") as UnityEngine.Behaviour;
                if (batPickup != null) batPickup.enabled = true;

                Rigidbody rb = netItem.GetComponent<Rigidbody>();
                Vector3 velocity = (rb != null) ? rb.linearVelocity : Vector3.zero;
                RPC_SyncReleaseToAll(id, slotIndex, netItem.transform.position, netItem.transform.rotation, velocity);
            }
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestPickupToServer(NetworkId itemId)
    {
        if (Runner.TryFindObject(itemId, out NetworkObject itemNetObj))
        {
            NetworkPickupItem netItem = itemNetObj.GetComponent<NetworkPickupItem>();
            if (netItem != null && !netItem.IsHeld)
            {
                netItem.IsHeld = true;
                RPC_ConfirmPickupToOthers(itemId);
            }
            else
            {
                RPC_RejectPickupToOwner(itemId);
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ConfirmPickupToOthers(NetworkId itemId)
    {
        if (HasInputAuthority) return;

        if (_inventory == null) _inventory = GetComponentInChildren<PlayerInventory>(true);
        if (Runner.TryFindObject(itemId, out NetworkObject itemNetObj) && _inventory != null)
        {
            PickupItem item = itemNetObj.GetComponent<PickupItem>();
            if (item != null)
            {
                _inventory.AddItem(item);
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_RejectPickupToOwner(NetworkId itemId)
    {
        if (_inventory == null) _inventory = GetComponentInChildren<PlayerInventory>(true);
        if (_inventory != null)
        {
            _inventory.DropItem();
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void RPC_SyncSelectSlot(int slotIndex)
    {
        if (HasInputAuthority) return;

        if (_inventory == null) _inventory = GetComponentInChildren<PlayerInventory>(true);
        if (_inventory != null)
        {
            _inventory.SelectSlot(slotIndex);
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void RPC_SyncReleaseToAll(NetworkId itemId, int slotIndex, Vector3 pos, Quaternion rot, Vector3 velocity)
    {
        if (!HasInputAuthority)
        {
            if (_inventory == null) _inventory = GetComponentInChildren<PlayerInventory>(true);
            if (_inventory != null)
            {
                _inventory.SelectSlot(slotIndex);
                _inventory.DropItem();
            }
        }

        if (Runner.TryFindObject(itemId, out NetworkObject itemNetObj))
        {
            UnityEngine.Behaviour batPickup = itemNetObj.GetComponent("BatProximityPickup") as UnityEngine.Behaviour;
            if (batPickup != null) batPickup.enabled = true;

            itemNetObj.transform.position = pos;
            itemNetObj.transform.rotation = rot;

            Rigidbody rb = itemNetObj.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
            {
                rb.linearVelocity = velocity;
            }

            if (HasStateAuthority)
            {
                NetworkPickupItem netItem = itemNetObj.GetComponent<NetworkPickupItem>();
                if (netItem != null)
                {
                    netItem.IsHeld = false;
                }
            }
        }
    }
}