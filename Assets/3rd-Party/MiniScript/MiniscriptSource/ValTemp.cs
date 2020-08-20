using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Miniscript
{
    //TODO ValTemp is allocated frequently, we should pool it
	public class ValTemp : Value {
		public int tempNum;

		public ValTemp(int tempNum) {
			this.tempNum = tempNum;
		}

		public override Value Val(Context context, bool takeRef) {
			Value v = context.GetTemp(tempNum);
            if (takeRef && v != null)
                v.Ref();
            return v;
		}

		public override Value Val(Context context, out ValMap valueFoundIn) {
			valueFoundIn = null;
			return context.GetTemp(tempNum);
		}

		public override string ToString(Machine vm) {
			return "_" + tempNum.ToString(CultureInfo.InvariantCulture);
		}

		public override int Hash(int recursionDepth=16) {
			return tempNum.GetHashCode();
		}

		public override double Equality(Value rhs, int recursionDepth=16) {
			return rhs is ValTemp && ((ValTemp)rhs).tempNum == tempNum ? 1 : 0;
		}

        public override int GetBaseMiniscriptType()
        {
            return MiniscriptTypeInts.ValTempTypeInt;
        }
	}
}
