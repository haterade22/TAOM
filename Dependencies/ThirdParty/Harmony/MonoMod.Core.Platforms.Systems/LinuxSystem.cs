using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using MonoMod.Core.Interop;
using MonoMod.Core.Platforms.Memory;
using MonoMod.Core.Utils;
using MonoMod.Utils;

namespace MonoMod.Core.Platforms.Systems;

internal sealed class LinuxSystem : ISystem, IInitialize<IArchitecture>
{
	private sealed class MmapPagedMemoryAllocator : PagedMemoryAllocator
	{
		private sealed class SyscallNotImplementedException : Exception
		{
		}

		private static int PageProbePipeReadFD;

		private static int PageProbePipeWriteFD;

		private bool canTestPageAllocation = true;

		public MmapPagedMemoryAllocator(nint pageSize)
			: base(pageSize)
		{
		}

		unsafe static MmapPagedMemoryAllocator()
		{
			byte* num = stackalloc byte[8];
			if (Unix.Pipe2((int*)num, Unix.PipeFlags.CloseOnExec) == -1)
			{
				throw new Win32Exception(Unix.Errno, "Failed to create pipe for page probes");
			}
			PageProbePipeReadFD = *(int*)num;
			PageProbePipeWriteFD = ((int*)num)[1];
		}

		public unsafe static bool PageAllocated(nint page)
		{
			byte b = default(byte);
			if (Unix.Mincore(page, 1u, &b) == -1)
			{
				int errno = Unix.Errno;
				return errno switch
				{
					12 => false, 
					38 => throw new SyscallNotImplementedException(), 
					_ => throw new NotImplementedException($"Got unimplemented errno for mincore(2); errno = {errno}"), 
				};
			}
			return true;
		}

		public unsafe static bool PageReadable(nint page)
		{
			if (Unix.Write(PageProbePipeWriteFD, page, 1) == -1)
			{
				int errno = Unix.Errno;
				if (errno == 14)
				{
					return false;
				}
				throw new NotImplementedException($"Got unimplemented errno for write(2); errno = {errno}");
			}
			byte b = default(byte);
			if (Unix.Read(PageProbePipeReadFD, new IntPtr(&b), 1) == -1)
			{
				throw new Win32Exception("Failed to clean up page probe pipe after successful page probe");
			}
			return true;
		}

		protected override bool TryAllocateNewPage(AllocationRequest request, [_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMaybeNullWhen(false)] out IAllocatedMemory allocated)
		{
			Unix.Protection protection = (request.Executable ? Unix.Protection.Execute : Unix.Protection.None);
			protection |= Unix.Protection.Read | Unix.Protection.Write;
			nint num = Unix.Mmap(IntPtr.Zero, (nuint)base.PageSize, protection, Unix.MmapFlags.Private | Unix.MmapFlags.Anonymous, -1, 0);
			long num2 = num;
			bool isEnabled = (((ulong)(num2 - -1) <= 1uL) ? true : false);
			if (isEnabled)
			{
				int errno = Unix.Errno;
				_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogErrorStringHandler message = new _003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogErrorStringHandler(28, 2, out isEnabled);
				if (isEnabled)
				{
					message.AppendLiteral("Error creating allocation: ");
					message.AppendFormatted(errno);
					message.AppendLiteral(" ");
					message.AppendFormatted(new Win32Exception(errno).Message);
				}
				_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.Error(ref message);
				allocated = null;
				return false;
			}
			Page page = new Page(this, num, (uint)base.PageSize, request.Executable);
			InsertAllocatedPage(page);
			if (!page.TryAllocate((uint)request.Size, (uint)request.Alignment, out PageAllocation alloc))
			{
				RegisterForCleanup(page);
				allocated = null;
				return false;
			}
			allocated = alloc;
			return true;
		}

