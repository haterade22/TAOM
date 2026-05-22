using System;

namespace MonoMod.Core.Platforms;

internal readonly record struct PositionedAllocationRequest(IntPtr Target, IntPtr LowBound, IntPtr HighBound, AllocationRequest Base);
