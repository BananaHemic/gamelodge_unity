using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AvatarPropertyDisplay : SceneObjectORBundleItemReferencePropertyDisplay
{
    protected override string GetDefaultOption()
    {
        return "default";
    }
    protected override void GetAllRuntimeInstances(List<SceneObject> sceneObjects)
    {
        var potentials = CharacterBehavior._allCharacterBehaviors;
        for(int i = 0; i < potentials.Count; i++)
        {
            CharacterBehavior potential = potentials[i];
            if (potential.IsPossessed)
                continue;
            sceneObjects.Add(potential.GetSceneObject());
        }
    }
    protected override string GetRequiredBundleItemScript()
    {
        return nameof(AvatarDescriptor);
    }
    protected override string GetSelectOptionTitleText()
    {
        return "Select Avatar";
    }
    protected override SubBundle.SubBundleType GetSubBundleType()
    {
        return SubBundle.SubBundleType.Prefab;
    }
}
