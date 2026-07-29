using UnityEngine;
using UnityEngine.Events;

public class SnapTarget : MonoBehaviour
{
    [Header("Properties")]
    public SnapObjectType acceptedType;
    public bool isOcuppied;
    public SnapObject snappedObject;
    public bool lookSnap;

    [Header("Snap Point")]
    public Transform snapPoint;
    public UnityEvent onAction;

    private void Start()
    {
        snapPoint = this.transform;
    }
}
