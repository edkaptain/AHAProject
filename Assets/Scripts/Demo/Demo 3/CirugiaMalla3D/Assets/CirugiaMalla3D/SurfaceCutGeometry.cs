using System;
using System.Collections.Generic;

namespace Cirugia3D
{
    // Geometria material 3D, independiente de Unity. Conserva la superficie
    // original; el desplazamiento de los labios solo se aplica al dibujar.
    public sealed class SurfaceCutGeometry
    {
        public struct V
        {
            public double X, Y, Z;
            public V(double x, double y, double z) { X = x; Y = y; Z = z; }
            public static V operator +(V a, V b) { return new V(a.X + b.X, a.Y + b.Y, a.Z + b.Z); }
            public static V operator -(V a, V b) { return new V(a.X - b.X, a.Y - b.Y, a.Z - b.Z); }
            public static V operator *(V a, double s) { return new V(a.X * s, a.Y * s, a.Z * s); }
            public double Square { get { return Dot(this, this); } }
            public double Length { get { return Math.Sqrt(Square); } }
            public V Unit { get { double d = Length; return d > 1e-30 ? this * (1 / d) : new V(); } }
        }

        public struct Face
        {
            public int A, B, C, Source;
            public Face(int a, int b, int c, int source) { A = a; B = b; C = c; Source = source; }
            public int this[int i] { get { return i == 0 ? A : (i == 1 ? B : C); } }
        }

        public struct Segment
        {
            public int Source;
            public V From, To;
            public Segment(int source, V from, V to) { Source = source; From = from; To = to; }
        }

        public sealed class Edge
        {
            public int A, B, F0, K0, F1 = -1, K1 = -1;
        }

        public sealed class Display
        {
            // Una posicion por esquina: preserva costuras UV y normales duras.
            public V[] Positions;
            public bool[] Separated;
            public Dictionary<ulong, Edge> Edges;
        }

        private readonly List<V> points = new List<V>();
        private List<Face> faces = new List<Face>();
        private HashSet<ulong> cuts = new HashSet<ulong>();
        private readonly List<Face> sources = new List<Face>();
        private readonly List<ulong> originalBoundary = new List<ulong>();
        private readonly double[] sourceAreas;
        public readonly double Epsilon;
        public IReadOnlyList<V> Points { get { return points; } }
        public IReadOnlyList<Face> Faces { get { return faces; } }
        public IReadOnlyList<Face> Sources { get { return sources; } }
        public IEnumerable<ulong> Cuts { get { return cuts; } }
        public int CutCount { get { return cuts.Count; } }
        public int MaxFaces = 30000;
        public string LastError { get; private set; }

        public SurfaceCutGeometry(V[] vertices, int[] triangles, double epsilon, bool weldSeams = true)
        {
            if (vertices == null || vertices.Length < 3 || triangles == null || triangles.Length % 3 != 0)
                throw new ArgumentException("Malla de triangulos invalida.");
            Epsilon = Math.Max(1e-12, epsilon);
            int[] map = new int[vertices.Length];
            var cells = new Dictionary<Cell, List<int>>();
            for (int i = 0; i < vertices.Length; i++)
            {
                if (!Finite(vertices[i])) throw new ArgumentException("La malla contiene posiciones no finitas.");
                Cell cell = new Cell(vertices[i], Epsilon);
                int id = -1;
                if (weldSeams)
                {
                    for (int x = -1; x <= 1 && id < 0; x++)
                    for (int y = -1; y <= 1 && id < 0; y++)
                    for (int z = -1; z <= 1 && id < 0; z++)
                    {
                        List<int> bucket;
                        if (!cells.TryGetValue(cell.Offset(x, y, z), out bucket)) continue;
                        foreach (int candidate in bucket)
                            if ((vertices[i] - points[candidate]).Square <= Epsilon * Epsilon) { id = candidate; break; }
                    }
                }
                if (id < 0)
                {
                    id = points.Count; points.Add(vertices[i]);
                    List<int> bucket;
                    if (!cells.TryGetValue(cell, out bucket)) { bucket = new List<int>(); cells.Add(cell, bucket); }
                    bucket.Add(id);
                }
                map[i] = id;
            }
            sourceAreas = new double[triangles.Length / 3];
            for (int t = 0; t < triangles.Length; t += 3)
            {
                for (int k = 0; k < 3; k++)
                    if (triangles[t + k] < 0 || triangles[t + k] >= vertices.Length)
                        throw new ArgumentException("Indice de triangulo fuera del rango.");
                Face f = new Face(map[triangles[t]], map[triangles[t + 1]], map[triangles[t + 2]], t / 3);
                double area = Cross(points[f.B] - points[f.A], points[f.C] - points[f.A]).Length;
                if (f.A == f.B || f.A == f.C || f.B == f.C || area <= Epsilon * Epsilon)
                    throw new ArgumentException("Triangulo degenerado en el modelo: " + t / 3 + ". Limpia el modelo o reduce Tolerance Relative.");
                sources.Add(f); faces.Add(f); sourceAreas[f.Source] = area;
            }
            if (faces.Count == 0) throw new ArgumentException("La malla no tiene triangulos.");
            foreach (var pair in BuildEdges()) if (pair.Value.F1 < 0) originalBoundary.Add(pair.Key);
            MaxFaces = Math.Max(MaxFaces, faces.Count);
            Validate();
        }

