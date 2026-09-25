using UnityEngine;
using UnityEngine.Events;

public class SnapTarget : MonoBehaviour
{
    [Header("Properties")]
    public bool isOcuppied;
    public SnapObject snappedObject;
    public bool lookSnap;

    [Header("Snap Point")]
    private Transform snapPoint;
    public UnityEvent onAction;

    private void Start()
    {
        snapPoint = this.transform;
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.LogWarning($"The object was detected {other}");
        SnapObject snapObject = other.GetComponent<SnapObject>();

        if (snapObject == null)
            return;

        if (isOcuppied)
            return;

        if (snapObject.currentTarget != this)
            return;

        snapObject.SetParent(transform.root);
        snapObject.Lock();

        snappedObject = snapObject;
        isOcuppied = true;

        onAction?.Invoke();
    }


}
