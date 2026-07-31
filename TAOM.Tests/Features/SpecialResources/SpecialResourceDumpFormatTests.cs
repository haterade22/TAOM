using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SpecialResources.Cheats;

namespace TAOM.Tests.Features.SpecialResources;

/// <summary>
/// Pins the `taom.print_special_resources` console report — the read-only sibling of
/// `taom.add_special_resources`.
///
/// Deliberately NOT implemented as <c>GrantAmount(..., 0f)</c>. That would look like a free read but
/// clamps to the cap and writes back, so on a save whose balance predates a lowered cap a command
/// named "print" would silently mutate the balance. Same finding as the first console command's RCA,
/// wearing a different hat.
/// </summary>
[TestClass]
public class SpecialResourceDumpFormatTests
{
    [TestMethod]
    public void FormatDump_ResolvedResource_RendersNameIdBalanceAndCap()
    {
        var report = SpecialResourceCheats.FormatDump(
            displayName: "War Spoils", resourceId: "war_spoils",
            amount: 412f, cap: 500f, tierLevel: 2, tierCount: 3, availableAfterPending: 412f);

        StringAssert.Contains(report, "War Spoils");
        StringAssert.Contains(report, "war_spoils");
        StringAssert.Contains(report, "412");
        StringAssert.Contains(report, "500");
    }

    /// <summary>
    /// Wording must match `FormatResult`'s unresolved branch, so the two commands cannot disagree
    /// about what "your kingdom grants nothing" reads like.
    /// </summary>
    [TestMethod]
    public void FormatDump_Unresolved_UsesTheSameWordingAsTheGrantCommand()
    {
        var report = SpecialResourceCheats.FormatDump(
            displayName: null, resourceId: null,
            amount: 0f, cap: 0f, tierLevel: 0, tierCount: 0, availableAfterPending: 0f);

        StringAssert.Contains(report, SpecialResourceCheats.NoResourceMessage);
    }

    /// <summary>Tier 0 means "below the first milestone", not "tier 0 of 3".</summary>
    [TestMethod]
    public void FormatDump_TierZero_SaysNoTierReachedRatherThanTierZero()
    {
        var report = SpecialResourceCheats.FormatDump(
            displayName: "War Spoils", resourceId: "war_spoils",
            amount: 10f, cap: 500f, tierLevel: 0, tierCount: 3, availableAfterPending: 10f);

        StringAssert.Contains(report, "no tier");
        Assert.IsFalse(report.Contains("tier 0 of"));
    }

    /// <summary>A resource with no tier system configured must not render a tier clause at all.</summary>
    [TestMethod]
    public void FormatDump_ResourceWithoutTiers_OmitsTheTierClause()
    {
        var report = SpecialResourceCheats.FormatDump(
            displayName: "Lake Fish", resourceId: "lake_fish",
            amount: 40f, cap: 200f, tierLevel: 0, tierCount: 0, availableAfterPending: 40f);

        Assert.IsFalse(report.Contains("tier"), report);
    }

    /// <summary>
    /// An open party-screen session holds pending spend, so the spendable figure differs from the
    /// balance. Both must show — a single number would be ambiguous exactly when it matters.
    /// </summary>
    [TestMethod]
    public void FormatDump_PendingSpendDiffersFromBalance_ShowsBoth()
    {
        var report = SpecialResourceCheats.FormatDump(
            displayName: "War Spoils", resourceId: "war_spoils",
            amount: 412f, cap: 500f, tierLevel: 2, tierCount: 3, availableAfterPending: 120f);

        StringAssert.Contains(report, "412");
        StringAssert.Contains(report, "120");
        StringAssert.Contains(report, "pending");
    }

    [TestMethod]
    public void FormatDump_NoPendingSpend_OmitsThePendingClause()
    {
        var report = SpecialResourceCheats.FormatDump(
            displayName: "War Spoils", resourceId: "war_spoils",
            amount: 412f, cap: 500f, tierLevel: 2, tierCount: 3, availableAfterPending: 412f);

        Assert.IsFalse(report.Contains("pending"), report);
    }
}
