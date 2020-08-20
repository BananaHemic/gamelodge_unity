using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RLD;
using System.Security.Cryptography;

public class RLDHelper : GenericSingleton<RLDHelper>
{
    private readonly List<bool> _rldEnabledFields = new List<bool>();
    private readonly List<string> _selectedInputFields = new List<string>();
    private bool _hasInit = false;

    IEnumerator Start()
    {
        // Wait until RLD has init
        while (RTScene.Get == null)
            yield return null;
        // Sorta nasty to have an i++ here, but it works
        _rldEnabledFields.Add(RTSceneGrid.Get.Hotkeys.GridUp.IsEnabled);
        _rldEnabledFields.Add(RTSceneGrid.Get.Hotkeys.GridDown.IsEnabled);
        _rldEnabledFields.Add(RTSceneGrid.Get.Hotkeys.SnapToCursorPickPoint.IsEnabled);
        _rldEnabledFields.Add(RTSceneGrid.Get.Hotkeys.SnapToCursorPickPoint.IsEnabled);
        //RTObjectSelection.Get.Hotkeys.AppendToSelection
        //RTObjectSelection.Get.Hotkeys.MultiDeselect
        _rldEnabledFields.Add(RTObjectSelection.Get.Hotkeys.FocusCameraOnSelection.IsEnabled);
        _rldEnabledFields.Add(RTObjectSelection.Get.Hotkeys.DuplicateSelection.IsEnabled);
        _rldEnabledFields.Add(RTObjectSelection.Get.GrabHotkeys.ToggleGrab.IsEnabled);
        _rldEnabledFields.Add(RTObjectSelection.Get.GrabHotkeys.EnableRotation.IsEnabled);
        _rldEnabledFields.Add(RTObjectSelection.Get.GrabHotkeys.EnableRotationAroundAnchor.IsEnabled);
        _rldEnabledFields.Add(RTObjectSelection.Get.GrabHotkeys.EnableScaling.IsEnabled);
        _rldEnabledFields.Add(RTObjectSelection.Get.GrabHotkeys.EnableOffsetFromSurface.IsEnabled);
        _rldEnabledFields.Add(RTObjectSelection.Get.GrabHotkeys.EnableAnchorAdjust.IsEnabled);
        _rldEnabledFields.Add(RTObjectSelection.Get.GrabHotkeys.EnableOffsetFromAnchor.IsEnabled);
        _rldEnabledFields.Add(RTObjectSelection.Get.GrabHotkeys.NextAlignmentAxis.IsEnabled);
        _rldEnabledFields.Add(RTObjectSelection.Get.Object2ObjectSnapHotkeys.ToggleSnap.IsEnabled);
        _rldEnabledFields.Add(RTObjectSelection.Get.Object2ObjectSnapHotkeys.ToggleSitBelowSurface.IsEnabled);
        _rldEnabledFields.Add(RTObjectSelection.Get.Object2ObjectSnapHotkeys.EnableMoreControl.IsEnabled);
        _rldEnabledFields.Add(RTObjectSelection.Get.Object2ObjectSnapHotkeys.EnableFlexiSnap.IsEnabled);
        _rldEnabledFields.Add(RTObjectSelection.Get.RotationHotkeys.RotateAroundX.IsEnabled);
        _rldEnabledFields.Add(RTObjectSelection.Get.RotationHotkeys.RotateAroundY.IsEnabled);
        _rldEnabledFields.Add(RTObjectSelection.Get.RotationHotkeys.RotateAroundZ.IsEnabled);
        _rldEnabledFields.Add(RTObjectSelection.Get.RotationHotkeys.SetRotationToIdentity.IsEnabled);

        _rldEnabledFields.Add(RTObjectSelectionGizmos.Get.Hotkeys.ActivateMoveGizmo.IsEnabled);
        _rldEnabledFields.Add(RTObjectSelectionGizmos.Get.Hotkeys.ActivateRotationGizmo.IsEnabled);
        _rldEnabledFields.Add(RTObjectSelectionGizmos.Get.Hotkeys.ActivateScaleGizmo.IsEnabled);
        _rldEnabledFields.Add(RTObjectSelectionGizmos.Get.Hotkeys.ActivateBoxScaleGizmo.IsEnabled);
        _rldEnabledFields.Add(RTObjectSelectionGizmos.Get.Hotkeys.ActivateUniversalGizmo.IsEnabled);
        _rldEnabledFields.Add(RTObjectSelectionGizmos.Get.Hotkeys.ActivateExtrudeGizmo.IsEnabled);
        _rldEnabledFields.Add(RTObjectSelectionGizmos.Get.Hotkeys.ToggleTransformSpace.IsEnabled);
        _rldEnabledFields.Add(RTObjectSelectionGizmos.Get.MoveGizmoHotkeys.Enable2DMode.IsEnabled);
        _rldEnabledFields.Add(RTObjectSelectionGizmos.Get.MoveGizmoHotkeys.EnableSnapping.IsEnabled);
        _rldEnabledFields.Add(RTObjectSelectionGizmos.Get.MoveGizmoHotkeys.EnableVertexSnapping.IsEnabled);
        _rldEnabledFields.Add(RTObjectSelectionGizmos.Get.RotationGizmoHotkeys.EnableSnapping.IsEnabled);
        _rldEnabledFields.Add(RTObjectSelectionGizmos.Get.RotationGizmoHotkeys.EnableSnapping.IsEnabled);
        _rldEnabledFields.Add(RTObjectSelectionGizmos.Get.ScaleGizmoHotkeys.EnableSnapping.IsEnabled);
        _rldEnabledFields.Add(RTObjectSelectionGizmos.Get.ScaleGizmoHotkeys.ChangeMultiAxisMode.IsEnabled);
        _rldEnabledFields.Add(RTObjectSelectionGizmos.Get.BoxScaleGizmoHotkeys.EnableSnapping.IsEnabled);
        _rldEnabledFields.Add(RTObjectSelectionGizmos.Get.BoxScaleGizmoHotkeys.EnableCenterPivot.IsEnabled);
        _rldEnabledFields.Add(RTObjectSelectionGizmos.Get.UniversalGizmoHotkeys.Enable2DMode.IsEnabled);
        _rldEnabledFields.Add(RTObjectSelectionGizmos.Get.UniversalGizmoHotkeys.EnableSnapping.IsEnabled);
        _rldEnabledFields.Add(RTObjectSelectionGizmos.Get.UniversalGizmoHotkeys.EnableVertexSnapping.IsEnabled);
        _rldEnabledFields.Add(RTObjectSelectionGizmos.Get.ExtrudeGozmoHotkeys.EnableOverlapTest.IsEnabled);

        _rldEnabledFields.Add(RTFocusCamera.Get.Hotkeys.MoveForward.IsEnabled);
        _rldEnabledFields.Add(RTFocusCamera.Get.Hotkeys.MoveBack.IsEnabled);
        _rldEnabledFields.Add(RTFocusCamera.Get.Hotkeys.StrafeLeft.IsEnabled);
        _rldEnabledFields.Add(RTFocusCamera.Get.Hotkeys.StrafeRight.IsEnabled);
        _rldEnabledFields.Add(RTFocusCamera.Get.Hotkeys.MoveUp.IsEnabled);
        _rldEnabledFields.Add(RTFocusCamera.Get.Hotkeys.MoveDown.IsEnabled);
        _rldEnabledFields.Add(RTFocusCamera.Get.Hotkeys.Pan.IsEnabled);
        _rldEnabledFields.Add(RTFocusCamera.Get.Hotkeys.LookAround.IsEnabled);
        _rldEnabledFields.Add(RTFocusCamera.Get.Hotkeys.Orbit.IsEnabled);
        _rldEnabledFields.Add(RTFocusCamera.Get.Hotkeys.AlternateMoveSpeed.IsEnabled);

        _rldEnabledFields.Add(RTUndoRedo.Get.ReceiveUndoRedoInput);

        _hasInit = true;
        // inputs may have been selected/deselected while we were waiting for RLD initialization
        if (_selectedInputFields.Count > 0)
            SetRLDReceiveKeyboardInput(false);
    }

