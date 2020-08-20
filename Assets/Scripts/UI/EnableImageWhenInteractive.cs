using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class EnableImageWhenInteractive : MonoBehaviour
{
    public Button Buttons;
    private Image _image;
    private bool _isVisible;

    private void Awake()
    {
        _image = GetComponent<Image>();
        _isVisible = _image.enabled;
    }
    void Update()
    {
        bool shouldBeVisible = !Buttons.interactable;
        if (shouldBeVisible == _isVisible)
            return;
        _image.enabled = shouldBeVisible;
        _isVisible = shouldBeVisible;
    }
}
