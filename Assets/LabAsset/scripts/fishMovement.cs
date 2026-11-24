using UnityEngine;
using UnityEngine.AI;

namespace underWaterLab
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class fishMovement : MonoBehaviour
    {
        public float wanderRadius = 20f;
        public float stoppingDistance = 1.5f;

        private NavMeshAgent agent;

        void Start()
        {
            agent = GetComponent<NavMeshAgent>();
            agent.stoppingDistance = stoppingDistance;

            SetNewDestination();
        }

        void Update()
        {
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                SetNewDestination();
            }
        }

        void SetNewDestination()
        {
            Vector3 randomDirection = Random.insideUnitSphere * wanderRadius;
            randomDirection += transform.position;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomDirection, out hit, wanderRadius, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }
        }
    }

}
