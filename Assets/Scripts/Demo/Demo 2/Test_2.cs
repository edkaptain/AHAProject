using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(
    typeof(MeshFilter),
    typeof(MeshRenderer),
    typeof(MeshCollider))]
public class EditableModel2 : MonoBehaviour
{
    [Header("Reference model")]
    [SerializeField] private MeshFilter originalMeshFilter;

    [Header("Mouse")]
    [SerializeField] private Camera playerCamera;

    [Header("Subdivision")]
    [SerializeField] private float distanceBetweenPoints = 0.05f;

    private Mesh editableMesh;
    private MeshCollider meshCollider;

    private readonly List<Vector3> vertices = new();
    private readonly List<int> triangles = new();
    private readonly List<Vector2> uvs = new();

    private Vector3 lastPoint;
    private bool hasLastPoint;

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
        // Comenzar un nuevo recorrido
        if (Input.GetMouseButtonDown(0))
        {
            hasLastPoint = false;
        }

        // Crear triángulos mientras se mantiene presionado
        if (Input.GetMouseButton(0))
        {
            FollowMouse();
        }

        // Terminar el recorrido
        if (Input.GetMouseButtonUp(0))
        {
            hasLastPoint = false;
        }
    }

    private void CreateEditableCopy()
    {
        if (originalMeshFilter == null)
        {
            Debug.LogError("No original MeshFilter was assigned.");
            return;
        }

        Mesh original = originalMeshFilter.sharedMesh;

        if (original == null)
        {
            Debug.LogError("The original MeshFilter has no mesh.");
            return;
        }

        // Copiar los datos del modelo original
        vertices.AddRange(original.vertices);
        triangles.AddRange(original.triangles);

        // Copiar UV
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

        // Crear el nuevo mesh editable
        editableMesh = new Mesh();
        editableMesh.name = original.name + "_Editable";

        // Permite tener más de 65,535 vértices
        editableMesh.indexFormat = IndexFormat.UInt32;
        editableMesh.MarkDynamic();

        UpdateMesh();

        // Copiar el material del objeto original
        MeshRenderer originalRenderer =
            originalMeshFilter.GetComponent<MeshRenderer>();

        if (originalRenderer != null)
        {
            GetComponent<MeshRenderer>().sharedMaterial =
                originalRenderer.sharedMaterial;
        }
    }

    private void FollowMouse()
    {
        if (playerCamera == null || editableMesh == null)
        {
            return;
        }

        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit))
        {
            return;
        }

        // Comprobar que el mouse está tocando este modelo
        if (hit.collider != meshCollider)
        {
            return;
        }

        // Evitar crear triángulos si el mouse casi no se ha movido
        if (hasLastPoint)
        {
            float distance = Vector3.Distance(
                lastPoint,
                hit.point
            );

            if (distance < distanceBetweenPoints)
            {
                return;
            }
        }

        DivideTriangle(hit);

        lastPoint = hit.point;
        hasLastPoint = true;
    }

    private void DivideTriangle(RaycastHit hit)
    {
        /*
         * Cada triángulo ocupa tres posiciones:
         *
         * Triángulo 0 = índices 0, 1, 2
         * Triángulo 1 = índices 3, 4, 5
         * Triángulo 2 = índices 6, 7, 8
         */
        int triangleStart = hit.triangleIndex * 3;

        if (triangleStart + 2 >= triangles.Count)
        {
            return;
        }

        // Obtener los vértices del triángulo tocado
        int vertexA = triangles[triangleStart];
        int vertexB = triangles[triangleStart + 1];
        int vertexC = triangles[triangleStart + 2];

        // Posición del mouse convertida al espacio local del mesh
        Vector3 localHitPoint =
            transform.InverseTransformPoint(hit.point);

        // El índice que tendrá el nuevo vértice
        int newVertexIndex = vertices.Count;

        // Agregar el nuevo vértice
        vertices.Add(localHitPoint);

        // Calcular la UV del nuevo vértice
        Vector3 barycentric = hit.barycentricCoordinate;

        Vector2 newUV =
            uvs[vertexA] * barycentric.x +
            uvs[vertexB] * barycentric.y +
            uvs[vertexC] * barycentric.z;

        uvs.Add(newUV);

        // Eliminar el triángulo tocado
        triangles.RemoveRange(triangleStart, 3);

        // Reemplazarlo con tres triángulos nuevos
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

        // Actualizar el collider para detectar los nuevos triángulos
        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = editableMesh;
    }
}