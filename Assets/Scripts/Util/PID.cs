using System.Collections.Generic;
using System;
using UnityEngine;

//[Serializable]
public class PID
{
    public float P, I, D;

    /// <summary>
    /// A well documented error of PID
    /// controllers is that they can accumulate error in the integral
    /// component. So we only measure the integral over a certain period
    /// </summary>
    public int MaxIntegralHistory;

    float _integral;
    float _lastError;
    private readonly Queue<float> _valuesAddedToIntegral;
    //private readonly Queue<float> _valuesAddedToIntegral = new Queue<float>(100);

    /// <summary>
    /// Essentially, a basic solver to find the value that
    /// best minimizes error. Keeps a small memory .
    /// 
    /// </summary>
    /// <param name="pFactor">The correction factor for the position</param>
    /// <param name="iFactor">The correction factor for the veloctiy</param>
    /// <param name="dFactor">The correction factor for the integral</param>
    public PID(float pFactor, float iFactor, float dFactor, int max)
    {
        P = pFactor;
        I = iFactor;
        D = dFactor;
        MaxIntegralHistory = max;
        _valuesAddedToIntegral = new Queue<float>(max);
    }

    public void Clear()
    {
        _valuesAddedToIntegral.Clear();
    }

    /// <summary>
    /// How much change to apply, given where want
    /// to go, where we are, and how long a step in time is
    /// </summary>
    /// <param name="setpoint">The target value</param>
    /// <param name="actual">The actual value</param>
    /// <param name="timeFrame">How long the correction will be applied for</param>
    /// <returns></returns>
    public float Update(float setpoint, float actual, float timeFrame)
    {
        float present = setpoint - actual;
        float integralVal = present * timeFrame;
        //Debug.Log("Integral count " + _valuesAddedToIntegral.Count + " max " + MaxIntegralHistory);
        // Make sure we don't keep building to the integral endlessly
        // by removing old contributions
        if (_valuesAddedToIntegral.Count >= MaxIntegralHistory)
            _integral -= _valuesAddedToIntegral.Dequeue();

        _integral += integralVal;
        _valuesAddedToIntegral.Enqueue(integralVal);
        float deriv = (present - _lastError) / timeFrame;
        _lastError = present;
        return present * P + _integral * I + deriv * D;
    }
}
