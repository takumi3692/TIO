using UnityEngine;

public class DoorController : MonoBehaviour
{
    [Header("開閉の設定")]
    public float openAngle = 90f;       // 開く角度
    public float smoothSpeed = 5f;      // 開閉の滑らかさ
    public bool openOnTouch = false;    // trueなら接触で開く、falseならEキーで開閉
    public bool invertDirection = false;// 開く向きが逆になる場合はチェックを入れる

    private bool isOpen = false;
    private bool isPlayerNearby = false;
    private Transform playerTransform;
    private Quaternion closedRotation;
    private Quaternion targetOpenRotation;

    void Start()
    {
        closedRotation = transform.localRotation;
        targetOpenRotation = closedRotation;
    }

    void Update()
    {
        if (!openOnTouch && isPlayerNearby && Input.GetKeyDown(KeyCode.E))
        {
            if (!isOpen)
            {
                OpenDoorAwayFromPlayer();
            }
            else
            {
                CloseDoor();
            }
        }

        Quaternion target = isOpen ? targetOpenRotation : closedRotation;
        transform.localRotation = Quaternion.Slerp(transform.localRotation, target, Time.deltaTime * smoothSpeed);
    }

    private void OpenDoorAwayFromPlayer()
    {
        isOpen = true;
        float direction = 1f;

        if (playerTransform != null)
        {
            Vector3 toPlayer = (playerTransform.position - transform.position).normalized;
            float dot = Vector3.Dot(transform.forward, toPlayer);
            direction = (dot >= 0) ? -1f : 1f;
        }

        if (invertDirection) direction *= -1f;

        targetOpenRotation = Quaternion.Euler(closedRotation.eulerAngles + new Vector3(0, openAngle * direction, 0));
    }

    private void CloseDoor()
    {
        isOpen = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = true;
            playerTransform = other.transform;

            if (openOnTouch && !isOpen)
            {
                OpenDoorAwayFromPlayer();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = false;
            playerTransform = null;

            if (openOnTouch && isOpen)
            {
                CloseDoor();
            }
        }
    }
}