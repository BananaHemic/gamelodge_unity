using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public abstract class SceneDraggable : MonoBehaviour, IDragHandler, IEndDragHandler
{
    protected bool _isDragging = false;
    protected abstract void OnDragBegin();
    protected abstract void OnDragEnd();
    public abstract void DrawDragOnUI(Vector3 mousePosition);
    public abstract void DrawDragOnTable(Vector3 mousePosition);
    public abstract void DrawDragOnSkybox(Vector3 mousePosition, Quaternion rotation);

    public void OnDrag(PointerEventData eventData)
    {
        if (!_isDragging)
        {
            OnDragBegin();
            UIManager.Instance.BeginDragUIElement(this);
            _isDragging = true;
        }
    }
    public void OnEndDrag(PointerEventData eventData)
    {
        _isDragging = false;
        OnDragEnd();
        UIManager.Instance.EndDragUIElement(this);
    }
}
