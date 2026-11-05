// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), frame-buffer-guard and frame-buffer-watch.
using System;
using System.Runtime.InteropServices;

namespace TAOM.Adapters;

/// <summary>
/// kernel32 side of the skeleton buffer guard. Reads and writes go through ReadProcessMemory and WriteProcessMemory on
/// the current process, which return false for an unmapped or protected address where a raw pointer copy would raise an
/// access violation (not catchable on .NET Framework). The write path is main-thread only by contract: the service calls
/// it at the first main menu, when no skeleton is being drawn.
/// </summary>
public sealed class SkeletonBufferMemoryAdapter : ISkeletonBufferMemoryAdapter
{
    private const uint MemCommit = 0x1000, MemReserve = 0x2000, MemRelease = 0x8000;
    private const uint PageReadWrite = 0x04, PageExecuteRead = 0x20, PageExecuteReadWrite = 0x40;
    private const long Granularity = 0x10000;
    private const long Step = 0x100000;

    [DllImport("kernel32", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr GetModuleHandleW(string lpModuleName);

    [DllImport("kernel32", ExactSpelling = true)]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32", ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr VirtualAlloc(IntPtr lpAddress, UIntPtr dwSize, uint flAllocationType, uint flProtect);

    [DllImport("kernel32", ExactSpelling = true, SetLastError = true)]
    private static extern bool VirtualFree(IntPtr lpAddress, UIntPtr dwSize, uint dwFreeType);

    [DllImport("kernel32", ExactSpelling = true, SetLastError = true)]
    private static extern bool VirtualProtect(IntPtr lpAddress, UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);

    [DllImport("kernel32", ExactSpelling = true, SetLastError = true)]
    private static extern bool FlushInstructionCache(IntPtr hProcess, IntPtr lpBaseAddress, UIntPtr dwSize);

    [DllImport("kernel32", ExactSpelling = true, SetLastError = true)]
    private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, UIntPtr nSize, out UIntPtr lpNumberOfBytesRead);

    [DllImport("kernel32", EntryPoint = "ReadProcessMemory", ExactSpelling = true, SetLastError = true)]
    private static extern bool ReadProcessMemoryInt32(IntPtr hProcess, IntPtr lpBaseAddress, out int lpBuffer, UIntPtr nSize, out UIntPtr lpNumberOfBytesRead);

    [DllImport("kernel32", EntryPoint = "ReadProcessMemory", ExactSpelling = true, SetLastError = true)]
    private static extern bool ReadProcessMemoryInt64(IntPtr hProcess, IntPtr lpBaseAddress, out long lpBuffer, UIntPtr nSize, out UIntPtr lpNumberOfBytesRead);

    [DllImport("kernel32", ExactSpelling = true, SetLastError = true)]
    private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, UIntPtr nSize, out UIntPtr lpNumberOfBytesWritten);

    public long GetModuleBase(string moduleFileName) => GetModuleHandleW(moduleFileName).ToInt64();

    public byte[]? Read(long address, int count)
    {
        if (count <= 0) return null;
        var buffer = new byte[count];
        return ReadProcessMemory(GetCurrentProcess(), new IntPtr(address), buffer, (UIntPtr)(uint)count, out var read)
            && read.ToUInt64() == (ulong)count
            ? buffer
            : null;
    }

    public int? ReadInt32(long address) =>
        ReadProcessMemoryInt32(GetCurrentProcess(), new IntPtr(address), out var value, (UIntPtr)4u, out var read) && read.ToUInt32() == 4
            ? value
            : (int?)null;

    public long? ReadInt64(long address) =>
        ReadProcessMemoryInt64(GetCurrentProcess(), new IntPtr(address), out var value, (UIntPtr)8u, out var read) && read.ToUInt32() == 8
            ? value
            : (long?)null;

    public long AllocateNear(long highest, long lowest, int bytes)
    {
        if (bytes <= 0 || highest < lowest) return 0;
        for (var address = highest & ~(Granularity - 1); address >= lowest && address >= Granularity; address -= Step)
        {
            var got = VirtualAlloc(new IntPtr(address), (UIntPtr)(uint)bytes, MemCommit | MemReserve, PageReadWrite);
            if (got != IntPtr.Zero)
                return got.ToInt64();
        }
        return 0;
    }

    public bool Free(long address) => VirtualFree(new IntPtr(address), UIntPtr.Zero, MemRelease);

    public bool Write(long address, byte[] bytes) =>
        WriteProcessMemory(GetCurrentProcess(), new IntPtr(address), bytes, (UIntPtr)(uint)bytes.Length, out var written)
        && written.ToUInt64() == (ulong)bytes.Length;

    public bool MakeExecutable(long address, int bytes)
    {
        var at = new IntPtr(address);
        var size = (UIntPtr)(uint)bytes;
        return VirtualProtect(at, size, PageExecuteRead, out _) && FlushInstructionCache(GetCurrentProcess(), at, size);
    }

    public EngineCodePatchResult PatchCode(long address, byte[] expected, byte[] replacement)
    {
        if (expected.Length != replacement.Length || expected.Length == 0) return EngineCodePatchResult.NotWritten;
        if (!Matches(address, expected)) return EngineCodePatchResult.NotWritten;

        var at = new IntPtr(address);
        var size = (UIntPtr)(uint)expected.Length;
        if (!VirtualProtect(at, size, PageExecuteReadWrite, out var previous)) return EngineCodePatchResult.NotWritten;
        try
        {
            Write(address, replacement);
            FlushInstructionCache(GetCurrentProcess(), at, size);
            if (Matches(address, replacement)) return EngineCodePatchResult.Written;

            // A partial write: put the engine's bytes back and say whether that held.
            Write(address, expected);
            FlushInstructionCache(GetCurrentProcess(), at, size);
            return Matches(address, expected) ? EngineCodePatchResult.NotWritten : EngineCodePatchResult.Unknown;
        }
        catch
        {
            return Matches(address, expected) ? EngineCodePatchResult.NotWritten : EngineCodePatchResult.Unknown;
        }
        finally
        {
            VirtualProtect(at, size, previous, out _);
            FlushInstructionCache(GetCurrentProcess(), at, size);
        }
    }

    private bool Matches(long address, byte[] bytes)
    {
        var actual = Read(address, bytes.Length);
        if (actual == null) return false;
        for (var i = 0; i < bytes.Length; i++)
            if (actual[i] != bytes[i]) return false;
        return true;
    }
}
