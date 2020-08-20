using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Miniscript
{
    /// <summary>
    /// TAC.Context keeps track of the runtime environment, including local 
    /// variables.  Context objects form a linked list via a "parent" reference,
    /// with a new context formed on each function call (this is known as the
    /// call stack).
    /// </summary>
    public class Context : IDisposable {

        public List<Line> code;			// TAC lines we're executing
        public int lineNum;				// next line to be executed
        public ValMap variables;		// local variables for this call frame
        public ValMap outerVars;		// variables of the context where this function was defined
        public Stack<Value> args;		// pushed arguments for upcoming calls
        public Context parent;			// parent (calling) context
        public Value resultStorage;		// where to store the return value (in the calling context)
        public Machine vm;				// virtual machine
        public Intrinsic.Result partialResult;	// work-in-progress of our current intrinsic
        public int implicitResultCounter;	// how many times we have stored an implicit result
        readonly List<TempEntry> temps = new List<TempEntry>();			// values of temporaries; temps[0] is always return value

        [ThreadStatic]
        private static Stack<Context> _pool;

        private struct TempEntry
        {
            public Value value;
            public bool Unref;
        }

        public bool done {
            get { return lineNum >= code.Count; }
        }

        public Context root {
            get {
                Context c = this;
                while (c.parent != null) c = c.parent;
                return c;
            }
        }

        public Interpreter interpreter {
            get {
                if (vm == null || vm.interpreter == null) return null;
                return vm.interpreter.Target as Interpreter;
            }
        }

        public static Context Create(List<Line> code)
        {
            if (_pool == null)
                _pool = new Stack<Context>();
            else if(_pool.Count > 0)
            {
                Context c = _pool.Pop();
                c.code = code;
                //Console.WriteLine("Using pooled context. Num left " + _pool.Count);
                return c;
            }
            //Console.WriteLine("Making ctx");
            return new Context(code);
        }
        private Context(List<Line> code) {
            this.code = code;
        }
        
        /// <summary>
        /// Reset this context to the first line of code, clearing out any 
        /// temporary variables, and optionally clearing out all variables.
        /// </summary>
        /// <param name="clearVariables">if true, clear our local variables</param>
        public void Reset(bool clearVariables=true) {
            lineNum = 0;
            // #0 is the return variable, which we don't want to unref
            // unless this is the root context, in which case we unref it all
            int start = root == this ? 0 : 1;
            for(int i = start; i < temps.Count; i++)
            {
                TempEntry entry = temps[i];
                if (!entry.Unref)
                    continue;
                entry.value?.Unref();
            }
            temps.Clear();
            if (clearVariables)
            {
                if(variables != null)
                    variables.Unref();
                variables = null;
            }
        }

        public void Dispose()
        {
            Reset(true);
            code = null;
            lineNum = 0;
            variables = null;
            outerVars = null;
            if(args != null)
                args.Clear();
            parent = null;
            resultStorage = null;
            vm = null;
            partialResult = default(Intrinsic.Result);
            implicitResultCounter = 0;
            if (_pool == null)
                _pool = new Stack<Context>();
            _pool.Push(this);
            //Console.WriteLine("ctx push");
        }

        public void JumpToEnd() {
            lineNum = code.Count;
        }

        public void SetTemp(int tempNum, Value value, bool unrefWhenDone) {
            if(temps.Count <= tempNum)
            {
                while (temps.Count <= tempNum)
                    temps.Add(default(TempEntry));
            }
            else
            {
                TempEntry existing = temps[tempNum];
                if (existing.Unref && existing.value != null)
                    existing.value.Unref();
            }
            temps[tempNum] = new TempEntry
            {
                value = value,
                Unref = unrefWhenDone
            };
        }

        public Value GetTemp(int tempNum) {
            return temps == null ? null : temps[tempNum].value;
        }

        public Value GetTemp(int tempNum, Value defaultValue) {
            if (temps != null && tempNum < temps.Count) return temps[tempNum].value;
            return defaultValue;
        }

        public void SetVar(Value identifier, Value value) {
            if (variables == null) variables = ValMap.Create();
            if (variables.assignOverride == null || !variables.assignOverride(identifier, value)) {
                variables.SetElem(identifier, value, false);
            }
        }
        public void SetVar(string identifier, Value value)
        {
            if (identifier == "globals" || identifier == "locals") {
                throw new RuntimeException("can't assign to " + identifier);
            }

            if (variables == null) variables = ValMap.Create();
            var identifierStr = ValString.Create(identifier);
            if (variables.assignOverride == null || !variables.assignOverride(identifierStr, value)) {
                //variables[identifier] = value;
                variables.SetElem(identifier, value, false);
                //variables.SetElem(identifier, value, true);
            }
            identifierStr.Unref();
        }
        
        /// <summary>
        /// Get the value of a local variable ONLY -- does not check any other
        /// scopes, nor check for special built-in identifiers like "globals".
        /// Used mainly by host apps to easily look up an argument to an
        /// intrinsic function call by the parameter name.
        /// </summary>
        public Value GetLocal(string identifier, Value defaultValue=null) {
            Value result;
            if (variables != null && variables.TryGetValue(identifier, out result)) {
                return result;
            }
            return defaultValue;
        }
        public Value GetLocal(ValString identifier, Value defaultValue=null) {
            Value result;
            if (variables != null && variables.TryGetValue(identifier, out result)) {
                return result;
            }
            return defaultValue;
        }
        
        public int GetLocalInt(string identifier, int defaultValue = 0) {
            Value result;
            if (variables != null && variables.TryGetValue(identifier, out result)) {
                return result.IntValue();
            }
            return defaultValue;
        }

        public bool GetLocalBool(string identifier, bool defaultValue = false) {
            Value result;
            if (variables != null && variables.TryGetValue(identifier, out result)) {
                return result.BoolValue();
            }
            return defaultValue;
        }

        public float GetLocalFloat(string identifier, float defaultValue = 0) {
            Value result;
            if (variables != null && variables.TryGetValue(identifier, out result)) {
                return result.FloatValue();
            }
            return defaultValue;
        }

        public string GetLocalString(string identifier, string defaultValue = null) {
            Value result;
            if (variables != null && variables.TryGetValue(identifier, out result)) {
                if (result == null) return null;	// variable found, but its value was null!
                return result.ToString();
            }
            return defaultValue;
        }

        
        
        /// <summary>
        /// Get the value of a variable available in this context (including
        /// locals, globals, and intrinsics).  Raise an exception if no such
        /// identifier can be found.
        /// </summary>
        /// <param name="identifier">name of identifier to look up</param>
        /// <returns>value of that identifier</returns>
        public Value GetVar(string identifier) {
            // check for special built-in identifiers 'locals' and 'globals'
            if (identifier == "locals") {
                if (variables == null) variables = ValMap.Create();
                return variables;
            }
            if (identifier == "globals") {
                if (root.variables == null) root.variables = ValMap.Create();
                return root.variables;
            }
            if (identifier == "outer") {
                // return module variables, if we have them; else globals
                if (outerVars != null) return outerVars;
                if (root.variables == null) root.variables = ValMap.Create();
                return root.variables;
            }

            // check for a local variable
            Value result;
            if (variables != null && variables.TryGetValue(identifier, out result)) {
                return result;
            }

            // check for a module variable
            if (outerVars != null && outerVars.TryGetValue(identifier, out result)) {
                return result;
            }

            // OK, we don't have a local or module variable with that name.
            // Check the global scope (if that's not us already).
            if (parent != null) {
                Context globals = root;
                if (globals.variables != null && globals.variables.ContainsKey(identifier)) {
                    return globals.variables[identifier];
                }
            }

            // Finally, check intrinsics.
            Intrinsic intrinsic = Intrinsic.GetByName(identifier);
            if (intrinsic != null) return intrinsic.GetFunc();

            // No luck there either?  Undefined identifier.
            throw new UndefinedIdentifierException(identifier);
        }
        public Value GetVar(Value identifier) {
            // check for a local variable
            Value result;
            if (variables != null && variables.TryGetValue(identifier, out result)) {
                return result;
            }

            // check for a module variable
            if (outerVars != null && outerVars.TryGetValue(identifier, out result)) {
                return result;
            }

            // OK, we don't have a local or module variable with that name.
            // Check the global scope (if that's not us already).
            if (parent != null) {
                Context globals = root;
                if (globals.variables != null && globals.variables.ContainsKey(identifier)) {
                    return globals.variables[identifier];
                }
            }

            // Finally, check intrinsics.
            ValString identStrVal = identifier as ValString;
            string identStr = identStrVal == null ? string.Empty : identStrVal.value;
            if(!string.IsNullOrEmpty(identStr))
            {
                Intrinsic intrinsic = Intrinsic.GetByName(identStr);
                if (intrinsic != null)
                    return intrinsic.GetFunc();
            }

            // No luck there either?  Undefined identifier.
            throw new UndefinedIdentifierException(identStr);
        }

        public void StoreValue(Value lhs, Value value, bool unrefWhenDone=false) {
            if (lhs is ValTemp) {
                SetTemp(((ValTemp)lhs).tempNum, value, unrefWhenDone);
            } else if (lhs is ValVar) {
                //SetVar(lhs, value);
                SetVar(((ValVar)lhs).identifier, value);
            } else if (lhs is ValSeqElem) {
                ValSeqElem seqElem = (ValSeqElem)lhs;
                Value seq = seqElem.sequence.Val(this, false);
                if (seq == null) throw new RuntimeException("can't set indexed element of null");
                if (!seq.CanSetElem()) {
                    throw new RuntimeException("can't set an indexed element in this type");
                }
                Value index = seqElem.index;
                if (index is ValVar || index is ValSeqElem || 
                    index is ValTemp) index = index.Val(this, false);
                seq.SetElem(index, value);

                // Now seq owns a ref, so we can unref
                if (unrefWhenDone && value != null)
                    value.Unref();
            } else {
                if (lhs != null) throw new RuntimeException("not an lvalue");
            }
        }
        
        public Value ValueInContext(Value value) {
            if (value == null) return null;
            return value.Val(this, false);
        }

        /// <summary>
        /// Store a parameter argument in preparation for an upcoming call
        /// (which should be executed in the context returned by NextCallContext).
        /// </summary>
        /// <param name="arg">Argument.</param>
        public void PushParamArgument(Value arg) {
            if (args == null) args = new Stack<Value>();
            args.Push(arg);
        }

        /// <summary>
        /// Get a context for the next call, which includes any parameter arguments
        /// that have been set.
        /// </summary>
        /// <returns>The call context.</returns>
        /// <param name="func">Function to call.</param>
        /// <param name="argCount">How many arguments to pop off the stack.</param>
        /// <param name="gotSelf">Whether this method was called with dot syntax.</param> 
        /// <param name="resultStorage">Value to stuff the result into when done.</param>
        public Context NextCallContext(Function func, int argCount, bool gotSelf, Value resultStorage) {
            Context result = Context.Create(func.code);

            result.code = func.code;
            result.resultStorage = resultStorage;
            result.parent = this;
            result.vm = vm;

            // Stuff arguments, stored in our 'args' stack,
            // into local variables corrersponding to parameter names.
            // As a special case, skip over the first parameter if it is named 'self'
            // and we were invoked with dot syntax.
            int selfParam = (gotSelf && func.parameters.Count > 0 && func.parameters[0].name == "self"
             ? 1 : 0);
            for (int i = 0; i < argCount; i++) {
                // Careful -- when we pop them off, they're in reverse order.
                Value argument = args.Pop();
                argument?.Ref();
                int paramNum = argCount - 1 - i + selfParam;
                if (paramNum >= func.parameters.Count) {
                    throw new TooManyArgumentsException();
                }
                result.SetVar(func.parameters[paramNum].name, argument);
            }
            // And fill in the rest with default values
            for (int paramNum = argCount+selfParam; paramNum < func.parameters.Count; paramNum++) {
                Value defVal = func.parameters[paramNum].defaultValue;
                defVal?.Ref();
                result.SetVar(func.parameters[paramNum].name, defVal);
            }

            return result;
        }

        public void Dump() {
            Console.WriteLine("CODE:");
            for (int i = 0; i < code.Count; i++) {
                Console.WriteLine("{0} {1:00}: {2}", i == lineNum ? ">" : " ", i, code[i]);
            }

            Console.WriteLine("\nVARS:");
            if (variables == null) {
                Console.WriteLine(" NONE");
            } else {
                foreach (Value v in variables.Keys) {
                    string id = v.ToString(vm);
                    Console.WriteLine(string.Format("{0}: {1}", id, variables[id].ToString(vm)));
                }
            }

            Console.WriteLine("\nTEMPS:");
            if (temps == null) {
                Console.WriteLine(" NONE");
            } else {
                for (int i = 0; i < temps.Count; i++) {
                    Console.WriteLine(string.Format("_{0}: {1}", i, temps[i]));
                }
            }
        }

        public override string ToString() {
            return string.Format("Context[{0}/{1}]", lineNum, code.Count);
        }
    }
}
