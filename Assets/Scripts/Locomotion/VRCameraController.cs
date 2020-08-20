using KinematicCharacterController;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VRCameraController : GenericSingleton<VRCameraController>
{
    private KinematicCharacterMotor _motor;

    public void Init(KinematicCharacterMotor motor)
    {
        _motor = motor;
    }

    public void UpdateFromInputs()
    {
        Vector3 pos = transform.position;

        // We have two options for where we should move
        // 1) TransientPosition. This is the "goal" position for the character: where it wants to move by the next fixed update
        // 2) .position This is the actual position of the transform during the given frame. Kinematic character controller will update this every frame with the interpolated value

        //transform.position = CharacterMotor.TransientPosition + FollowTransform.localPosition;
        //transform.position = FollowTransform.position;
        transform.position = _motor.transform.position;
        Vector3 delta = transform.position - pos;
        //Debug.LogWarning("Moving camera " + delta.ToPrettyString() + " mag " + delta.sqrMagnitude);
    }
}
