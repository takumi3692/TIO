using UnityEngine;

public class SpawnArea : MonoBehaviour
{
    [Header("4人分の立ち位置（空欄なら子オブジェクトを自動取得）")]
    [SerializeField] private Transform[] spawnPoints;

    private void Awake()
    {
        CollectChildPointsIfEmpty();
    }

    private void CollectChildPointsIfEmpty()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            spawnPoints = new Transform[transform.childCount];
            for (int i = 0; i < transform.childCount; i++)
            {
                spawnPoints[i] = transform.GetChild(i);
            }
        }
    }

    /// <summary>
    /// 指定されたプレイヤー番号（0〜3）に対応する位置と向きを返す
    /// </summary>
    public (Vector3 position, Quaternion rotation) GetSpawnTransform(int playerIndex)
    {
        CollectChildPointsIfEmpty();

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            int index = playerIndex % spawnPoints.Length;
            if (spawnPoints[index] != null)
            {
                return (spawnPoints[index].position, spawnPoints[index].rotation);
            }
        }

        // 万が一子オブジェクトが無い場合は親の位置から横にずらす
        Vector3 fallbackPos = transform.position + transform.right * (playerIndex * 2.0f);
        return (fallbackPos, transform.rotation);
    }

    // Sceneビューで立ち位置と正面方向を見やすく表示する
    private void OnDrawGizmos()
    {
        CollectChildPointsIfEmpty();
        if (spawnPoints == null) return;

        Gizmos.color = Color.cyan;
        foreach (var point in spawnPoints)
        {
            if (point == null) continue;
            Gizmos.DrawWireSphere(point.position, 0.4f);
            Gizmos.DrawRay(point.position, point.forward * 1.2f);
        }
    }
}