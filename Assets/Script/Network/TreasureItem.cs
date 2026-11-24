using UnityEngine;

public class TreasureItem : MonoBehaviour
{
    [Header("お宝の価値設定")]
    [SerializeField] private string treasureName = "スクラップ";
    [SerializeField] private int value = 100;

    public string TreasureName => treasureName;
    public int Value => value;
}