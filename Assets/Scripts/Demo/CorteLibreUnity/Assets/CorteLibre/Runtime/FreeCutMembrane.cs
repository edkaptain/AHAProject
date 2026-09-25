using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using P = CorteLibre.PlanarCutMesh.Point;

namespace CorteLibre
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class FreeCutMembrane : MonoBehaviour
    {
        [Header("Superficie local XZ; regenerar tras cambiar dimensiones")]
        [Min(.01f)] public float width = .6f;
        [Min(.01f)] public float depth = .4f;
        [Range(1, 60)] public int columns = 12;
        [Range(1, 60)] public int rows = 8;
        [Header("Corte y visualización")]
        [Min(0)] public float openingWidth = .004f;
        [Min(.0001f)] public float minimumStrokeDistance = .004f;
        [Min(100)] public int maxTriangles = 16000;
        public bool showWireframe = true;
        public Material wireMaterial;

        public PlanarCutMesh Core { get; private set; }
        public int Generation { get; private set; }
        public string Status { get; private set; }
        public Vector3 SurfaceNormal { get { return transform.up; } }
        private PlanarCutMesh.RenderData render;
        private Mesh mesh, wireMesh;
        private MeshRenderer wireRenderer;
        private GameObject wireObject;
        private float previousOpening;

        private void Awake()
        {
            mesh = new Mesh { name = "Membrana remallada", indexFormat = IndexFormat.UInt32 };
            mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = mesh;
            wireObject = new GameObject("Aristas de la malla");
            wireObject.transform.SetParent(transform, false);
            wireObject.transform.localPosition = Vector3.up * .00008f;
            wireMesh = new Mesh { name = "Aristas remalladas", indexFormat = IndexFormat.UInt32 };
            wireMesh.MarkDynamic();
            wireObject.AddComponent<MeshFilter>().sharedMesh = wireMesh;
            wireRenderer = wireObject.AddComponent<MeshRenderer>();
            wireRenderer.sharedMaterial = wireMaterial;
            ResetMembrane();
        }
        private void Update()
        {
            if (Core == null) return;
            if (!Mathf.Approximately(previousOpening, openingWidth)) RebuildDisplay();
            wireRenderer.enabled = showWireframe && wireMaterial != null;
        }
        [ContextMenu("Reset Membrane")]
        public void ResetMembrane()
        {
            if (!Application.isPlaying || mesh == null) return;
            Core = new PlanarCutMesh(Mathf.Max(.01f, width), Mathf.Max(.01f, depth), Mathf.Clamp(columns, 1, 60), Mathf.Clamp(rows, 1, 60));
            Core.MaxFaces = Mathf.Max(Core.Faces.Count, maxTriangles);
            Generation++; Status = "Membrana intacta"; RebuildDisplay();
        }
        public bool CutSegment(P from, P to)
        {
            if (Core == null) return false;
            bool changed = Core.AddIncision(from, to);
            if (Core.LastError != null) { Status = Core.LastError; return false; }
            if (changed) { Status = "Incisión registrada"; RebuildDisplay(); }
            return changed;
        }
        public bool UniformScale(out float scale)
        {
            Vector3 s = transform.lossyScale; scale = s.x;
            bool ok = scale > 0 && Mathf.Abs(s.x - s.y) < .0001f && Mathf.Abs(s.x - s.z) < .0001f;
            if (!ok) Status = "Usa escala uniforme positiva en Membrana, por ejemplo (1,1,1).";
            return ok;
        }
        // Contacto con la geometría ABIERTA. La coordenada material se obtiene
        // por interpolación baricéntrica, para conservar la identidad del tejido.
        public bool TrySphereContact(Vector3 worldCenter, float worldRadius, out P materialPoint)
        {
            materialPoint = new P(); float scale;
            if (Core == null || render == null || worldRadius <= 0 || !UniformScale(out scale)) return false;
            Vector3 local = transform.InverseTransformPoint(worldCenter);
            double radius = worldRadius / scale, disk2 = radius * radius - local.y * local.y;
            if (disk2 < 0) return false;
            double disk = System.Math.Sqrt(disk2), best = double.PositiveInfinity; int bestFace = -1;
            P p = new P(local.x, local.z), nearest = new P();
            for (int t = 0; t < render.Indices.Length; t += 3)
            {
                P a = render.Positions[render.Indices[t]], b = render.Positions[render.Indices[t + 1]], c = render.Positions[render.Indices[t + 2]];
                if (p.X < System.Math.Min(a.X, System.Math.Min(b.X, c.X)) - disk || p.X > System.Math.Max(a.X, System.Math.Max(b.X, c.X)) + disk ||
                   p.Y < System.Math.Min(a.Y, System.Math.Min(b.Y, c.Y)) - disk || p.Y > System.Math.Max(a.Y, System.Math.Max(b.Y, c.Y)) + disk) continue;
                P q = PlanarCutMesh.ClosestTriangle(p, a, b, c), delta = p - q; double d = PlanarCutMesh.Dot(delta, delta);
                if (d < best) { best = d; nearest = q; bestFace = t; }
                if (best < 1e-24) break;
            }
            if (bestFace < 0 || best > disk2 + 1e-14) return false;
            int ia = render.Indices[bestFace], ib = render.Indices[bestFace + 1], ic = render.Indices[bestFace + 2];
            materialPoint = PlanarCutMesh.BarycentricMaterial(nearest, render.Positions[ia], render.Positions[ib], render.Positions[ic],
                Core.Points[render.Sources[ia]], Core.Points[render.Sources[ib]], Core.Points[render.Sources[ic]]);
            return true;
        }
        private void RebuildDisplay()
        {
            render = Core.BuildRender(Mathf.Max(0, openingWidth));
            var vertices = new Vector3[render.Positions.Length]; var uv = new Vector2[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                P p = render.Positions[i], source = Core.Points[render.Sources[i]];
                vertices[i] = new Vector3((float)p.X, 0, (float)p.Y);
                uv[i] = new Vector2((float)(source.X / Core.Width + .5), (float)(source.Y / Core.Height + .5));
            }
            int[] indices = (int[])render.Indices.Clone();
            for (int i = 0; i < indices.Length; i += 3) { int swap = indices[i + 1]; indices[i + 1] = indices[i + 2]; indices[i + 2] = swap; }
            mesh.Clear(); mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = indices; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var edges = new HashSet<ulong>(); var lines = new List<int>();
            for (int i = 0; i < indices.Length; i += 3) for (int k = 0; k < 3; k++)
            {
                int a = indices[i + k], b = indices[i + (k + 1) % 3];
                if (edges.Add(PlanarCutMesh.Key(a, b))) { lines.Add(a); lines.Add(b); }
            }
            wireMesh.Clear(); wireMesh.vertices = vertices; wireMesh.SetIndices(lines.ToArray(), MeshTopology.Lines, 0); wireMesh.RecalculateBounds();
            wireRenderer.enabled = showWireframe && wireMaterial != null; previousOpening = openingWidth;
        }
        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh); if (wireMesh != null) Destroy(wireMesh); if (wireObject != null) Destroy(wireObject);
        }
    }
}
