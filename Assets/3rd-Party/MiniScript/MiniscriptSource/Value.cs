using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Miniscript
{
	/// <summary>
	/// Value: abstract base class for the MiniScript type hierarchy.
	/// Defines a number of handy methods that you can call on ANY
	/// value (though some of these do nothing for some types).
	/// </summary>
	public abstract class Value {
        /// <summary>
        /// Returns an int for the type of the object. It's faster to
        /// do this than a bunch of "is xyz" everywhere
        /// </summary>
        /// <returns></returns>
        public abstract int GetBaseMiniscriptType();
        /// <summary>
        /// Get the current value of this Value in the given context.  Basic types
        /// evaluate to themselves, but some types (e.g. variable references) may
        /// evaluate to something else.
        /// </summary>
        /// <param name="context">TAC context to evaluate in</param>
        /// <returns>value of this value (possibly the same as this)</returns>
        public virtual Value Val(Context context, bool takeRef) {
			return this;		// most types evaluate to themselves
		}
		
		public override string ToString() {
			return ToString(null);
		}

        // Mainly used for pooling, so that we don't have to cast all the time
        // Non-Poolables will have this as a no-op
        public virtual void Ref() { }
        public virtual void Unref() { }
		
		public abstract string ToString(Machine vm);
		
		/// <summary>
		/// This version of Val is like the one above, but also returns
		/// (via the output parameter) the ValMap the value was found in,
		/// which could be several steps up the __isa chain.
		/// </summary>
		/// <returns>The value.</returns>
		/// <param name="context">Context.</param>
		/// <param name="valueFoundIn">Value found in.</param>
		public virtual Value Val(Context context, out ValMap valueFoundIn) {
			valueFoundIn = null;
			return this;
		}
		
		/// <summary>
		/// Similar to Val, but recurses into the sub-values contained by this
		/// value (if it happens to be a container, such as a list or map).
		/// </summary>
		/// <param name="context">context in which to evaluate</param>
		/// <returns>fully-evaluated value</returns>
		public virtual Value FullEval(Context context) {
			return this;
		}
		
		/// <summary>
		/// Get the numeric value of this Value as an integer.
		/// </summary>
		/// <returns>this value, as signed integer</returns>
		public virtual int IntValue() {
			return (int)DoubleValue();
		}
		
		/// <summary>
		/// Get the numeric value of this Value as an unsigned integer.
		/// </summary>
		/// <returns>this value, as unsigned int</returns>
		public virtual uint UIntValue() {
			return (uint)DoubleValue();
		}
		
		/// <summary>
		/// Get the numeric value of this Value as a single-precision float.
		/// </summary>
		/// <returns>this value, as a float</returns>
		public virtual float FloatValue() {
			return (float)DoubleValue();
		}
		
		/// <summary>
		/// Get the numeric value of this Value as a double-precision floating-point number.
		/// </summary>
		/// <returns>this value, as a double</returns>
		public virtual double DoubleValue() {
			return 0;				// most types don't have a numeric value
		}
		
		/// <summary>
		/// Get the boolean (truth) value of this Value.  By default, we consider
		/// any numeric value other than zero to be true.  (But subclasses override
		/// this with different criteria for strings, lists, and maps.)
		/// </summary>
		/// <returns>this value, as a bool</returns>
		public virtual bool BoolValue() {
			return IntValue() != 0;
		}
		
		/// <summary>
		/// Get this value in the form of a MiniScript literal.
		/// </summary>
		/// <param name="recursionLimit">how deeply we can recurse, or -1 for no limit</param>
		/// <returns></returns>
		public virtual string CodeForm(Machine vm, int recursionLimit=-1) {
			return ToString(vm);
		}
		
		/// <summary>
		/// Get a hash value for this Value.  Two values that are considered
		/// equal will return the same hash value.
		/// </summary>
		/// <returns>hash value</returns>
		public abstract int Hash(int recursionDepth=16);
		
		/// <summary>
		/// Check whether this Value is equal to another Value.
		/// </summary>
		/// <param name="rhs">other value to compare to</param>
		/// <returns>1if these values are considered equal; 0 if not equal; 0.5 if unsure</returns>
		public abstract double Equality(Value rhs, int recursionDepth=16);
		
		/// <summary>
		/// Can we set elements within this value?  (I.e., is it a list or map?)
		/// </summary>
		/// <returns>true if SetElem can work; false if it does nothing</returns>
		public virtual bool CanSetElem() { return false; }
		
		/// <summary>
		/// Set an element associated with the given index within this Value.
		/// </summary>
		/// <param name="index">index/key for the value to set</param>
		/// <param name="value">value to set</param>
		public virtual void SetElem(Value index, Value value) {}

		/// <summary>
		/// Return whether this value is the given type (or some subclass thereof)
		/// in the context of the given virtual machine.
		/// </summary>
		public virtual bool IsA(Value type, Machine vm) {
			return false;
		}

		public static int Compare(Value x, Value y) {
			// If either argument is a string, do a string comparison
			if (x is ValString || y is ValString) {
					var sx = x.ToString();
					var sy = y.ToString();
					return sx.CompareTo(sy);
			}
			// If both arguments are numbers, compare numerically
			if (x is ValNumber && y is ValNumber) {
				double fx = ((ValNumber)x).value;
				double fy = ((ValNumber)y).value;
				if (fx < fy) return -1;
				if (fx > fy) return 1;
				return 0;
			}
			// Otherwise, consider all values equal, for sorting purposes.
			return 0;
		}
	}
}
