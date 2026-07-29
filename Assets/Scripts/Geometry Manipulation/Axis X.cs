using Unity.VisualScripting;
using UnityEngine;

public class AxisInteraction : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Ejes axis;
    public bool flag = false;

    public AxisController parent;

    public enum Ejes
    {
        X,
        Y,
        Z
    }

    private Quaternion lastRotation;

    private void Start()
    {
        lastRotation = transform.localRotation;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        parent.isTriggered = true;

        Vector3 scale = transform.localScale;
        scale.y = 3f;
        transform.localScale = scale;

        parent.ActivateOnly(gameObject);

        lastRotation = transform.localRotation;

        Vector3 currentEuler = parent.target.transform.localEulerAngles;
        parent.accumulatedX = NormalizeAngle(currentEuler.x);
        parent.accumulatedY = NormalizeAngle(currentEuler.y);
        parent.accumulatedZ = NormalizeAngle(currentEuler.z);
    }

    private void Update()
    {
        if (parent.isTriggered)
        {
            Quaternion currentRotation = transform.localRotation;

            Quaternion deltaRotation = currentRotation * Quaternion.Inverse(lastRotation);

            deltaRotation.ToAngleAxis(out float deltaAngle, out Vector3 deltaAxis);

            if (deltaAngle > 180f)
                deltaAngle -= 360f;

            float deltaX = deltaAngle * Mathf.Sign(Vector3.Dot(deltaAxis, Vector3.right));

            parent.accumulatedX += deltaX;
            parent.ApplyRotation();

            lastRotation = currentRotation;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        parent.isTriggered = false;

        Vector3 scale = transform.localScale;
        scale.y = 1.105f;
        transform.localScale = scale;

        parent.ActivateAll();
    }

    private float NormalizeAngle(float angle)
    {
        if (angle > 180f)
            angle -= 360f;

        return angle;
    }
}