using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MonoMod.Utils;

namespace MonoMod.Core.Interop;

internal static class Windows
{
	[Conditional("NEVER")]
	[AttributeUsage(AttributeTargets.All)]
	private sealed class SetsLastSystemErrorAttribute : Attribute
	{
	}

	[Conditional("NEVER")]
	[AttributeUsage(AttributeTargets.All)]
	private sealed class NativeTypeNameAttribute : Attribute
	{
		public NativeTypeNameAttribute(string x)
		{
		}
	}

	public struct MEMORY_BASIC_INFORMATION
	{
		public unsafe void* BaseAddress;

		public unsafe void* AllocationBase;

		public uint AllocationProtect;

		public nuint RegionSize;

		public uint State;

		public uint Protect;

		public uint Type;
	}

	public struct SYSTEM_INFO
	{
		[StructLayout(LayoutKind.Explicit)]
		public struct _Anonymous_e__Union
		{
			public struct _Anonymous_e__Struct
			{
				public ushort wProcessorArchitecture;

				public ushort wReserved;
			}

			[FieldOffset(0)]
			public uint dwOemId;

			[FieldOffset(0)]
			public _Anonymous_e__Struct Anonymous;
		}

		public _Anonymous_e__Union Anonymous;

		public uint dwPageSize;

		public unsafe void* lpMinimumApplicationAddress;

		public unsafe void* lpMaximumApplicationAddress;

		public nuint dwActiveProcessorMask;

		public uint dwNumberOfProcessors;

		public uint dwProcessorType;

		public uint dwAllocationGranularity;

		public ushort wProcessorLevel;

		public ushort wProcessorRevision;

