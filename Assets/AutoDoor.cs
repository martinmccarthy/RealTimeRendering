using UnityEngine;

public class AutoDoor : MonoBehaviour
{
    [Header("Door Settings")]
    public float openAngle = 90f;    // Y rotation relative to closed
    public float openSpeed = 3f;     // How fast door opens/closes

    [Header("Detection")]
    public string aiTag = "AI";      // Tag on your AI, e.g. "AI"

    private bool shouldOpen = false;
    private Quaternion closedRotation;
    private Quaternion openRotation;

    private void Awake()
    {
        // Remember the original rotation as "closed"
        closedRotation = transform.localRotation;
        openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
    }

    private void Update()
    {
        // Pick which rotation we are moving toward
        Quaternion targetRot = shouldOpen ? openRotation : closedRotation;

        // Smoothly rotate between current and target
        transform.localRotation = Quaternion.Slerp(
            transform.localRotation,
            targetRot,
            openSpeed * Time.deltaTime
        );
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"{name}: OnTriggerEnter with {other.name}");

        if (other.CompareTag(aiTag))
        {
            Debug.Log($"{name}: AI entered, opening door.");
            shouldOpen = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Debug.Log($"{name}: OnTriggerExit with {other.name}");

        if (other.CompareTag(aiTag))
        {
            Debug.Log($"{name}: AI left, closing door.");
            shouldOpen = false;
        }
    }
}
