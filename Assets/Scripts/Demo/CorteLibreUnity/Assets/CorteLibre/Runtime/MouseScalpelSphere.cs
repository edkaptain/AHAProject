using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using P=CorteLibre.PlanarCutMesh.Point;

namespace CorteLibre
{
    [RequireComponent(typeof(SphereCollider))]
    public sealed class MouseScalpelSphere : MonoBehaviour
    {
        public FreeCutMembrane membrane;
        public Camera inputCamera;
        public CorteLibreHud hud;
        [Tooltip("Activado: sigue al mouse SOLO mientras mantienes clic izquierdo. Desactivado: tú mueves la esfera.")]
        public bool followMouseOnClick = true;
        public bool cuttingEnabled=true;
        [Range(0,0.95f)] public float mouseHeightInRadii=.25f;
        [Min(.01f)] public float maximumTravelPerFrame=.6f;
        [Range(8,256)] public int maximumSamplesPerFrame=96;

        private SphereCollider sphere;
        private bool previousButton,armed,previousFollow,hasCenter,hasAnchor,hasLatest;
        private Vector3 previousCenter;
        private P anchor,latest;
        private int generation=-1;
        private FreeCutMembrane previousMembrane;

        private void Awake() { sphere=GetComponent<SphereCollider>();previousFollow=followMouseOnClick; }
        private void LateUpdate()
        {
            if(membrane==null||!membrane.isActiveAndEnabled||membrane.Core==null) { CancelStroke();return; }
            if(previousFollow!=followMouseOnClick||generation!=membrane.Generation||previousMembrane!=membrane)
            {
                CancelStroke();armed=false;previousButton=false;previousFollow=followMouseOnClick;generation=membrane.Generation;previousMembrane=membrane;
            }
            Vector3 s=transform.lossyScale;
            float radius=sphere.radius*Mathf.Max(Mathf.Abs(s.x),Mathf.Abs(s.y),Mathf.Abs(s.z));
            if(radius<=0) { CancelStroke();return; }
            if(followMouseOnClick)
            {
                Vector2 pointer;bool pressed;
                if(!ReadMouse(out pointer,out pressed)) { CancelStroke();previousButton=false;return; }
                bool down=pressed&&!previousButton,up=!pressed&&previousButton;
                previousButton=pressed;
                if(up) { FinishStroke();armed=false;return; }
                if(down) { CancelStroke();armed=hud==null||!hud.BlocksPointer(pointer); }
                if(!pressed||!armed)return;
                if(hud!=null&&hud.BlocksPointer(pointer)) { CancelStroke();return; }
                Camera cam=inputCamera!=null?inputCamera:Camera.main;
                if(cam==null||!cam.pixelRect.Contains(pointer)) { CancelStroke();return; }
                var plane=new Plane(membrane.SurfaceNormal,membrane.transform.position);float distance;
                Ray ray=cam.ScreenPointToRay(pointer);
                if(!plane.Raycast(ray,out distance)) { CancelStroke();return; }
                Vector3 center=ray.GetPoint(distance)+membrane.SurfaceNormal*(radius*mouseHeightInRadii);
                // Incluye Center del SphereCollider, aunque sea diferente de cero.
                transform.position=center-transform.TransformVector(sphere.center);
            }
            if(!cuttingEnabled) { CancelStroke();return; }
            ProcessCenter(transform.TransformPoint(sphere.center),radius);
        }
        private void ProcessCenter(Vector3 center,float radius)
        {
            float scale;if(!membrane.UniformScale(out scale)) { CancelStroke();return; }
            Vector3 from=hasCenter?previousCenter:center;float travel=Vector3.Distance(from,center);
            float step=Mathf.Max(.00001f,Mathf.Min(radius*.4f,membrane.minimumStrokeDistance*scale*.5f));
            int count=Mathf.Max(1,Mathf.CeilToInt(travel/step));
            if(travel>maximumTravelPerFrame||count>maximumSamplesPerFrame)
            { CancelStroke();from=center;count=1; }
            for(int i=1;i<=count;i++)
            {
                P contact;Vector3 sample=Vector3.Lerp(from,center,(float)i/count);
                if(!membrane.TrySphereContact(sample,radius,out contact)) { hasAnchor=false;hasLatest=false;continue; }
                latest=contact;hasLatest=true;
                if(!hasAnchor) { anchor=contact;hasAnchor=true;continue; }
                if((contact-anchor).Length<membrane.minimumStrokeDistance)continue;
                membrane.CutSegment(anchor,contact);
                if(membrane.Core.LastError!=null) { hasAnchor=false;hasLatest=false; }
                else anchor=contact;
            }
            previousCenter=center;hasCenter=true;
        }
        // Método público para gatillo XR o cualquier otro controlador.
        public void SetCutting(bool value)
        { if(!value&&cuttingEnabled)FinishStroke();cuttingEnabled=value; }
        public void FinishStroke()
        {
            if(membrane!=null&&membrane.Core!=null&&hasAnchor&&hasLatest&&
                (latest-anchor).Length>membrane.Core.Epsilon*8)
                membrane.CutSegment(anchor,latest);
            CancelStroke();
        }
        public void CancelStroke() { hasCenter=false;hasAnchor=false;hasLatest=false; }
        private void OnDisable() { CancelStroke();armed=false;previousButton=false; }
        private void OnApplicationFocus(bool focus)
        { if(!focus) { CancelStroke();armed=false;previousButton=false; } }
        private static bool ReadMouse(out Vector2 position,out bool pressed)
        {
#if ENABLE_INPUT_SYSTEM
            if(Mouse.current==null) { position=Vector2.zero;pressed=false;return false; }
            position=Mouse.current.position.ReadValue();pressed=Mouse.current.leftButton.isPressed;return true;
#else
            position=Input.mousePosition;pressed=Input.GetMouseButton(0);return Input.mousePresent;
#endif
        }
    }
}
