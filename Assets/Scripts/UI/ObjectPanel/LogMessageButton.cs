using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class LogMessageButton : MonoBehaviour
{
    public TMP_Text MessageText;

    public enum LogMessageType
    {
        CompilerError,
        RuntimeException,
        PrintMessage
    }

    private int _line;
    private LogMessageType _messageType;

    public void Init(string txt, int line, LogMessageType logType)
    {
        MessageText.text = txt;
        _line = line;
        _messageType = logType;
    }

    public void OnClick()
    {
        CodeUI.Instance.OnLogMessageClicked(_line);
    }
}