		protected override bool TryAllocateNewPage(PositionedAllocationRequest request, nint targetPage, nint lowPageBound, nint highPageBound, [_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMaybeNullWhen(false)] out IAllocatedMemory allocated)
		{
			if (!canTestPageAllocation)
			{
				allocated = null;
				return false;
			}
			Unix.Protection protection = (request.Base.Executable ? Unix.Protection.Execute : Unix.Protection.None);
			protection |= Unix.Protection.Read | Unix.Protection.Write;
			nint num = request.Base.Size / base.PageSize + 1;
			nint num2 = targetPage - base.PageSize;
			nint num3 = targetPage;
			nint num4 = -1;
			try
			{
				while (num2 >= lowPageBound || num3 <= highPageBound)
				{
					if (num3 <= highPageBound)
					{
						nint num5 = 0;
						while (true)
						{
							if (num5 < num)
							{
								if (PageAllocated(num3 + base.PageSize * num5))
								{
									num3 += base.PageSize;
									goto IL_008e;
								}
								num5++;
								continue;
							}
							num4 = num3;
							break;
						}
						break;
					}
					goto IL_008e;
					IL_008e:
					if (num2 < lowPageBound)
					{
						continue;
					}
					nint num6 = 0;
					while (true)
					{
						if (num6 < num)
						{
							if (PageAllocated(num2 + base.PageSize * num6))
							{
								num2 -= base.PageSize;
								goto IL_00c5;
							}
							num6++;
							continue;
						}
						num4 = num2;
						break;
					}
					break;
					IL_00c5:;
				}
			}
			catch (SyscallNotImplementedException)
			{
				canTestPageAllocation = false;
				allocated = null;
				return false;
			}
			if (num4 == -1)
			{
				allocated = null;
				return false;
			}
			nint num7 = Unix.Mmap(num4, (nuint)base.PageSize, protection, Unix.MmapFlags.Private | Unix.MmapFlags.Anonymous | Unix.MmapFlags.FixedNoReplace, -1, 0);
			long num8 = num7;
			if (((ulong)(num8 - -1) <= 1uL) ? true : false)
			{
				allocated = null;
				return false;
			}
			Page page = new Page(this, num7, (uint)base.PageSize, request.Base.Executable);
			InsertAllocatedPage(page);
			if (!page.TryAllocate((uint)request.Base.Size, (uint)request.Base.Alignment, out PageAllocation alloc))
			{
				RegisterForCleanup(page);
				allocated = null;
				return false;
			}
			if ((nint)alloc.BaseAddress < (nint)request.LowBound || (nint)alloc.BaseAddress + alloc.Size >= (nint)request.HighBound)
			{
				alloc.Dispose();
				allocated = null;
				return false;
			}
			allocated = alloc;
			return true;
		}

		protected override bool TryFreePage(Page page, [_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003ENotNullWhen(false)] out string? errorMsg)
		{
			if (Unix.Munmap(page.BaseAddr, page.Size) != 0)
			{
				errorMsg = new Win32Exception(Unix.Errno).Message;
				return false;
			}
			errorMsg = null;
			return true;
		}
	}

	private sealed class LinuxNativeLibDrop : PosixNativeLibraryDrop
	{
		public static readonly LinuxNativeLibDrop Instance = new LinuxNativeLibDrop();

		protected override void CloseFileDescriptor(nint fd)
		{
			Unix.Close((int)fd);
		}

