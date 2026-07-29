using UnityEngine;

public class SnapObject : MonoBehaviour
{
    [Header("Properties")]
    public SnapObjectType objectType;
    public SnapTarget currentTarget;
    public bool isSnapped;
    public bool isSelected;

    [Header("Settings")]
    public float vibrationTime = 0.2f;
}
