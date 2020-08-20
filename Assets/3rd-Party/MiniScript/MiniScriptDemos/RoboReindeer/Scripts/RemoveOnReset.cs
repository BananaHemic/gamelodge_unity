using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

public class RemoveOnReset : MonoBehaviour {
	#region Public Properties

	#endregion
	//--------------------------------------------------------------------------------
	#region Private Properties

	#endregion
	//--------------------------------------------------------------------------------
	#region MonoBehaviour Events
	void Start() {
	
	}
	
	void Update() {
	
	}

	#endregion
	//--------------------------------------------------------------------------------
	#region Public Methods
	
	public void GameReset() {
		Object.Destroy(gameObject);
	}
	
	#endregion
	//--------------------------------------------------------------------------------
	#region Private Methods

	#endregion
}
