using UnityEngine;
using System.Collections;

namespace UnityEngine.UI
{
    public interface ILoopScrollDataSource
    {
        void ProvideData(GameObject go, int idx, object userData);
    }

	public class LoopScrollSendIndexSource : ILoopScrollDataSource
    {
		public static readonly LoopScrollSendIndexSource Instance = new LoopScrollSendIndexSource();

		LoopScrollSendIndexSource(){}

        public void ProvideData(GameObject go, int idx, object userData)
        {
            int i = (int)userData;
            Debug.Log("Providing data for #" + i);
            go.SendMessage("ScrollCellIndex", i);
        }
    }

	public class LoopScrollArraySource<T> : ILoopScrollDataSource
    {
        T[] objectsToFill;

		public LoopScrollArraySource(T[] objectsToFill)
        {
            this.objectsToFill = objectsToFill;
        }

        public void ProvideData(GameObject go, int idx, object userData)
        {
            go.SendMessage("ScrollCellContent", objectsToFill[idx]);
        }
    }
}