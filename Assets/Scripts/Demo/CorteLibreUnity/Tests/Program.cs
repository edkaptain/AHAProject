using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CorteLibre;
using P=CorteLibre.PlanarCutMesh.Point;

internal static class Program
{
    static int count;
    static void Assert(bool ok,string message) { if(!ok)throw new Exception(message); count++; }
    static void Cut(PlanarCutMesh m,P a,P b)
    { m.AddIncision(a,b);Assert(m.LastError==null,m.LastError+" segment "+a.X+","+a.Y+" -> "+b.X+","+b.Y);Check(m);CheckPath(m,a,b); }
    static void CheckPath(PlanarCutMesh m,P a,P b)
    {
        P d=b-a;
        // Comprobación independiente: todos los puntos muestreados del trazo
        // dentro del rectángulo deben pertenecer a aristas de incisión.
        for(int i=1;i<100;i++)
        {
            P p=a+d*(i/100.0);
            if(Math.Abs(p.X)>m.Width/2||Math.Abs(p.Y)>m.Height/2)continue;
            bool found=false;
            foreach(ulong edge in m.Cuts)
            {
                P u=m.Points[PlanarCutMesh.KeyA(edge)],v=m.Points[PlanarCutMesh.KeyB(edge)];
                if((PlanarCutMesh.ClosestSegment(p,u,v)-p).Length<m.Epsilon*12){found=true;break;}
            }
            Assert(found,"The requested path has an uncut interval");
        }
    }
    static void Check(PlanarCutMesh m)
    {
        m.Validate();var r=m.BuildRender(.004);Assert(r.Indices.Length==m.Faces.Count*3,"Face count");
        for(int t=0;t<m.Faces.Count;t++)
        {
            int a=r.Indices[3*t],b=r.Indices[3*t+1],c=r.Indices[3*t+2];
            Assert(r.Sources[a]==m.Faces[t].A&&r.Sources[b]==m.Faces[t].B&&r.Sources[c]==m.Faces[t].C,"Source identity");
            Assert(PlanarCutMesh.Cross(r.Positions[b]-r.Positions[a],r.Positions[c]-r.Positions[a])>0,"Inverted opening");
        }
    }
    static void Export(PlanarCutMesh m,string path)
    {
        var r=m.BuildRender(.004);using(var w=new StreamWriter(path))
        {
            w.Write("{\"vertices\":[");for(int i=0;i<r.Positions.Length;i++){if(i>0)w.Write(",");w.Write("["+r.Positions[i].X.ToString("R",CultureInfo.InvariantCulture)+","+r.Positions[i].Y.ToString("R",CultureInfo.InvariantCulture)+"]");}
            w.Write("],\"triangles\":[");for(int i=0;i<r.Indices.Length;i+=3){if(i>0)w.Write(",");w.Write("["+r.Indices[i]+","+r.Indices[i+1]+","+r.Indices[i+2]+"]");}w.Write("]}");
        }
    }
    static void Main()
    {
        var basic=new PlanarCutMesh(.6,.4);Check(basic);
        Cut(basic,new P(-.217,-.131),new P(.233,.137));
        Assert(basic.Points.Count>117,"Did not insert arbitrary vertices");
        int vertices=basic.Points.Count,faces=basic.Faces.Count,cuts=basic.CutCount;
        Cut(basic,new P(-.217,-.131),new P(.233,.137));
        Assert(basic.Points.Count==vertices&&basic.Faces.Count==faces&&basic.CutCount==cuts,"Retrace changed topology");
        Cut(basic,new P(-.231,.129),new P(.227,-.117));
        Console.WriteLine("PASS: arbitrary diagonal, retrace, crossing");
        var straight=new PlanarCutMesh(.6,.4);
        Cut(straight,new P(-.3,0),new P(.3,0));
        Cut(straight,new P(0,-.2),new P(0,.2));
        Cut(straight,new P(-1,.077),new P(1,.077));
        Console.WriteLine("PASS: existing edges, existing vertices, rectangle clipping");
        var same=new PlanarCutMesh(.6,.4,1,1);
        Cut(same,new P(-.2,-.12),new P(-.17,-.1));
        Assert(same.BuildRender(.004).Positions.Length>same.Points.Count,"A short incision must open inside one face");
        Console.WriteLine("PASS: start and end inside one face");
        var loop=new PlanarCutMesh(.6,.4);P previous=new P(.121,.012);
        for(int i=1;i<=48;i++){double a=2*Math.PI*i/48;P next=new P(.121*Math.Cos(a),.012+.092*Math.Sin(a));Cut(loop,previous,next);previous=next;}
        Console.WriteLine("PASS: closed curved stroke");
        var curve=new PlanarCutMesh(.6,.4);previous=new P(-.25,.085*Math.Sin(-.25*16));
        for(int i=1;i<=70;i++){double x=-.25+.5*i/70;P next=new P(x,.085*Math.Sin(x*16));Cut(curve,previous,next);previous=next;}
        Console.WriteLine("PASS: free curved stroke");
        Export(curve,"curve_geometry.json");
        var random=new Random(23);var mixed=new PlanarCutMesh(.6,.4);
        for(int i=0;i<50;i++)
        {
            try { Cut(mixed,new P(random.NextDouble()*.54-.27,random.NextDouble()*.34-.17),new P(random.NextDouble()*.54-.27,random.NextDouble()*.34-.17)); }
            catch(Exception ex) { throw new Exception("Random "+i+": "+ex.Message); }
        }
        Console.WriteLine("PASS: 50 mixed arbitrary segments");
        var rollback=new PlanarCutMesh(.6,.4);rollback.MaxFaces=rollback.Faces.Count;
        Assert(!rollback.AddIncision(new P(-.213,-.111),new P(.219,.139)),"Budget must reject");
        Assert(rollback.Points.Count==117&&rollback.CutCount==0&&rollback.Faces.Count==192,"Rollback lost original");Check(rollback);
        Console.WriteLine("PASS: transactional budget rollback");
        Console.WriteLine("SUCCESS: "+count+" assertions; native C# core executed.");
    }
}
