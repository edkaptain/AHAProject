using UnityEngine;

namespace CorteLibre
{
    // Panel IMGUI sin dependencias de Canvas, TextMeshPro ni EventSystem.
    public sealed class CorteLibreHud : MonoBehaviour
    {
        public FreeCutMembrane membrane;
        public MouseScalpelSphere sphere;
        private readonly Rect panel = new Rect(12, 12, 350, 215);
        public bool BlocksPointer(Vector2 screenPoint)
        {
            return isActiveAndEnabled && panel.Contains(new Vector2(screenPoint.x, Screen.height - screenPoint.y));
        }
        private void OnGUI()
        {
            if (membrane == null || sphere == null) 
                return;
            GUILayout.BeginArea(panel, GUI.skin.box);
            GUILayout.Label("CORTE LIBRE · Membrana plana");
            GUILayout.Label("Mantén clic izquierdo y arrastra fuera del panel.");
            bool follow = GUILayout.Toggle(sphere.followMouseOnClick, "Seguir mouse con clic");
            if (follow != sphere.followMouseOnClick)
            {
                sphere.CancelStroke();
                sphere.followMouseOnClick = follow;
            }
            bool cutting = GUILayout.Toggle(sphere.cuttingEnabled, "Corte habilitado");
            if (cutting != sphere.cuttingEnabled)
                sphere.SetCutting(cutting);
            membrane.showWireframe = GUILayout.Toggle(membrane.showWireframe, "Mostrar triángulos");

            if (GUILayout.Button("Restaurar membrana"))
            {
                sphere.CancelStroke(); membrane.ResetMembrane();
            }
            if (membrane.Core != null)
                GUILayout.Label(membrane.Core.Points.Count + " vértices materiales · " + membrane.Core.Faces.Count + " triángulos");
            GUILayout.Label(membrane.Status);
            GUILayout.EndArea();
        }
    }
}
