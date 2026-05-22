using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using MonoMod.Logs;
using MonoMod.Utils;

namespace MonoMod.Core.Interop;

internal static class OSX
{
	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public struct vm_region_submap_short_info_64
	{
		public vm_prot_t protection;

		public vm_prot_t max_protection;

		public vm_inherit_t inheritance;

		public ulong offset;

		public uint user_tag;

		public uint ref_count;

		public ushort shadow_depth;

		public byte external_pager;

		public ShareMode share_mode;

		public boolean_t is_submap;

		public vm_behavior_t behavior;

		public uint object_id;

		public ushort user_wired_count;

		public unsafe static int Count => sizeof(vm_region_submap_short_info_64) / 4;
	}

	public enum ShareMode : byte
	{
		COW = 1,
		Private,
		Empty,
		Shared,
		TrueShared,
		PrivateAliased,
		SharedAliased,
		LargePage
	}

	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public struct task_dyld_info
	{
		public ulong all_image_info_addr;

		public ulong all_image_info_size;

		public task_dyld_all_image_info_format all_image_info_format;

		public unsafe dyld_all_image_infos* all_image_infos => (dyld_all_image_infos*)all_image_info_addr;

		public unsafe static int Count => sizeof(task_dyld_info) / 4;
	}

	public struct dyld_all_image_infos
	{
		public uint version;

		public uint infoArrayCount;

		public unsafe dyld_image_info* infoArray;

		public unsafe ReadOnlySpan<dyld_image_info> InfoArray => new ReadOnlySpan<dyld_image_info>(infoArray, (int)infoArrayCount);
	}

	public struct dyld_image_info
	{
		public unsafe void* imageLoadAddress;

		public PCSTR imageFilePath;

		public nuint imageFileModDate;
	}

	public enum task_dyld_all_image_info_format
	{
		Bits32,
		Bits64
	}

	public enum task_flavor_t : uint
	{
		DyldInfo = 17u
	}

	public enum vm_region_flavor_t
	{
		BasicInfo64 = 9
	}

	[Flags]
	public enum vm_prot_t
	{
		None = 0,
		Read = 1,
		Write = 2,
		Execute = 4,
		Default = 3,
		All = 7,
		[Obsolete("Only used for memory_object_lock_request. Invalid otherwise.")]
		NoChange = 8,
		Copy = 0x10,
		WantsCopy = 0x10,
		[Obsolete("Invalid value. Indicates that other bits are to be applied as mask to actual bits.")]
		IsMask = 0x40,
		[Obsolete("Invalid value. Tells mprotect to not set Read. Used for execute-only.")]
		StripRead = 0x80,
		[Obsolete("Invalid value. Use only for mprotect.")]
		ExecuteOnly = 0x84
	}

	public struct VmProtFmtProxy : IDebugFormattable
	{
		private readonly vm_prot_t value;

		public VmProtFmtProxy(vm_prot_t value)
		{
			this.value = value;
		}

		public bool TryFormatInto(Span<char> span, out int wrote)
		{
			int num = 0;
			if (value.Has(vm_prot_t.NoChange))
			{
				if (span.Slice(num).Length < 1)
				{
					wrote = num;
					return false;
				}
				span[num++] = '~';
			}
			if (span.Slice(num).Length < 3)
			{
				wrote = 0;
				return false;
			}
			span[num++] = (value.Has(vm_prot_t.Read) ? 'r' : '-');
			span[num++] = (value.Has(vm_prot_t.Write) ? 'w' : '-');
			span[num++] = (value.Has(vm_prot_t.Execute) ? 'x' : '-');
			if (value.Has(vm_prot_t.StripRead))
			{
				if (span.Slice(num).Length < 1)
				{
					wrote = num;
					return false;
				}
				span[num++] = '!';
			}
			if (value.Has(vm_prot_t.Copy))
			{
				if (span.Slice(num).Length < 1)
				{
					wrote = num;
					return false;
				}
				span[num++] = 'c';
			}
			if (value.Has(vm_prot_t.IsMask))
			{
				if (span.Slice(num).Length < " (mask)".Length)
				{
					wrote = num;
					return false;
				}
				" (mask)".AsSpan().CopyTo(span.Slice(num));
				num += " (mask)".Length;
			}
			wrote = num;
			return true;
		}
	}

	[Flags]
	public enum vm_flags
	{
		Fixed = 0,
		Anywhere = 1,
		Purgable = 2,
		Chunk4GB = 4,
		RandomAddr = 8,
		NoCache = 0x10,
		Overwrite = 0x4000,
		SuperpageMask = 0x70000,
		SuperpageSizeAny = 0x10000,
		SuperpageWSize2MB = 0x20000,
		AliasMask = -16777216
	}

	public enum vm_inherit_t : uint
	{
		Share = 0u,
		Copy = 1u,
		None = 2u,
		DonateCopy = 3u,
		Default = 1u,
		LastValid = 2u
	}

	public enum vm_behavior_t
	{
		Default,
		Random,
		Sequential,
		ReverseSequential,
		WillNeed,
		DontNeed,
		Free,
		ZeroWiredPages,
		Reusable,
		Reuse,
		CanReuse,
		PageOut
	}

	[DebuggerDisplay("{ToString(),nq}")]
	public struct boolean_t
	{
		private int value;

		public boolean_t(bool value)
		{
			this.value = (value ? 1 : 0);
		}

