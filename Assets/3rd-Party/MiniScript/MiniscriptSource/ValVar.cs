using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Miniscript
{
	public class ValVar : Value {
		public string identifier;
		public bool noInvoke;	// reflects use of "@" (address-of) operator

		public ValVar(string identifier) {
			this.identifier = identifier;
		}

		public override Value Val(Context context, bool takeRef) {
			return context.GetVar(identifier);
		}

		public override Value Val(Context context, out ValMap valueFoundIn) {
			valueFoundIn = null;
			return context.GetVar(identifier);
		}

		public override string ToString(Machine vm) {
			if (noInvoke) return "@" + identifier;
			return identifier;
		}

		public override int Hash(int recursionDepth=16) {
			return identifier.GetHashCode();
		}

		public override double Equality(Value rhs, int recursionDepth=16) {
			return rhs is ValVar && ((ValVar)rhs).identifier == identifier ? 1 : 0;
		}

        public override int GetBaseMiniscriptType()
        {
            return MiniscriptTypeInts.ValVarTypeInt;
        }

		// Special name for the implicit result variable we assign to on expression statements:
		public static ValVar implicitResult = new ValVar("_");
	}
}
