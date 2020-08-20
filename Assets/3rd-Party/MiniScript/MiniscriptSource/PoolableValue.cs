using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Miniscript
{
    /// <summary>
    /// In certain platforms, like Unity, memory allocation can
    /// be very slow. To help alleviate this, Minscript values
    /// are pooled in a thread static stack of unused values
    /// </summary>
    public abstract class PoolableValue : Value
    {
        protected int _refCount = 1;
        protected bool _poolable;

        protected PoolableValue(bool usePool)
        {
            _poolable = usePool;
        }
        protected abstract void ResetState();
        protected abstract void ReturnToPool();
        public override void Ref()
        {
            if (!_poolable)
                return;
            if (_refCount == 0)
                MiniCompat.LogError("Reffed out of the pool!");
            _refCount++;
        }
        public int GetRefCount()
        {
            return _refCount;
        }
        public override void Unref()
        {
            if (!_poolable)
                return;

            _refCount--;
            if (_refCount > 0)
                return;
            else if (_refCount < 0)
            {
                MiniCompat.LogError("Extra unref! For " + GetType().ToString());
                return;
            }
            ResetState();
            ReturnToPool();
            //Console.WriteLine("into pool " + GetType().ToString());
        }
        public override Value Val(Context context, bool takeRef)
        {
            if(takeRef)
                Ref();
            return base.Val(context, takeRef);
        }
        public override Value Val(Context context, out ValMap valueFoundIn)
        {
            //Console.WriteLine("valref 2");
            Ref();
            return base.Val(context, out valueFoundIn);
        }

        protected class ValuePool<T> where T : PoolableValue
        {
            private Stack<T> _pool = new Stack<T>();
            public int Count { get { return _pool.Count; } }

            public T GetInstance()
            {
                if (_pool.Count == 0)
                    return null;
                //Console.WriteLine("from pool");
                T val = _pool.Pop();
                if(val._refCount != 0)
                    MiniCompat.LogError("Error, pulled value with too high a ref, " + val._refCount + " type " + val.GetType().ToString());
                return val;
            }
            public void ReturnToPool(T poolableValue)
            {
                _pool.Push(poolableValue);
            }
        }
    }
}
