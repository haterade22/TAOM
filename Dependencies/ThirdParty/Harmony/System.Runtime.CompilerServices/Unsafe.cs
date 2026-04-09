using MonoMod.Backports.ILHelpers;

namespace System.Runtime.CompilerServices;

[CLSCompliant(false)]
internal static class Unsafe
{
	private static class PerTypeValues<T>
	{
		public static readonly nint TypeSize = ComputeTypeSize();

		private static nint ComputeTypeSize()
		{
			T[] array = new T[2];
			return UnsafeRaw.ByteOffset(ref array[0], ref array[1]);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[NonVersionable]
	public unsafe static T Read<T>(void* source)
	{
		return UnsafeRaw.Read<T>(source);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[NonVersionable]
	public unsafe static T ReadUnaligned<T>(void* source)
	{
		return UnsafeRaw.ReadUnaligned<T>(source);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[NonVersionable]
	public static T ReadUnaligned<T>(ref byte source)
	{
		return UnsafeRaw.ReadUnaligned<T>(ref source);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[NonVersionable]
	public unsafe static void Write<T>(void* destination, T value)
	{
		UnsafeRaw.Write(destination, value);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[NonVersionable]
	public unsafe static void WriteUnaligned<T>(void* destination, T value)
	{
		UnsafeRaw.WriteUnaligned(destination, value);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[NonVersionable]
	public static void WriteUnaligned<T>(ref byte destination, T value)
	{
		UnsafeRaw.WriteUnaligned(ref destination, value);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[NonVersionable]
	public unsafe static void Copy<T>(void* destination, ref T source)
	{
		UnsafeRaw.Copy(destination, ref source);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[NonVersionable]
	public unsafe static void Copy<T>(ref T destination, void* source)
	{
		UnsafeRaw.Copy(ref destination, source);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[NonVersionable]
	public unsafe static void* AsPointer<T>(ref T value)
	{
		return UnsafeRaw.AsPointer(ref value);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[NonVersionable]
	public static void SkipInit<T>(out T value)
	{
		UnsafeRaw.SkipInit<T>(out value);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[NonVersionable]
	public unsafe static void CopyBlock(void* destination, void* source, uint byteCount)
	{
		UnsafeRaw.CopyBlock(destination, source, byteCount);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[NonVersionable]
	public static void CopyBlock(ref byte destination, ref byte source, uint byteCount)
	{
		UnsafeRaw.CopyBlock(ref destination, ref source, byteCount);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[NonVersionable]
	public unsafe static void CopyBlockUnaligned(void* destination, void* source, uint byteCount)
	{
		UnsafeRaw.CopyBlockUnaligned(destination, source, byteCount);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[NonVersionable]
	public static void CopyBlockUnaligned(ref byte destination, ref byte source, uint byteCount)
	{
		UnsafeRaw.CopyBlockUnaligned(ref destination, ref source, byteCount);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[NonVersionable]
	public unsafe static void InitBlock(void* startAddress, byte value, uint byteCount)
	{
		UnsafeRaw.InitBlock(startAddress, value, byteCount);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[NonVersionable]
	public static void InitBlock(ref byte startAddress, byte value, uint byteCount)
	{
		UnsafeRaw.InitBlock(ref startAddress, value, byteCount);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[NonVersionable]
	public unsafe static void InitBlockUnaligned(void* startAddress, byte value, uint byteCount)
	{
		UnsafeRaw.InitBlockUnaligned(startAddress, value, byteCount);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[NonVersionable]
	public static void InitBlockUnaligned(ref byte startAddress, byte value, uint byteCount)
	{
		UnsafeRaw.InitBlockUnaligned(ref startAddress, value, byteCount);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[NonVersionable]
	public static T As<T>(object o) where T : class
	{
		return UnsafeRaw.As<T>(o);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[NonVersionable]
	public unsafe static ref T AsRef<T>(void* source)
	{
		return ref UnsafeRaw.AsRef<T>(source);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[NonVersionable]
	public static ref T AsRef<T>(in T source)
	{
		return ref UnsafeRaw.AsRef(in source);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[NonVersionable]
	public static ref TTo As<TFrom, TTo>(ref TFrom source)
	{
		return ref UnsafeRaw.As<TFrom, TTo>(ref source);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[NonVersionable]
	public static ref T Unbox<T>(object box) where T : struct
	{
		return ref UnsafeRaw.Unbox<T>(box);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[NonVersionable]
	public static ref T AddByteOffset<T>(ref T source, nint byteOffset)
	{
		return ref UnsafeRaw.AddByteOffset(ref source, byteOffset);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[NonVersionable]
	public static ref T AddByteOffset<T>(ref T source, nuint byteOffset)
	{
		return ref UnsafeRaw.AddByteOffset(ref source, byteOffset);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[NonVersionable]
	public static ref T SubtractByteOffset<T>(ref T source, nint byteOffset)
	{
		return ref UnsafeRaw.SubtractByteOffset(ref source, byteOffset);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[NonVersionable]
	public static ref T SubtractByteOffset<T>(ref T source, nuint byteOffset)
	{
		return ref UnsafeRaw.SubtractByteOffset(ref source, byteOffset);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[NonVersionable]
	public static nint ByteOffset<T>(ref T origin, ref T target)
	{
		return UnsafeRaw.ByteOffset(ref origin, ref target);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[NonVersionable]
	public static bool AreSame<T>(ref T left, ref T right)
	{
		return UnsafeRaw.AreSame(ref left, ref right);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[NonVersionable]
	public static bool IsAddressGreaterThan<T>(ref T left, ref T right)
	{
		return UnsafeRaw.IsAddressGreaterThan(ref left, ref right);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[NonVersionable]
	public static bool IsAddressLessThan<T>(ref T left, ref T right)
	{
		return UnsafeRaw.IsAddressLessThan(ref left, ref right);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[NonVersionable]
	public static bool IsNullRef<T>(ref T source)
	{
		return UnsafeRaw.IsNullRef(ref source);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[NonVersionable]
	public static ref T NullRef<T>()
	{
		return ref UnsafeRaw.NullRef<T>();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[NonVersionable]
	public static int SizeOf<T>()
	{
		return (int)PerTypeValues<T>.TypeSize;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[NonVersionable]
	public static ref T Add<T>(ref T source, int elementOffset)
	{
		return ref UnsafeRaw.AddByteOffset(ref source, elementOffset * PerTypeValues<T>.TypeSize);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[NonVersionable]
	public unsafe static void* Add<T>(void* source, int elementOffset)
	{
		return (byte*)source + elementOffset * PerTypeValues<T>.TypeSize;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[NonVersionable]
	public static ref T Add<T>(ref T source, nint elementOffset)
	{
		return ref UnsafeRaw.AddByteOffset(ref source, elementOffset * PerTypeValues<T>.TypeSize);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[NonVersionable]
	public static ref T Add<T>(ref T source, nuint elementOffset)
	{
		return ref UnsafeRaw.AddByteOffset(ref source, elementOffset * (nuint)PerTypeValues<T>.TypeSize);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[NonVersionable]
	public static ref T Subtract<T>(ref T source, int elementOffset)
	{
		return ref UnsafeRaw.SubtractByteOffset(ref source, elementOffset * PerTypeValues<T>.TypeSize);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[NonVersionable]
	public unsafe static void* Subtract<T>(void* source, int elementOffset)
	{
		return (byte*)source - elementOffset * PerTypeValues<T>.TypeSize;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[NonVersionable]
	public static ref T Subtract<T>(ref T source, nint elementOffset)
	{
		return ref UnsafeRaw.SubtractByteOffset(ref source, elementOffset * PerTypeValues<T>.TypeSize);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[NonVersionable]
	public static ref T Subtract<T>(ref T source, nuint elementOffset)
	{
		return ref UnsafeRaw.SubtractByteOffset(ref source, elementOffset * (nuint)PerTypeValues<T>.TypeSize);
	}
}
