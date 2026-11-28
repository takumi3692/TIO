using System.Collections.Generic;
using Fusion;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class ReturnVehicleZone : MonoBehaviour
{
    public static ReturnVehicleZone Instance { get; private set; }

    private BoxCollider _boxCollider;

    private void Awake()
    {
        Instance = this;
        _boxCollider = GetComponent<BoxCollider>();
        _boxCollider.isTrigger = true;
    }

    /// <summary>
    /// 指定したプレイヤーが現在乗車エリア内にいるかを確実に判定する
    /// </summary>
    public bool IsPlayerInside(NetworkPlayerSetup playerSetup)
    {
        if (playerSetup == null || _boxCollider == null) return false;

        // マイナスScale対策：絶対値でワールド空間の判定ボックスを計算
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
            NetworkPlayerSetup found = hit.GetComponentInParent<NetworkPlayerSetup>();
            if (found == playerSetup)
            {
                return true;
            }
        }

        // フォールバック：実際に移動する本体（CharacterController等）の座標がボックス内にあるか直接チェック
        CharacterController cc = playerSetup.GetComponentInChildren<CharacterController>(true);
        Transform checkTarget = (cc != null) ? cc.transform : playerSetup.transform;
        Vector3 localPos = transform.InverseTransformPoint(checkTarget.position + Vector3.up * 0.5f) - _boxCollider.center;
        Vector3 halfSize = new Vector3(
            Mathf.Abs(_boxCollider.size.x) * 0.5f,
            Mathf.Abs(_boxCollider.size.y) * 0.5f,
            Mathf.Abs(_boxCollider.size.z) * 0.5f
        );

        return Mathf.Abs(localPos.x) <= halfSize.x &&
               Mathf.Abs(localPos.y) <= halfSize.y &&
               Mathf.Abs(localPos.z) <= halfSize.z;
    }
}