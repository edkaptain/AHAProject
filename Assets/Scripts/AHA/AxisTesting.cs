using UnityEngine;

public class AxisTesting : MonoBehaviour
{
    public Collider mainCollider;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        
        if (mainCollider != null)
        {
            mainCollider.enabled = false;
        }

    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;       

        if (mainCollider != null)
        {
            mainCollider.enabled = true;
        }
    }

    public void ActivateExternalCollider()
    {
        mainCollider.enabled = true;
    }
}