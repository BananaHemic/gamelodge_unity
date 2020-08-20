using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IMaterialProperty
{
    void ColorChanged(Color newColor);
    int GetPropertyIndex();
}
