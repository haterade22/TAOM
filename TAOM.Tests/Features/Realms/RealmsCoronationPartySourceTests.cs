using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.Realms;

/// <summary>
/// The crowned companion's party (RealmsCampaign.Coronation.LeadParty). Vanilla's companion-made-lord path
/// (CompanionRolesCampaignBehavior.ClanNameSelectionIsDone) only runs face to face, so the companion is in the main
/// party or leads his own; it takes him out of the main party and adopts a party he leads. The Realms coronation runs
/// from the titles screen for any member of the player's clan, wherever he rides, so a claimant travelling as a plain
/// member of another clan party must leave that party too, or the whole party (and its leader's command) moves into the
/// new ruling house. Source pin: the engine types cannot be staged without the game.
/// </summary>
[TestClass]
public class RealmsCoronationPartySourceTests
{
    private const string Path = "Main/Features/Realms/RealmsCampaign.Coronation.cs";

    private static string LeadPartyBody()
    {
        var src = RepoPaths.ReadSource(Path, stripComments: true);
        var body = Regex.Match(src, @"static void LeadParty\([\s\S]*?\n    \}").Value;
        Assert.IsTrue(body.Length > 0, "LeadParty not found");
        return body;
    }

    [TestMethod]
    public void LeadParty_ClaimantRidingInAPartyHeDoesNotLead_LeavesThatParty()
    {
        var body = LeadPartyBody();

        StringAssert.Matches(body, new Regex(
            @"if \(current != null && current\.LeaderHero != hero\)\s*current\.MemberRoster\.AddToCounts\(hero\.CharacterObject, -1\);"));
        Assert.IsFalse(body.Contains("hero.PartyBelongedTo == MobileParty.MainParty"),
            "the main party is only one of the parties he can ride in without leading it");
    }

    [TestMethod]
    public void LeadParty_ClaimantWithoutAPartyOfHisOwn_GetsANewOneWhereHeWas()
    {
        var body = LeadPartyBody();

        StringAssert.Matches(body, new Regex(@"var spawnAt = current\?\.Position \?\? MobileParty\.MainParty\.Position;"));
        StringAssert.Matches(body, new Regex(@"LordPartyComponent\.CreateLordParty\([^;]*spawnAt,"));
    }

    [TestMethod]
    public void LeadParty_PartyTheClaimantLeads_IsStillAdopted()
    {
        var body = LeadPartyBody();

        StringAssert.Matches(body, new Regex(@"party\.ActualClan = clan;"));
    }
}
