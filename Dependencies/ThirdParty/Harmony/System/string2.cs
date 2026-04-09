using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace System;

internal static class string2
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsNullOrEmpty([_003Cb37590d4_002D39fb_002D478a_002D88de_002Dd293f3364852_003ENotNullWhen(false)] string? value)
	{
		return string.IsNullOrEmpty(value);
	}
}
