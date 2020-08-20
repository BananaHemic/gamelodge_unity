using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class Output : MonoBehaviour {

	public int charLimit = 4096;

	Text outputText;

	static Output _defaultInstance;

	void Awake() {
		if (_defaultInstance == null) _defaultInstance = this;
		outputText = GetComponent<Text>();

	}

	public void PrintLine(string line) {
		string t = outputText.text + "\n" + line;
		if (t.Length > charLimit) t = t.Substring(t.Length - charLimit);
		outputText.text = t;
	}
	
	public void Clear() {
		outputText.text = "";
	}
	
	public static void Print(string line) {
		_defaultInstance.PrintLine(line);
	}
	
}
