using System.Collections.Generic;
using UnityEngine;

public class BoxHandDetector : MonoBehaviour
{
    [Header("Parent")]
    [SerializeField] private GameObject parentInteractable;

    [Header("Children")]
    [SerializeField] private GameObject[] childInteractables;

    private HashSet<Collider> handsInside = new HashSet<Collider>();

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("VRController")) return;

        handsInside.Add(other);

        UpdateInteractionMode();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("VRController")) return;

        handsInside.Remove(other);

        UpdateInteractionMode();
    }

    private void UpdateInteractionMode()
    {
        int handCount = handsInside.Count;

        if (handCount >= 2)
        {
            // Dos manos: activar función del padre
            SetParentMode(true);
            SetChildrenMode(false);
        }
        else
        {
            // Cero o una mano: permitir hijos
            SetParentMode(false);
            SetChildrenMode(true);
        }
    }

    private void SetParentMode(bool active)
    {
        if (parentInteractable != null)
            parentInteractable.SetActive(active);
    }

    private void SetChildrenMode(bool active)
    {
        foreach (GameObject child in childInteractables)
        {
            if (child != null)
                child.SetActive(active);
        }
    }
}