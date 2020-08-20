using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class UsernameDisplay : MonoBehaviour
{
    public TMP_Text UsernameText;
    public GameObject UsernameBackground;

    private UserDisplay OwningDisplay;

    public void Init(UserDisplay userDisp)
    {
        OwningDisplay = userDisp;
        UsernameText.text = string.Format("{0}", userDisp.DRUserObj.DisplayName);
    }
    public void Reset()
    {
        OwningDisplay = null;
    }
    private void Update()
    {
        if (OwningDisplay == null)
            return;
        transform.position = OwningDisplay.transform.position + new Vector3(0, 2f, 0);
        //transform.LookAt(UserObject.Instance.transform);
        Vector3 lookDir = transform.position - UserObject.Instance.transform.position;
        lookDir.y = 0;
        transform.rotation = Quaternion.LookRotation(lookDir);
    }
}
