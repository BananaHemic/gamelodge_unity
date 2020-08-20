using UnityEngine;
using System.Collections;

public interface TakesProjectileDamage {
	// Take damage from the given projectile and return true,
	// or ignore it and return false.  (The projectile will 
	// automatically be destroyed if you return true.)
	bool TakeProjectileDamage(float damage, Projectile projectile);
}

public class Projectile : MonoBehaviour {
	
	public float speed = 50;
	public float damage = 30;
	public string throwerName;
	bool dead = false;
	
	// Update is called once per frame
	void Update () {
		if (dead) return;
		transform.Translate(Vector3.right * speed * Time.deltaTime);
		if (transform.position.x > 50 || transform.position.y > 50 ||
			transform.position.x < - 50 || transform.position.y < -50) {
			// Out of bounds; die silently.
			Destroy(gameObject);
		}
	}
	
	protected void OnTriggerEnter(Collider other) {
		if (dead) return;
		//Debug.Log(gameObject.name + " hit: " + other.gameObject.name);
		TakesProjectileDamage victim = other.GetComponent<TakesProjectileDamage>();
		if (victim != null && victim.TakeProjectileDamage(damage, this)) {
			// We've hit something, so it's time to play our sound and die.
			AudioSource aud = GetComponent<AudioSource>();
			if (aud != null && aud.clip != null) {
				aud.Play();
				GetComponent<Renderer>().enabled = false;	// hide for now...
				dead = true;
				Destroy(gameObject, aud.clip.length);		// destroy when sound is done
			}
		}
	}
}