		public static implicit operator bool(boolean_t v)
		{
			return v.value != 0;
		}

		public static implicit operator boolean_t(bool v)
		{
			return new boolean_t(v);
		}

		public static bool operator true(boolean_t v)
		{
			return v;
		}

		public static bool operator false(boolean_t v)
		{
			return !v;
		}

		public override string ToString()
		{
			if (!this)
			{
				return "false";
			}
			return "true";
		}
	}

	[DebuggerDisplay("{Value}")]
	public struct kern_return_t : IEquatable<kern_return_t>
	{
		private int value;

		public static kern_return_t Success = new kern_return_t(0);

		public static kern_return_t InvalidAddress = new kern_return_t(1);

		public static kern_return_t ProtectionFailure = new kern_return_t(2);

		public static kern_return_t NoSpace = new kern_return_t(3);

		public static kern_return_t InvalidArgument = new kern_return_t(4);

		public static kern_return_t Failure = new kern_return_t(5);

		public int Value => value;

		public kern_return_t(int value)
		{
			this.value = value;
		}

		public static implicit operator bool(kern_return_t v)
		{
			return v.value == 0;
		}

		public static bool operator ==(kern_return_t x, kern_return_t y)
		{
			return x.value == y.value;
		}

		public static bool operator !=(kern_return_t x, kern_return_t y)
		{
			return x.value != y.value;
		}

		public override bool Equals(object? obj)
		{
			if (obj is kern_return_t other)
			{
				return Equals(other);
			}
			return false;
		}

		public bool Equals(kern_return_t other)
		{
			return value == other.value;
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(value);
		}
	}

	[Flags]
	public enum map_prot
	{
		None = 0,
		Read = 1,
		Write = 2,
		Execute = 4
	}

	[Flags]
	public enum map_flags
	{
		Private = 2,
		Fixed = 0x10,
		JIT = 0x800,
		Anonymous = 0x1000,
		Failed = 0x1001
	}

	public const string LibSystem = "libSystem";

	private unsafe static int* mach_task_self_;

	public unsafe static int Errno => *__error();

	[DllImport("libSystem", EntryPoint = "getpagesize")]
	public static extern int GetPageSize();

	[DllImport("libSystem")]
	public unsafe static extern void sys_icache_invalidate(void* start, nuint size);

	[DllImport("libSystem", CallingConvention = CallingConvention.Cdecl, EntryPoint = "mkstemp")]
	public unsafe static extern int MkSTemp(byte* template);

	[DllImport("libSystem", CallingConvention = CallingConvention.Cdecl, EntryPoint = "close")]
	public static extern int Close(int fd);

	[DllImport("libSystem", CallingConvention = CallingConvention.Cdecl)]
	public unsafe static extern int* __error();

	static OSX()
	{
		_ = Errno;
	}

	[DllImport("libSystem")]
	public unsafe static extern kern_return_t mach_vm_region_recurse(int targetTask, [In][Out] ulong* address, [Out] ulong* size, [In][Out] int* nestingDepth, [Out] vm_region_submap_short_info_64* info, [In][Out] int* infoSize);

	[DllImport("libSystem")]
	public static extern kern_return_t mach_vm_protect(int targetTask, ulong address, ulong size, boolean_t setMax, vm_prot_t protection);

	[DllImport("libSystem")]
	public unsafe static extern kern_return_t mach_vm_allocate(int targetTask, [In][Out] ulong* address, ulong size, vm_flags flags);

	[DllImport("libSystem")]
	public unsafe static extern kern_return_t mach_vm_map(int targetTask, [In][Out] ulong* address, ulong size, ulong mask, vm_flags flags, int @object, ulong offset, boolean_t copy, vm_prot_t curProt, vm_prot_t maxProt, vm_inherit_t inheritance);

	[DllImport("libSystem")]
	public unsafe static extern kern_return_t mach_vm_remap(int targetTask, [In][Out] ulong* targetAddress, ulong size, ulong offset, vm_flags flags, int srcTask, ulong srcAddress, boolean_t copy, [Out] vm_prot_t* curProt, [Out] vm_prot_t* maxProt, vm_inherit_t inherit);

	[DllImport("libSystem")]
	public unsafe static extern kern_return_t mach_make_memory_entry_64(int targetTask, [In][Out] ulong* size, ulong offset, vm_prot_t permission, int* objectHandle, int parentHandle);

	[DllImport("libSystem")]
	public static extern kern_return_t mach_vm_deallocate(int targetTask, ulong address, ulong size);

	public unsafe static int mach_task_self()
	{
		int* ptr = mach_task_self_;
		if (ptr == null)
		{
			IntPtr intPtr = DynDll.OpenLibrary("libSystem");
			try
			{
				ptr = (mach_task_self_ = (int*)(void*)intPtr.GetExport("mach_task_self_"));
			}
			finally
			{
				DynDll.CloseLibrary(intPtr);
			}
		}
		return *ptr;
	}

	[DllImport("libSystem")]
	public unsafe static extern kern_return_t task_info(int targetTask, task_flavor_t flavor, [Out] task_dyld_info* taskInfoOut, int* taskInfoCnt);

	public static VmProtFmtProxy P(vm_prot_t prot)
	{
		return new VmProtFmtProxy(prot);
	}

	[DllImport("libSystem")]
	public static extern IntPtr mmap(IntPtr address, ulong length, map_prot prot, map_flags flags, int fd, long offset);
}
