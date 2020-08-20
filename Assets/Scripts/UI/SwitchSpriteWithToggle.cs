using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Toggle))]
public class SwitchSpriteWithToggle : MonoBehaviour
{
    public Sprite OnSprite;
    public Sprite OffSprite;
    public Image MainImage;

    //private Toggle _toggle;
    
    void Start()
    {
        //_toggle = GetComponent<Toggle>();
    }
    public void OnValueChange(bool val)
    {
        MainImage.sprite = val ? OnSprite : OffSprite;
    }
}
