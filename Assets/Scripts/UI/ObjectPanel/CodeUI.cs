using Miniscript;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Text;

public class CodeUI : GenericSingleton<CodeUI>
{
    public Sprite CodeSprite;
    public CodeEditor MainCodeEditor;
    public TMP_InputField TitleInputField;
    public Button SelectScriptButton;
    public Button AddNewButton;
    public Button DuplicateButton;
    public Toggle SyncPosRotScaleToggle;
    public TMP_Dropdown WhoRunsDropdown;
    public Button CheckMyCodeButton;
    public Button ApplyButton;
    public Image CodeValidityImage;
    public Sprite ValidCodeImage;
    public Sprite InvalidCodeImage;
    public ScrollRect LogDisplay;
    public RectTransform LogContainer;
    public TMP_Text AvailableVariablesText;
    public TMP_Text AvailableFunctionsText;
    public TMP_Text AvailableEventsText;
    public TMP_LinkHandler VariableLinkHandler;
    public TMP_LinkHandler FunctionLinkHandler;
    public TMP_LinkHandler EventLinkHandler;
    public GameObject LogMessagePrefab;

    public DRUserScript CurrentUserScript { get; private set; }

    /// <summary>
    /// Is the current code checked for compiler syntax errors?
    /// </summary>
    private bool _isCodeSyntaxChecked = false;
    /// <summary>
    /// Has Compile() not generated any errors?
    /// (Only valid if _isCodeSyntaxChecked is true)
    /// </summary>
    private bool _isCodeSyntaxValid = false;
    /// <summary>
    /// Do we have changes to the script settings/name that have not yet been synced with the server?
    /// </summary>
    private bool _isScriptInfoUnsynced = false;
    /// <summary>
    /// Do we have local changes to the code that have not been synced with
    /// the server
    /// </summary>
    private bool _isScriptCodeUnsynced = false;
    private SceneObject _selectedSceneObject;
    private readonly Parser _checkSourceParser = new Parser();

    private readonly Queue<LogMessageButton> _logMessages = new Queue<LogMessageButton>(MaxLogMessages);
    private readonly StringBuilder _workingStringBuilder = new StringBuilder();
    private List<SourceLine> _workingSourceLines = new List<SourceLine>();
    public static readonly int MaxExampleLineLength = 64;
    const string NameInputFieldIdentifier = "codeName";
    const string CodeInputFieldIdentifier = "mainCode";
    const int DefaultWhoRunsValue = 0;
    const bool DefaultSyncPosRotScale = false;
    const int MaxLogMessages = 42;

