using System;
using System.Runtime.CompilerServices;

namespace MonoMod;

internal static class ILHelpers
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public unsafe static T TailCallDelegatePtr<T>(IntPtr source)
	{
		return ((delegate*<T>)source)();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T TailCallFunc<T>(Func<T> func)
	{
		return func();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public unsafe static ref T ObjectAsRef<T>(object obj)
	{
		// Original IL: ldarg.0; ret — reinterprets object ref as byref T.
		// Used by Span/ReadOnlySpan for pinning. When obj is null, returns null ref
		// (caller adds byte offset to get actual pointer).
		var rawPtr = Unsafe.As<object, IntPtr>(ref obj);
		return ref Unsafe.AsRef<T>((void*)rawPtr);
	}
}
