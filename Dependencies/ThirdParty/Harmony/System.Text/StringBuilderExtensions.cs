using System.Runtime.CompilerServices;

namespace System.Text;

internal static class StringBuilderExtensions
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static StringBuilder Clear(this StringBuilder builder)
	{
		System.ThrowHelper.ThrowIfArgumentNull(builder, "builder");
		return builder.Clear();
	}
}
