using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Miniscript
{
    public abstract class ValCustom : PoolableValue
    {
        protected ValCustom(bool usePool) : base(usePool)
        {
        }
        public override int GetBaseMiniscriptType()
        {
            return MiniscriptTypeInts.ValCustomTypeInt;
        }
        public abstract bool Resolve(string identifier, out Value val);
        public virtual Value Lookup(Value key) { return null; }
        public virtual Value APlusB(Value other, int otherType, Context context, bool isSelfLhs)
        {
            return null;
        }
        public virtual Value AMinusB(Value rhs, int rhsType, Context context, bool isSelfLhs)
        {
            return null;
        }
        public virtual Value ATimesB(Value rhs, int rhsType, Context context, bool isSelfLhs)
        {
            return null;
        }
        public virtual Value ADividedByB(Value rhs, int rhsType, Context context, bool isSelfLhs)
        {
            return null;
        }
    }
}
