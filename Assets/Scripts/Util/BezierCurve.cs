using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//using System.Math;

public class BezierCurve
{
    /**
     * X component of Bezier coefficient C
     */
    private readonly double cx;

    /**
     * X component of Bezier coefficient B
     */
    private readonly double bx;

    /**
     * X component of Bezier coefficient A
     */
    private readonly double ax;

    /**
     * Y component of Bezier coefficient C
     */
    private readonly double cy;
    /**
     * Y component of Bezier coefficient B
     */
    private readonly double by;

    /**
     * Y component of Bezier coefficient A
     */
    private readonly double ay;

    /**
     * Defines a cubic-bezier curve given the middle two control points.
     * NOTE: first and last control points are implicitly (0,0) and (1,1).
     * @param p1x {number} X component of control point 1
     * @param p1y {number} Y component of control point 1
     * @param p2x {number} X component of control point 2
     * @param p2y {number} Y component of control point 2
     */
    public BezierCurve(double p1x, double p1y, double p2x, double p2y)
    {
        cx = 3.0 * p1x;
        bx = 3.0 * (p2x - p1x) - cx;
        ax = 1.0 - cx - bx;
        cy = 3.0 * p1y;
        by = 3.0 * (p2y - p1y) - cy;
        ay = 1.0 - cy - by;
    }

    private static BezierCurve _standardCurve;
    public static BezierCurve GetStandardBezierCurve()
    {
        // Values from Material Design
        // https://material.io/design/motion/speed.html#easing
        if (_standardCurve == null)
            _standardCurve = new BezierCurve(0.4, 0, 0.2, 1);
        return _standardCurve;
    }
    /**
	 * The epsilon value we pass to UnitBezier::solve given that the animation is going to run over |dur| seconds.
	 * The longer the animation, the more precision we need in the timing function result to avoid ugly discontinuities.
	 * http://svn.webkit.org/repository/webkit/trunk/Source/WebCore/page/animation/AnimationBase.cpp
	 */
    private double solveEpsilon (double duration) {
		return 1.0 / (200.0 * duration);
	}
    private double sampleCurveX(double t) {
        // `ax t^3 + bx t^2 + cx t' expanded using Horner's rule.
        return ((ax* t + bx) * t + cx) * t;
    }
    private double sampleCurveY(double t) {
        return ((ay* t + by) * t + cy) * t;
    }

    private double sampleCurveDerivativeX (double t) {
        return (3.0 * ax * t + 2.0 * bx) * t + cx;
    }

    /**
     * Given an x value, find a parametric value it came from.
     * @param x {number} value of x along the bezier curve, 0.0 <= x <= 1.0
     * @param epsilon {number} accuracy limit of t for the given x
     * @return {number} the t value corresponding to x
     */
    private double solveCurveX (double x, double epsilon) {
        double t0, t1, t2, x2, d2;
        int i;

        // First try a few iterations of Newton's method -- normally very fast.
        for (t2 = x, i = 0; i< 8; i++) {
            x2 = sampleCurveX(t2) - x;
            if (Math.Abs(x2) < epsilon) {
                return t2;
            }
            d2 = sampleCurveDerivativeX(t2);
            if (Math.Abs(d2) < 1e-6) {
                break;
            }
            t2 = t2 - x2 / d2;
        }

        // Fall back to the bisection method for reliability.
        t0 = 0.0;
        t1 = 1.0;
        t2 = x;

        if (t2<t0) {
            return t0;
        }
        if (t2 > t1) {
            return t1;
        }

        while (t0<t1) {
            x2 = sampleCurveX(t2);
            if (Math.Abs(x2 - x) < epsilon) {
                return t2;
            }
            if (x > x2) {
                t0 = t2;
            } else {
                t1 = t2;
            }
            t2 = (t1 - t0) * 0.5 + t0;
        }

        // Failure.
        return t2;
    }

    /**
     * @param x {number} the value of x along the bezier curve, 0.0 <= x <= 1.0
     * @param epsilon {number} the accuracy of t for the given x
     * @return {number} the y value along the bezier curve
     */
    private double _solve(double x, double epsilon) {
        return sampleCurveY(solveCurveX(x, epsilon));
    }

    // public interface --------------------------------------------
    public double Solve(double t, double duration)
    {
        return _solve(t, solveEpsilon(duration));
        
    }
}
