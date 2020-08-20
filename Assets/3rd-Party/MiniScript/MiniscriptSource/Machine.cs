using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Miniscript
{
    /// <summary>
    /// TAC.Machine implements a complete MiniScript virtual machine.  It 
    /// keeps the context stack, keeps track of run time, and provides 
    /// methods to step, stop, or reset the program.		
    /// </summary>
    public class Machine : IDisposable {
        public WeakReference interpreter;		// interpreter hosting this machine
        public TextOutputMethod standardOutput;	// where print() results should go
        public bool storeImplicit = false;		// whether to store implicit values (e.g. for REPL)
        public bool yielding = false;			// set to true by yield intrinsic
        public ValMap functionType;
        public ValMap listType;
        public ValMap mapType;
        public ValMap numberType;
        public ValMap stringType;
        public ValMap versionMap;
        
        public Context globalContext {			// contains global variables
            get { return _globalContext; }
        }

        public bool done {
            get { return (stack.Count <= 1 && stack.Peek().done); }
        }

        public double runTime {
            get { return stopwatch == null ? 0 : stopwatch.Elapsed.TotalSeconds; }
        }

        Context _globalContext;
        Stack<Context> stack;
        System.Diagnostics.Stopwatch stopwatch;

        public Machine(Context globalContext, TextOutputMethod standardOutput) {
            _globalContext = globalContext;
            _globalContext.vm = this;
            this.standardOutput = (standardOutput == null ? Console.WriteLine : standardOutput);
            stack = new Stack<Context>();
            stack.Push(_globalContext);
        }
        
        public void Stop() {
            while (stack.Count > 1)
            {
                Context c = stack.Pop();
                c.Dispose();
            }
            stack.Peek().JumpToEnd();
        }
        
        public void Reset() {
            while (stack.Count > 1)
            {
                Context c = stack.Pop();
                c.Dispose();
            }
            stack.Peek().Reset(false);
        }

        public void Step() {
            if (stack.Count == 0) return;		// not even a global context
            if (stopwatch == null) {
                stopwatch = new System.Diagnostics.Stopwatch();
                stopwatch.Start();
            }
            Context context = stack.Peek();
            while (context.done) {
                if (stack.Count == 1) return;	// all done (can't pop the global context)
                PopContext();
                context = stack.Peek();
            }

            Line line = context.code[context.lineNum++];
            try {
                DoOneLine(line, context);
            } catch (MiniscriptException mse) {
                mse.location = line.location;
                throw mse;
            }
        }
        
        /// <summary>
        /// Directly invoke a ValFunction by manually pushing it onto the call stack.
        /// This might be useful, for example, in invoking handlers that have somehow
        /// been registered with the host app via intrinsics.
        /// </summary>
        /// <param name="func">Miniscript function to invoke</param>
        /// <param name="resultStorage">where to store result of the call, in the calling context</param>
        public void ManuallyPushCall(ValFunction func, Value resultStorage=null) {
            int argCount = 0;
            Value self = null;	// "self" is always null for a manually pushed call
            Context nextContext = stack.Peek().NextCallContext(func.function, argCount, self != null, null);
            if (self != null) nextContext.SetVar("self", self);
            nextContext.resultStorage = resultStorage;
            stack.Push(nextContext);				
        }
        
        void DoOneLine(Line line, Context context) {
//				Console.WriteLine("EXECUTING line " + (context.lineNum-1) + ": " + line);
            if (line.op == Line.Op.PushParam) {
                Value val = context.ValueInContext(line.rhsA);
                context.PushParamArgument(val);
            } else if (line.op == Line.Op.CallFunctionA) {
                // Resolve rhsA.  If it's a function, invoke it; otherwise,
                // just store it directly.
                ValMap valueFoundIn;
                Value funcVal = line.rhsA.Val(context, out valueFoundIn);	// resolves the whole dot chain, if any
                if (funcVal is ValFunction) {
                    Value self = null;
                    // bind "super" to the parent of the map the function was found in
                    Value super = valueFoundIn == null ? null : valueFoundIn.Lookup(ValString.magicIsA);
                    if (line.rhsA is ValSeqElem) {
                        // bind "self" to the object used to invoke the call, except
                        // when invoking via "super"
                        Value seq = ((ValSeqElem)(line.rhsA)).sequence;
                        if (seq is ValVar && ((ValVar)seq).identifier == "super") self = context.GetVar("self");
                        else self = context.ValueInContext(seq);
                    }
                    ValFunction func = (ValFunction)funcVal;
                    int argCount = line.rhsB.IntValue();
                    Context nextContext = context.NextCallContext(func.function, argCount, self != null, line.lhs);
                    nextContext.outerVars = func.outerVars;
                    if (valueFoundIn != null)
                    {
                        super?.Ref();
                        nextContext.SetVar("super", super);
                    }
                    if (self != null)
                    {
                        // Take a ref to self
                        self?.Ref();
                        nextContext.SetVar("self", self);   // (set only if bound above)
                    }
                    stack.Push(nextContext);
                } else {
                    // The line.rhsA.Val doesn't call PoolableValue Val() when rhs is a ValVar, so we
                    // need to make sure to ref the value here, if the result of this operation is
                    // going into a ValVar
                    if(line.lhs is ValVar && line.rhsA is ValVar)
                        funcVal?.Ref();
                    context.StoreValue(line.lhs, funcVal);
                }
            } else if (line.op == Line.Op.ReturnA) {
                Value val = line.Evaluate(context);
                context.StoreValue(line.lhs, val, false);
                PopContext();
            } else if (line.op == Line.Op.AssignImplicit) {
                Value val = line.Evaluate(context);
                if (storeImplicit) {
                    context.StoreValue(ValVar.implicitResult, val);
                    context.implicitResultCounter++;
                }
                else
                {
                    // Normally the ctx unrefs when it's done, so if
                    // not then we need to unref here
                    if(val != null)
                        val.Unref();
                }
            } else {
                Value val = line.Evaluate(context);
                context.StoreValue(line.lhs, val, true);
            }
        }

        void PopContext() {
            // Our top context is done; pop it off, and copy the return value in temp 0.
            if (stack.Count == 1) return;	// down to just the global stack (which we keep)
            Context oldContext = stack.Pop();
            Value result = oldContext.GetTemp(0, null);
            Value storage = oldContext.resultStorage;
            Context context = stack.Peek();
            context.StoreValue(storage, result, true);
            oldContext.Dispose();
        }

        public Context GetTopContext() {
            return stack.Peek();
        }

        public void DumpTopContext() {
            stack.Peek().Dump();
        }
        
        public string FindShortName(Value val) {
            if (globalContext == null || globalContext.variables == null) return null;
            var globalVars = globalContext.variables.Values;
            var globalKeys = globalContext.variables.Keys;
            for(int i = 0; i < globalVars.Count; i++)
                if (globalVars[i] == val && globalKeys[i] != val) return globalKeys[i].ToString(this);

            string result = null;
            Intrinsic.shortNames.TryGetValue(val, out result);
            return result;
        }

        public void Dispose()
        {
            if(stack != null)
            {
                while (stack.Count > 0)
                {
                    Context c = stack.Pop();
                    c.Dispose();
                }
            }
            stack = null;
        }
    }

}
