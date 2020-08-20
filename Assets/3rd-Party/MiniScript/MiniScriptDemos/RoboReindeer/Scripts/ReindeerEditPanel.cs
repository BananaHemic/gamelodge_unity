using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections.Generic;
using Miniscript;

public class ReindeerEditPanel : MonoBehaviour {
	#region Public Properties
	
	public Graphic background;
	public InputField nameField;
	public CodeEditor codeField;
	public Text checkResultText;
	public Reindeer deer;
	
	public UnityEvent onEditorClosed;
	
	#endregion
	//--------------------------------------------------------------------------------
	#region Private Properties
	
	
	#endregion
	//--------------------------------------------------------------------------------
	#region MonoBehaviour Events
	void Start() {
		Hide();
	}
	
	void Update() {
	}

	#endregion
	//--------------------------------------------------------------------------------
	#region Public Methods
	
	public void Show(Reindeer deer) {
		this.deer = deer;
		background.color = new Color(deer.color.r, deer.color.g, deer.color.b, 0.5f);
		nameField.text = deer.name;
		codeField.source = deer.miniscript;
		checkResultText.text = "Enter MiniScript code above.  Click Check Syntax to check for basic errors.";
		ReindeerScript scp = deer.GetComponent<ReindeerScript>();
		if (!string.IsNullOrEmpty(scp.lastError)) ShowError(scp.lastError);
		Debug.Log("scp.lastError: " + scp.lastError);
		gameObject.SetActive(true);
		codeField.Select();
	}
	
	public void Hide() {
		if (deer != null) {
			deer.name = nameField.text;
			deer.miniscript = codeField.source;
		}
		gameObject.SetActive(false);	
		if (onEditorClosed != null) onEditorClosed.Invoke();
	}
	
	public void NameChanged() {
		deer.name = nameField.text;
	}
	
	public void CheckSource() {
		checkResultText.text = null;
		Miniscript.Interpreter interp = new Miniscript.Interpreter();
		interp.errorOutput = (string s) => ShowError(s);
		interp.Reset(codeField.source);
		try {
			interp.Compile();
		} catch (Miniscript.MiniscriptException err) {
			ShowError(err.Description());
		}
		if (string.IsNullOrEmpty(checkResultText.text)) {
			checkResultText.text = "Looks good!";
		}
	}
	
	#endregion
	//--------------------------------------------------------------------------------
	#region Private Methods
	
	void ShowError(string err) {
		checkResultText.text = err;
	}
	
	#endregion
}
