using UnityEngine;
using UnityEngine.UI;

public class ForwardOnlyPathLockedGrab : MonoBehaviour
{
    [Header("Path")]
    public Transform[] points;

    [Header("Grab / Hand")]
    public Transform handTransform;

    [Header("Settings")]
    public float snapDistance = 0.02f;
    public float reachEndThreshold = 0.98f;
    public bool lockedToPath = false;

    private int currentIndex = 0;
    private float currentT = 0f;

    [Header("Testing")]
    public Text progressText;
    public bool isLocked;
    public Surgery_001 surger_key;
    public LineRenderer lineRenderer;
    public GameObject grabGameobject;
    void Update()
    {
        if (points == null || points.Length < 2) return;
        if (handTransform == null) return;

        if (!lockedToPath)
        {
            TryLockToStart();
        }
        else
        {
            MoveForwardOnlyOnPath();
        }
    }

    private void TryLockToStart()
    {
        float distance = Vector3.Distance(transform.position, points[0].position);

        if (distance <= snapDistance)
        {
            lockedToPath = true;
            currentIndex = 0;
            currentT = 0f;

            transform.position = points[0].position;

            Debug.Log("Objeto bloqueado al inicio del path");
        }
    }

    private void MoveForwardOnlyOnPath()
    {
        if (currentIndex >= points.Length - 1)
        {
            //transform.position = points[points.Length - 1].position;

            if (progressText != null)
                progressText.text = "100%";

            grabGameobject.SetActive(true);
            lineRenderer.enabled = false;
            return;
        }

        Vector3 a = points[currentIndex].position;
        Vector3 b = points[currentIndex + 1].position;

        float handT = GetTOnSegment(a, b, handTransform.position);

        if (isLocked)
        {

            currentT = Mathf.Max(currentT, handT);
        }
        else
        {
            currentT = handT;
        }



        transform.position = Vector3.Lerp(a, b, currentT);

        Vector3 direction = b - a;

        if (direction.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(direction.normalized);
        }

        // Porcentaje total del path
        float totalSegments = points.Length - 1;
        float totalProgress = (currentIndex + currentT) / totalSegments;
        float percent = totalProgress * 100f;
        surger_key.key = percent; // testing

        if (progressText != null)
        {
            progressText.text = percent.ToString("F0") + "%";
        }

        if (currentT >= reachEndThreshold)
        {
            currentIndex++;
            currentT = 0f;

            transform.position = points[currentIndex].position;

            Debug.Log("Pasó al siguiente punto: " + currentIndex);
        }
    }

    private float GetTOnSegment(Vector3 a, Vector3 b, Vector3 point)
    {
        Vector3 ab = b - a;
        Vector3 ap = point - a;

        if (ab.sqrMagnitude <= 0.0001f)
        {
            return 0f;
        }

        float t = Vector3.Dot(ap, ab) / ab.sqrMagnitude;

        return Mathf.Clamp01(t);
    }
}