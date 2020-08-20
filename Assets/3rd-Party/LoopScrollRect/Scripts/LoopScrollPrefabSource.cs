using UnityEngine;
using System.Collections;

namespace UnityEngine.UI
{
    public interface ILoopScrollPrefabSource
    {
        GameObject GetObject(Transform parent);
        void ReturnObject(GameObject go); 
    }
    [System.Serializable]
    public class LoopScrollPrefabSource : ILoopScrollPrefabSource
    {
        public string prefabName;
        public int poolSize = 5;

        private bool inited = false;
        public virtual GameObject GetObject(Transform parent)
        {
            if(!inited)
            {
                SG.ResourceManager.Instance.InitPool(prefabName, poolSize);
                inited = true;
            }
            var obj = SG.ResourceManager.Instance.GetObjectFromPool(prefabName);
            obj.transform.SetParent(parent, false);
            return obj;
        }

        public virtual void ReturnObject(GameObject go)
        {
            go.SendMessage("ScrollCellReturn", SendMessageOptions.DontRequireReceiver);
            SG.ResourceManager.Instance.ReturnObjectToPool(go.gameObject);
        }
    }
}
