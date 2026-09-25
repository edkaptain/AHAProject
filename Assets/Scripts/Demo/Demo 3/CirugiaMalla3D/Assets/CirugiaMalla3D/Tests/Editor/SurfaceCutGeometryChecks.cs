using System;
using System.Collections.Generic;
using G = Cirugia3D.SurfaceCutGeometry;
using V = Cirugia3D.SurfaceCutGeometry.V;

namespace Cirugia3D.Checks
{
    // Sin NUnit ni paquetes adicionales. Tambien puede compilarse junto al
    // nucleo fuera de Unity definiendo SURGERY_STANDALONE.
    public static class SurfaceCutGeometryChecks
    {
#if UNITY_EDITOR
        [UnityEditor.MenuItem("Tools/Cirugia 3D/Ejecutar comprobaciones geometricas")]
        public static void RunInEditor()
        {
            try { RunAll(message => UnityEngine.Debug.Log(message)); }
            catch (Exception ex) { UnityEngine.Debug.LogException(ex); }
        }
#endif
#if SURGERY_STANDALONE
        public static int Main()
        {
            try { RunAll(Console.WriteLine); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
#endif
        public static void RunAll(Action<string> report)
        {
            Action[] cases = { InteriorCut, AcrossFacesAndRepeat, CrossingCuts, FoldedSurface,
                SeamWelding, TransactionRollback, InvalidContactRollback, OpeningDoesNotInvert,
                ClosedSurface, DeterministicStrokes };
            foreach (Action test in cases) { test(); report("PASS " + test.Method.Name); }
            report("PASS: " + cases.Length + " comprobaciones geometricas.");
        }
        private static G Quad(bool duplicatedSeam = false)
        {
            V[] p = { new V(0, 0, 0), new V(1, 0, 0), new V(1, 1, 0), new V(0, 1, 0), new V(0, 0, 0), new V(1, 1, 0) };
            return new G(p, duplicatedSeam ? new[] { 0, 1, 2, 4, 5, 3 } : new[] { 0, 1, 2, 0, 2, 3 }, 1e-7);
        }
        private static void Assert(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("CHECK FAILED: " + message); }
        private static void Apply(G g, IList<G.Segment> path)
        {
            Assert(g.CutBatch(path), g.LastError ?? "El corte no cambio la malla."); g.Validate();
        }
        private static List<G.Segment> PlanarPath(double ax, double ay, double bx, double by)
        {
            V a = new V(ax, ay, 0), b = new V(bx, by, 0);
            double da = ax - ay, db = bx - by;
            var list = new List<G.Segment>();
            if (da * db < 0)
            {
                V middle = a + (b - a) * (da / (da - db));
                list.Add(new G.Segment(da >= 0 ? 0 : 1, a, middle));
                list.Add(new G.Segment(db >= 0 ? 0 : 1, middle, b));
            }
            else list.Add(new G.Segment(da + db >= 0 ? 0 : 1, a, b));
            return list;
        }
        private static double Area(G g)
        {
            double sum = 0;
            foreach (G.Face f in g.Faces) sum += G.Cross(g.Points[f.B] - g.Points[f.A], g.Points[f.C] - g.Points[f.A]).Length * .5;
            return sum;
        }
        private static void InteriorCut()
        {
            G g = Quad(); Apply(g, PlanarPath(.6, .2, .85, .25));
            Assert(Math.Abs(Area(g) - 1) < 1e-10, "Se perdio area.");
            G.Display d = g.BuildDisplay(.02); int separated = 0;
            foreach (bool value in d.Separated) if (value) separated++;
            Assert(separated > 0 && g.CutCount >= 2, "La incision corta necesita un punto central que pueda abrirse.");
        }
        private static void AcrossFacesAndRepeat()
        {
            G g = Quad(); var path = PlanarPath(.15, .5, .85, .5); Apply(g, path);
            int faces = g.Faces.Count, points = g.Points.Count, cuts = g.CutCount;
            g.CutBatch(path);
            Assert(g.LastError == null, g.LastError);
            Assert(g.Faces.Count == faces && g.Points.Count == points && g.CutCount == cuts, "Repetir un corte genero geometria extra.");
            Assert(Math.Abs(Area(g) - 1) < 1e-10, "Se perdio area entre caras.");
        }
        private static void CrossingCuts()
        {
            G g = Quad(); Apply(g, PlanarPath(.1, .5, .9, .5)); Apply(g, PlanarPath(.45, .1, .45, .9));
            Assert(Math.Abs(Area(g) - 1) < 1e-10, "Cruzar incisiones elimino superficie."); g.BuildDisplay(.03);
        }
        private static void FoldedSurface()
        {
            V[] p = { new V(0, 0, 0), new V(1, 0, 0), new V(0, 1, 0), new V(0, 0, 1) };
            G g = new G(p, new[] { 0, 1, 2, 1, 0, 3 }, 1e-7);
            V edge = new V(.4, 0, 0);
            Apply(g, new[] { new G.Segment(0, new V(.3, .3, 0), edge), new G.Segment(1, edge, new V(.3, 0, .3)) });
            Assert(Math.Abs(Area(g) - 1) < 1e-10, "El corte sobre el pliegue perdio area."); g.BuildDisplay(.02);
        }
        private static void SeamWelding()
        {
            G g = Quad(true); Assert(g.Points.Count == 4, "Las posiciones de la costura no se unieron.");
            Apply(g, PlanarPath(.2, .6, .8, .6)); g.BuildDisplay(.04);
        }
        private static void TransactionRollback()
        {
            G g = Quad(); g.MaxFaces = 2;
            bool changed = g.CutBatch(PlanarPath(.15, .5, .85, .5));
            Assert(!changed && g.LastError != null, "No se respeto el limite.");
            Assert(g.Points.Count == 4 && g.Faces.Count == 2 && g.CutCount == 0, "La operacion no se revirtio por completo."); g.Validate();
        }
        private static void InvalidContactRollback()
        {
            G g = Quad(); var path = PlanarPath(.6, .2, .85, .25);
            path.Add(new G.Segment(0, new V(100, 100, 0), new V(101, 100, 0)));
            Assert(!g.CutBatch(path) && g.LastError != null, "Se acepto un contacto fuera del modelo.");
            Assert(g.Points.Count == 4 && g.Faces.Count == 2 && g.CutCount == 0, "El lote quedo aplicado parcialmente.");
        }
        private static void OpeningDoesNotInvert()
        {
            G g = Quad(); Apply(g, PlanarPath(.1, .6, .9, .6));
            G.Display d = g.BuildDisplay(100);
            for (int i = 0; i < g.Faces.Count; i++)
                Assert(G.Dot(G.Cross(d.Positions[i * 3 + 1] - d.Positions[i * 3], d.Positions[i * 3 + 2] - d.Positions[i * 3]), g.Normal(g.Faces[i].Source)) > 0, "Se invirtio una cara al abrir.");
        }
        private static void ClosedSurface()
        {
            V[] p = { new V(1, 1, 1), new V(-1, -1, 1), new V(-1, 1, -1), new V(1, -1, -1) };
            int[] triangles = { 0, 1, 2, 0, 3, 1, 0, 2, 3, 1, 3, 2 };
            // Asegurar normales exteriores.
            for (int i = 0; i < triangles.Length; i += 3)
            {
                V a = p[triangles[i]], b = p[triangles[i + 1]], c = p[triangles[i + 2]];
                if (G.Dot(G.Cross(b - a, c - a), a + b + c) < 0)
                { int swap = triangles[i + 1]; triangles[i + 1] = triangles[i + 2]; triangles[i + 2] = swap; }
            }
            G g = new G(p, triangles, 1e-7); double area = Area(g);
            G.Face f0 = g.Sources[0], f1 = g.Sources[1];
            V center0 = (g.Points[f0.A] + g.Points[f0.B] + g.Points[f0.C]) * (1.0 / 3);
            V center1 = (g.Points[f1.A] + g.Points[f1.B] + g.Points[f1.C]) * (1.0 / 3);
            V crossing = (p[0] + p[1]) * .5;
            Apply(g, new[] { new G.Segment(0, center0, crossing), new G.Segment(1, crossing, center1) });
            foreach (G.Edge e in g.BuildEdges().Values) Assert(e.F1 >= 0, "La triangulacion material cerrada adquirio un agujero.");
            Assert(Math.Abs(Area(g) - area) < 1e-9, "Se perdio area de la superficie cerrada."); g.BuildDisplay(.04);
        }
        private static void DeterministicStrokes()
        {
            G g = Quad(); var random = new Random(1537); int successes = 0;
            for (int i = 0; i < 24; i++)
            {
                double x = .1 + random.NextDouble() * .8, y = .1 + random.NextDouble() * .8;
                double bx = .1 + random.NextDouble() * .8, by = .1 + random.NextDouble() * .8;
                if (g.CutBatch(PlanarPath(x, y, bx, by))) successes++;
                g.Validate(); g.BuildDisplay(.03);
            }
            Assert(successes >= 20, "Demasiados trazos se rechazaron: " + successes);
            Assert(Math.Abs(Area(g) - 1) < 1e-8, "Multiples trazos perdieron superficie.");
        }
    }
}
