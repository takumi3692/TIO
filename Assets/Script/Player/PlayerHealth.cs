
using UnityEngine;
using UnityEngine.InputSystem;
using EasyPeasyFirstPersonController;
using UnityEngine.UI;
using System.Collections;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 100;

    [Header("HP UI")]
    [SerializeField] private Slider hpSlider;

    private int currentHealth;
    private bool isDowned = false;
    private bool isDead = false;

    [Header("Player Components")]
    private FirstPersonController movementController;
    private PlayerInventory inventoryController;
    private PlayerPickup pickupController;
    private WeaponAttack weaponAttackController;
    private Animator playerAnimator;

    [Header("Damage Effect")]
    [SerializeField] private Image damageOverlay;
    [SerializeField] private float flashDuration = 0.2f;

    private Coroutine flashCoroutine;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDowned => isDowned;
    public bool IsDead => isDead;

    private void Awake()
    {
        currentHealth = maxHealth;

        if (hpSlider != null)
        {
            hpSlider.maxValue = maxHealth;
            hpSlider.value = currentHealth;
        }

        // プレイヤーのコンポーネントを取得
        movementController = GetComponent<FirstPersonController>();
        inventoryController = GetComponent<PlayerInventory>();
        pickupController = GetComponent<PlayerPickup>();
        weaponAttackController = GetComponent<WeaponAttack>();

        // 子オブジェクトからAnimatorを取得
        playerAnimator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        if (Keyboard.current == null) return;
        if (isDowned || isDead) return;

        // Kキーで10ダメージ（テスト用）
        if (Keyboard.current.kKey.wasPressedThisFrame)
        {
            TakeDamage(10);
        }

        // Hキーで20回復（テスト用）
        if (Keyboard.current.hKey.wasPressedThisFrame)
        {
            Heal(20);
        }
    }

    // HPバー更新
    private void UpdateHPBar()
    {
        if (hpSlider != null)
        {
            hpSlider.value = currentHealth;
        }
    }

    // ダメージを受ける
    public void TakeDamage(int damage)
    {
        if (isDowned || isDead) return;
        if (damage <= 0) return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0);

        UpdateHPBar();
        PlayDamageEffect();

        Debug.Log("HP: " + currentHealth + " / " + maxHealth);

        if (currentHealth <= 0)
        {
            EnterDownedState();
        }
    }

    // 回復（テスト用）
    public void Heal(int amount)
    {
        if (isDowned || isDead) return;
        if (amount <= 0) return;

        currentHealth += amount;
        currentHealth = Mathf.Min(currentHealth, maxHealth);

        UpdateHPBar();

        Debug.Log("HP: " + currentHealth + " / " + maxHealth);
    }

    // ダウン状態にする
    private void EnterDownedState()
    {
        if (isDowned || isDead) return;

        isDowned = true;

        // 移動・視点操作を停止
        if (movementController != null)
        {
            movementController.enabled = false;
        }

        // インベントリ操作を停止
        if (inventoryController != null)
        {
            inventoryController.enabled = false;
        }

        // アイテム取得を停止
        if (pickupController != null)
        {
            pickupController.enabled = false;
        }

        // 武器攻撃を停止
        if (weaponAttackController != null)
        {
            weaponAttackController.enabled = false;
        }

        // ダウンアニメーションを再生
        if (playerAnimator != null)
        {
            playerAnimator.SetTrigger("Down");
        }

        Debug.Log("プレイヤーがダウンしました！");
    }

    // ダメージエフェクト
    private void PlayDamageEffect()
    {
        if (damageOverlay == null) return;

        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
        }

        flashCoroutine = StartCoroutine(DamageFlash());
    }

    private IEnumerator DamageFlash()
    {
        Color color = damageOverlay.color;
        color.a = 0.4f;
        damageOverlay.color = color;

        float elapsed = 0f;

        while (elapsed < flashDuration)
        {
            elapsed += Time.deltaTime;

            color.a = Mathf.Lerp(
                0.4f,
                0f,
                elapsed / flashDuration
            );

            damageOverlay.color = color;

            yield return null;
        }

        color.a = 0f;
        damageOverlay.color = color;

        flashCoroutine = null;
    }
}
