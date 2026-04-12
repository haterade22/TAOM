using System.Runtime.CompilerServices;

namespace System.Threading;

internal static class MonitorEx
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void Enter(object obj, ref bool lockTaken)
	{
		Monitor.Enter(obj, ref lockTaken);
	}
}
