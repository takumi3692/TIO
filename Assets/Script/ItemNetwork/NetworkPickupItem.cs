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

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _floating = GetComponent("FloatingPickup");
    }

    public override void Spawned()
    {
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

    public override void FixedUpdateNetwork()
    {
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
        if (!HasStateAuthority && !IsHeld && Object != null && Object.IsValid)
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