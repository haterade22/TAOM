using System;

namespace MonoMod.Core.Platforms;

[Flags]
internal enum SystemFeature
{
	None = 0,
	RWXPages = 1,
	RXPages = 2,
	MayUseNativeJitHooks = 0x10
}
