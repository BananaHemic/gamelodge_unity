using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Miniscript
{
	/// <summary>
	/// ValList represents a MiniScript list (which, under the hood, is
	/// just a wrapper for a List of Values).
	/// </summary>
	public class ValList : PoolableValue {
		public static long maxSize = 0xFFFFFF;      // about 16 MB

        public int Count { get { return values.Count - StartIndex; } }
        /// <summary>
        /// Pull is a very common operation, so to speed it up we
        /// simply keep a buffer in the front, instead of doing a RemoveAt
        /// each time
        /// </summary>
        public int StartIndex { get; private set; }
        private readonly List<Value> values;
        [ThreadStatic]
        private static ValuePool<ValList> _valuePool;
        [ThreadStatic]
        private static StringBuilder _workingStringBuilder;

#if MINISCRIPT_DEBUG
        [ThreadStatic]
        protected static uint _numInstancesAllocated = 0;
        public static long NumInstancesInUse { get { return _numInstancesAllocated - (_valuePool == null ? 0 : _valuePool.Count); } }
        private static int _num;
        public int _id;
#endif

        private ValList(int capacity, bool poolable) : base(poolable) {
            values = new List<Value>(capacity);
#if MINISCRIPT_DEBUG
            _id = _num++;
#endif
		}
        public static ValList Create(int capacity=0)
        {
            //Console.WriteLine("ValList create cap = " + capacity + " ID " + _num);
            if (_valuePool == null)
                _valuePool = new ValuePool<ValList>();
            else
            {
                ValList existing = _valuePool.GetInstance();
                if(existing != null)
                {
                    existing._refCount = 1;
                    existing.EnsureCapacity(capacity);
#if MINISCRIPT_DEBUG
                    existing._id = _num++;
#endif
                    return existing;
                }
            }

#if MINISCRIPT_DEBUG
            _numInstancesAllocated++;
#endif
            return new ValList(capacity, true);
        }
#if MINISCRIPT_DEBUG
        public override void Ref()
        {
            if(_id == 1)
            { }
            base.Ref();
        }
        public override void Unref()
        {
            if(_id == 1)
            { }
            if(base._refCount == 0)
                Console.WriteLine("ValList #" + _id + " double unref");
            base.Unref();
        }
#endif
        protected override void ResetState()
        {
            for(int i = StartIndex; i < values.Count;i++)
                values[i]?.Unref();
            //Console.WriteLine("ValList #" + _id + " back in pool");
            values.Clear();
            StartIndex = 0;
        }
        public void Add(Value value, bool takeRef=true)
        {
            ValString str = value as ValString;
            if (takeRef)
                value?.Ref();
            values.Add(value);
        }
        public void SetToList(List<Value> recvValues)
        {
            // Ref all the input variables
            // we need to do this first, otherwise, there's
            // weird issues where we unref into the pool, then ref
            for (int i = 0; i < recvValues.Count; i++)
                recvValues[i]?.Ref();
            // Unref the values we have
            for (int i = StartIndex; i < values.Count; i++)
                values[i]?.Unref();
            values.Clear();
            StartIndex = 0;
            // Copy them over
            for (int i = 0; i < recvValues.Count; i++)
                values.Add(recvValues[i]);
        }
        public void EnsureCapacity(int capacity)
        {
            if (values.Capacity < capacity)
                values.Capacity = capacity; //TODO maybe enfore this being a PoT?
        }
        protected override void ReturnToPool()
        {
            if (!base._poolable)
                return;
            if (_valuePool == null)
                _valuePool = new ValuePool<ValList>();
            _valuePool.ReturnToPool(this);
            //Console.WriteLine("ValList #" + _id + " returned");
        }
        public void Insert(int idx, Value value)
        {
            ValString str = value as ValString;
            value?.Ref();
            values.Insert(StartIndex + idx, value);
        }
        public override Value FullEval(Context context) {
			// Evaluate each of our list elements, and if any of those is
			// a variable or temp, then resolve those now.
			// CAUTION: do not mutate our original list!  We may need
			// it in its original form on future iterations.
			ValList result = null;
			for (var i = StartIndex; i < values.Count; i++) {
				var copied = false;
				if (values[i] is ValTemp || values[i] is ValVar) {
					Value newVal = values[i].Val(context, false);
					if (newVal != values[i]) {
						// OK, something changed, so we're going to need a new copy of the list.
						if (result == null) {
							result = ValList.Create();
							for (var j = 0; j < i; j++) result.Add(values[j]);
						}
						result.Add(newVal);
						copied = true;
					}
				}
				if (!copied && result != null) {
					// No change; but we have new results to return, so copy it as-is
					result.Add(values[i]);
				}
			}
			return result ?? this;
		}

		public ValList EvalCopy(Context context) {
			// Create a copy of this list, evaluating its members as we go.
			// This is used when a list literal appears in the source, to
			// ensure that each time that code executes, we get a new, distinct
			// mutable object, rather than the same object multiple times.
			var result = ValList.Create(values.Count);
			for (var i = StartIndex; i < values.Count; i++) {
                // Sometimes Val is a ValTemp that returns a value that should be reffed
                // so we Val without Refing, then Ref during Add
				result.Add(values[i] == null ? null : values[i].Val(context, false), true);
			}
			return result;
		}

		public override string CodeForm(Machine vm, int recursionLimit=-1) {
			if (recursionLimit == 0) return "[...]";
			if (recursionLimit > 0 && recursionLimit < 3 && vm != null) {
				string shortName = vm.FindShortName(this);
				if (shortName != null) return shortName;
			}
            if (_workingStringBuilder == null)
                _workingStringBuilder = new StringBuilder();
            else
                _workingStringBuilder.Clear();
            _workingStringBuilder.Append("[");
			for (var i = StartIndex; i < values.Count; i++) {
                Value val = values[i];
                _workingStringBuilder.Append(val == null ? "null" : val.CodeForm(vm, recursionLimit - 1));
                if (i != values.Count - 1)
                    _workingStringBuilder.Append(", ");
			}
            _workingStringBuilder.Append("]");
            return _workingStringBuilder.ToString();
		}

		public override string ToString(Machine vm) {
			return CodeForm(vm, 3);
		}

		public override bool BoolValue() {
			// A list is considered true if it is nonempty.
			return values != null && Count > 0;
		}

		public override bool IsA(Value type, Machine vm) {
			return type == vm.listType;
		}

		public override int Hash(int recursionDepth=16) {
			//return values.GetHashCode();
			int result = Count.GetHashCode();
			if (recursionDepth < 1) return result;
			for (var i = StartIndex; i < values.Count; i++) {
				result ^= values[i].Hash(recursionDepth-1);
			}
			return result;
		}

		public override double Equality(Value rhs, int recursionDepth=16) {
			if (!(rhs is ValList)) return 0;
            ValList rList = rhs as ValList;
			List<Value> rhl = rList.values;
            int rStart = rList.StartIndex;
			if (rhl == values) return 1;  // (same list)
			int count = Count;
			if (count != rhl.Count) return 0;
			if (recursionDepth < 1) return 0.5;		// in too deep
			double result = 1;
			for (var i = 0; i < Count; i++) {
				result *= values[StartIndex + i].Equality(rhl[rStart + i], recursionDepth-1);
				if (result <= 0) break;
			}
			return result;
		}

		public override bool CanSetElem() { return true; }

		public override void SetElem(Value index, Value value) {
            SetElem(index, value, true);
		}
		public void SetElem(Value index, Value value, bool takeValueRef) {
			var i = index.IntValue();
			if (i < 0) i += Count;
			if (i < 0 || i >= Count) {
				throw new IndexException("Index Error (list index " + index + " out of range)");
			}
            i += StartIndex;
            ValString str = value as ValString;
            // Unref existing
            values[i]?.Unref();
            // Ref new
            if (takeValueRef)
                value?.Ref();
			values[i] = value;
		}
        public void RemoveAt(int i)
        {
            values[i + StartIndex]?.Unref();
            // If this is the first element,
            // then just move StartIndex, instead of
            // doing an expensive RemoveAt
            if (i == 0)
            {
                values[i + StartIndex] = null;
                StartIndex++;
            }
            else
                values.RemoveAt(StartIndex + i);
        }
        public Value GetElem(Value index) {
			var i = index.IntValue();
			if (i < 0) i += Count;
			if (i < 0 || i >= Count) {
				throw new IndexException("Index Error (list index " + index + " out of range)");
			}
            i += StartIndex;
			return values[i];
		}
        public int IndexOf(Value val, int after=0)
        {
            if(val == null)
            {
                for(int i = StartIndex + after; i < values.Count; i++)
                {
                    Value v = values[i];
                    if (v == null)
                        return i;
                }
            }
            else
            {
                for(int i = StartIndex + after; i < values.Count; i++)
                {
                    Value v = values[i];
                    if (v != null && v.Equality(val) == 1)
                        return i;
                }
            }
            return -1;
        }
        public void Sort()
        {
            if (StartIndex > 0)
            {
                values.RemoveRange(0, StartIndex);
                StartIndex = 0;
            }
            // Sort the list in place
            values.Sort(ValueSorter.instance);
        }
        public Value this[int i]
        {
            get { return values[i + StartIndex]; }
            set {
                i += StartIndex;
                values[i]?.Unref();
                value?.Ref();
                values[i] = value;
            }
        }
        public override int GetBaseMiniscriptType()
        {
            return MiniscriptTypeInts.ValListTypeInt;
        }
    }
}
