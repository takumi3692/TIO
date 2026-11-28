using UnityEngine;

public class AbyssKillZone : MonoBehaviour
{
    [Header("判定設定")]
    [SerializeField] private string playerTag = "Player";

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[Abyss] OnTriggerEnter: {other.name} (Tag: {other.tag})");
        TryKillPlayer(other.gameObject);
    }

    private void OnTriggerStay(Collider other)
    {
        TryKillPlayer(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log($"[Abyss] OnCollisionEnter: {collision.gameObject.name}");
        TryKillPlayer(collision.gameObject);
    }

private void TryKillPlayer(GameObject hitObj)
{
    Debug.Log($"[Abyss] TryKillPlayer 開始: {hitObj.name}");

    GameObject playerObj = FindPlayerRoot(hitObj);
    if (playerObj == null)
    {
        Debug.LogWarning($"[Abyss] '{playerTag}' タグが親階層に見つかりません: {hitObj.name}");
        return;
    }

    PlayerHealth health = playerObj.GetComponent<PlayerHealth>()
        ?? playerObj.GetComponentInChildren<PlayerHealth>()
        ?? playerObj.transform.root.GetComponentInChildren<PlayerHealth>();

    if (health == null)
    {
        Debug.LogWarning($"[Abyss] {playerObj.name} 周辺に PlayerHealth が見つかりません！");
        return;
    }

    Debug.Log($"[Abyss] {health.gameObject.name} のHPを0にします！");
    health.TakeDamage(health.CurrentHealth);
}

    private GameObject FindPlayerRoot(GameObject obj)
    {
        Transform current = obj.transform;
        while (current != null)
        {
            if (current.CompareTag(playerTag))
            {
                return current.gameObject;
            }
            current = current.parent;
        }
        return null;
    }
}