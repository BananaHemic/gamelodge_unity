using Miniscript;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class ExposedFunction : IExposedProperty
{
    public string Name { get; private set; }
    public Intrinsic Intrinsic { get; private set; }
    public string Description { get; private set; }
    public string ExampleReturnVariable { get; private set; }
    public FunctionParam[] Params { get; private set; }

    public ExposedFunction(Intrinsic intrinsic, string description, string exampleReturnVariable)
    {
        Name = intrinsic.name;
        Intrinsic = intrinsic;
        Description = description;
        List<Function.Param> intrinsicParams = intrinsic.GetParams();
        Params = new FunctionParam[intrinsicParams.Count];
        for (int i = 0; i < Params.Length; i++)
            Params[i] = new FunctionParam(intrinsicParams[i]);
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
        SourceLine retLine = new SourceLine(SourceLine.GetCharArray(CodeUI.MaxExampleLineLength));
        if (!string.IsNullOrEmpty(ExampleReturnVariable))
        {
            retLine.Append(ExampleReturnVariable);
            retLine.Append(" = ");
        }
        retLine.Append(Name);
        retLine.Append('(');
        for(int i = 0; i < Params.Length; i++)
        {
            retLine.Append(Params[i].GetFunctionParamName());
            if (i < Params.Length - 1)
                retLine.Append(',');
        }
        retLine.Append(')');
        exampleLines.Add(retLine);
    }
}
