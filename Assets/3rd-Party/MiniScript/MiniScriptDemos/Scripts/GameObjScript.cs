using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using Miniscript;

public class GameObjScript : MonoBehaviour {
	#region Public Properties
	
	public string globalVarName = "ship";
	public Interpreter interpreter;
	
	#endregion
	//--------------------------------------------------------------------------------
	#region Private Properties

	#endregion
	//--------------------------------------------------------------------------------
	#region MonoBehaviour Events
	void Awake() {
		interpreter = new Interpreter();
		
	}
	
	void Update() {
		if (interpreter.Running()) {
			interpreter.RunUntilDone(0.01);
		}
		UpdateFromScript();
	}

	#endregion
	//--------------------------------------------------------------------------------
	#region Public Methods
	
	public void UpdateFromScript() {
		ValMap data = null;
		try {
			data = interpreter.GetGlobalValue(globalVarName) as ValMap;
		} catch (UndefinedIdentifierException) {
			Debug.LogWarning(globalVarName + " not found in global context.");
		}
		if (data == null) return;
		
		Transform t = transform;
		Vector3 pos = t.localPosition;
		
		Value xval = data["x"];
		if (xval != null) pos.x = xval.FloatValue();
		Value yval = data["y"];
		if (yval != null) pos.y = yval.FloatValue();
		t.localPosition = pos;
		
		Value rotVal = data["rot"];
		if (rotVal != null) t.localRotation = Quaternion.Euler(0, 0, (float)rotVal.FloatValue());
	}
	
	public void RunScript(string sourceCode) {
		string extraSource = "ship.reset = function(); self.x=0; self.y=0; self.rot=0; end function\n";
		interpreter.Reset(extraSource + sourceCode);
		interpreter.Compile();
		ValMap data = ValMap.Create();
		data["x"] = ValNumber.Create(transform.localPosition.x);
		data["y"] = ValNumber.Create(transform.localPosition.y);
		data["rot"] = ValNumber.Create(transform.localRotation.z);
		interpreter.SetGlobalValue(globalVarName, data);
	}
	
	#endregion
	//--------------------------------------------------------------------------------
	#region Private Methods
	
	
	
	#endregion
}
