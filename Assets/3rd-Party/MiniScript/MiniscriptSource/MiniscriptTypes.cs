/*	MiniscriptTypes.cs

Classes in this file represent the MiniScript type system.  Value is the 
abstract base class for all of them (i.e., represents ANY value in MiniScript),
from which more specific types are derived.

*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Text;
using System.Collections;

namespace Miniscript {

    public static class MiniscriptTypeInts
    {
        public const int ValNumberTypeInt = 1;
        public const int ValSeqElemTypeInt = 2;
        public const int ValStringTypeInt = 3;
        public const int ValTempTypeInt = 4;
        public const int ValVarTypeInt = 5;
        public const int ValNullTypeInt = 6;
        public const int ValMapTypeInt = 7;
        public const int ValListTypeInt = 8;
        public const int ValFunctionTypeInt = 9;
        public const int ValCustomTypeInt = 10;
    }

	public class ValueSorter : IComparer<Value> {
		public static ValueSorter instance = new ValueSorter();
		public int Compare(Value x, Value y) {
			return Value.Compare(x, y);
		}
	}
	
	public class RValueEqualityComparer : IEqualityComparer<Value> {
		public bool Equals(Value val1, Value val2) {
			return val1.Equality(val2) > 0;
		}

		public int GetHashCode(Value val) {
			return val.Hash();
		}

		static RValueEqualityComparer _instance = null;
		public static RValueEqualityComparer instance {
			get {
				if (_instance == null) _instance = new RValueEqualityComparer();
				return _instance;
			}
		}
	}
}

