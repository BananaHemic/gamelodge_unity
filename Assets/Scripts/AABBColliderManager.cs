using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AABBColliderManager : GenericSingleton<AABBColliderManager>
{
    public GameObject AABBColliderPrefab;
    public static readonly string AABBColliderName = "AABBCollider";

    void Start()
    {
        
    }
    public AABBCollider RequestAABBCollider(SceneObject sceneObject)
    {
        GameObject obj = SimplePool.Instance.Spawn(AABBColliderPrefab);
        obj.name = AABBColliderName;
        AABBCollider aabbCollider = obj.GetComponent<AABBCollider>();
        aabbCollider.Init(sceneObject);
        return aabbCollider;
    }
    public void ReturnAABBCollider(AABBCollider aabbCollider)
    {
        aabbCollider.DeInit();
        SimplePool.Instance.Despawn(aabbCollider.gameObject);
    }
}
