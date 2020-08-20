using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StepFrameButton : GenericSingleton<StepFrameButton>
{
    public Button Button;
    public void OnStepFrameClicked()
    {
        TimeManager.Instance.StepOnce();
    }
}
