using Fusion;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class SimplePlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5.0f;
    [SerializeField] private float gravity = -19.62f;
    [SerializeField] private float jumpHeight = 1.5f;

    [Header("Camera Settings")]
    [SerializeField] private Camera playerCamera;

    private CharacterController _controller;
    private float _verticalVelocity;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();

        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
        }
    }

    public override void Spawned()
    {
        if (HasInputAuthority)
        {
            if (playerCamera != null)
            {
                playerCamera.gameObject.SetActive(true);
                AudioListener listener = playerCamera.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = true;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            if (playerCamera != null)
            {
                playerCamera.gameObject.SetActive(false);
                AudioListener listener = playerCamera.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = false;
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        bool isGrounded = _controller.isGrounded;
        bool jumpPressed = false;

        if (isGrounded && _verticalVelocity < 0)
        {
            _verticalVelocity = -4.0f;
        }

        // Fusionに入力されたデータを取り出して反映
        if (GetInput(out NetworkInputData data))
        {
            // 1. 向き（回転）の適用
            // 本体を左右回転 (Yaw)
            transform.rotation = Quaternion.Euler(0f, data.yaw, 0f);

            // カメラを上下回転 (Pitch)
            if (playerCamera != null)
            {
                playerCamera.transform.localRotation = Quaternion.Euler(data.pitch, 0f, 0f);
            }

            // 2. 移動方向の適用 (更新された回転を基準に進む)
            Vector3 moveDirection = (transform.forward * data.direction.z + transform.right * data.direction.x).normalized;
            _controller.Move(moveDirection * moveSpeed * Runner.DeltaTime);

            jumpPressed = data.jump;
        }

        // 3. ジャンプ計算
        if (isGrounded && jumpPressed)
        {
            _verticalVelocity = Mathf.Sqrt(jumpHeight * -2.0f * gravity);
        }

        // 4. 重力適用
        _verticalVelocity += gravity * Runner.DeltaTime;
        _controller.Move(new Vector3(0, _verticalVelocity, 0) * Runner.DeltaTime);
    }
}