        public static double Dot(V a, V b) { return a.X * b.X + a.Y * b.Y + a.Z * b.Z; }
        public static V Cross(V a, V b) { return new V(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X); }
        public static ulong Key(int a, int b) { return ((ulong)(uint)Math.Min(a, b) << 32) | (uint)Math.Max(a, b); }
        public static int KeyA(ulong key) { return (int)(key >> 32); }
        public static int KeyB(ulong key) { return (int)(key & 0xffffffffUL); }
        public V Normal(int source) { Face f = sources[source]; return Cross(points[f.B] - points[f.A], points[f.C] - points[f.A]).Unit; }
        public static V ClosestSegment(V p, V a, V b)
        {
            V d = b - a;
            return d.Square < 1e-30 ? a : a + d * Math.Max(0, Math.Min(1, Dot(p - a, d) / d.Square));
        }
        public static V Barycentric(V p, V a, V b, V c)
        {
            V u = b - a, v = c - a, q = p - a;
            double uu = Dot(u, u), uv = Dot(u, v), vv = Dot(v, v);
            double d = uu * vv - uv * uv;
            if (Math.Abs(d) < 1e-40) throw new InvalidOperationException("Triangulo demasiado pequeno.");
            double y = (vv * Dot(q, u) - uv * Dot(q, v)) / d;
            double z = (uu * Dot(q, v) - uv * Dot(q, u)) / d;
            return new V(1 - y - z, y, z);
        }

        // Todo el movimiento de mouse se confirma o se revierte junto.
        public bool CutBatch(IList<Segment> segments)
        {
            LastError = null;
            if (segments == null || segments.Count == 0) return false;
            int oldPoints = points.Count;
            var oldFaces = new List<Face>(faces);
            var oldCuts = new HashSet<ulong>(cuts);
            try
            {
                foreach (Segment s in segments)
                {
                    if (s.Source < 0 || s.Source >= sources.Count || !Finite(s.From) || !Finite(s.To))
                        throw new InvalidOperationException("Contacto de corte invalido.");
                    AddSegment(s);
                    if (faces.Count > MaxFaces) throw new InvalidOperationException("Limite de triangulos alcanzado. Restaura la malla.");
                }
                EnsureIsolatedCutMidpoints();
                if (faces.Count > MaxFaces) throw new InvalidOperationException("Limite de triangulos alcanzado. Restaura la malla.");
                Validate();
                return cuts.Count != oldCuts.Count || faces.Count != oldFaces.Count;
            }
            catch (InvalidOperationException ex)
            {
                points.RemoveRange(oldPoints, points.Count - oldPoints);
                faces = oldFaces; cuts = oldCuts; LastError = ex.Message;
                return false;
            }
        }

