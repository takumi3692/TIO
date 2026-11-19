
using UnityEngine;

public class FloatingPickup : MonoBehaviour
{
    [Header("Floating Settings")]
    public float floatHeight = 0.08f;
    public float floatSpeed = 2f;
    public float rotateSpeed = 60f;

    [Header("Drop Settings")]
    public float groundCheckHeight = 2f;
    public float groundCheckDistance = 10f;
    public float groundOffset = 0.08f;

    // 落としたときの親オブジェクトの向き
    public Vector3 dropRotation = Vector3.zero;

    private Vector3 startPosition;
    private bool wasStored;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        wasStored = transform.parent != null;
        startPosition = transform.position;
    }

    private void Update()
    {
        // インベントリに入っている間
        if (transform.parent != null)
        {
            wasStored = true;
            return;
        }

        // 投げられて移動している間は浮遊処理を停止
        if (rb != null &&
            !rb.isKinematic &&
            rb.linearVelocity.sqrMagnitude > 0.1f)
        {
            return;
        }

        // インベントリから落とされた瞬間
        if (wasStored)
        {
            wasStored = false;

            // 向きをリセット
            transform.rotation = Quaternion.Euler(dropRotation);

            // 地面を探す
            Vector3 rayOrigin =
                transform.position + Vector3.up * groundCheckHeight;

            if (Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                groundCheckDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore))
            {
                transform.position =
                    hit.point + Vector3.up * groundOffset;
            }

            startPosition = transform.position;
        }

        // 上下に浮遊
        float y = Mathf.Sin(Time.time * floatSpeed) * floatHeight;

        transform.position =
            startPosition + Vector3.up * y;

        // 回転
        transform.Rotate(
            Vector3.up,
            rotateSpeed * Time.deltaTime,
            Space.World
        );
    }
}
