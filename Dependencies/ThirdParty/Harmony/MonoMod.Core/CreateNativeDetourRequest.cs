using System;

namespace MonoMod.Core;

[CLSCompliant(true)]
internal readonly record struct CreateNativeDetourRequest(IntPtr Source, IntPtr Target)
{
	public bool ApplyByDefault { get; init; } = true;
}