        private void AddSegment(Segment s)
        {
            V normal = Normal(s.Source), origin = points[sources[s.Source].A];
            V start = s.From - normal * Dot(s.From - origin, normal);
            V end = s.To - normal * Dot(s.To - origin, normal);
            if ((end - start).Length <= Epsilon * 8) return;
            if (!InsideSource(start, s.Source) || !InsideSource(end, s.Source))
                throw new InvalidOperationException("El trazo sale de su triangulo de referencia.");
            int first = InsertPoint(start, s.Source), last = InsertPoint(end, s.Source);
            if (first == last) return;
            start = points[first]; end = points[last];
            V direction = end - start;
            double length = direction.Length;
            V side = Cross(normal, direction) * (1 / length);
            var output = new List<Face>(faces.Count + 8);
            var intersections = new Dictionary<ulong, int>();
            var addedCuts = new HashSet<ulong>();
            foreach (Face f in faces)
            {
                if (f.Source != s.Source) { output.Add(f); continue; }
                int[] ids = { f.A, f.B, f.C };
                double[] d = new double[3];
                bool positive = false, negative = false;
                var line = new List<int>(3);
                double lo = double.PositiveInfinity, hi = double.NegativeInfinity;
                for (int k = 0; k < 3; k++)
                {
                    d[k] = Dot(points[ids[k]] - start, side);
                    if (Math.Abs(d[k]) <= Epsilon) d[k] = 0;
                    positive |= d[k] > 0; negative |= d[k] < 0;
                    if (d[k] == 0)
                    {
                        Unique(line, ids[k]);
                        double t = Dot(points[ids[k]] - start, direction) / direction.Square;
                        lo = Math.Min(lo, t); hi = Math.Max(hi, t);
                    }
                }
                for (int k = 0; k < 3; k++)
                {
                    int j = (k + 1) % 3;
                    if (d[k] * d[j] >= 0) continue;
                    V q = points[ids[k]] + (points[ids[j]] - points[ids[k]]) * (d[k] / (d[k] - d[j]));
                    double t = Dot(q - start, direction) / direction.Square;
                    lo = Math.Min(lo, t); hi = Math.Max(hi, t);
                }
                if (double.IsInfinity(lo) || hi <= lo || hi <= Epsilon / length || lo >= 1 - Epsilon / length)
                { output.Add(f); continue; }
                if (lo < -Epsilon * 8 / length || hi > 1 + Epsilon * 8 / length)
                    throw new InvalidOperationException("Cruce ambiguo cerca de un vertice. Prueba un trazo un poco mas separado.");
                if (positive && negative)
                {
                    var left = new List<int>(4); var right = new List<int>(4);
                    for (int k = 0; k < 3; k++)
                    {
                        int j = (k + 1) % 3;
                        if (d[k] >= 0) Unique(left, ids[k]);
                        if (d[k] <= 0) Unique(right, ids[k]);
                        if (d[k] * d[j] >= 0) continue;
                        ulong key = Key(ids[k], ids[j]); int v;
                        if (!intersections.TryGetValue(key, out v))
                        {
                            v = points.Count;
                            points.Add(points[ids[k]] + (points[ids[j]] - points[ids[k]]) * (d[k] / (d[k] - d[j])));
                            intersections.Add(key, v);
                        }
                        Unique(left, v); Unique(right, v); Unique(line, v);
                    }
                    Fan(left, f.Source, output); Fan(right, f.Source, output);
                }
                else output.Add(f);
                if (line.Count == 2) addedCuts.Add(Key(line[0], line[1]));
            }
            faces = output;
            foreach (var pair in intersections) InheritCut(pair.Key, pair.Value);
            cuts.UnionWith(addedCuts);
        }

