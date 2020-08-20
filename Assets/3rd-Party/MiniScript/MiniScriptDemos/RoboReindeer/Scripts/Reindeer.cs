using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

public class Reindeer : MonoBehaviour, TakesProjectileDamage {
	#region Public Properties
	
	[Multiline]
	public string miniscript;
	
	public Color color;
	public int health = 100;
	public float energy = 50;
	public string lastOutput;
	public float lastOutputTime;
	
	public float targetAngle;
	public float curAngle;
	public float targetSpeed;
	public float curSpeed;
	public string killedBy;
	
	public float turnSpeed = 360;
	public float acceleration = 100;
	public float maxSpeed = 2;
	public float energyGainRest = 1;
	public float energyGainMoving = -0.5f;
	public float chargeDamage = 20;
	
	public UnityEngine.UI.Text eventFloaterPrefab;
	public GameObject snowballPrefab;
	public GameObject minePrefab;
	public AudioClip crashSound;
	public AudioClip throwSound;
	public AudioClip plopSound;
	
	#endregion
	//--------------------------------------------------------------------------------
	#region Private Properties
	
	Material material;
	ReindeerScript rscript;
	bool running;
	
	#endregion
	//--------------------------------------------------------------------------------
	#region MonoBehaviour Events
	void Awake() {
		rscript = GetComponent<ReindeerScript>();		
	}
	
	void Start() {
		material = GetComponentInChildren<MeshRenderer>().material;
		material.color = color;
	}
	
	void Update() {
		if (!running) return;
		
		targetAngle = targetAngle % 360;
		if (targetAngle < 0) targetAngle += 360;
		
		curAngle = curAngle % 360;
		if (curAngle < 0) curAngle += 360;
		
		targetSpeed = Mathf.Clamp(targetSpeed, -0.5f * maxSpeed, maxSpeed);
		if (energy < 0) {
			curSpeed = 0;
		} else {
			if (curAngle != targetAngle) {
				curAngle = Mathf.MoveTowardsAngle(curAngle, targetAngle, turnSpeed * Time.deltaTime);
			}
			curSpeed = Mathf.MoveTowards(curSpeed, targetSpeed, acceleration * Time.deltaTime);
			if (energy < 5) curSpeed *= energy/5f;
		}
		
		float gainRate = Mathf.Lerp(energyGainRest, energyGainMoving, Mathf.Abs(curSpeed)/maxSpeed);
		energy += gainRate * Time.deltaTime;
		if (energy > 100) energy = 100;
		
		UpdateModel();
	}
	
	protected void OnTriggerEnter(Collider other) {
		// If the other object is a reindeer, and we're moving TOWARDS it at
		// reasonably high speed, then we'll count this as a charge and inflict
		// damage!
		if (curSpeed < maxSpeed/2) return;	// too slow
		Reindeer victim = other.GetComponent<Reindeer>();
		if (victim == null) return;		// didn't hit a reindeer
		Vector2 forward = transform.right;
		Vector2 dvec = victim.transform.position - transform.position;
		float angle = Vector2.Angle(forward, dvec);
		//Debug.Log(name + " hits " + other + " at speed " + curSpeed + " and angle " + angle);
		if (angle > 30) return;			// glancing blow
		
		float damage = chargeDamage * curSpeed/maxSpeed;
		victim.TakeCrashDamage(damage, name);
		
		GetComponent<AudioSource>().PlayOneShot(crashSound, curSpeed / maxSpeed);
	}
	
	#endregion
	//--------------------------------------------------------------------------------
	#region Public Methods
	
	public void Say(string s) {
		lastOutput = s;
		lastOutputTime = Time.time;
	}
	
	public void Reset() {
		transform.position = new Vector3(
			Random.Range(-40f, 40f), Random.Range(-40f,40f), 0);
		curAngle = targetAngle = Random.Range(0, 360);
		curSpeed = targetSpeed = 0;
		health = 100;
		energy = 50;
		lastOutput = null;
		gameObject.SetActive(true);
		running = false;
		killedBy = null;
	}
	
	public void Run() {
		running = true;
		rscript.RunScript(miniscript);
	}
	
	public void Stop() {
		running = false;
		rscript.StopScript();
	}
	
	public bool TakeDamage(float damage, string fromWhat="") {
		int intDam = Mathf.RoundToInt(damage);
		if (intDam < 1) return false;
		
		UnityEngine.UI.Text floater = GameObject.Instantiate(eventFloaterPrefab) as UnityEngine.UI.Text;
		floater.text = "-" + intDam;
		floater.color = color;
		WorldCanvas.Position(floater.rectTransform, transform.position);
		
		health -= intDam;
		if (health <= 0) Die(fromWhat);
		return true;
	}
	
	public bool TakeProjectileDamage(float damage, Projectile projectile) {
		return TakeDamage(damage, projectile.throwerName);
	}
	
	public bool TakeMineDamage(float damage, MeadowMine mine) {
		return TakeDamage(damage, "a mine");
	}
	
	public bool TakeCrashDamage(float damage, string crasher) {
		return TakeDamage(damage, crasher);
	}
	
	public bool ThrowSnowball(float ballEnergy) {
		if (energy < 0 || ballEnergy < 1) return false;
		energy -= ballEnergy;
		GameObject ball = GameObject.Instantiate(snowballPrefab) as GameObject;
		ball.GetComponent<MeshRenderer>().sharedMaterial = material;
		Projectile proj = ball.GetComponent<Projectile>();
		proj.damage = ballEnergy;
		proj.throwerName = name;
		Vector3 p = ball.transform.position;
		ball.transform.position = transform.TransformPoint(p);
		ball.transform.rotation = transform.rotation;
		GetComponent<AudioSource>().PlayOneShot(throwSound);
		return true;
	}
	
	public bool LayMine(float mineEnergy, float armTime) {
		if (energy < 0 || mineEnergy < 1) return false;
		energy -= mineEnergy;
		GameObject mine = GameObject.Instantiate(minePrefab) as GameObject;
		mine.GetComponent<MeshRenderer>().sharedMaterial = material;
		MeadowMine mm = mine.GetComponent<MeadowMine>();
		mm.damage = mineEnergy * 2;
		mm.armTime = armTime;
		Vector3 p = mine.transform.position;
		mine.transform.position = transform.TransformPoint(p);
		GetComponent<AudioSource>().PlayOneShot(plopSound);
		return true;		
	}
	
	public void Die(string fromWhat) {
		Stop();
		gameObject.SetActive(false);
		killedBy = fromWhat;
	}
	
	#endregion
	//--------------------------------------------------------------------------------
	#region Private Methods
	
	void UpdateModel() {
		transform.localRotation = Quaternion.Euler(0, 0, curAngle);
		if (curSpeed != 0) {
			transform.Translate(Vector3.right * curSpeed);
			float x = Mathf.Clamp(transform.position.x, -50, 50);
			float y = Mathf.Clamp(transform.position.y, -50, 50);
			transform.position = new Vector3(x, y, 0);
		}
	}
	
	#endregion
}
