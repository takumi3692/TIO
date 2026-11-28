using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAIController : MonoBehaviour
{
    private enum State
    {
        Patrol, // 巡回
        Chase,  // 追跡
        Attack  // 攻撃
    }

    [Header("巡回設定")]
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private float patrolSpeed = 2.5f;
    [SerializeField] private float waitTimeAtPoint = 1.5f;
    [SerializeField] private float waypointReachDistance = 0.8f;

    [Header("視界・索敵設定")]
    [SerializeField] private string targetTag = "Player";
    [SerializeField] private float viewDistance = 12f;
    [SerializeField, Range(0f, 180f)] private float viewAngle = 90f; // 全体の視野角（例：90度なら左右45度ずつ）
    [SerializeField] private float eyeHeight = 1.4f; // 視線の高さ
    [SerializeField] private LayerMask obstacleMask = ~0; // 壁・障害物レイヤー（Playerや敵自身は除外設定推奨）
    [SerializeField] private float loseTargetTime = 3.0f; // 見失ってから巡回に戻るまでの猶予

    [Header("追跡・攻撃設定")]
    [SerializeField] private float chaseSpeed = 5.0f;
    [SerializeField] private float attackRange = 2.0f;
    [SerializeField] private float attackCooldown = 1.5f;
    [SerializeField] private float attackWindup = 0.35f; // 攻撃前の溜め（予兆）
    [SerializeField] private int attackDamage = 10;

    private NavMeshAgent agent;
    private State currentState = State.Patrol;

    private int currentPatrolIndex = 0;
    private float waitTimer = 0f;
    private float loseTargetTimer = 0f;
    private float lastAttackTime = -999f;
    private bool isAttacking = false;

    private Transform targetPlayer;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        EnterPatrolState();
    }

    private void Update()
    {
        if (isAttacking) return;

        // 毎フレーム視界内にPlayerがいるかチェック
        bool canSeePlayer = CheckCanSeePlayer();

        if (canSeePlayer && targetPlayer != null)
        {
            loseTargetTimer = 0f;

            float distanceToPlayer = Vector3.Distance(transform.position, targetPlayer.position);
            if (distanceToPlayer <= attackRange)
            {
                currentState = State.Attack;
            }
            else
            {
                currentState = State.Chase;
            }
        }
        else if (currentState == State.Chase || currentState == State.Attack)
        {
            // 一度見つけたが視界から外れた場合、一定時間は最後の位置へ向かう
            loseTargetTimer += Time.deltaTime;
            if (loseTargetTimer >= loseTargetTime)
            {
                targetPlayer = null;
                EnterPatrolState();
            }
            else
            {
                currentState = State.Chase;
            }
        }

        switch (currentState)
        {
            case State.Patrol:
                UpdatePatrol();
                break;
            case State.Chase:
                UpdateChase();
                break;
            case State.Attack:
                UpdateAttack();
                break;
        }
    }

    private void EnterPatrolState()
    {
        currentState = State.Patrol;
        agent.isStopped = false;
        agent.speed = patrolSpeed;
        waitTimer = 0f;

        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            agent.SetDestination(patrolPoints[currentPatrolIndex].position);
        }
    }

    private void UpdatePatrol()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return;

        if (!agent.pathPending && agent.remainingDistance <= waypointReachDistance)
        {
            waitTimer += Time.deltaTime;
            if (waitTimer >= waitTimeAtPoint)
            {
                currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
                agent.SetDestination(patrolPoints[currentPatrolIndex].position);
                waitTimer = 0f;
            }
        }
    }

    private void UpdateChase()
    {
        if (targetPlayer == null)
        {
            EnterPatrolState();
            return;
        }

        agent.isStopped = false;
        agent.speed = chaseSpeed;
        agent.SetDestination(targetPlayer.position);
    }

    private void UpdateAttack()
    {
        if (targetPlayer == null)
        {
            EnterPatrolState();
            return;
        }

        agent.isStopped = true;
        FaceTarget(targetPlayer.position);

        float distanceToPlayer = Vector3.Distance(transform.position, targetPlayer.position);
        if (distanceToPlayer > attackRange * 1.15f)
        {
            // 攻撃範囲外に出たら追跡に戻す
            currentState = State.Chase;
            agent.isStopped = false;
            return;
        }

        if (Time.time >= lastAttackTime + attackCooldown)
        {
            StartCoroutine(PerformAttack());
        }
    }

    private IEnumerator PerformAttack()
    {
        isAttacking = true;
        lastAttackTime = Time.time;

        // 攻撃前の溜め
        if (targetPlayer != null)
        {
            FaceTarget(targetPlayer.position);
        }
        yield return new WaitForSeconds(attackWindup);

        // 溜め後に攻撃判定
        if (targetPlayer != null)
        {
            float dist = Vector3.Distance(transform.position, targetPlayer.position);
            if (dist <= attackRange + 0.5f)
            {
                ExecuteAttackDamage(targetPlayer.gameObject);
            }
        }

        isAttacking = false;
    }

