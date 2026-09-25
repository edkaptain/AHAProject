using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Genera una membrana intacta en el plano LOCAL XZ.
/// Una esfera dibuja un camino ajustado a aristas horizontales/verticales.
/// Las incisiones se conservan. La apertura es geometrica, no biomecanica.
/// Se usa contacto geometrico con los triangulos, sin MeshCollider.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class CuttableMembrane : MonoBehaviour
{
    [Header("Cuadricula: cambiar y ejecutar Reset Membrane")]
    [Min(0.01f)] public float width = 0.4f;
    [Min(0.01f)] public float depth = 0.3f;
    [Range(4, 100)] public int cellsX = 40;
    [Range(4, 100)] public int cellsZ = 30;

    [Header("Apertura en unidades locales")]
    [Min(0f)] public float openingWidth = 0.002f;
    [Tooltip("Ver la superficie desde arriba y abajo. No agrega grosor.")]
    public bool doubleSided = true;

    [Header("Muestreo del movimiento")]
    [Tooltip("Un salto mayor que esto inicia un nuevo trazo, no corta el salto.")]
    [Min(0.01f)] public float maxTravelPerFrame = 0.5f;
    [Range(16, 1024)] public int maxSamplesPerFrame = 256;
    public bool showCutGizmos = true;

    public int CutEdgeCount { get { return topology == null ? 0 : topology.CutEdgeCount; } }
    public int TopVertexCount { get { return topPositions == null ? 0 : topPositions.Length; } }
    public int OriginalVertexCount { get { return rest == null ? 0 : rest.Length; } }

    private Mesh mesh;
    private EdgeCutTopology topology;
    private Vector3[] rest, topPositions;
    private Vector2[] restUV;
    private int[] baseTriangles, topTriangles;
    // Estas dimensiones no cambian hasta ResetMembrane.
    private int nx, nz;
    private float builtWidth, builtDepth, dx, dz;
    private float lastOpening;
    private bool lastDoubleSided, hasPrevious, warnedScale;
    private Vector3 previousCenter;
    private int lastVertex = -1;

    private void Awake() { ResetMembrane(); }

    private void Update()
    {
        if (mesh != null &&
            (!Mathf.Approximately(lastOpening, openingWidth) || lastDoubleSided != doubleSided))
            RebuildMesh();
    }

    [ContextMenu("Reset Membrane")]
    public void ResetMembrane()
    {
        if (!Application.isPlaying) return;

        nx = Mathf.Clamp(cellsX, 4, 100);
        nz = Mathf.Clamp(cellsZ, 4, 100);
        builtWidth = Mathf.Max(0.01f, width);
        builtDepth = Mathf.Max(0.01f, depth);
        dx = builtWidth / nx;
        dz = builtDepth / nz;
        rest = new Vector3[(nx + 1) * (nz + 1)];
        restUV = new Vector2[rest.Length];

        for (int z = 0; z <= nz; z++)
        for (int x = 0; x <= nx; x++)
        {
            int v = VertexId(x, z);
            rest[v] = new Vector3(x * dx - builtWidth * 0.5f, 0f,
                                  z * dz - builtDepth * 0.5f);
            restUV[v] = new Vector2((float)x / nx, (float)z / nz);
        }

        baseTriangles = new int[nx * nz * 6];
        int c = 0;
        for (int z = 0; z < nz; z++)
        for (int x = 0; x < nx; x++)
        {
            int a = VertexId(x, z), b = VertexId(x + 1, z);
            int d = VertexId(x, z + 1), e = VertexId(x + 1, z + 1);
            // Orden de vertices: normales hacia +Y.
            baseTriangles[c++] = a; baseTriangles[c++] = d; baseTriangles[c++] = b;
            baseTriangles[c++] = b; baseTriangles[c++] = d; baseTriangles[c++] = e;
        }

        topology = new EdgeCutTopology(rest.Length, baseTriangles);
        if (mesh != null) Destroy(mesh);
        mesh = new Mesh { name = "Membrana con cortes por aristas" };
        mesh.indexFormat = IndexFormat.UInt32;
        mesh.MarkDynamic();
        GetComponent<MeshFilter>().sharedMesh = mesh;
        EndStroke();
        RebuildMesh();
    }

    private int VertexId(int x, int z) { return z * (nx + 1) + x; }

    public void EndStroke()
    {
        hasPrevious = false;
        lastVertex = -1;
    }

    /// <summary>ScalpelSphere llama a este metodo una vez por frame.</summary>
    public void CutWithSphere(Vector3 worldCenter, float worldRadius)
    {
        if (topology == null || worldRadius <= 0f)
        {
            EndStroke();
            return;
        }
        Vector3 scale = transform.lossyScale;
        // El contacto se calcula en local: se admite escala uniforme positiva.
        if (scale.x <= 0f || Mathf.Abs(scale.x - scale.y) > 0.0001f ||
            Mathf.Abs(scale.x - scale.z) > 0.0001f)
        {
            if (!warnedScale)
                Debug.LogError("La membrana necesita escala uniforme positiva, por ejemplo (1,1,1).", this);
            warnedScale = true;
            EndStroke();
            return;
        }

        Vector3 center = transform.InverseTransformPoint(worldCenter);
        float radius = worldRadius / scale.x;
        Vector3 from = hasPrevious ? previousCenter : center;
        float travel = Vector3.Distance(from, center);
        float step = Mathf.Max(0.000001f, Mathf.Min(dx, dz, radius) * 0.35f);
        int samples = Mathf.Max(1, Mathf.CeilToInt(travel / step));

        if (travel > maxTravelPerFrame / scale.x || samples > maxSamplesPerFrame)
        {
            // No unir teletransportes ni degradar silenciosamente el muestreo.
            lastVertex = -1;
            from = center;
            samples = 1;
        }

        bool changed = false;
        for (int i = 1; i <= samples; i++)
        {
            Vector3 contact;
            Vector3 sample = Vector3.Lerp(from, center, (float)i / samples);
            if (!TrySphereContact(sample, radius, out contact))
            {
                lastVertex = -1; // Al salir del tejido termina la incisión.
                continue;
            }

            int x = Mathf.Clamp(Mathf.RoundToInt((contact.x + builtWidth * 0.5f) / dx), 0, nx);
            int z = Mathf.Clamp(Mathf.RoundToInt((contact.z + builtDepth * 0.5f) / dz), 0, nz);
            int currentVertex = VertexId(x, z);
            if (lastVertex >= 0 && lastVertex != currentVertex)
                changed |= CutGridPath(lastVertex, currentVertex);
            lastVertex = currentVertex;
        }

        previousCenter = center;
        hasPrevious = true;
        if (changed) RebuildMesh(); // Una reconstruccion por frame de corte.
    }

    private bool CutGridPath(int start, int end)
    {
        int x = start % (nx + 1), z = start / (nx + 1);
        int ex = end % (nx + 1), ez = end / (nx + 1);
        Vector2 a = XZ(rest[start]), b = XZ(rest[end]);
        Vector2 line = b - a;
        bool changed = false;

        while (x != ex || z != ez)
        {
            int nextX = x, nextZ = z;
            if (x == ex) nextZ += ez > z ? 1 : -1;
            else if (z == ez) nextX += ex > x ? 1 : -1;
            else
            {
                int sx = x + (ex > x ? 1 : -1);
                int sz = z + (ez > z ? 1 : -1);
                // Elegir la arista horizontal/vertical mas cercana al trazo.
                float errorX = Mathf.Abs(Cross2(line, XZ(rest[VertexId(sx, z)]) - a));
                float errorZ = Mathf.Abs(Cross2(line, XZ(rest[VertexId(x, sz)]) - a));
                if (errorX <= errorZ) nextX = sx;
                else nextZ = sz;
            }
            changed |= topology.CutEdge(VertexId(x, z), VertexId(nextX, nextZ));
            x = nextX; z = nextZ;
        }
        return changed;
    }

    private void RebuildMesh()
    {
        EdgeCutTopology.SplitResult split = topology.BuildSplit();
        topTriangles = split.Triangles;
        topPositions = new Vector3[split.SourceVertex.Length];
        var directions = new Vector3[topPositions.Length];

        foreach (EdgeCutTopology.Edge edge in topology.CutEdges)
        {
            Vector3 along = rest[edge.B] - rest[edge.A];
            Vector3 normal = new Vector3(-along.z, 0f, along.x).normalized;
            int t = (edge.A0 / 3) * 3;
            Vector3 centroid = (rest[baseTriangles[t]] + rest[baseTriangles[t + 1]] +
                                rest[baseTriangles[t + 2]]) / 3f;
            if (Vector3.Dot(normal, centroid - rest[edge.A]) < 0f) normal = -normal;

            AddOppositeDirections(topTriangles[edge.A0], topTriangles[edge.A1], normal, directions);
            AddOppositeDirections(topTriangles[edge.B0], topTriangles[edge.B1], normal, directions);
        }

        // Apertura pequena: limita la distorsion de las celdas vecinas.
        float halfGap = Mathf.Clamp(openingWidth, 0f, 0.4f * Mathf.Min(dx, dz)) * 0.5f;
        int copies = doubleSided ? 2 : 1;
        int count = topPositions.Length;
        var positions = new Vector3[count * copies];
        var uv = new Vector2[positions.Length];

        for (int i = 0; i < count; i++)
        {
            int source = split.SourceVertex[i];
            topPositions[i] = rest[source] + directions[i].normalized * halfGap;
            positions[i] = topPositions[i];
            uv[i] = restUV[source];
            if (doubleSided)
            {
                positions[i + count] = positions[i];
                uv[i + count] = uv[i];
            }
        }

        var renderTriangles = new int[topTriangles.Length * copies];
        System.Array.Copy(topTriangles, renderTriangles, topTriangles.Length);
        if (doubleSided)
        {
            for (int i = 0; i < topTriangles.Length; i += 3)
            {
                int back = topTriangles.Length + i;
                renderTriangles[back] = topTriangles[i] + count;
                renderTriangles[back + 1] = topTriangles[i + 2] + count;
                renderTriangles[back + 2] = topTriangles[i + 1] + count;
            }
        }

        mesh.Clear();
        mesh.vertices = positions;
        mesh.uv = uv;
        mesh.triangles = renderTriangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        lastOpening = openingWidth;
        lastDoubleSided = doubleSided;
    }

    private static void AddOppositeDirections(int first, int second, Vector3 normal, Vector3[] output)
    {
        if (first == second) return; // Punta aún conectada: permanece cerrada.
        output[first] += normal;
        output[second] -= normal;
    }

    private bool TrySphereContact(Vector3 center, float radius, out Vector3 contact)
    {
        contact = Vector3.zero;
        // Todas las posiciones siguen en y=0. La esfera corta ese plano
        // en un disco. Buscamos contacto con los triangulos YA abiertos.
        float diskSquared = radius * radius - center.y * center.y;
        if (diskSquared < 0f) return false;
        float diskRadius = Mathf.Sqrt(diskSquared);
        Vector2 p = XZ(center);
        float best = float.PositiveInfinity;
        Vector2 nearest = Vector2.zero;

        for (int i = 0; i < topTriangles.Length; i += 3)
        {
            Vector2 a = XZ(topPositions[topTriangles[i]]);
            Vector2 b = XZ(topPositions[topTriangles[i + 1]]);
            Vector2 c = XZ(topPositions[topTriangles[i + 2]]);
            // Rechazo rapido por caja: evita calcular distancias lejanas.
            if (p.x < Mathf.Min(a.x, b.x, c.x) - diskRadius ||
                p.x > Mathf.Max(a.x, b.x, c.x) + diskRadius ||
                p.y < Mathf.Min(a.y, b.y, c.y) - diskRadius ||
                p.y > Mathf.Max(a.y, b.y, c.y) + diskRadius) continue;

            Vector2 q = ClosestPointInTriangle(p, a, b, c);
            float distanceSquared = (p - q).sqrMagnitude;
            if (distanceSquared < best) { best = distanceSquared; nearest = q; }
            if (best <= 1e-14f) break;
        }

        if (best > diskSquared + 1e-12f) return false;
        contact = new Vector3(nearest.x, 0f, nearest.y);
        return true;
    }

    private static Vector2 ClosestPointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float ab = Cross2(b - a, p - a);
        float bc = Cross2(c - b, p - b);
        float ca = Cross2(a - c, p - c);
        if ((ab >= 0f && bc >= 0f && ca >= 0f) || (ab <= 0f && bc <= 0f && ca <= 0f))
            return p;
        Vector2 q = ClosestPointOnSegment(p, a, b);
        Vector2 r = ClosestPointOnSegment(p, b, c);
        Vector2 s = ClosestPointOnSegment(p, c, a);
        if ((r - p).sqrMagnitude < (q - p).sqrMagnitude) q = r;
        if ((s - p).sqrMagnitude < (q - p).sqrMagnitude) q = s;
        return q;
    }

    private static Vector2 ClosestPointOnSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lengthSquared = ab.sqrMagnitude;
        if (lengthSquared < 1e-14f) return a;
        return a + ab * Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSquared);
    }

    private static Vector2 XZ(Vector3 v) { return new Vector2(v.x, v.z); }
    private static float Cross2(Vector2 a, Vector2 b) { return a.x * b.y - a.y * b.x; }

    [ContextMenu("Print Mesh Stats")]
    public void PrintMeshStats()
    {
        if (topology == null) return;
        Debug.Log("Originales: " + OriginalVertexCount +
                  " | Vertices superiores actuales: " + TopVertexCount +
                  " | Triangulos superiores: " + topTriangles.Length / 3 +
                  " | Aristas cortadas: " + CutEdgeCount, this);
    }

    private void OnDrawGizmosSelected()
    {
        if (!showCutGizmos || topology == null) return;
        Gizmos.color = Color.red;
        foreach (EdgeCutTopology.Edge e in topology.CutEdges)
            Gizmos.DrawLine(transform.TransformPoint(rest[e.A] + Vector3.up * 0.0002f),
                            transform.TransformPoint(rest[e.B] + Vector3.up * 0.0002f));
    }

    private void OnDisable() { EndStroke(); }
    private void OnDestroy() { if (mesh != null) Destroy(mesh); }
}
