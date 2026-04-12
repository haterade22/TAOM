using System;
using System.Runtime.CompilerServices;
using MonoMod.Logs;

namespace MonoMod.Core.Utils;

internal readonly struct AddressMeaning : IEquatable<AddressMeaning>
{
	public AddressKind Kind { get; }

	public int RelativeToOffset { get; }

	public ulong ConstantValue { get; }

	public AddressMeaning(AddressKind kind)
	{
		ConstantValue = 0uL;
		kind.Validate("kind");
		if (!kind.IsAbsolute())
		{
			throw new ArgumentOutOfRangeException("kind");
		}
		Kind = kind;
		RelativeToOffset = 0;
	}

	public AddressMeaning(AddressKind kind, int relativeOffset)
	{
		ConstantValue = 0uL;
		kind.Validate("kind");
		if (!kind.IsRelative())
		{
			throw new ArgumentOutOfRangeException("kind");
		}
		if (relativeOffset < 0)
		{
			throw new ArgumentOutOfRangeException("relativeOffset");
		}
		Kind = kind;
		RelativeToOffset = relativeOffset;
	}

	public AddressMeaning(AddressKind kind, int relativeOffset, ulong constantValue)
	{
		kind.Validate("kind");
		if (!kind.IsRelative())
		{
			throw new ArgumentOutOfRangeException("kind");
		}
		if (relativeOffset < 0)
		{
			throw new ArgumentOutOfRangeException("relativeOffset");
		}
		Kind = kind;
		RelativeToOffset = relativeOffset;
		ConstantValue = constantValue;
	}

	private unsafe static nint DoProcessAddress(AddressKind kind, nint basePtr, int offset, ulong constantValue, ulong address)
	{
		if (kind.IsConstant())
		{
			address = constantValue;
		}
		nint num;
		if (kind.IsAbsolute())
		{
			num = (nint)address;
		}
		else
		{
			long num2 = (kind.Is32Bit() ? Unsafe.As<ulong, int>(ref address) : Unsafe.As<ulong, long>(ref address));
			num = (nint)(basePtr + offset + num2);
		}
		if (kind.IsIndirect())
		{
			num = *(nint*)num;
		}
		return num;
	}

	public nint ProcessAddress(nint basePtr, int offset, ulong address)
	{
		return DoProcessAddress(Kind, basePtr, offset + RelativeToOffset, ConstantValue, address);
	}

	public override bool Equals(object? obj)
	{
		if (obj is AddressMeaning other)
		{
			return Equals(other);
		}
		return false;
	}

	public bool Equals(AddressMeaning other)
	{
		if (Kind == other.Kind && RelativeToOffset == other.RelativeToOffset)
		{
			return ConstantValue == other.ConstantValue;
		}
		return false;
	}

	public override string ToString()
	{
		FormatInterpolatedStringHandler handler = new FormatInterpolatedStringHandler(38, 3);
		handler.AppendLiteral("AddressMeaning(");
		handler.AppendFormatted(Kind.FastToString());
		handler.AppendLiteral(", offset: ");
		handler.AppendFormatted(RelativeToOffset);
		handler.AppendLiteral(", constant: ");
		handler.AppendFormatted(ConstantValue);
		handler.AppendLiteral(")");
		return DebugFormatter.Format(ref handler);
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(Kind, RelativeToOffset, ConstantValue);
	}

	public static bool operator ==(AddressMeaning left, AddressMeaning right)
	{
		return left.Equals(right);
	}

	public static bool operator !=(AddressMeaning left, AddressMeaning right)
	{
		return !(left == right);
	}
}
