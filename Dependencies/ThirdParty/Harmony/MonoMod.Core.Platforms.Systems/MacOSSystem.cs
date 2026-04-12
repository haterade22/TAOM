using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using MonoMod.Core.Interop;
using MonoMod.Core.Platforms.Memory;
using MonoMod.Core.Utils;
using MonoMod.Utils;

namespace MonoMod.Core.Platforms.Systems;

internal sealed class MacOSSystem : ISystem, IInitialize<IArchitecture>
{
	private sealed class MacOsQueryingAllocator : QueryingMemoryPageAllocatorBase
	{
		public override uint PageSize { get; }

		public MacOsQueryingAllocator()
		{
			PageSize = (uint)OSX.GetPageSize();
		}

		public unsafe override bool TryAllocatePage(nint size, bool executable, out IntPtr allocated)
		{
			Helpers.Assert(size == PageSize, null, "size == PageSize");
			OSX.vm_prot_t vm_prot_t = (executable ? OSX.vm_prot_t.Execute : OSX.vm_prot_t.None);
			vm_prot_t |= OSX.vm_prot_t.Default;
			bool isEnabled;
			if (PlatformDetection.Architecture == ArchitectureKind.Arm64 && vm_prot_t == OSX.vm_prot_t.All)
			{
				_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.Trace("RWX memory detected, doing mmap with MAP_JIT");
				allocated = OSX.mmap(IntPtr.Zero, (ulong)size, OSX.map_prot.Read | OSX.map_prot.Write | OSX.map_prot.Execute, OSX.map_flags.Private | OSX.map_flags.JIT | OSX.map_flags.Anonymous, -1, 0L);
				if (allocated == (IntPtr)(-1))
				{
					int errno = OSX.Errno;
					Win32Exception value = new Win32Exception(errno);
					_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogErrorStringHandler message = new _003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogErrorStringHandler(37, 2, out isEnabled);
					if (isEnabled)
					{
						message.AppendLiteral("Error creating allocation anywhere! ");
						message.AppendFormatted(errno);
						message.AppendLiteral(" ");
						message.AppendFormatted(value);
					}
					_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.Error(ref message);
					allocated = default(IntPtr);
					return false;
				}
				_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogTraceStringHandler message2 = new _003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogTraceStringHandler(37, 2, out isEnabled);
				if (isEnabled)
				{
					message2.AppendLiteral("RWX memory allocated to 0x");
					message2.AppendFormatted(allocated, "X16");
					message2.AppendLiteral(" with size ");
					message2.AppendFormatted(size);
				}
				_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.Trace(ref message2);
				return true;
			}
			ulong num = 0uL;
			OSX.kern_return_t kern_return_t = OSX.mach_vm_map(OSX.mach_task_self(), &num, (ulong)size, 0uL, OSX.vm_flags.Anywhere, 0, 0uL, true, vm_prot_t, vm_prot_t, OSX.vm_inherit_t.Copy);
			if (!kern_return_t)
			{
				_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogErrorStringHandler message3 = new _003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogErrorStringHandler(41, 1, out isEnabled);
				if (isEnabled)
				{
					message3.AppendLiteral("Error creating allocation anywhere! kr = ");
					message3.AppendFormatted(kern_return_t.Value);
				}
				_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.Error(ref message3);
				allocated = default(IntPtr);
				return false;
			}
			allocated = (IntPtr)(long)num;
			return true;
		}

