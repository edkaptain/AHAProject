using System;
using System.Collections.Generic;

namespace CorteLibre
{
    // Núcleo geométrico independiente de Unity. Todas las posiciones son
    // coordenadas materiales 2D; RenderData contiene la superficie abierta.
    public sealed class PlanarCutMesh
    {
        public struct Point
        {
            public double X, Y;
            public Point(double x, double y) { X = x; Y = y; }
            public static Point operator +(Point a, Point b) { return new Point(a.X+b.X,a.Y+b.Y); }
            public static Point operator -(Point a, Point b) { return new Point(a.X-b.X,a.Y-b.Y); }
            public static Point operator *(Point a, double s) { return new Point(a.X*s,a.Y*s); }
            public double Length { get { return Math.Sqrt(X*X+Y*Y); } }
        }
        public struct Face
        {
            public int A, B, C;
            public Face(int a,int b,int c) { A=a; B=b; C=c; }
            public int this[int i] { get { return i==0?A:(i==1?B:C); } }
        }
        public sealed class RenderData
        {
            public Point[] Positions;
            public int[] Sources, Indices;
        }
        private sealed class Edge
        {
            public int A,B,A0,B0,A1=-1,B1=-1;
            public Edge(int a,int b,int ca,int cb) { A=a; B=b; A0=ca; B0=cb; }
        }
        private List<Point> points = new List<Point>();
        private List<Face> faces = new List<Face>();
        private HashSet<ulong> cuts = new HashSet<ulong>();
        public IReadOnlyList<Point> Points { get { return points; } }
        public IReadOnlyList<Face> Faces { get { return faces; } }
        public IEnumerable<ulong> Cuts { get { return cuts; } }
        public int CutCount { get { return cuts.Count; } }
        public int MaxFaces = 16000;
        public string LastError { get; private set; }
        public readonly double Width, Height, Epsilon;

        public PlanarCutMesh(double width,double height,int columns=12,int rows=8)
        {
            if(width<=0 || height<=0 || columns<1 || rows<1)
                throw new ArgumentException("Dimensiones inválidas.");
            Width=width; Height=height; Epsilon=Math.Max(width,height)*1e-7;
            for(int y=0;y<=rows;y++) for(int x=0;x<=columns;x++)
                points.Add(new Point(width*x/columns-width/2,height*y/rows-height/2));
            for(int y=0;y<rows;y++) for(int x=0;x<columns;x++)
            {
                int a=y*(columns+1)+x,b=a+1,c=a+columns+1,d=c+1;
                // Orientación CCW en 2D. Unity invertirá el orden para +Y.
                faces.Add(new Face(a,b,c)); faces.Add(new Face(b,d,c));
            }
        }
        public static double Cross(Point a,Point b) { return a.X*b.Y-a.Y*b.X; }
        public static double Dot(Point a,Point b) { return a.X*b.X+a.Y*b.Y; }
        public static ulong Key(int a,int b) { return ((ulong)(uint)Math.Min(a,b)<<32)|(uint)Math.Max(a,b); }
        public static int KeyA(ulong k) { return (int)(k>>32); }
        public static int KeyB(ulong k) { return (int)(k&0xffffffffUL); }
        private static double Clamp(double x,double a,double b) { return Math.Max(a,Math.Min(b,x)); }

