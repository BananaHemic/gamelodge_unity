using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Miniscript;
using System;

public class ValLine : ValCustom
{
    public Vector3 Origin;
    public Vector3 Direction;
    public Color Color;
    private XRLineRenderer _lineRenderer;
    [ThreadStatic]
    protected static ValuePool<ValLine> _valuePool;

    private static bool _hasInitIntrinsics = false;
    const string UpdateFuncName = "Update";
    const string DestroyFuncName = "Destroy";
    const string DirectionParamName = "direction";
    const string ColorParamName = "color";
    private static readonly ValString _dirParamVal = ValString.Create(DirectionParamName, false);
    private static readonly ValString _colorParamVal = ValString.Create(ColorParamName, false);
    private static Intrinsic _updateIntrinsic;

    private ValLine(Vector3 origin, Vector3 direction, Color color) : base(true)
    {
        Init(origin, direction, color);
    }
    private void Init(Vector3 origin, Vector3 direction, Color color)
    {
        Origin = origin;
        Direction = direction;
        Color = color;
        _lineRenderer = LineRenderingManager.Instance.GetLineRenderer(origin, direction, color);
    }
    public static ValLine Create(Vector3 origin, Vector3 direction, Color color)
    {
        if (_valuePool == null)
            _valuePool = new ValuePool<ValLine>();
        else
        {
            ValLine valLine = _valuePool.GetInstance();
            if (valLine != null)
            {
                valLine._refCount = 1;
                valLine.Init(origin, direction, color);
                return valLine;
            }
        }
        return new ValLine(origin, direction, color);
    }
    protected override void ResetState()
    {
        if(_lineRenderer != null)
        {
            GameObject.Destroy(_lineRenderer.gameObject);
            _lineRenderer = null;
        }
    }
    protected override void ReturnToPool()
    {
        if (!base._poolable)
            return;
        if (_valuePool == null)
            _valuePool = new ValuePool<ValLine>();
        _valuePool.ReturnToPool(this);
    }
    public override string CodeForm(Machine vm, int recursionLimit = -1)
    {
        return ToString(vm);
    }
    public void Update(Vector3 origin, Vector3 direction)
    {
        Origin = origin;
        Direction = direction;
        LineRenderingManager.Instance.ConfigureLineRenderer(_lineRenderer, Origin, Direction, Color);
    }
    public override bool Resolve(string identifier, out Value val)
    {
        switch (identifier)
        {
            case UpdateFuncName:
                val = _updateIntrinsic.GetFunc();
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
        Intrinsic ctor = Intrinsic.Create("Line");
        ctor.AddParam(ValString.positionStr.value);
        ctor.AddParam(DirectionParamName);
        ctor.AddParam(ColorParamName);
        ctor.code = (context, partialResult) =>
        {
            var vecType = UserScriptManager.ParseVecInput(
                context.GetVar(ValString.positionStr),
                out double num,
                out Vector2 vec2,
                out Vector3 position,
                out Quaternion quat
                );

            if (vecType != UserScriptManager.VecType.Vector3)
            {
                UserScriptManager.LogToCode(context, "Bad position param provided", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            vecType = UserScriptManager.ParseVecInput(
                context.GetVar(_dirParamVal),
                out num,
                out vec2,
                out Vector3 direction,
                out quat
                );

            if (vecType != UserScriptManager.VecType.Vector3)
            {
                UserScriptManager.LogToCode(context, "Bad direction param provided", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            bool didGetColor = UserScriptManager.ParseColorInput(context.GetVar(_colorParamVal), out Color color);
            if(!didGetColor)
            {
                UserScriptManager.LogToCode(context, "Bad color input", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            return new Intrinsic.Result(ValLine.Create(position, direction, color));
        };

        // Load the method(s)
        _updateIntrinsic = Intrinsic.Create(UpdateFuncName, false);
        _updateIntrinsic.AddParam(ValString.positionStr.value);
        _updateIntrinsic.AddParam(DirectionParamName);
        _updateIntrinsic.code = (context, partialResult) =>
        {
            ValLine self = context.GetVar(ValString.selfStr) as ValLine;
            if (self == null)
                return Intrinsic.Result.Null;

            var vecType = UserScriptManager.ParseVecInput(
                context.GetVar(ValString.positionStr),
                out double num,
                out Vector2 vec2,
                out Vector3 position,
                out Quaternion quat
                );

            if (vecType != UserScriptManager.VecType.Vector3)
            {
                UserScriptManager.LogToCode(context, "Bad position param provided", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            vecType = UserScriptManager.ParseVecInput(
                context.GetVar(_dirParamVal),
                out num,
                out vec2,
                out Vector3 direction,
                out quat
                );

            if (vecType != UserScriptManager.VecType.Vector3)
            {
                UserScriptManager.LogToCode(context, "Bad direction param provided", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            self.Update(position, direction);
            return Intrinsic.Result.True;
        };
    }
    public override double Equality(Value rhs, int recursionDepth = 16)
    {
        return -1;
    }
    public override int Hash(int recursionDepth = 16)
    {
        return this.GetHashCode();
    }
    public override string ToString(Machine vm)
    {
        return "line";
    }
}
