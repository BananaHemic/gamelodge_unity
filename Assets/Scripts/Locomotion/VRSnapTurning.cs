using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VRSnapTurning : GenericSingleton<VRSnapTurning>
{
    public float SnapTurnAngle = 45f;

    private float _timeOfLastTurn = float.MinValue;
    const float MinSnapTurnWaitTime = 0.2f;

    void Start()
    {
        ControllerAbstraction.OnControllerPoseUpdate_SnapTurn += UpdateSnapTurn;
    }
    void UpdateSnapTurn()
    {
        if (VRSDKUtils.Instance.CurrentSDK == VRSDKUtils.SDK.Desktop)
            return;
        if (Time.realtimeSinceStartup - _timeOfLastTurn < MinSnapTurnWaitTime)
            return;
        // Don't snap turn if another script is using the X joystick
        if (ControlLock.Instance.IsLocked(ControlLock.ControlType.XJoystick_Right))
            return;

        if (ControllerAbstraction.Instances[0].GetSnapTurnLeftDown())
            RotateUser(-SnapTurnAngle);
        else if (ControllerAbstraction.Instances[0].GetSnapTurnRightDown())
            RotateUser(SnapTurnAngle);
    }
    void RotateUser(float angle)
    {
        // We want to rotate the user by rotating the user obj, but
        // keep the camera in the same position. So what we do is rotate
        // the User obj, and then just move the User obj to keep the camera in the same position
        Vector3 prevPos = ControllerAbstraction.Instances[0].GetPosition();
        transform.Rotate(Vector3.up, angle);
        Vector3 deltaPos = prevPos - ControllerAbstraction.Instances[0].GetPosition();
        transform.localPosition += deltaPos;
        //Vector3 newPos = (prevPos - ControllerAbstraction.Instances[0].GetPosition());
        //Debug.Log("Rotating user " + deltaPos.ToPrettyString() + " res " + newPos.ToPrettyString());
        _timeOfLastTurn = Time.realtimeSinceStartup;

        CharacterBehavior character = UserManager.Instance.LocalUserDisplay.PossessedBehavior;
        if(character != null)
            character.CharacterController.AddVelocity(deltaPos / TimeManager.Instance.PhysicsTimestep);
    }
}
