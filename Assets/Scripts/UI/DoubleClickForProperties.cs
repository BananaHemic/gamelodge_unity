using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RLD;

public class DoubleClickForProperties : MonoBehaviour
{
    public CanvasGroup ModelContainer;
    public CanvasGroup PropertiesAndCodeContainer;
    public PropertiesAndBehaviors PropertiesAndCodeObj;

    public enum BuildWindows
    {
        Models,
        CodeAndProperties
    }
    private Coroutine _transitionRoutine;
    private float _progress;
    public bool IsTransitioning{ get; private set; }
    public BuildWindows ActiveBuildWindow{ get; private set; }
    //public Vector2 ModelContainerStartPos;
    //public Vector2 ModelContainerEndPos;
    //public Vector2 PropertiesStartPos;
    //public Vector2 PropertiesEndPos;

    private Vector2 _initialModelPosition;
    private Vector2 _initialPropPosition;

    void Start()
    {
        RTObjectSelection.Get.PreSelectCustomize += Get_PreSelectCustomize;
        RTObjectSelection.Get.PreDeselectCustomize += Get_PreDeselectCustomize;

        //Debug.Log((ModelContainer.transform as RectTransform).anchoredPosition);
        //Debug.Log((PropertiesAndCodeContainer.transform as RectTransform).anchoredPosition);
        _initialModelPosition = (ModelContainer.transform as RectTransform).anchoredPosition;
        _initialPropPosition = (PropertiesAndCodeContainer.transform as RectTransform).anchoredPosition;
    }

    private float _lastClickTime = -DoubleClickDuration;
    private Transform _selectedParent;
    const float DoubleClickDuration = 0.5f;
    public float TransitionTime = 0.150f;

