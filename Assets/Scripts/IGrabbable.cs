using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IGrabbable
{
    SceneObject GetSceneObject();
    void OnCanGrabStateChange(bool canGrab, ControllerAbstraction.ControllerType controllerType);
    bool OnLocalGrabStart(int controllers);
    void OnLocalGrabUpdate();
    void OnLocalGrabEnd(ControllerAbstraction.ControllerType detachedController);
    bool CanGrab(ControllerAbstraction.ControllerType controllerType);
}
