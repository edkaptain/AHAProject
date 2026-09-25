using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(
    typeof(MeshFilter),
    typeof(MeshRenderer),
    typeof(MeshCollider))]
public class MouseIncision : MonoBehaviour
{
    [Header("Reference model")]
    [SerializeField] private MeshFilter originalMeshFilter;

    [Header("Mouse")]
    [SerializeField] private Camera playerCamera;

    [Header("Cut settings")]

    [Tooltip("Distancia que debe avanzar el mouse para crear otro corte.")]
    [SerializeField] private float distanceBetweenPoints = 0.02f;

    [Tooltip("Ancho de los triángulos eliminados.")]
    [SerializeField] private float cutWidth = 0.015f;

    [Tooltip("Área alrededor del corte que será deformada.")]
    [SerializeField] private float deformationRadius = 0.08f;

    [Tooltip("Cuánto se separan los bordes.")]
    [SerializeField] private float openingAmount = 0.02f;

    [Tooltip("Cuánto se hunden los bordes.")]
    [SerializeField] private float cutDepth = 0.01f;

    private Mesh editableMesh;
    private MeshCollider meshCollider;

    private readonly List<Vector3> vertices = new();
    private readonly List<int> triangles = new();
    private readonly List<Vector2> uvs = new();

    // Evita mover repetidamente el mismo vértice durante un recorrido.
    private readonly HashSet<int> movedVertices = new();

    private Vector3 lastMousePoint;
    private bool hasLastMousePoint;

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
        // Comenzar el corte
        if (Input.GetMouseButtonDown(0))
        {
            movedVertices.Clear();

            if (TryGetSurfacePoint(out RaycastHit hit))
            {
                lastMousePoint = hit.point;
                hasLastMousePoint = true;
            }
        }

        // Continuar el corte
        if (Input.GetMouseButton(0) && hasLastMousePoint)
        {
            FollowMouse();
        }

