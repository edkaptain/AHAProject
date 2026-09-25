using UnityEngine;

public class SnapObject : MonoBehaviour
{
    [Header("Properties")]
    public SnapTarget currentTarget;
    public bool isSnapped;
    public GameObject interactable;
    private bool isSelected;

    [Header("Settings")]
    public float vibrationTime = 0.2f;

    public void Lock()
    {
        if (currentTarget == null)
        {
            Debug.LogError("Current target no esta asignado", this);
            return;
        }

        isSnapped = true;

        if (interactable != null)
        {
            interactable.SetActive(false);
        }

        transform.SetPositionAndRotation(currentTarget.transform.position, currentTarget.transform.rotation);
    }

    public void IsSelected(bool status)
    {
        isSelected = status;
    }

    public void SetParent(Transform obj)
    {
        transform.parent = obj;
    }
}
