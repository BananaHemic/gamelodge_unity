using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using Miniscript;

public class ReindeerScript : MonoBehaviour {
	#region Public Properties
	
	public string lastError;
	
	#endregion
	//--------------------------------------------------------------------------------
	#region Private Properties
	Interpreter interpreter;
	Reindeer reindeer;
	ValMap positionMap;
	ValMap actualMap;
	ReindeerGame game;
	
	static ValString xStr = ValString.Create("x");
	static ValString yStr = ValString.Create("y");
	static ValString distanceStr = ValString.Create("distance");
	static ValString directionStr = ValString.Create("direction");
	static ValString headingStr = ValString.Create("heading");
	static ValString speedStr = ValString.Create("speed");
	static ValString energyStr = ValString.Create("energy");
	static ValString healthStr = ValString.Create("health");
	static bool intrinsicsAdded = false;
	
	static ValNumber deerCount=null;
	static int deerCountFrame=-1;		// frame num on which deerCount was last updated
	
	#endregion
	//--------------------------------------------------------------------------------
	#region MonoBehaviour Events
	void Awake() {
		AddIntrinsics();
		
		reindeer = GetComponent<Reindeer>();
		
		interpreter = new Interpreter();
		interpreter.hostData = this;
		interpreter.standardOutput = (string s) => reindeer.Say(s);
		interpreter.implicitOutput = (string s) => reindeer.Say(
			"<color=#66bb66>" + s + "</color>");
		interpreter.errorOutput = (string s) => {
			reindeer.Say("<color=red>" + s + "</color>");
			interpreter.Stop();
			lastError = s;
		};
	}
	
	void Start() {
		game = GameObject.FindObjectOfType<ReindeerGame>();
	}
	
	void Update() {
		if (interpreter.Running()) {
			UpdateScriptFromSelf();
			if (reindeer.energy > 0) {
				try {
					interpreter.RunUntilDone(0.01);
				} catch (Miniscript.MiniscriptException err) {
					reindeer.health = 0;
					lastError = err.Description();
					reindeer.killedBy = lastError;
					Debug.Log("Update error: " + lastError);
					return;
				}
			}
		}
		UpdateSelfFromScript();
	}

	#endregion
	//--------------------------------------------------------------------------------
	#region Public Methods
	
	public void UpdateSelfFromScript() {
		Value val = interpreter.GetGlobalValue("heading");
		if (val != null) reindeer.targetAngle = val.FloatValue() % 360f;
		
		val = interpreter.GetGlobalValue("speed");
		if (val != null) reindeer.targetSpeed = val.FloatValue() * 0.01f * reindeer.maxSpeed;	
	}
	
	public void UpdateScriptFromSelf() {		
		if (positionMap == null) positionMap = ValMap.Create();
		positionMap.SetElem(xStr,  ValNumber.Create(transform.position.x));
		positionMap.SetElem(yStr, ValNumber.Create(transform.position.y));
		interpreter.SetGlobalValue("position", positionMap);
		
		if (actualMap == null) actualMap = ValMap.Create();
		actualMap.SetElem(headingStr, ValNumber.Create(Mathf.Round(reindeer.curAngle)));
		actualMap.SetElem(speedStr, ValNumber.Create(Mathf.Round(reindeer.curSpeed / reindeer.maxSpeed * 100f)));
		interpreter.SetGlobalValue("actual", actualMap);
		
		interpreter.SetGlobalValue("energy", ValNumber.Create(Mathf.Round(reindeer.energy)));
		interpreter.SetGlobalValue("health", ValNumber.Create(reindeer.health));
		interpreter.SetGlobalValue("deerCount", GetDeerCount());
		interpreter.SetGlobalValue("heading", ValNumber.Create(Mathf.Round(reindeer.targetAngle)));
	}
	
