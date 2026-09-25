using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif


[RequireComponent(
    typeof(MeshFilter),
    typeof(MeshRenderer),
    typeof(MeshCollider))]
public class SurgicalMouseScalpel : MonoBehaviour
{
    // =========================================================
    // REFERENCES
    // =========================================================

    [Header("References")]
    [SerializeField] private Camera playerCamera;


    // =========================================================
    // INCISION
    // =========================================================

    [Header("Incision")]

    [Tooltip("Ancho de apertura de la incisión.")]
    [SerializeField]
    private float openingWidth = 0.003f;

    [Tooltip("Profundidad de la incisión.")]
    [SerializeField]
    private float cutDepth = 0.008f;

    [Tooltip("Distancia mínima que debe recorrer el bisturí.")]
    [SerializeField]
    private float minimumStrokeDistance = 0.003f;


    // =========================================================
    // SAMPLING
    // =========================================================

    [Header("Mouse sampling")]

    [Tooltip("Cada cuántos píxeles comprobamos otro punto del bisturí.")]
    [SerializeField]
    private float pixelsPerSample = 6f;

    [Tooltip("Máximo de muestras por frame.")]
    [SerializeField]
    private int maximumSamplesPerFrame = 20;


    // =========================================================
    // MESH
    // =========================================================

    private MeshFilter meshFilter;
    private MeshCollider meshCollider;

    private Mesh editableMesh;

    private readonly List<Vector3> vertices = new();
    private readonly List<int> triangles = new();
    private readonly List<Vector2> uvs = new();


    // =========================================================
    // MOUSE STATE
    // =========================================================

    private bool previousButton;
    private bool cutting;

    private Vector2 previousMousePosition;

    private Vector3 previousWorldPoint;

    private bool hasPreviousWorldPoint;


    // =========================================================
    // INTERNAL CUT VERTEX
    // =========================================================

    private struct CutVertex
    {
        public Vector3 position;
        public Vector2 uv;

        // true si este punto pertenece
        // exactamente a la línea del corte
        public bool onCut;


        public CutVertex(
            Vector3 position,
            Vector2 uv,
            bool onCut)
        {
            this.position = position;
            this.uv = uv;
            this.onCut = onCut;
        }
    }


    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();

