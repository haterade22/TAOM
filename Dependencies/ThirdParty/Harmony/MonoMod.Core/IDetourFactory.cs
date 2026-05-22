using System;

namespace MonoMod.Core;

[CLSCompliant(true)]
internal interface IDetourFactory
{
	bool SupportsNativeDetourOrigEntrypoint { get; }

	ICoreDetour CreateDetour(CreateDetourRequest request);

	ICoreNativeDetour CreateNativeDetour(CreateNativeDetourRequest request);
}
