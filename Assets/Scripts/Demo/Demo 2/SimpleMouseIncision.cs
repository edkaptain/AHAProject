using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Copia un objeto 3D y construye una incisión con el arrastre del mouse.
/// Versión simple para MeshFilter estáticos (no SkinnedMeshRenderer).
/// </summary>
public class SimpleMouseIncision : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Objeto 3D con MeshFilter y MeshRenderer.")]
    public GameObject originalObject;
    public Camera cutCamera;
    public Material insideMaterial;

    [Header("Incisión - unidades locales del modelo")]
    [Min(0.0001f)] public float incisionWidth = 0.008f;
    [Min(0.0001f)] public float incisionDepth = 0.006f;

    [Header("Arrastre")]
    [Min(1f)] public float pixelsBetweenCuts = 4f;
    [Range(1, 50)] public int maximumCutsPerFrame = 20;
    [Min(1)] public int maximumTotalCuts = 800;

    [Header("Copia")]
    public bool hideOriginalObject = true;

    private GameObject copyObject;
    private Mesh visibleMesh;
    private Mesh collisionMesh;
    private MeshCollider meshCollider;
    private Material generatedInsideMaterial;

    private readonly List<Vector3> vertices = new List<Vector3>();
    private readonly List<Vector2> uvs = new List<Vector2>();
    private readonly List<int> surfaceTriangles = new List<int>();
    private readonly List<int> insideTriangles = new List<int>();

    private bool wasOriginalActive;
    private bool previousMousePressed;
    private bool dragging;
    private Vector2 lastMouseSample;
    private int totalCuts;

    private void Start()
    {
        CreateEditableCopy();
    }

    private void Update()
    {
        if (meshCollider == null)
            return;

        Vector2 mousePosition;
        bool mousePressed;

        if (!ReadMouse(out mousePosition, out mousePressed))
            return;

        bool mouseDown = mousePressed && !previousMousePressed;
        bool mouseUp = !mousePressed && previousMousePressed;
        previousMousePressed = mousePressed;

        if (mouseDown)
        {
            dragging = true;
            lastMouseSample = mousePosition;
            TryCut(mousePosition);
        }
        else if (mousePressed && dragging)
        {
            CutMousePath(lastMouseSample, mousePosition);
        }

        if (mouseUp)
            dragging = false;
    }

    private void CreateEditableCopy()
    {
        if (originalObject == null)
        {
            Debug.LogError("Asigna Original Object en SimpleMouseIncision.");
            enabled = false;
            return;
        }

        if (originalObject == gameObject)
        {
            Debug.LogError("Coloca SimpleMouseIncision en un GameObject vacío.");
            enabled = false;
            return;
        }

        MeshFilter originalFilter = originalObject.GetComponent<MeshFilter>();
        MeshRenderer originalRenderer = originalObject.GetComponent<MeshRenderer>();

        if (originalFilter == null || originalRenderer == null ||
            originalFilter.sharedMesh == null)
        {
            Debug.LogError(
                "Original Object necesita MeshFilter y MeshRenderer " +
                "en el mismo GameObject.");
            enabled = false;
            return;
        }

        if (!originalFilter.sharedMesh.isReadable)
        {
            Debug.LogError(
                "Activa Read/Write en los Import Settings del modelo.");
            enabled = false;
            return;
        }

        wasOriginalActive = originalObject.activeSelf;

        // PASO 1: replicar el objeto.
        copyObject = Instantiate(
            originalObject,
            originalObject.transform.position,
            originalObject.transform.rotation,
            originalObject.transform.parent);

        copyObject.name = originalObject.name + "_IncisionCopy";
        copyObject.SetActive(true);

        MeshFilter copyFilter = copyObject.GetComponent<MeshFilter>();
        MeshRenderer copyRenderer = copyObject.GetComponent<MeshRenderer>();

        // La copia usa otra instancia de Mesh; el asset original no cambia.
        Mesh originalMesh = originalFilter.sharedMesh;

        // Guardar la información antes de cambiar IndexFormat.
        vertices.AddRange(originalMesh.vertices);
        surfaceTriangles.AddRange(originalMesh.triangles);
        CopyUVs(originalMesh);

        // Crear la copia editable.
        visibleMesh = Instantiate(originalMesh);
        visibleMesh.name = originalMesh.name + "_IncisionMesh";
        visibleMesh.Clear();
        visibleMesh.indexFormat = IndexFormat.UInt32;
        visibleMesh.MarkDynamic();

        copyFilter.sharedMesh = visibleMesh;

        // Material 0 = piel/superficie. Material 1 = interior de la herida.
        Material wound = insideMaterial != null
            ? insideMaterial
            : CreateInsideMaterial();

        copyRenderer.sharedMaterials = new[]
        {
            originalRenderer.sharedMaterial,
            wound
        };

        // Se usa otra Mesh sin el fondo rojo para que el mouse sólo detecte
        // la superficie que todavía puede cortarse.
        collisionMesh = new Mesh
        {
            name = "IncisionCollisionMesh",
            indexFormat = IndexFormat.UInt32
        };
        collisionMesh.MarkDynamic();

        meshCollider = copyObject.GetComponent<MeshCollider>();

        if (meshCollider == null)
            meshCollider = copyObject.AddComponent<MeshCollider>();

        meshCollider.convex = false;
        ApplyMesh();

        if (hideOriginalObject)
            originalObject.SetActive(false);
    }

    private void CutMousePath(Vector2 from, Vector2 to)
    {
        float distance = Vector2.Distance(from, to);

        if (distance < pixelsBetweenCuts)
            return;

        int samples = Mathf.Clamp(
            Mathf.CeilToInt(distance / pixelsBetweenCuts),
            1,
            maximumCutsPerFrame);

        for (int i = 1; i <= samples; i++)
        {
            TryCut(Vector2.Lerp(from, to, (float)i / samples));

            if (totalCuts >= maximumTotalCuts)
                break;
        }

        lastMouseSample = to;
    }

    private void TryCut(Vector2 mousePosition)
    {
        if (totalCuts >= maximumTotalCuts)
            return;

        Camera cameraToUse = cutCamera != null ? cutCamera : Camera.main;

        if (cameraToUse == null)
            return;

        Ray ray = cameraToUse.ScreenPointToRay(mousePosition);
        RaycastHit hit;

        // PASO 2: encontrar el triángulo tocado por el mouse.
        if (meshCollider.Raycast(ray, out hit, cameraToUse.farClipPlane))
            CutTriangle(hit);
    }

    private void CutTriangle(RaycastHit hit)
    {
        int triangleStart = hit.triangleIndex * 3;

        if (triangleStart < 0 || triangleStart + 2 >= surfaceTriangles.Count)
            return;

        int aIndex = surfaceTriangles[triangleStart];
        int bIndex = surfaceTriangles[triangleStart + 1];
        int cIndex = surfaceTriangles[triangleStart + 2];

        Vector3 a = vertices[aIndex];
        Vector3 b = vertices[bIndex];
        Vector3 c = vertices[cIndex];

        // Las coordenadas baricéntricas colocan el corte dentro del triángulo.
        // El mínimo evita crear geometría degenerada exactamente en una arista.
        Vector3 barycentric = hit.barycentricCoordinate;
        barycentric.x = Mathf.Max(0.035f, barycentric.x);
        barycentric.y = Mathf.Max(0.035f, barycentric.y);
        barycentric.z = Mathf.Max(0.035f, barycentric.z);
        barycentric /= barycentric.x + barycentric.y + barycentric.z;

        Vector3 center =
            a * barycentric.x +
            b * barycentric.y +
            c * barycentric.z;

        Vector3 normal = Vector3.Cross(b - a, c - a).normalized;

        if (normal.sqrMagnitude < 0.000001f)
            return;

        float radius = incisionWidth * 0.5f;
        float amountA = AmountTowardCorner(center, a, radius);
        float amountB = AmountTowardCorner(center, b, radius);
        float amountC = AmountTowardCorner(center, c, radius);

        Vector3 innerA = Vector3.Lerp(center, a, amountA);
        Vector3 innerB = Vector3.Lerp(center, b, amountB);
        Vector3 innerC = Vector3.Lerp(center, c, amountC);
        Vector3 bottom = center - normal * incisionDepth;

        Vector2 centerUV =
            uvs[aIndex] * barycentric.x +
            uvs[bIndex] * barycentric.y +
            uvs[cIndex] * barycentric.z;

        int innerAIndex = AddVertex(
            innerA, Vector2.Lerp(centerUV, uvs[aIndex], amountA));
        int innerBIndex = AddVertex(
            innerB, Vector2.Lerp(centerUV, uvs[bIndex], amountB));
        int innerCIndex = AddVertex(
            innerC, Vector2.Lerp(centerUV, uvs[cIndex], amountC));
        int bottomIndex = AddVertex(bottom, centerUV);

        // PASO 3: eliminar el triángulo original.
        surfaceTriangles.RemoveRange(triangleStart, 3);

        // PASO 4: construir 6 triángulos alrededor del hueco.
        AddTriangle(surfaceTriangles, aIndex, bIndex, innerBIndex);
        AddTriangle(surfaceTriangles, aIndex, innerBIndex, innerAIndex);

        AddTriangle(surfaceTriangles, bIndex, cIndex, innerCIndex);
        AddTriangle(surfaceTriangles, bIndex, innerCIndex, innerBIndex);

        AddTriangle(surfaceTriangles, cIndex, aIndex, innerAIndex);
        AddTriangle(surfaceTriangles, cIndex, innerAIndex, innerCIndex);

        // PASO 5: construir 3 paredes triangulares hacia el fondo.
        // Cada pared se repite al revés para verla desde ambos lados.
        AddWall(innerAIndex, innerBIndex, bottomIndex);
        AddWall(innerBIndex, innerCIndex, bottomIndex);
        AddWall(innerCIndex, innerAIndex, bottomIndex);

        totalCuts++;
        ApplyMesh();
    }

    private float AmountTowardCorner(
        Vector3 center, Vector3 corner, float wantedRadius)
    {
        float distance = Vector3.Distance(center, corner);

        if (distance < 0.000001f)
            return 0.035f;

        // 0.45 conserva parte del triángulo original alrededor del hueco.
        return Mathf.Clamp(wantedRadius / distance, 0.035f, 0.45f);
    }

    private int AddVertex(Vector3 position, Vector2 uv)
    {
        int index = vertices.Count;
        vertices.Add(position);
        uvs.Add(uv);
        return index;
    }

    private static void AddTriangle(List<int> list, int a, int b, int c)
    {
        list.Add(a);
        list.Add(b);
        list.Add(c);
    }

    private void AddWall(int a, int b, int bottom)
    {
        AddTriangle(insideTriangles, a, b, bottom);
        AddTriangle(insideTriangles, bottom, b, a);
    }

    private void ApplyMesh()
    {
        // Malla que ve el jugador.
        visibleMesh.Clear();
        visibleMesh.SetVertices(vertices);
        visibleMesh.SetUVs(0, uvs);
        visibleMesh.subMeshCount = 2;
        visibleMesh.SetTriangles(surfaceTriangles, 0, false);
        visibleMesh.SetTriangles(insideTriangles, 1, false);
        visibleMesh.RecalculateNormals();
        visibleMesh.RecalculateBounds();

        // Malla que usa el raycast del mouse.
        collisionMesh.Clear();
        collisionMesh.SetVertices(vertices);
        collisionMesh.SetTriangles(surfaceTriangles, 0, false);
        collisionMesh.RecalculateBounds();

        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = collisionMesh;
    }

    private void CopyUVs(Mesh sourceMesh)
    {
        Vector2[] originalUVs = sourceMesh.uv;

        if (originalUVs.Length == vertices.Count)
        {
            uvs.AddRange(originalUVs);
            return;
        }

        for (int i = 0; i < vertices.Count; i++)
            uvs.Add(Vector2.zero);
    }

    private Material CreateInsideMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");

        if (shader == null)
            shader = Shader.Find("Standard");

        if (shader == null)
            return null;

        generatedInsideMaterial = new Material(shader);
        generatedInsideMaterial.name = "Generated Inside Material";
        generatedInsideMaterial.color =
            new Color(0.22f, 0.01f, 0.015f, 1f);

        return generatedInsideMaterial;
    }

    private static bool ReadMouse(out Vector2 position, out bool pressed)
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current == null)
        {
            position = Vector2.zero;
            pressed = false;
            return false;
        }

        position = Mouse.current.position.ReadValue();
        pressed = Mouse.current.leftButton.isPressed;
        return true;
#else
        position = Input.mousePosition;
        pressed = Input.GetMouseButton(0);
        return Input.mousePresent;
#endif
    }

    private void OnDestroy()
    {
        if (copyObject != null)
            Destroy(copyObject);

        if (visibleMesh != null)
            Destroy(visibleMesh);

        if (collisionMesh != null)
            Destroy(collisionMesh);

        if (generatedInsideMaterial != null)
            Destroy(generatedInsideMaterial);

        if (originalObject != null)
            originalObject.SetActive(wasOriginalActive);
    }
}
