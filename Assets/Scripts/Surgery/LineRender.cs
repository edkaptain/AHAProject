using UnityEngine;

public class CuttingLineRender : MonoBehaviour
{
    public GameObject parentVertices;
    public GameObject[] vertices;
    public LineRenderer linerender;

    private bool showSpheres;
    private void OnValidate()
    {
        
    }

    [ContextMenu("Setup vertices points")]
    public void SetUpPoints()
    {
        if (linerender == null) return;
        if (parentVertices == null) return;

        vertices = new GameObject[parentVertices.transform.childCount];

        for (int i = 0; i < parentVertices.transform.childCount; i++)
        {
            vertices[i] = parentVertices.transform.GetChild(i).gameObject;
        }

        if (vertices == null || vertices.Length == 0) return;

        linerender.positionCount = vertices.Length;

        for (int i = 0; i < vertices.Length; i++)
        {
            if (vertices[i] != null)
            {
                linerender.SetPosition(i, vertices[i].transform.position);
            }
        }
    }

    [ContextMenu("Show spheres")]
    public void ShowSpheres()
    {
        if (parentVertices == null) return;

        showSpheres = !showSpheres;

        foreach (Transform t in parentVertices.transform)
        {
            MeshRenderer renderer = t.GetComponent<MeshRenderer>();

            if(renderer != null)
            {
                renderer.enabled = showSpheres;
            };

        }
    }
}
