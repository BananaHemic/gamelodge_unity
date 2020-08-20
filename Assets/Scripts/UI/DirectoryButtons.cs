using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using TMPro;

public class DirectoryButtons : MonoBehaviour
{
    public GameObject ButtonPrefab;

    private readonly List<DirectoryButton> _buttons = new List<DirectoryButton>();
    private readonly Dictionary<DirectoryButton, Action<string>> _buttonActions = new Dictionary<DirectoryButton, Action<string>>();

    public GameObject AddButton(string buttonText, Action<string> onClicked)
    {
        GameObject newButton = SimplePool.Instance.SpawnUI(ButtonPrefab, transform);
        DirectoryButton button = newButton.GetComponent<DirectoryButton>();
        _buttons.Add(button);
        button.Init(buttonText, this);
        _buttonActions.Add(button, onClicked);
        return newButton;
    }
    public void OnButtonClicked(DirectoryButton button, string name)
    {
        Debug.Log("Clicked directory button " + name);
        Action<string> action;
        if(!_buttonActions.TryGetValue(button, out action))
        {
            Debug.LogError("No action for clicked button " + name);
            return;
        }
        action(name);
    }
    public void ClearButtons()
    {
        foreach (var button in _buttons)
            SimplePool.Instance.DespawnUI(button.gameObject);
        _buttons.Clear();
        _buttonActions.Clear();
    }
}
