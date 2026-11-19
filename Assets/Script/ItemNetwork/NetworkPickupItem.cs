using Fusion;
using UnityEngine;

[RequireComponent(typeof(PickupItem))]
[RequireComponent(typeof(NetworkObject))]
public class NetworkPickupItem : NetworkBehaviour
{
    [Networked] public NetworkBool IsHeld { get; set; }
    [Networked] private Vector3 SyncedPosition { get; set; }
    [Networked] private Quaternion SyncedRotation { get; set; }
    [Networked] private NetworkBool IsMoving { get; set; }

    private Rigidbody _rb;
    private Component _floating;
    private bool _isSpawned = false;

    public bool IsReady => _isSpawned && Object != null && Object.IsValid;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _floating = GetComponent("FloatingPickup");
    }

    public override void Spawned()
    {
        _isSpawned = true;

        if (HasStateAuthority)
        {
            SyncedPosition = transform.position;
            SyncedRotation = transform.rotation;
        }
        else if (!IsHeld)
        {
            transform.position = SyncedPosition;
            transform.rotation = SyncedRotation;
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        _isSpawned = false;
    }

    public override void FixedUpdateNetwork()
    {
        if (!IsReady) return;

        if (HasStateAuthority && !IsHeld)
        {
            bool moving = _rb != null && !_rb.isKinematic && _rb.linearVelocity.sqrMagnitude > 0.05f;
            IsMoving = moving;

            // 投げられて動いている間、または浮遊スクリプトが無いアイテムは位置を同期
            if (moving || _floating == null)
            {
                SyncedPosition = transform.position;
                SyncedRotation = transform.rotation;
            }
        }
    }

    private void Update()
    {
        // 必ず Spawned() 完了後になってから IsHeld などの通信変数にアクセスする
        if (!IsReady) return;

        if (!HasStateAuthority && !IsHeld)
        {
            // 投げられて飛んでいる最中だけ座標を滑らかに同期（静止後の浮遊はFloatingPickupに任せる）
            if (IsMoving || _floating == null)
            {
                transform.position = Vector3.Lerp(transform.position, SyncedPosition, Time.deltaTime * 15f);
                transform.rotation = Quaternion.Slerp(transform.rotation, SyncedRotation, Time.deltaTime * 15f);
            }
        }
    }
}