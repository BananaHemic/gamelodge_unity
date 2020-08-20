using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections.Generic;

public class ReindeerPanel : MonoBehaviour {
	#region Public Properties
	
	public Reindeer deer;
	
	public Graphic portrait;
	public Graphic deathX;
	public Text nameText;
	public Text energyText;
	public Text healthText;
	public Graphic speechBalloon;
	public Text speechText;
	public Text killedByText;
	
	public ReindeerEditPanel editPanel;
	
	#endregion
	//--------------------------------------------------------------------------------
	#region Private Properties
	
	
	#endregion
	//--------------------------------------------------------------------------------
	#region MonoBehaviour Events
	
	void LateUpdate() {
		UpdatePanel();
	}

	#endregion
	//--------------------------------------------------------------------------------
	#region Public Methods
	
	public void UpdatePanel() {
		if (deer == null) {
			portrait.color = Color.gray;
			nameText.text = "(none)";
			killedByText.gameObject.SetActive(false);
			speechBalloon.gameObject.SetActive(false);			
		} else {
			bool dead = (deer.health <= 0);
			portrait.color = deer.color;
			killedByText.gameObject.SetActive(dead);
			deathX.gameObject.SetActive(dead);
			if (dead) killedByText.text = "Killed by " + deer.killedBy;
			nameText.text = deer.name;
			energyText.text = string.Format("{0:0}", deer.energy);
			healthText.text = deer.health.ToString();
			if (string.IsNullOrEmpty(deer.lastOutput) || deer.lastOutputTime < Time.time - 5) {
				speechBalloon.gameObject.SetActive(false);
			} else {
				speechBalloon.gameObject.SetActive(true);
				speechText.text = deer.lastOutput;
			}
		}
	}
	
	public void OpenEditor() {
		editPanel.Show(deer);
	}
	
	#endregion
	//--------------------------------------------------------------------------------
	#region Private Methods

	#endregion
}
