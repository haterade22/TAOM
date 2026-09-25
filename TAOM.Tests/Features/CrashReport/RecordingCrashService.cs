using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TAOM.Features.CrashReport;
using TAOM.Features.CrashReport.Hooks;

namespace TAOM.Tests.Features.CrashReport;

// A reachable capture service for the hooks' swallow path. No test calls IoC.Configure, so without
// this the hooks only ever reach their hand-back fallback. Install() puts it in
// CrashReportPatchHelper's private cache; call CrashReportPatchHelper.ResetForUnload() in cleanup.
internal sealed class RecordingCrashService : ICrashReportService
{
    private static readonly FieldInfo CachedService =
        typeof(CrashReportPatchHelper).GetField("_service", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("CrashReportPatchHelper._service not found");

    internal readonly List<(Exception Exception, string Origin, bool OffMainThread)> Calls = new();

    // Runs inside HandleException, to test re-entry.
    internal Action? During;

    internal Exception? ThrowFromHandle;

    public bool IsHandling => false;

    public string? HandleException(Exception exception, string originatingPatchTarget, bool offMainThread = false)
    {
        Calls.Add((exception, originatingPatchTarget, offMainThread));
        During?.Invoke();
        if (ThrowFromHandle != null) throw ThrowFromHandle;
        return null;
    }

    internal static RecordingCrashService Install()
    {
        var service = new RecordingCrashService();
        CachedService.SetValue(null, service);
        return service;
    }
}

// Exception.Data is virtual, and the runtime's preallocated agile exceptions (out of memory, stack
// overflow) return a read-only dictionary from it (mscorlib System.Exception.Data), so a hook
// cannot rely on writing to it.
internal sealed class ReadOnlyDataException : Exception
{
    private readonly IDictionary _data = new ReadOnlyTable();

    internal ReadOnlyDataException() : base("taom-006 read-only Data") { }

    public override IDictionary Data => _data;

    private sealed class ReadOnlyTable : Hashtable
    {
        public override bool IsReadOnly => true;

        public override object? this[object key]
        {
            get => base[key];
            set => throw new NotSupportedException("read-only");
        }

        public override void Add(object key, object? value) => throw new NotSupportedException("read-only");
    }
}
