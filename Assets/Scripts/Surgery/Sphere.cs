using UnityEngine;

public class SphereScript : MonoBehaviour
{
    public bool isAllowed;

    public CuttingLineRender cuttingLine;
    public int sphereIndex;

    public void Configure(
        CuttingLineRender line,
        int index
    )
    {
        cuttingLine = line;
        sphereIndex = index;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Interactor"))
            return;

        if (!isAllowed)
            return;

        isAllowed = false;
        Debug.LogWarning($"The {other} has touched the sphere {sphereIndex}");
        cuttingLine.SphereTouched(sphereIndex);
    }
}