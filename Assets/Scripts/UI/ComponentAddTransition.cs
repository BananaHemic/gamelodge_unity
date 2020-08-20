using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ComponentAddTransition : MonoBehaviour
{
    public Image ComponentAddBackground;
    public RectTransform ComponentAddObject;
    public BehaviorDialog ComponentAddDialog;
    //public Vector2 ComponentAddActivePosition;
    //public Vector2 ComponentAddInactivePosition;
    public float BackgroundActiveOpacity = 0.7490196f;
    public float BackgroundInactiveOpacity = 0;

    private Coroutine _transitionRoutine;
    private float _progress;
    public bool IsTransitioning{ get; private set; }
    public bool IsComponentAddOpen { get; private set; }
    public float TransitionTime = 0.250f;

    public void OpenComponentAdd()
    {
        if (_transitionRoutine != null)
            StopCoroutine(_transitionRoutine);
        _transitionRoutine = StartCoroutine(TransitionComponent(true));
    }
    public void CloseComponentAdd()
    {
        if (_transitionRoutine != null)
            StopCoroutine(_transitionRoutine);
        _transitionRoutine = StartCoroutine(TransitionComponent(false));
    }
    private float LinearProgress2Animated(float progress)
    {
        //TODO maybe a linear->gamma too?
        return (float)BezierCurve.GetStandardBezierCurve().Solve(progress, TransitionTime);
    }
    private IEnumerator TransitionComponent(bool isComponentAddOpen)
    {
        IsTransitioning = true;
        if (_progress >= 1 || _progress == 0)
            _progress = 0;
        else
            _progress = 1 - _progress;

        IsComponentAddOpen = isComponentAddOpen;

        Debug.Log("Transitioning to add on: " + IsComponentAddOpen);

        // Turn all modes on
        ComponentAddBackground.gameObject.SetActive(true);

        Vector2 activePos = Vector2.zero;
        while (_progress <= 1)
        {
            float modelProgress = 0;
            if(IsComponentAddOpen)
            {
                modelProgress = _progress;
            }else
            {
                modelProgress = 1 - _progress;
            }
            Vector2 inactivePos = new Vector2(0, -ComponentAddBackground.rectTransform.rect.height / 2 - ComponentAddObject.rect.height / 2);
            ComponentAddBackground.SetAlpha(Mathf.Lerp(BackgroundInactiveOpacity, BackgroundActiveOpacity, modelProgress));
            ComponentAddObject.anchoredPosition = Vector2.Lerp(inactivePos, activePos, LinearProgress2Animated(modelProgress));
            //ComponentAddObject.anchoredPosition = Vector2.Lerp(ComponentAddInactivePosition, ComponentAddActivePosition, LinearProgress2Animated(modelProgress));

            _progress += Time.unscaledDeltaTime / TransitionTime;
            yield return null;
        }
        _progress = 1;
        if(IsComponentAddOpen)
        {
            ComponentAddBackground.SetAlpha(BackgroundActiveOpacity);
            ComponentAddObject.anchoredPosition = activePos;
        }else
        {
            ComponentAddBackground.gameObject.SetActive(false);
            Vector2 inactivePos = new Vector2(0, -ComponentAddBackground.rectTransform.rect.height / 2 - ComponentAddObject.rect.height / 2);
            ComponentAddObject.anchoredPosition = inactivePos;
        }
        IsTransitioning = false;
    }
    //private void Update()
    //{
        //if (Input.GetKeyDown(KeyCode.P))
        //{
            //Debug.Log("Add pos: " + ComponentAddObject.anchoredPosition + " w: " + ComponentAddObject.rect.width + " h: " + ComponentAddObject.rect.height);
            //Debug.Log("holder h: " + ComponentAddBackground.rectTransform.rect.height);
        //}
    //}
}
