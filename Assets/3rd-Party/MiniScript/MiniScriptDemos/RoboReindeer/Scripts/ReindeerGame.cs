using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class ReindeerGame : MonoBehaviour {
	#region Public Properties
	
	const int kDeerLimit = 6;
	
	public Color[] colors = new Color[kDeerLimit];
	public ReindeerPanel panelPrototype;
	public Reindeer deerPrefab;
	public Image winPanel;
	
	public Reindeer[] deer = new Reindeer[kDeerLimit];
	
	#endregion
	//--------------------------------------------------------------------------------
	#region Private Properties
	
	ReindeerPanel[] panels = new ReindeerPanel[kDeerLimit];
	bool running = false;
	
	#endregion
	//--------------------------------------------------------------------------------
	#region MonoBehaviour Events
	void Start() {
		CreateDeer();
		CreatePanels();
		LoadReindeer();
		Reset();
	}
	
	void Update() {
		if (running) CheckForWin();
	}

	#endregion
	//--------------------------------------------------------------------------------
	#region Public Methods
	
	public void LoadReindeer() {
		ReindeerFiles rfiles = GetComponent<ReindeerFiles>();
		for (int i=0; i<kDeerLimit; i++) {
			rfiles.LoadSlot(i, deer[i]);
		}
	}
	
	public void SaveReindeer() {
		ReindeerFiles rfiles = GetComponent<ReindeerFiles>();
		for (int i=0; i<kDeerLimit; i++) {
			rfiles.SaveSlot(i, deer[i]);
		}
		
	}
	
	public void Reset() {
		winPanel.gameObject.SetActive(false);
		foreach (Reindeer rd in deer) {
			rd.Stop();
			rd.Reset();
		}
		foreach (RemoveOnReset ror in GameObject.FindObjectsOfType<RemoveOnReset>()) {
			ror.GameReset();
		}
		running = false;
	}
	
	public void StartGame() {
		winPanel.gameObject.SetActive(false);
		foreach (Reindeer rd in deer) {
			rd.Run();
		}
		running = true;
	}
	
	#endregion
	//--------------------------------------------------------------------------------
	#region Private Methods
	
	void CreateDeer() {
		for (int i=0; i<kDeerLimit; i++) {
			deer[i] = GameObject.Instantiate(deerPrefab) as Reindeer;
			deer[i].name = "Reindeer " + i;
			deer[i].color = colors[i];
			deer[i].gameObject.SetActive(false);
		}
	}
	
	void CreatePanels() {
		// Assume our panel prototype is in the right place and all set
		// up for deer #0.  Create the rest.
		panels[0] = panelPrototype;
		panels[0].deer = deer[0];
		Vector2 pos = (panels[0].transform as RectTransform).anchoredPosition;
		for (int i=1; i<kDeerLimit; i++) {
			pos.y -= 77;
			ReindeerPanel newPanel = GameObject.Instantiate(panelPrototype) as ReindeerPanel;
			newPanel.deer = deer[i];
			RectTransform rt = newPanel.GetComponent<RectTransform>();
			rt.transform.SetParent(panelPrototype.transform.parent, false);
			rt.anchoredPosition = pos;			
		}
	}
	
	void CheckForWin() {
		int aliveIdx = -1;
		for (int i=0; i<kDeerLimit; i++) {
			if (deer[i].health > 0) {
				if (aliveIdx >= 0) return;	// more than one deer alive
				aliveIdx = i;
			}
		}
		if (aliveIdx >= 0) {
			Debug.Log(deer[aliveIdx].name + " Wins!");
			winPanel.color = deer[aliveIdx].color;
			winPanel.transform.Find("Deer Name Text").GetComponent<TextMeshProUGUI>().text = deer[aliveIdx].name;
			winPanel.gameObject.SetActive(true);
		}
		running = false;
	}
	
	#endregion
}
