using UnityEngine;

public class ShutterLeverController : MonoBehaviour
{
    [Header("対象オブジェクト")]
    [Tooltip("上に開くシャッター（例: Cube (7)）")]
    public Transform shutter;

    [Tooltip("操作するレバー（根元ごと動かす対象、例: Cube (8)）")]
    public Transform lever;

    [Header("操作設定")]
    public KeyCode interactKey = KeyCode.E;
    [Tooltip("開けきるまでに必要な長押し秒数")]
    public float holdDuration = 5.0f;

    [Tooltip("途中でEキーを離したときに進捗を戻す場合はtrue")]
    public bool resetOnRelease = false;

    [Header("シャッターの移動量（Y軸）")]
    [Tooltip("開いたときに現在Y座標から上に動かす距離")]
    public float shutterOpenOffsetY = 3.0f;

    [Header("レバーの移動と角度（根元ごと下げる）")]
    [Tooltip("5秒押したときにレバーを下に下げる量（マイナスで下方向）")]
    public float leverDropOffsetY = -0.52f;

    [Tooltip("押し始めのZ角度")]
    public float leverStartZ = 15.0f;

    [Tooltip("5秒押したときのZ角度")]
    public float leverEndZ = -15.0f;

    private float holdTimer = 0f;
    private Vector3 shutterClosedPos;
    private Vector3 shutterOpenPos;
    private Vector3 leverStartPos;
    private Vector3 leverEndPos;
    private Vector3 leverBaseEuler;
    private bool isCompleted = false;

    void Start()
    {
        if (shutter != null)
        {
            shutterClosedPos = shutter.position;
            shutterOpenPos = shutterClosedPos + Vector3.up * shutterOpenOffsetY;
        }

        if (lever != null)
        {
            leverStartPos = lever.position;
            leverEndPos = leverStartPos + Vector3.up * leverDropOffsetY;
            leverBaseEuler = lever.localEulerAngles;
            SetLeverZ(leverStartZ);
        }
    }

    void Update()
    {
        if (shutter == null || lever == null || isCompleted) return;

        if (Input.GetKey(interactKey))
        {
            holdTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(holdTimer / holdDuration);
            ApplyProgress(progress);

            if (progress >= 1f)
            {
                isCompleted = true;
            }
        }
        else if (resetOnRelease && holdTimer > 0f)
        {
            holdTimer = Mathf.Max(0f, holdTimer - Time.deltaTime * 2f);
            ApplyProgress(holdTimer / holdDuration);
        }
    }

    private void ApplyProgress(float progress)
    {
        shutter.position = Vector3.Lerp(shutterClosedPos, shutterOpenPos, progress);
        lever.position = Vector3.Lerp(leverStartPos, leverEndPos, progress);

        float currentZ = Mathf.Lerp(leverStartZ, leverEndZ, progress);
        SetLeverZ(currentZ);
    }

    private void SetLeverZ(float zAngle)
    {
        lever.localEulerAngles = new Vector3(leverBaseEuler.x, leverBaseEuler.y, zAngle);
    }
}