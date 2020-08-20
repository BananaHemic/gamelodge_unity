using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Miniscript;
using System.Text;

public class ExposedEvent : IExposedProperty
{
    public string Name { get; private set; }
    public ValString NameVal { get; private set; }
    public string Description { get; private set; }

    private readonly FunctionParam[] _functionParams;

    public ExposedEvent(ValString eventName, string description, Function.Param[] functionParams)
    {
        Name = eventName.value;
        NameVal = eventName;
        Description = description;
        _functionParams = new FunctionParam[functionParams != null ? functionParams.Length : 0];
        for(int i = 0; i < _functionParams.Length; i++)
            _functionParams[i] = new FunctionParam(functionParams[i]);
    }
    public void GetExample(ref List<SourceLine> exampleLines)
    {
        int numCharsRead = 0;
        while(numCharsRead < Description.Length)
        {
            SourceLine descLine = new SourceLine(SourceLine.GetCharArray(CodeUI.MaxExampleLineLength));
            descLine.Append("// ");
            int startIndex = numCharsRead;
            int endIndex = startIndex + CodeUI.MaxExampleLineLength;
            int count;
            // Exit early if we're at the end
            if(endIndex >= Description.Length)
            {
                count = Description.Length - startIndex;
                descLine.Append(Description, startIndex, count);
                descLine.Append('\n');
                exampleLines.Add(descLine);
                break;
            }
            // Get the index after the max length that's a space
            while (Description.Length > endIndex && Description[endIndex] != ' ')
                endIndex++;

            count = endIndex - startIndex;
            descLine.Append(Description, startIndex, count);
            descLine.Append('\n');
            numCharsRead += count + 1;
            exampleLines.Add(descLine);
        }
        SourceLine nameLine = new SourceLine(SourceLine.GetCharArray(Name.Length));
        nameLine.Append(Name);
        nameLine.Append(" = function(");
        if(_functionParams != null)
        {
            for(int i = 0; i < _functionParams.Length; i++)
            {
                nameLine.Append(_functionParams[i].GetFunctionParamName());
                if (i < _functionParams.Length - 1)
                    nameLine.Append(",");
            }
        }
        nameLine.Append(')');
        exampleLines.Add(nameLine);
        SourceLine endFunctionLine = new SourceLine(SourceLine.GetCharArray(32));
        endFunctionLine.Append("end function");
        exampleLines.Add(endFunctionLine);
    }
}
