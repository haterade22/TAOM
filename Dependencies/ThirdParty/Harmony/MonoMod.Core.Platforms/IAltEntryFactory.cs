using System;

namespace MonoMod.Core.Platforms;

internal interface IAltEntryFactory
{
	IntPtr CreateAlternateEntrypoint(IntPtr entrypoint, int minLength, out IDisposable? handle);
}