        private bool InsideSource(V p, int source)
        {
            Face f = sources[source];
            return Inside(p, f, Epsilon * 4);
        }
        private bool Inside(V p, Face f, double tolerance)
        {
            V n = Normal(f.Source);
            for (int k = 0; k < 3; k++)
            {
                V a = points[f[k]], b = points[f[(k + 1) % 3]];
                if (Dot(Cross(b - a, p - a), n) < -tolerance * (b - a).Length) return false;
            }
            return true;
        }
        private int InsertPoint(V p, int source)
        {
            // La busqueda esta restringida a esta superficie: no une capas cercanas.
            foreach (Face f in faces)
                if (f.Source == source)
                    for (int k = 0; k < 3; k++)
                        if ((points[f[k]] - p).Length <= Epsilon * 2) return f[k];
            foreach (Face f in faces)
            {
                if (f.Source != source) continue;
                for (int k = 0; k < 3; k++)
                {
                    int a = f[k], b = f[(k + 1) % 3];
                    V q = ClosestSegment(p, points[a], points[b]);
                    if ((q - p).Length > Epsilon * 2) continue;
                    return SplitEdge(a, b, q);
                }
            }
            for (int i = 0; i < faces.Count; i++)
            {
                Face f = faces[i];
                if (f.Source != source || !Inside(p, f, 0)) continue;
                int v = points.Count; points.Add(p); faces.RemoveAt(i);
                AddFace(faces, f.A, f.B, v, source);
                AddFace(faces, f.B, f.C, v, source);
                AddFace(faces, f.C, f.A, v, source);
                return v;
            }
            throw new InvalidOperationException("No se pudo insertar el contacto en la superficie.");
        }
        private int SplitEdge(int a, int b, V q)
        {
            int v = points.Count; points.Add(q);
            var output = new List<Face>(faces.Count + 2);
            foreach (Face f in faces)
            {
                bool found = false;
                for (int k = 0; k < 3; k++)
                {
                    int x = f[k], y = f[(k + 1) % 3], z = f[(k + 2) % 3];
                    if (!((x == a && y == b) || (x == b && y == a))) continue;
                    AddFace(output, x, v, z, f.Source); AddFace(output, v, y, z, f.Source);
                    found = true; break;
                }
                if (!found) output.Add(f);
            }
            faces = output; InheritCut(Key(a, b), v); return v;
        }
        private void InheritCut(ulong key, int v)
        {
            if (!cuts.Remove(key)) return;
            cuts.Add(Key(KeyA(key), v)); cuts.Add(Key(v, KeyB(key)));
        }
        private void EnsureIsolatedCutMidpoints()
        {
            var degree = new Dictionary<int, int>();
            foreach (ulong key in cuts)
            {
                int a = KeyA(key), b = KeyB(key), count;
                degree.TryGetValue(a, out count); degree[a] = count + 1;
                degree.TryGetValue(b, out count); degree[b] = count + 1;
            }
            var isolated = new List<ulong>();
            foreach (ulong key in cuts) if (degree[KeyA(key)] == 1 && degree[KeyB(key)] == 1) isolated.Add(key);
            foreach (ulong key in isolated)
            {
                int a = KeyA(key), b = KeyB(key);
                SplitEdge(a, b, (points[a] + points[b]) * .5);
            }
        }
        private void Fan(List<int> polygon, int source, List<Face> output)
        { for (int k = 1; k < polygon.Count - 1; k++) AddFace(output, polygon[0], polygon[k], polygon[k + 1], source); }
        private void AddFace(List<Face> output, int a, int b, int c, int source)
        {
            double area = Dot(Cross(points[b] - points[a], points[c] - points[a]), Normal(source));
            if (a == b || a == c || b == c || area <= Epsilon * Epsilon)
                throw new InvalidOperationException("Operacion revertida: triangulo demasiado delgado o invertido.");
            output.Add(new Face(a, b, c, source));
        }

        public Dictionary<ulong, Edge> BuildEdges()
        {
            var result = new Dictionary<ulong, Edge>();
            for (int t = 0; t < faces.Count; t++) for (int k = 0; k < 3; k++)
            {
                int a = faces[t][k], b = faces[t][(k + 1) % 3];
                ulong key = Key(a, b); Edge e;
                if (!result.TryGetValue(key, out e))
                    result.Add(key, new Edge { A = a, B = b, F0 = t, K0 = k });
                else
                {
                    if (e.F1 >= 0) throw new InvalidOperationException("Malla no manifold: una arista tiene mas de dos caras. Limpia el modelo o desactiva Weld Seams.");
                    if (e.A != b || e.B != a) throw new InvalidOperationException("Caras duplicadas o winding inconsistente. Corrige las normales del modelo.");
                    e.F1 = t; e.K1 = k;
                }
            }
            return result;
        }

        public void Validate()
        {
            var edges = BuildEdges(); var areas = new double[sources.Count];
            foreach (Face f in faces)
            {
                double area = Dot(Cross(points[f.B] - points[f.A], points[f.C] - points[f.A]), Normal(f.Source));
                if (area <= Epsilon * Epsilon) throw new InvalidOperationException("Cara invertida o degenerada.");
                areas[f.Source] += area;
            }
            for (int i = 0; i < areas.Length; i++)
                if (Math.Abs(areas[i] - sourceAreas[i]) > Math.Max(sourceAreas[i] * 1e-6, Epsilon * Epsilon * 32))
                    throw new InvalidOperationException("Operacion revertida: no se conservo el area del triangulo " + i + ".");
            foreach (ulong cut in cuts)
                if (!edges.ContainsKey(cut)) throw new InvalidOperationException("Referencia a una incision perdida.");
            foreach (var pair in edges)
            {
                Edge e = pair.Value;
                if (e.F1 >= 0) continue;
                bool boundary = false;
                foreach (ulong key in originalBoundary)
                {
                    V a = points[KeyA(key)], b = points[KeyB(key)];
                    if ((ClosestSegment(points[e.A], a, b) - points[e.A]).Length <= Epsilon * 4 &&
                        (ClosestSegment(points[e.B], a, b) - points[e.B]).Length <= Epsilon * 4)
                    { boundary = true; break; }
                }
                if (!boundary) throw new InvalidOperationException("Operacion revertida: aparecio una grieta fuera del corte.");
            }
        }

