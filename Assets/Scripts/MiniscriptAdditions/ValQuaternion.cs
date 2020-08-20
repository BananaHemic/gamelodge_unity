using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Miniscript;
using System;

public class ValQuaternion : ValCustom
{
    public Quaternion Quaternion;

    private static bool _hasInitIntrinsics = false;
    private static ValMap _typeMap;

    // The functions available on this object
    const string ToEulerFuncName = "ToEuler";
    const string InverseFuncName = "inv";
    const string ToAngleAxisFuncName = "ToAngleAxis";
    const string AngleAroundAxisFuncName = "AngleAroundAxis";
    private static Intrinsic _angleAxisFunc;
    private static Intrinsic _angleAroundAxisFunc;

    public ValQuaternion(Quaternion quaternion) : base(false)
    {
        Quaternion = quaternion;
    }
    protected override void ResetState()
    {
    }
    protected override void ReturnToPool()
    {
    }
    public override Value ATimesB(Value other, int otherType, Context context, bool isSelfLhs)
    {
        ValQuaternion valQuat = other as ValQuaternion;
        if (valQuat != null)
            return new ValQuaternion(Quaternion * valQuat.Quaternion);

        // Try to also handle quaternion times vector3
        ValVector3 valVec3 = other as ValVector3;
        if (valVec3 != null)
        {
            //Debug.Log("Doing quat times vec3. Quat " + Quaternion.eulerAngles.ToPrettyString() + " vec " + valVec3.Vector3.ToPrettyString() + " res " + (Quaternion * valVec3.Vector3).ToPrettyString());
            return new ValVector3(Quaternion * valVec3.Vector3);
        }

        UserScriptManager.LogToCode(context, "Quaternion * undefined for " + other.GetType().ToString(), UserScriptManager.CodeLogType.Error);
        return ValNull.instance;
    }
    public override Value APlusB(Value other, int otherType, Context context, bool isSelfLhs)
    {
        UserScriptManager.LogToCode(context, "Quaternion + undefined", UserScriptManager.CodeLogType.Error);
        return ValNull.instance;
    }
    public override Value AMinusB(Value other, int otherTypeInt, Context context, bool isSelfLhs)
    {
        // AMinusB may be called if we're trying to negate this
        if(otherTypeInt == MiniscriptTypeInts.ValNumberTypeInt)
        {
            ValNumber num = other as ValNumber;
            if (num.value == 0)
            {
                Quaternion flipped = new Quaternion(-Quaternion.x, -Quaternion.y, -Quaternion.z, -Quaternion.w);
                return new ValQuaternion(flipped);
            }
        }
        UserScriptManager.LogToCode(context, "Quaternion - undefined", UserScriptManager.CodeLogType.Error);
        return ValNull.instance;
    }
    public override Value ADividedByB(Value other, int otherTypeInt, Context context, bool isSelfLhs)
    {
        UserScriptManager.LogToCode(context, "Quaternion / undefined", UserScriptManager.CodeLogType.Error);
        return ValNull.instance;
    }
    public override double Equality(Value rhs, int recursionDepth = 16)
    {
        ValQuaternion valQuat = rhs as ValQuaternion;
        if (valQuat == null)
            return 0;

        return Quaternion == valQuat.Quaternion ? 1 : 0;
    }
    public override int Hash(int recursionDepth = 16)
    {
        return Quaternion.GetHashCode();
    }
    public override string ToString(Machine vm)
    {
        return string.Format("({0},{1},{2},{3})", Quaternion.x, Quaternion.y, Quaternion.z, Quaternion.w);
    }
    public override string CodeForm(Machine vm, int recursionLimit = -1)
    {
        return ToString(vm);
    }
    public override bool Resolve(string identifier, out Value val)
    {
        switch (identifier)
        {
            case ToEulerFuncName:
                val = new ValVector3(Quaternion.eulerAngles);
                return true;
            case InverseFuncName:
                val = new ValQuaternion(Quaternion.Inverse(Quaternion));
                return true;
            case ToAngleAxisFuncName:
                val = _angleAxisFunc.GetFunc();
                return true;
            case AngleAroundAxisFuncName:
                val = _angleAroundAxisFunc.GetFunc();
                return true;
        }
        val = ValNull.instance;
        return false;
    }
    public static void InitIntrinsics()
    {
        if (_hasInitIntrinsics)
            return;
        _hasInitIntrinsics = true;
        // Load the constructor
        Intrinsic ctor = Intrinsic.Create("Quaternion");
        ctor.AddParam(ValString.xStr.value);
        ctor.AddParam(ValString.yStr.value);
        ctor.AddParam(ValString.zStr.value);
        ctor.AddParam(ValString.wStr.value);
        ctor.code = (context, partialResult) =>
        {
            Value xVal = context.GetVar(ValString.xStr);
            ValNumber xNum = xVal as ValNumber;
            if(xNum != null)
            {
                // Try to parse this as an angle, axis ctor
                ValVector3 axisVal = context.GetVar(ValString.yStr) as ValVector3;
                if(axisVal != null)
                    return new Intrinsic.Result(new ValQuaternion(Quaternion.AngleAxis((float)xNum.value, axisVal.Vector3)));
            }
            Value yVal = context.GetVar(ValString.yStr);
            var vecType = UserScriptManager.ParseVecInput(
                xVal,
                yVal,
                context.GetVar(ValString.zStr),
                context.GetVar(ValString.wStr),
                out double num,
                out Vector2 vec2,
                out Vector3 vec3,
                out Quaternion quat
                );

            if (vecType != UserScriptManager.VecType.Quaternion
            && vecType != UserScriptManager.VecType.Vector3)
                return Intrinsic.Result.Null;

            if (vecType == UserScriptManager.VecType.Vector3)
            {
                // We may have received an angle-axis representation
                // So check if y is a number
                if(xVal is ValVector3)
                {
                    ValNumber yNum = yVal as ValNumber;
                    if(yNum != null)
                    {
                        //Debug.Log("angle axis ");
                        quat = Quaternion.AngleAxis(Mathf.Rad2Deg * (float)yNum.value, vec3);
                        return new Intrinsic.Result(new ValQuaternion(quat));
                    }
                }
                //Debug.Log("euler");
                quat = Quaternion.Euler(vec3);
            }

            //Debug.Log("Made quaternion " + quat.eulerAngles.ToPrettyString());
            return new Intrinsic.Result(new ValQuaternion(quat));
        };

        _angleAxisFunc = Intrinsic.Create(ToAngleAxisFuncName, false);
        _angleAxisFunc.code = (context, partialResult) =>
        {
            ValQuaternion self = context.GetVar(ValString.selfStr) as ValQuaternion;
            if (self == null)
                return Intrinsic.Result.Null;

            self.Quaternion.ToAngleAxis(out float angle, out Vector3 axis);
            ValMap result = ValMap.Create();
            result[ValString.Create("angle")] = ValNumber.Create((double)angle);
            result[ValString.Create("axis")] = new ValVector3(axis);

            return new Intrinsic.Result(result);
        };
        _angleAroundAxisFunc = Intrinsic.Create(AngleAroundAxisFuncName, false);
        _angleAroundAxisFunc.AddParam("axis");
        _angleAroundAxisFunc.code = (context, partialResult) =>
        {
            ValQuaternion self = context.GetVar(ValString.selfStr) as ValQuaternion;
            if (self == null)
                return Intrinsic.Result.Null;
            ValVector3 valAxis = context.GetVar(ValString.Create("axis")) as ValVector3;
            Vector3 axis = valAxis.Vector3;

            // Get a vector orthogonal to axis
            Vector3 tangent = axis;
            Vector3.OrthoNormalize(ref axis, ref tangent);

            // Rotate the orthogonal vector
            Vector3 transformed = self.Quaternion * tangent;

            // Project transformed vector onto plane
            Vector3 flattened = transformed - (Vector3.Dot(transformed, axis) * axis);
            flattened.Normalize();

            // Get angle between original vector and projected transform to get angle around normal
            float angleRad = (float)Math.Acos(Vector3.Dot(tangent, flattened));

            return new Intrinsic.Result(angleRad * Mathf.Rad2Deg);
        };
    }

}
