using UnityEngine;

public class Surgery_001 : MonoBehaviour
{
    [Header("Progress 0 - 100")]
    [Range(0, 100)]
    public float key;

    public SkinnedMeshRenderer skinnedMesh;

    private void OnValidate()
    {
        UpdateShapeKeys();
    }

    private void Update()
    {
        UpdateShapeKeys();
    }

    private void UpdateShapeKeys()
    {
        if (skinnedMesh == null) return;
        if (skinnedMesh.sharedMesh == null) return;

        int numberOfKeys = skinnedMesh.sharedMesh.blendShapeCount;

        if (numberOfKeys <= 0) return;

        float sectionSize = 100f / numberOfKeys;

        for (int i = 0; i < numberOfKeys; i++)
        {
            float start = i * sectionSize;
            float end = start + sectionSize;

            float normalizedProgress = Mathf.InverseLerp(start, end, key);
            normalizedProgress = Mathf.Clamp01(normalizedProgress);

            float weight = normalizedProgress * 100f;

            skinnedMesh.SetBlendShapeWeight(i, weight);
        }
    }
}