		[UnscopedRef]
		public ref uint dwOemId
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				return ref Anonymous.dwOemId;
			}
		}

		[UnscopedRef]
		public ref ushort wProcessorArchitecture
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				return ref Anonymous.Anonymous.wProcessorArchitecture;
			}
		}

		[UnscopedRef]
		public ref ushort wReserved
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				return ref Anonymous.Anonymous.wReserved;
			}
		}
	}

	public readonly struct BOOL : IComparable, IComparable<BOOL>, IEquatable<BOOL>, IFormattable
	{
		public readonly int Value;

		public static BOOL FALSE => new BOOL(0);

		public static BOOL TRUE => new BOOL(1);

		public BOOL(int value)
		{
			Value = value;
		}

		public static bool operator ==(BOOL left, BOOL right)
		{
			return left.Value == right.Value;
		}

		public static bool operator !=(BOOL left, BOOL right)
		{
			return left.Value != right.Value;
		}

		public static bool operator <(BOOL left, BOOL right)
		{
			return left.Value < right.Value;
		}

		public static bool operator <=(BOOL left, BOOL right)
		{
			return left.Value <= right.Value;
		}

		public static bool operator >(BOOL left, BOOL right)
		{
			return left.Value > right.Value;
		}

		public static bool operator >=(BOOL left, BOOL right)
		{
			return left.Value >= right.Value;
		}

		public static implicit operator bool(BOOL value)
		{
			return value.Value != 0;
		}

		public static implicit operator BOOL(bool value)
		{
			return new BOOL(value ? 1 : 0);
		}

		public static bool operator false(BOOL value)
		{
			return value.Value == 0;
		}

		public static bool operator true(BOOL value)
		{
			return value.Value != 0;
		}

		public static implicit operator BOOL(byte value)
		{
			return new BOOL(value);
		}

		public static explicit operator byte(BOOL value)
		{
			return (byte)value.Value;
		}

		public static implicit operator BOOL(short value)
		{
			return new BOOL(value);
		}

		public static explicit operator short(BOOL value)
		{
			return (short)value.Value;
		}

		public static implicit operator BOOL(int value)
		{
			return new BOOL(value);
		}

		public static implicit operator int(BOOL value)
		{
			return value.Value;
		}

		public static explicit operator BOOL(long value)
		{
			return new BOOL((int)value);
		}

		public static implicit operator long(BOOL value)
		{
			return value.Value;
		}

		public static explicit operator BOOL(nint value)
		{
			return new BOOL((int)value);
		}

		public static implicit operator nint(BOOL value)
		{
			return value.Value;
		}

		public static implicit operator BOOL(sbyte value)
		{
			return new BOOL(value);
		}

		public static explicit operator sbyte(BOOL value)
		{
			return (sbyte)value.Value;
		}

		public static implicit operator BOOL(ushort value)
		{
			return new BOOL(value);
		}

		public static explicit operator ushort(BOOL value)
		{
			return (ushort)value.Value;
		}

		public static explicit operator BOOL(uint value)
		{
			return new BOOL((int)value);
		}

		public static explicit operator uint(BOOL value)
		{
			return (uint)value.Value;
		}

		public static explicit operator BOOL(ulong value)
		{
			return new BOOL((int)value);
		}

		public static explicit operator ulong(BOOL value)
		{
			return (ulong)value.Value;
		}

		public static explicit operator BOOL(nuint value)
		{
			return new BOOL((int)value);
		}

		public static explicit operator nuint(BOOL value)
		{
			return (nuint)value.Value;
		}

		public int CompareTo(object? obj)
		{
			if (obj is BOOL other)
			{
				return CompareTo(other);
			}
			if (obj != null)
			{
				throw new ArgumentException("obj is not an instance of BOOL.");
			}
			return 1;
		}

		public int CompareTo(BOOL other)
		{
			return Value.CompareTo(other.Value);
		}

		public override bool Equals(object? obj)
		{
			if (obj is BOOL other)
			{
				return Equals(other);
			}
			return false;
		}

		public bool Equals(BOOL other)
		{
			return Value.Equals(other.Value);
		}

		public override int GetHashCode()
		{
			return Value.GetHashCode();
		}

		public override string ToString()
		{
			return Value.ToString((IFormatProvider)null);
		}

		public string ToString(string? format, IFormatProvider? formatProvider)
		{
			return Value.ToString(format, formatProvider);
		}
	}

	public readonly struct HANDLE : IComparable, IComparable<HANDLE>, IEquatable<HANDLE>, IFormattable
	{
		public unsafe readonly void* Value;

		public unsafe static HANDLE INVALID_VALUE => new HANDLE((void*)(-1));

		public unsafe static HANDLE NULL => new HANDLE(null);

		public unsafe HANDLE(void* value)
		{
			Value = value;
		}

		public unsafe static bool operator ==(HANDLE left, HANDLE right)
		{
			return left.Value == right.Value;
		}

		public unsafe static bool operator !=(HANDLE left, HANDLE right)
		{
			return left.Value != right.Value;
		}

		public unsafe static bool operator <(HANDLE left, HANDLE right)
		{
			return left.Value < right.Value;
		}

		public unsafe static bool operator <=(HANDLE left, HANDLE right)
		{
			return left.Value <= right.Value;
		}

		public unsafe static bool operator >(HANDLE left, HANDLE right)
		{
			return left.Value > right.Value;
		}

		public unsafe static bool operator >=(HANDLE left, HANDLE right)
		{
			return left.Value >= right.Value;
		}

		public unsafe static explicit operator HANDLE(void* value)
		{
			return new HANDLE(value);
		}

		public unsafe static implicit operator void*(HANDLE value)
		{
			return value.Value;
		}

		public unsafe static explicit operator HANDLE(byte value)
		{
			return new HANDLE((void*)value);
		}

		public unsafe static explicit operator byte(HANDLE value)
		{
			return (byte)value.Value;
		}

		public unsafe static explicit operator HANDLE(short value)
		{
			return new HANDLE((void*)value);
		}

		public unsafe static explicit operator short(HANDLE value)
		{
			return (short)value.Value;
		}

		public unsafe static explicit operator HANDLE(int value)
		{
			return new HANDLE((void*)value);
		}

		public unsafe static explicit operator int(HANDLE value)
		{
			return (int)value.Value;
		}

		public unsafe static explicit operator HANDLE(long value)
		{
			return new HANDLE((void*)value);
		}

		public unsafe static explicit operator long(HANDLE value)
		{
			return (long)value.Value;
		}

		public unsafe static explicit operator HANDLE(nint value)
		{
			return new HANDLE((void*)value);
		}

		public unsafe static implicit operator nint(HANDLE value)
		{
			return (nint)value.Value;
		}

		public unsafe static explicit operator HANDLE(sbyte value)
		{
			return new HANDLE((void*)value);
		}

		public unsafe static explicit operator sbyte(HANDLE value)
		{
			return (sbyte)value.Value;
		}

		public unsafe static explicit operator HANDLE(ushort value)
		{
			return new HANDLE((void*)value);
		}

		public unsafe static explicit operator ushort(HANDLE value)
		{
			return (ushort)value.Value;
		}

		public unsafe static explicit operator HANDLE(uint value)
		{
			return new HANDLE((void*)value);
		}

		public unsafe static explicit operator uint(HANDLE value)
		{
			return (uint)value.Value;
		}

		public unsafe static explicit operator HANDLE(ulong value)
		{
			return new HANDLE((void*)value);
		}

		public unsafe static explicit operator ulong(HANDLE value)
		{
			return (ulong)value.Value;
		}

		public unsafe static explicit operator HANDLE(nuint value)
		{
			return new HANDLE((void*)value);
		}

		public unsafe static implicit operator nuint(HANDLE value)
		{
			return (nuint)value.Value;
		}

		public int CompareTo(object? obj)
		{
			if (obj is HANDLE other)
			{
				return CompareTo(other);
			}
			if (obj != null)
			{
				throw new ArgumentException("obj is not an instance of HANDLE.");
			}
			return 1;
		}

		public unsafe int CompareTo(HANDLE other)
		{
			if (sizeof(IntPtr) != 4)
			{
				return ((ulong)Value).CompareTo((ulong)other.Value);
			}
			return ((uint)Value).CompareTo((uint)other.Value);
		}

		public override bool Equals(object? obj)
		{
			if (obj is HANDLE other)
			{
				return Equals(other);
			}
			return false;
		}

		public unsafe bool Equals(HANDLE other)
		{
			return ((UIntPtr)Value).Equals((nuint)other.Value);
		}

		public unsafe override int GetHashCode()
		{
			return ((UIntPtr)Value).GetHashCode();
		}

		public unsafe override string ToString()
		{
			if (sizeof(UIntPtr) != 4)
			{
				return ((ulong)Value).ToString("X16", null);
			}
			return ((uint)Value).ToString("X8", null);
		}

		public unsafe string ToString(string? format, IFormatProvider? formatProvider)
		{
			if (sizeof(IntPtr) != 4)
			{
				return ((ulong)Value).ToString(format, formatProvider);
			}
			return ((uint)Value).ToString(format, formatProvider);
		}
	}

	public struct CFG_CALL_TARGET_INFO
	{
		public nuint Offset;

		public nuint Flags;
	}

	public const int MEM_COMMIT = 4096;

	public const int MEM_RESERVE = 8192;

	public const int MEM_REPLACE_PLACEHOLDER = 16384;

	public const int MEM_RESERVE_PLACEHOLDER = 262144;

	public const int MEM_RESET = 524288;

	public const int MEM_TOP_DOWN = 1048576;

	public const int MEM_WRITE_WATCH = 2097152;

	public const int MEM_PHYSICAL = 4194304;

	public const int MEM_ROTATE = 8388608;

	public const int MEM_DIFFERENT_IMAGE_BASE_OK = 8388608;

	public const int MEM_RESET_UNDO = 16777216;

	public const int MEM_LARGE_PAGES = 536870912;

	public const uint MEM_4MB_PAGES = 2147483648u;

	public const int MEM_64K_PAGES = 541065216;

	public const int MEM_UNMAP_WITH_TRANSIENT_BOOST = 1;

	public const int MEM_COALESCE_PLACEHOLDERS = 1;

	public const int MEM_PRESERVE_PLACEHOLDER = 2;

	public const int MEM_DECOMMIT = 16384;

	public const int MEM_RELEASE = 32768;

	public const int MEM_FREE = 65536;

	public const int MEM_EXTENDED_PARAMETER_GRAPHICS = 1;

	public const int MEM_EXTENDED_PARAMETER_NONPAGED = 2;

	public const int MEM_EXTENDED_PARAMETER_ZERO_PAGES_OPTIONAL = 4;

	public const int MEM_EXTENDED_PARAMETER_NONPAGED_LARGE = 8;

	public const int MEM_EXTENDED_PARAMETER_NONPAGED_HUGE = 16;

	public const int MEM_EXTENDED_PARAMETER_SOFT_FAULT_PAGES = 32;

	public const int MEM_EXTENDED_PARAMETER_EC_CODE = 64;

	public const int MEM_EXTENDED_PARAMETER_IMAGE_NO_HPAT = 128;

	public const long MEM_EXTENDED_PARAMETER_NUMA_NODE_MANDATORY = long.MinValue;

	public const int MEM_EXTENDED_PARAMETER_TYPE_BITS = 8;

	public const ulong MEM_DEDICATED_ATTRIBUTE_NOT_SPECIFIED = ulong.MaxValue;

	public const int MEM_PRIVATE = 131072;

	public const int MEM_MAPPED = 262144;

	public const int MEM_IMAGE = 16777216;

	public const int PAGE_NOACCESS = 1;

	public const int PAGE_READONLY = 2;

	public const int PAGE_READWRITE = 4;

	public const int PAGE_WRITECOPY = 8;

	public const int PAGE_EXECUTE = 16;

	public const int PAGE_EXECUTE_READ = 32;

	public const int PAGE_EXECUTE_READWRITE = 64;

	public const int PAGE_EXECUTE_WRITECOPY = 128;

	public const int PAGE_GUARD = 256;

	public const int PAGE_NOCACHE = 512;

	public const int PAGE_WRITECOMBINE = 1024;

	public const int PAGE_GRAPHICS_NOACCESS = 2048;

	public const int PAGE_GRAPHICS_READONLY = 4096;

	public const int PAGE_GRAPHICS_READWRITE = 8192;

	public const int PAGE_GRAPHICS_EXECUTE = 16384;

	public const int PAGE_GRAPHICS_EXECUTE_READ = 32768;

	public const int PAGE_GRAPHICS_EXECUTE_READWRITE = 65536;

	public const int PAGE_GRAPHICS_COHERENT = 131072;

	public const int PAGE_GRAPHICS_NOCACHE = 262144;

	public const uint PAGE_ENCLAVE_THREAD_CONTROL = 2147483648u;

	public const uint PAGE_REVERT_TO_FILE_MAP = 2147483648u;

	public const int PAGE_TARGETS_NO_UPDATE = 1073741824;

	public const int PAGE_TARGETS_INVALID = 1073741824;

	public const int PAGE_ENCLAVE_UNVALIDATED = 536870912;

	public const int PAGE_ENCLAVE_MASK = 268435456;

	public const int PAGE_ENCLAVE_DECOMMIT = 268435456;

	public const int PAGE_ENCLAVE_SS_FIRST = 268435457;

	public const int PAGE_ENCLAVE_SS_REST = 268435458;

	public const int PROCESSOR_ARCHITECTURE_INTEL = 0;

	public const int PROCESSOR_ARCHITECTURE_MIPS = 1;

	public const int PROCESSOR_ARCHITECTURE_ALPHA = 2;

	public const int PROCESSOR_ARCHITECTURE_PPC = 3;

	public const int PROCESSOR_ARCHITECTURE_SHX = 4;

	public const int PROCESSOR_ARCHITECTURE_ARM = 5;

	public const int PROCESSOR_ARCHITECTURE_IA64 = 6;

	public const int PROCESSOR_ARCHITECTURE_ALPHA64 = 7;

	public const int PROCESSOR_ARCHITECTURE_MSIL = 8;

	public const int PROCESSOR_ARCHITECTURE_AMD64 = 9;

	public const int PROCESSOR_ARCHITECTURE_IA32_ON_WIN64 = 10;

	public const int PROCESSOR_ARCHITECTURE_NEUTRAL = 11;

	public const int PROCESSOR_ARCHITECTURE_ARM64 = 12;

	public const int PROCESSOR_ARCHITECTURE_ARM32_ON_WIN64 = 13;

	public const int PROCESSOR_ARCHITECTURE_IA32_ON_ARM64 = 14;

	public const int PROCESSOR_ARCHITECTURE_UNKNOWN = 65535;

	public const int CFG_CALL_TARGET_VALID = 1;

	public const int CFG_CALL_TARGET_PROCESSED = 2;

	public const int CFG_CALL_TARGET_CONVERT_EXPORT_SUPPRESSED_TO_VALID = 4;

	public const int CFG_CALL_TARGET_VALID_XFG = 8;

	public const int CFG_CALL_TARGET_CONVERT_XFG_TO_CFG = 16;

	private static bool? hasSetProcessValidCallTargets;

	public static bool HasSetProcessValidCallTargets
	{
		get
		{
			bool valueOrDefault = hasSetProcessValidCallTargets == true;
			if (!hasSetProcessValidCallTargets.HasValue)
			{
				valueOrDefault = DoGet();
				hasSetProcessValidCallTargets = valueOrDefault;
				return valueOrDefault;
			}
			return valueOrDefault;
			[MethodImpl(MethodImplOptions.NoInlining)]
			static bool DoGet()
			{
				IntPtr intPtr = DynDll.OpenLibrary("kernelbase");
				try
				{
					IntPtr functionPtr;
					return intPtr.TryGetExport("SetProcessValidCallTargets", out functionPtr);
				}
				finally
				{
					DynDll.CloseLibrary(intPtr);
				}
			}
		}
	}

	[DllImport("kernel32", ExactSpelling = true)]
	public unsafe static extern void* VirtualAlloc(void* lpAddress, nuint dwSize, uint flAllocationType, uint flProtect);

	[DllImport("kernel32", ExactSpelling = true)]
	public unsafe static extern BOOL VirtualProtect(void* lpAddress, nuint dwSize, uint flNewProtect, uint* lpflOldProtect);

	[DllImport("kernel32", ExactSpelling = true)]
	public unsafe static extern BOOL VirtualFree(void* lpAddress, nuint dwSize, uint dwFreeType);

	[DllImport("kernel32", ExactSpelling = true)]
	public unsafe static extern nuint VirtualQuery(void* lpAddress, MEMORY_BASIC_INFORMATION* lpBuffer, nuint dwLength);

	[DllImport("kernel32", ExactSpelling = true)]
	public unsafe static extern void GetSystemInfo(SYSTEM_INFO* lpSystemInfo);

	[DllImport("kernel32", ExactSpelling = true)]
	public static extern HANDLE GetCurrentProcess();

	[DllImport("kernel32", ExactSpelling = true)]
	public unsafe static extern BOOL FlushInstructionCache(HANDLE hProcess, void* lpBaseAddress, nuint dwSize);

	[DllImport("kernel32", ExactSpelling = true)]
	public static extern uint GetLastError();

	[DllImport("kernelbase", ExactSpelling = true)]
	private unsafe static extern BOOL SetProcessValidCallTargets(HANDLE hProcess, void* VirtualAddress, nuint RegionSize, uint NumberOfOffsets, CFG_CALL_TARGET_INFO* OffsetInformation);

	public unsafe static BOOL TrySetProcessValidCallTargets(void* VirtualAddress, nuint RegionSize, uint NumberOfOffsets, CFG_CALL_TARGET_INFO* OffsetInformation)
	{
		if (HasSetProcessValidCallTargets)
		{
			return SetProcessValidCallTargets(GetCurrentProcess(), VirtualAddress, RegionSize, NumberOfOffsets, OffsetInformation);
		}
		return false;
	}
}
