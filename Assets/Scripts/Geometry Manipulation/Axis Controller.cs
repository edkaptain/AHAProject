using UnityEngine;

public class AxisController : MonoBehaviour
{
    public GameObject target;
    public bool isUsingAxis;

    [Header("Axis Control")]
    [SerializeField] private GameObject axisX;
    [SerializeField] private GameObject axisY;
    [SerializeField] private GameObject axisZ;

    public bool isTriggered;

    public float accumulatedX;
    public float accumulatedY;
    public float accumulatedZ;

    private void OnEnable()
    {
        gameObject.transform.position = target.transform.position;
        gameObject.transform.rotation = target.transform.rotation;
        gameObject.transform.localScale = target.transform.localScale;
    }

    private void OnDisable()
    {

    }
    /// <summary>
    /// Applies the rotation
    /// </summary>
    public void ApplyRotation()
    {
        target.transform.localRotation = Quaternion.Euler(accumulatedX, accumulatedY, accumulatedZ);
    }

    public void ActivateOnly(GameObject targetAxis)
    {
        isUsingAxis = true;
        axisX.SetActive(axisX == targetAxis);
        axisY.SetActive(axisY == targetAxis);
        axisZ.SetActive(axisZ == targetAxis);
    }

    public void UpdateAxisController(float x, float y, float z)
    {
        accumulatedX += x;
        accumulatedY += y;
        accumulatedZ += z;

    }

    public void ActivateAll()
    {
        isUsingAxis = false;
        axisX.SetActive(true);
        axisY.SetActive(true);
        axisZ.SetActive(true);
    }
}