	public void RunScript(string miniscript) {
		interpreter.Reset(miniscript);
		try {
			interpreter.Compile();
			lastError = null;
		} catch (Miniscript.MiniscriptException err) {
			reindeer.health = 0;
			reindeer.killedBy = err.Description();
			lastError = err.Description();
			Debug.Log("RunScript error: " + lastError);
			return;
		}
		interpreter.SetGlobalValue("heading", ValNumber.Create(reindeer.targetAngle));
		interpreter.SetGlobalValue("speed", ValNumber.Create(reindeer.targetSpeed));
		UpdateScriptFromSelf();
	}
	
	public void StopScript() {
		interpreter.Reset();
	}
		
	#endregion
	//--------------------------------------------------------------------------------
	#region Private Methods
	
	ValNumber GetDeerCount() {
		if (Time.frameCount == deerCountFrame && deerCount != null) return deerCount;
		int count = 0;
		foreach (Reindeer rd in game.deer) {
			if (rd.gameObject.activeSelf) count++;
		}
		deerCountFrame = Time.frameCount;
		deerCount = ValNumber.Create(count);
		return deerCount;
	}
	
	static void AddIntrinsics() {
		if (intrinsicsAdded) return;		// already done!
		intrinsicsAdded = true;
		
		Intrinsic f;
		
		// look
		// finds the closest enemy within a narrow angle of our current heading,
		// and returns a map with: distance, direction, heading, speed, energy, health.
		f = Intrinsic.Create("look");
		f.code = (context, partialResult) => {
			ReindeerScript rs = context.interpreter.hostData as ReindeerScript;
			Vector2 here = rs.transform.position;
			Vector2 forward = rs.transform.right;
			Reindeer reindeer = rs.reindeer;
			ValMap result = null;
			float resultDist = 0;
			foreach (Reindeer deer in rs.game.deer) {
				if (deer == reindeer) continue;
				if (!deer.gameObject.activeSelf) continue;
				Vector2 dvec = (Vector2)deer.transform.position - here;
				float ang = Vector2.Angle(dvec, forward);
				if (ang < 5) {
					float dist = dvec.magnitude;
					if (result == null || dist < resultDist) {
						float direction = Mathf.Atan2(dvec.y, dvec.x) * Mathf.Rad2Deg;
						if (result == null) result = ValMap.Create();
						resultDist = dist;
						// On second thought, not including direction, as this makes
						// aiming *too* precise.
						//result.SetElem(directionStr, new ValNumber(direction));
						result.SetElem(distanceStr, ValNumber.Create(Mathf.Round(dist)));
						result.SetElem(headingStr, ValNumber.Create(deer.curAngle));
						result.SetElem(speedStr, ValNumber.Create(deer.curSpeed));
						result.SetElem(energyStr, ValNumber.Create(Mathf.Round(deer.energy)));
						result.SetElem(healthStr, ValNumber.Create(deer.health));
					}
				}
			}
			return new Intrinsic.Result(result);
		};
		
		// throw(energy=20)
		// fires a snowball in our current heading; returns 1 if success, 0 if failed.
		f = Intrinsic.Create("throw");
		f.AddParam("energy", ValNumber.Create(20));
		f.code = (context, partialResult) => {
			ReindeerScript rs = context.interpreter.hostData as ReindeerScript;
			Reindeer reindeer = rs.reindeer;
			float eCost = (float)context.GetVar("energy").DoubleValue();
			if (reindeer.ThrowSnowball(eCost)) return new Intrinsic.Result(ValNumber.one);
			return new Intrinsic.Result(ValNumber.zero);
		};
		
		// drop(energy=20, delay=5)
		// drops a meadow mine; returns 1 if success, 0 if failed
		f = Intrinsic.Create("drop");
		f.AddParam("energy", ValNumber.Create(20));
		f.AddParam("delay", ValNumber.Create(5));
		f.code = (context, partialResult) => {
			ReindeerScript rs = context.interpreter.hostData as ReindeerScript;
			Reindeer reindeer = rs.reindeer;
			float eCost = (float)context.GetVar("energy").DoubleValue();
			float delay = (float)context.GetVar("delay").DoubleValue();
			if (reindeer.LayMine(eCost, delay)) return new Intrinsic.Result(ValNumber.one);
			return new Intrinsic.Result(ValNumber.zero);
		};
		
	}
	
	#endregion
}
