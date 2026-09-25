using UnityEngine;

/// <summary>
/// Creates a summary for the figure
/// </summary>
public class MeshSummary : MonoBehaviour
{
    public MeshFilter originalModel;

    private void Start()
    {
        Mesh meshOriginal = originalModel.mesh;

        Vector3[] vertices = meshOriginal.vertices;
        int[] triangles = meshOriginal.triangles;

        Debug.LogWarning("Name: " + originalModel.gameObject.name);
        Debug.LogWarning("Vertices: " + vertices.Length);
        Debug.LogWarning("Indices de triangulos: " + triangles.Length);
        Debug.LogWarning("Numero de triangulos: " + triangles.Length);
    }
}