using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Enlistment;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// The join predicate behind the field report *"my lord's army respawned; I was with the party but
/// was not pulled into the fight."*
///
/// A `MapEvent` lists the parties that ENTERED it. A lord attached to someone else's army does not
/// enter it himself — the army leader does — so matching only his own party misses every battle he
/// fights while attached, and the join falls through to the hourly recovery up to an in-game hour
/// later. A live session measured 6 of 10 joins arriving that way (#408).
/// </summary>
[TestClass]
public class CommanderBattleMatchPolicyTests
{
    private static IEnumerable<string> Involved(params string[] ids) => ids;

    [TestMethod]
    public void CommandersOwnParty_Matches()
        => Assert.IsTrue(CommanderBattleMatchPolicy.IsCommanderInvolved(
            "lord_party_1", null, Involved("bandit_7", "lord_party_1")));

    [TestMethod]
    public void CommanderAttachedToAnotherArmy_MatchesOnTheArmyLeader()
    {
        // THE REPORTED BUG. The commander fights under his army leader, so only the leader's party
        // is listed; his own never appears.
        Assert.IsTrue(CommanderBattleMatchPolicy.IsCommanderInvolved(
            "lord_party_1", "lord_marshal_party", Involved("lord_marshal_party", "enemy_9")));
    }

    [TestMethod]
    public void UnrelatedBattle_DoesNotMatch()
        => Assert.IsFalse(CommanderBattleMatchPolicy.IsCommanderInvolved(
            "lord_party_1", "lord_marshal_party", Involved("bandit_7", "villagers_2")));

    [TestMethod]
    public void CommanderLeadsHisOwnArmy_StillMatchesOnce()
    {
        // armyLeaderPartyId == commanderPartyId collapses into the own-party check; assert it does
        // not somehow stop matching.
        Assert.IsTrue(CommanderBattleMatchPolicy.IsCommanderInvolved(
            "lord_party_1", "lord_party_1", Involved("lord_party_1")));
    }

    [TestMethod]
    public void NoCommanderParty_NeverMatches()
    {
        // A commander with no party is the CommanderUnavailable grace, not a battle to join.
        // Matching on the army leader alone there would join a battle for a lord who has no troops.
        Assert.IsFalse(CommanderBattleMatchPolicy.IsCommanderInvolved(
            null, "lord_marshal_party", Involved("lord_marshal_party")));
        Assert.IsFalse(CommanderBattleMatchPolicy.IsCommanderInvolved(
            "", "lord_marshal_party", Involved("lord_marshal_party")));
    }

    [TestMethod]
    public void NullsAndBlanksAreTolerated()
    {
        Assert.IsFalse(CommanderBattleMatchPolicy.IsCommanderInvolved("lord_party_1", null, null));
        Assert.IsTrue(CommanderBattleMatchPolicy.IsCommanderInvolved(
            "lord_party_1", null, Involved(null, "", "lord_party_1")));
    }
}
