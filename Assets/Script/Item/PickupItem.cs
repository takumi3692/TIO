
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PickupItem : MonoBehaviour
{
    private Rigidbody rb;
    private Collider[] itemColliders;
    private bool originalKinematic;
    private bool originalUseGravity;
    private bool[] originalColliderStates;
    private bool initialized;

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (initialized) return;

        rb = GetComponent<Rigidbody>();
        itemColliders = GetComponentsInChildren<Collider>();

        originalKinematic = rb.isKinematic;
        originalUseGravity = rb.useGravity;

        originalColliderStates = new bool[itemColliders.Length];

        for (int i = 0; i < itemColliders.Length; i++)
        {
            originalColliderStates[i] = itemColliders[i].enabled;
        }

        initialized = true;
    }

    public void Store()
    {
        Initialize();

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        rb.useGravity = false;

        foreach (Collider col in itemColliders)
            col.enabled = false;
    }

    public void Release(Vector3 force)
    {
        Initialize();

        transform.SetParent(null);
        gameObject.SetActive(true);

        for (int i = 0; i < itemColliders.Length; i++)
            itemColliders[i].enabled = originalColliderStates[i];

        rb.isKinematic = originalKinematic;
        rb.useGravity = originalUseGravity;

        if (!rb.isKinematic)
            rb.AddForce(force, ForceMode.Impulse);
    }
}
