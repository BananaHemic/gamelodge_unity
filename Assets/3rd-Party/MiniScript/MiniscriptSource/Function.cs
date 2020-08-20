using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Miniscript
{
	/// <summary>
	/// Function: our internal representation of a MiniScript function.  This includes
	/// its parameters and its code.  (It does not include a name -- functions don't 
	/// actually HAVE names; instead there are named variables whose value may happen 
	/// to be a function.)
	/// </summary>
	public class Function {
        [ThreadStatic]
        private static StringBuilder _workingStringBuilder;
        public bool _usePool;
		/// <summary>
		/// Param: helper class representing a function parameter.
		/// </summary>
		public class Param {
			public string name;
			public Value defaultValue;

			public Param(string name, Value defaultValue) {
				this.name = name;
				this.defaultValue = defaultValue;
			}
		}
		
		// Function parameters
		public List<Param> parameters;
		
		// Function code (compiled down to TAC form)
		public List<Line> code;

		public Function(List<Line> code, bool usePool=true) {
			this.code = code;
            _usePool = usePool;
			parameters = new List<Param>();
		}

        public void Dispose()
        {
            if (!_usePool)
                return;
            foreach(var p in parameters)
            {
                if (p.defaultValue != null)
                    p.defaultValue.Unref();
            }
            parameters.Clear();
            foreach(var l in code)
            {
                if (l.rhsA != null)
                    l.rhsA.Unref();
                if (l.rhsB != null)
                    l.rhsB.Unref();
            }
            code.Clear();
        }

		public string ToString(Machine vm) {
            if (_workingStringBuilder == null)
                _workingStringBuilder = new StringBuilder();
            else
                _workingStringBuilder.Clear();
			_workingStringBuilder.Append("FUNCTION(");			
			for (var i=0; i < parameters.Count(); i++) {
				if (i > 0) _workingStringBuilder.Append(", ");
				_workingStringBuilder.Append(parameters[i].name);
				if (parameters[i].defaultValue != null) _workingStringBuilder.Append("=" + parameters[i].defaultValue.CodeForm(vm));
			}
			_workingStringBuilder.Append(")");
			return _workingStringBuilder.ToString();
		}
	}
}
