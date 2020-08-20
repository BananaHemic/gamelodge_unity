using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;
using Miniscript;

public class ScriptableButton : EventTrigger {
	
	// Careful: EventTrigger subclasses don't show their public properties
	// in the Inspector as they should.  This is a bug (or design flaw) in
	// Unity.  So, you have to switch the Inspector to Debug mode to view
	// or change these.
	public InputField sourceField;
	public Output output;
	public TextAsset initialSource;
	
	Interpreter interpreter;
	bool suppressErrorOutput;
	
	void Start() {
		sourceField.text = initialSource.text;
		ResetScript();
	}
	
	public override void OnBeginDrag( PointerEventData data ) {
		Invoke("onBeginDrag");
	}
	
	public override void OnCancel( BaseEventData data ) {
		Invoke("onCancel");
	}
	
	public override void OnDeselect( BaseEventData data ) {
		Invoke("onDeselect");
	}
	
	public override void OnDrag( PointerEventData data ) {
		Invoke("onDrag");
	}
	
	public override void OnDrop( PointerEventData data ) {
		Invoke("onDrop");
	}
	
	public override void OnEndDrag( PointerEventData data ) {
		Invoke("onEndDrag");
	}
	
	public override void OnInitializePotentialDrag( PointerEventData data ) {
		Invoke("onInitializePotentialDrag");
	}
	
	public override void OnMove( AxisEventData data ) {
		Invoke("onMove");
	}
	
	public override void OnPointerClick( PointerEventData data ) {
		Invoke("onPointerClick");
	}
	
	public override void OnPointerDown( PointerEventData data ) {
		Invoke("onPointerDown");
	}
	
	public override void OnPointerEnter( PointerEventData data ) {
		Invoke("onPointerEnter");
	}
	
	public override void OnPointerExit( PointerEventData data ) {
		Invoke("onPointerExit");
	}
	
	public override void OnPointerUp( PointerEventData data ) {
		Invoke("onPointerUp");
	}
	
	public override void OnScroll( PointerEventData data ) {
		Invoke("onScroll");
	}
	
	public override void OnSelect( BaseEventData data ) {
		Invoke("onSelect");
	}
	
	public override void OnSubmit( BaseEventData data ) {
		Invoke("onSubmit");
	}
	
	public override void OnUpdateSelected( BaseEventData data ) {
		Invoke("onUpdateSelected");
	}
	
	public void ResetScript() {
		if (interpreter == null) {
			interpreter = new Interpreter();
			interpreter.standardOutput = (string s) => output.PrintLine(s);
			interpreter.errorOutput = (string s) => {
				if (!suppressErrorOutput) {
					output.PrintLine("<color=red>" + s + "</color>");
					suppressErrorOutput = true;
				}
				interpreter.Stop();
			};
		}
		
		// Grab the source code from the user
		string sourceCode = sourceField.text;
		
		// Append our secret sauce: the main event loop.
		sourceCode += @"
_events = []
while 1
  if _events.len > 0 then
    _nextEvent = _events.pull
    _nextEvent
  end if
end while";
		
		// Reset the interpreter with this combined source code.
		interpreter.Reset(sourceCode);
		interpreter.Compile();
		suppressErrorOutput = false;
	}
	
	void Update() {
		if (!interpreter.Running()) interpreter.Restart();
		try {
			interpreter.RunUntilDone(0.01);
		} catch (MiniscriptException err) {
			if (!suppressErrorOutput) {
				output.PrintLine("<color=red>" + err.Description() + "</color>");
				suppressErrorOutput = true;
			}
		}
			
	}
	
	void Invoke(string funcName) {
		Debug.Log("Invoking: " + funcName);
		Value handler = interpreter.GetGlobalValue(funcName);
		if (handler == null) return;   // no handler defined
		var eventQ = interpreter.GetGlobalValue("_events") as ValList;
		if (eventQ == null) {     // make sure _events list exists!
			eventQ = ValList.Create();
			interpreter.SetGlobalValue("_events", eventQ);
            eventQ.Unref();
		}
		// Add a reference to the handler function onto the event queue.
		// The main event loop will pick this up and invoke it ASAP.
		eventQ.Add(handler); 
	}
	
	
}
