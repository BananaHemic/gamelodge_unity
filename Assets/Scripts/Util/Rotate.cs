using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rotate : MonoBehaviour
{
    public Vector3 DeltaAngle = new Vector3(0, 100f, 0);
    void Update()
    {
        transform.Rotate(DeltaAngle * Time.unscaledDeltaTime);
    }
}
