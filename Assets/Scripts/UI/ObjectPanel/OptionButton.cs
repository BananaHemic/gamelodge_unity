using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class OptionButton : MonoBehaviour
{
    public Image ScriptTypeImage;
    public TextMeshProUGUI ScriptNameText;

    private int _index;

    public void Init(Sprite icon, string titleText, int index)
    {
        _index = index;
        ScriptTypeImage.enabled = icon != null;
        ScriptTypeImage.sprite = icon;
        ScriptNameText.SetText(titleText);
    }
    public void OnClick()
    {
        //Debug.Log("Click for " + _index);
        OptionPopup.Instance.OnOptionSelected(_index);
    }
}