		public unsafe override bool TryAllocatePage(IntPtr pageAddr, nint size, bool executable, out IntPtr allocated)
		{
			Helpers.Assert(size == PageSize, null, "size == PageSize");
			OSX.vm_prot_t vm_prot_t = (executable ? OSX.vm_prot_t.Execute : OSX.vm_prot_t.None);
			vm_prot_t |= OSX.vm_prot_t.Default;
			bool isEnabled;
			if (PlatformDetection.Architecture == ArchitectureKind.Arm64 && vm_prot_t == OSX.vm_prot_t.All)
			{
				_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.Trace("RWX memory detected, doing mmap with MAP_JIT");
				allocated = OSX.mmap(pageAddr, (ulong)size, OSX.map_prot.Read | OSX.map_prot.Write | OSX.map_prot.Execute, OSX.map_flags.Private | OSX.map_flags.Fixed | OSX.map_flags.JIT | OSX.map_flags.Anonymous, -1, 0L);
				if (allocated == (IntPtr)(-1))
				{
					int errno = OSX.Errno;
					Win32Exception value = new Win32Exception(errno);
					_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogErrorStringHandler message = new _003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogErrorStringHandler(37, 2, out isEnabled);
					if (isEnabled)
					{
						message.AppendLiteral("Error creating allocation anywhere! ");
						message.AppendFormatted(errno);
						message.AppendLiteral(" ");
						message.AppendFormatted(value);
					}
					_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.Error(ref message);
					allocated = default(IntPtr);
					return false;
				}
				_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogTraceStringHandler message2 = new _003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogTraceStringHandler(45, 2, out isEnabled);
				if (isEnabled)
				{
					message2.AppendLiteral("RWX memory allocated to page at 0x");
					message2.AppendFormatted(pageAddr, "X16");
					message2.AppendLiteral(" with size ");
					message2.AppendFormatted(size);
				}
				_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.Trace(ref message2);
				return true;
			}
			ulong num = (ulong)(long)pageAddr;
			OSX.kern_return_t kern_return_t = OSX.mach_vm_map(OSX.mach_task_self(), &num, (ulong)size, 0uL, OSX.vm_flags.Fixed, 0, 0uL, true, vm_prot_t, vm_prot_t, OSX.vm_inherit_t.Copy);
			if (!kern_return_t)
			{
				_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogSpamStringHandler message3 = new _003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogSpamStringHandler(38, 2, out isEnabled);
				if (isEnabled)
				{
					message3.AppendLiteral("Error creating allocation at 0x");
					message3.AppendFormatted(num, "x16");
					message3.AppendLiteral(": kr = ");
					message3.AppendFormatted(kern_return_t.Value);
				}
				_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.Spam(ref message3);
				allocated = default(IntPtr);
				return false;
			}
			allocated = (IntPtr)(long)num;
			return true;
		}

		public override bool TryFreePage(IntPtr pageAddr, [_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003ENotNullWhen(false)] out string? errorMsg)
		{
			OSX.kern_return_t kern_return_t = OSX.mach_vm_deallocate(OSX.mach_task_self(), (ulong)(long)pageAddr, PageSize);
			if (!kern_return_t)
			{
				errorMsg = $"Could not deallocate page: kr = {kern_return_t.Value}";
				return false;
			}
			errorMsg = null;
			return true;
		}

		public override bool TryQueryPage(IntPtr pageAddr, out bool isFree, out IntPtr allocBase, out nint allocSize)
		{
			OSX.vm_prot_t prot;
			OSX.vm_prot_t maxProt;
			OSX.kern_return_t localRegionInfo = GetLocalRegionInfo(pageAddr, out allocBase, out allocSize, out prot, out maxProt);
			if ((bool)localRegionInfo)
			{
				if ((nint)allocBase > (nint)pageAddr)
				{
					allocSize = (nint)allocBase - (nint)pageAddr;
					allocBase = pageAddr;
					isFree = true;
					return true;
				}
				isFree = false;
				return true;
			}
			if (localRegionInfo == OSX.kern_return_t.InvalidAddress)
			{
				isFree = true;
				return true;
			}
			isFree = false;
			return false;
		}
	}

	private sealed class MacOSNativeLibDrop : PosixNativeLibraryDrop
	{
		public static readonly MacOSNativeLibDrop Instance = new MacOSNativeLibDrop();

		protected override void CloseFileDescriptor(nint fd)
		{
			OSX.Close((int)fd);
		}

		protected unsafe override nint Mkstemp(Span<byte> template)
		{
			int num;
			fixed (byte* template2 = template)
			{
				num = OSX.MkSTemp(template2);
			}
			if (num == -1)
			{
				int errno = OSX.Errno;
				Win32Exception ex = new Win32Exception(errno);
				bool isEnabled;
				_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogErrorStringHandler message = new _003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogErrorStringHandler(29, 2, out isEnabled);
				if (isEnabled)
				{
					message.AppendLiteral("Could not create temp file: ");
					message.AppendFormatted(errno);
					message.AppendLiteral(" ");
					message.AppendFormatted(ex);
				}
				_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.Error(ref message);
				throw ex;
			}
			return num;
		}
	}

