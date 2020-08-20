using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Miniscript
{
    public class Line : IDisposable {

        [ThreadStatic]
        private static StringBuilder _workingStringBuilder;
		
        public enum Op {
            Noop = 0,
            AssignA,
            AssignImplicit,
            APlusB,
            AMinusB,
            ATimesB,
            ADividedByB,
            AModB,
            APowB,
            AEqualB,
            ANotEqualB,
            AGreaterThanB,
            AGreatOrEqualB,
            ALessThanB,
            ALessOrEqualB,
            AisaB,
            AAndB,
            AOrB,
            BindAssignA,
            CopyA,
            NotA,
            GotoA,
            GotoAifB,
            GotoAifTrulyB,
            GotoAifNotB,
            PushParam,
            CallFunctionA,
            CallIntrinsicA,
            ReturnA,
            ElemBofA,
            ElemBofIterA,
            LengthOfA
        }

        public Value lhs;
        public Op op;
        public Value rhsA;
        public Value rhsB;
//			public string comment;
        public SourceLoc location;

        public Line(Value lhs, Op op, Value rhsA=null, Value rhsB=null) {
            this.lhs = lhs;
            this.op = op;
            this.rhsA = rhsA;
            this.rhsB = rhsB;
        }

        public void Dispose()
        {
            ValFunction rhsAFunc = rhsA as ValFunction;
            if (rhsAFunc != null)
                rhsAFunc.Dispose();
            ValFunction rhsBFunc = rhsB as ValFunction;
            if (rhsBFunc != null)
                rhsBFunc.Dispose();

            if (lhs != null)
                lhs.Unref();
            if (rhsA != null)
                rhsA.Unref();
            if (rhsB != null)
                rhsB.Unref();
            lhs = null;
            rhsA = null;
            rhsB = null;
        }
        
        public override int GetHashCode() {
            return lhs.GetHashCode() ^ op.GetHashCode() ^ rhsA.GetHashCode() ^ rhsB.GetHashCode() ^ location.GetHashCode();
        }
        
        public override bool Equals(object obj) {
            if (!(obj is Line)) return false;
            Line b = (Line)obj;
            return op == b.op && lhs == b.lhs && rhsA == b.rhsA && rhsB == b.rhsB && location == b.location;
        }
        
        public override string ToString() {
            string text;
            switch (op) {
            case Op.AssignA:
                text = string.Format("{0} := {1}", lhs, rhsA);
                break;
            case Op.AssignImplicit:
                text = string.Format("_ := {0}", rhsA);
                break;
            case Op.APlusB:
                text = string.Format("{0} := {1} + {2}", lhs, rhsA, rhsB);
                break;
            case Op.AMinusB:
                text = string.Format("{0} := {1} - {2}", lhs, rhsA, rhsB);
                break;
            case Op.ATimesB:
                text = string.Format("{0} := {1} * {2}", lhs, rhsA, rhsB);
                break;
            case Op.ADividedByB:
                text = string.Format("{0} := {1} / {2}", lhs, rhsA, rhsB);
                break;
            case Op.AModB:
                text = string.Format("{0} := {1} % {2}", lhs, rhsA, rhsB);
                break;
            case Op.APowB:
                text = string.Format("{0} := {1} ^ {2}", lhs, rhsA, rhsB);
                break;
            case Op.AEqualB:
                text = string.Format("{0} := {1} == {2}", lhs, rhsA, rhsB);
                break;
            case Op.ANotEqualB:
                text = string.Format("{0} := {1} != {2}", lhs, rhsA, rhsB);
                break;
            case Op.AGreaterThanB:
                text = string.Format("{0} := {1} > {2}", lhs, rhsA, rhsB);
                break;
            case Op.AGreatOrEqualB:
                text = string.Format("{0} := {1} >= {2}", lhs, rhsA, rhsB);
                break;
            case Op.ALessThanB:
                text = string.Format("{0} := {1} < {2}", lhs, rhsA, rhsB);
                break;
            case Op.ALessOrEqualB:
                text = string.Format("{0} := {1} <= {2}", lhs, rhsA, rhsB);
                break;
            case Op.AAndB:
                text = string.Format("{0} := {1} and {2}", lhs, rhsA, rhsB);
                break;
            case Op.AOrB:
                text = string.Format("{0} := {1} or {2}", lhs, rhsA, rhsB);
                break;
            case Op.AisaB:
                text = string.Format("{0} := {1} isa {2}", lhs, rhsA, rhsB);
                break;
            case Op.BindAssignA:
                text = string.Format("{0} := {1} {0}.outerVars=", rhsA, rhsB);
                break;
            case Op.CopyA:
                text = string.Format("{0} := copy of {1}", lhs, rhsA);
                break;
            case Op.NotA:
                text = string.Format("{0} := not {1}", lhs, rhsA);
                break;
            case Op.GotoA:
                text = string.Format("goto {0}", rhsA);
                break;
            case Op.GotoAifB:
                text = string.Format("goto {0} if {1}", rhsA, rhsB);
                break;
            case Op.GotoAifTrulyB:
                text = string.Format("goto {0} if truly {1}", rhsA, rhsB);
                break;
            case Op.GotoAifNotB:
                text = string.Format("goto {0} if not {1}", rhsA, rhsB);
                break;
            case Op.PushParam:
                text = string.Format("push param {0}", rhsA);
                break;
            case Op.CallFunctionA:
                text = string.Format("{0} := call {1} with {2} args", lhs, rhsA, rhsB);
                break;
            case Op.CallIntrinsicA:
                text = string.Format("intrinsic {0}", Intrinsic.GetByID(rhsA.IntValue()));
                break;
            case Op.ReturnA:
                text = string.Format("{0} := {1}; return", lhs, rhsA);
                break;
            case Op.ElemBofA:
                text = string.Format("{0} = {1}[{2}]", lhs, rhsA, rhsB);
                break;
            case Op.ElemBofIterA:
                text = string.Format("{0} = {1} iter {2}", lhs, rhsA, rhsB);
                break;
            case Op.LengthOfA:
                text = string.Format("{0} = len({1})", lhs, rhsA);
                break;
            default:
                throw new RuntimeException("unknown opcode: " + op);
                
            }
//				if (comment != null) text = text + "\t// " + comment;
            return text;
        }

        /// <summary>
        /// Evaluate this line and return the value that would be stored
        /// into the lhs.
        /// </summary>
        public Value Evaluate(Context context) {

            int rhsATypeInt = rhsA == null ? -1 : rhsA.GetBaseMiniscriptType();

            if (op == Op.AssignA || op == Op.ReturnA || op == Op.AssignImplicit) {
                // Assignment is a bit of a special case.  It's EXTREMELY common
                // in TAC, so needs to be efficient, but we have to watch out for
                // the case of a RHS that is a list or map.  This means it was a
                // literal in the source, and may contain references that need to
                // be evaluated now.
                //if (rhsA is ValList || rhsA is ValMap) {
                if (rhsATypeInt == MiniscriptTypeInts.ValListTypeInt || rhsATypeInt == MiniscriptTypeInts.ValMapTypeInt) {
                    return rhsA.FullEval(context);
                } else if (rhsA == null) {
                    return null;
                } else {
                    return rhsA.Val(context, true);
                }
            }
            if (op == Op.CopyA) {
                // This opcode is used for assigning a literal.  We actually have
                // to copy the literal, in the case of a mutable object like a
                // list or map, to ensure that if the same code executes again,
                // we get a new, unique object.
                //if (rhsA is ValList) {
                if (rhsATypeInt == MiniscriptTypeInts.ValListTypeInt) {
                    return ((ValList)rhsA).EvalCopy(context);
                //} else if (rhsA is ValMap) {
                } else if (rhsATypeInt == MiniscriptTypeInts.ValMapTypeInt) {
                    return ((ValMap)rhsA).EvalCopy(context);
                } else if (rhsA == null) {
                    return null;
                } else {
                    return rhsA.Val(context, true);
                }
            }

            Value opA = rhsA!=null ? rhsA.Val(context, false) : null;
            Value opB = rhsB!=null ? rhsB.Val(context, false) : null;

            int opATypeInt = opA == null ? -1 : opA.GetBaseMiniscriptType();
            int opBTypeInt = opB == null ? -1 : opB.GetBaseMiniscriptType();

            if (op == Op.AisaB) {
                if (opA == null) return ValNumber.Truth(opB == null);
                return ValNumber.Truth(opA.IsA(opB, context.vm));
            }

            //if (op == Op.ElemBofA && opB is ValString) {
            if (op == Op.ElemBofA && opBTypeInt == MiniscriptTypeInts.ValStringTypeInt) {
                // You can now look for a string in almost anything...
                // and we have a convenient (and relatively fast) method for it:
                ValMap ignored;
                return ValSeqElem.Resolve(opA, ((ValString)opB).value, context, out ignored);
            }

            // check for special cases of comparison to null (works with any type)
            if (op == Op.AEqualB && (opA == null || opB == null)) {
                return ValNumber.Truth(opA == opB);
            }
            if (op == Op.ANotEqualB && (opA == null || opB == null)) {
                return ValNumber.Truth(opA != opB);
            }
            
            // check for implicit coersion of other types to string; this happens
            // when either side is a string and the operator is addition.
            //if ((opA is ValString || opB is ValString) && op == Op.APlusB) {
            if ((opATypeInt == MiniscriptTypeInts.ValStringTypeInt || opBTypeInt == MiniscriptTypeInts.ValStringTypeInt) && op == Op.APlusB) {
                string sA = opA != null ? opA.ToString(context.vm) : string.Empty;
                string sB = opB != null ? opB.ToString(context.vm) : string.Empty;
                if (sA.Length + sB.Length > ValString.maxSize)
                    throw new LimitExceededException("string too large");
                return ValString.Create(sA + sB);
            }

            //if (opA is ValNumber) {
            if (opATypeInt == MiniscriptTypeInts.ValNumberTypeInt) {
                double numA = ((ValNumber)opA).value;
                switch (op) {
                case Op.GotoA:
                    context.lineNum = (int)numA;
                    return null;
                case Op.GotoAifB:
                    if (opB != null && opB.BoolValue()) context.lineNum = (int)numA;
                    return null;
                case Op.GotoAifTrulyB:
                    {
                        // Unlike GotoAifB, which branches if B has any nonzero
                        // value (including 0.5 or 0.001), this branches only if
                        // B is TRULY true, i.e., its integer value is nonzero.
                        // (Used for short-circuit evaluation of "or".)
                        int i = 0;
                        if (opB != null) i = opB.IntValue();
                        if (i != 0) context.lineNum = (int)numA;
                        return null;
                    }
                case Op.GotoAifNotB:
                    if (opB == null || !opB.BoolValue()) context.lineNum = (int)numA;
                    return null;
                case Op.CallIntrinsicA:
                    // NOTE: intrinsics do not go through NextFunctionContext.  Instead
                    // they execute directly in the current context.  (But usually, the
                    // current context is a wrapper function that was invoked via
                    // Op.CallFunction, so it got a parameter context at that time.)
                    Intrinsic.Result result = Intrinsic.Execute((int)numA, context, context.partialResult);
                    if (result.done) {
                        context.partialResult = default(Intrinsic.Result);
                        return result.result;
                    }
                    // OK, this intrinsic function is not yet done with its work.
                    // We need to stay on this same line and call it again with 
                    // the partial result, until it reports that its job is complete.
                    context.partialResult = result;
                    context.lineNum--;
                    return null;
                case Op.NotA:
                    return ValNumber.Create(1.0 - AbsClamp01(numA));
                }
                //if (opB is ValNumber || opB == null) {
                if (opBTypeInt == MiniscriptTypeInts.ValNumberTypeInt || opB == null) {
                    double numB = opB != null ? ((ValNumber)opB).value : 0;
                    switch (op) {
                    case Op.APlusB:
                        return ValNumber.Create(numA + numB);
                    case Op.AMinusB:
                        return ValNumber.Create(numA - numB);
                    case Op.ATimesB:
                        return ValNumber.Create(numA * numB);
                    case Op.ADividedByB:
                        return ValNumber.Create(numA / numB);
                    case Op.AModB:
                        return ValNumber.Create(numA % numB);
                    case Op.APowB:
                        return ValNumber.Create(Math.Pow(numA, numB));
                    case Op.AEqualB:
                        return ValNumber.Truth(numA == numB);
                    case Op.ANotEqualB:
                        return ValNumber.Truth(numA != numB);
                    case Op.AGreaterThanB:
                        return ValNumber.Truth(numA > numB);
                    case Op.AGreatOrEqualB:
                        return ValNumber.Truth(numA >= numB);
                    case Op.ALessThanB:
                        return ValNumber.Truth(numA < numB);
                    case Op.ALessOrEqualB:
                        return ValNumber.Truth(numA <= numB);
                    case Op.AAndB:
                        //if (!(opB is ValNumber))
                            //numB = opB != null && opB.BoolValue() ? 1 : 0;
                        return ValNumber.Create(Clamp01(numA * numB));
                    case Op.AOrB:
                        //if (!(opB is ValNumber))
                            //numB = opB != null && opB.BoolValue() ? 1 : 0;
                        return ValNumber.Create(Clamp01(numA + numB - numA * numB));
                    default:
                        break;
                    }
                }else if(opBTypeInt == MiniscriptTypeInts.ValCustomTypeInt)
                {
                    // Most types should commute with number
                    // But in case not we have a flag to say if the other is on
                    // the lhs or rhs
                    ValCustom customB = opB as ValCustom;
                    switch (op)
                    {
                        case Op.APlusB:
                            return customB.APlusB(opA, opATypeInt, context, false);
                        case Op.AMinusB:
                            return customB.AMinusB(opA, opATypeInt, context, false);
                        case Op.ATimesB:
                            return customB.ATimesB(opA, opATypeInt, context, false);
                        case Op.ADividedByB:
                            return customB.ADividedByB(opA, opATypeInt, context, false);
                    }
                }
                // Handle equality testing between a number (opA) and a non-number (opB).
                // These are always considered unequal.
                if (op == Op.AEqualB) return ValNumber.zero;
                if (op == Op.ANotEqualB) return ValNumber.one;
            //} else if (opA is ValString) {
            } else if (opATypeInt == MiniscriptTypeInts.ValStringTypeInt) {
                string strA = ((ValString)opA).value;
                if (op == Op.ATimesB || op == Op.ADividedByB) {
                    double factor = 0;
                    if (op == Op.ATimesB) {
                        Check.Type(opB, typeof(ValNumber), "string replication");
                        factor = ((ValNumber)opB).value;
                    } else {
                        Check.Type(opB, typeof(ValNumber), "string division");
                        factor = 1.0 / ((ValNumber)opB).value;								
                    }
                    int repeats = (int)factor;
                    if (repeats < 0)
                        return ValString.empty;
                    if (repeats * strA.Length > ValString.maxSize)
                        throw new LimitExceededException("string too large");
                    if (_workingStringBuilder == null)
                        _workingStringBuilder = new StringBuilder();
                    else
                        _workingStringBuilder.Clear();
                    for (int i = 0; i < repeats; i++)
                        _workingStringBuilder.Append(strA);
                    int extraChars = (int)(strA.Length * (factor - repeats));
                    if (extraChars > 0)
                        _workingStringBuilder.Append(strA.Substring(0, extraChars));
                    return ValString.Create(_workingStringBuilder.ToString());						
                }
                if (op == Op.ElemBofA || op == Op.ElemBofIterA) {
                    int idx = opB.IntValue();
                    Check.Range(idx, -strA.Length, strA.Length - 1, "string index");
                    if (idx < 0)
                        idx += strA.Length;
                    return ValString.Create(strA.Substring(idx, 1));
                }
                //if (opB == null || opB is ValString) {
                if (opB == null || opBTypeInt == MiniscriptTypeInts.ValStringTypeInt) {
                    string sB = (opB == null ? null : opB.ToString(context.vm));
                    switch (op) {
                        case Op.AMinusB: {
                                if (opB == null)
                                {
                                    opA.Ref();
                                    return opA;
                                }
                                if (strA.EndsWith(sB))
                                    strA = strA.Substring(0, strA.Length - sB.Length);
                                return ValString.Create(strA);
                            }
                        case Op.NotA:
                            return ValNumber.Truth(string.IsNullOrEmpty(strA));
                        case Op.AEqualB:
                            return ValNumber.Truth(string.Equals(strA, sB));
                        case Op.ANotEqualB:
                            return ValNumber.Truth(!string.Equals(strA, sB));
                        case Op.AGreaterThanB:
                            return ValNumber.Truth(string.Compare(strA, sB, StringComparison.Ordinal) > 0);
                        case Op.AGreatOrEqualB:
                            return ValNumber.Truth(string.Compare(strA, sB, StringComparison.Ordinal) >= 0);
                        case Op.ALessThanB:
                            int foo = string.Compare(strA, sB, StringComparison.Ordinal);
                            return ValNumber.Truth(foo < 0);
                        case Op.ALessOrEqualB:
                            return ValNumber.Truth(string.Compare(strA, sB, StringComparison.Ordinal) <= 0);
                        case Op.LengthOfA:
                            return ValNumber.Create(strA.Length);
                        default:
                            break;
                    }
                } else {
                    // RHS is neither null nor a string.
                    // We no longer automatically coerce in all these cases; about
                    // all we can do is equal or unequal testing.
                    // (Note that addition was handled way above here.)
                    if (op == Op.AEqualB) return ValNumber.zero;
                    if (op == Op.ANotEqualB) return ValNumber.one;						
                }
            //} else if (opA is ValList) {
            } else if (opATypeInt == MiniscriptTypeInts.ValListTypeInt) {
                ValList listA = ((ValList)opA);
                if (op == Op.ElemBofA || op == Op.ElemBofIterA) {
                    // list indexing
                    int idx = opB.IntValue();
                    Check.Range(idx, -listA.Count, listA.Count - 1, "list index");
                    if (idx < 0) idx += listA.Count;
                    Value val = listA[idx];
                    if(val != null)
                        val.Ref();
                    return val;
                } else if (op == Op.LengthOfA) {
                    return ValNumber.Create(listA.Count);
                } else if (op == Op.AEqualB) {
                    return ValNumber.Truth(((ValList)opA).Equality(opB));
                } else if (op == Op.ANotEqualB) {
                    return ValNumber.Truth(1.0 - ((ValList)opA).Equality(opB));
                } else if (op == Op.APlusB) {
                    // list concatenation
                    Check.Type(opB, typeof(ValList), "list concatenation");
                    ValList listB = ((ValList)opB);
                    if (listA.Count + listB.Count > ValList.maxSize)
                        throw new LimitExceededException("list too large");
                    ValList result = ValList.Create(listA.Count + listB.Count);
                    for(int i = 0; i < listA.Count; i++)
                        result.Add(context.ValueInContext(listA[i]));
                    for(int i = 0; i < listB.Count; i++)
                        result.Add(context.ValueInContext(listB[i]));
                    return result;
                } else if (op == Op.ATimesB || op == Op.ADividedByB) {
                    // list replication (or division)
                    double factor = 0;
                    if (op == Op.ATimesB) {
                        Check.Type(opB, typeof(ValNumber), "list replication");
                        factor = ((ValNumber)opB).value;
                    } else {
                        Check.Type(opB, typeof(ValNumber), "list division");
                        factor = 1.0 / ((ValNumber)opB).value;								
                    }
                    if (factor <= 0) return ValList.Create();
                    int finalCount = (int)(listA.Count * factor);
                    if (finalCount > ValList.maxSize) throw new LimitExceededException("list too large");
                    ValList result = ValList.Create(finalCount);
                    for (int i = 0; i < finalCount; i++) {
                        result.Add(listA[i % listA.Count]);
                    }
                    return result;
                } else if (op == Op.NotA) {
                    return ValNumber.Truth(!opA.BoolValue());
                }
            //} else if (opA is ValMap) {
            } else if (opATypeInt == MiniscriptTypeInts.ValMapTypeInt) {
                if (op == Op.ElemBofA) {
                    // map lookup
                    // (note, cases where opB is a string are handled above, along with
                    // all the other types; so we'll only get here for non-string cases)
                    ValSeqElem se = ValSeqElem.Create(opA, opB);
                    Value ret = se.Val(context, true);
                    //if (se != null)
                        //se.Unref();
                    return ret;
                    // (This ensures we walk the "__isa" chain in the standard way.)
                } else if (op == Op.ElemBofIterA) {
                    // With a map, ElemBofIterA is different from ElemBofA.  This one
                    // returns a mini-map containing a key/value pair.
                    return ((ValMap)opA).GetKeyValuePair(opB.IntValue());
                } else if (op == Op.LengthOfA) {
                    return ValNumber.Create(((ValMap)opA).Count);
                } else if (op == Op.AEqualB) {
                    return ValNumber.Truth(((ValMap)opA).Equality(opB));
                } else if (op == Op.ANotEqualB) {
                    return ValNumber.Truth(1.0 - ((ValMap)opA).Equality(opB));
                } else if (op == Op.APlusB) {
                    // map combination
                    Check.Type(opB, typeof(ValMap), "map combination");
                    ValMap result = ValMap.Create();
                    ValMap mapA = opA as ValMap;
                    var aKeys = mapA.Keys;
                    var aVals = mapA.Values;
                    for(int i = 0; i < aKeys.Count;i++)
                        result[aKeys[i]] = context.ValueInContext(aVals[i]);
                    ValMap mapB = opB as ValMap;
                    var bKeys = mapB.Keys;
                    var bVals = mapB.Values;
                    for(int i = 0; i < bKeys.Count;i++)
                        result[bKeys[i]] = context.ValueInContext(bVals[i]);
                    return result;
                } else if (op == Op.NotA) {
                    return ValNumber.Truth(!opA.BoolValue());
                }
            //} else if (opA is ValFunction && opB is ValFunction) {
            } else if (opATypeInt == MiniscriptTypeInts.ValFunctionTypeInt && opBTypeInt == MiniscriptTypeInts.ValFunctionTypeInt) {
                Function fA = ((ValFunction)opA).function;
                Function fB = ((ValFunction)opB).function;
                switch (op) {
                case Op.AEqualB:
                    return ValNumber.Truth(fA == fB);
                case Op.ANotEqualB:
                    return ValNumber.Truth(fA != fB);
                }
            } else if(opATypeInt == MiniscriptTypeInts.ValCustomTypeInt)
            {
                ValCustom customA = opA as ValCustom;
                switch (op)
                {
                    case Op.APlusB:
                        return customA.APlusB(opB, opBTypeInt, context, true);
                    case Op.AMinusB:
                        return customA.AMinusB(opB, opBTypeInt, context, true);
                    case Op.ATimesB:
                        return customA.ATimesB(opB, opBTypeInt, context, true);
                    case Op.ADividedByB:
                        return customA.ADividedByB(opB, opBTypeInt, context, true);
                }

            } else {
                // opA is something else... perhaps null
                switch (op) {
                    case Op.BindAssignA:
                        {
                            if (context.variables == null) context.variables = ValMap.Create();
                            ValFunction valFunc = (ValFunction)opA;
                            return valFunc.BindAndCopy(context.variables);
                            //valFunc.outerVars = context.variables;
                            //return null;
                        }
                    case Op.NotA:
                    return opA != null && opA.BoolValue() ? ValNumber.zero : ValNumber.one;
                }
            }
            

            if (op == Op.AAndB || op == Op.AOrB) {
                // We already handled the case where opA was a number above;
                // this code handles the case where opA is something else.
                double numA = opA != null && opA.BoolValue() ? 1 : 0;
                double numB;
                //if (opB is ValNumber) fB = ((ValNumber)opB).value;
                if (opBTypeInt == MiniscriptTypeInts.ValNumberTypeInt)
                    numB = ((ValNumber)opB).value;
                else
                    numB = opB != null && opB.BoolValue() ? 1 : 0;
                double result;
                if (op == Op.AAndB) {
                    result = numA * numB;
                } else {
                    result = 1.0 - (1.0 - AbsClamp01(numA)) * (1.0 - AbsClamp01(numB));
                }
                return ValNumber.Create(result);
            }
            return null;
        }

        static double Clamp01(double d) {
            if (d < 0) return 0;
            if (d > 1) return 1;
            return d;
        }
        static double AbsClamp01(double d) {
            if (d < 0) d = -d;
            if (d > 1) return 1;
            return d;
        }

    }
}
