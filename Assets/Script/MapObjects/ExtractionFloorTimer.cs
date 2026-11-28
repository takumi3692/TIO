using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ExtractionFloorTimer : MonoBehaviour
{
    [Header("タイマー設定")]
    [SerializeField] private float countdownDuration = 10.0f;
    [SerializeField] private string playerTag = "Player";

    [Header("画面上部のUI表示")]
    [Tooltip("床を踏んだ瞬間に表示し、終了時に隠す親Panel（任意）")]
    [SerializeField] private GameObject timerPanel;

    [Tooltip("旧UI Textを使う場合（任意）")]
    [SerializeField] private Text timerText;

    [Tooltip("TextMeshProを使う場合（任意）")]
    [SerializeField] private TMP_Text tmpTimerText;

    [SerializeField] private string prefixText = "残り時間: ";
    [SerializeField] private string suffixText = "s";

    private bool timerStarted = false;
    private float remainingTime = 0f;

    // 一度でもこの床を踏んだPlayerオブジェクトの登録
    private readonly HashSet<GameObject> visitedPlayers = new HashSet<GameObject>();

    private void Start()
    {
        SetTimerVisible(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        HandlePlayerEnter(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandlePlayerEnter(collision.gameObject);
    }

    private void HandlePlayerEnter(GameObject hitObj)
    {
        Debug.Log($"[FloorTimer] 接触検知: {hitObj.name} (Tag: {hitObj.tag})");
        GameObject playerObj = FindPlayerRoot(hitObj);
        if (playerObj == null) return;

        // このプレイヤーが床を踏んだことを記録
        visitedPlayers.Add(playerObj);

        // Playerタグのオブジェクトが踏んだ瞬間にカウントダウン＆画面上部表示を開始
        if (!timerStarted)
        {
            timerStarted = true;
            remainingTime = countdownDuration;
            SetTimerVisible(true);
            UpdateTimerText(remainingTime);
            StartCoroutine(CountdownRoutine());
            Debug.Log($"[FloorTimer] {playerObj.name} が床を踏んだため {countdownDuration}秒のカウントダウンを開始！");
        }
    }

    private GameObject FindPlayerRoot(GameObject obj)
    {
        Transform current = obj.transform;
        while (current != null)
        {
            if (current.CompareTag(playerTag))
            {
                return current.gameObject;
            }
            current = current.parent;
        }
        return null;
    }

    private IEnumerator CountdownRoutine()
    {
        while (remainingTime > 0f)
        {
            remainingTime -= Time.deltaTime;
            float displayTime = Mathf.Max(0f, remainingTime);
            UpdateTimerText(displayTime);
            yield return null;
        }

        UpdateTimerText(0f);
        SetTimerVisible(false);
        CheckUnvisitedPlayersAndKill();
    }

    private void CheckUnvisitedPlayersAndKill()
    {
        GameObject[] allPlayers = GameObject.FindGameObjectsWithTag(playerTag);

        foreach (GameObject player in allPlayers)
        {
            if (player == null) continue;

            if (!visitedPlayers.Contains(player))
            {
                PlayerHealth health = player.GetComponent<PlayerHealth>();
                if (health == null)
                {
                    health = player.GetComponentInChildren<PlayerHealth>();
                }

                if (health != null && !health.IsDead)
                {
                    Debug.Log($"[FloorTimer] 時間切れ！床を踏まなかった {player.name} のHPを0にします。");
                    health.Kill();
                }
            }
        }
    }

    private void SetTimerVisible(bool visible)
    {
        if (timerPanel != null)
        {
            timerPanel.SetActive(visible);
        }

        if (timerText != null)
        {
            timerText.gameObject.SetActive(visible);
        }

        if (tmpTimerText != null)
        {
            tmpTimerText.gameObject.SetActive(visible);
        }
    }

    private void UpdateTimerText(float time)
    {
        string value = $"{prefixText}{time:0.0}{suffixText}";

        if (timerText != null)
        {
            timerText.text = value;
        }

        if (tmpTimerText != null)
        {
            tmpTimerText.text = value;
        }
    }
}