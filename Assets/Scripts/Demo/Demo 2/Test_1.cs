using System.Collections.Generic;
using UnityEngine;

[RequireComponent(
    typeof(MeshFilter),
    typeof(MeshRenderer),
    typeof(MeshCollider))]
public class EditableModel : MonoBehaviour
{
    [Header("Reference model")]
    [SerializeField] private MeshFilter originalMeshFilter;

    [Header("Mouse")]
    [SerializeField] private Camera playerCamera;

    private Mesh editableMesh;
    private MeshCollider meshCollider;

    private readonly List<Vector3> vertices = new();
    private readonly List<int> triangles = new();
    private readonly List<Vector2> uvs = new();


    private void Start()
    {
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        meshCollider = GetComponent<MeshCollider>();

        CreateEditableCopy();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            DetectTriangle();
        }
    }

    private void CreateEditableCopy()
    {
        Mesh original = originalMeshFilter.sharedMesh;

        // Copiar la información del modelo original
        vertices.AddRange(original.vertices);
        triangles.AddRange(original.triangles);

        if (original.uv.Length == original.vertexCount)
        {
            uvs.AddRange(original.uv);
        }
        else
        {
            // Crear UV vacías si el modelo no tiene UV
            for (int i = 0; i < original.vertexCount; i++)
            {
                uvs.Add(Vector2.zero);
            }
        }

        // Crear nuestro propio mesh editable
        editableMesh = new Mesh();
        editableMesh.name = original.name + "_Editable";
        editableMesh.indexFormat = original.indexFormat;
        editableMesh.MarkDynamic();

        UpdateMesh();

        // Copiar el material del modelo original
        MeshRenderer originalRenderer =
            originalMeshFilter.GetComponent<MeshRenderer>();

        if (originalRenderer != null)
        {
            GetComponent<MeshRenderer>().sharedMaterial =
                originalRenderer.sharedMaterial;
        }
    }

    private void DetectTriangle()
    {
        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            // Comprobar que tocamos este modelo
            if (hit.collider == meshCollider)
            {
                Debug.Log("The model was touched");
                DivideTriangle(hit);
            }
        }
    }

    private void DivideTriangle(RaycastHit hit)
    {
        int triangleStart = hit.triangleIndex * 3;

        // Validación de seguridad
        if (triangleStart + 2 >= triangles.Count)
        {
            return;
        }

        int vertexA = triangles[triangleStart];
        int vertexB = triangles[triangleStart + 1];
        int vertexC = triangles[triangleStart + 2];

        // Convertir el punto tocado de coordenadas mundiales a locales
        Vector3 localHitPoint =
            transform.InverseTransformPoint(hit.point);

        int newVertexIndex = vertices.Count;

        // Agregar el nuevo vértice
        vertices.Add(localHitPoint);

        // Calcular la UV correspondiente al nuevo punto
        Vector3 barycentric = hit.barycentricCoordinate;

        Vector2 newUV =
            uvs[vertexA] * barycentric.x +
            uvs[vertexB] * barycentric.y +
            uvs[vertexC] * barycentric.z;

        uvs.Add(newUV);

        // Eliminar el triángulo tocado
        triangles.RemoveRange(triangleStart, 3);

        // Reemplazarlo con tres triángulos
        triangles.AddRange(new int[]
        {
            vertexA, vertexB, newVertexIndex,
            vertexB, vertexC, newVertexIndex,
            vertexC, vertexA, newVertexIndex
        });

        UpdateMesh();
    }

    private void UpdateMesh()
    {
        editableMesh.Clear();

        editableMesh.SetVertices(vertices);
        editableMesh.SetTriangles(triangles, 0);
        editableMesh.SetUVs(0, uvs);

        editableMesh.RecalculateNormals();
        editableMesh.RecalculateTangents();
        editableMesh.RecalculateBounds();

        GetComponent<MeshFilter>().mesh = editableMesh;

        // Actualizar el collider para reconocer los nuevos triángulos
        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = editableMesh;
    }
}