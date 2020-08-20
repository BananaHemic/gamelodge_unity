using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BasicSceneObjectReferenceDisplay : SceneObjectPropertyDisplay
{
    protected override string GetDefaultOption()
    {
        return "null";
    }
    protected override string GetRequiredScript()
    {
        return null;
    }
    protected override string GetSelectOptionTitleText()
    {
        return "Select SceneObject";
    }
}
