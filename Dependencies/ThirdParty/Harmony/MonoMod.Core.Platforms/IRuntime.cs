using System;
using System.Reflection;
using MonoMod.Utils;

namespace MonoMod.Core.Platforms;

internal interface IRuntime
{
	RuntimeKind Target { get; }

	RuntimeFeature Features { get; }

	Abi Abi { get; }

	event OnMethodCompiledCallback? OnMethodCompiled;

	MethodBase GetIdentifiable(MethodBase method);

	RuntimeMethodHandle GetMethodHandle(MethodBase method);

	bool RequiresGenericContext(MethodBase method);

	void DisableInlining(MethodBase method);

	IDisposable? PinMethodIfNeeded(MethodBase method);

	IntPtr GetMethodEntryPoint(MethodBase method);

	void Compile(MethodBase method);
}
