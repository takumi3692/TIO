using Fusion;
using UnityEngine;

public class NetworkPlayerSetup : NetworkBehaviour
{
    [Header("実際に移動・回転するキャラクター本体（子のFirstPersonControllerを指定）")]
    [SerializeField] private Transform characterRoot;

    [Header("キャラクターのAnimator（PrototypeCharacterのAnimatorを指定）")]
    [SerializeField] private Animator characterAnimator;

    [Header("自分のキャラの時だけONにするオブジェクト（CameraParent, 自分用UI Canvasなど）")]
    [SerializeField] private GameObject[] localOnlyObjects;

    [Header("自分のキャラの時だけONにするスクリプト（手動追加用）")]
    [SerializeField] private UnityEngine.Behaviour[] localOnlyComponents;

    [Networked] private Vector3 SyncedPosition { get; set; }
    [Networked] private Quaternion SyncedRotation { get; set; }
    [Networked] private float SyncedSpeed { get; set; }
    [Networked] private NetworkBool SyncedIsSprinting { get; set; }

    private bool _isInitialized = false;

    public void InitializeSpawnTransform(Vector3 pos, Quaternion rot)
    {
        SyncedPosition = pos;
        SyncedRotation = rot;
    }

    public override void Spawned()
    {
        if (characterRoot == null)
        {
            characterRoot = transform;
        }

        if (characterAnimator == null)
        {
            characterAnimator = GetComponentInChildren<Animator>(true);
        }

        bool isLocalPlayer = HasInputAuthority;

        // 1. 自分専用オブジェクトのON/OFF切り替え
        if (localOnlyObjects != null)
        {
            foreach (var obj in localOnlyObjects)
            {
                if (obj != null) obj.SetActive(isLocalPlayer);
            }
        }

        // 2. 手動指定されたスクリプトのON/OFF切り替え
        if (localOnlyComponents != null)
        {
            foreach (var comp in localOnlyComponents)
            {
                if (comp != null) comp.enabled = isLocalPlayer;
            }
        }

        // 3. 自動安全装置：プレハブ内のローカル用自作スクリプト（移動・アニメーション入力・拾得など）をすべて自動で切り替え
        MonoBehaviour[] allScripts = GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var script in allScripts)
        {
            if (script == null) continue;

            // ネットワーク同期用スクリプト（NetworkBehaviour継承）は全端末で動かすため除外
            if (script is NetworkBehaviour) continue;

            // Unity標準・TMPのコンポーネントは除外
            string ns = script.GetType().Namespace;
            if (!string.IsNullOrEmpty(ns) && (ns.StartsWith("UnityEngine") || ns.StartsWith("TMPro") || ns.StartsWith("Fusion")))
            {
                continue;
            }

            // それ以外の自作スクリプトは自分のキャラの時だけ有効化
            script.enabled = isLocalPlayer;
        }

        // 4. Animator自体は全端末で必ず有効にしておく（Tポーズ防止）
        if (characterAnimator != null)
        {
            characterAnimator.enabled = true;
        }

        // 5. CameraとAudioListenerを自分だけ有効化
        Camera[] cameras = GetComponentsInChildren<Camera>(true);
        foreach (var cam in cameras)
        {
            cam.enabled = isLocalPlayer;
            var listener = cam.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = isLocalPlayer;
        }

        // 6. 初期スポーン位置・向きを全端末で確実に適用する
        ApplyTransformSafely(SyncedPosition, SyncedRotation);
        _isInitialized = true;

        if (isLocalPlayer)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void ApplyTransformSafely(Vector3 targetPos, Quaternion targetRot)
    {
        var cc = characterRoot.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        characterRoot.position = targetPos;
        characterRoot.rotation = targetRot;
        Physics.SyncTransforms();

        if (cc != null) cc.enabled = true;
    }

    public override void FixedUpdateNetwork()
    {
        if (!_isInitialized) return;

        if (HasInputAuthority && characterRoot != null)
        {
            float currentSpeed = 0f;
            bool currentSprint = false;

            if (characterAnimator != null)
            {
                currentSpeed = characterAnimator.GetFloat("Speed");
                currentSprint = characterAnimator.GetBool("IsSprinting");
            }

            if (HasStateAuthority)
            {
                SyncedPosition = characterRoot.position;
                SyncedRotation = characterRoot.rotation;
                SyncedSpeed = currentSpeed;
                SyncedIsSprinting = currentSprint;
            }
            else
            {
                RPC_UpdateState(characterRoot.position, characterRoot.rotation, currentSpeed, currentSprint);
            }
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_UpdateState(Vector3 pos, Quaternion rot, float speed, NetworkBool isSprinting)
    {
        SyncedPosition = pos;
        SyncedRotation = rot;
        SyncedSpeed = speed;
        SyncedIsSprinting = isSprinting;
    }

    private void Update()
    {
        if (!_isInitialized) return;

        if (!HasInputAuthority && characterRoot != null)
        {
            characterRoot.position = Vector3.Lerp(characterRoot.position, SyncedPosition, Time.deltaTime * 15f);
            characterRoot.rotation = Quaternion.Slerp(characterRoot.rotation, SyncedRotation, Time.deltaTime * 15f);

            if (characterAnimator != null)
            {
                characterAnimator.SetFloat("Speed", SyncedSpeed);
                characterAnimator.SetBool("IsSprinting", SyncedIsSprinting);
            }
        }
    }
}