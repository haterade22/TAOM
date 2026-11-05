using System;
using System.IO;
using System.Text;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Dependencies.Foundation;

namespace TAOM.Tests.Infrastructure.Dependencies;

/// <summary>
/// <c>DiagLog.TryLog</c>: the same line as <c>Log</c>, plus whether it reached diag.log. PatchShield writes a repeated
/// swallow line once and counts the rest, so it must know when the one write failed (maintainer decision D16).
/// </summary>
[TestClass]
public class DiagLogTests
{
    private static string Since(long offset)
    {
        using var stream = new FileStream(RuntimeLog.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        stream.Seek(offset, SeekOrigin.Begin);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static long Length() => File.Exists(RuntimeLog.Path) ? new FileInfo(RuntimeLog.Path).Length : 0;

    [TestMethod]
    public void TryLog_LogWritable_AppendsTheLineAndReturnsTrue()
    {
        var message = "plan034-trylog-" + Guid.NewGuid().ToString("N");
        long offset = Length();

        var written = DiagLog.TryLog("DiagLogTests", message);

        Assert.IsTrue(written);
        StringAssert.Contains(Since(offset), "[INFO  ] [DiagLogTests] " + message);
    }

    [TestMethod]
    public void TryLog_LogHeldOpenByAnotherHandle_ReturnsFalseWritesNothingAndThrowsNothing()
    {
        // A sharing violation (a viewer or a scanner holding the file) is the transient fault the caller must retry.
        DiagLog.Log("DiagLogTests", "diag.log exists before it is locked");
        var message = "plan034-trylog-locked-" + Guid.NewGuid().ToString("N");
        long offset = Length();
        bool written;
        using (new FileStream(RuntimeLog.Path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            written = DiagLog.TryLog("DiagLogTests", message);
        }

        Assert.IsFalse(written);
        Assert.IsFalse(Since(offset).Contains(message), "the write failed, so the line is not in the log");
        Assert.IsTrue(DiagLog.TryLog("DiagLogTests", message), "the same call succeeds once the log is free");
    }

    [TestMethod]
    public void TryLog_LogPathDidNotResolve_ReturnsFalseAndThrowsNothing()
    {
        // RuntimeLog.Path is empty when the module directory cannot be derived; nothing can be written then.
        var cached = AccessTools.Field(typeof(RuntimeLog), "_path");
        var saved = cached.GetValue(null);
        cached.SetValue(null, string.Empty);
        try
        {
            Assert.IsFalse(DiagLog.TryLog("DiagLogTests", "plan034 no path"));
            DiagLog.Log("DiagLogTests", "plan034 no path");   // the void entry points stay silent as before
        }
        finally
        {
            cached.SetValue(null, saved);
        }
    }
}
