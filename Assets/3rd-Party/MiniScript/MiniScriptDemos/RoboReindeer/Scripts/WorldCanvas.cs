using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(Canvas))]

public class WorldCanvas : MonoBehaviour {
	
	static WorldCanvas _instance;
	
	void Awake() {
		_instance = this;
	}
	
	protected void OnDestroy() {
		_instance = null;
	}
	
	public static void Position(RectTransform item, Vector3 worldPos) {
		item.anchorMin = item.anchorMax = Vector2.zero;
		Canvas canvas = _instance.GetComponent<Canvas>();
		item.transform.SetParent(canvas.transform, false);
		item.anchoredPosition = canvas.worldCamera.WorldToScreenPoint(worldPos);
	}
	
}
