using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CorteLibre.Editor
{
    public static class CorteLibreDemoBuilder
    {
        [MenuItem("Tools/Corte libre/Crear escena de prueba")]
        public static void CreateDemo()
        {
            if(Application.isPlaying)
            { Debug.LogWarning("Sal de Play antes de crear la escena.");return; }
            Shader shader=Shader.Find("CorteLibre/UnlitSurface");
            if(shader==null)
            { Debug.LogError("Importa también Shaders/CorteLibreSurface.shader y espera a que Unity termine de compilar.");return; }
            // Flujo estándar de Unity para no perder una escena con cambios.
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            const string folder="Assets/CorteLibre/Generated";EnsureFolder(folder);
            Material tissue=CreateMaterial(shader,new Color(.82f,.47f,.51f),folder+"/Tejido.mat");
            Material wire=CreateMaterial(shader,new Color(.36f,.18f,.24f),folder+"/Aristas.mat");
            Material pointer=CreateMaterial(shader,new Color(.25f,.95f,.76f),folder+"/Esfera.mat");
            Material backing=CreateMaterial(shader,new Color(.08f,.10f,.15f),folder+"/Base.mat");

            var cameraObject=new GameObject("Main Camera");cameraObject.tag="MainCamera";
            Camera camera=cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.position=new Vector3(-.08f,.8f,0);
            cameraObject.transform.rotation=Quaternion.Euler(90,0,0);
            camera.orthographic=true;camera.orthographicSize=.28f;camera.nearClipPlane=.01f;camera.farClipPlane=10;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.055f,.066f,.095f);

            var membraneObject=new GameObject("Membrana - corte libre");
            FreeCutMembrane membrane=membraneObject.AddComponent<FreeCutMembrane>();
            membraneObject.GetComponent<MeshRenderer>().sharedMaterial=tissue;
            membrane.wireMaterial=wire;
            // Vista previa visible antes de Play. El componente crea su propia
            // instancia de Mesh al ejecutar; este asset permanece intacto.
            var preview=new Mesh {name="Membrana inicial"};
            preview.vertices=new [] {new Vector3(-.3f,0,-.2f),new Vector3(.3f,0,-.2f),new Vector3(-.3f,0,.2f),new Vector3(.3f,0,.2f)};
            preview.triangles=new [] {0,2,1,1,2,3};preview.RecalculateNormals();preview.RecalculateBounds();
            AssetDatabase.CreateAsset(preview,AssetDatabase.GenerateUniqueAssetPath(folder+"/MembranaInicial.asset"));
            membraneObject.GetComponent<MeshFilter>().sharedMesh=preview;

            GameObject board=GameObject.CreatePrimitive(PrimitiveType.Cube);board.name="Base bajo el tejido";
            board.transform.position=new Vector3(0,-.005f,0);board.transform.localScale=new Vector3(.64f,.008f,.44f);
            board.GetComponent<MeshRenderer>().sharedMaterial=backing;Object.DestroyImmediate(board.GetComponent<Collider>());
            GameObject ball=GameObject.CreatePrimitive(PrimitiveType.Sphere);ball.name="Esfera bisturi - control mouse";
            ball.transform.localScale=Vector3.one*.012f;ball.transform.position=new Vector3(-.23f,.0015f,0);
            ball.GetComponent<MeshRenderer>().sharedMaterial=pointer;ball.GetComponent<SphereCollider>().isTrigger=true;
            MouseScalpelSphere scalpel=ball.AddComponent<MouseScalpelSphere>();
            scalpel.membrane=membrane;scalpel.inputCamera=camera;scalpel.followMouseOnClick=true;

            var hudObject=new GameObject("Controles de prueba");CorteLibreHud hud=hudObject.AddComponent<CorteLibreHud>();
            hud.membrane=membrane;hud.sphere=scalpel;scalpel.hud=hud;
            var lightObject=new GameObject("Luz de referencia");Light light=lightObject.AddComponent<Light>();
            light.type=LightType.Directional;light.intensity=1;lightObject.transform.rotation=Quaternion.Euler(45,-25,0);
            AssetDatabase.SaveAssets();
            string path=AssetDatabase.GenerateUniqueAssetPath(folder+"/CorteLibreDemo.unity");
            EditorSceneManager.SaveScene(scene,path);Selection.activeGameObject=ball;
            Debug.Log("Escena creada: "+path+". Pulsa Play y arrastra con clic izquierdo sobre la membrana.");
        }
        private static Material CreateMaterial(Shader shader,Color color,string path)
        {
            var material=new Material(shader);material.SetColor("_Color",color);
            AssetDatabase.CreateAsset(material,AssetDatabase.GenerateUniqueAssetPath(path));return material;
        }
        private static void EnsureFolder(string path)
        {
            string[] parts=path.Split('/');string current=parts[0];
            for(int i=1;i<parts.Length;i++)
            { string next=current+"/"+parts[i];if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(current,parts[i]);current=next; }
        }
    }
}
