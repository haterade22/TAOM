using System;
using MonoMod.Utils;

namespace MonoMod.Core.Platforms.Runtimes;

internal abstract class FxBaseRuntime : FxCoreBaseRuntime
{
	public override RuntimeKind Target => RuntimeKind.Framework;

	public static FxBaseRuntime CreateForVersion(Version version, ISystem system)
	{
		if (version.Major == 4)
		{
			return new FxCLR4Runtime(system);
		}
		if (version.Major == 2)
		{
			return new FxCLR2Runtime(system);
		}
		throw new PlatformNotSupportedException($"CLR version {version} is not suppoted.");
	}
}
