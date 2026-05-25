using System;
using System.Collections.Generic;
using System.Diagnostics;
using TAOM.Features.CrashReport.Domain;

namespace TAOM.Features.CrashReport.Collectors;

public static class StackFrameSnapshotBuilder
{
    public static IReadOnlyList<StackFrameSnapshot> FromException(Exception? ex)
    {
        if (ex == null) return Array.Empty<StackFrameSnapshot>();
        try
        {
            var st = new StackTrace(ex, fNeedFileInfo: true);
            return Build(st);
        }
        catch { return Array.Empty<StackFrameSnapshot>(); }
    }

    public static IReadOnlyList<StackFrameSnapshot> FromCurrent(int skipFrames = 1)
    {
        try
        {
            var st = new StackTrace(skipFrames, fNeedFileInfo: true);
            return Build(st);
        }
        catch { return Array.Empty<StackFrameSnapshot>(); }
    }

    private static IReadOnlyList<StackFrameSnapshot> Build(StackTrace st)
    {
        var frames = st.GetFrames();
        if (frames == null || frames.Length == 0) return Array.Empty<StackFrameSnapshot>();
        var result = new List<StackFrameSnapshot>(frames.Length);
        for (int i = 0; i < frames.Length; i++)
        {
            var f = frames[i];
            string? methodFull = null;
            string? declType = null;
            string? declAsm = null;
            try
            {
                var m = f.GetMethod();
                if (m != null)
                {
                    declType = m.DeclaringType?.FullName;
                    declAsm = m.DeclaringType?.Assembly?.GetName()?.Name;
                    methodFull = (declType ?? "?") + "." + m.Name;
                }
            }
            catch { }
            result.Add(new StackFrameSnapshot(
                Index: i,
                MethodFullName: methodFull,
                DeclaringTypeFullName: declType,
                DeclaringAssemblyName: declAsm,
                FileName: f.GetFileName(),
                FileLine: f.GetFileLineNumber(),
                IlOffset: f.GetILOffset()));
        }
        return result;
    }
}
