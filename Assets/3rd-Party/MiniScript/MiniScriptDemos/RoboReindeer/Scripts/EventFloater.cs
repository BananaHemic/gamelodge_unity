using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(Text))]

public class EventFloater : MonoBehaviour {
	
	public float duration = 2;
	public float floatHeight = 20;
	
	Graphic graphic;
	RectTransform rectTransform;
	float startTime;
	float startY;
	
	void Start () {
		graphic = GetComponent<Graphic>();
		rectTransform = transform as RectTransform;
		startTime = Time.time;
		startY = rectTransform.anchoredPosition.y;
	}
	
	void Update () {
		float t = (Time.time - startTime) / duration;
		if (t > 1) {
			Destroy(gameObject);
			return;
		}
		Vector2 pos = rectTransform.anchoredPosition;
		pos.y = Mathf.Lerp(startY, startY + floatHeight, t);
		rectTransform.anchoredPosition = pos;
		
		Color c = graphic.color;
		c.a = Mathf.Lerp(1, 0, t);
		graphic.color = c;
	}

}
