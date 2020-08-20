using Miniscript;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FunctionParam
{
    Function.Param _param;
    public FunctionParam(Function.Param param)
    {
        _param = param;
    }
    public string GetFunctionParamName()
    {
        return _param.name;
    }
}