	private sealed class JitMemcpyHelper : PosixExceptionHelper
	{
		private readonly IntPtr mmch_jit_memcpy;

		private readonly IntPtr mmch_jit_hook_config;

		private JitMemcpyHelper(IArchitecture arch, IntPtr getExPtr, IntPtr m2n, IntPtr n2m, IntPtr memcpy, IntPtr jitCfg)
			: base(arch, getExPtr, m2n, n2m)
		{
			mmch_jit_memcpy = memcpy;
			mmch_jit_hook_config = jitCfg;
		}

		public new static JitMemcpyHelper CreateHelper(IArchitecture arch, string filename)
		{
			IntPtr intPtr = DynDll.OpenLibrary(filename);
			IntPtr export;
			IntPtr export2;
			IntPtr export3;
			IntPtr export4;
			IntPtr export5;
			try
			{
				export = intPtr.GetExport("eh_get_exception_ptr");
				export2 = intPtr.GetExport("eh_managed_to_native");
				export3 = intPtr.GetExport("eh_native_to_managed");
				export4 = intPtr.GetExport("mmch_jit_memcpy");
				export5 = intPtr.GetExport("mmch_jit_hook_config");
				Helpers.Assert(export != IntPtr.Zero, null, "eh_get_exception_ptr != IntPtr.Zero");
				Helpers.Assert(export2 != IntPtr.Zero, null, "eh_managed_to_native != IntPtr.Zero");
				Helpers.Assert(export3 != IntPtr.Zero, null, "eh_native_to_managed != IntPtr.Zero");
				Helpers.Assert(export3 != IntPtr.Zero, null, "eh_native_to_managed != IntPtr.Zero");
				Helpers.Assert(export4 != IntPtr.Zero, null, "mmch_jit_memcpy != IntPtr.Zero");
				Helpers.Assert(export5 != IntPtr.Zero, null, "mmch_jit_hook_config != IntPtr.Zero");
			}
			catch
			{
				DynDll.CloseLibrary(intPtr);
				throw;
			}
			return new JitMemcpyHelper(arch, export, export2, export3, export4, export5);
		}

		public unsafe void JitMemCpy(IntPtr dst, IntPtr src, ulong size)
		{
			((delegate* unmanaged[Cdecl]<IntPtr, IntPtr, ulong, void>)(void*)mmch_jit_memcpy)(dst, src, size);
		}

		internal unsafe IntPtr GetJitHookConfig(int runtimeMajMin)
		{
			return ((delegate* unmanaged[Cdecl]<int, IntPtr>)(void*)mmch_jit_hook_config)(runtimeMajMin);
		}
	}

	private IArchitecture? arch;

	private PosixExceptionHelper? lazyNativeExceptionHelper;

	public OSKind Target => OSKind.OSX;

	public SystemFeature Features { get; }

	public Abi? DefaultAbi { get; }

	public IMemoryAllocator MemoryAllocator { get; } = new QueryingPagedMemoryAllocator(new MacOsQueryingAllocator());

	public INativeExceptionHelper? NativeExceptionHelper => lazyNativeExceptionHelper ?? (lazyNativeExceptionHelper = CreateNativeExceptionHelper());

	private static ReadOnlySpan<byte> NEHTempl => "/tmp/mm-exhelper.dylib.XXXXXX"u8;

	public MacOSSystem()
	{
		switch (PlatformDetection.Architecture)
		{
		case ArchitectureKind.x86_64:
			Features = SystemFeature.RWXPages | SystemFeature.RXPages;
			DefaultAbi = new Abi(new SpecialArgumentKind[3]
			{
				SpecialArgumentKind.ReturnBuffer,
				SpecialArgumentKind.ThisPointer,
				SpecialArgumentKind.UserArguments
			}, SystemVABI.ClassifyAMD64, ReturnsReturnBuffer: true);
			break;
		case ArchitectureKind.Arm64:
			Features = SystemFeature.RXPages | SystemFeature.MayUseNativeJitHooks;
			DefaultAbi = new Abi(new SpecialArgumentKind[2]
			{
				SpecialArgumentKind.ThisPointer,
				SpecialArgumentKind.UserArguments
			}, SystemVABI.ClassifyARM64, ReturnsReturnBuffer: false);
			break;
		default:
			throw new NotImplementedException();
		}
	}

