using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Miniscript;
using System;

public class ValVector3 : ValCustom
{
    public Vector3 Vector3;

    private static bool _hasInitIntrinsics = false;
    private static Intrinsic _componentDivFunc;
    private static Intrinsic _componentMulFunc;
    private static Intrinsic _clampFunc;

    // The variables
    const string ToQuaternionFuncName = "ToQuaternion";
    const string NormalizedFuncName = "Normalized";
    const string MagnitudeFuncName = "Magnitude";
    const string SqrMagnitudeFuncName = "SqrMagnitude";
    // The functions
    const string ComponentDivideFuncName = "ComponentDiv";
    const string ComponentMultiplyFuncName = "ComponentMul";
    const string ClampFuncName = "Clamp";

    public ValVector3(Vector3 vec3) : base(false)
    {
        Vector3 = vec3;
    }
    protected override void ResetState()
    {
    }
    protected override void ReturnToPool()
    {
    }
    public override Value ATimesB(Value other, int otherType, Context context, bool isSelfLhs)
    {
        ValNumber valNum = other as ValNumber;
        if (valNum != null)
            return new ValVector3(Vector3 * (float)valNum.value);
        UserScriptManager.LogToCode(context, "Vector3 * undefined for " + other.GetType().ToString(), UserScriptManager.CodeLogType.Error);
        return null;
    }
    public override Value APlusB(Value other, int otherType, Context context, bool isSelfLhs)
    {
        ValVector3 rhsVec = other as ValVector3;
        if (rhsVec != null)
            return new ValVector3(Vector3 + rhsVec.Vector3);
        UserScriptManager.LogToCode(context, "Vector3 + undefined for " + other.GetType().ToString(), UserScriptManager.CodeLogType.Error);
        return null;
    }
    public override Value AMinusB(Value other, int otherTypeInt, Context context, bool isSelfLhs)
    {
        ValVector3 rhsVec = other as ValVector3;
        if (rhsVec != null)
        {
            return isSelfLhs // This shouldn't come up, the AMinusB would be called on the lhs one
                ? new ValVector3(Vector3 - rhsVec.Vector3)
                : new ValVector3(rhsVec.Vector3 - Vector3);
        }
        // AMinusB may be called if we're trying to negate this
        if(otherTypeInt == MiniscriptTypeInts.ValNumberTypeInt)
        {
            ValNumber num = other as ValNumber;
            if (num.value == 0)
                return new ValVector3(-Vector3);
        }
        UserScriptManager.LogToCode(context, "Vector3 - undefined for " + other.GetType().ToString(), UserScriptManager.CodeLogType.Error);
        return null;
    }
    public override Value ADividedByB(Value other, int otherTypeInt, Context context, bool isSelfLhs)
    {
        ValNumber rhsNum = other as ValNumber;
        if (rhsNum != null)
        {
            // Division doesn't commute
            return isSelfLhs
                ? new ValVector3(Vector3 / (float)rhsNum.value)
                : new ValVector3(new Vector3(
                    (float)rhsNum.value / Vector3.x,
                    (float)rhsNum.value / Vector3.y,
                    (float)rhsNum.value / Vector3.z)
                    );
        }
        UserScriptManager.LogToCode(context, "Vector3 / undefined for " + other.GetType().ToString(), UserScriptManager.CodeLogType.Error);
        return null;
    }
    public override double Equality(Value other, int recursionDepth = 16)
    {
        ValVector3 valVec3 = other as ValVector3;
        if (valVec3 == null)
            return 0;

        return Vector3 == valVec3.Vector3 ? 1 : 0;
    }
    public override int Hash(int recursionDepth = 16)
    {
        return Vector3.GetHashCode();
    }
    public override string ToString(Machine vm)
    {
        return string.Format("({0},{1},{2})", Vector3.x, Vector3.y, Vector3.z);
    }
    public override string CodeForm(Machine vm, int recursionLimit = -1)
    {
        return ToString(vm);
    }
    public override bool Resolve(string identifier, out Value val)
    {
        switch (identifier)
        {
            case ToQuaternionFuncName:
                val = new ValQuaternion(Quaternion.Euler(Vector3));
                return true;
            case NormalizedFuncName:
                val = new ValVector3(Vector3.normalized);
                return true;
            case MagnitudeFuncName:
                val = ValNumber.Create(Vector3.magnitude);
                return true;
            case SqrMagnitudeFuncName:
                val = ValNumber.Create(Vector3.sqrMagnitude);
                return true;
            case ComponentDivideFuncName:
                val = _componentDivFunc.GetFunc();
                return true;
            case ComponentMultiplyFuncName:
                val = _componentMulFunc.GetFunc();
                return true;
            case ClampFuncName:
                val = _clampFunc.GetFunc();
                return true;
            case "x":
                val = ValNumber.Create(Vector3.x);
                return true;
            case "y":
                val = ValNumber.Create(Vector3.y);
                return true;
            case "z":
                val = ValNumber.Create(Vector3.z);
                return true;
        }
        val = null;
        return false;
    }
    public static void InitIntrinsics()
    {
        if (_hasInitIntrinsics)
            return;
        _hasInitIntrinsics = true;
        // Load the constructor
        Intrinsic ctor = Intrinsic.Create("Vector3");
        ctor.AddParam(ValString.xStr.value, 0.0);
        ctor.AddParam(ValString.yStr.value, 0.0);
        ctor.AddParam(ValString.zStr.value, 0.0);
        ctor.code = (context, partialResult) =>
        {
            var vecType = UserScriptManager.ParseVecInput(
                context.GetVar(ValString.xStr),
                context.GetVar(ValString.yStr),
                context.GetVar(ValString.zStr),
                null,
                out double num,
                out Vector2 vec2,
                out Vector3 vec3,
                out Quaternion quat
                );

            if (vecType != UserScriptManager.VecType.Vector3)
                return Intrinsic.Result.Null;

            return new Intrinsic.Result(new ValVector3(vec3));
        };

        _componentDivFunc = Intrinsic.Create(ComponentDivideFuncName, false);
        _componentDivFunc.AddParam("divisor");
        _componentDivFunc.code = (context, partialResult) =>
        {
            ValVector3 self = context.GetVar(ValString.selfStr) as ValVector3;
            if (self == null)
                return Intrinsic.Result.Null;
            ValVector3 div = context.GetVar("divisor") as ValVector3;
            if (div == null)
                return Intrinsic.Result.Null;

            Vector3 selfVec = self.Vector3;
            Vector3 divVec = div.Vector3;
            Vector3 vec = new Vector3(selfVec.x / divVec.x, selfVec.y / divVec.y, selfVec.z / divVec.z);

            return new Intrinsic.Result(new ValVector3(vec));
        };

        _componentMulFunc = Intrinsic.Create(ComponentMultiplyFuncName, false);
        _componentMulFunc.AddParam("rhs");
        _componentMulFunc.code = (context, partialResult) =>
        {
            ValVector3 self = context.GetVar(ValString.selfStr) as ValVector3;
            if (self == null)
                return Intrinsic.Result.Null;
            ValVector3 mul = context.GetVar("rhs") as ValVector3;
            if (mul == null)
                return Intrinsic.Result.Null;

            Vector3 selfVec = self.Vector3;
            Vector3 mulVec = mul.Vector3;
            Vector3 vec = new Vector3(selfVec.x * mulVec.x, selfVec.y * mulVec.y, selfVec.z * mulVec.z);

            return new Intrinsic.Result(new ValVector3(vec));
        };

        _clampFunc = Intrinsic.Create(ClampFuncName, false);
        _clampFunc.AddParam("min");
        _clampFunc.AddParam("max");
        _clampFunc.code = (context, partialResult) =>
        {
            ValVector3 self = context.GetVar(ValString.selfStr) as ValVector3;
            if (self == null)
                return Intrinsic.Result.Null;
            UserScriptManager.VecType minVecType = UserScriptManager.ParseVecInput(context.GetVar("min"), out double minNum, out Vector2 vec2, out Vector3 minVec3, out Quaternion rot);
            if (minVecType != UserScriptManager.VecType.Double
                && minVecType != UserScriptManager.VecType.Vector3)
                return Intrinsic.Result.Null;

            UserScriptManager.VecType maxVecType = UserScriptManager.ParseVecInput(context.GetVar("max"), out double maxNum, out vec2, out Vector3 maxVec3, out rot);
            if (maxVecType != minVecType)
                return Intrinsic.Result.Null;

            Vector3 currVec = self.Vector3;
            Vector3 newVec;
            if (minVecType == UserScriptManager.VecType.Double)
            {
                newVec = new Vector3(
                    Mathf.Clamp(currVec.x, (float)minNum, (float)maxNum),
                    Mathf.Clamp(currVec.y, (float)minNum, (float)maxNum),
                    Mathf.Clamp(currVec.z, (float)minNum, (float)maxNum));
            }
            else if (minVecType == UserScriptManager.VecType.Vector3)
            {
                newVec = new Vector3(
                    Mathf.Clamp(currVec.x, minVec3.x, maxVec3.x),
                    Mathf.Clamp(currVec.y, minVec3.y, maxVec3.y),
                    Mathf.Clamp(currVec.z, minVec3.z, maxVec3.z));
            }
            else
                newVec = Vector3.zero;
            return new Intrinsic.Result(new ValVector3(newVec));
        };
    }

}
