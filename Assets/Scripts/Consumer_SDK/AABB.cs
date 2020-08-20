using UnityEngine;
using System.Collections.Generic;

public struct AABB
{
    public readonly Vector3 Center;
    public readonly Vector3 Size;
    public readonly bool IsValid;
    public Vector3 Extents { get { return Size * 0.5f; } }

    public AABB(Vector3 center, Vector3 size)
    {
        Center = center;
        Size = size;
        IsValid = true;
    }
    public AABB(Bounds bounds)
    {
        Center = bounds.center;
        Size = bounds.size;
        IsValid = true;
    }
    public static AABB GetInvalid()
    {
        return new AABB();
    }
    public AABB ApplyTransformMatrix(Matrix4x4 transformMatrix)
    {
        Vector3 rightAxis = transformMatrix.GetColumn(0);
        Vector3 upAxis = transformMatrix.GetColumn(1);
        Vector3 lookAxis = transformMatrix.GetColumn(2);

        Vector3 extents = Size * 0.5f;
        Vector3 rightExtent = rightAxis * extents.x;
        Vector3 upExtent = upAxis * extents.y;
        Vector3 lookExtent = lookAxis * extents.z;

        float extentX = Mathf.Abs(rightExtent.x) + Mathf.Abs(upExtent.x) + Mathf.Abs(lookExtent.x);
        float extentY = Mathf.Abs(rightExtent.y) + Mathf.Abs(upExtent.y) + Mathf.Abs(lookExtent.y);
        float extentZ = Mathf.Abs(rightExtent.z) + Mathf.Abs(upExtent.z) + Mathf.Abs(lookExtent.z);

        Vector3 transformedCenter = transformMatrix.MultiplyPoint(Center);
        Vector3 transformedSize = new Vector3(extentX, extentY, extentZ) * 2.0f;

        return new AABB(transformedCenter, transformedSize);
    }
    public static AABB Add(AABB lhs, AABB rhs)
    {
        if (!lhs.IsValid)
            return rhs.IsValid ? rhs : new AABB();
        if (!rhs.IsValid)
            return lhs;

        Vector3 lhsMin = lhs.Center - lhs.Extents;
        Vector3 lhsMax = lhs.Center + lhs.Extents;

        Vector3 rhsMin = rhs.Center - rhs.Extents;
        Vector3 rhsMax = rhs.Center + rhs.Extents;

        // Compare to find the smallest min and
        // largest max. Store them in lhs
        if (rhsMin.x < lhsMin.x)
            lhsMin.x = rhsMin.x;
        if (rhsMin.y < lhsMin.y)
            lhsMin.y = rhsMin.y;
        if (rhsMin.z < lhsMin.z)
            lhsMin.z = rhsMin.z;

        if (rhsMax.x > lhsMax.x)
            lhsMax.x = rhsMax.x;
        if (rhsMax.y > lhsMax.y)
            lhsMax.y = rhsMax.y;
        if (rhsMax.z > lhsMax.z)
            lhsMax.z = rhsMax.z;

        // Use the min, max to find the correct center/size
        Vector3 center = (lhsMin + lhsMax) * 0.5f;
        Vector3 size = lhsMax - lhsMin;
        return new AABB(center, size);
    }

}
