using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Miniscript;

public class CustomMiniscriptInterpreter : Interpreter
{
    public delegate void FormattedErrorMethod(string description, int line);
    private FormattedErrorMethod _readableErrorOutput;

    public CustomMiniscriptInterpreter(Parser source, FormattedErrorMethod onError) : base(source)
    {
        _readableErrorOutput = onError;
    }

    protected override void ReportError(MiniscriptException mse)
    {
        //base.ReportError(mse);
        //Debug.Log("Got exception " + mse);
        if(_readableErrorOutput != null)
            _readableErrorOutput(mse.Message, mse.location != null ? mse.location.lineNum : -1);
    }
}
