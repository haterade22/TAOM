using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace System;

internal static class ThrowHelper
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static void ThrowIfArgumentNull([_003C1c2fb156_002De9ba_002D45cc_002Daf54_002Dd5335bdb59af_003ENotNull] object? obj, System.ExceptionArgument argument)
	{
		if (obj == null)
		{
			ThrowArgumentNullException(argument);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static void ThrowIfArgumentNull([_003C1c2fb156_002De9ba_002D45cc_002Daf54_002Dd5335bdb59af_003ENotNull] object? obj, string argument, string? message = null)
	{
		if (obj == null)
		{
			ThrowArgumentNullException(argument, message);
		}
	}

	[_003C1c2fb156_002De9ba_002D45cc_002Daf54_002Dd5335bdb59af_003EDoesNotReturn]
	internal static void ThrowArgumentNullException(System.ExceptionArgument argument)
	{
		throw CreateArgumentNullException(argument);
	}

	[_003C1c2fb156_002De9ba_002D45cc_002Daf54_002Dd5335bdb59af_003EDoesNotReturn]
	internal static void ThrowArgumentNullException(string argument, string? message = null)
	{
		throw CreateArgumentNullException(argument, message);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static Exception CreateArgumentNullException(System.ExceptionArgument argument)
	{
		return CreateArgumentNullException(argument.ToString());
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static Exception CreateArgumentNullException(string argument, string? message = null)
	{
		return new ArgumentNullException(argument, message);
	}

	[_003C1c2fb156_002De9ba_002D45cc_002Daf54_002Dd5335bdb59af_003EDoesNotReturn]
	internal static void ThrowArrayTypeMismatchException()
	{
		throw CreateArrayTypeMismatchException();
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static Exception CreateArrayTypeMismatchException()
	{
		return new ArrayTypeMismatchException();
	}

	[_003C1c2fb156_002De9ba_002D45cc_002Daf54_002Dd5335bdb59af_003EDoesNotReturn]
	internal static void ThrowArgumentException_InvalidTypeWithPointersNotSupported(Type type)
	{
		throw CreateArgumentException_InvalidTypeWithPointersNotSupported(type);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static Exception CreateArgumentException_InvalidTypeWithPointersNotSupported(Type type)
	{
		return new ArgumentException($"Type {type} with managed pointers cannot be used in a Span");
	}

	[_003C1c2fb156_002De9ba_002D45cc_002Daf54_002Dd5335bdb59af_003EDoesNotReturn]
	internal static void ThrowArgumentException_DestinationTooShort()
	{
		throw CreateArgumentException_DestinationTooShort();
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static Exception CreateArgumentException_DestinationTooShort()
	{
		return new ArgumentException("Destination too short");
	}

	[_003C1c2fb156_002De9ba_002D45cc_002Daf54_002Dd5335bdb59af_003EDoesNotReturn]
	internal static void ThrowArgumentException(string message, string? argument = null)
	{
		throw CreateArgumentException(message, argument);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static Exception CreateArgumentException(string message, string? argument)
	{
		return new ArgumentException(message, argument ?? "");
	}

	[_003C1c2fb156_002De9ba_002D45cc_002Daf54_002Dd5335bdb59af_003EDoesNotReturn]
	internal static void ThrowIndexOutOfRangeException()
	{
		throw CreateIndexOutOfRangeException();
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static Exception CreateIndexOutOfRangeException()
	{
		return new IndexOutOfRangeException();
	}

	[_003C1c2fb156_002De9ba_002D45cc_002Daf54_002Dd5335bdb59af_003EDoesNotReturn]
	internal static void ThrowArgumentOutOfRangeException()
	{
		throw CreateArgumentOutOfRangeException();
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static Exception CreateArgumentOutOfRangeException()
	{
		return new ArgumentOutOfRangeException();
	}

	[_003C1c2fb156_002De9ba_002D45cc_002Daf54_002Dd5335bdb59af_003EDoesNotReturn]
	internal static void ThrowArgumentOutOfRangeException(System.ExceptionArgument argument)
	{
		throw CreateArgumentOutOfRangeException(argument);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static Exception CreateArgumentOutOfRangeException(System.ExceptionArgument argument)
	{
		return new ArgumentOutOfRangeException(argument.ToString());
	}

	[_003C1c2fb156_002De9ba_002D45cc_002Daf54_002Dd5335bdb59af_003EDoesNotReturn]
	internal static void ThrowArgumentOutOfRangeException_PrecisionTooLarge()
	{
		throw CreateArgumentOutOfRangeException_PrecisionTooLarge();
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static Exception CreateArgumentOutOfRangeException_PrecisionTooLarge()
	{
		return new ArgumentOutOfRangeException("precision", $"Precision too large (max: {99})");
	}

	[_003C1c2fb156_002De9ba_002D45cc_002Daf54_002Dd5335bdb59af_003EDoesNotReturn]
	internal static void ThrowArgumentOutOfRangeException_SymbolDoesNotFit()
	{
		throw CreateArgumentOutOfRangeException_SymbolDoesNotFit();
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static Exception CreateArgumentOutOfRangeException_SymbolDoesNotFit()
	{
		return new ArgumentOutOfRangeException("symbol", "Bad format specifier");
	}

	[_003C1c2fb156_002De9ba_002D45cc_002Daf54_002Dd5335bdb59af_003EDoesNotReturn]
	internal static void ThrowInvalidOperationException()
	{
		throw CreateInvalidOperationException();
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static Exception CreateInvalidOperationException()
	{
		return new InvalidOperationException();
	}

	[_003C1c2fb156_002De9ba_002D45cc_002Daf54_002Dd5335bdb59af_003EDoesNotReturn]
	internal static void ThrowInvalidOperationException_OutstandingReferences()
	{
		throw CreateInvalidOperationException_OutstandingReferences();
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static Exception CreateInvalidOperationException_OutstandingReferences()
	{
		return new InvalidOperationException("Outstanding references");
	}

	[_003C1c2fb156_002De9ba_002D45cc_002Daf54_002Dd5335bdb59af_003EDoesNotReturn]
	internal static void ThrowInvalidOperationException_UnexpectedSegmentType()
	{
		throw CreateInvalidOperationException_UnexpectedSegmentType();
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static Exception CreateInvalidOperationException_UnexpectedSegmentType()
	{
		return new InvalidOperationException("Unexpected segment type");
	}

	[_003C1c2fb156_002De9ba_002D45cc_002Daf54_002Dd5335bdb59af_003EDoesNotReturn]
	internal static void ThrowInvalidOperationException_EndPositionNotReached()
	{
		throw CreateInvalidOperationException_EndPositionNotReached();
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static Exception CreateInvalidOperationException_EndPositionNotReached()
	{
		return new InvalidOperationException("End position not reached");
	}

	[_003C1c2fb156_002De9ba_002D45cc_002Daf54_002Dd5335bdb59af_003EDoesNotReturn]
	internal static void ThrowArgumentOutOfRangeException_PositionOutOfRange()
	{
		throw CreateArgumentOutOfRangeException_PositionOutOfRange();
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static Exception CreateArgumentOutOfRangeException_PositionOutOfRange()
	{
		return new ArgumentOutOfRangeException("position");
	}

	[_003C1c2fb156_002De9ba_002D45cc_002Daf54_002Dd5335bdb59af_003EDoesNotReturn]
	internal static void ThrowArgumentOutOfRangeException_OffsetOutOfRange()
	{
		throw CreateArgumentOutOfRangeException_OffsetOutOfRange();
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static Exception CreateArgumentOutOfRangeException_OffsetOutOfRange()
	{
		return new ArgumentOutOfRangeException("offset");
	}

	[_003C1c2fb156_002De9ba_002D45cc_002Daf54_002Dd5335bdb59af_003EDoesNotReturn]
	internal static void ThrowObjectDisposedException_ArrayMemoryPoolBuffer()
	{
		throw CreateObjectDisposedException_ArrayMemoryPoolBuffer();
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static Exception CreateObjectDisposedException_ArrayMemoryPoolBuffer()
	{
		return new ObjectDisposedException("ArrayMemoryPoolBuffer");
	}

	[_003C1c2fb156_002De9ba_002D45cc_002Daf54_002Dd5335bdb59af_003EDoesNotReturn]
	internal static void ThrowFormatException_BadFormatSpecifier()
	{
		throw CreateFormatException_BadFormatSpecifier();
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static Exception CreateFormatException_BadFormatSpecifier()
	{
		return new FormatException("Bad format specifier");
	}

	[_003C1c2fb156_002De9ba_002D45cc_002Daf54_002Dd5335bdb59af_003EDoesNotReturn]
	internal static void ThrowArgumentException_OverlapAlignmentMismatch()
	{
		throw CreateArgumentException_OverlapAlignmentMismatch();
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static Exception CreateArgumentException_OverlapAlignmentMismatch()
	{
		return new ArgumentException("Overlap alignment mismatch");
	}

	[_003C1c2fb156_002De9ba_002D45cc_002Daf54_002Dd5335bdb59af_003EDoesNotReturn]
	internal static void ThrowNotSupportedException(string? msg = null)
	{
		throw CreateThrowNotSupportedException(msg);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static Exception CreateThrowNotSupportedException(string? msg)
	{
		return new NotSupportedException();
	}

	[_003C1c2fb156_002De9ba_002D45cc_002Daf54_002Dd5335bdb59af_003EDoesNotReturn]
	internal static void ThrowKeyNullException()
	{
		ThrowArgumentNullException(System.ExceptionArgument.key);
	}

	[_003C1c2fb156_002De9ba_002D45cc_002Daf54_002Dd5335bdb59af_003EDoesNotReturn]
	internal static void ThrowValueNullException()
	{
		throw CreateThrowValueNullException();
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static Exception CreateThrowValueNullException()
	{
		return new ArgumentException("Value is null");
	}

	[_003C1c2fb156_002De9ba_002D45cc_002Daf54_002Dd5335bdb59af_003EDoesNotReturn]
	internal static void ThrowOutOfMemoryException()
	{
		throw CreateOutOfMemoryException();
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static Exception CreateOutOfMemoryException()
	{
		return new OutOfMemoryException();
	}

	public static bool TryFormatThrowFormatException(out int bytesWritten)
	{
		bytesWritten = 0;
		ThrowFormatException_BadFormatSpecifier();
		return false;
	}

	public static bool TryParseThrowFormatException<T>(out T value, out int bytesConsumed)
	{
		value = default(T);
		bytesConsumed = 0;
		ThrowFormatException_BadFormatSpecifier();
		return false;
	}

	[_003C1c2fb156_002De9ba_002D45cc_002Daf54_002Dd5335bdb59af_003EDoesNotReturn]
	public static void ThrowArgumentValidationException<T>(ReadOnlySequenceSegment<T>? startSegment, int startIndex, ReadOnlySequenceSegment<T>? endSegment)
	{
		throw CreateArgumentValidationException(startSegment, startIndex, endSegment);
	}

	private static Exception CreateArgumentValidationException<T>(ReadOnlySequenceSegment<T>? startSegment, int startIndex, ReadOnlySequenceSegment<T>? endSegment)
	{
		if (startSegment == null)
		{
			return CreateArgumentNullException(System.ExceptionArgument.startSegment);
		}
		if (endSegment == null)
		{
			return CreateArgumentNullException(System.ExceptionArgument.endSegment);
		}
		if (startSegment != endSegment && startSegment.RunningIndex > endSegment.RunningIndex)
		{
			return CreateArgumentOutOfRangeException(System.ExceptionArgument.endSegment);
		}
		if ((uint)startSegment.Memory.Length < (uint)startIndex)
		{
			return CreateArgumentOutOfRangeException(System.ExceptionArgument.startIndex);
		}
		return CreateArgumentOutOfRangeException(System.ExceptionArgument.endIndex);
	}

	[_003C1c2fb156_002De9ba_002D45cc_002Daf54_002Dd5335bdb59af_003EDoesNotReturn]
	public static void ThrowArgumentValidationException(Array? array, int start)
	{
		throw CreateArgumentValidationException(array, start);
	}

	private static Exception CreateArgumentValidationException(Array? array, int start)
	{
		if (array == null)
		{
			return CreateArgumentNullException(System.ExceptionArgument.array);
		}
		if ((uint)start > (uint)array.Length)
		{
			return CreateArgumentOutOfRangeException(System.ExceptionArgument.start);
		}
		return CreateArgumentOutOfRangeException(System.ExceptionArgument.length);
	}

	[_003C1c2fb156_002De9ba_002D45cc_002Daf54_002Dd5335bdb59af_003EDoesNotReturn]
	internal static void ThrowArgumentException_TupleIncorrectType(object other)
	{
		throw new ArgumentException($"Value tuple of incorrect type (found {other.GetType()})", "other");
	}

	[_003C1c2fb156_002De9ba_002D45cc_002Daf54_002Dd5335bdb59af_003EDoesNotReturn]
	public static void ThrowStartOrEndArgumentValidationException(long start)
	{
		throw CreateStartOrEndArgumentValidationException(start);
	}

	private static Exception CreateStartOrEndArgumentValidationException(long start)
	{
		if (start < 0)
		{
			return CreateArgumentOutOfRangeException(System.ExceptionArgument.start);
		}
		return CreateArgumentOutOfRangeException(System.ExceptionArgument.length);
	}
}
