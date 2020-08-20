/*	MiniscriptTAC.cs

This file defines the three-address code (TAC) which represents compiled
MiniScript code.  TAC is sort of a pseudo-assembly language, composed of
simple instructions containing an opcode and up to three variable/value 
references.

This is all internal MiniScript virtual machine code.  You don't need to
deal with it directly (see MiniscriptInterpreter instead).

*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Miniscript {

	public static class TAC {

		public static void Dump(List<Line> lines) {
			int lineNum = 0;
			foreach (Line line in lines) {
				Console.WriteLine((lineNum++).ToString() + ". " + line);
			}
		}
		public static ValTemp LTemp(int tempNum) {
			return new ValTemp(tempNum);
		}
		public static ValVar LVar(string identifier) {
			return new ValVar(identifier);
		}
		public static ValTemp RTemp(int tempNum) {
			return new ValTemp(tempNum);
		}
		public static ValNumber Num(double value) {
			return ValNumber.Create(value);
		}
		public static ValString Str(string value) {
			return ValString.Create(value);
		}
		public static ValNumber IntrinsicByName(string name) {
			return ValNumber.Create(Intrinsic.GetByName(name).id);
		}
	}
}

