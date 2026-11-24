using System.Collections.Generic;
using Fusion;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ReturnVehicleZone : MonoBehaviour
{
    public static ReturnVehicleZone Instance { get; private set; }

    private readonly HashSet<NetworkObject> _playersInside = new HashSet<NetworkObject>();

    private void Awake()
    {
        Instance = this;
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        NetworkObject playerNetObj = other.GetComponentInParent<NetworkObject>();
        if (playerNetObj != null && playerNetObj.GetComponent<NetworkPlayerSetup>() != null)
        {
            _playersInside.Add(playerNetObj);
            Debug.Log($"[帰りの車] 乗車検知 (車内人数: {_playersInside.Count}人)");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        NetworkObject playerNetObj = other.GetComponentInParent<NetworkObject>();
        if (playerNetObj != null && _playersInside.Contains(playerNetObj))
        {
            _playersInside.Remove(playerNetObj);
            Debug.Log($"[帰りの車] 降車検知 (車内人数: {_playersInside.Count}人)");
        }
    }

    public bool IsPlayerInside(NetworkObject playerNetObj)
    {
        _playersInside.RemoveWhere(p => p == null || !p.IsValid);
        return playerNetObj != null && _playersInside.Contains(playerNetObj);
    }
}