    /// <summary>
    /// We want to be robust to input field select/deselect messages occuring out of order
    /// To do this, we keep track of every object that has an input selected.
    /// Only when there are no input fields selected do the RLD controls turn on
    /// </summary>
    /// <param name="selectedInputFieldIdentifier"></param>
    public void RegisterInputSelected(string selectedInputFieldIdentifier)
    {
        if(!_selectedInputFields.Contains(selectedInputFieldIdentifier))
            _selectedInputFields.Add(selectedInputFieldIdentifier);

        if (_selectedInputFields.Count > 0)
            SetRLDReceiveKeyboardInput(false);
    }
    public void RegisterInputDeselected(string deselectedInputFieldIdentifier)
    {
        if (!_selectedInputFields.RemoveBySwap(deselectedInputFieldIdentifier))
            return;

        if (_selectedInputFields.Count == 0)
            SetRLDReceiveKeyboardInput(true);
    }

    private void SetRLDReceiveKeyboardInput(bool shouldReceive)
    {
        if (Orchestrator.Instance.IsAppClosing)
            return;
        if (!_hasInit)
            return;
        //Debug.Log("Setting RLD keyboard input to " + shouldReceive);
        int i = 0;
        // Sorta nasty to have an i++ here, but it works
        RTSceneGrid.Get.Hotkeys.GridUp.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTSceneGrid.Get.Hotkeys.GridDown.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTSceneGrid.Get.Hotkeys.SnapToCursorPickPoint.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTSceneGrid.Get.Hotkeys.SnapToCursorPickPoint.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        //RTObjectSelection.Get.Hotkeys.AppendToSelection
        //RTObjectSelection.Get.Hotkeys.MultiDeselect
        RTObjectSelection.Get.Hotkeys.FocusCameraOnSelection.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTObjectSelection.Get.Hotkeys.DuplicateSelection.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTObjectSelection.Get.GrabHotkeys.ToggleGrab.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTObjectSelection.Get.GrabHotkeys.EnableRotation.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTObjectSelection.Get.GrabHotkeys.EnableRotationAroundAnchor.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTObjectSelection.Get.GrabHotkeys.EnableScaling.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTObjectSelection.Get.GrabHotkeys.EnableOffsetFromSurface.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTObjectSelection.Get.GrabHotkeys.EnableAnchorAdjust.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTObjectSelection.Get.GrabHotkeys.EnableOffsetFromAnchor.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTObjectSelection.Get.GrabHotkeys.NextAlignmentAxis.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTObjectSelection.Get.Object2ObjectSnapHotkeys.ToggleSnap.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTObjectSelection.Get.Object2ObjectSnapHotkeys.ToggleSitBelowSurface.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTObjectSelection.Get.Object2ObjectSnapHotkeys.EnableMoreControl.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTObjectSelection.Get.Object2ObjectSnapHotkeys.EnableFlexiSnap.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTObjectSelection.Get.RotationHotkeys.RotateAroundX.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTObjectSelection.Get.RotationHotkeys.RotateAroundY.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTObjectSelection.Get.RotationHotkeys.RotateAroundZ.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTObjectSelection.Get.RotationHotkeys.SetRotationToIdentity.IsEnabled = shouldReceive && _rldEnabledFields[i++];

        RTObjectSelectionGizmos.Get.Hotkeys.ActivateMoveGizmo.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTObjectSelectionGizmos.Get.Hotkeys.ActivateRotationGizmo.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTObjectSelectionGizmos.Get.Hotkeys.ActivateScaleGizmo.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTObjectSelectionGizmos.Get.Hotkeys.ActivateBoxScaleGizmo.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTObjectSelectionGizmos.Get.Hotkeys.ActivateUniversalGizmo.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTObjectSelectionGizmos.Get.Hotkeys.ActivateExtrudeGizmo.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTObjectSelectionGizmos.Get.Hotkeys.ToggleTransformSpace.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTObjectSelectionGizmos.Get.MoveGizmoHotkeys.Enable2DMode.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTObjectSelectionGizmos.Get.MoveGizmoHotkeys.EnableSnapping.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTObjectSelectionGizmos.Get.MoveGizmoHotkeys.EnableVertexSnapping.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTObjectSelectionGizmos.Get.RotationGizmoHotkeys.EnableSnapping.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTObjectSelectionGizmos.Get.RotationGizmoHotkeys.EnableSnapping.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTObjectSelectionGizmos.Get.ScaleGizmoHotkeys.EnableSnapping.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTObjectSelectionGizmos.Get.ScaleGizmoHotkeys.ChangeMultiAxisMode.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTObjectSelectionGizmos.Get.BoxScaleGizmoHotkeys.EnableSnapping.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTObjectSelectionGizmos.Get.BoxScaleGizmoHotkeys.EnableCenterPivot.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTObjectSelectionGizmos.Get.UniversalGizmoHotkeys.Enable2DMode.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTObjectSelectionGizmos.Get.UniversalGizmoHotkeys.EnableSnapping.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTObjectSelectionGizmos.Get.UniversalGizmoHotkeys.EnableVertexSnapping.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTObjectSelectionGizmos.Get.ExtrudeGozmoHotkeys.EnableOverlapTest.IsEnabled = shouldReceive && _rldEnabledFields[i++];

        RTFocusCamera.Get.Hotkeys.MoveForward.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTFocusCamera.Get.Hotkeys.MoveBack.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTFocusCamera.Get.Hotkeys.StrafeLeft.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTFocusCamera.Get.Hotkeys.StrafeRight.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTFocusCamera.Get.Hotkeys.MoveUp.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTFocusCamera.Get.Hotkeys.MoveDown.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTFocusCamera.Get.Hotkeys.Pan.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTFocusCamera.Get.Hotkeys.LookAround.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTFocusCamera.Get.Hotkeys.Orbit.IsEnabled = shouldReceive && _rldEnabledFields[i++];
        RTFocusCamera.Get.Hotkeys.AlternateMoveSpeed.IsEnabled = shouldReceive && _rldEnabledFields[i++];

        RTUndoRedo.Get.ReceiveUndoRedoInput = shouldReceive && _rldEnabledFields[i++];
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F8))
        {
            if (RLDApp.Get != null)
            {
                RLDApp.Get.gameObject.SetActive(!RLDApp.Get.gameObject.activeSelf);
                Debug.Log("toggle RLD");
            }
            else
                Debug.LogError("No RLD");
        }
    }
}
