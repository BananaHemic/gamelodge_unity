using UnityEngine;
using System.Collections;

public class ReindeerFiles : MonoBehaviour {
	
	[Multiline]
	public string defaultData = "print(\"Hello world!\")";
	
	
	public void LoadIntoDeer(string data, Reindeer deer) {
		string[] lines = data.Trim().Split(new string[] { "\r\n", "\n" }, 2, 
			System.StringSplitOptions.None);
		string firstLine = lines[0];
		if (firstLine.StartsWith("// Name:")) {
			deer.name = firstLine.Substring(8).Trim();
		}
		deer.miniscript = data;
	}
	
	public void LoadSlot(int slot, Reindeer deer) {
		string key = "DEER" + slot;
		string data = PlayerPrefs.GetString(key);
		if (string.IsNullOrEmpty(data)) {
			//Debug.Log("Loading default data for " + key);
			data = defaultData;
		} else {
			//Debug.Log("Loaded data from prefs for " + key);
		}
		LoadIntoDeer(data, deer);
	}
	
	public void SaveSlot(int slot, Reindeer deer) {
		if (deer == null) return;
		string key = "DEER" + slot;
		string data = deer.miniscript.Trim();
		if (!data.StartsWith("// Name:")) {
			data = "// Name: " + deer.name + System.Environment.NewLine + data;
		}
		PlayerPrefs.SetString(key, data);
		//Debug.Log("Stored data for " + key);
	}
}
