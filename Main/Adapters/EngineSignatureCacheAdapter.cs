using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace TAOM.Adapters;

/// <summary>
/// Reads and writes <c>engine_signature_cache.txt</c> in Documents\Mount and Blade II Bannerlord\TAOM and stats the
/// loaded module's file. The write goes to a temporary file first and replaces the cache in one step, so a crash
/// mid-write leaves the old cache, not half of a new one. The module's file path comes from the loader
/// (<c>GetModuleHandleW</c>, <c>GetModuleFileNameW</c>), the same lookup <c>NativeModuleMemoryAdapter</c> uses for the base
/// address, not from a walk of every module in the process.
/// </summary>
public sealed class EngineSignatureCacheAdapter : IEngineSignatureCacheAdapter
{
    private const string FileName = "engine_signature_cache.txt";
    private const int MaxPath = 32768;

    [DllImport("kernel32", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr GetModuleHandleW(string lpModuleName);

    [DllImport("kernel32", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern uint GetModuleFileNameW(IntPtr hModule, StringBuilder lpFilename, uint nSize);

    private readonly string _directory;

    public EngineSignatureCacheAdapter()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Mount and Blade II Bannerlord", "TAOM"))
    {
    }

    internal EngineSignatureCacheAdapter(string directory) => _directory = directory;

    internal string FilePath => Path.Combine(_directory, FileName);

    public string? ReadCache(int maxChars)
    {
        if (!File.Exists(FilePath))
            return null;
        using var reader = new StreamReader(FilePath, Encoding.UTF8);
        var buffer = new char[maxChars];
        var count = reader.ReadBlock(buffer, 0, maxChars);
        return new string(buffer, 0, count);
    }

    public void WriteCache(string text)
    {
        Directory.CreateDirectory(_directory);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, text, new UTF8Encoding(false));
        if (File.Exists(FilePath))
            File.Replace(temp, FilePath, null);
        else
            File.Move(temp, FilePath);
    }

    public string? ModuleFileStamp(string moduleFileName)
    {
        var handle = GetModuleHandleW(moduleFileName);
        if (handle == IntPtr.Zero)
            return null;
        var path = new StringBuilder(MaxPath);
        if (GetModuleFileNameW(handle, path, MaxPath) == 0)
            return null;
        var info = new FileInfo(path.ToString());
        return info.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|"
            + info.LastWriteTimeUtc.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
