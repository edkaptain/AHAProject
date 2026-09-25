using UnityEngine;

/// <summary>
/// Colocar en la esfera hija del bisturi. Lee el centro y radio del
/// SphereCollider, pero no utiliza OnTriggerEnter ni necesita Rigidbody.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public sealed class ScalpelSphere : MonoBehaviour
{
    public CuttableMembrane membrane;
    public bool cuttingEnabled = true;
    private SphereCollider sphere;

    private void Awake() { sphere = GetComponent<SphereCollider>(); }

    private void LateUpdate()
    {
        if (membrane == null) return;
        if (!cuttingEnabled || !membrane.isActiveAndEnabled)
        {
            membrane.EndStroke();
            return;
        }

        Vector3 scale = transform.lossyScale;
        float radius = sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        Vector3 center = transform.TransformPoint(sphere.center);
        membrane.CutWithSphere(center, radius);
    }

    // Se puede conectar a un evento de boton/agarre del sistema XR.
    public void SetCutting(bool enabled)
    {
        cuttingEnabled = enabled;
        if (!enabled && membrane != null) membrane.EndStroke();
    }

    private void OnDisable()
    {
        if (membrane != null) membrane.EndStroke();
    }
}
