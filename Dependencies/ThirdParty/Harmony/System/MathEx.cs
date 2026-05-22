using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace System;

internal static class MathEx
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static byte Clamp(byte value, byte min, byte max)
	{
		if (min > max)
		{
			ThrowMinMaxException(min, max);
		}
		if (value < min)
		{
			return min;
		}
		if (value > max)
		{
			return max;
		}
		return value;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static decimal Clamp(decimal value, decimal min, decimal max)
	{
		if (min > max)
		{
			ThrowMinMaxException(min, max);
		}
		if (value < min)
		{
			return min;
		}
		if (value > max)
		{
			return max;
		}
		return value;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static double Clamp(double value, double min, double max)
	{
		if (min > max)
		{
			ThrowMinMaxException(min, max);
		}
		if (value < min)
		{
			return min;
		}
		if (value > max)
		{
			return max;
		}
		return value;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static short Clamp(short value, short min, short max)
	{
		if (min > max)
		{
			ThrowMinMaxException(min, max);
		}
		if (value < min)
		{
			return min;
		}
		if (value > max)
		{
			return max;
		}
		return value;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int Clamp(int value, int min, int max)
	{
		if (min > max)
		{
			ThrowMinMaxException(min, max);
		}
		if (value < min)
		{
			return min;
		}
		if (value > max)
		{
			return max;
		}
		return value;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static long Clamp(long value, long min, long max)
	{
		if (min > max)
		{
			ThrowMinMaxException(min, max);
		}
		if (value < min)
		{
			return min;
		}
		if (value > max)
		{
			return max;
		}
		return value;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static nint Clamp(nint value, nint min, nint max)
	{
		if (min > max)
		{
			ThrowMinMaxException(min, max);
		}
		if (value < min)
		{
			return min;
		}
		if (value > max)
		{
			return max;
		}
		return value;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[CLSCompliant(false)]
	public static sbyte Clamp(sbyte value, sbyte min, sbyte max)
	{
		if (min > max)
		{
			ThrowMinMaxException(min, max);
		}
		if (value < min)
		{
			return min;
		}
		if (value > max)
		{
			return max;
		}
		return value;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float Clamp(float value, float min, float max)
	{
		if (min > max)
		{
			ThrowMinMaxException(min, max);
		}
		if (value < min)
		{
			return min;
		}
		if (value > max)
		{
			return max;
		}
		return value;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[CLSCompliant(false)]
	public static ushort Clamp(ushort value, ushort min, ushort max)
	{
		if (min > max)
		{
			ThrowMinMaxException(min, max);
		}
		if (value < min)
		{
			return min;
		}
		if (value > max)
		{
			return max;
		}
		return value;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[CLSCompliant(false)]
	public static uint Clamp(uint value, uint min, uint max)
	{
		if (min > max)
		{
			ThrowMinMaxException(min, max);
		}
		if (value < min)
		{
			return min;
		}
		if (value > max)
		{
			return max;
		}
		return value;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[CLSCompliant(false)]
	public static ulong Clamp(ulong value, ulong min, ulong max)
	{
		if (min > max)
		{
			ThrowMinMaxException(min, max);
		}
		if (value < min)
		{
			return min;
		}
		if (value > max)
		{
			return max;
		}
		return value;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[CLSCompliant(false)]
	public static nuint Clamp(nuint value, nuint min, nuint max)
	{
		if (min > max)
		{
			ThrowMinMaxException(min, max);
		}
		if (value < min)
		{
			return min;
		}
		if (value > max)
		{
			return max;
		}
		return value;
	}

	[_003C1c2fb156_002De9ba_002D45cc_002Daf54_002Dd5335bdb59af_003EDoesNotReturn]
	private static void ThrowMinMaxException<T>(T min, T max)
	{
		throw new ArgumentException($"Minimum {min} is less than maximum {max}");
	}
}
