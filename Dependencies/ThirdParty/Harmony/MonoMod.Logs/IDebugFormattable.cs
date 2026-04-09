using System;

namespace MonoMod.Logs;

internal interface IDebugFormattable
{
	bool TryFormatInto(Span<char> span, out int wrote);
}
