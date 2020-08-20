/*	
This class demonstrates how to position a UI element over a
given world position.  It's actually quite simple, IF you ensure
a couple of  things:
	1. The UI element must have all anchors set to 0.
	2. The canvas must be in Screen Space - Camera mode.
*/

using UnityEngine;
using System.Collections;

public class WorldPosUITracker : MonoBehaviour {
	
	public Transform target;
	
	void Start() {
		RectTransform rt = transform as RectTransform;
		rt.anchorMin = rt.anchorMax = Vector2.zero;
	}
	
	void Update () {
		RectTransform rt = transform as RectTransform;
		Vector3 worldPt = target.transform.position;
		rt.anchoredPosition = Camera.main.WorldToScreenPoint(worldPt);
	}
}