        meshCollider = GetComponent<MeshCollider>();


        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }


        CreateEditableMesh();
    }


    private void Update()
    {
        if (!ReadMouse(
                out Vector2 mousePosition,
                out bool pressed))
        {
            return;
        }


        bool down =
            pressed && !previousButton;

        bool up =
            !pressed && previousButton;


        // -----------------------------------------------------
        // START
        // -----------------------------------------------------

        if (down)
        {
            BeginStroke(mousePosition);
        }


        // -----------------------------------------------------
        // CONTINUE
        // -----------------------------------------------------

        if (pressed && cutting)
        {
            ContinueStroke(mousePosition);
        }


        // -----------------------------------------------------
        // END
        // -----------------------------------------------------

        if (up)
        {
            EndStroke();
        }


        previousButton = pressed;
    }


    // =========================================================
    // CREATE EDITABLE MESH
    // =========================================================

    private void CreateEditableMesh()
    {
        Mesh original =
            meshFilter.sharedMesh;


        if (original == null)
        {
            Debug.LogError(
                "The MeshFilter does not contain a mesh."
            );

            enabled = false;

            return;
        }


        // Copiamos el mesh para NO modificar
        // el asset original.
        editableMesh =
            Instantiate(original);


        editableMesh.name =
            original.name +
            "_SurgicalIncision";


        editableMesh.indexFormat =
            IndexFormat.UInt32;


        editableMesh.MarkDynamic();


        // -----------------------------------------------------
        // COPY DATA
        // -----------------------------------------------------

        vertices.Clear();

        vertices.AddRange(
            editableMesh.vertices
        );


        triangles.Clear();

        triangles.AddRange(
            editableMesh.triangles
        );


        uvs.Clear();


        if (editableMesh.uv != null &&
            editableMesh.uv.Length ==
            editableMesh.vertexCount)
        {
            uvs.AddRange(
                editableMesh.uv
            );
        }
        else
        {
            for (
                int i = 0;
                i < editableMesh.vertexCount;
                i++)
            {
                uvs.Add(Vector2.zero);
            }
        }


        meshFilter.sharedMesh =
            editableMesh;


        RefreshCollider();
    }


    // =========================================================
    // START STROKE
    // =========================================================

    private void BeginStroke(
        Vector2 mousePosition)
    {
        if (!TryRaycast(
                mousePosition,
                out RaycastHit hit))
        {
            cutting = false;

            return;
        }


        cutting = true;


        previousMousePosition =
            mousePosition;


        previousWorldPoint =
            hit.point;


        hasPreviousWorldPoint =
            true;
    }


    // =========================================================
    // CONTINUE STROKE
    // =========================================================

    private void ContinueStroke(
        Vector2 mousePosition)
    {
        float mouseTravel =
            Vector2.Distance(
                previousMousePosition,
                mousePosition
            );


        int sampleCount =
            Mathf.CeilToInt(
                mouseTravel /
                Mathf.Max(
                    pixelsPerSample,
                    1f
                )
            );


        sampleCount =
            Mathf.Clamp(
                sampleCount,
                1,
                maximumSamplesPerFrame
            );


        Vector3 lastPoint =
            previousWorldPoint;


        bool foundPoint = false;


        // -----------------------------------------------------
        // Esto evita saltarnos triángulos si el mouse
        // se mueve rápidamente.
        // -----------------------------------------------------

        for (
            int i = 1;
            i <= sampleCount;
            i++)
        {
            float t =
                (float)i /
                sampleCount;


            Vector2 sampleMouse =
                Vector2.Lerp(
                    previousMousePosition,
                    mousePosition,
                    t
                );


            if (!TryRaycast(
                    sampleMouse,
                    out RaycastHit hit))
            {
                continue;
            }


            if (!hasPreviousWorldPoint)
            {
                lastPoint =
                    hit.point;

                hasPreviousWorldPoint =
                    true;

                continue;
            }


            float distance =
                Vector3.Distance(
                    lastPoint,
                    hit.point
                );


            if (distance <
                minimumStrokeDistance)
            {
                continue;
            }


            // =================================================
            // AQUÍ CORTAMOS EL TRIÁNGULO
            // =================================================

            CutTriangle(
                hit,
                lastPoint,
                hit.point
            );


            lastPoint =
                hit.point;


            foundPoint = true;
        }


        if (foundPoint)
        {
            previousWorldPoint =
                lastPoint;
        }


        previousMousePosition =
            mousePosition;
    }


    // =========================================================
    // END STROKE
    // =========================================================

    private void EndStroke()
    {
        cutting = false;

        hasPreviousWorldPoint = false;
    }


    // =========================================================
    // CUT TRIANGLE
    // =========================================================

    private void CutTriangle(
        RaycastHit hit,
        Vector3 previousPointWorld,
        Vector3 currentPointWorld)
    {
        // -----------------------------------------------------
        // El triangleIndex apunta al TRIÁNGULO,
        // pero nuestro array contiene:
        //
        // triangle 0 -> indices 0,1,2
        // triangle 1 -> indices 3,4,5
        // triangle 2 -> indices 6,7,8
        //
        // -----------------------------------------------------

        int triangleStart =
            hit.triangleIndex * 3;


        if (triangleStart < 0 ||
            triangleStart + 2 >=
            triangles.Count)
        {
            return;
        }


        int i0 =
            triangles[triangleStart];

        int i1 =
            triangles[triangleStart + 1];

        int i2 =
            triangles[triangleStart + 2];


        Vector3 v0 =
            vertices[i0];

        Vector3 v1 =
            vertices[i1];

        Vector3 v2 =
            vertices[i2];


        // -----------------------------------------------------
        // TRIANGLE NORMAL
        // -----------------------------------------------------

        Vector3 triangleNormal =
            Vector3.Cross(
                v1 - v0,
                v2 - v0
            );


        if (triangleNormal.sqrMagnitude <
            0.0000001f)
        {
            return;
        }


        triangleNormal.Normalize();


        // -----------------------------------------------------
        // WORLD -> LOCAL
        // -----------------------------------------------------

        Vector3 cutPoint =
            transform.InverseTransformPoint(
                hit.point
            );


        Vector3 previousLocal =
            transform.InverseTransformPoint(
                previousPointWorld
            );


        Vector3 currentLocal =
            transform.InverseTransformPoint(
                currentPointWorld
            );


        Vector3 cutDirection =
            currentLocal -
            previousLocal;


        // -----------------------------------------------------
        // Proyectamos la dirección sobre el plano
        // del triángulo.
        // -----------------------------------------------------

        cutDirection =
            Vector3.ProjectOnPlane(
                cutDirection,
                triangleNormal
            );


        if (cutDirection.sqrMagnitude <
            0.0000001f)
        {
            return;
        }


        cutDirection.Normalize();


        // -----------------------------------------------------
        // Dirección perpendicular al bisturí.
        //
        //          side
        //           ↑
        //
        // ----------→ cutDirection
        //
        // -----------------------------------------------------

        Vector3 sideDirection =
            Vector3.Cross(
                triangleNormal,
                cutDirection
            ).normalized;


        // =====================================================
        // ORIGINAL TRIANGLE
        // =====================================================

        List<CutVertex> originalTriangle =
            new List<CutVertex>(3)
            {
                new CutVertex(
                    v0,
                    uvs[i0],
                    false
                ),

                new CutVertex(
                    v1,
                    uvs[i1],
                    false
                ),

                new CutVertex(
                    v2,
                    uvs[i2],
                    false
                )
            };


        // =====================================================
        // ENCONTRAR LOS DOS PUNTOS DONDE EL BISTURÍ
        // CRUZA LAS ARISTAS DEL TRIÁNGULO
        // =====================================================

        List<CutVertex> intersections =
            FindIntersections(
                originalTriangle,
                cutPoint,
                sideDirection
            );


        if (intersections.Count != 2)
        {
            return;
        }


        // Ordenamos de acuerdo con la dirección
        // del corte.
        intersections.Sort(
            (a, b) =>
            {
                float da =
                    Vector3.Dot(
                        a.position,
                        cutDirection
                    );

                float db =
                    Vector3.Dot(
                        b.position,
                        cutDirection
                    );

                return da.CompareTo(db);
            }
        );


        // =====================================================
        // DIVIDIR TRIÁNGULO EN DOS POLÍGONOS
        // =====================================================

        List<CutVertex> positiveSide =
            ClipTriangle(
                originalTriangle,
                cutPoint,
                sideDirection,
                true
            );


        List<CutVertex> negativeSide =
            ClipTriangle(
                originalTriangle,
                cutPoint,
                sideDirection,
                false
            );


        if (positiveSide.Count < 3 ||
            negativeSide.Count < 3)
        {
            return;
        }


        // =====================================================
        // ELIMINAR SOLO EL TRIÁNGULO ORIGINAL
        // =====================================================

        triangles.RemoveRange(
            triangleStart,
            3
        );


        float halfOpening =
            openingWidth * 0.5f;


        // =====================================================
        // CREAR LADO IZQUIERDO
        // =====================================================

        AddSurfacePolygon(
            positiveSide,
            sideDirection,
            halfOpening
        );


        // =====================================================
        // CREAR LADO DERECHO
        // =====================================================

        AddSurfacePolygon(
            negativeSide,
            -sideDirection,
            halfOpening
        );


        // =====================================================
        // CREAR INTERIOR DE LA INCISIÓN
        //
        //           surface
        //
        //        \        /
        //         \      /
        //          \    /
        //           \  /
        //            \/
        //           depth
        //
        // =====================================================

        CreateIncisionWalls(
            intersections[0],
            intersections[1],
            triangleNormal,
            sideDirection,
            halfOpening
        );


        // =====================================================
        // APPLY
        // =====================================================

        ApplyMesh();

        RefreshCollider();
    }


    // =========================================================
    // FIND INTERSECTIONS
    // =========================================================

    private List<CutVertex> FindIntersections(
        List<CutVertex> triangle,
        Vector3 planePoint,
        Vector3 sideDirection)
    {
        List<CutVertex> result =
            new List<CutVertex>();


        const float epsilon =
            0.00001f;


        for (
            int i = 0;
            i < triangle.Count;
            i++)
        {
            CutVertex a =
                triangle[i];


            CutVertex b =
                triangle[
                    (i + 1) %
                    triangle.Count
                ];


            float distanceA =
                Vector3.Dot(
                    a.position -
                    planePoint,
                    sideDirection
                );


            float distanceB =
                Vector3.Dot(
                    b.position -
                    planePoint,
                    sideDirection
                );


            // -------------------------------------------------
            // Vertex exactamente sobre la línea
            // -------------------------------------------------

            if (Mathf.Abs(distanceA) <
                epsilon)
            {
                AddUniqueIntersection(
                    result,
                    new CutVertex(
                        a.position,
                        a.uv,
                        true
                    )
                );
            }


            // -------------------------------------------------
            // La arista cruza de un lado al otro
            // -------------------------------------------------

            if ((distanceA > epsilon &&
                 distanceB < -epsilon)
                ||
                (distanceA < -epsilon &&
                 distanceB > epsilon))
            {
                float t =
                    distanceA /
                    (distanceA -
                     distanceB);


                Vector3 position =
                    Vector3.Lerp(
                        a.position,
                        b.position,
                        t
                    );


                Vector2 uv =
                    Vector2.Lerp(
                        a.uv,
                        b.uv,
                        t
                    );


                AddUniqueIntersection(
                    result,
                    new CutVertex(
                        position,
                        uv,
                        true
                    )
                );
            }
        }


        return result;
    }


    // =========================================================
    // ADD UNIQUE INTERSECTION
    // =========================================================

    private void AddUniqueIntersection(
        List<CutVertex> list,
        CutVertex vertex)
    {
        const float epsilonSquared =
            0.00000001f;


        foreach (
            CutVertex existing
            in list)
        {
            if (
                (existing.position -
                 vertex.position)
                .sqrMagnitude
                <
                epsilonSquared)
            {
                return;
            }
        }


        list.Add(vertex);
    }


    // =========================================================
    // CLIP TRIANGLE
    // =========================================================

    private List<CutVertex> ClipTriangle(
        List<CutVertex> input,
        Vector3 planePoint,
        Vector3 sideDirection,
        bool keepPositive)
    {
        List<CutVertex> output =
            new List<CutVertex>();


        const float epsilon =
            0.00001f;


        CutVertex previous =
            input[
                input.Count - 1
            ];


        float previousDistance =
            Vector3.Dot(
                previous.position -
                planePoint,
                sideDirection
            );


        bool previousInside =
            keepPositive
                ? previousDistance >=
                  -epsilon
                : previousDistance <=
                  epsilon;


        for (
            int i = 0;
            i < input.Count;
            i++)
        {
            CutVertex current =
                input[i];


            float currentDistance =
                Vector3.Dot(
                    current.position -
                    planePoint,
                    sideDirection
                );


            bool currentInside =
                keepPositive
                    ? currentDistance >=
                      -epsilon
                    : currentDistance <=
                      epsilon;


            // ---------------------------------------------
            // ENTERING
            // ---------------------------------------------

            if (!previousInside &&
                currentInside)
            {
                CutVertex intersection =
                    IntersectEdge(
                        previous,
                        current,
                        previousDistance,
                        currentDistance
                    );


                output.Add(
                    intersection
                );
            }


            // ---------------------------------------------
            // CURRENT INSIDE
            // ---------------------------------------------

            if (currentInside)
            {
                CutVertex copy =
                    current;


                if (Mathf.Abs(
                        currentDistance)
                    < epsilon)
                {
                    copy.onCut =
                        true;
                }


                output.Add(copy);
            }


            // ---------------------------------------------
            // LEAVING
            // ---------------------------------------------

            if (previousInside &&
                !currentInside)
            {
                CutVertex intersection =
                    IntersectEdge(
                        previous,
                        current,
                        previousDistance,
                        currentDistance
                    );


                output.Add(
                    intersection
                );
            }


            previous =
                current;


            previousDistance =
                currentDistance;


            previousInside =
                currentInside;
        }


        return output;
    }


    // =========================================================
    // INTERSECT EDGE
    // =========================================================

    private CutVertex IntersectEdge(
        CutVertex a,
        CutVertex b,
        float distanceA,
        float distanceB)
    {
        float denominator =
            distanceA -
            distanceB;


        float t = 0.5f;


        if (Mathf.Abs(denominator) >
            0.000001f)
        {
            t =
                distanceA /
                denominator;
        }


        t =
            Mathf.Clamp01(t);


        return new CutVertex(
            Vector3.Lerp(
                a.position,
                b.position,
                t
            ),

            Vector2.Lerp(
                a.uv,
                b.uv,
                t
            ),

            true
        );
    }


    // =========================================================
    // ADD SURFACE POLYGON
    // =========================================================

    private void AddSurfacePolygon(
        List<CutVertex> polygon,
        Vector3 openingDirection,
        float halfOpening)
    {
        int firstIndex =
            vertices.Count;


        // -----------------------------------------------------
        // ADD VERTICES
        // -----------------------------------------------------

        foreach (
            CutVertex vertex
            in polygon)
        {
            Vector3 position =
                vertex.position;


            // SOLO desplazamos los puntos que están
            // exactamente sobre la incisión.
            if (vertex.onCut)
            {
                position +=
                    openingDirection *
                    halfOpening;
            }


            vertices.Add(
                position
            );


            uvs.Add(
                vertex.uv
            );
        }


        // -----------------------------------------------------
        // TRIANGULATE POLYGON
        //
        // Triangle = 3 vertices
        // Quad     = 4 vertices
        //
        // Fan:
        //
        // 0-1-2
        // 0-2-3
        // -----------------------------------------------------

        for (
            int i = 1;
            i < polygon.Count - 1;
            i++)
        {
            triangles.Add(
                firstIndex
            );

            triangles.Add(
                firstIndex + i
            );

            triangles.Add(
                firstIndex + i + 1
            );
        }
    }


    // =========================================================
    // CREATE INCISION WALLS
    // =========================================================

    private void CreateIncisionWalls(
        CutVertex start,
        CutVertex end,
        Vector3 surfaceNormal,
        Vector3 sideDirection,
        float halfOpening)
    {
        // -----------------------------------------------------
        // UPPER EDGES
        // -----------------------------------------------------

        Vector3 leftStart =
            start.position +
            sideDirection *
            halfOpening;


        Vector3 leftEnd =
            end.position +
            sideDirection *
            halfOpening;


        Vector3 rightStart =
            start.position -
            sideDirection *
            halfOpening;


        Vector3 rightEnd =
            end.position -
            sideDirection *
            halfOpening;


        // -----------------------------------------------------
        // BOTTOM
        //
        // El fondo permanece en la línea central,
        // pero hacia dentro del tejido.
        // -----------------------------------------------------

        Vector3 bottomStart =
            start.position -
            surfaceNormal *
            cutDepth;


        Vector3 bottomEnd =
            end.position -
            surfaceNormal *
            cutDepth;


        // =====================================================
        // LEFT WALL
        // =====================================================

        int leftBase =
            vertices.Count;


        vertices.Add(leftStart);
        vertices.Add(leftEnd);
        vertices.Add(bottomStart);
        vertices.Add(bottomEnd);


        uvs.Add(start.uv);
        uvs.Add(end.uv);
        uvs.Add(start.uv);
        uvs.Add(end.uv);


        /*
             0 -------- 1
              \        |
               \       |
                2 ---- 3
        */


        triangles.Add(leftBase + 0);
        triangles.Add(leftBase + 2);
        triangles.Add(leftBase + 1);


        triangles.Add(leftBase + 1);
        triangles.Add(leftBase + 2);
        triangles.Add(leftBase + 3);


        // =====================================================
        // RIGHT WALL
        // =====================================================

        int rightBase =
            vertices.Count;


        vertices.Add(rightStart);
        vertices.Add(rightEnd);
        vertices.Add(bottomStart);
        vertices.Add(bottomEnd);


        uvs.Add(start.uv);
        uvs.Add(end.uv);
        uvs.Add(start.uv);
        uvs.Add(end.uv);


        triangles.Add(rightBase + 0);
        triangles.Add(rightBase + 1);
        triangles.Add(rightBase + 2);


        triangles.Add(rightBase + 1);
        triangles.Add(rightBase + 3);
        triangles.Add(rightBase + 2);
    }


    // =========================================================
    // RAYCAST
    // =========================================================

    private bool TryRaycast(
        Vector2 screenPosition,
        out RaycastHit hit)
    {
        hit = default;


        if (playerCamera == null ||
            meshCollider == null)
        {
            return false;
        }


        Ray ray =
            playerCamera.ScreenPointToRay(
                screenPosition
            );


        if (!Physics.Raycast(
                ray,
                out hit,
                Mathf.Infinity,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore))
        {
            return false;
        }


        // Solamente podemos cortar
        // nuestro propio MeshCollider.
        return hit.collider ==
               meshCollider;
    }


    // =========================================================
    // APPLY MESH
    // =========================================================

    private void ApplyMesh()
    {
        editableMesh.Clear();


        editableMesh.SetVertices(
            vertices
        );


        editableMesh.SetUVs(
            0,
            uvs
        );


        editableMesh.SetTriangles(
            triangles,
            0
        );


        editableMesh
            .RecalculateNormals();


        editableMesh
            .RecalculateTangents();


        editableMesh
            .RecalculateBounds();


        meshFilter.sharedMesh =
            editableMesh;
    }


    // =========================================================
    // COLLIDER
    // =========================================================

    private void RefreshCollider()
    {
        meshCollider.sharedMesh =
            null;


        meshCollider.sharedMesh =
            editableMesh;
    }


    // =========================================================
    // INPUT
    // =========================================================

    private static bool ReadMouse(
        out Vector2 position,
        out bool pressed)
    {
#if ENABLE_INPUT_SYSTEM

        if (Mouse.current == null)
        {
            position =
                Vector2.zero;

            pressed =
                false;

            return false;
        }


        position =
            Mouse.current
                .position
                .ReadValue();


        pressed =
            Mouse.current
                .leftButton
                .isPressed;


        return true;

#else

        position =
            Input.mousePosition;


        pressed =
            Input.GetMouseButton(0);


        return Input.mousePresent;

#endif
    }
}