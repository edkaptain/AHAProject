using System;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// Clones the reference figure with all the vertices and meshes
/// </summary>
/// 
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class CloneFigure : MonoBehaviour
{
    [SerializeField] MeshFilter originalMeshFilter;
    private void Start()
    {
        // Original and clone mesh
        Mesh original = originalMeshFilter.sharedMesh;
        Mesh meshClone = new Mesh();

        meshClone.name = original.name + "_Clone";
        meshClone.indexFormat = original.indexFormat;

        // Copy geometry
        meshClone.vertices = original.vertices;
        meshClone.uv = original.uv;
        meshClone.triangles = original.triangles;

        // Recalculate data
        meshClone.subMeshCount = original.subMeshCount;

        for(int i = 0; i < original.subMeshCount; i++)
        {
            meshClone.SetTriangles(original.GetTriangles(i), i);
        }

        meshClone.RecalculateNormals();
        meshClone.RecalculateTangents();
        meshClone.RecalculateBounds();

        GetComponent<MeshFilter>().mesh = meshClone;

        // Copy materials
        MeshRenderer originalRenderer = originalMeshFilter.GetComponent<MeshRenderer>();

        MeshRenderer cloneRenderer = meshClone.GetComponent<MeshRenderer>();

        cloneRenderer.sharedMaterials = originalRenderer.sharedMaterials;
    }
}
