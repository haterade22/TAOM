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
		// Decompilation artifact: original IL pins the object reference directly.
		// This implementation achieves the same result via GCHandle.
		var handle = System.Runtime.InteropServices.GCHandle.Alloc(obj, System.Runtime.InteropServices.GCHandleType.Pinned);
		try
		{
			T** ptr = (T**)(void*)handle.AddrOfPinnedObject();
			return ref *(*ptr);
		}
		finally
		{
			handle.Free();
		}
	}
}
