using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace TAOM.Adapters;

/// <summary>
/// Reads and writes <c>engine_signature_cache.txt</c> in Documents\Mount and Blade II Bannerlord\TAOM and stats the
/// loaded module's file. The write goes to a temporary file first and replaces the cache in one step, so a crash
/// mid-write leaves the old cache, not half of a new one.
/// </summary>
public sealed class EngineSignatureCacheAdapter : IEngineSignatureCacheAdapter
{
    private const string FileName = "engine_signature_cache.txt";

    private readonly string _directory;

    public EngineSignatureCacheAdapter()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Mount and Blade II Bannerlord", "TAOM"))
    {
    }

    internal EngineSignatureCacheAdapter(string directory) => _directory = directory;

    internal string FilePath => Path.Combine(_directory, FileName);

    public string? ReadCache() => File.Exists(FilePath) ? File.ReadAllText(FilePath, Encoding.UTF8) : null;

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
        foreach (ProcessModule module in Process.GetCurrentProcess().Modules)
        {
            if (!string.Equals(module.ModuleName, moduleFileName, StringComparison.OrdinalIgnoreCase))
                continue;
            var info = new FileInfo(module.FileName);
            return info.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|"
                + info.LastWriteTimeUtc.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        return null;
    }
}
