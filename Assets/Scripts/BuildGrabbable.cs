using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildGrabbable : BaseGrabbable, IGrabbable
{
    private BoxCollider _boxCollider;
    private Coroutine _waitForBundleItem;
    private bool _hasInit = false;

    const float MinSize = 0.001f; // 1mm

    void Awake()
    {
        _boxCollider = GetComponent<BoxCollider>();
#if UNITY_EDITOR
        if (gameObject.layer != GLLayers.BuildGrabbableLayerNum)
            Debug.LogError("Wrong layer for build grabbable!");
        if(!gameObject.CompareTag(GLLayers.BuildGrabbableTag))
            Debug.LogError("Wrong tag for build grabbable!");
#endif
    }
    public void Init(SceneObject sceneObject)
    {
        if (_hasInit)
            Debug.LogError("Already init BuildGrabbable!");
        _hasInit = true;
        base.SetSceneObject(sceneObject);
        transform.SetParent(SceneObject.transform, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;

        if(sceneObject.BundleItem == null)
        {
            _boxCollider.enabled = false;
            _waitForBundleItem = StartCoroutine(WaitForBundleItem());
            return;
        }
        ConfigureCollider(sceneObject.BundleItem.AABBInfo);
    }
    public void Reset()
    {
        base.SceneObject = null;
    }
    protected override  void OnCanGrab()
    {
        base.OnCanGrab();
        // TODO
        if (VRSDKUtils.Instance.CurrentSDK == VRSDKUtils.SDK.Desktop)
            return;
        SceneObject.SetObjectOutline(ObjectOutline.OutlineState.GameHover);
    }
    public override bool OnLocalGrabStart(int controllers)
    {
        if (!base.OnLocalGrabStart(controllers))
            return false;
        SceneObject.SetObjectOutline(ObjectOutline.OutlineState.Off);
        SceneObject.BuildSelect();
        return true;
    }
    public override void OnLocalGrabEnd(ControllerAbstraction.ControllerType detachedController)
    {
        base.OnLocalGrabEnd(detachedController);
        Debug.Log("Build OnGrabEnd " + detachedController + " controllers " + Controllers);
        if (IsIdle)
            SceneObject.EndBuildSelect();
    }
    protected override void OnCannotGrab()
    {
        base.OnCannotGrab();
        if(SceneObject != null)
            SceneObject.SetObjectOutline(ObjectOutline.OutlineState.Off);
    }
    private void ConfigureCollider(ModelAABB modelAABB)
    {
        _boxCollider.enabled = true;
        _boxCollider.center = modelAABB.Center;
        Vector3 size = modelAABB.Extents * 2;
        size = new Vector3(
            Mathf.Max(size.x, MinSize),
            Mathf.Max(size.y, MinSize),
            Mathf.Max(size.z, MinSize)
            );
        _boxCollider.size = size;
    }
    IEnumerator WaitForBundleItem()
    {
        //Debug.Log("Will wait for AABB");
        while (SceneObject.BundleItem == null)
            yield return null;
        ConfigureCollider(SceneObject.BundleItem.AABBInfo);
    }
    public void SetColliderOn(bool on)
    {
        _boxCollider.enabled = on;
    }
    protected void OnDisable()
    {
        _hasInit = false;
        if (_waitForBundleItem != null)
            StopCoroutine(_waitForBundleItem);
        _waitForBundleItem = null;
    }
}
