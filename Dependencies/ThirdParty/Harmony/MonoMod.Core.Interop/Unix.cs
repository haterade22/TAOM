using System;
using System.Runtime.InteropServices;

namespace MonoMod.Core.Interop;

internal static class Unix
{
	[Flags]
	public enum PipeFlags
	{
		CloseOnExec = 0x80000
	}

	[Flags]
	public enum Protection
	{
		None = 0,
		Read = 1,
		Write = 2,
		Execute = 4
	}

	[Flags]
	public enum MmapFlags
	{
		Shared = 1,
		Private = 2,
		SharedValidate = 3,
		Fixed = 0x10,
		Anonymous = 0x20,
		GrowsDown = 0x100,
		DenyWrite = 0x800,
		[Obsolete("Use Protection.Execute instead", true)]
		Executable = 0x1000,
		Locked = 0x2000,
		NoReserve = 0x4000,
		Populate = 0x8000,
		NonBlock = 0x10000,
		Stack = 0x20000,
		HugeTLB = 0x40000,
		Sync = 0x80000,
		FixedNoReplace = 0x100000
	}

	public enum SysconfName
	{
		ArgMax,
		ChildMax,
		ClockTick,
		NGroupsMax,
		OpenMax,
		StreamMax,
		TZNameMax,
		JobControl,
		SavedIds,
		RealtimeSignals,
		PriorityScheduling,
		Timers,
		AsyncIO,
		PrioritizedIO,
		SynchronizedIO,
		FSync,
		MappedFiles,
		MemLock,
		MemLockRange,
		MemoryProtection,
		MessagePassing,
		Semaphores,
		SharedMemoryObjects,
		AIOListIOMax,
		AIOMax,
		AIOPrioDeltaMax,
		DelayTimerMax,
		MQOpenMax,
		MQPrioMax,
		Version,
		PageSize,
		RTSigMax,
		SemNSemsMax,
		SemValueMax,
		SigQueueMax,
		TimerMax
	}

	public const string LibC = "libc";

	public unsafe static int Errno => *__errno_location();

	[DllImport("libc", CallingConvention = CallingConvention.Cdecl, EntryPoint = "read")]
	public static extern nint Read(int fd, IntPtr buf, nint count);

	[DllImport("libc", CallingConvention = CallingConvention.Cdecl, EntryPoint = "write")]
	public static extern nint Write(int fd, IntPtr buf, nint count);

	[DllImport("libc", CallingConvention = CallingConvention.Cdecl, EntryPoint = "pipe2")]
	public unsafe static extern int Pipe2(int* pipefd, PipeFlags flags);

	[DllImport("libc", CallingConvention = CallingConvention.Cdecl, EntryPoint = "close")]
	public static extern int Close(int fd);

	[DllImport("libc", CallingConvention = CallingConvention.Cdecl, EntryPoint = "mmap")]
	public static extern nint Mmap(IntPtr addr, nuint length, Protection prot, MmapFlags flags, int fd, int offset);

	[DllImport("libc", CallingConvention = CallingConvention.Cdecl, EntryPoint = "munmap")]
	public static extern int Munmap(IntPtr addr, nuint length);

	[DllImport("libc", CallingConvention = CallingConvention.Cdecl, EntryPoint = "mprotect")]
	public static extern int Mprotect(IntPtr addr, nuint len, Protection prot);

	[DllImport("libc", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sysconf")]
	public static extern long Sysconf(SysconfName name);

	[DllImport("libc", CallingConvention = CallingConvention.Cdecl, EntryPoint = "mincore")]
	public unsafe static extern int Mincore(IntPtr addr, nuint len, byte* vec);

	[DllImport("libc", CallingConvention = CallingConvention.Cdecl, EntryPoint = "mkstemp")]
	public unsafe static extern int MkSTemp(byte* template);

	[DllImport("libc", CallingConvention = CallingConvention.Cdecl)]
	public unsafe static extern int* __errno_location();

	static Unix()
	{
		_ = Errno;
	}
}
