using UnityEngine;

public class VesselProperties : MonoBehaviour
{
    public bool isSelected;

    public void UpdateSelected(bool status)
    {
        isSelected = status;
    }
}
