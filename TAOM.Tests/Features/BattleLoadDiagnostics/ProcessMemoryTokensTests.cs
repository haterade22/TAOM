using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BattleLoadDiagnostics;

namespace TAOM.Tests.Features.BattleLoadDiagnostics;

// The process-memory token tail shared by [BattleLoad] and [SaveLoad] phase lines. Extracted from
// BattleLoadDiagnosticsService.MemStats on 2026-09-12 so the campaign-launch phases carry the same
// vocabulary; these pin the shape so the extraction cannot drift from what the triage tooling and
// the BattleLoad service tests already expect (gc=, heapMB=, privMB=, wsMB=).
[TestClass]
public class ProcessMemoryTokensTests
{
    private static readonly Regex Shape = new Regex(
        @"^gc=\d+/\d+/\d+ heapMB=\d+ privMB=\d+ wsMB=\d+$", RegexOptions.Compiled);

    [TestMethod]
    public void Format_OnTheTestHost_ProducesTheFourTokensInOrder()
    {
        var tokens = ProcessMemoryTokens.Format();

        Assert.IsTrue(Shape.IsMatch(tokens), $"unexpected token shape: '{tokens}'");
    }

    [TestMethod]
    public void Format_HeapToken_IsNonNegativeMegabytes()
    {
        var tokens = ProcessMemoryTokens.Format();

        var heap = Regex.Match(tokens, @"heapMB=(\d+)").Groups[1].Value;
        Assert.IsTrue(long.Parse(heap) >= 0);
    }

    [TestMethod]
    public void Format_ProcessTokens_ArePositiveOnALiveProcess()
    {
        var tokens = ProcessMemoryTokens.Format();

        var priv = long.Parse(Regex.Match(tokens, @"privMB=(\d+)").Groups[1].Value);
        var ws = long.Parse(Regex.Match(tokens, @"wsMB=(\d+)").Groups[1].Value);
        Assert.IsTrue(priv > 0, $"a live test host has private bytes: {tokens}");
        Assert.IsTrue(ws > 0, $"a live test host has a working set: {tokens}");
    }
}
