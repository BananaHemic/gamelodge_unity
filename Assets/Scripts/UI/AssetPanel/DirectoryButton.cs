using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class DirectoryButton : MonoBehaviour
{
    public TMP_Text NameText;

    private string Name;
    private DirectoryButtons Parent;

    public void Init(string name, DirectoryButtons parent)
    {
        //button.name = buttonText;
        Name = name;
        NameText.text = name;
        Parent = parent;
    }
    public void OnClick()
    {
        Parent.OnButtonClicked(this, Name);
    }
}
