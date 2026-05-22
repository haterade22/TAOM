using System;
using MonoMod.Core.Interop;
using MonoMod.Utils;

namespace MonoMod.Core.Platforms.Runtimes;

internal class Core31Runtime : Core30Runtime
{
	protected override CoreCLR.InvokeCompileMethodPtr InvokeCompileMethodPtr => CoreCLR.V21.InvokeCompileMethodPtr;

	public Core31Runtime(ISystem system)
		: base(system)
	{
	}

	protected override Delegate CastCompileHookToRealType(Delegate del)
	{
		return del.CastDelegate<CoreCLR.V21.CompileMethodDelegate>();
	}
}