    protected override void Awake()
    {
        base.Awake();
        if(_logMessages.Count == 0)
            LogDisplay.gameObject.SetActive(false);
        MainCodeEditor.OnInputFieldSelected += OnCodeInputSelected;
        MainCodeEditor.OnInputFieldDeselected += OnCodeInputDeselected;
        SyncPosRotScaleToggle.isOn = DefaultSyncPosRotScale;
        RefreshButtonForScriptSettingChange();
        VariableLinkHandler.OnLinkSelected += OnLinkClicked;
        FunctionLinkHandler.OnLinkSelected += OnLinkClicked;
        EventLinkHandler.OnLinkSelected += OnLinkClicked;
    }
    private void OnEnable()
    {
        MainCodeEditor.interactable = true;
    }
    private void OnDisable()
    {
        MainCodeEditor.interactable = false;
    }
    public void RefreshScriptCodeAndSettings()
    {
        if (!_isScriptInfoUnsynced)
        {
            SyncPosRotScaleToggle.isOn = CurrentUserScript == null ? DefaultSyncPosRotScale : CurrentUserScript.SyncPosRotScale;
            TitleInputField.text = CurrentUserScript == null ? "" : CurrentUserScript.Name;
            WhoRunsDropdown.value = CurrentUserScript == null ? DefaultWhoRunsValue : (int)CurrentUserScript.WhoRunsScript;
            _isScriptInfoUnsynced = false;
        }
        if (!_isScriptCodeUnsynced)// Don't change the code if we have unsynced changes
        {
            MainCodeEditor.source = CurrentUserScript == null ? MainCodeEditor.initialSourceCode.text : CurrentUserScript.GetCodeWithoutPostScript();
            _isScriptCodeUnsynced = false;
        }
        RefreshButtonForScriptSettingChange();
    }
    private void RefreshButtonForScriptSettingChange()
    {
        //ApplyButton.interactable = _isScriptCodeUnsynced || (CurrentUserScript != null && _isScriptInfoUnsynced);
        if (_isCodeSyntaxChecked)
        {
            CodeValidityImage.gameObject.SetActive(true);
            CodeValidityImage.sprite = _isCodeSyntaxValid ? ValidCodeImage : InvalidCodeImage;
        }
        else
        {
            CodeValidityImage.gameObject.SetActive(false);
        }
    }
    public void OnAddNewScriptButtonClicked()
    {
        CurrentUserScript = null;
        _isCodeSyntaxChecked = false;
        _isScriptInfoUnsynced = false;
        _isScriptCodeUnsynced = false;
        SyncPosRotScaleToggle.isOn = DefaultSyncPosRotScale;
        RefreshScriptCodeAndSettings();
        RefreshButtonForScriptSettingChange();
    }
    public void OnListScriptsButtonClicked()
    {
        List<MiniscriptBehaviorInfo> scripts = UserScriptManager.Instance.GetAllNetworkBehaviors();
        OptionPopup.Instance.GetListsToLoadInto(out List<string> optionTexts, out List<int> callbackData, out List<Sprite> junk);
        for (int i = 0; i < scripts.Count; i++)
            optionTexts.Add(scripts[i].Name);
        for (int i = 0; i < scripts.Count; i++)
            callbackData.Add(scripts[i].BehaviorID);
        OptionPopup.Instance.LoadOptions("Select Script", optionTexts, CodeSprite, OnListScriptOptionClicked, callbackData);
    }
    private void OnListScriptOptionClicked(bool wasCancel, int selectedIndex)
    {
        if (wasCancel)
            return;
        DRUserScript userScript;
        ushort selectedID = (ushort)selectedIndex;
        if(!UserScriptManager.Instance.TryGetDRUserScript(selectedID, out userScript))
        {
            Debug.LogWarning("Failed to get user script for #" + selectedIndex + " not switching selected script");
            return;
        }
        CurrentUserScript = userScript;
        _isCodeSyntaxChecked = false;
        _isScriptInfoUnsynced = false;
        _isScriptCodeUnsynced = false;
        RefreshScriptCodeAndSettings();
        // Clear all log messages
        ClearLogMessages();
    }
    public void ClearLogMessages()
    {
        while(_logMessages.Count > 0)
        {
            var oldMsg = _logMessages.Dequeue();
            SimplePool.Instance.DespawnUI(oldMsg.gameObject);
        }
        LogDisplay.gameObject.SetActive(false);
    }
    public void OnDuplicateScriptButtonClicked()
    {
        TitleInputField.text = TitleInputField.text + "(Copy)";
        OnApplyButtonClicked();
        CurrentUserScript = null;
        _isCodeSyntaxChecked = false;
        _isScriptInfoUnsynced = false;
        _isScriptCodeUnsynced = false;
        RefreshButtonForScriptSettingChange();
    }
    public void OnNameInputFieldSelect()
    {
        //Debug.Log("Name input deselected");
        RLDHelper.Instance.RegisterInputSelected(NameInputFieldIdentifier);
    }
    public void OnNameInputFieldDeselect()
    {
        //Debug.Log("Name input selected");
        RLDHelper.Instance.RegisterInputDeselected(NameInputFieldIdentifier);
    }
    private void OnCodeInputSelected()
    {
        //Debug.Log("Code input selected");
        RLDHelper.Instance.RegisterInputSelected(CodeInputFieldIdentifier);

        if (!this.enabled)
        {
            //Debug.Log("Immediately deselecting code editor -- we are not enabled");
            StartCoroutine(DeselectCodeEditorAfterFrame());
        }
    }
    /// <summary>
    /// Selectable complains if we try to turn off interactable
    /// while the code editor is being selected, so we turn it off
    /// after a frame
    /// </summary>
    /// <returns></returns>
    private IEnumerator DeselectCodeEditorAfterFrame()
    {
        yield return null;
        MainCodeEditor.interactable = false;
    }
    private void OnCodeInputDeselected()
    {
        //Debug.Log("Code input deselected");
        RLDHelper.Instance.RegisterInputDeselected(CodeInputFieldIdentifier);
    }
    public void OnSyncPosRotScaleToggleClicked()
    {
        _isScriptInfoUnsynced = true;
        RefreshButtonForScriptSettingChange();
    }
    public void OnWhoRunsDowndownChanged()
    {
        _isScriptInfoUnsynced = true;
        RefreshButtonForScriptSettingChange();
    }
    public void OnCheckCodeButtonClicked()
    {
        Debug.Log("Checking code");
        _isCodeSyntaxValid = true;
		try {
            _checkSourceParser.Parse(MainCodeEditor.source);
		} catch (Miniscript.CompilerException mse) {
            //Debug.LogError("Check source error: " + mse.Description());
            _isCodeSyntaxValid = false;
            OnCodeCompilerErrorOutput(mse.Message, mse.location != null ? mse.location.lineNum : -1);
		}
        _isCodeSyntaxChecked = true;
        RefreshButtonForScriptSettingChange();
    }
    /// <summary>
    /// Takes the title/code/settings and either creates a new script or updates an existing
    /// script
    /// </summary>
    public void OnApplyButtonClicked()
    {
        if (CurrentUserScript == null)
            CurrentUserScript = UserScriptManager.Instance.CreateNewScript(TitleInputField.text, MainCodeEditor.source, SyncPosRotScaleToggle.isOn, (DRUserScript.WhoRuns)WhoRunsDropdown.value);
        else
            UserScriptManager.Instance.LocalUpdateToUserScript(CurrentUserScript, TitleInputField.text, MainCodeEditor.source, SyncPosRotScaleToggle.isOn, (DRUserScript.WhoRuns)WhoRunsDropdown.value);
        _isScriptInfoUnsynced = false;
        _isScriptCodeUnsynced = false;
        RefreshButtonForScriptSettingChange();
    }
    public void OnNameInputFieldChange()
    {
        _isScriptInfoUnsynced = true;
        RefreshButtonForScriptSettingChange();
    }
    public void InitForSelectedSceneObject(SceneObject selectedSceneObject)
    {
        _selectedSceneObject = selectedSceneObject;
        if(selectedSceneObject == null)
        {
            // just use the default stuff
            _workingStringBuilder.Clear();
            List<ExposedVariable> alwaysVariables = new List<ExposedVariable>();
            SceneObject.GetAllSceneObjectExposedVariables(alwaysVariables);
            for(int i = 0; i < alwaysVariables.Count; i++)
            {
                ExposedVariable variable = alwaysVariables[i];
                _workingStringBuilder.Append("<link=");
                _workingStringBuilder.Append(i);
                _workingStringBuilder.Append(">");
                _workingStringBuilder.Append(variable.Name);
                _workingStringBuilder.Append("</link>");
                if (i != alwaysVariables.Count - 1)
                    _workingStringBuilder.Append(", ");
            }
            AvailableVariablesText.text = _workingStringBuilder.ToString();
            VariableLinkHandler.SetLinks(alwaysVariables);

            _workingStringBuilder.Clear();
            List<ExposedFunction> alwaysFunctions = UserScriptManager.Instance.GetAllAlwaysExposedFunctions();
            for(int i = 0; i < alwaysFunctions.Count; i++)
            {
                ExposedFunction function = alwaysFunctions[i];
                _workingStringBuilder.Append("<link=");
                _workingStringBuilder.Append(i);
                _workingStringBuilder.Append(">");
                _workingStringBuilder.Append(function.Name);
                _workingStringBuilder.Append("</link>");
                _workingStringBuilder.Append("()");
                if (i != alwaysFunctions.Count - 1)
                    _workingStringBuilder.Append(", ");
            }
            AvailableFunctionsText.text = _workingStringBuilder.ToString();
            FunctionLinkHandler.SetLinks(alwaysFunctions);

            _workingStringBuilder.Clear();
            List<ExposedEvent> alwaysEvents = new List<ExposedEvent>();
            SceneObject.GetAllSceneObjectExposedEvents(alwaysEvents);
            alwaysEvents.AddRange(UserScriptManager.Instance.GetAllAlwaysExposedEvents());
            for(int i = 0; i < alwaysEvents.Count; i++)
            {
                ExposedEvent exposedEvent = alwaysEvents[i];
                _workingStringBuilder.Append("<link=");
                _workingStringBuilder.Append(i);
                _workingStringBuilder.Append(">");
                _workingStringBuilder.Append(exposedEvent.Name);
                _workingStringBuilder.Append("</link>");
                if (i != alwaysEvents.Count - 1)
                    _workingStringBuilder.Append(", ");
            }
            AvailableEventsText.text = _workingStringBuilder.ToString();
            EventLinkHandler.SetLinks(alwaysEvents);
            return;
        }
        // TODO update the available methods and stuff
        _workingStringBuilder.Clear();
        List<ExposedVariable> userVariables = selectedSceneObject.GetAllExposedVariables();
        for(int i = 0; i < userVariables.Count; i++)
        {
            ExposedVariable variable = userVariables[i];
            _workingStringBuilder.Append("<link=");
            _workingStringBuilder.Append(i);
            _workingStringBuilder.Append(">");
            _workingStringBuilder.Append(variable.Name);
            _workingStringBuilder.Append("</link>");
            if (i != userVariables.Count - 1)
                _workingStringBuilder.Append(", ");
        }
        AvailableVariablesText.text = _workingStringBuilder.ToString();
        VariableLinkHandler.SetLinks(userVariables);

        _workingStringBuilder.Clear();
        List<ExposedFunction> userFunctions = selectedSceneObject.GetAllAvailableUserFunctions();
        for(int i = 0; i < userFunctions.Count; i++)
        {
            ExposedFunction function = userFunctions[i];
            _workingStringBuilder.Append("<link=");
            _workingStringBuilder.Append(i);
            _workingStringBuilder.Append(">");
            _workingStringBuilder.Append(function.Name);
            _workingStringBuilder.Append("()");
            _workingStringBuilder.Append("</link>");
            if (i != userFunctions.Count - 1)
                _workingStringBuilder.Append(", ");
        }
        AvailableFunctionsText.text = _workingStringBuilder.ToString();
        FunctionLinkHandler.SetLinks(userFunctions);

        _workingStringBuilder.Clear();
        List<ExposedEvent> userEvents = selectedSceneObject.GetAllExposedEvents();
        for(int i = 0; i < userEvents.Count; i++)
        {
            ExposedEvent exposedEvent = userEvents[i];
            _workingStringBuilder.Append("<link=");
            _workingStringBuilder.Append(i);
            _workingStringBuilder.Append(">");
            _workingStringBuilder.Append(exposedEvent.Name);
            _workingStringBuilder.Append("</link>");
            if (i != userEvents.Count - 1)
                _workingStringBuilder.Append(", ");
        }
        AvailableEventsText.text = _workingStringBuilder.ToString();
        EventLinkHandler.SetLinks(userEvents);
    }
    private void OnLinkClicked(IExposedProperty exposedProperty)
    {
        // Insert an example of the variable
        exposedProperty.GetExample(ref _workingSourceLines);
        for (int i = 0; i < _workingSourceLines.Count; i++)
        {
            SourceLine line = _workingSourceLines[i];
            MainCodeEditor.InsertLine(MainCodeEditor.SourceLines.Count, ref line);
        }
        _workingSourceLines.Clear();
        MainCodeEditor.UpdateLineYPositions();
    }
    private void OnCodeCompilerErrorOutput(string errorText, int line)
    {
        Debug.LogError("Error text: " + errorText);
        AddLogMessage(errorText, line, LogMessageButton.LogMessageType.CompilerError);
        _isCodeSyntaxValid = false;
    }
    public void AddLogMessage(string msg, int line, LogMessageButton.LogMessageType messageType)
    {
        if (_logMessages.Count == 0)
            LogDisplay.gameObject.SetActive(true);

        if(_logMessages.Count >= MaxLogMessages)
        {
            var oldMsg = _logMessages.Dequeue();
            SimplePool.Instance.DespawnUI(oldMsg.gameObject);
        }

        var newMsg = SimplePool.Instance.SpawnUI(LogMessagePrefab, LogContainer);
        LogMessageButton messageButton = newMsg.GetComponent<LogMessageButton>();
        messageButton.Init(msg, line, messageType);
        _logMessages.Enqueue(messageButton);
    }
    public void OnLogMessageClicked(int line)
    {
        //Debug.Log("msg line " + line);
        if (line <= 0)
            return;
        else if(line >= MainCodeEditor.SourceLines.Count)
            line = MainCodeEditor.SourceLines.Count - 1;
        //Debug.Log("Moving to line #" + line);
        //MainCodeEditor.ScrollIntoView(new CodeEditor.TextPosition(line, 0));
        MainCodeEditor.MoveCaretToPosition(new CodeEditor.TextPosition(line - 1, 0));
    }
    private void Update()
    {
        // Poll the code editor to see if the script text has changed
        // TODO this would be better as an event, but I don't want to change the miniscript code editor
        //if (!_isScriptCodeUnsynced)
        //{
            //_isScriptCodeUnsynced = CurrentUserScript == null
            //? MainCodeEditor.source != MainCodeEditor.initialSourceCode.text
            //: MainCodeEditor.source != CurrentUserScript.GetCodeWithoutPostScript();
            //if (_isScriptCodeUnsynced)
            //{
                //Debug.Log("No longer have initial source code");
                //RefreshButtonForScriptSettingChange();
            //}
        //}

        //if (Input.GetKeyDown(KeyCode.F1))
            //Debug.Log(CurrentUserScript.GetCodeWithoutPostScript());
    }
}
