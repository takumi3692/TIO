using UnityEngine;

public class DoorController : MonoBehaviour
{
    [Header("開閉の設定")]
    public float openAngle = 90f;       // 開く角度（逆向きに開く場合は -90 にする）
    public float smoothSpeed = 5f;      // 開閉の滑らかさ
    public bool openOnTouch = false;    // trueなら体当たり(接触)で開く、falseならEキーで開閉

    private bool isOpen = false;
    private bool isPlayerNearby = false;
    private Quaternion closedRotation;
    private Quaternion openRotation;

    void Start()
    {
        // 初期の角度を記憶し、開いた時の目標角度を計算
        closedRotation = transform.localRotation;
        openRotation = Quaternion.Euler(transform.localEulerAngles + new Vector3(0, openAngle, 0));
    }

    void Update()
    {
        // Eキーでの開閉モード（プレイヤーが近くにいる時）
        if (!openOnTouch && isPlayerNearby && Input.GetKeyDown(KeyCode.E))
        {
            isOpen = !isOpen;
        }

        // 目標の角度に向かって滑らかに回転させる
        Quaternion targetRotation = isOpen ? openRotation : closedRotation;
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, Time.deltaTime * smoothSpeed);
    }

    // プレイヤーが扉のエリアに入った時
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = true;

            // 体当たり（接触）モードなら自動で開く
            if (openOnTouch)
            {
                isOpen = true;
            }
        }
    }

    // プレイヤーが扉のエリアから離れた時
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = false;

            // 体当たりモードの場合、離れたら自動で閉まる（閉めたくない場合はこのif文を削除）
            if (openOnTouch)
            {
                isOpen = false;
            }
        }
    }
}