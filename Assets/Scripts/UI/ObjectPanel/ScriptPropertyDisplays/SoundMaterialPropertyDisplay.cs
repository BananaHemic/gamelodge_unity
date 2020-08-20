using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SoundMaterialPropertyDisplay : BundleItemReferencePropertyDisplay
{
    protected override string GetDefaultOption()
    {
        return "null";
    }
    protected override string GetRequiredScript()
    {
        //return nameof(PhysSound.PhysSoundMaterial);
        return "PhysSound.PhysSoundMaterial";
    }
    protected override string GetSelectOptionTitleText()
    {
        return "Select Sound Material";
    }
    protected override SubBundle.SubBundleType GetSubBundleType()
    {
        return SubBundle.SubBundleType.ScriptableObject;
    }
}
