using System;
using System.Diagnostics.CodeAnalysis;

namespace MonoMod.Core.Platforms.Memory;

internal abstract class QueryingMemoryPageAllocatorBase
{
	public abstract uint PageSize { get; }

	public abstract bool TryQueryPage(IntPtr pageAddr, out bool isFree, out IntPtr allocBase, out nint allocSize);

	public abstract bool TryAllocatePage(nint size, bool executable, out IntPtr allocated);

	public abstract bool TryAllocatePage(IntPtr pageAddr, nint size, bool executable, out IntPtr allocated);

	public abstract bool TryFreePage(IntPtr pageAddr, [_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003ENotNullWhen(false)] out string? errorMsg);
}
