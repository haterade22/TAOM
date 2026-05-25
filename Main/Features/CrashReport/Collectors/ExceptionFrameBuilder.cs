using System;
using System.Collections;
using System.Collections.Generic;
using TAOM.Features.CrashReport.Domain;

namespace TAOM.Features.CrashReport.Collectors;

// Walks a System.Exception into ExceptionFrame DTOs with the inner-chain depth
// capped (10 by default). All field reads are defensive — some custom exception
// types throw inside their own getters.
public static class ExceptionFrameBuilder
{
    public const int DefaultMaxDepth = 10;

    public static ExceptionFrame? Build(Exception? ex, int maxDepth = DefaultMaxDepth)
        => ex == null ? null : BuildRec(ex, depth: 0, maxDepth);

    private static ExceptionFrame BuildRec(Exception ex, int depth, int maxDepth)
    {
        string type = SafeReadString(() => ex.GetType().FullName) ?? "(unknown type)";
        string message = SafeReadString(() => ex.Message) ?? "(no message)";
        string? source = SafeReadString(() => ex.Source);
        int hresult = 0;
        try { hresult = ex.HResult; } catch { }
        string? targetSite = SafeReadString(() =>
        {
            var ts = ex.TargetSite;
            if (ts == null) return null;
            var decl = ts.DeclaringType?.FullName ?? "?";
            return $"{decl}.{ts.Name}";
        });
        string? stackTrace = SafeReadString(() => ex.StackTrace);

        var data = ReadDataDictionary(ex);

        ExceptionFrame? inner = null;
        if (depth + 1 < maxDepth && ex.InnerException != null)
            inner = BuildRec(ex.InnerException, depth + 1, maxDepth);

        return new ExceptionFrame(
            Type: type,
            Message: message,
            Source: source,
            HResult: hresult,
            TargetSiteFullName: targetSite,
            StackTrace: stackTrace,
            Data: data,
            InnerException: inner);
    }

    private static IReadOnlyList<DataEntry> ReadDataDictionary(Exception ex)
    {
        IDictionary? data;
        try { data = ex.Data; } catch { return Array.Empty<DataEntry>(); }
        if (data == null || data.Count == 0) return Array.Empty<DataEntry>();

        var list = new List<DataEntry>(data.Count);
        try
        {
            foreach (DictionaryEntry kvp in data)
            {
                string key = SafeReadString(() => kvp.Key?.ToString()) ?? "(null-key)";
                string value = SafeReadString(() => kvp.Value?.ToString()) ?? "(null-value)";
                list.Add(new DataEntry(key, value));
            }
        }
        catch
        {
            // ex.Data enumeration itself may throw on exotic implementations
        }
        return list;
    }

    private static string? SafeReadString(Func<string?> read)
    {
        try { return read(); } catch (Exception e) { return $"(read failed: {e.GetType().Name})"; }
    }
}
