using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// An only-physics representation of a SceneObject.
/// This is used so that we can immediately move a mesh renderer,
/// while still having correct the physics position
/// </summary>
public class PhysicsFollowHand : MonoBehaviour
{
    public Rigidbody OurRigidbody;
    [SerializeField]
    private Transform _modelTransform;
    private static readonly List<Collider> _workingColliderArray = new List<Collider>(4);

    private readonly List<Collider> _originalColliders = new List<Collider>(4);
    private bool _isInit = false;
    private int _layer;

    public void Init(SceneObject sceneObject)
    {
        if (_isInit)
            Debug.LogError("Double init of PhysicsFollowHand!");
        _isInit = true;
        if(sceneObject.Model == null)
        {
            Debug.LogError("Can't init PhysicsFollowHand when there is no model!");
            return;
        }
        Rigidbody theirRigidbody = sceneObject.Rigidbody;
        OurRigidbody.angularDrag = theirRigidbody.angularDrag;
        OurRigidbody.mass = theirRigidbody.mass;
        OurRigidbody.drag = theirRigidbody.drag;
        OurRigidbody.interpolation = theirRigidbody.interpolation;
        OurRigidbody.isKinematic = true;

        _layer = sceneObject.Layer;
        _modelTransform = new GameObject("ModelTransform").transform;
        _modelTransform.SetParent(transform);
        _modelTransform.gameObject.layer = _layer;
        
        // Move us to where the SceneObject is
        transform.SetParent(sceneObject.transform.parent);
        transform.localPosition = sceneObject.transform.localPosition;
        transform.localRotation = sceneObject.transform.localRotation;
        transform.localScale = sceneObject.transform.localScale;
        // Move the model to where the scene object has one
        _modelTransform.localPosition = sceneObject.Model.transform.localPosition;
        _modelTransform.localRotation = sceneObject.Model.transform.localRotation;
        _modelTransform.localScale = sceneObject.Model.transform.localScale;

        LoadColliders(sceneObject.Model.transform, _modelTransform);
    }
    private void LoadColliders(Transform originalTransform, Transform ourTransform)
    {
        // Get all active colliders, and disable them
        _workingColliderArray.Clear();
        originalTransform.gameObject.GetComponents(_workingColliderArray);
        for(int i = 0; i < _workingColliderArray.Count; i++)
        {
            Collider collider = _workingColliderArray[i];
            _originalColliders.Add(collider);
            if (collider.enabled)
            {
                // Create a new collider on object
                CopyCollider(collider, ourTransform.gameObject);
                collider.enabled = false;
            }
        }

        // Recursively check all children in a breadth-first
        // fashion
        int nChildren = originalTransform.childCount;
        for(int i = 0; i < nChildren; i++)
        {
            Transform child = originalTransform.GetChild(i);
            // We need to create the same hierarchy present in
            // the original, so we create a new child transform here
            GameObject createdChild = new GameObject(child.name);
            createdChild.layer = _layer;
            createdChild.transform.SetParent(ourTransform);
            createdChild.transform.localPosition = child.localPosition;
            createdChild.transform.localRotation = child.localRotation;
            createdChild.transform.localScale = child.localScale;

            LoadColliders(child, createdChild.transform);
        }
     }
    //TODO it might be nice to have a pool for each collider type
    private static void CopyCollider(Collider original, GameObject holder)
    {
        //Debug.Log("Got collider of type " + original.GetType());
        Type originalType = original.GetType();

        Collider addedCollider;
        if(originalType == typeof(BoxCollider))
        {
            BoxCollider originalTypedCollider = original as BoxCollider;
            BoxCollider addedTypedCollider = holder.AddComponent<BoxCollider>();
            addedCollider = addedTypedCollider;
            addedTypedCollider.center = originalTypedCollider.center;
            addedTypedCollider.size = originalTypedCollider.size;
        }else if(originalType == typeof(SphereCollider))
        {
            SphereCollider originalTypedCollider = original as SphereCollider;
            SphereCollider addedTypedCollider = holder.AddComponent<SphereCollider>();
            addedCollider = addedTypedCollider;
            addedTypedCollider.center = originalTypedCollider.center;
            addedTypedCollider.radius = originalTypedCollider.radius;
        }else if(originalType == typeof(CapsuleCollider))
        {
            CapsuleCollider originalTypedCollider = original as CapsuleCollider;
            CapsuleCollider addedTypedCollider = holder.AddComponent<CapsuleCollider>();
            addedCollider = addedTypedCollider;
            addedTypedCollider.center = originalTypedCollider.center;
            addedTypedCollider.radius = originalTypedCollider.radius;
            addedTypedCollider.height = originalTypedCollider.height;
        }else if(originalType == typeof(WheelCollider))
        {
            WheelCollider originalTypedCollider = original as WheelCollider;
            WheelCollider addedTypedCollider = holder.AddComponent<WheelCollider>();
            addedCollider = addedTypedCollider;
            addedTypedCollider.center = originalTypedCollider.center;
            addedTypedCollider.brakeTorque = originalTypedCollider.brakeTorque;
            addedTypedCollider.forceAppPointDistance = originalTypedCollider.forceAppPointDistance;
            addedTypedCollider.forwardFriction = originalTypedCollider.forwardFriction;
            addedTypedCollider.mass = originalTypedCollider.mass;
            addedTypedCollider.motorTorque = originalTypedCollider.motorTorque;
            addedTypedCollider.radius = originalTypedCollider.radius;
            addedTypedCollider.sidewaysFriction = originalTypedCollider.sidewaysFriction;
            addedTypedCollider.steerAngle = originalTypedCollider.steerAngle;
            addedTypedCollider.suspensionDistance = originalTypedCollider.suspensionDistance;
            addedTypedCollider.suspensionSpring = originalTypedCollider.suspensionSpring;
            addedTypedCollider.wheelDampingRate = originalTypedCollider.wheelDampingRate;
        }else if(originalType == typeof(MeshCollider))
        {
            MeshCollider originalTypedCollider = original as MeshCollider;
            MeshCollider addedTypedCollider = holder.AddComponent<MeshCollider>();
            addedCollider = addedTypedCollider;
            addedTypedCollider.convex = originalTypedCollider.convex;
            addedTypedCollider.cookingOptions = originalTypedCollider.cookingOptions;
            addedTypedCollider.sharedMesh = originalTypedCollider.sharedMesh;
        }else if(originalType == typeof(TerrainCollider))
        {
            TerrainCollider originalTypedCollider = original as TerrainCollider;
            TerrainCollider addedTypedCollider = holder.AddComponent<TerrainCollider>();
            addedCollider = addedTypedCollider;
            addedTypedCollider.terrainData = originalTypedCollider.terrainData;
        }
        else
        {
            Debug.LogError("Unhandled collider of type: " + originalType);
            return;
        }
        addedCollider.contactOffset = original.contactOffset;
        addedCollider.isTrigger = original.isTrigger;
        addedCollider.sharedMaterial = original.sharedMaterial;
    }
    public void ResetState()
    {
        if (!_isInit)
            Debug.LogError("Resetting state for PhysicsFollowHand w/o init!");
        _isInit = false;
        // Re-enabled all disabled original colliders
        foreach (var collider in _originalColliders)
            collider.enabled = true;
        _originalColliders.Clear();
        // Destroy the collider we made
        if (_modelTransform != null)
            Destroy(_modelTransform.gameObject);
        _modelTransform = null;
    }
}
