using UnityEngine;

public class TreasureItem : MonoBehaviour
{
    [Header("お宝の設定")]
    [Tooltip("StageGameManagerの持ち越しプレハブ登録名と一致させるキー")]
    [SerializeField] private string itemKey = "Scrap";
    [SerializeField] private string treasureName = "スクラップ";
    [SerializeField] private int value = 100;

    public string ItemKey => string.IsNullOrWhiteSpace(itemKey) ? gameObject.name.Replace("(Clone)", "").Trim() : itemKey;
    public string TreasureName => treasureName;
    public int Value => value;
}