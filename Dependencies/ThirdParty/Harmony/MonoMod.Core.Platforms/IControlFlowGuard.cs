using System;

namespace MonoMod.Core.Platforms;

internal interface IControlFlowGuard
{
	bool IsSupported { get; }

	int TargetAlignmentRequirement { get; }

	unsafe void RegisterValidIndirectCallTargets(void* memoryRegionStart, nint memoryRegionLength, ReadOnlySpan<nint> validTargetsInMemoryRegion);
}
