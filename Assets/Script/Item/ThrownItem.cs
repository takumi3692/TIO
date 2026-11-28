using UnityEngine;

public class ThrownItem : MonoBehaviour
{
    [Header("Impact Settings")]
    [SerializeField] private int impactDamage = 10;

    private bool isThrown;
    private bool isBroken;

    // 投げたときだけ呼び出す
    public void MarkAsThrown()
    {
        isThrown = true;
        isBroken = false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!isThrown || isBroken) return;

        // 敵に衝突したか確認
        EnemyHealth enemy =
            collision.collider.GetComponentInParent<EnemyHealth>();

        if (enemy == null) return;

        isBroken = true;

        // 敵にダメージ
        enemy.TakeDamage(impactDamage);

        Debug.Log("投げたアイテムが敵に命中！");

        // アイテムを破壊
        Destroy(gameObject);
    }
}
