using System;
using System.Collections.Generic;
using System.Text;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.MissionPerf.AnimMemory;

/// <summary>
/// The address cache behind <see cref="SignatureScanSession"/>. The file is plain text: a version line, a
/// <c>key</c> line naming the DLL it was made for, then one <c>id rva</c> line per signature (hex). The key is the file's
/// size and last-write time plus the PE header fields a rebuilt DLL changes (time stamp, checksum, image and code sizes,
/// entry point) and the code section's place. A file with another key, another version or any line that does not parse is
/// dropped whole, with one log line when it was damaged; the next scan rewrites it. Nothing here throws.
/// </summary>
public sealed class EngineSignatureCache : IEngineSignatureCache
{
    internal const string VersionLine = "taom-engine-signature-cache 1";

    private readonly IEngineSignatureCacheAdapter _adapter;
    private readonly IModLogger _logger;

    public EngineSignatureCache(IEngineSignatureCacheAdapter adapter, IModLogger logger)
    {
        _adapter = adapter;
        _logger = logger;
    }

    public SignatureScanSession Begin(string moduleFileName, byte[] headers, int textRva, int textSize)
    {
        try
        {
            var key = BuildKey(_adapter.ModuleFileStamp(moduleFileName), headers, textRva, textSize);
            if (key == null)
                return SignatureScanSession.Disabled;
            var entries = Load(key);
            return new SignatureScanSession(textRva, entries, saved => Save(key, saved));
        }
        catch (Exception ex)
        {
            Say("could not open the address cache (" + ex.GetType().Name + ": " + ex.Message + "), scanning in full");
            return SignatureScanSession.Disabled;
        }
    }

    internal static string? BuildKey(string? stamp, byte[] headers, int textRva, int textSize)
    {
        if (headers == null || headers.Length < 0x40)
            return null;
        var peAt = BitConverter.ToInt32(headers, 0x3C);
        // COFF header at peAt + 4 (TimeDateStamp +4), optional header at peAt + 24 (SizeOfCode +4, entry point +16, SizeOfImage +56, CheckSum +64).
        if (peAt < 0 || peAt > headers.Length - 24 - 68 || BitConverter.ToUInt32(headers, peAt) != 0x00004550)
            return null;
        var opt = peAt + 24;
        return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}|{1:X}|{2:X}|{3:X}|{4:X}|{5:X}|{6:X}|{7:X}",
            string.IsNullOrWhiteSpace(stamp) ? "nostamp" : stamp,
            BitConverter.ToUInt32(headers, peAt + 8), BitConverter.ToUInt32(headers, opt + 64), BitConverter.ToUInt32(headers, opt + 56),
            BitConverter.ToUInt32(headers, opt + 4), BitConverter.ToUInt32(headers, opt + 16), textRva, textSize);
    }

    private Dictionary<string, int> Load(string key)
    {
        var entries = new Dictionary<string, int>();
        string? text;
        try
        {
            text = _adapter.ReadCache();
        }
        catch (Exception ex)
        {
            Say("the address cache could not be read (" + ex.GetType().Name + ": " + ex.Message + "), scanning in full");
            return entries;
        }
        if (string.IsNullOrWhiteSpace(text))
            return entries;

        var lines = text!.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2 || lines[0] != VersionLine || !lines[1].StartsWith("key ", StringComparison.Ordinal))
        {
            Say("the address cache is not in the expected format, scanning in full");
            return entries;
        }
        if (lines[1].Substring(4) != key)
            return entries;    // made for another DLL build: an expected miss, not damage

        for (var i = 2; i < lines.Length; i++)
        {
            var parts = lines[i].Split(' ');
            if (parts.Length != 2 || parts[0].Length == 0
                || !int.TryParse(parts[1], System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture, out var rva) || rva < 0)
            {
                Say("the address cache has a line that does not parse, scanning in full");
                return new Dictionary<string, int>();
            }
            entries[parts[0]] = rva;
        }
        return entries;
    }

    private void Save(string key, Dictionary<string, int> entries)
    {
        try
        {
            var sb = new StringBuilder();
            sb.Append(VersionLine).Append('\n').Append("key ").Append(key).Append('\n');
            var ids = new List<string>(entries.Keys);
            ids.Sort(StringComparer.Ordinal);
            foreach (var id in ids)
                sb.Append(id).Append(' ').Append(entries[id].ToString("X", System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
            _adapter.WriteCache(sb.ToString());
        }
        catch (Exception ex)
        {
            Say("the address cache could not be written (" + ex.GetType().Name + ": " + ex.Message + ")");
        }
    }

    private void Say(string message)
    {
        try { _logger.LogInfo("[SigCache] " + message); }
        catch { /* diagnostic only */ }
    }
}
