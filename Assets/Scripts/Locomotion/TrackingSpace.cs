using KinematicCharacterController;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Script to help keep the character controller under the head.
/// </summary>
public class TrackingSpace : GenericSingleton<TrackingSpace>
{
    private Vector3 _lastMotorPos;
    private Vector3 _lastOurPos;
    //private Vector3 _expectedDeltaPos;
    private Vector3 _cameraOffset;
    private Vector3 _lastTarget;
    private bool _waitingToApplyOffset = false;

    protected override void Awake()
    {
        base.Awake();
    }
    public void SetCameraOffset(Vector3 offsetWorld)
    {
        var possessedObj = UserManager.Instance.LocalUserDisplay.PossessedBehavior;
        if(possessedObj == null)
        {
            Debug.LogError("No possessed user for SetCameraOffset!");
            return;
        }

        _lastOurPos = transform.localPosition;
        if (_waitingToApplyOffset)
        {
            Debug.LogWarning("Recv new cam offset while one was pending");
            // If we haven't yet finished moving the tracking space to a new position,
            // then we can safely assume that that movement was successful and complete
            _lastOurPos = _lastTarget;
        }
        _lastMotorPos = possessedObj.CharacterController.transform.position;
        //_expectedDeltaPos = deltaPosWorld;
        _cameraOffset = transform.InverseTransformVector(offsetWorld);
        _lastTarget = _lastOurPos - _cameraOffset;
        _waitingToApplyOffset = true;
        //Debug.Log("Recv new cam offset " + _cameraOffset);
    }
    public bool FinishMove()
    {
        if (!_waitingToApplyOffset)
            return false;
        transform.localPosition = _lastTarget;
        _waitingToApplyOffset = false;
        return true;
    }
    /// <summary>
    /// The controller will try to move to where the head is,
    /// and then it will tell this script what direction that
    /// was. This method moves the tracking space back in the
    /// opposite direction, in an interpolated way. This keeps
    /// the character controller under the head.
    /// </summary>
    public void MoveTrackingSpaceFromController()
    {
        //Debug.Log("a " + Time.frameCount);
        if (!_waitingToApplyOffset)
            return;

        var possessedObj = UserManager.Instance.LocalUserDisplay.PossessedBehavior;
        if(possessedObj == null)
        {
            Debug.LogError("No possessed user for MoveTrackingSpaceFromController!");
            return;
        }
        //KinematicCharacterSystems runs early, default is -100
        Vector3 realDeltaPos = possessedObj.CharacterController.transform.position - _lastMotorPos;
        //float percentMoved =  realDeltaPos.magnitude / _expectedDeltaPos.magnitude;
        float percentMoved = KinematicCharacterSystem.GetLastInterpolationFactor();
        if(percentMoved > 1)
            Debug.LogWarning("Moved > 1! " + percentMoved);
        //Debug.Log("Moved " + percentMoved + "%. real change: " + realDeltaPos + " full change " + _cameraOffset);
        //transform.position = _lastOurPos - _cameraOffset * percentMoved;
        Vector3 newPos = _lastOurPos - _cameraOffset * percentMoved;
        //Debug.Log("Moving from " + _lastOurPos.ToPrettyString() + " to " + newPos.ToPrettyString());
        transform.localPosition = newPos;
        if (percentMoved >= 1f)
        {
            //Debug.Log("Motor moved fully");
            _waitingToApplyOffset = false;
        }
    }
}
