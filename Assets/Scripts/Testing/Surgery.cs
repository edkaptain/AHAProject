using UnityEngine;

public class BlendShapeByDistance : MonoBehaviour
{
    [Header("Objeto que se acerca")]
    [SerializeField] private Transform target;

    [Header("Objeto deformable")]
    [SerializeField] private SkinnedMeshRenderer skinnedMesh;
    [SerializeField] private int blendShapeIndex = 0;

    [Header("Distancias")]
    [SerializeField] private float minDistance = 0.02f;
    [SerializeField] private float maxDistance = 0.30f;

    [Header("Collider del objeto")]
    [SerializeField] private Collider objectCollider;

    private void Update()
    {
        if (target == null || skinnedMesh == null || objectCollider == null)
            return;

        // Punto de la superficie más cercano al target
        Vector3 closestPoint = objectCollider.ClosestPoint(target.position);

        // Distancia entre el target y la superficie
        float distance = Vector3.Distance(target.position, closestPoint);

        // Convierte la distancia a un rango de 0 a 1
        float normalizedDistance =
            Mathf.InverseLerp(maxDistance, minDistance, distance);

        // Convierte de 0–1 a 0–100
        float blendWeight = normalizedDistance * 100f;

        skinnedMesh.SetBlendShapeWeight(blendShapeIndex, blendWeight);
    }
}