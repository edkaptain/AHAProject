using UnityEngine;
using static Oculus.Interaction.Context;


public enum SnapObjectType
{
    Default, Scalpel
}
public class SnapManager : MonoBehaviour
{
    public static SnapManager Instance;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }
}
