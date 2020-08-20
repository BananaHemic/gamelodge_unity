using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AudioClipPropertyDisplay : BundleItemReferencePropertyDisplay
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
        return "Select Sound Clip";
    }
    protected override SubBundle.SubBundleType GetSubBundleType()
    {
        return SubBundle.SubBundleType.Sound;
    }
}
