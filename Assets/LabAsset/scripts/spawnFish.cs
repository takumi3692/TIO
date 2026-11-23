using UnityEngine;
using UnityEngine.AI;

namespace underWaterLab
{
    public class spawnFish : MonoBehaviour
    {
        public GameObject[] prefabOptions;
        private int spawnCount;
        public int spawnCountMin;
        public int spawnCountMax;

        private Vector3 center;
        private Vector3 size; // defines spawn area
        [SerializeField] private GameObject fishRoute;

        void Start()
        {
            center = fishRoute.transform.position;
            size = fishRoute.transform.localScale;
            spawnCount = Mathf.RoundToInt(Random.RandomRange(10, 20));
            for (int i = 0; i < spawnCount; i++)
            {
                Vector3 randomPos = GetRandomPointOnNavMesh();
                GameObject prefab = prefabOptions[Random.Range(0, prefabOptions.Length)];

                Instantiate(prefab, randomPos, Quaternion.Euler(0, Random.Range(0f, 360f), 0f));
            }
        }

        Vector3 GetRandomPointOnNavMesh()
        {
            for (int i = 0; i < 30; i++) // try multiple times
            {
                Vector3 randomPoint = center + new Vector3(
                    Random.Range(-size.x / 2, size.x / 2),
                    0,
                    Random.Range(-size.z / 2, size.z / 2)
                );

                NavMeshHit hit;
                if (NavMesh.SamplePosition(randomPoint, out hit, 2f, NavMesh.AllAreas))
                {
                    return hit.position;
                }
            }

            return center; // fallback
        }
    }

}
