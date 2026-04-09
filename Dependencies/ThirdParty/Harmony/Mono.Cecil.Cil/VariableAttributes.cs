using System;

namespace Mono.Cecil.Cil;

[Flags]
internal enum VariableAttributes : ushort
{
	None = 0,
	DebuggerHidden = 1
}
