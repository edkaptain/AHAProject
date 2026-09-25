using UnityEngine;

/// <summary>
/// Opcional: mueve la esfera una vez de izquierda a derecha, sin visor VR.
/// Desactivar este componente antes de controlar el bisturi manualmente.
/// </summary>
public sealed class DemoScalpelMotion : MonoBehaviour
{
    public CuttableMembrane membrane;
    [Min(0.1f)] public float duration = 6f;
    [Tooltip("Altura LOCAL del centro. Debe ser menor que el radio de la esfera.")]
    public float height = 0.002f;
    private float startTime;

    private void OnEnable() { startTime = Time.time; }

    private void Update()
    {
        if (membrane == null) return;
        float t = Mathf.Clamp01((Time.time - startTime) / Mathf.Max(0.1f, duration));
        float x = Mathf.Lerp(-membrane.width * 0.35f, membrane.width * 0.35f, t);
        transform.position = membrane.transform.TransformPoint(new Vector3(x, height, 0f));
    }

    [ContextMenu("Restart Demo")]
    public void RestartDemo()
    {
        if (!Application.isPlaying) return;
        if (membrane != null) membrane.ResetMembrane();
        startTime = Time.time;
    }
}
