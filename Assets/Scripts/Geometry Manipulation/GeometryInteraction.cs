using Oculus.Interaction;
using UnityEngine;
/// <summary>
/// This script functions as the main control for the object
/// </summary>
public class GeometryInteraction : MonoBehaviour
{
    #region ===== Inspector References =====
    private Grabbable grabbable;
    public int grabCount;
    public bool isUsing;
    public AxisController axisController;
    public ModelController modelController;

    private Quaternion lastRotation;

    
    #endregion
    private void Awake()
    {
        grabbable = GetComponent<Grabbable>();
    }

    private void Update()
    {
        // Check how many interactors are grabbing this object
        grabCount = grabbable.PointsCount;

        if (grabCount == 2)
        {
            Debug.Log("Object is grabbed with TWO hands");

            // 👉 Do something here
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // This indicates if geometry interaction is using (flag)
        isUsing = true;
        // Disables the sliders for geometry manipulation
        modelController.DesactivateSliders();

        lastRotation = transform.localRotation;
    }

    private void OnTriggerStay(Collider other)
    {
        if (axisController.isUsingAxis == false)
        {
            Quaternion currentRotation = transform.localRotation;

            Quaternion deltaRotation = currentRotation * Quaternion.Inverse(lastRotation);

            deltaRotation.ToAngleAxis(out float deltaAngle, out Vector3 deltaAxis);

            if (deltaAngle > 180f)
                deltaAngle -= 360f;

            float deltaX = deltaAngle * Mathf.Sign(Vector3.Dot(deltaAxis, Vector3.right));
            float deltaY = deltaAngle * Mathf.Sign(Vector3.Dot(deltaAxis, Vector3.up));
            float deltaZ = deltaAngle * Mathf.Sign(Vector3.Dot(deltaAxis, Vector3.forward));

            if (axisController.isUsingAxis == false)
            {
                axisController.UpdateAxisController(deltaX, deltaY, deltaZ);
            }

            lastRotation = currentRotation;
        }

        modelController.UpdateDashboard();
        modelController.UpdateSliders();

    }

    private void OnTriggerExit(Collider other)
    {
        // Enables the sliders controllers again
        modelController.ActivateSliders();
        isUsing = false;
    }
}