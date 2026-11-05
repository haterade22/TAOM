using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// #1019 item 1: the join seeded from inside a settlement the party stands in (a raid reaching the soldier's
/// village) leaves one INFO line, so the owed in-game run has something to read. Source pins, comment-stripped.
/// </summary>
[TestClass]
public class InsideSettlementJoinDiagnosticTests
{
    private static string SeedBody()
    {
        var src = RepoPaths.ReadSource("Main/Adapters/EncounterAdapter.cs", stripComments: true);
        var start = src.IndexOf("public bool EnsureEncounterAgainst", System.StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "EnsureEncounterAgainst not found.");
        var end = src.IndexOf("public bool JoinBattle", start, System.StringComparison.Ordinal);
        Assert.IsTrue(end > start, "JoinBattle not found after EnsureEncounterAgainst.");
        return src.Substring(start, end - start);
    }

    [TestMethod]
    public void TheDiagnosticLine_ReadsTheSettlementAndLogsBeforeTheEncounterIsRestarted()
    {
        var body = SeedBody();
        var read = body.IndexOf("CurrentSettlement == place", System.StringComparison.Ordinal);
        var log = body.IndexOf("[EnlistDiag] seeding the join from inside", System.StringComparison.Ordinal);
        var restart = body.IndexOf("RestartPlayerEncounter(", System.StringComparison.Ordinal);

        Assert.IsTrue(read >= 0, "The CurrentSettlement == place read is missing.");
        Assert.IsTrue(log > read, "The log must follow the read.");
        Assert.IsTrue(restart > log, "The log must come before RestartPlayerEncounter.");
        StringAssert.Contains(body, "sameFactionAsSettlement={main.MapFaction == place.MapFaction}");
    }

    [TestMethod]
    public void TheDiagnosticLine_IsNotBehindTheDiagnosticsToggle()
    {
        var body = SeedBody();
        var log = body.IndexOf("[EnlistDiag] seeding the join from inside", System.StringComparison.Ordinal);
        var guard = body.LastIndexOf("if (", log, System.StringComparison.Ordinal);

        var condition = body.Substring(guard, log - guard);
        StringAssert.Contains(condition, "place != null && main?.CurrentSettlement == place");
        StringAssert.DoesNotMatch(condition, new System.Text.RegularExpressions.Regex("Trace|Diagnostic|Enabled|Settings"));
    }
}
