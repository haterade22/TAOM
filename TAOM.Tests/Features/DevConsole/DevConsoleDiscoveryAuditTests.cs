using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.DevConsole;

namespace TAOM.Tests.Features.DevConsole;

/// <summary>
/// Pins the four verdicts of the startup discovery audit.
///
/// The audit exists because TAOM cannot prove offline that the engine's
/// <c>CollectCommandLineFunctions</c> pass ever reaches the TAOM assembly — the call site lives in
/// TaleWorlds.Native.dll and is not decompilable
/// (<c>docs/reviews/rca-specialresources-cheat-2026-07-30.md</c>). It also cannot be settled by
/// disassembly in a way that generalises, because module load order is data-dependent.
///
/// The vanilla control command is what makes the reading decisive. Without it, "TAOM command not
/// registered" is ambiguous between "discovery has not run yet at this lifecycle point" and
/// "discovery ran and dropped our command" — and those need opposite responses. Testing the
/// formatter directly is the RCA lesson from the first console command: when a routine returns a
/// report as well as performing a check, test the report.
/// </summary>
[TestClass]
public class DevConsoleDiscoveryAuditTests
{
    private static readonly IReadOnlyList<string> TwoCommands =
        new[] { "taom.add_special_resources", "taom.print_patches" };

    private static readonly IReadOnlyList<string> None = new string[0];

    [TestMethod]
    public void BuildVerdict_ControlFoundAndAllCommandsRegistered_IsProvenAndConclusive()
    {
        // Act
        var verdict = DevConsoleDiscoveryAudit.BuildVerdict(controlFound: true, TwoCommands, missing: None);

        // Assert
        Assert.AreEqual(DevConsoleDiscoveryAudit.Severity.Info, verdict.Level);
        Assert.IsTrue(verdict.IsConclusive, "A found control settles the question; the audit need not re-run.");
        StringAssert.Contains(verdict.Message, "PROVEN");
        StringAssert.Contains(verdict.Message, "2");
    }

    /// <summary>
    /// The genuinely alarming quadrant: discovery ran (control present) but a TAOM command is absent.
    /// That means a duplicate-name drop or a partial abort — a real bug, and the only way to see it
    /// on a player's machine.
    /// </summary>
    [TestMethod]
    public void BuildVerdict_ControlFoundButCommandMissing_IsErrorAndNamesTheCommand()
    {
        var verdict = DevConsoleDiscoveryAudit.BuildVerdict(
            controlFound: true, TwoCommands, missing: new[] { "taom.print_patches" });

        Assert.AreEqual(DevConsoleDiscoveryAudit.Severity.Error, verdict.Level);
        Assert.IsTrue(verdict.IsConclusive);
        StringAssert.Contains(verdict.Message, "taom.print_patches");
    }

    [TestMethod]
    public void BuildVerdict_ControlMissingAndAllCommandsMissing_IsInconclusiveAndRetryable()
    {
        var verdict = DevConsoleDiscoveryAudit.BuildVerdict(
            controlFound: false, TwoCommands, missing: TwoCommands);

        Assert.AreEqual(DevConsoleDiscoveryAudit.Severity.Warning, verdict.Level);
        Assert.IsFalse(verdict.IsConclusive, "Nothing registered yet means too early, not broken — allow a re-check.");
        StringAssert.Contains(verdict.Message, "inconclusive");
    }

    /// <summary>
    /// Impossible unless the control command itself was renamed by an engine bump. Reported rather
    /// than silently treated as success, so the audit cannot rot into a permanent false negative.
    /// </summary>
    [TestMethod]
    public void BuildVerdict_ControlMissingButCommandsRegistered_WarnsAboutTheControl()
    {
        var verdict = DevConsoleDiscoveryAudit.BuildVerdict(controlFound: false, TwoCommands, missing: None);

        Assert.AreEqual(DevConsoleDiscoveryAudit.Severity.Warning, verdict.Level);
        StringAssert.Contains(verdict.Message, DevConsoleDiscoveryAudit.VanillaControlCommand);
    }

    /// <summary>
    /// An assembly with no attributed commands at all is a broken build, not a clean bill of health —
    /// "0 of 0 registered" must never render as PROVEN.
    /// </summary>
    [TestMethod]
    public void BuildVerdict_NoCommandsDeclared_DoesNotReportProven()
    {
        var verdict = DevConsoleDiscoveryAudit.BuildVerdict(controlFound: true, None, missing: None);

        Assert.AreNotEqual(DevConsoleDiscoveryAudit.Severity.Info, verdict.Level);
        Assert.IsFalse(verdict.Message.Contains("PROVEN"));
    }
}
