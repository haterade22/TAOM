using System.Diagnostics.CodeAnalysis;

namespace MonoMod.Core.Platforms;

internal interface IMemoryAllocator
{
	int MaxSize { get; }

	bool TryAllocate(AllocationRequest request, [_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMaybeNullWhen(false)] out IAllocatedMemory allocated);

	bool TryAllocateInRange(PositionedAllocationRequest request, [_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMaybeNullWhen(false)] out IAllocatedMemory allocated);
}
