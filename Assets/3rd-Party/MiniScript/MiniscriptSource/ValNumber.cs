using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Miniscript
{
	/// <summary>
	/// ValNumber represents a numeric (double-precision floating point) value in MiniScript.
	/// Since we also use numbers to represent boolean values, ValNumber does that job too.
	/// </summary>
	public class ValNumber : PoolableValue {
		public double value { get; private set; }
        [ThreadStatic]
        protected static ValuePool<ValNumber> _valuePool;
#if MINISCRIPT_DEBUG
        [ThreadStatic]
        protected static uint _numInstancesAllocated = 0;
        public static long NumInstancesInUse { get { return _numInstancesAllocated - (_valuePool == null ? 0 : _valuePool.Count); } }

        private static int _num = 0;
        public static List<int> _instances = new List<int>();
        public int _id;
#endif

		private ValNumber(double value, bool usePool) : base(usePool) {
			this.value = value;
#if MINISCRIPT_DEBUG
            this._id = _num++;
#endif
		}
        public static ValNumber Create(double value)
        {
            //Console.WriteLine("Alloc num " + value + " ID " + (_num));
#if MINISCRIPT_DEBUG
            _instances.Add(_num);
#endif
            // Use a predefined static for common values
            switch (value)
            {
                case -1:
                    return _negativeOne;
                case 0:
                    return _zero;
                case 1:
                    return _one;
                case 2:
                    return _two;
                case 3:
                    return _three;
            }
            if (_valuePool == null)
                _valuePool = new ValuePool<ValNumber>();
            else
            {
                ValNumber val = _valuePool.GetInstance();
                if (val != null)
                {
                    val._refCount = 1;
                    val.value = value;
#if MINISCRIPT_DEBUG
                    val._id = _num++;
#endif
                    return val;
                }
            }
#if MINISCRIPT_DEBUG
            _numInstancesAllocated++;
#endif

            return new ValNumber(value, true);
        }
#if MINISCRIPT_DEBUG
        public override void Unref()
        {
            //if (base._refCount == 1)
                //Console.WriteLine("Recyclying val " + value + " ID " + _id);
            if (base._refCount == 0)
                Console.WriteLine("Extra unref Val: " + value + " ID " + _id);
            base.Unref();
        }
        public override void Ref()
        {
            base.Ref();
        }
#endif
        protected override void ResetState() {
            //Console.WriteLine("Return num " + value + " ID " + _id);
#if MINISCRIPT_DEBUG
            if (!_instances.Remove(_id))
                Console.WriteLine("Extra ID " + _id);
#endif
        }
        protected override void ReturnToPool()
        {
            if (!base._poolable)
                return;
            if (_valuePool == null)
                _valuePool = new ValuePool<ValNumber>();
            _valuePool.ReturnToPool(this);
        }

        public override string ToString(Machine vm) {
			// Convert to a string in the standard MiniScript way.
			if (value % 1.0 == 0.0) {
				// integer values as integers
				return value.ToString("0", CultureInfo.InvariantCulture);
			} else if (value > 1E10 || value < -1E10 || (value < 1E-6 && value > -1E-6)) {
				// very large/small numbers in exponential form
				return value.ToString("E6", CultureInfo.InvariantCulture);
			} else {
				// all others in decimal form, with 1-6 digits past the decimal point
				return value.ToString("0.0#####", CultureInfo.InvariantCulture);
			}
		}

		public override int IntValue() {
			return (int)value;
		}

		public override double DoubleValue() {
			return value;
		}
		
		public override bool BoolValue() {
			// Any nonzero value is considered true, when treated as a bool.
			return value != 0;
		}

		public override bool IsA(Value type, Machine vm) {
			return type == vm.numberType;
		}

		public override int Hash(int recursionDepth=16) {
			return value.GetHashCode();
		}

		public override double Equality(Value rhs, int recursionDepth=16) {
			return rhs is ValNumber && ((ValNumber)rhs).value == value ? 1 : 0;
		}

        static ValNumber _zero = new ValNumber(0, false);
        static ValNumber _one = new ValNumber(1, false);
        static ValNumber _two = new ValNumber(2, false);
        static ValNumber _three = new ValNumber(3, false);
        static ValNumber _negativeOne = new ValNumber(-1, false);
		
		/// <summary>
		/// Handy accessor to a shared "zero" (0) value.
		/// IMPORTANT: do not alter the value of the object returned!
		/// </summary>
		public static ValNumber zero { get { return _zero; } }
		
		/// <summary>
		/// Handy accessor to a shared "one" (1) value.
		/// IMPORTANT: do not alter the value of the object returned!
		/// </summary>
		public static ValNumber one { get { return _one; } }
		
		/// <summary>
		/// Convenience method to get a reference to zero or one, according
		/// to the given boolean.  (Note that this only covers Boolean
		/// truth values; MiniScript also allows fuzzy truth values, like
		/// 0.483, but obviously this method won't help with that.)
		/// IMPORTANT: do not alter the value of the object returned!
		/// </summary>
		/// <param name="truthValue">whether to return 1 (true) or 0 (false)</param>
		/// <returns>ValNumber.one or ValNumber.zero</returns>
		public static ValNumber Truth(bool truthValue) {
			return truthValue ? one : zero;
		}
		
		/// <summary>
		/// Basically this just makes a ValNumber out of a double,
		/// BUT it is optimized for the case where the given value
		///	is either 0 or 1 (as is usually the case with truth tests).
		/// </summary>
		public static ValNumber Truth(double truthValue) {
			if (truthValue == 0.0) return zero;
			if (truthValue == 1.0) return one;
			return ValNumber.Create(truthValue);
		}
        public override int GetBaseMiniscriptType()
        {
            return MiniscriptTypeInts.ValNumberTypeInt;
        }
    }
}
