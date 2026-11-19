using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponAttack : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;

    private PlayerInventory inventory;
    private float nextAttackTime;

    private void Awake()
    {
        inventory = GetComponent<PlayerInventory>();

        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();
    }

    private void Update()
    {
        if (Mouse.current == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryAttack();
        }
    }

    private void TryAttack()
    {
        if (Time.time < nextAttackTime) return;

        if (inventory == null || playerCamera == null) return;

        WeaponItem weapon = inventory.GetSelectedWeapon();

        if (weapon == null) return;

        nextAttackTime = Time.time + weapon.attackCooldown;

        Debug.Log("武器で攻撃！");

        weapon.Attack(playerCamera.transform);
    }
}
