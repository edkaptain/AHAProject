using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class DrawRectable : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;
    private Mesh mesh;
    private MeshCollider meshCollider;

    private readonly List<Vector3> vertices = new();
    private readonly List<int> triangles = new();
    private readonly List<Vector2> uvs = new();

    private void Start()
    {
        if (playerCamera == null) playerCamera = Camera.main;

        meshCollider = GetComponent<MeshCollider>();

        CreateRectangle();

    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0)) {

            DetectTriangle();
        }
    }

    private void CreateRectangle()
    {
        mesh = new Mesh();
        mesh.name = "Rectangle Mesh";
        mesh.MarkDynamic();

        // Create vertices poitns for the rectanlge
        vertices.Add(new Vector3(-1, 0, -1)); // 0
        vertices.Add(new Vector3(1, 0, -1));  // 1
        vertices.Add(new Vector3(1, 0, 1));   // 2
        vertices.Add(new Vector3(-1, 0, 1));  // 3

        // Create the triangles
        triangles.AddRange(new int[]
        {
            0, 2, 1,
            0, 3, 2
        });

        // Coordenadas de la textura
        uvs.Add(new Vector2(0, 0));
        uvs.Add(new Vector2(1, 0));
        uvs.Add(new Vector2(1, 1));
        uvs.Add(new Vector2(0, 1));

        UpdateMesh();
    }

    private void DetectTriangle()
    {
        if( playerCamera == null) return;

        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);

        if(Physics.Raycast(ray, out RaycastHit hit))
        {

            if(hit.collider != meshCollider) return;

            DivideTriangle(hit);


        }
    }

    private void DivideTriangle(RaycastHit hit)
    {
        // Cada triángulo contiene tres índices
        int triangleStart = hit.triangleIndex * 3;

        int vertexA = triangles[triangleStart];
        int vertexB = triangles[triangleStart + 1];
        int vertexC = triangles[triangleStart + 2];

        // Convertir la posición mundial a la posición local del mesh
        Vector3 newVertex =
            transform.InverseTransformPoint(hit.point);

        // El nuevo vértice estará al final de la lista
        int newVertexIndex = vertices.Count;

        vertices.Add(newVertex);

        // Calcular la UV del nuevo vértice
        Vector3 barycentric = hit.barycentricCoordinate;

        Vector2 newUV =
            uvs[vertexA] * barycentric.x +
            uvs[vertexB] * barycentric.y +
            uvs[vertexC] * barycentric.z;

        uvs.Add(newUV);

        // Eliminar el triángulo original
        triangles.RemoveRange(triangleStart, 3);

        // Crear tres triángulos nuevos
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
        mesh.Clear();

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);

        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().mesh = mesh;

        // Actualizar el collider con la nueva geometría
        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = mesh;
    }
}