        // Terminar el corte
        if (Input.GetMouseButtonUp(0))
        {
            hasLastMousePoint = false;

            // Actualizar el collider al terminar.
            RefreshCollider();
        }
    }

    private void CreateEditableCopy()
    {
        if (originalMeshFilter == null)
        {
            Debug.LogError("Assign an original MeshFilter.");
            return;
        }

        Mesh original = originalMeshFilter.sharedMesh;

        if (original == null)
        {
            Debug.LogError("The original MeshFilter has no mesh.");
            return;
        }

        vertices.AddRange(original.vertices);
        triangles.AddRange(original.triangles);

        // Copiar UV
        if (original.uv.Length == original.vertexCount)
        {
            uvs.AddRange(original.uv);
        }
        else
        {
            for (int i = 0; i < original.vertexCount; i++)
            {
                uvs.Add(Vector2.zero);
            }
        }

        editableMesh = new Mesh
        {
            name = original.name + "_Incision",
            indexFormat = IndexFormat.UInt32
        };

        editableMesh.MarkDynamic();

        ApplyMesh();
        RefreshCollider();

        // Copiar material
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
        if (!TryGetSurfacePoint(out RaycastHit hit))
        {
            return;
        }

        float travelledDistance =
            Vector3.Distance(lastMousePoint, hit.point);

        if (travelledDistance < distanceBetweenPoints)
        {
            return;
        }

        CreateCutSegment(
            lastMousePoint,
            hit.point,
            hit.normal
        );

        lastMousePoint = hit.point;
    }

    private bool TryGetSurfacePoint(out RaycastHit hit)
    {
        hit = default;
        if (playerCamera == null || editableMesh == null)
        {
            Debug.LogWarning($"Cam null: {playerCamera == null}, Mesh null: {editableMesh == null}");
            return false;
        }

        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out hit))
        {
            Debug.LogWarning("Raycast no golpeó nada.");
            return false;
        }

        if (hit.collider != meshCollider)
            Debug.LogWarning($"Golpeó otro collider: {hit.collider.name}");

        return hit.collider == meshCollider;
    }

    private void CreateCutSegment(
        Vector3 worldPointA,
        Vector3 worldPointB,
        Vector3 worldNormal)
    {
        // Convertir el recorrido del mouse al espacio local del mesh
        Vector3 pointA =
            transform.InverseTransformPoint(worldPointA);

        Vector3 pointB =
            transform.InverseTransformPoint(worldPointB);

        Vector3 surfaceNormal =
            transform.InverseTransformDirection(worldNormal).normalized;

        Vector3 cutDirection = pointB - pointA;

        if (cutDirection.sqrMagnitude < 0.000001f)
        {
            return;
        }

        cutDirection.Normalize();

        // Dirección hacia los lados de la incisión
        Vector3 sideDirection =
            Vector3.Cross(surfaceNormal, cutDirection).normalized;

        // Quitar los triángulos que están sobre el recorrido
        RemoveTrianglesNearSegment(pointA, pointB);

        // Separar y hundir los vértices cercanos
        DeformVerticesNearSegment(
            pointA,
            pointB,
            surfaceNormal,
            sideDirection
        );

        ApplyMesh();
    }

    private void RemoveTrianglesNearSegment(
    Vector3 pointA,
    Vector3 pointB)
    {
        const int minimumRemainingTriangles = 10;

        for (int i = triangles.Count - 3; i >= 0; i -= 3)
        {
            // No permitir que la malla se quede sin triángulos
            if (triangles.Count / 3 <= minimumRemainingTriangles)
            {
                break;
            }

            int indexA = triangles[i];
            int indexB = triangles[i + 1];
            int indexC = triangles[i + 2];

            Vector3 triangleCenter =
                (vertices[indexA] +
                 vertices[indexB] +
                 vertices[indexC]) / 3f;

            float distance = DistancePointToSegment(
                triangleCenter,
                pointA,
                pointB
            );

            if (distance <= cutWidth)
            {
                triangles.RemoveRange(i, 3);
            }
        }
    }

    private void DeformVerticesNearSegment(
        Vector3 pointA,
        Vector3 pointB,
        Vector3 surfaceNormal,
        Vector3 sideDirection)
    {
        for (int i = 0; i < vertices.Count; i++)
        {
            // No mover varias veces el mismo vértice
            // durante un solo recorrido.
            if (movedVertices.Contains(i))
            {
                continue;
            }

            Vector3 closestPoint = ClosestPointOnSegment(
                vertices[i],
                pointA,
                pointB
            );

            Vector3 difference = vertices[i] - closestPoint;
            float distance = difference.magnitude;

            if (distance > deformationRadius)
            {
                continue;
            }

            // 1 cerca del corte y 0 lejos del corte
            float influence =
                1f - distance / deformationRadius;

            float vertexSide =
                Vector3.Dot(difference, sideDirection);

            float sideSign = vertexSide >= 0f ? 1f : -1f;

            // Abrir hacia los lados
            Vector3 openingMovement =
                sideDirection *
                sideSign *
                openingAmount *
                influence;

            // Hundir ligeramente la superficie
            Vector3 depthMovement =
                -surfaceNormal *
                cutDepth *
                influence;

            vertices[i] += openingMovement + depthMovement;

            movedVertices.Add(i);
        }
    }

    private Vector3 ClosestPointOnSegment(
        Vector3 point,
        Vector3 segmentA,
        Vector3 segmentB)
    {
        Vector3 segment = segmentB - segmentA;

        float lengthSquared = segment.sqrMagnitude;

        if (lengthSquared == 0f)
        {
            return segmentA;
        }

        float t = Vector3.Dot(
            point - segmentA,
            segment
        ) / lengthSquared;

        t = Mathf.Clamp01(t);

        return segmentA + segment * t;
    }

    private float DistancePointToSegment(
        Vector3 point,
        Vector3 segmentA,
        Vector3 segmentB)
    {
        Vector3 closestPoint = ClosestPointOnSegment(
            point,
            segmentA,
            segmentB
        );

        return Vector3.Distance(point, closestPoint);
    }

    private void ApplyMesh()
    {
        editableMesh.Clear();

        editableMesh.SetVertices(vertices);
        editableMesh.SetTriangles(triangles, 0);
        editableMesh.SetUVs(0, uvs);

        editableMesh.RecalculateNormals();
        editableMesh.RecalculateTangents();
        editableMesh.RecalculateBounds();

        GetComponent<MeshFilter>().mesh = editableMesh;
    }

    private void RefreshCollider()
    {
        if (editableMesh == null)
        {
            return;
        }

        if (!HasValidTriangle())
        {
            Debug.LogWarning(
                "The collider was not updated because " +
                "the mesh has no valid triangles."
            );

            return;
        }

        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = editableMesh;
    }

    private bool HasValidTriangle()
    {
        for (int i = 0; i <= triangles.Count - 3; i += 3)
        {
            Vector3 vertexA = vertices[triangles[i]];
            Vector3 vertexB = vertices[triangles[i + 1]];
            Vector3 vertexC = vertices[triangles[i + 2]];

            Vector3 sideAB = vertexB - vertexA;
            Vector3 sideAC = vertexC - vertexA;

            // Si el área no es cero, el triángulo es válido
            if (Vector3.Cross(sideAB, sideAC).sqrMagnitude > 0.00000001f)
            {
                return true;
            }
        }

        return false;
    }
}