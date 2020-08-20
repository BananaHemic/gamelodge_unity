using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class OptionPopupTransition : GenericSingleton<OptionPopupTransition>
{
    public float BackgroundActiveOpacity = 0.7490196f;
    public float BackgroundInactiveOpacity = 0;

    private Coroutine _transitionRoutine;
    private float _progress;
    public bool IsTransitioning { get; private set; }
    public bool IsComponentAddOpen { get; private set; }
    public float TransitionTime = 0.250f;

    public void OpenOptions()
    {
        if (_transitionRoutine != null)
            StopCoroutine(_transitionRoutine);
        _transitionRoutine = StartCoroutine(TransitionComponent(true));
    }
    public void CloseOptions()
    {
        if (!IsComponentAddOpen)
            return;
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

        //Debug.Log("Transitioning to add on: " + IsComponentAddOpen);

        // Turn it on
        OptionPopup.Instance.SetActive(true);

        Vector2 activePos = Vector2.zero;
        while (_progress <= 1)
        {
            float modelProgress = 0;
            if (IsComponentAddOpen)
            {
                modelProgress = _progress;
            }
            else
            {
                modelProgress = 1 - _progress;
            }
            //Vector2 inactivePos = new Vector2(0, -ComponentAddBackground.rectTransform.rect.height / 2 - ComponentAddObject.rect.height / 2);
            Vector2 inactivePos = new Vector2(0, -OptionPopup.Instance.Background.rectTransform.rect.height / 2 - OptionPopup.Instance.OptionContainer.rect.height / 2);
            //ComponentAddBackground.SetAlpha(Mathf.Lerp(BackgroundInactiveOpacity, BackgroundActiveOpacity, modelProgress));
            //ComponentAddObject.anchoredPosition = Vector2.Lerp(inactivePos, activePos, LinearProgress2Animated(modelProgress));
            OptionPopup.Instance.Background.SetAlpha(Mathf.Lerp(BackgroundInactiveOpacity, BackgroundActiveOpacity, modelProgress));
            OptionPopup.Instance.OptionContainer.anchoredPosition = Vector2.Lerp(inactivePos, activePos, LinearProgress2Animated(modelProgress));

            _progress += Time.unscaledDeltaTime / TransitionTime;
            yield return null;
        }
        _progress = 1;
        if (IsComponentAddOpen)
        {
            //ComponentAddBackground.SetAlpha(BackgroundActiveOpacity);
            //ComponentAddObject.anchoredPosition = activePos;
            OptionPopup.Instance.Background.SetAlpha(BackgroundActiveOpacity);
            OptionPopup.Instance.OptionContainer.anchoredPosition = activePos;
        }
        else
        {
            //ComponentAddBackground.gameObject.SetActive(false);
            //Vector2 inactivePos = new Vector2(0, -ComponentAddBackground.rectTransform.rect.height / 2 - ComponentAddObject.rect.height / 2);
            //ComponentAddObject.anchoredPosition = inactivePos;
            //OptionPopup.Instance.Background.gameObject.SetActive(false);
            Vector2 inactivePos = new Vector2(0, -OptionPopup.Instance.Background.rectTransform.rect.height / 2 - OptionPopup.Instance.OptionContainer.rect.height / 2);
            OptionPopup.Instance.OptionContainer.anchoredPosition = inactivePos;
            // Turn it off
            OptionPopup.Instance.SetActive(false);
        }
        IsTransitioning = false;
    }
}
