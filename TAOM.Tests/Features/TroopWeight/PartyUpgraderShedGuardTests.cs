using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Features.TroopWeight;

/// <summary>
/// Pins the leaderless-party guard in the TroopWeight shed hook. This has already shipped broken
/// once: CHANGELOG 2026-08-07, "shed-on-upgrade was deleting every settlement's militia down to ~20".
/// Vanilla drives `UpgradeReadyTroops` from `DailyTickPartyEvent`, which tickers over
/// `MobileParty.All`, so without the guard the postfix reaches garrisons, militia and bandit parties
/// and `PlanShed` removes their entire overflow in a single tick.
///
/// The guard matters more since 2026-09-01, when `AiGarrisonSizeFactor` dropped from 3.0 to 1.0.
/// Lowering a garrison's cap to a third is only survivable because the shed cannot see garrisons at
/// all: `GarrisonPartyComponent` overrides `PartyOwner` but NOT `Leader`, so `PartyComponent.Leader`
/// keeps its base `=> null` and `PartyBase.LeaderHero` is null for every garrison. Vanilla desertion
/// then drains an over-cap garrison at a throttled 25% of the overflow per day rather than at once.
///
/// The hook takes a sealed `PartyBase` that cannot be constructed in a unit test, so this is a
/// source-text assertion, the same technique as `AiPartySizeOrderingTests` and
/// `BannerTripletOrderingTests`.
/// </summary>
[TestClass]
public class PartyUpgraderShedGuardTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TAOM.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new FileNotFoundException("TAOM.sln not found walking upward from cwd");
    }

    private static string ReadHook()
    {
        var path = Path.Combine(
            FindRepoRoot(), "Main", "Features", "TroopWeight", "Hooks",
            "PartyUpgraderUpgradeReadyTroopsHook.cs");
        Assert.IsTrue(File.Exists(path), $"PartyUpgraderUpgradeReadyTroopsHook.cs not found at {path}");
        return File.ReadAllText(path);
    }

    /// <summary>
    /// The guard itself. A garrison, a militia party and a bandit party all have a null LeaderHero,
    /// so this one line is what keeps every one of them out of the shed.
    /// </summary>
    [TestMethod]
    public void ShedHook_BailsOutOnALeaderlessParty()
    {
        var src = ReadHook();

        Assert.IsTrue(
            src.Contains("if (party.LeaderHero == null)"),
            "The leaderless-party guard is missing. Without it the daily shed reaches garrisons, "
            + "militia and bandit parties and deletes their overflow in one tick (CHANGELOG 2026-08-07).");
    }

    /// <summary>
    /// Order matters as much as presence: the guard has to precede the roster work, or the shed is
    /// already planned by the time the party is rejected.
    /// </summary>
    [TestMethod]
    public void ShedHook_GuardPrecedesTheRosterRead()
    {
        var src = ReadHook();

        var guard = src.IndexOf("if (party.LeaderHero == null)", System.StringComparison.Ordinal);
        var roster = src.IndexOf("party.MemberRoster", System.StringComparison.Ordinal);

        Assert.IsTrue(guard >= 0, "leaderless guard not found");
        Assert.IsTrue(roster >= 0, "roster read not found");
        Assert.IsTrue(guard < roster,
            "The leaderless guard must run before the roster is read, or a leaderless party is "
            + "already being processed by the time it is rejected.");
    }

    /// <summary>
    /// The main party is exempt for a separate reason (vanilla's own guard), and losing it would
    /// shed the player's own troops. Pinned alongside, since both live in the same early-out block.
    /// </summary>
    [TestMethod]
    public void ShedHook_BailsOutOnTheMainParty()
    {
        var src = ReadHook();

        Assert.IsTrue(
            src.Contains("party == PartyBase.MainParty"),
            "The main-party guard is missing; the shed would trim the player's own party.");
    }
}
