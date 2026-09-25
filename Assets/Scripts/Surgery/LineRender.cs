using UnityEngine;
using UnityEngine.Events;

public class CuttingLineRender : MonoBehaviour
{
    public GameObject parentVertices;
    public GameObject[] vertices;
    public LineRenderer lineRenderer;
    public LineRenderer lineRender_OK;
    public GameObject greenVessel;

    public Material target, transparent, green;

    public int current;
    public UnityEvent events;

    private bool operationCompleted;

    private void Update()
    {
        if(operationCompleted == false)
        {
            greenVessel.transform.localScale = gameObject.transform.root.localScale * 100;
        }
    }

    [ContextMenu("Setup vertices points")]
    public void SetUpPoints()
    {
        if (lineRenderer == null || parentVertices == null)
            return;

        int count = parentVertices.transform.childCount;

        vertices = new GameObject[count];

        for (int i = 0; i < count; i++)
        {
            GameObject sphere =
                parentVertices.transform.GetChild(i).gameObject;

            sphere.GetComponent<SphereScript>().cuttingLine = this;

            vertices[i] = sphere;

            SphereScript sphereScript =
                sphere.GetComponent<SphereScript>();

            if (sphereScript != null)
            {
                sphereScript.Configure(this, i);
            }

            // Al principio ocultamos todas las esferas
            sphere.SetActive(false);
        }

        lineRenderer.positionCount = 0;

        ShowPath();
    }

    public void StartProcess()
    {
        if (vertices == null || vertices.Length == 0)
        {
            Debug.LogWarning("Primero debes ejecutar SetUpPoints.");
            return;
        }

        current = 0;
        ShowPath();
    }

    public void ShowPath()
    {
        if (current >= vertices.Length)
        {
            FinishProcess();
            return;
        }

        // Activa la esfera actual
        vertices[current].SetActive(true);
        vertices[current].GetComponent<MeshRenderer>().material = target;

        SphereScript currentSphere =
            vertices[current].GetComponent<SphereScript>();

        if (currentSphere != null)
        {
            currentSphere.isAllowed = true;
        }

        int next = current + 1;
        int previous = current - 1;

        // Si existe una esfera siguiente
        if (next < vertices.Length)
        {
            //vertices[next].SetActive(true);

            //SphereScript nextSphere =
            //    vertices[next].GetComponent<SphereScript>();

            //nextSphere.gameObject.GetComponent<MeshRenderer>().material = transparent;

            //if (nextSphere != null)
            //{
            //    nextSphere.isAllowed = false;
            //}

            // Dibuja la línea entre la esfera actual y la siguiente
            lineRenderer.positionCount = 2;

            lineRenderer.SetPosition(
                0, lineRenderer.transform.InverseTransformPoint(
                vertices[previous].transform.position)
            );

            lineRenderer.SetPosition(
                1, lineRenderer.transform.InverseTransformPoint(
                vertices[current].transform.position)
            );
        }
        else
        {
            // Solo queda la última esfera
            lineRenderer.positionCount = 1;

            lineRenderer.SetPosition(
                0,
                vertices[current].transform.position
            );
        }
    }

    public void SphereTouched(int sphereIndex)
    {
        // Solamente acepta la esfera correspondiente
        if (sphereIndex != current)
        {
            Debug.Log("Esa esfera todavía no corresponde.");
            return;
        }

        // Desactiva el punto anterior
        //vertices[current].SetActive(false);
        vertices[current].GetComponent<MeshRenderer>().material = green;
        vertices[current].GetComponent<SphereScript>().enabled = false;

        ControllerVibration.Instance.Vibrate(0.1f);

        // Avanza a la siguiente posición
        current++;

        // Testing

        lineRender_OK.positionCount = current;

        for (int i = 0; i < current; i++)
        {
            lineRender_OK.SetPosition(i, lineRender_OK.transform.InverseTransformPoint(vertices[i].transform.position));
        }

        ShowPath();
    }

    private void FinishProcess()
    {
        events?.Invoke();
        operationCompleted = true;
        lineRenderer.positionCount = 0;
        lineRender_OK.positionCount = 0;

        Debug.Log("Secuencia terminada.");
    }

    [ContextMenu("Show all the content")]
    public void ShowEntirePath()
    {
        lineRenderer.positionCount = vertices.Length;

        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i].SetActive(true);
            lineRenderer.SetPosition(i, vertices[i].transform.position);
        }
    }
}