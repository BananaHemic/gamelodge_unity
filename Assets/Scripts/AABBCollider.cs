using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AABBCollider : MonoBehaviour
{
    public SceneObject SceneObject { get; private set; }

    private BoxCollider _boxCollider;
    private bool _hasInit = false;
    private Coroutine _waitForBundleItem;
    private bool _isColliderOn = true;
    private bool _isColliderPendingConfig = true;

    void Awake()
    {
        _boxCollider = GetComponent<BoxCollider>();
    }
    public void Init(SceneObject sceneObject)
    {
        if (_hasInit)
            Debug.LogError("Double init AABB Collider");
        _hasInit = true;

        SceneObject = sceneObject;
        transform.SetParent(SceneObject.transform, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        gameObject.layer = sceneObject.Layer;
        gameObject.tag = sceneObject.Tag;
        if (SceneObject.BundleItem == null)
            _waitForBundleItem = StartCoroutine(WaitForBundleItem());
        else
            ConfigureCollider(sceneObject.BundleItem.AABBInfo);
    }
    private void ConfigureCollider(ModelAABB modelAABB)
    {
        _isColliderPendingConfig = false;
        _boxCollider.center = modelAABB.Center;
        _boxCollider.size = modelAABB.Extents * 2;
        _boxCollider.enabled = !_isColliderPendingConfig && _isColliderOn;
    }
    IEnumerator WaitForBundleItem()
    {
        _isColliderPendingConfig = true;
        _boxCollider.enabled = false;
        //Debug.Log("Will wait for AABB");
        while (SceneObject.BundleItem == null)
            yield return null;
        ConfigureCollider(SceneObject.BundleItem.AABBInfo);
    }
    public void DeInit()
    {
        _hasInit = false;
        if (_waitForBundleItem != null)
            StopCoroutine(WaitForBundleItem());
        _waitForBundleItem = null;
        _isColliderOn = true;
        _boxCollider.sharedMaterial = null;
    }
    public void SetCollidersOn(bool colliderOn)
    {
        if (_isColliderOn == colliderOn)
            return;
        _isColliderOn = colliderOn;
        _boxCollider.enabled = !_isColliderPendingConfig && _isColliderOn;
    }
    public void SetPhysicsMaterial(PhysicMaterial physicsMaterial)
    {
        _boxCollider.sharedMaterial = physicsMaterial;
    }
}
