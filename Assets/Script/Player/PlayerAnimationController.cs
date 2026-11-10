using UnityEngine;
using EasyPeasyFirstPersonController;

public class PlayerAnimationController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;

    private FirstPersonController player;
    private bool wasJumpPressed;

    private void Awake()
    {
        player = GetComponentInParent<FirstPersonController>();

        if (animator == null)
            animator = GetComponent<Animator>();

        if (player == null)
            Debug.LogError("FirstPersonControllerが見つかりません");

        if (animator == null)
            Debug.LogError("Animatorが見つかりません");
    }

    private void Update()
    {
        if (player == null || animator == null)
            return;

        // 移動入力を取得
        Vector2 moveInput = player.input.moveInput;

        // Shiftキーの入力を取得
        bool isMoving = moveInput.magnitude > 0.1f;
        bool isSprinting = player.input.sprint && isMoving;

        // Animatorに移動情報を渡す
        animator.SetFloat("Speed", moveInput.magnitude);
        animator.SetBool("IsSprinting", isSprinting);

        // 地面にいるかどうか
        animator.SetBool("IsGrounded", player.isGrounded);

        // ジャンプ入力（押した瞬間だけ）
        bool jumpPressed = player.input.jump;

        if (jumpPressed && !wasJumpPressed && player.isGrounded)
        {
            animator.SetTrigger("Jump");
        }

        wasJumpPressed = jumpPressed;
    }
}