        // La operación es transaccional: ante un caso degenerado o un límite,
        // recupera la geometría anterior en vez de dejar una malla incompleta.
        public bool AddIncision(Point start,Point end)
        {
            LastError=null;
            if(!Finite(start)||!Finite(end)) { LastError="Punto no finito."; return false; }
            if(!ClipRectangle(ref start,ref end)||(end-start).Length<Epsilon*8) return false;
            int oldPointCount=points.Count;
            List<Face> oldFaces=new List<Face>(faces);
            HashSet<ulong> oldCuts=new HashSet<ulong>(cuts);
            try
            {
                int first=InsertPoint(start),last=InsertPoint(end);
                if(first==last) return false;
                start=points[first]; end=points[last];
                Point direction=end-start;
                double length=direction.Length, squared=Dot(direction,direction);
                var output=new List<Face>(faces.Count+16);
                var intersections=new Dictionary<ulong,int>();
                var newCuts=new HashSet<ulong>();
                foreach(Face face in faces)
                {
                    int[] ids={face.A,face.B,face.C};
                    double[] distances=new double[3];
                    var linePoints=new List<int>(2);
                    bool positive=false,negative=false;
                    for(int i=0;i<3;i++)
                    {
                        double d=Cross(direction,points[ids[i]]-start)/length;
                        if(Math.Abs(d)<=Epsilon) d=0;
                        distances[i]=d; positive|=d>0; negative|=d<0;
                        if(d==0) AddUnique(linePoints,ids[i]);
                    }
                    // Primero comprobar el intervalo del cruce sin insertar
                    // vértices fuera del segmento finito del bisturí.
                    double minT=double.PositiveInfinity,maxT=double.NegativeInfinity;
                    foreach(int id in linePoints)
                    {
                        double t=Dot(points[id]-start,direction)/squared;
                        minT=Math.Min(minT,t); maxT=Math.Max(maxT,t);
                    }
                    for(int i=0;i<3;i++)
                    {
                        int j=(i+1)%3;
                        if(distances[i]*distances[j]<0)
                        {
                            double s=distances[i]/(distances[i]-distances[j]);
                            Point p=points[ids[i]]+(points[ids[j]]-points[ids[i]])*s;
                            double t=Dot(p-start,direction)/squared;
                            minT=Math.Min(minT,t); maxT=Math.Max(maxT,t);
                        }
                    }
                    if(double.IsInfinity(minT) || maxT<=minT || maxT<=0 || minT>=1)
                    { output.Add(face); continue; }
                    if(minT < -Epsilon*4/length || maxT > 1+Epsilon*4/length)
                        throw new InvalidOperationException("El extremo no quedó insertado en la triangulación.");

                    if(positive&&negative)
                    {
                        var left=new List<int>(4); var right=new List<int>(4);
                        for(int i=0;i<3;i++)
                        {
                            int j=(i+1)%3;
                            if(distances[i]>=0) AddUnique(left,ids[i]);
                            if(distances[i]<=0) AddUnique(right,ids[i]);
                            if(distances[i]*distances[j]<0)
                            {
                                ulong edge=Key(ids[i],ids[j]); int v;
                                if(!intersections.TryGetValue(edge,out v))
                                {
                                    double s=distances[i]/(distances[i]-distances[j]);
                                    v=points.Count;
                                    points.Add(points[ids[i]]+(points[ids[j]]-points[ids[i]])*s);
                                    intersections.Add(edge,v);
                                }
                                AddUnique(left,v); AddUnique(right,v); AddUnique(linePoints,v);
                            }
                        }
                        TriangulatePolygon(left,output); TriangulatePolygon(right,output);
                    }
                    else output.Add(face); // Recorrido coincidente con una arista.
                    if(linePoints.Count==2) newCuts.Add(Key(linePoints[0],linePoints[1]));
                }
                faces=output;
                foreach(var entry in intersections) InheritCut(entry.Key,entry.Value);
                cuts.UnionWith(newCuts);
                // Una incisión aislada de una sola arista necesita un punto
                // intermedio: sus dos puntas permanecen unidas al tejido, pero
                // el vértice central sí puede separarse en dos labios.
                var degree=new Dictionary<int,int>();
                foreach(ulong cut in cuts)
                {
                    int a=KeyA(cut),b=KeyB(cut),value;
                    degree.TryGetValue(a,out value);degree[a]=value+1;
                    degree.TryGetValue(b,out value);degree[b]=value+1;
                }
                var isolated=new List<ulong>();
                foreach(ulong cut in cuts)
                    if(degree[KeyA(cut)]==1&&degree[KeyB(cut)]==1&&
                       !OnSameBoundary(points[KeyA(cut)],points[KeyB(cut)]))isolated.Add(cut);
                foreach(ulong cut in isolated)
                    InsertPoint((points[KeyA(cut)]+points[KeyB(cut)])*.5);
                if(faces.Count>MaxFaces) throw new InvalidOperationException("Límite de triángulos alcanzado; reinicia la membrana.");
                Validate();
                return newCuts.Count>0 || faces.Count!=oldFaces.Count;
            }
            catch(InvalidOperationException ex)
            {
                points.RemoveRange(oldPointCount,points.Count-oldPointCount);
                faces=oldFaces; cuts=oldCuts; LastError=ex.Message;
                return false;
            }
        }
        private static bool Finite(Point p)
        { return !double.IsNaN(p.X)&&!double.IsInfinity(p.X)&&!double.IsNaN(p.Y)&&!double.IsInfinity(p.Y); }
        private static void AddUnique(List<int> list,int id) { if(!list.Contains(id)) list.Add(id); }
        private void TriangulatePolygon(List<int> polygon,List<Face> output)
        { for(int i=1;i<polygon.Count-1;i++) AddFace(output,polygon[0],polygon[i],polygon[i+1]); }
        private void AddFace(List<Face> output,int a,int b,int c)
        {
            double area=Cross(points[b]-points[a],points[c]-points[a]);
            if(a==b||a==c||b==c||Math.Abs(area)<=Epsilon*Epsilon)
                throw new InvalidOperationException("Triángulo degenerado; separa un poco el siguiente trazo.");
            output.Add(area>0?new Face(a,b,c):new Face(a,c,b));
        }
        private int InsertPoint(Point p)
        {
            int nearest=-1; double distance=Epsilon;
            for(int i=0;i<points.Count;i++)
            { double d=(p-points[i]).Length; if(d<=distance) { nearest=i; distance=d; } }
            if(nearest>=0) return nearest;
            Dictionary<ulong,Edge> edges=BuildEdges();
            foreach(var item in edges)
            {
                Edge edge=item.Value; Point a=points[edge.A],b=points[edge.B];
                Point q=ClosestSegment(p,a,b);
                if((q-p).Length>Epsilon) continue;
                int v=points.Count; points.Add(q);
                var output=new List<Face>(faces.Count+2);
                foreach(Face f in faces)
                {
                    bool hasA=f.A==edge.A||f.B==edge.A||f.C==edge.A;
                    bool hasB=f.A==edge.B||f.B==edge.B||f.C==edge.B;
                    if(!hasA||!hasB) { output.Add(f); continue; }
                    int third=f.A!=edge.A&&f.A!=edge.B?f.A:(f.B!=edge.A&&f.B!=edge.B?f.B:f.C);
                    AddFace(output,edge.A,v,third); AddFace(output,v,edge.B,third);
                }
                faces=output; InheritCut(item.Key,v); return v;
            }
            for(int i=0;i<faces.Count;i++)
            {
                Face f=faces[i];
                if(!Inside(p,points[f.A],points[f.B],points[f.C])) continue;
                int v=points.Count; points.Add(p);
                faces.RemoveAt(i);
                AddFace(faces,f.A,f.B,v); AddFace(faces,f.B,f.C,v); AddFace(faces,f.C,f.A,v);
                return v;
            }
            throw new InvalidOperationException("No se encontró la cara para insertar el punto.");
        }
        private void InheritCut(ulong original,int inserted)
        {
            if(!cuts.Remove(original)) return;
            cuts.Add(Key(KeyA(original),inserted)); cuts.Add(Key(inserted,KeyB(original)));
        }
        private Dictionary<ulong,Edge> BuildEdges()
        {
            var result=new Dictionary<ulong,Edge>();
            for(int t=0;t<faces.Count;t++) for(int k=0;k<3;k++)
            {
                int j=(k+1)%3,a=faces[t][k],b=faces[t][j],ca=t*3+k,cb=t*3+j;
                if(a>b) { int s=a;a=b;b=s; s=ca;ca=cb;cb=s; }
                ulong key=Key(a,b); Edge edge;
                if(!result.TryGetValue(key,out edge)) result.Add(key,new Edge(a,b,ca,cb));
                else
                {
                    if(edge.A1>=0) throw new InvalidOperationException("Arista no manifold.");
                    edge.A1=ca; edge.B1=cb;
                }
            }
            return result;
        }
        public void Validate()
        {
            Dictionary<ulong,Edge> edges=BuildEdges(); double area=0;
            foreach(Face f in faces)
            {
                double a=Cross(points[f.B]-points[f.A],points[f.C]-points[f.A]);
                if(a<=Epsilon*Epsilon) throw new InvalidOperationException("Cara invertida o degenerada.");
                area+=a/2;
            }
            if(Math.Abs(area-Width*Height)>Width*Height*1e-6)
                throw new InvalidOperationException("La operación no conservó el área.");
            foreach(ulong cut in cuts)
                if(!edges.ContainsKey(cut)) throw new InvalidOperationException("Referencia a una incisión perdida.");
            foreach(Edge e in edges.Values)
                if(e.A1<0&&!OnSameBoundary(points[e.A],points[e.B]))
                    throw new InvalidOperationException("Grieta topológica: "+e.A+" "+e.B+" ("+points[e.A].X+","+points[e.A].Y+") ("+points[e.B].X+","+points[e.B].Y+").");
        }
        private bool OnSameBoundary(Point a,Point b)
        {
            double e=Epsilon*8;
            return (Math.Abs(a.X+Width/2)<e&&Math.Abs(b.X+Width/2)<e)||
                   (Math.Abs(a.X-Width/2)<e&&Math.Abs(b.X-Width/2)<e)||
                   (Math.Abs(a.Y+Height/2)<e&&Math.Abs(b.Y+Height/2)<e)||
                   (Math.Abs(a.Y-Height/2)<e&&Math.Abs(b.Y-Height/2)<e);
        }
        // Separa grupos alrededor de cada vértice, conservando unidas las
        // puntas internas. Los IDs materiales siguen estables para remallar.
        public RenderData BuildRender(double opening)
        {
            var edges=BuildEdges(); var union=new UnionFind(faces.Count*3);
            foreach(var entry in edges)
            {
                Edge e=entry.Value;
                if(e.A1>=0&&!cuts.Contains(entry.Key)) { union.Join(e.A0,e.A1); union.Join(e.B0,e.B1); }
            }
            var map=new Dictionary<int,int>();var sources=new List<int>();int[] indices=new int[faces.Count*3];
            for(int c=0;c<indices.Length;c++)
            {
                int root=union.Find(c),id;
                if(!map.TryGetValue(root,out id)) { id=sources.Count;map.Add(root,id);sources.Add(faces[c/3][c%3]); }
                indices[c]=id;
            }
            Point[] directions=new Point[sources.Count]; double[] limit=new double[sources.Count];
            for(int i=0;i<limit.Length;i++) limit[i]=Math.Max(0,opening)*.5;
            foreach(ulong key in cuts)
            {
                Edge e=edges[key]; if(e.A1<0) continue;
                Point d=points[e.B]-points[e.A]; Point n=new Point(-d.Y,d.X)*(1/d.Length);
                Face f=faces[e.A0/3]; Point center=(points[f.A]+points[f.B]+points[f.C])*(1.0/3);
                if(Dot(n,center-points[e.A])<0) n=n*(-1);
                AddOpening(indices[e.A0],indices[e.A1],n,directions);
                AddOpening(indices[e.B0],indices[e.B1],n,directions);
            }
            // Límite local conservador por altura de cada triángulo. Evita
            // que abrir los bordes invierta caras delgadas del remallado.
            for(int t=0;t<faces.Count;t++)
            {
                Face f=faces[t]; Point a=points[f.A],b=points[f.B],c=points[f.C];
                double longest=Math.Max((b-a).Length,Math.Max((c-b).Length,(a-c).Length));
                double maxMove=Cross(b-a,c-a)/longest*.12;
                for(int k=0;k<3;k++) limit[indices[t*3+k]]=Math.Min(limit[indices[t*3+k]],maxMove);
            }
            Point[] positions=new Point[sources.Count];
            for(int i=0;i<positions.Length;i++)
            {
                double length=directions[i].Length;
                positions[i]=points[sources[i]]+(length>1e-12?directions[i]*(limit[i]/length):new Point());
            }
            return new RenderData {Positions=positions,Sources=sources.ToArray(),Indices=indices};
        }
        private static void AddOpening(int a,int b,Point n,Point[] output)
        { if(a!=b) { output[a]=output[a]+n; output[b]=output[b]-n; } }
        public static bool Inside(Point p,Point a,Point b,Point c)
        { return Cross(b-a,p-a)>=0&&Cross(c-b,p-b)>=0&&Cross(a-c,p-c)>=0; }
        public static Point ClosestSegment(Point p,Point a,Point b)
        {
            Point d=b-a;double square=Dot(d,d);
            return square<=1e-30?a:a+d*Clamp(Dot(p-a,d)/square,0,1);
        }
        public static Point ClosestTriangle(Point p,Point a,Point b,Point c)
        {
            if(Inside(p,a,b,c)) return p;
            Point q=ClosestSegment(p,a,b),r=ClosestSegment(p,b,c),s=ClosestSegment(p,c,a);
            if((r-p).Length<(q-p).Length) q=r;
            if((s-p).Length<(q-p).Length) q=s;
            return q;
        }
        public static Point BarycentricMaterial(Point q,Point a,Point b,Point c,Point ma,Point mb,Point mc)
        {
            double total=Cross(b-a,c-a);
            double u=Cross(b-q,c-q)/total,v=Cross(c-q,a-q)/total;
            return ma*u+mb*v+mc*(1-u-v);
        }
        private bool ClipRectangle(ref Point a,ref Point b)
        {
            Point d=b-a;double lo=0,hi=1;
            if(!Clip(-d.X,a.X+Width/2,ref lo,ref hi)||!Clip(d.X,Width/2-a.X,ref lo,ref hi)||
               !Clip(-d.Y,a.Y+Height/2,ref lo,ref hi)||!Clip(d.Y,Height/2-a.Y,ref lo,ref hi)) return false;
            b=a+d*hi;a=a+d*lo;return true;
        }
        private static bool Clip(double p,double q,ref double lo,ref double hi)
        {
            if(Math.Abs(p)<1e-30) return q>=0;
            double t=q/p;if(p<0)lo=Math.Max(lo,t);else hi=Math.Min(hi,t);return lo<=hi;
        }
        private sealed class UnionFind
        {
            private readonly int[] parent;private readonly byte[] rank;
            public UnionFind(int n) { parent=new int[n];rank=new byte[n];for(int i=0;i<n;i++)parent[i]=i; }
            public int Find(int x) { while(parent[x]!=x){parent[x]=parent[parent[x]];x=parent[x];}return x; }
            public void Join(int a,int b)
            { a=Find(a);b=Find(b);if(a==b)return;if(rank[a]<rank[b])parent[a]=b;else{parent[b]=a;if(rank[a]==rank[b])rank[a]++;} }
        }
    }
}
