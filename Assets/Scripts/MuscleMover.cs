using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MuscleMover : MonoBehaviour
{
    /*
    public float TargetSpeed = 2f;
    /// <summary>
    /// The max force that this user can create
    /// I think the average user interaction will
    /// be curl, or something like a curl, so we
    /// use the established guidelines there
    /// https://strengthlevel.com/strength-standards/dumbbell-curl/lb
    /// </summary>
    public float MaxLiftWeightLBs = 50;
    public float MaxLiftSpeedMPH = 10;
    /// <summary>
    /// The A parameter from Hill's muscle model
    /// </summary>
    public float CoefficientOfShorteningHeat = 0.25f;

    public float NearDistVelocityControl = 0.1f;
    public float P, I, D;
    public int IntegrationInterval = 100;
    public Transform Target;
    public Rigidbody Rigid;
    public bool DropIDInCollision = false;

    private PID _pidController;
    private int _frameOfLastInCollision;
    private Rigidbody _rigidbody;
    private Rigidbody _targetRigidbody;

    void Start()
    {
        _pidController = new PID(P, I, D, IntegrationInterval);
        _rigidbody = GetComponent<Rigidbody>();
        _targetRigidbody = Target.GetComponent<Rigidbody>();
    }
    void OnCollisionEnter()
    {
        _frameOfLastInCollision = Time.frameCount;
        //Debug.Log("enter");
    }
    void OnCollisionStay()
    {
        _frameOfLastInCollision = Time.frameCount;
        //Debug.Log("stay "+ _frameOfLastInCollision);
    }

    private float expectedX = float.NaN;

    private float GetAppliedForce(float current, float goal, float velocity, float goalVelocity, float deltaTime)
    {
        // N = (kg) * g
        float maxForceN = (MaxLiftWeightLBs / 2.205f) * 9.81f;
        // m/s = mph / 2.237
        float maxVelocityMs = MaxLiftSpeedMPH / 2.237f;

        float x = goal - current;

        // Get the force that will get us to where we need to go next frame
        float m = _rigidbody.mass;
        //TODO the force applied will change over the course of deltaTime, so we 
        // should use an integral here. Probably doesn't make much of a real difference
        //TODO this seems to be off by a factor of 2
        float reqF_pos = 2 * m * (x - velocity * deltaTime) / (deltaTime * deltaTime);
        float reqF_vel = m * (goalVelocity - velocity) / deltaTime;

        // We want to match both the target's position and velocity
        // so, ideally, we would plot the course that matches the target's
        // current trajectory and speed in the minimum amount of time, but
        // alas, idk how to do that without an expensive and complex brute force
        // so we simply just match the position when far away, and gradually try to
        // match the velocity when we get close
        float lerpDistance = Mathf.Abs((x / NearDistVelocityControl));
        //Debug.Log("Lerping by " + lerpDistance);
        // Tried using CosLerp, but it caused extra bouncing when the target
        // is in a constant move
        float reqF = Mathf.Lerp(reqF_vel, reqF_pos, lerpDistance);
        //float reqF = ExtensionMethods.CosLerp(reqF_vel, reqF_pos, lerpDistance);

        float resForce;
        if (Mathf.Abs(velocity) < 0.1f
            || Mathf.Sign(reqF) * Mathf.Sign(velocity) == 1f)
        {
            // delta and velocity are in the same direction,
            // use Hill's to get the correct force

            // NB we are now using normalized F/V
            float v = Mathf.Abs(velocity) / maxVelocityMs;
            // If we're already at the max velocity,
            // then we can't make any force
            if (v >= 1f)
            {
                //Debug.Log("Moving towards goal at max vel");
                return 0;
            }

            //NB this is the normalized form, so a=b
            float a = CoefficientOfShorteningHeat;
            float b = a;
            float fMaxNorm = 1;
            float f = b * (fMaxNorm + a) / (v + b) - a;

            //Debug.Log("Could apply (normalized) f " + f + " v " + v);
            // Now get the non-normalized value
            resForce = maxForceN * f;
        }
        else
        {
            // If we're applying a force against the velocity, then
            // this is an eccentric contraction and the max force is just
            // 1.3 times the concentric max force
            resForce = maxForceN * 1.3f;
            Debug.Log("Moving against goal, using eccentric of " + resForce);
        }

        // If this force is less than what we can apply, just use it
        if (Mathf.Abs(reqF) <= resForce)
            return reqF;
        // Otherwise, we clamp the applied force by how much we can apply
        resForce = resForce * Mathf.Sign(reqF);
        Debug.Log("Final force " + resForce);
        return resForce;
    }

    void HumanContraction()
    {
        // TODO convert to local-space velocity
        Vector3 vel = _rigidbody.velocity;
        Vector3 targVel = _targetRigidbody.velocity;
        float f = GetAppliedForce(transform.localPosition.x, Target.transform.localPosition.x, vel.x, targVel.x, Time.fixedUnscaledDeltaTime);
        _rigidbody.AddForce(Vector3.right * f, ForceMode.Force);
    }
    void FixedUpdate()
    {
        //float force = 0;
        //if (Input.GetKeyDown(KeyCode.L))
        //    force = 3f;
        //float error = expectedX - transform.localPosition.x;
        //expectedX = transform.localPosition.x + _rigidbody.velocity.x * Time.fixedUnscaledDeltaTime + 0.5f * (force / _rigidbody.mass) * (Time.fixedUnscaledDeltaTime * Time.fixedUnscaledDeltaTime);
        //Debug.Log("Error " + error + " next delta " + expectedX);
        //_rigidbody.AddForce(Vector3.right * force, ForceMode.Force);
        HumanContraction();
        return;
        Debug.Log("update "+ Time.frameCount);
        _pidController.P = P;
        if(_frameOfLastInCollision == Time.frameCount - 1
            && DropIDInCollision)
        {
            Debug.Log("Dropping ID");
            _pidController.I = 0;
            _pidController.D = 0;
            _pidController.Clear();
        }
        else
        {
            _pidController.I = I;
            _pidController.D = D;
        }
        _pidController.MaxIntegralHistory = IntegrationInterval;
        float correction = _pidController.Update(Target.localPosition.x, transform.localPosition.x, Time.fixedUnscaledDeltaTime);
        Rigid.AddForce(Vector3.right * correction, ForceMode.Force);
    }

    void Update()
    {
        if (Input.GetKey(KeyCode.RightArrow))
            Target.Translate(Vector3.right * Time.deltaTime * TargetSpeed);
        if (Input.GetKey(KeyCode.LeftArrow))
            Target.Translate(Vector3.left * Time.deltaTime * TargetSpeed);

        
    }
    */
}
