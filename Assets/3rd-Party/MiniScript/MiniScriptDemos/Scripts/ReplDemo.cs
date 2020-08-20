using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using Miniscript;

public class ReplDemo : MonoBehaviour {
	#region Public Properties
	
	public UnityEngine.UI.Text prompt;
	public UnityEngine.UI.InputField input;
	public Output output;
	public GameObjScript demoScript;
	public TextAsset setupCode;
	
	#endregion
	//--------------------------------------------------------------------------------
	#region Private Properties
	
	bool wasRunning;
	
	#endregion
	//--------------------------------------------------------------------------------
	#region MonoBehaviour Events
	
	void Awake() {
		// You don't really need to run the unit tests.  But if you want to, here's how:
		Miniscript.UnitTest.Run();
	}
	
	void Start() {
		// Define what to do with output from the interpreter.
		// We'll pass it to our output.PrintLine method, but wrap it in some color
		// tags depending on what sort of output it is.
		demoScript.interpreter.standardOutput = (string s) => output.PrintLine(s);
		demoScript.interpreter.implicitOutput = (string s) => output.PrintLine(
			"<color=#66bb66>" + s + "</color>");
		demoScript.interpreter.errorOutput = (string s) => {
			Debug.LogWarning(s);
			output.PrintLine("<color=red>" + s + "</color>");
			// ...and in case of error, we'll also stop the interpreter.
			demoScript.interpreter.Stop();
		};
		
		// Initialize the interpreter with our setup code.
		if (setupCode != null) demoScript.RunScript(setupCode.text);
		else demoScript.RunScript("");
		
		UpdatePrompt();
		input.ActivateInputField();
	}
	
	void Update() {
		UpdatePrompt();
	}

	#endregion
	//--------------------------------------------------------------------------------
	#region Public Methods
	
	public void HandleInput(string line) {
		if (string.IsNullOrEmpty(line) || !gameObject.activeInHierarchy) return;
		output.PrintLine("<color=grey>" + line + "</color>");
		demoScript.interpreter.REPL(line, 0.01);
		demoScript.UpdateFromScript();
		UpdatePrompt();
	}
	
	#endregion
	//--------------------------------------------------------------------------------
	#region Private Methods
	
	void UpdatePrompt() {
		if (!gameObject.activeInHierarchy) return;
		RectTransform promptR = prompt.GetComponent<RectTransform>();
		string promptStr;
		if (demoScript.interpreter.NeedMoreInput()) {
			promptStr = ". . . > ";
			promptR.sizeDelta = new Vector2(60, promptR.sizeDelta.y);
		} else if (demoScript.interpreter.Running()) {
			promptStr = null;
		} else {
			promptStr = "> ";
			promptR.sizeDelta = new Vector2(25, promptR.sizeDelta.y);
		}
		
		prompt.text = promptStr;
		RectTransform inputR = input.GetComponent<RectTransform>();
		inputR.anchoredPosition = new Vector2(promptR.sizeDelta.x, inputR.anchoredPosition.y);
		if (promptStr == null) {
			input.DeactivateInputField();
			input.gameObject.SetActive(false);
		} else {
			input.gameObject.SetActive(true);
			input.ActivateInputField();
		}
	}
	
	#endregion
}
