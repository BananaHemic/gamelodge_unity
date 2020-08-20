///
/// Simple pooling for Unity.
///   Author: Martin "quill18" Glaude (quill18@quill18.com)
///   Latest Version: https://gist.github.com/quill18/5a7cfffae68892621267
///   License: CC0 (http://creativecommons.org/publicdomain/zero/1.0/)
///   UPDATES:
/// 	2015-04-16: Changed Pool to use a Stack generic.
/// 
/// Usage:
/// 
///   There's no need to do any special setup of any kind.
/// 
///   Instead of calling Instantiate(), use this:
///       SimplePool.Spawn(somePrefab, somePosition, someRotation);
/// 
///   Instead of destroying an object, use this:
///       SimplePool.Despawn(myGameObject);
/// 
///   If desired, you can preload the pool with a number of instances:
///       SimplePool.Preload(somePrefab, 20);
/// 
/// Remember that Awake and Start will only ever be called on the first instantiation
/// and that member variables won't be reset automatically.  You should reset your
/// object yourself after calling Spawn().  (i.e. You'll have to do things like set
/// the object's HPs to max, reset animation states, etc...)
/// 
/// 
/// 


using UnityEngine;
using System.Collections.Generic;

public class SimplePool : MonoBehaviour
{
    public RectTransform UIPoolHolder;
    public static SimplePool Instance { get; private set; }
    private void Awake()
    {
        if (Instance != null)
        {
            Debug.LogError("Destroying SimplePool");
            Destroy(Instance);
        }
        Instance = this;
    }

    // You can avoid resizing of the Stack's internal data by
    // setting this to a number equal to or greater to what you
    // expect most of your pool sizes to be.
    // Note, you can also use Preload() to set the initial size
    // of a pool -- this can be handy if only some of your pools
    // are going to be exceptionally large (for example, your bullets.)
    const int DEFAULT_POOL_SIZE = 3;

    /// <summary>
    /// The Pool class represents the pool for a particular prefab.
    /// </summary>
    class Pool
    {
        // We append an id to the name of anything we instantiate.
        // This is purely cosmetic.
        int nextId = 1;

        // The structure containing our inactive objects.
        // Why a Stack and not a List? Because we'll never need to
        // pluck an object from the start or middle of the array.
        // We'll always just grab the last one, which eliminates
        // any need to shuffle the objects around in memory.
        Stack<GameObject> inactive;

        // The prefab that we are pooling
        GameObject prefab;

        // Constructor
        public Pool(GameObject prefab, int initialQty)
        {
            this.prefab = prefab;

            // If Stack uses a linked list internally, then this
            // whole initialQty thing is a placebo that we could
            // strip out for more minimal code. But it can't *hurt*.
            inactive = new Stack<GameObject>(initialQty);
        }

        // Spawn an object from our pool
        public GameObject Spawn(Vector3 pos, Quaternion rot, bool enable=true)
        {
            GameObject obj;
            if (inactive.Count == 0)
            {
                //Debug.Log("instantiating");
                // We don't have an object in our pool, so we
                // instantiate a whole new object.
                obj = (GameObject)GameObject.Instantiate(prefab, pos, rot);
#if UNITY_EDITOR
                obj.name = prefab.name + " (" + (nextId++) + ")";
#endif

                // Add a PoolMember component so we know what pool
                // we belong to.
                obj.AddComponent<PoolMember>().myPool = this;
            }
            else
            {
                // Grab the last object in the inactive array
                obj = inactive.Pop();

                if (obj == null)
                {
                    // The inactive object we expected to find no longer exists.
                    // The most likely causes are:
                    //   - Someone calling Destroy() on our object
                    //   - A scene change (which will destroy all our objects).
                    //     NOTE: This could be prevented with a DontDestroyOnLoad
                    //	   if you really don't want this.
                    // No worries -- we'll just try the next one in our sequence.

                    return Spawn(pos, rot, enable);
                }
            }

            obj.transform.position = pos;
            obj.transform.rotation = rot;
            if(enable)
                obj.SetActive(true);
            return obj;

        }

        // Return an object to the inactive pool.
        public void Despawn(GameObject obj)
        {
            obj.SetActive(false);

            // Since Stack doesn't have a Capacity member, we can't control
            // the growth factor if it does have to expand an internal array.
            // On the other hand, it might simply be using a linked list 
            // internally.  But then, why does it allow us to specify a size
            // in the constructor? Maybe it's a placebo? Stack is weird.
            inactive.Push(obj);
        }

    }


