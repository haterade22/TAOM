using System;
using System.Runtime.InteropServices;

namespace TAOM.Adapters;

/// <summary>
/// Reads a loaded native module's memory through kernel32 and <see cref="Marshal"/>. It has no write
/// method of any kind. <c>GetModuleHandleW</c> takes no reference on the module, so nothing is freed.
/// </summary>
public sealed class NativeModuleMemoryAdapter : INativeModuleMemoryAdapter
{
    [DllImport("kernel32", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr GetModuleHandleW(string lpModuleName);

    public long GetModuleBase(string moduleFileName) => GetModuleHandleW(moduleFileName).ToInt64();

    public byte[] Copy(long address, int count)
    {
        var buffer = new byte[count];
        Marshal.Copy(new IntPtr(address), buffer, 0, count);
        return buffer;
    }

    public int ReadInt32(long address) => Marshal.ReadInt32(new IntPtr(address));
}
