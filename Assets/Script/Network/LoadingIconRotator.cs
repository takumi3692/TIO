using UnityEngine;

public class LoadingIconRotator : MonoBehaviour
{
    [SerializeField] private float rotateSpeed = -200f; // マイナスで時計回り

    private void Update()
    {
        transform.Rotate(0f, 0f, rotateSpeed * Time.deltaTime);
    }
}