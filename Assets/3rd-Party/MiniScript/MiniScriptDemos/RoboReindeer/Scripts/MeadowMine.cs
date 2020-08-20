using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

public class MeadowMine : MonoBehaviour {
	#region Public Properties
	
	public float damage = 20;
	public float armTime = 5;	// how long after start before we arm the mine
	public Material armedMaterial;	// material to use once armed
	public bool isArmed {
		get { return Time.time - startTime > armTime; }
	}
	#endregion
	//--------------------------------------------------------------------------------
	#region Private Properties
	
	float startTime;
	float boomTime = 0;
	Transform boomSphere;
	List<GameObject> particles;
	Renderer _renderer;
	
	#endregion
	//--------------------------------------------------------------------------------
	#region MonoBehaviour Events
	void Start() {
		_renderer = GetComponent<Renderer>();
		startTime = Time.time;
		boomSphere = transform.Find("Boom");
		boomSphere.gameObject.SetActive(false);
		particles = new List<GameObject>();
		for (int i=0; i<transform.childCount; i++) {
			ParticleSystem p = transform.GetChild(i).GetComponent<ParticleSystem>();
			if (p != null) particles.Add(p.gameObject);
		}
	}
	
	void Update() {
		float t = Time.time - startTime;
		float lastT = Time.time - Time.deltaTime - startTime;
		if (t >= armTime && lastT < armTime) {
			_renderer.sharedMaterial = armedMaterial;
		}
		if (boomTime > 0) ContinueBoom();
	}
	
	protected void OnTriggerEnter(Collider other) {
		if (!isArmed) return;
		Reindeer deer = other.GetComponent<Reindeer>();
		if (deer == null) return;
		GoBoom();
		deer.TakeMineDamage(damage, this);
	}
	
	
	#endregion
	//--------------------------------------------------------------------------------
	#region Public Methods
	
	#endregion
	//--------------------------------------------------------------------------------
	#region Private Methods
	
	void GoBoom() {
		boomTime = Time.time;
		boomSphere.gameObject.SetActive(true);
		boomSphere.localScale = Vector3.one;
		foreach (GameObject gob in particles) {
			gob.SetActive(true);
		}
		Object.Destroy(gameObject, 1f);
		GetComponent<AudioSource>().Play();
	}
	
	void ContinueBoom() {
		// Lerp the boom sphere, and hide it and the sphere after some time
		float boomDuration = 0.3f;
		float t = (Time.time - boomTime) / boomDuration;
		if (t > 1 && boomSphere.gameObject.activeSelf) {
			boomSphere.gameObject.SetActive(false);
			_renderer.enabled = false;
		} else {
			boomSphere.localScale = Vector3.one * Mathf.Lerp(1f, 4f, t);
		}

	}
	#endregion
}
