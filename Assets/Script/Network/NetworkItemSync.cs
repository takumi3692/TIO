using Fusion;
using UnityEngine;

public class NetworkItemSync : NetworkBehaviour
{
    private PlayerInventory _inventory;
    private PlayerPickup _pickup;

    private void Awake()
    {
        _inventory = GetComponent<PlayerInventory>();
        _pickup = GetComponent<PlayerPickup>();
    }

    public override void Spawned()
    {
        // 他人の画面に生成されたプレイヤーの場合、ローカル入力スクリプトを無効化
        if (!HasInputAuthority)
        {
            if (_inventory != null) _inventory.enabled = false;
            if (_pickup != null) _pickup.enabled = false;
        }
    }

    /// <summary>
    /// アイテム拾得リクエスト（全端末へ同期呼び出し）
    /// </summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    public void RPC_RequestPickup(NetworkObject itemNetworkObj)
    {
        if (itemNetworkObj != null && _inventory != null)
        {
            PickupItem item = itemNetworkObj.GetComponent<PickupItem>();
            if (item != null)
            {
                // 友達のAddItem処理を実行して手元に格納
                _inventory.AddItem(item);
            }
        }
    }

    /// <summary>
    /// アイテム投擲/ドロップリクエスト（全端末へ同期呼び出し）
    /// </summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    public void RPC_RequestRelease(int slotIndex, Vector3 releasePosition, Vector3 force)
    {
        if (_inventory != null)
        {
            // 指定スロットのアイテムを手放す処理を全画面で同調
            _inventory.SelectSlot(slotIndex);
            _inventory.ThrowItem();
        }
    }
}