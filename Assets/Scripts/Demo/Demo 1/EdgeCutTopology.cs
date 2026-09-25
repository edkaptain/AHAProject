using System;
using System.Collections.Generic;

/// <summary>
/// Topologia de una superficie triangular. No depende de Unity.
/// Conserva todos los triangulos y separa sus vertices SOLO donde un corte
/// desconecta los grupos de triangulos que rodeaban al mismo vertice.
/// </summary>
public sealed class EdgeCutTopology
{
    public sealed class Edge
    {
        public readonly int A, B;
        // Indices de ESQUINAS dentro del arreglo de triangulos, no vertices.
        public readonly int A0, B0;
        public int A1 = -1, B1 = -1;
        public bool IsBoundary { get { return A1 < 0; } }

        public Edge(int a, int b, int a0, int b0)
        {
            A = a; B = b; A0 = a0; B0 = b0;
        }
    }

    public sealed class SplitResult
    {
        // SourceVertex[i] indica de que vertice ORIGINAL proviene el nuevo i.
        public readonly int[] SourceVertex;
        // Mismos triangulos; indices nuevos despues de separar conexiones.
        public readonly int[] Triangles;

        public SplitResult(int[] sourceVertex, int[] triangles)
        {
            SourceVertex = sourceVertex;
            Triangles = triangles;
        }
    }

    private readonly int[] triangles;
    private readonly Dictionary<ulong, Edge> edges = new Dictionary<ulong, Edge>();
    private readonly HashSet<ulong> cuts = new HashSet<ulong>();

    public int CutEdgeCount { get { return cuts.Count; } }

    public EdgeCutTopology(int vertexCount, int[] triangleIndices)
    {
        if (triangleIndices == null || triangleIndices.Length % 3 != 0)
            throw new ArgumentException("Se necesitan triangulos completos.");

        triangles = (int[])triangleIndices.Clone();
        for (int i = 0; i < triangles.Length; i++)
            if (triangles[i] < 0 || triangles[i] >= vertexCount)
                throw new ArgumentException("Indice de vertice fuera de rango.");

        for (int c = 0; c < triangles.Length; c += 3)
        {
            if (triangles[c] == triangles[c + 1] ||
                triangles[c] == triangles[c + 2] ||
                triangles[c + 1] == triangles[c + 2])
                throw new ArgumentException("Triangulo con vertices repetidos.");
            AddEdge(c, c + 1);
            AddEdge(c + 1, c + 2);
            AddEdge(c + 2, c);
        }
    }

    public static ulong Key(int a, int b)
    {
        uint lo = (uint)Math.Min(a, b);
        uint hi = (uint)Math.Max(a, b);
        return ((ulong)lo << 32) | hi;
    }

    private void AddEdge(int cornerA, int cornerB)
    {
        if (triangles[cornerA] > triangles[cornerB])
        {
            int swap = cornerA; cornerA = cornerB; cornerB = swap;
        }
        int a = triangles[cornerA], b = triangles[cornerB];
        ulong key = Key(a, b);
        Edge edge;
        if (!edges.TryGetValue(key, out edge))
        {
            edges.Add(key, new Edge(a, b, cornerA, cornerB));
            return;
        }
        if (!edge.IsBoundary)
            throw new ArgumentException("Arista con mas de dos triangulos.");
        edge.A1 = cornerA;
        edge.B1 = cornerB;
    }

    public bool CutEdge(int a, int b)
    {
        Edge edge;
        ulong key = Key(a, b);
        // El borde exterior ya esta abierto; no tiene una union que cortar.
        if (!edges.TryGetValue(key, out edge) || edge.IsBoundary)
            return false;
        return cuts.Add(key); // Repetir un corte no genera duplicados extra.
    }

    public IEnumerable<Edge> CutEdges
    {
        get
        {
            foreach (ulong key in cuts)
                yield return edges[key];
        }
    }

    public SplitResult BuildSplit()
    {
        // Una esquina por cada entrada del arreglo de triangulos.
        var groups = new DisjointSet(triangles.Length);

        foreach (KeyValuePair<ulong, Edge> entry in edges)
        {
            Edge e = entry.Value;
            if (e.IsBoundary || cuts.Contains(entry.Key))
                continue;

            // Los triangulos vecinos conservan sus vertices compartidos
            // solamente si la arista que los une NO esta cortada.
            groups.Union(e.A0, e.A1);
            groups.Union(e.B0, e.B1);
        }

        int[] rootToOutput = new int[triangles.Length];
        for (int i = 0; i < rootToOutput.Length; i++)
            rootToOutput[i] = -1;

        var sources = new List<int>();
        int[] outputTriangles = new int[triangles.Length];

        for (int corner = 0; corner < triangles.Length; corner++)
        {
            int root = groups.Find(corner);
            if (rootToOutput[root] < 0)
            {
                rootToOutput[root] = sources.Count;
                sources.Add(triangles[corner]);
            }
            outputTriangles[corner] = rootToOutput[root];
        }

        return new SplitResult(sources.ToArray(), outputTriangles);
    }

    private sealed class DisjointSet
    {
        private readonly int[] parent;
        private readonly byte[] rank;

        public DisjointSet(int count)
        {
            parent = new int[count];
            rank = new byte[count];
            for (int i = 0; i < count; i++) parent[i] = i;
        }

        public int Find(int x)
        {
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]];
                x = parent[x];
            }
            return x;
        }

        public void Union(int a, int b)
        {
            a = Find(a); b = Find(b);
            if (a == b) return;
            if (rank[a] < rank[b]) parent[a] = b;
            else
            {
                parent[b] = a;
                if (rank[a] == rank[b]) rank[a]++;
            }
        }
    }
}