    private Transform GetSerializedParentFromModel(Transform model)
    {
        if (model == null)
            return null;
        Transform parent = model.parent;
        if (parent == null)
            return null;
        // Iterate until end of lineage, or we find a scene object
        while (parent.GetComponent<SceneObject>() == null)
        {
            parent = parent.parent;
            if (parent == null)
                break;
        }
        return parent;
    }
    private float LinearProgress2Animated(float progress)
    {
        return (float)BezierCurve.GetStandardBezierCurve().Solve(progress, TransitionTime);
    }
    private IEnumerator TransitionBuildWindow()
    {
        IsTransitioning = true;
        if (_progress >= 1 || _progress == 0)
            _progress = 0;
        else
            _progress = 1 - _progress;

        Debug.Log("Transitioning to " + ActiveBuildWindow);

        // Turn all modes on
        ModelContainer.gameObject.SetActive(true);
        PropertiesAndCodeContainer.gameObject.SetActive(true);

        RectTransform modelTransform = ModelContainer.transform as RectTransform;
        RectTransform propTransform = PropertiesAndCodeContainer.transform as RectTransform;

        while (_progress <= 1)
        {
            float modelProgress = 0;
            float propProgress = 0;
            if(ActiveBuildWindow == BuildWindows.Models)
            {
                modelProgress = _progress;
                propProgress = 1 - _progress;
            }else if(ActiveBuildWindow == BuildWindows.CodeAndProperties)
            {
                modelProgress = 1 - _progress;
                propProgress = _progress;
            }
            // Rebuild these each time, in case the user is resizing while this is occurring
            Vector2 activeModelPosition = new Vector2(_initialModelPosition.x, modelTransform.rect.height / 2f);
            Vector2 inactiveModelPosition = new Vector2(_initialModelPosition.x, -modelTransform.rect.height / 2f);
            //Vector2 activePropPosition = new Vector2(propTransform.rect.width / 2, _initialPropPosition.y);
            //Vector2 inactivePropPosition = new Vector2(-propTransform.rect.width / 2, _initialPropPosition.y);
            Vector2 activePropPosition = new Vector2(0, _initialPropPosition.y);
            Vector2 inactivePropPosition = new Vector2(-propTransform.rect.width, _initialPropPosition.y);

            ModelContainer.alpha = ColorUtils.LinearToGammaNormalized(modelProgress);
            PropertiesAndCodeContainer.alpha = ColorUtils.LinearToGammaNormalized(propProgress);
            propTransform.anchoredPosition = Vector2.Lerp(inactivePropPosition, activePropPosition, LinearProgress2Animated(propProgress));
            modelTransform.anchoredPosition = Vector2.Lerp(inactiveModelPosition, activeModelPosition, LinearProgress2Animated(modelProgress));

            _progress += Time.unscaledDeltaTime / TransitionTime;
            yield return null;
        }
        _progress = 1;
        if(ActiveBuildWindow == BuildWindows.Models)
        {
            ModelContainer.alpha = 1;
            PropertiesAndCodeContainer.gameObject.SetActive(false);
        }else if(ActiveBuildWindow == BuildWindows.CodeAndProperties)
        {
            PropertiesAndCodeContainer.alpha = 1;
            ModelContainer.gameObject.SetActive(false);
        }
        IsTransitioning = false;
    }
    private void OnDisable()
    {
        // Reset the modes
        if (IsTransitioning)
        {
            if (_transitionRoutine != null)
                StopCoroutine(_transitionRoutine);
            _transitionRoutine = null;

            _progress = 0;
            if(ActiveBuildWindow == BuildWindows.Models)
            {
                ModelContainer.alpha = 1;
                PropertiesAndCodeContainer.gameObject.SetActive(false);
            }else if(ActiveBuildWindow == BuildWindows.CodeAndProperties)
            {
                PropertiesAndCodeContainer.alpha = 1;
                PropertiesAndCodeContainer.gameObject.SetActive(false);
            }
        }
    }
    private void OpenModelsWindow()
    {
        // No transition if we're already at models
        if (ActiveBuildWindow == BuildWindows.Models)
            return;
        ActiveBuildWindow = BuildWindows.Models;
        if (_transitionRoutine != null)
            StopCoroutine(_transitionRoutine);
        _transitionRoutine = StartCoroutine(TransitionBuildWindow());
    }
    private void OpenCodeAndPropertiesWindow(Transform selected)
    {
        // Tell the CodeAndProperties window which object we have selected
        SceneObject sceneObject = selected.GetComponent<SceneObject>();
        PropertiesAndCodeObj.InitForSelectedObject(sceneObject);

        // No transition if we're already at Code And Properties
        if (ActiveBuildWindow == BuildWindows.CodeAndProperties)
            return;
        ActiveBuildWindow = BuildWindows.CodeAndProperties;
        if (_transitionRoutine != null)
            StopCoroutine(_transitionRoutine);
        _transitionRoutine = StartCoroutine(TransitionBuildWindow());
    }
    private void Get_PreSelectCustomize(ObjectPreSelectCustomizeInfo customizeInfo, List<GameObject> toBeSelected)
    {
        //Debug.Log("Pre select");
        // Only care if both clicks are down
        if (!RTInputDevice.Get.Device.WasButtonPressedInCurrentFrame(RTObjectSelection._objectPickDeviceBtnIndex))
            return;

        //Debug.Log("Selected: " + toBeSelected.Count);
        if(toBeSelected.Count != 1)
        {
            _lastClickTime = -DoubleClickDuration;
            _selectedParent = null;
            return;
        }
        Transform selected = GetSerializedParentFromModel(toBeSelected[0].transform);

        if(Time.unscaledTime - _lastClickTime <= DoubleClickDuration
            && _selectedParent == selected)
        {
            //Debug.Log("double click on " + selected);
            OpenCodeAndPropertiesWindow(selected);

            _lastClickTime = -DoubleClickDuration;
            _selectedParent = null;
        }
        else
        {
            _selectedParent = selected;
            _lastClickTime = Time.unscaledTime;
        }
    }
    private void Get_PreDeselectCustomize(ObjectPreDeselectCustomizeInfo customizeInfo, List<GameObject> toBeDeselected)
    {
        //Debug.Log("Deselecting " + toBeDeselected.Count);
        // See if we're going to deselect everything
        if (!RTObjectSelection.Get.IsSelectionExactMatch(toBeDeselected))
            return;
        //Debug.Log("Deselecting all");

        // Switch to model mode when nothing is selected
        OpenModelsWindow();
    }

    private void Update()
    {

        if (ActiveBuildWindow != BuildWindows.Models
            && RTObjectSelection.Get.NumSelectedObjects == 0)
            OpenModelsWindow();

        //if (Input.GetKeyDown(KeyCode.Space))
        //{
            //var model = ModelContainer.transform as RectTransform;
            //Debug.Log("Model: " + model.anchoredPosition + " w: " + model.rect.width + " h: " + model.rect.height);
            //var prop = PropertiesAndCodeContainer.transform as RectTransform;
            //Debug.Log("Properties: " + prop.anchoredPosition + " w: " + prop.rect.width + " h: " + prop.rect.height);
        //}
    }
}
