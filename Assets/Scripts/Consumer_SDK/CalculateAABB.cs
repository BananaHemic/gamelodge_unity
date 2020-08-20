using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class CalculateAABB
{
    public static AABB CalcModelAABB(GameObject gameObject)
    {
        Mesh mesh = GetMesh(gameObject);
        if (mesh == null)
            return AABB.GetInvalid();
        return new AABB(mesh.bounds);
    }
    public static AABB GetHierarchyAABB(GameObject root)
    {
        Matrix4x4 rootMatrix = root.transform.localToWorldMatrix;
        AABB finalAABB = CalcModelAABB(root);

        List<GameObject> children = new List<GameObject>();
        GetAllChildren(root.transform, children, false);
        for(int i = 0; i < children.Count; i++)
        {
            GameObject child = children[i];
            AABB modelAABB = CalcModelAABB(child);
            if (!modelAABB.IsValid)
                continue;

            // Get the AABB in the root transform's local space
            Matrix4x4 local2Root = rootMatrix.inverse * child.transform.localToWorldMatrix;
            modelAABB = modelAABB.ApplyTransformMatrix(local2Root);

            finalAABB = finalAABB.IsValid ? AABB.Add(finalAABB, modelAABB) : modelAABB;
        }

        return finalAABB;
    }
    private static List<GameObject> GetAllChildren(Transform aParent, List<GameObject> objects, bool includeRoot=true)
    {
        if(includeRoot)
            objects.Add(aParent.gameObject);
        for (int i = 0; i < aParent.childCount; i++)
            GetAllChildren(aParent.GetChild(i), objects);
        return objects;
    }
    public static Mesh GetMesh(GameObject gameObject)
    {
        MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
        if (meshFilter != null)
            return meshFilter.sharedMesh;

        SkinnedMeshRenderer skinnedMeshRenderer = gameObject.GetComponent<SkinnedMeshRenderer>();
        if (skinnedMeshRenderer != null)
            return skinnedMeshRenderer.sharedMesh;
        return null;
    }
}
