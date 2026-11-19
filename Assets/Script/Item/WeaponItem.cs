using System.Collections;
using UnityEngine;

public class WeaponItem : MonoBehaviour
{
    [Header("Weapon Settings")]
    public int damage = 25;
    public float attackRange = 1.5f;
    public float attackCooldown = 0.7f;

    [Header("Swing Animation")]
    [SerializeField] private Transform weaponModel;
    [SerializeField] private Vector3 swingRotation =
        new Vector3(-90f, 0f, 0f);
    [SerializeField] private float swingDuration = 0.2f;

    private Quaternion originalRotation;
    private Coroutine swingCoroutine;

    private void Awake()
    {
        if (weaponModel != null)
            originalRotation = weaponModel.localRotation;
    }

    
    public void Attack(Transform cameraTransform)
    {
        // バットを振るアニメーション
        if (weaponModel != null)
        {
            if (swingCoroutine != null)
                StopCoroutine(swingCoroutine);

            swingCoroutine = StartCoroutine(Swing());
        }

        // 球状の攻撃判定
        Vector3 origin = cameraTransform.position;
        Vector3 direction = cameraTransform.forward;

        float hitRadius = 0.5f;

        if (Physics.SphereCast(
            origin,
            hitRadius,
            direction,
            out RaycastHit hit,
            attackRange,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore))
        {
            EnemyHealth enemy =
                hit.collider.GetComponentInParent<EnemyHealth>();

            if (enemy != null)
            {
                enemy.TakeDamage(damage);
                Debug.Log("敵に " + damage + " ダメージ！");
            }
        }
    }


    private IEnumerator Swing()
    {
        Quaternion startRotation = originalRotation;
        Quaternion endRotation =
            originalRotation * Quaternion.Euler(swingRotation);

        float halfDuration = swingDuration / 2f;
        float timer = 0f;

        // 振り下ろす
        while (timer < halfDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / halfDuration);

            weaponModel.localRotation =
                Quaternion.Slerp(startRotation, endRotation, t);

            yield return null;
        }

        timer = 0f;

        // 元の位置に戻す
        while (timer < halfDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / halfDuration);

            weaponModel.localRotation =
                Quaternion.Slerp(endRotation, startRotation, t);

            yield return null;
        }

        weaponModel.localRotation = originalRotation;
        swingCoroutine = null;
    }
}
