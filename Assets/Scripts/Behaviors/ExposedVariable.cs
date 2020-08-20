using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Miniscript;
using System.Text;

public class ExposedVariable : IExposedProperty
{
    public string Name { get; private set; }
    public string Description { get; private set; }
    public Value Key { get; private set; }
    public Value Value { get; private set; }

    private ValNumber _valNumber;
    private ValString _valString;
    //private ValMap _valMap;
    private ValVector3 _valVec3;
    private ValQuaternion _valQuat;

    public ExposedVariable(ValString key, string description, float num)
    {
        Key = key;
        Name = key.value;
        Description = description;
        _valNumber = ValNumber.Create(num);
        Value = _valNumber;
    }
    public ExposedVariable(ValString key, string description, string str)
    {
        Key = key;
        Name = key.value;
        Description = description;
        _valString = ValString.Create(str);
        Value = _valString;
    }
    public ExposedVariable(ValString key, string description, Vector3 vec)
    {
        Key = key;
        Name = key.value;
        Description = description;
        _valVec3 = new ValVector3(vec);
        Value = _valVec3;
    }
    public ExposedVariable(ValString key, string description, Quaternion quat)
    {
        Key = key;
        Name = key.value;
        Description = description;
        _valQuat = new ValQuaternion(quat);
        Value = _valQuat;
    }
    public void SetNumber(float num)
    {
        // We have to make a new ValNumber, otherwise
        // things like:
        // t = time
        // wait 1
        // print(t)
        // show that t is still getting updated, even
        // though it shouldn't
        if (_valNumber != null)
            _valNumber.Unref();
        _valNumber = ValNumber.Create(num);
        Value = _valNumber;
    }
    public void SetString(string str)
    {
        if (_valString != null)
            _valString.Unref();
        _valString = ValString.Create(str);
        Value = _valString;
    }
    public void SetVector3(Vector3 vec)
    {
        _valVec3 = new ValVector3(vec);
        Value = _valVec3;
    }
    public void SetQuaternion(Quaternion quat)
    {
        _valQuat = new ValQuaternion(quat);
        Value = _valQuat;
    }
    public void InsertIntoInterpreter(Interpreter interpreter)
    {
        interpreter.SetGlobalValue(Key, Value);
        // Now we're done with it, we can unref and clear out local copy
        //if (_valMap != null)
            //_valMap.Unref();
        //_valMap = null;
        if (_valNumber != null)
            _valNumber.Unref();
        _valNumber = null;
        if (_valString != null)
            _valString.Unref();
        _valString = null;
        _valVec3 = null;
        _valQuat = null;
        Value = null;
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
        exampleLines.Add(nameLine);
    }
}