    /// <summary>
    /// Added to freshly instantiated objects, so we can link back
    /// to the correct pool on despawn.
    /// </summary>
    class PoolMember : MonoBehaviour
    {
        public Pool myPool;
    }

    // All of our pools
    private readonly Dictionary<GameObject, Pool> pools = new Dictionary<GameObject, Pool>();

    /// <summary>
    /// Initialize our dictionary.
    /// </summary>
    private void Init(GameObject prefab = null, int qty = DEFAULT_POOL_SIZE)
    {
        if (prefab != null && pools.ContainsKey(prefab) == false)
        {
            pools[prefab] = new Pool(prefab, qty);
        }
    }

    /// <summary>
    /// If you want to preload a few copies of an object at the start
    /// of a scene, you can use this. Really not needed unless you're
    /// going to go from zero instances to 100+ very quickly.
    /// Could technically be optimized more, but in practice the
    /// Spawn/Despawn sequence is going to be pretty darn quick and
    /// this avoids code duplication.
    /// </summary>
    public void Preload(GameObject prefab, int qty = 1)
    {
        Init(prefab, qty);

        // Make an array to grab the objects we're about to pre-spawn.
        GameObject[] obs = new GameObject[qty];
        for (int i = 0; i < qty; i++)
        {
            obs[i] = Spawn(prefab, Vector3.zero, Quaternion.identity);
        }

        // Now despawn them all.
        for (int i = 0; i < qty; i++)
        {
            Despawn(obs[i]);
        }
    }

    /// <summary>
    /// Spawns a copy of the specified prefab (instantiating one if required).
    /// NOTE: Remember that Awake() or Start() will only run on the very first
    /// spawn and that member variables won't get reset.  OnEnable will run
    /// after spawning -- but remember that toggling IsActive will also
    /// call that function.
    /// </summary>
    public GameObject Spawn(GameObject prefab, Vector3 pos, Quaternion rot)
    {
        Init(prefab);

        return pools[prefab].Spawn(pos, rot);
    }

    public GameObject Spawn(GameObject prefab)
    {
        return Spawn(prefab, Vector3.zero, Quaternion.identity);
    }
    //TODO option to just get a pool, to avoid an annoying dictionary lookup
    public GameObject SpawnUI(GameObject prefab, Transform newParent, bool worldPositionStays=false)
    {
        Init(prefab);
        // Faster to reparent, and then enable
        GameObject newObj = pools[prefab].Spawn(Vector3.zero, Quaternion.identity, false);
        newObj.transform.SetParent(newParent, worldPositionStays);
        newObj.SetActive(true);
        newObj.transform.localPosition = Vector3.zero;
        newObj.transform.localRotation = Quaternion.identity;
        return newObj;
    }
    /// <summary>
    /// Despawn the specified gameobject back into its pool.
    /// </summary>
    public void Despawn(GameObject obj)
    {
        PoolMember pm = obj.GetComponent<PoolMember>();
        if (pm == null)
        {
            Debug.Log("Object '" + obj.name + "' wasn't spawned from a pool. Destroying it instead.");
            GameObject.Destroy(obj);
        }
        else
        {
            // Set the object to be under a child of this object
            // so that it isn't deleted
            obj.transform.parent = this.transform;
            pm.myPool.Despawn(obj);
        }
    }
    /// <summary>
    /// Despawn the specified gameobject back into its pool.
    /// </summary>
    public void DespawnUI(GameObject uiObj)
    {
        PoolMember pm = uiObj.GetComponent<PoolMember>();
        if (pm == null)
        {
            Debug.Log("Object '" + uiObj.name + "' wasn't spawned from a pool. Destroying it instead.");
            GameObject.Destroy(uiObj);
        }
        else
        {
            // We despawn/make the object inactive, and then move it's parent. This eliminates an unneeded dirtying
            // https://unity3d.com/how-to/unity-ui-optimization-tips
            pm.myPool.Despawn(uiObj);
            // Set the object to be under a child of this object
            // so that it isn't deleted
            uiObj.transform.SetParent(UIPoolHolder, false);
        }
    }
}