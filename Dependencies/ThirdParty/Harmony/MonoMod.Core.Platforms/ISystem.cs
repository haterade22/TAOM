using System;
using System.Collections.Generic;
using MonoMod.Utils;

namespace MonoMod.Core.Platforms;

internal interface ISystem
{
	OSKind Target { get; }

	SystemFeature Features { get; }

	Abi? DefaultAbi { get; }

	IMemoryAllocator MemoryAllocator { get; }

	INativeExceptionHelper? NativeExceptionHelper { get; }

	IEnumerable<string?> EnumerateLoadedModuleFiles();

	nint GetSizeOfReadableMemory(IntPtr start, nint guess);

	void PatchData(PatchTargetKind targetKind, IntPtr patchTarget, ReadOnlySpan<byte> data, Span<byte> backup);

	IntPtr GetNativeJitHookConfig(int runtimeMajMin);
}
