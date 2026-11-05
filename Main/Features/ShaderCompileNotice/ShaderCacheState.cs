using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using TAOM.Core.Logging;

namespace TAOM.Features.ShaderCompileNotice;

/// <summary>
/// One <c>[ShaderCache]</c> line at startup answering "was this session cold from the start?". The
/// v1.4.8 native (D3 disassembly) deletes its runtime cache and rewrites <c>shader_mapping.bin</c> when
/// the module set or the game build changed, so a mapping written after this process started means
/// the cache was dropped or first built at this start. A file stat only; no engine call.
/// </summary>
public static class ShaderCacheState
{
    private const string Prefix = "[ShaderCache] CoreShaders/D3D11 ";
    private const string Stamp = "yyyy-MM-ddTHH:mm:ssZ";

    public static string Describe(bool folderExists, long? mappingBytes, DateTime? mappingWrittenUtc, int sacxCount, DateTime processStartUtc)
    {
        if (!folderExists) return Prefix + "folder not found";
        if (mappingBytes == null || mappingWrittenUtc == null)
            return Prefix + "rewrittenThisStart=unknown mapping=absent sacx=" + sacxCount.ToString(CultureInfo.InvariantCulture);

        string rewritten = mappingWrittenUtc.Value >= processStartUtc ? "true" : "false";
        return Prefix
            + "rewrittenThisStart=" + rewritten
            + " mappingBytes=" + mappingBytes.Value.ToString(CultureInfo.InvariantCulture)
            + " sacx=" + sacxCount.ToString(CultureInfo.InvariantCulture)
            + " mappingWrittenUtc=" + mappingWrittenUtc.Value.ToString(Stamp, CultureInfo.InvariantCulture)
            + " processStartUtc=" + processStartUtc.ToString(Stamp, CultureInfo.InvariantCulture);
    }

    public static void Log(IModLogger logger)
    {
        try
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Mount and Blade II Bannerlord", "Shaders", "CoreShaders", "D3D11");
            DateTime start = Process.GetCurrentProcess().StartTime.ToUniversalTime();
            if (!Directory.Exists(folder))
            {
                logger.LogInfo(Describe(false, null, null, 0, start));
                return;
            }

            var mapping = new FileInfo(Path.Combine(folder, "shader_mapping.bin"));
            int sacx = Directory.GetFiles(folder, "*.sacx").Length;
            logger.LogInfo(mapping.Exists
                ? Describe(true, mapping.Length, mapping.LastWriteTimeUtc, sacx, start)
                : Describe(true, null, null, sacx, start));
        }
        catch (Exception ex)
        {
            logger.LogWarning("[ShaderCache] state not read: " + ex.GetType().Name + ": " + ex.Message);
        }
    }
}