private void ExecuteAttackDamage(GameObject playerObj)
    {
        if (playerObj == null || !playerObj.CompareTag(targetTag)) return;

        // 対象自身、または親階層にある PlayerHealth を取得
        PlayerHealth playerHealth = playerObj.GetComponentInParent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(attackDamage);
        }
        else
        {
            Debug.LogWarning($"[EnemyAI] {playerObj.name} に PlayerHealth コンポーネントが見つかりませんでした。");
        }
    }

    private bool CheckCanSeePlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag(targetTag);
        if (players == null || players.Length == 0) return false;

        Vector3 eyeOrigin = transform.position + Vector3.up * eyeHeight;

        foreach (GameObject p in players)
        {
            if (p == null) continue;

            // Playerの中心付近を狙う（コライダー中心があればそちらを優先）
            Vector3 targetPoint = p.transform.position + Vector3.up * 1.0f;
            Collider col = p.GetComponent<Collider>();
            if (col != null)
            {
                targetPoint = col.bounds.center;
            }

            Vector3 toTarget = targetPoint - eyeOrigin;
            float distance = toTarget.magnitude;

            // 1. 距離判定
            if (distance > viewDistance) continue;

            // 2. 視野角判定（XZ平面で判定したい場合はYを0にして計算すると安定します）
            Vector3 flatForward = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
            Vector3 flatDir = new Vector3(toTarget.x, 0f, toTarget.z).normalized;
            if (flatForward.sqrMagnitude > 0.001f && flatDir.sqrMagnitude > 0.001f)
            {
                float angle = Vector3.Angle(flatForward, flatDir);
                if (angle > viewAngle * 0.5f) continue;
            }

            // 3. 遮蔽物判定（壁の向こうから見えないようにする）
            Vector3 dirNormalized = toTarget / distance;
            if (Physics.Raycast(eyeOrigin, dirNormalized, out RaycastHit hit, distance, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                // 当たったものがPlayer自身やその子オブジェクトなら視界が通っている
                if (hit.transform == p.transform || hit.transform.IsChildOf(p.transform))
                {
                    targetPlayer = p.transform;
                    return true;
                }
            }
            else
            {
                // obstacleMaskでPlayerレイヤーを除外している場合のフォールバック
                targetPlayer = p.transform;
                return true;
            }
        }

        return false;
    }

    private void FaceTarget(Vector3 destination)
    {
        Vector3 direction = destination - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
        }
    }

    // Sceneビューで視界・攻撃範囲を可視化
    private void OnDrawGizmosSelected()
    {
        Vector3 eyeOrigin = transform.position + Vector3.up * eyeHeight;

        // 視界距離
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(eyeOrigin, viewDistance);

        // 攻撃範囲
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // 視野角の左右ライン
        Gizmos.color = new Color(1f, 0.8f, 0f, 0.9f);
        Vector3 leftDir = Quaternion.Euler(0f, -viewAngle * 0.5f, 0f) * transform.forward;
        Vector3 rightDir = Quaternion.Euler(0f, viewAngle * 0.5f, 0f) * transform.forward;
        Gizmos.DrawRay(eyeOrigin, leftDir * viewDistance);
        Gizmos.DrawRay(eyeOrigin, rightDir * viewDistance);
    }
}