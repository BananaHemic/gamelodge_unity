using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using System.Collections.Generic;

public class CodeManager : MonoBehaviour {
	#region Public Properties
	
	public Miniscript.CodeEditor codeField;
	public Output output;
	public TextAsset[] exampleCode;
	public GameObjScript target;
	
	#endregion
	//--------------------------------------------------------------------------------
	#region Private Properties
	
	string myCode;
	float myCodeStoreTime;
	bool myCodeMode = false;
	
	#endregion
	//--------------------------------------------------------------------------------
	#region MonoBehaviour Events
	void Start() {
		LoadMyCode();
	}
	
	void Update() {
		if (myCode != null && Time.time > myCodeStoreTime) {
			PlayerPrefs.SetString("myCode", myCode);
			myCode = null;
		}
	}

	#endregion
	//--------------------------------------------------------------------------------
	#region Public Methods
	
	/// Load the indicated (0-based) example code.
	public void LoadExample(int exampleNum) {
		myCodeMode = false;
		codeField.source = exampleCode[exampleNum].text;
	}
	
	/// Load the player's own code, as stored in player prefs.
	public void LoadMyCode() {
		myCodeMode = true;
		if (myCode != null) codeField.source = myCode;
		else codeField.source = PlayerPrefs.GetString("myCode");
	}
	
	/// If we are currently in "my code" mode, save the current text
	/// and schedule it to be stored shortly (but try not to hit the
	/// disk on every keypress!).
	public void NoteTextChange() {
		if (!myCodeMode) return;
		myCode = codeField.source;
		myCodeStoreTime = Time.time + 2f;
	}
	
	public void Run() {
		output.Clear();
		target.interpreter.standardOutput = (string s) => output.PrintLine(s);
		target.interpreter.implicitOutput = null;
		target.interpreter.errorOutput = (string s) => {
			Debug.LogWarning(s);
			output.PrintLine("<color=red>" + s + "</color>");
			target.interpreter.Stop();
		};
		
		target.RunScript(codeField.source);
	}
	
	public void Restart() {
		target.interpreter.Restart();
		output.Clear();
		
	}
	
	#endregion
	//--------------------------------------------------------------------------------
	#region Private Methods

	#endregion
}
