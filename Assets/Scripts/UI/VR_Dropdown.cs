using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class VR_Dropdown : MonoBehaviour
{
    public Transform ActiveParent;

    private IEnumerator Start()
    {
        Canvas can = GetComponent<Canvas>();
        if(VRSDKUtils.Instance.CurrentDevice != VRSDKUtils.DEVICE.Desktop)
            can.overrideSorting = false;
        var layout = gameObject.AddComponent<LayoutElement>();
        var toggle = gameObject.AddComponent<CanvasToggle>();
        layout.ignoreLayout = true;
        yield return null;
        //Debug.Log("canvas start!");
        Vector3 localPos = ActiveParent.InverseTransformPoint(transform.position);
        //Debug.Log("Local pos " + localPos.ToPrettyString());
        transform.SetParent(ActiveParent, true);
        yield return null;
        transform.localPosition = localPos;
    }
}
