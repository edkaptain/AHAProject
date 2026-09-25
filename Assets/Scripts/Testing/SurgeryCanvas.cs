using UnityEngine;
using UnityEngine.UI;

public class SurgeryCanvas : MonoBehaviour
{
    public Text distanceText;

   public void UpdateDistance(float value)
    {
        distanceText.text = "Distance:" + value.ToString();
    }
}