        public Display BuildDisplay(double opening)
        {
            var edges = BuildEdges(); var union = new Union(faces.Count * 3);
            foreach (var pair in edges)
            {
                Edge e = pair.Value;
                if (e.F1 < 0 || cuts.Contains(pair.Key)) continue;
                union.Join(e.F0 * 3 + e.K0, e.F1 * 3 + (e.K1 + 1) % 3);
                union.Join(e.F0 * 3 + (e.K0 + 1) % 3, e.F1 * 3 + e.K1);
            }
            int count = faces.Count * 3;
            var directions = new V[count]; var limits = new double[count]; var separated = new bool[count];
            for (int i = 0; i < count; i++) limits[i] = Math.Max(0, opening) * .5;
            foreach (ulong key in cuts)
            {
                Edge e = edges[key];
                if (e.F1 < 0) continue;
                AddLip(e.F0, e.K0, e.F1, e.K1, union, directions, separated);
                AddLip(e.F1, e.K1, e.F0, e.K0, union, directions, separated);
            }
            for (int t = 0; t < faces.Count; t++)
            {
                Face f = faces[t]; V a = points[f.A], b = points[f.B], c = points[f.C];
                double longest = Math.Max((b - a).Length, Math.Max((c - a).Length, (c - b).Length));
                double maxMove = Cross(b - a, c - a).Length / longest * .5;
                for (int k = 0; k < 3; k++)
                {
                    int root = union.Find(t * 3 + k);
                    limits[root] = Math.Min(limits[root], maxMove);
                }
            }
            var output = new V[count]; var split = new bool[count];
            for (int i = 0; i < count; i++)
            {
                int root = union.Find(i);
                output[i] = points[faces[i / 3][i % 3]] + directions[root].Unit * limits[root];
                split[i] = separated[root];
            }
            // Comprobacion independiente de orientacion antes de mostrar.
            for (int t = 0; t < faces.Count; t++)
                if (Dot(Cross(output[t * 3 + 1] - output[t * 3], output[t * 3 + 2] - output[t * 3]), Normal(faces[t].Source)) <= 0)
                    throw new InvalidOperationException("La apertura invertiria una cara. Reduce Opening Width.");
            return new Display { Positions = output, Separated = split, Edges = edges };
        }
        private void AddLip(int face, int edge, int otherFace, int otherEdge, Union union, V[] directions, bool[] split)
        {
            Face f = faces[face]; V a = points[f[edge]], b = points[f[(edge + 1) % 3]];
            V inward = Cross(Normal(f.Source), b - a).Unit;
            for (int k = 0; k < 2; k++)
            {
                int root = union.Find(face * 3 + (edge + k) % 3);
                int opposite = union.Find(otherFace * 3 + (otherEdge + 1 - k) % 3);
                if (root == opposite) continue; // Mantener unidas las puntas internas.
                directions[root] = directions[root] + inward; split[root] = true;
            }
        }
        private static bool Finite(V p)
        { return !double.IsNaN(p.X) && !double.IsInfinity(p.X) && !double.IsNaN(p.Y) && !double.IsInfinity(p.Y) && !double.IsNaN(p.Z) && !double.IsInfinity(p.Z); }
        private static void Unique(List<int> list, int id) { if (!list.Contains(id)) list.Add(id); }

        private struct Cell : IEquatable<Cell>
        {
            private long x, y, z;
            public Cell(V p, double size) { x = (long)Math.Floor(p.X / size); y = (long)Math.Floor(p.Y / size); z = (long)Math.Floor(p.Z / size); }
            public Cell Offset(int a, int b, int c) { return new Cell { x = x + a, y = y + b, z = z + c }; }
            public bool Equals(Cell other) { return x == other.x && y == other.y && z == other.z; }
            public override bool Equals(object obj) { return obj is Cell && Equals((Cell)obj); }
            public override int GetHashCode() { unchecked { return x.GetHashCode() * 73856093 ^ y.GetHashCode() * 19349663 ^ z.GetHashCode() * 83492791; } }
        }
        private sealed class Union
        {
            private readonly int[] parent; private readonly byte[] rank;
            public Union(int n) { parent = new int[n]; rank = new byte[n]; for (int i = 0; i < n; i++) parent[i] = i; }
            public int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
            public void Join(int a, int b)
            { a = Find(a); b = Find(b); if (a == b) return; if (rank[a] < rank[b]) parent[a] = b; else { parent[b] = a; if (rank[a] == rank[b]) rank[a]++; } }
        }
    }
}
