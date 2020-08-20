using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Miniscript
{
	/// <summary>
	/// ValString represents a string (text) value.
	/// </summary>
	public class ValString : PoolableValue {
		public static long maxSize = 0xFFFFFF;		// about 16M elements
		
		public string value { get; protected set; }
        private bool _hasCachedHash = false;
        private int _cachedHash;
        // The unique ID for this instance. It is set only once
        // in constructor, so at two different time the same instance ID
        // can have different strings, unless pooling is off
        public readonly int InstanceID;
        // If this instance is a built-in value
        public readonly bool IsBuiltIn = false;

        [ThreadStatic]
        protected static ValuePool<ValString> _valuePool;
        [ThreadStatic]
        private static StringBuilder _workingSbA;
        [ThreadStatic]
        private static StringBuilder _workingSbB;

        private static int _nextInstanceID;
#if MINISCRIPT_DEBUG
        [ThreadStatic]
        protected static uint _numInstancesAllocated = 0;
        public static long NumInstancesInUse { get { return _numInstancesAllocated - (_valuePool == null ? 0 : _valuePool.Count); } }
        private static int _num;
        public int _id;
#endif

        //TODO add create with ValString for fast add
        public static ValString Create(string val, bool usePool=true) {
            if(!usePool)
                return new ValString(val, false);

            switch (val)
            {
                case "s":
                    return sStr;
                case "self":
                    return selfStr;
                case "to":
                    return toStr;
                case "from":
                    return fromStr;
                case "__isa":
                    return magicIsA;
                case "seq":
                    return seqStr;
                case "super":
                    return superStr;
                case "len":
                    return lenStr;
                case "__events":
                    return eventsStr;
                case "__eventVals":
                    return eventValsStr;
                case "__isAtEnd":
                    return isAtEndStr;
                case "yield":
                    return yieldStr;
                case " ":
                    return spaceStr;
                case "x":
                    return xStr;
                case "y":
                    return yStr;
                case "z":
                    return zStr;
                case "w":
                    return wStr;
                case "name":
                    return nameStr;
		        case "position":
                    return positionStr;
		        case "rotation":
                    return rotationStr;
		        case "velocity":
                    return velocityStr;
		        case "angularVelocity":
                    return angularVelocityStr;
		        case "forward":
                    return forwardStr;
		        case "right":
                    return rightStr;
		        case "time":
                    return timeStr;
		        case "deltaTime":
                    return deltaTimeStr;
		        case "frameCount":
                    return frameCountStr;
            }

            //Console.WriteLine("Alloc str \"" + val + "\" num " + _num);

            if (_valuePool == null)
                _valuePool = new ValuePool<ValString>();
            else
            {
                ValString valStr = _valuePool.GetInstance();
                if (valStr != null)
                {
                    valStr._refCount = 1;
                    valStr.value = val;
#if MINISCRIPT_DEBUG
                    valStr._id = _num++;
#endif
                    return valStr;
                }
            }

#if MINISCRIPT_DEBUG
            _numInstancesAllocated++;
#endif
            return new ValString(val, true);
        }
		protected ValString(string value, bool usePool) : base(usePool) {
			this.value = value ?? empty.value;
            InstanceID = _nextInstanceID++;
#if MINISCRIPT_DEBUG
            _id = _num++;
#endif
		}
		private ValString(string value, bool usePool, bool isBuiltIn) : this(value, usePool) {
            IsBuiltIn = isBuiltIn;
		}
#if MINISCRIPT_DEBUG
        public override void Ref()
        {
            if (!base._poolable)
                return;
            //Console.WriteLine("Str " + value + " ref, ref count #" + _refCount);
            base.Ref();
        }
        public override void Unref()
        {
            if (!base._poolable)
                return;
            if (base._refCount == 0)
                Console.WriteLine("Extra unref for: " + value + " ID " + _id);
            base.Unref();
        }
#endif
        protected override void ResetState()
        {
            _hasCachedHash = false;
            value = null;
        }
        protected override void ReturnToPool()
        {
            if (!base._poolable)
                return;
            if (_valuePool == null)
                _valuePool = new ValuePool<ValString>();
            _valuePool.ReturnToPool(this);
        }

        public override string ToString(Machine vm) {
			return value;
		}

		public override string CodeForm(Machine vm, int recursionLimit=-1) {
            if (_workingSbA == null)
                _workingSbA = new StringBuilder();
            else
                _workingSbA.Clear();
            if (_workingSbB == null)
                _workingSbB = new StringBuilder();
            else
                _workingSbB.Clear();
            _workingSbA.Append("\"");
            _workingSbB.Append(value);
            _workingSbB.Replace("\"", "\"\"");
            _workingSbA.Append(_workingSbB);
            _workingSbA.Append("\"");
            return _workingSbA.ToString();
			//return "\"" + value.Replace("\"", "\"\"") + "\"";
		}

		public override bool BoolValue() {
			// Any nonempty string is considered true.
			return !string.IsNullOrEmpty(value);
		}

		public override bool IsA(Value type, Machine vm) {
			return type == vm.stringType;
		}

		public override int Hash(int recursionDepth=16) {
            if (!_hasCachedHash) {
                _cachedHash = value.GetHashCode();
                _hasCachedHash = true;
            }
            return _cachedHash;
		}

		public override double Equality(Value rhs, int recursionDepth=16) {
			// String equality is treated the same as in C#.
			return rhs is ValString && ((ValString)rhs).value == value ? 1 : 0;
		}

		public Value GetElem(Value index) {
			var i = index.IntValue();
			if (i < 0) i += value.Length;
			if (i < 0 || i >= value.Length) {
				throw new IndexException("Index Error (string index " + index + " out of range)");

			}
			return ValString.Create(value.Substring(i, 1));
		}

        public override int GetBaseMiniscriptType()
        {
            return MiniscriptTypeInts.ValStringTypeInt;
        }

		// Magic identifier for the is-a entry in the class system:
		public static ValString selfStr = new ValString("self", false, true);
		public static ValString eventsStr = new ValString("__events", false, true);
		public static ValString eventValsStr = new ValString("__eventVals", false, true);
		public static ValString isAtEndStr = new ValString("__isAtEnd", false, true);
		public static ValString xStr = new ValString("x", false, true);
		public static ValString yStr = new ValString("y", false, true);
		public static ValString zStr = new ValString("z", false, true);
		public static ValString wStr = new ValString("w", false, true);
		public static ValString nameStr = new ValString("name", false, true);
		public static ValString positionStr = new ValString("position", false, true);
		public static ValString rotationStr = new ValString("rotation", false, true);
		public static ValString velocityStr = new ValString("velocity", false, true);
		public static ValString angularVelocityStr = new ValString("angularVelocity", false, true);
		public static ValString forwardStr = new ValString("forward", false, true);
		public static ValString rightStr = new ValString("right", false, true);
		public static ValString timeStr = new ValString("time", false, true);
		public static ValString deltaTimeStr = new ValString("deltaTime", false, true);
		public static ValString frameCountStr = new ValString("frameCount", false, true);
		public static ValString yieldStr = new ValString("yield", false, true);
		public static ValString magicIsA = new ValString("__isa", false, true);
		public static ValString sStr = new ValString("s", false, true);// Common, on account of print using this
		public static ValString spaceStr = new ValString(" ", false, true);
		public static ValString fromStr = new ValString("from", false, true);
		public static ValString toStr = new ValString("to", false, true);
		public static ValString seqStr = new ValString("seq", false, true);
		public static ValString superStr = new ValString("super", false, true);
		public static ValString lenStr = new ValString("len", false, true);
		public static ValString empty = new ValString("", false, true);
	}
}