		protected unsafe override nint Mkstemp(Span<byte> template)
		{
			int num;
			fixed (byte* template2 = template)
			{
				num = Unix.MkSTemp(template2);
			}
			if (num == -1)
			{
				int errno = Unix.Errno;
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

	private readonly Abi defaultAbi;

	private readonly nint PageSize;

	private readonly MmapPagedMemoryAllocator allocator;

	private IArchitecture? arch;

	private PosixExceptionHelper? lazyNativeExceptionHelper;

	public OSKind Target => OSKind.Linux;

	public SystemFeature Features => SystemFeature.RWXPages | SystemFeature.RXPages;

	public Abi? DefaultAbi => defaultAbi;

	public IMemoryAllocator MemoryAllocator => allocator;

	public INativeExceptionHelper? NativeExceptionHelper => lazyNativeExceptionHelper ?? (lazyNativeExceptionHelper = CreateNativeExceptionHelper());

	private static ReadOnlySpan<byte> NEHTempl => "/tmp/mm-exhelper.so.XXXXXX"u8;

	public IEnumerable<string?> EnumerateLoadedModuleFiles()
	{
		return from ProcessModule m in Process.GetCurrentProcess().Modules
			select m.FileName;
	}

	public LinuxSystem()
	{
		PageSize = (nint)Unix.Sysconf(Unix.SysconfName.PageSize);
		allocator = new MmapPagedMemoryAllocator(PageSize);
		switch (PlatformDetection.Architecture)
		{
		case ArchitectureKind.x86_64:
			defaultAbi = new Abi(new SpecialArgumentKind[3]
			{
				SpecialArgumentKind.ReturnBuffer,
				SpecialArgumentKind.ThisPointer,
				SpecialArgumentKind.UserArguments
			}, SystemVABI.ClassifyAMD64, ReturnsReturnBuffer: true);
			break;
		case ArchitectureKind.Arm64:
			defaultAbi = new Abi(new SpecialArgumentKind[2]
			{
				SpecialArgumentKind.ThisPointer,
				SpecialArgumentKind.UserArguments
			}, SystemVABI.ClassifyARM64, ReturnsReturnBuffer: false);
			break;
		default:
			throw new NotImplementedException();
		}
	}

	public nint GetSizeOfReadableMemory(IntPtr start, nint guess)
	{
		nint num = allocator.RoundDownToPageBoundary(start);
		if (!MmapPagedMemoryAllocator.PageReadable(num))
		{
			return 0;
		}
		num += PageSize;
		nint num2 = num - (nint)start;
		while (num2 < guess)
		{
			if (!MmapPagedMemoryAllocator.PageReadable(num))
			{
				return num2;
			}
			num2 += PageSize;
			num += PageSize;
		}
		return num2;
	}

	public unsafe void PatchData(PatchTargetKind patchKind, IntPtr patchTarget, ReadOnlySpan<byte> data, Span<byte> backup)
	{
		if (patchKind == PatchTargetKind.Executable)
		{
			ProtectRWX(patchTarget, data.Length);
		}
		else
		{
			ProtectRW(patchTarget, data.Length);
		}
		Span<byte> destination = new Span<byte>((void*)patchTarget, data.Length);
		destination.TryCopyTo(backup);
		data.CopyTo(destination);
	}

	private void RoundToPageBoundary(ref nint addr, ref nint size)
	{
		nint num = allocator.RoundDownToPageBoundary(addr);
		size += addr - num;
		addr = num;
	}

	private void ProtectRW(IntPtr addr, nint size)
	{
		RoundToPageBoundary(ref addr, ref size);
		if (Unix.Mprotect(addr, (nuint)size, Unix.Protection.Read | Unix.Protection.Write) != 0)
		{
			throw new Win32Exception(Unix.Errno);
		}
	}

	private void ProtectRWX(IntPtr addr, nint size)
	{
		RoundToPageBoundary(ref addr, ref size);
		if (Unix.Mprotect(addr, (nuint)size, Unix.Protection.Read | Unix.Protection.Write | Unix.Protection.Execute) != 0)
		{
			throw new Win32Exception(Unix.Errno);
		}
	}

	void IInitialize<IArchitecture>.Initialize(IArchitecture value)
	{
		arch = value;
	}

	private PosixExceptionHelper CreateNativeExceptionHelper()
	{
		Helpers.Assert(arch != null, null, "arch is not null");
		string name = arch.Target switch
		{
			ArchitectureKind.x86_64 => "exhelper_linux_x86_64.so", 
			ArchitectureKind.Arm64 => "exhelper_linux_arm64.so", 
			_ => throw new NotImplementedException("No exception helper for current arch"), 
		};
		string filename;
		using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
		{
			Helpers.Assert(stream != null, null, "embedded is not null");
			filename = LinuxNativeLibDrop.Instance.DropLibrary(stream, NEHTempl);
		}
		return PosixExceptionHelper.CreateHelper(arch, filename);
	}

	public IntPtr GetNativeJitHookConfig(int runtimeMajMin)
	{
		throw new NotImplementedException();
	}
}
