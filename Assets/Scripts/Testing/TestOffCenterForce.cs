using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestOffCenterForce : MonoBehaviour
{
    public Rigidbody Rigid;
    public Rigidbody Target;
    public Transform GrabPoint;
    public float GrabHandWidth = 0.08f; // 8cm

    private int _frameCount = 0;

    void Start()
    {
        
    }

    private void FixedUpdate()
    {
        _frameCount++;
        Vector3 secondaryLocation = GrabPoint.localPosition + (GrabPoint.localPosition - Rigid.centerOfMass).normalized * GrabHandWidth;
        float deltaTime = Time.fixedUnscaledDeltaTime;
        if (_frameCount == 5)
        {
            float m = Rigid.mass;
            Vector3 delPosition = Target.transform.position - Rigid.transform.position;
            Vector3 velocity = Rigid.velocity;
            // Get the position of where the anti-torque will be applied
            //TODO the force applied will change over the course of deltaTime, so we 
            // should use an integral here. Probably doesn't make much of a real difference
            Vector3 reqF_pos = m * (delPosition - velocity * deltaTime) / (deltaTime * deltaTime);
            // Break down the force into two parts, one main one to move the object, and another that's farther away
            // from the center of mass and points in the opposite direction to counteract the force applied

            //Rigid.AddForce(reqF_pos);
            //Rigid.AddForceAtPosition(reqF_pos, Rigid.centerOfMass);
            Rigid.AddForceAtPosition(reqF_pos, GrabPoint.localPosition);
            // The force applied will cause a torque on the object, so
            // we apply a counter force to negate that torque
            Vector3 forceTorque = (GrabPoint.localPosition - Rigid.centerOfMass).magnitude * reqF_pos;
            Vector3 antiTorque = -forceTorque / secondaryLocation.magnitude;
            //Rigid.AddForceAtPosition(antiTorque, antiTorqueLocation);
            Debug.Log("Center of mass " + Rigid.centerOfMass.ToPrettyString());
            Debug.Log("Adding force " + reqF_pos.ToPrettyString() + " mag " + reqF_pos.magnitude);
            Debug.Log("Counteracting torque force " + antiTorque.ToPrettyString() + " at " + secondaryLocation.ToPrettyString());
        }
        else if (_frameCount == 90)
        {
            Quaternion deltaRotation = Target.transform.rotation * Quaternion.Inverse(Rigid.transform.rotation);
            deltaRotation.ToAngleAxis(out float delAngle, out Vector3 axis);
            delAngle = Mathf.Deg2Rad * delAngle;
            Debug.Log("Angle " + delAngle);
            Vector3 grabPointRelCOM = Rigid.centerOfMass - GrabPoint.localPosition;
            Vector3 Fdir = -Vector3.Cross(axis, grabPointRelCOM);
            float inertia = Vector3.Dot(Rigid.inertiaTensorRotation * Rigid.inertiaTensor, Fdir.normalized);
            float angular_accel = delAngle / (deltaTime * deltaTime);
            float torque_magnitude = angular_accel * inertia;
            float force_each_spot = torque_magnitude / GrabHandWidth;

            //float force_magnitude = torque_magnitude / (GrabPoint.localPosition - secondaryLocation).magnitude;
            //Debug.Log("Using force " + force_magnitude);
            //Rigid.AddForceAtPosition(Fdir.normalized * force_magnitude, secondaryLocation);
            Rigid.AddForceAtPosition(-force_each_spot * Fdir, GrabPoint.localPosition);
            Rigid.AddForceAtPosition(force_each_spot * Fdir, secondaryLocation);
        }
        else
        {
            Rigid.velocity = Vector3.zero;
            Rigid.angularVelocity = Vector3.zero;
        }
    }

    void Update()
    {
        
    }
}
