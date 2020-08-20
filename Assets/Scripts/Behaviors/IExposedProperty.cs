using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using Miniscript;

public interface IExposedProperty
{
    void GetExample(ref List<SourceLine> exampleLines);
}
