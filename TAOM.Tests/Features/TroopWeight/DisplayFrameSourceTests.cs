using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Features.TroopWeight;

/// <summary>
/// Pins the 2026-09-06 usage-frame reframe at the two places a unit test cannot reach directly:
/// ApplyPartySizeWeightPenalty takes a sealed PartyBase and mutates a ref ExplainedNumber, so the
/// display/enforcement split is only observable in source. Same rationale (and shape) as
/// AiPartySizeOrderingTests.
///
/// What breaks if these regress: the tooltip goes back to printing "Heavy troops -9" and the party-size
/// limit visibly shrinks as the player recruits heavy troops — the exact user-reported complaint this
/// change exists to fix.
/// </summary>
[TestClass]
public class DisplayFrameSourceTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TAOM.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new FileNotFoundException("TAOM.sln not found walking upward from cwd");
    }

    private static string Read(params string[] parts)
    {
        var path = Path.Combine(FindRepoRoot(), Path.Combine(parts));
        Assert.IsTrue(File.Exists(path), $"Not found at {path}");
        return File.ReadAllText(path);
    }

    [TestMethod]
    public void PartySizeModel_PassesTheDescriptionsFlagThroughToTheWeightPenalty()
    {
        var src = Read("Main", "Features", "CulturalFeats", "Models", "TaomPartySizeModel.cs");

        StringAssert.Contains(
            src, "ApplyPartySizeWeightPenalty(party, ref result, includeDescriptions)",
            "TaomPartySizeModel must forward includeDescriptions to the weight penalty. Without it the "
            + "display path deflates too, and the party-size tooltip prints a 'Heavy troops -N' line.");
    }

    [TestMethod]
    public void PartySizeModel_KeepsTheOverrideBranchFree()
    {
        var src = Read("Main", "Features", "CulturalFeats", "Models", "TaomPartySizeModel.cs");

        int overrideStart = src.IndexOf("public override ExplainedNumber GetPartyMemberSizeLimit", StringComparison.Ordinal);
        Assert.AreNotEqual(-1, overrideStart, "GetPartyMemberSizeLimit override not found");
        int overrideEnd = src.IndexOf("public override ExplainedNumber CalculateGarrisonPartySizeLimit", StringComparison.Ordinal);
        Assert.AreNotEqual(-1, overrideEnd, "CalculateGarrisonPartySizeLimit override not found");

        var body = src.Substring(overrideStart, overrideEnd - overrideStart);
        // gamemodels.md rule 4: the override is an entry point and stays a straight-line delegate. The
        // includeDescriptions decision belongs in the service, not in a branch here.
        StringAssert.DoesNotMatch(
            body, new System.Text.RegularExpressions.Regex(@"^\s*(if|switch|foreach)\b", System.Text.RegularExpressions.RegexOptions.Multiline),
            "GetPartyMemberSizeLimit must contain no branching (gamemodels.md rule 4) — push the decision "
            + "into the service instead.");
    }

    [TestMethod]
    public void WeightPenalty_SkipsTheDisplayPathBeforeItCachesTheTrueBase()
    {
        var src = Read("Main", "Features", "TroopWeight", "TroopWeightService.cs");

        int gate = src.IndexOf("if (includeDescriptions)", StringComparison.Ordinal);
        int cache = src.IndexOf("_lastBaseLimit.GetOrCreateValue", StringComparison.Ordinal);
        int subtract = src.IndexOf("SubtractResultFramePenalty(ref limit", StringComparison.Ordinal);

        Assert.AreNotEqual(-1, gate, "ApplyPartySizeWeightPenalty must gate on includeDescriptions");
        Assert.AreNotEqual(-1, cache, "ApplyPartySizeWeightPenalty must cache the true base limit");
        Assert.AreNotEqual(-1, subtract, "ApplyPartySizeWeightPenalty must subtract the result-frame penalty");

        Assert.IsTrue(
            gate < cache && gate < subtract,
            "The includeDescriptions early-out must precede BOTH the _lastBaseLimit cache write and the "
            + "penalty subtraction. A tooltip query must not be able to write the shed planner's budget.");
    }

    [TestMethod]
    public void DisplayHook_DoesNotRewriteHeadcountRows()
    {
        var src = Read("Main", "Features", "TroopWeight", "Hooks", "TroopWeightDisplayHook.cs");

        // The phantom-wounded regression (RCA 2026-06-07): weighting Battle Ready / Wounded makes the weight
        // surplus render as fake wounds. Only the capacity row may be restated.
        StringAssert.Contains(src, "LandTroopCapacityLabel", "The tooltip rewrite must target the capacity row.");
        Assert.IsFalse(
            src.Contains("LVmkE2Ow") || src.Contains("TzLtVzdg"),
            "The display hook must NOT touch the Battle Ready ({=LVmkE2Ow}) or Wounded ({=TzLtVzdg}) tooltip "
            + "rows — those are headcounts and weighting them recreates the phantom-wounded bug.");
    }
}
