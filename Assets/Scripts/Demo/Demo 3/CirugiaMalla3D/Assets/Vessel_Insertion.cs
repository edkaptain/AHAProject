using Cirugia3D;
using UnityEngine;

public class Vessel_Insertion : MonoBehaviour
{
    [Header("Estado")]
    public bool isOk;

    [Header("Pieza")]
    public Transform referenceObject;     // Padre completo
    public Transform insertionAnchor;     // Hijo que marca el punto de inserción

    [Header("Interacción")]
    public GameObject interactable;

    // FInal attach
    public GameObject[] interactables;
    public GameObject generalInteractable;

    private void Update()
    {
        if (transform.localScale == Vector3.one)
        {
            isOk = false;
        }
        else
        {
            isOk = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Object") || isOk) return;

        Vector3 midpoint = GetComponent<ReferencedMeshSurgery>().midpoint;

        interactable.SetActive(false);

        PlaceAtMidpoint(midpoint);

        isOk = true;

        foreach (var item in interactables)
        {
            item.SetActive(false);
        }

        generalInteractable.SetActive(true);

    }

    private void PlaceAtMidpoint(Vector3 midpoint)
    {
        // ¿Cuánto necesita moverse el anchor?
        Vector3 offset = midpoint - insertionAnchor.position;

        // Movemos el padre completo esa misma cantidad
        referenceObject.position += offset;
    }
}