	public unsafe IEnumerable<string?> EnumerateLoadedModuleFiles()
	{
		int count = OSX.task_dyld_info.Count;
		OSX.task_dyld_info task_dyld_info = default(OSX.task_dyld_info);
		if (!OSX.task_info(OSX.mach_task_self(), OSX.task_flavor_t.DyldInfo, &task_dyld_info, &count))
		{
			return ArrayEx.Empty<string>();
		}
		ReadOnlySpan<OSX.dyld_image_info> infoArray = task_dyld_info.all_image_infos->InfoArray;
		string[] array = new string[infoArray.Length];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = infoArray[i].imageFilePath.ToString();
		}
		return array;
	}

	public nint GetSizeOfReadableMemory(IntPtr start, nint guess)
	{
		nint num = 0;
		do
		{
			if (!GetLocalRegionInfo(start, out var startAddr, out var outSize, out var prot, out var _))
			{
				return num;
			}
			if (startAddr > (nint)start)
			{
				return num;
			}
			if ((prot & OSX.vm_prot_t.Read) == 0)
			{
				return num;
			}
			num += startAddr + outSize - (nint)start;
			start = startAddr + outSize;
		}
		while (num < guess);
		return num;
	}

	public unsafe void PatchData(PatchTargetKind targetKind, IntPtr patchTarget, ReadOnlySpan<byte> data, Span<byte> backup)
	{
		int length = data.Length;
		bool isEnabled;
		bool flag;
		bool flag2;
		if (TryGetProtForMem(patchTarget, length, out var _, out var prot, out var crossesAllocBoundary, out var notAllocated))
		{
			if (crossesAllocBoundary)
			{
				_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogWarningStringHandler message = new _003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogWarningStringHandler(101, 2, out isEnabled);
				if (isEnabled)
				{
					message.AppendLiteral("Patch requested for memory which spans multiple memory allocations. Failures may result. (0x");
					message.AppendFormatted(patchTarget, "x16");
					message.AppendLiteral(" length ");
					message.AppendFormatted(length);
					message.AppendLiteral(")");
				}
				_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.Warning(ref message);
			}
			flag = prot.Has(OSX.vm_prot_t.Write);
			flag2 = prot.Has(OSX.vm_prot_t.Execute);
		}
		else
		{
			if (notAllocated)
			{
				_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogErrorStringHandler message2 = new _003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogErrorStringHandler(68, 2, out isEnabled);
				if (isEnabled)
				{
					message2.AppendLiteral("Requested patch of region which was not fully allocated (0x");
					message2.AppendFormatted(patchTarget, "x16");
					message2.AppendLiteral(" length ");
					message2.AppendFormatted(length);
					message2.AppendLiteral(")");
				}
				_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.Error(ref message2);
				throw new InvalidOperationException("Cannot patch unallocated region");
			}
			flag = false;
			flag2 = targetKind == PatchTargetKind.Executable;
		}
		if (!flag)
		{
			Helpers.Assert(!crossesAllocBoundary, null, "!crossesBoundary");
			MakePageWritable(patchTarget);
		}
		Span<byte> destination = new Span<byte>((void*)patchTarget, data.Length);
		destination.TryCopyTo(backup);
		if (NativeExceptionHelper is JitMemcpyHelper jitMemcpyHelper && prot == OSX.vm_prot_t.All)
		{
			_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.Trace("RWX memory detected, doing memcpy for MAP_JIT");
			fixed (byte* ptr = data)
			{
				jitMemcpyHelper.JitMemCpy(patchTarget, (IntPtr)ptr, (ulong)data.Length);
				_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogTraceStringHandler message3 = new _003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogTraceStringHandler(20, 2, out isEnabled);
				if (isEnabled)
				{
					message3.AppendFormatted(data.Length);
					message3.AppendLiteral(" bytes written to 0x");
					message3.AppendFormatted(patchTarget, "X16");
				}
				_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.Trace(ref message3);
			}
		}
		else
		{
			data.CopyTo(destination);
		}
		if (flag2)
		{
			OSX.sys_icache_invalidate((void*)patchTarget, (nuint)data.Length);
		}
	}

	private unsafe static void MakePageWritable(nint addrInPage)
	{
		Helpers.Assert(GetLocalRegionInfo(addrInPage, out IntPtr startAddr, out IntPtr outSize, out OSX.vm_prot_t prot, out OSX.vm_prot_t maxProt), null, "GetLocalRegionInfo(addrInPage, out var allocStart, out var allocSize, out var allocProt, out var allocMaxProt)");
		Helpers.Assert((nint)startAddr <= addrInPage, null, "allocStart <= addrInPage");
		if (prot.Has(OSX.vm_prot_t.Write))
		{
			return;
		}
		int targetTask = OSX.mach_task_self();
		bool isEnabled;
		OSX.kern_return_t kern_return_t;
		if (maxProt.Has(OSX.vm_prot_t.Write))
		{
			kern_return_t = OSX.mach_vm_protect(targetTask, (ulong)(nint)startAddr, (ulong)(nint)outSize, false, prot | OSX.vm_prot_t.Write);
			if ((bool)kern_return_t)
			{
				return;
			}
			_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogErrorStringHandler message = new _003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogErrorStringHandler(60, 6, out isEnabled);
			if (isEnabled)
			{
				message.AppendLiteral("Could not vm_protect page 0x");
				message.AppendFormatted(startAddr, "x16");
				message.AppendLiteral("+0x");
				message.AppendFormatted(outSize, "x");
				message.AppendLiteral(" ");
				message.AppendLiteral("from ");
				message.AppendFormatted(OSX.P(prot));
				message.AppendLiteral(" to ");
				message.AppendFormatted(OSX.P(prot | OSX.vm_prot_t.Write));
				message.AppendLiteral(" (max prot ");
				message.AppendFormatted(OSX.P(maxProt));
				message.AppendLiteral("): kr = ");
				message.AppendFormatted(kern_return_t.Value);
			}
			_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.Error(ref message);
			_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.Error("Trying copy/remap instead...");
		}
		if (!prot.Has(OSX.vm_prot_t.Read))
		{
			if (!maxProt.Has(OSX.vm_prot_t.Read))
			{
				_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogErrorStringHandler message2 = new _003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogErrorStringHandler(66, 3, out isEnabled);
				if (isEnabled)
				{
					message2.AppendLiteral("Requested 0x");
					message2.AppendFormatted(startAddr, "x16");
					message2.AppendLiteral("+0x");
					message2.AppendFormatted(outSize, "x");
					message2.AppendLiteral(" (max: ");
					message2.AppendFormatted(OSX.P(maxProt));
					message2.AppendLiteral(") to be made writable, but its not readable!");
				}
				_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.Error(ref message2);
				throw new NotSupportedException("Cannot make page writable because its not readable");
			}
			kern_return_t = OSX.mach_vm_protect(targetTask, (ulong)(nint)startAddr, (ulong)(nint)outSize, false, prot | OSX.vm_prot_t.Read);
			if (!kern_return_t)
			{
				_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogErrorStringHandler message3 = new _003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogErrorStringHandler(60, 4, out isEnabled);
				if (isEnabled)
				{
					message3.AppendLiteral("vm_protect of 0x");
					message3.AppendFormatted(startAddr, "x16");
					message3.AppendLiteral("+0x");
					message3.AppendFormatted(outSize, "x");
					message3.AppendLiteral(" (max: ");
					message3.AppendFormatted(OSX.P(maxProt));
					message3.AppendLiteral(") to become readable failed: kr = ");
					message3.AppendFormatted(kern_return_t.Value);
				}
				_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.Error(ref message3);
				throw new NotSupportedException("Could not make page readable for remap");
			}
		}
		_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogTraceStringHandler message4 = new _003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogTraceStringHandler(41, 5, out isEnabled);
		if (isEnabled)
		{
			message4.AppendLiteral("Performing page remap on 0x");
			message4.AppendFormatted(startAddr, "x16");
			message4.AppendLiteral("+0x");
			message4.AppendFormatted(outSize, "x");
			message4.AppendLiteral(" from ");
			message4.AppendFormatted(OSX.P(prot));
			message4.AppendLiteral("/");
			message4.AppendFormatted(OSX.P(maxProt));
			message4.AppendLiteral(" to ");
			message4.AppendFormatted(OSX.P(prot | OSX.vm_prot_t.Write));
		}
		_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.Trace(ref message4);
		OSX.vm_prot_t vm_prot_t = prot | OSX.vm_prot_t.Write;
		OSX.vm_prot_t vm_prot_t2 = maxProt | OSX.vm_prot_t.Write;
		ulong num = default(ulong);
		kern_return_t = OSX.mach_vm_map(targetTask, &num, (ulong)(nint)outSize, 0uL, OSX.vm_flags.Anywhere, 0, 0uL, true, vm_prot_t, vm_prot_t2, OSX.vm_inherit_t.Copy);
		if (!kern_return_t)
		{
			_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogErrorStringHandler message5 = new _003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogErrorStringHandler(36, 1, out isEnabled);
			if (isEnabled)
			{
				message5.AppendLiteral("Could not allocate new memory! kr = ");
				message5.AppendFormatted(kern_return_t.Value);
			}
			_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.Error(ref message5);
			throw new OutOfMemoryException();
		}
		try
		{
			new Span<byte>((void*)startAddr, (int)(nint)outSize).CopyTo(new Span<byte>((void*)num, (int)(nint)outSize));
			ulong value = (ulong)(nint)outSize;
			int num2 = default(int);
			kern_return_t = OSX.mach_make_memory_entry_64(targetTask, &value, num, vm_prot_t2, &num2, 0);
			if (!kern_return_t)
			{
				_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogErrorStringHandler message6 = new _003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogErrorStringHandler(79, 4, out isEnabled);
				if (isEnabled)
				{
					message6.AppendLiteral("make_memory_entry(task_self(), size: 0x");
					message6.AppendFormatted(value, "x");
					message6.AppendLiteral(", addr: ");
					message6.AppendFormatted(num, "x16");
					message6.AppendLiteral(", prot: ");
					message6.AppendFormatted(OSX.P(vm_prot_t2));
					message6.AppendLiteral(", &obj, 0) failed: kr = ");
					message6.AppendFormatted(kern_return_t.Value);
				}
				_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.Error(ref message6);
				throw new NotSupportedException("make_memory_entry() failed");
			}
			ulong value2 = (ulong)(nint)startAddr;
			kern_return_t = OSX.mach_vm_map(targetTask, &value2, (ulong)(nint)outSize, 0uL, OSX.vm_flags.Overwrite, num2, 0uL, true, vm_prot_t, vm_prot_t2, OSX.vm_inherit_t.Copy);
			if (!kern_return_t)
			{
				_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogErrorStringHandler message7 = new _003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogErrorStringHandler(78, 10, out isEnabled);
				if (isEnabled)
				{
					message7.AppendLiteral("vm_map() failed to map over target range: 0x");
					message7.AppendFormatted(value2, "x16");
					message7.AppendLiteral("+0x");
					message7.AppendFormatted(outSize, "x");
					message7.AppendLiteral(" (");
					message7.AppendFormatted(OSX.P(prot));
					message7.AppendLiteral("/");
					message7.AppendFormatted(OSX.P(maxProt));
					message7.AppendLiteral(")");
					message7.AppendLiteral(" <- (obj ");
					message7.AppendFormatted(num2);
					message7.AppendLiteral(") 0x");
					message7.AppendFormatted(num, "x16");
					message7.AppendLiteral("+0x");
					message7.AppendFormatted(outSize, "x");
					message7.AppendLiteral(" (");
					message7.AppendFormatted(OSX.P(vm_prot_t));
					message7.AppendLiteral("/");
					message7.AppendFormatted(OSX.P(vm_prot_t2));
					message7.AppendLiteral("), kr = ");
					message7.AppendFormatted(kern_return_t.Value);
				}
				_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.Error(ref message7);
				throw new NotSupportedException("vm_map() failed");
			}
		}
		finally
		{
			kern_return_t = OSX.mach_vm_deallocate(targetTask, num, (ulong)(nint)outSize);
			if (!kern_return_t)
			{
				_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogErrorStringHandler message8 = new _003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogErrorStringHandler(53, 3, out isEnabled);
				if (isEnabled)
				{
					message8.AppendLiteral("Could not deallocate created memory page 0x");
					message8.AppendFormatted(num, "x16");
					message8.AppendLiteral("+0x");
					message8.AppendFormatted(outSize, "x");
					message8.AppendLiteral("! kr = ");
					message8.AppendFormatted(kern_return_t.Value);
				}
				_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.Error(ref message8);
			}
		}
	}

	private static bool TryGetProtForMem(nint addr, int length, out OSX.vm_prot_t maxProt, out OSX.vm_prot_t prot, out bool crossesAllocBoundary, out bool notAllocated)
	{
		maxProt = (OSX.vm_prot_t)(-1);
		prot = (OSX.vm_prot_t)(-1);
		crossesAllocBoundary = false;
		notAllocated = false;
		nint num = addr;
		while (addr < num + length)
		{
			nint startAddr;
			nint outSize;
			OSX.vm_prot_t prot2;
			OSX.vm_prot_t maxProt2;
			OSX.kern_return_t localRegionInfo = GetLocalRegionInfo(addr, out startAddr, out outSize, out prot2, out maxProt2);
			if ((bool)localRegionInfo)
			{
				if (startAddr > addr)
				{
					notAllocated = true;
					return false;
				}
				prot &= prot2;
				maxProt &= maxProt2;
				addr = startAddr + outSize;
				if (addr >= num + length)
				{
					break;
				}
				crossesAllocBoundary = true;
				continue;
			}
			if (localRegionInfo == OSX.kern_return_t.NoSpace)
			{
				notAllocated = true;
				return false;
			}
			return false;
		}
		return true;
	}

	private unsafe static OSX.kern_return_t GetLocalRegionInfo(nint origAddr, out nint startAddr, out nint outSize, out OSX.vm_prot_t prot, out OSX.vm_prot_t maxProt)
	{
		int num = int.MaxValue;
		int count = OSX.vm_region_submap_short_info_64.Count;
		ulong num2 = (ulong)origAddr;
		ulong num3 = default(ulong);
		OSX.vm_region_submap_short_info_64 vm_region_submap_short_info_ = default(OSX.vm_region_submap_short_info_64);
		OSX.kern_return_t kern_return_t = OSX.mach_vm_region_recurse(OSX.mach_task_self(), &num2, &num3, &num, &vm_region_submap_short_info_, &count);
		if (!kern_return_t)
		{
			startAddr = 0;
			outSize = 0;
			prot = OSX.vm_prot_t.None;
			maxProt = OSX.vm_prot_t.None;
			return kern_return_t;
		}
		Helpers.Assert(!vm_region_submap_short_info_.is_submap, null, "!info.is_submap");
		startAddr = (nint)num2;
		outSize = (nint)num3;
		prot = vm_region_submap_short_info_.protection;
		maxProt = vm_region_submap_short_info_.max_protection;
		return kern_return_t;
	}

	void IInitialize<IArchitecture>.Initialize(IArchitecture value)
	{
		arch = value;
	}

	public IntPtr GetNativeJitHookConfig(int runtimeMajMin)
	{
		if (NativeExceptionHelper is JitMemcpyHelper jitMemcpyHelper)
		{
			return jitMemcpyHelper.GetJitHookConfig(runtimeMajMin);
		}
		return IntPtr.Zero;
	}

	private PosixExceptionHelper CreateNativeExceptionHelper()
	{
		Helpers.Assert(arch != null, null, "arch is not null");
		string name = arch.Target switch
		{
			ArchitectureKind.x86_64 => "exhelper_macos_x86_64.dylib", 
			ArchitectureKind.Arm64 => "exhelper_macos_arm64.dylib", 
			_ => throw new NotImplementedException("No exception helper for current arch"), 
		};
		string filename;
		using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
		{
			Helpers.Assert(stream != null, null, "embedded is not null");
			filename = MacOSNativeLibDrop.Instance.DropLibrary(stream, NEHTempl);
		}
		if (arch.Target != ArchitectureKind.Arm64)
		{
			return PosixExceptionHelper.CreateHelper(arch, filename);
		}
		return JitMemcpyHelper.CreateHelper(arch, filename);
	}
}
