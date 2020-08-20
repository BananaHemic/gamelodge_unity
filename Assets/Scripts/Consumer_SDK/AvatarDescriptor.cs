using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AvatarDescriptor : MonoBehaviour
{
    public Vector3 ViewPosition;

    void OnDrawGizmosSelected()
    {
        // Draw a yellow sphere at the transform's position
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(ViewPosition, 0.02f);
    }
}
