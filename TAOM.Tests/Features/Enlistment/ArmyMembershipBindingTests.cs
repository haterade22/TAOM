using System;
using System.IO;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// Binding pins for the transient battle-only army merge (#443, field reports 4 and 7).
///
/// <c>ArmyMembershipAdapter</c> touches <c>MobileParty.MainParty</c> and constructs a real
/// <c>Army</c>, so none of it can be exercised without a live campaign. What CAN be pinned is the
/// engine surface the adapter is built on — and, more importantly, the engine DEFECT it is built
/// around. These resolve by name against the installed assemblies, so an engine bump that reshapes
/// any of it fails here rather than in a player's campaign.
/// </summary>
[TestClass]
[TestCategory("BindingVerification")]
public class ArmyMembershipBindingTests
{
    /// <summary>
    /// The bare constructor is the whole reason this adapter exists rather than calling
    /// <c>Kingdom.CreateArmy</c>: <c>CreateArmy</c> runs <c>Gather()</c>, whose non-player branch
    /// calls <c>FindBestGatheringSettlementAndMoveTheLeader</c> and would march the commander off a
    /// battlefield as a side effect of a cosmetic team fix.
    /// </summary>
    [TestMethod]
    public void ArmyConstructor_StillTakesKingdomLeaderAndType()
    {
        var ctor = typeof(Army).GetConstructor(
            new[] { typeof(Kingdom), typeof(MobileParty), typeof(Army.ArmyTypes) });

        Assert.IsNotNull(ctor,
            "Army(Kingdom, MobileParty, ArmyTypes) is gone — the battle merge has no way to raise an army "
            + "for a commander who has none without Kingdom.CreateArmy's gathering side effects.");
        Assert.IsTrue(ctor.IsPublic);
    }

    /// <summary>
    /// THE DEFECT THIS FEATURE WORKS AROUND. An army built by the bare constructor never gets an
    /// <c>AiBehaviorObject</c> — <c>Gather()</c> is what seeds it — and
    /// <c>Army.GetLongTermBehaviorTextForAILeadedParty</c> dereferences it with no null guard in
    /// five of its seven cases (<c>GoToSettlement</c>, <c>BesiegeSettlement</c>,
    /// <c>RaidSettlement</c>, <c>DefendSettlement</c>, <c>PatrolAroundPoint</c>; only
    /// <c>Hold</c>/<c>GoToPoint</c> are guarded). Verified against installed 1.4.8 on 2026-08-11.
    ///
    /// That is why the adapter disbands the army it raised UNCONDITIONALLY. If this method ever
    /// disappears or changes shape, re-decompile it: TaleWorlds adding the guard would let the
    /// unconditional disband relax to something gentler.
    /// </summary>
    [TestMethod]
    public void BehaviorTextForAiLeadedParty_StillExists_AndIsStillReachedFromTheMapUi()
    {
        var method = typeof(Army).GetMethod(
            "GetLongTermBehaviorTextForAILeadedParty",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsNotNull(method,
            "Army.GetLongTermBehaviorTextForAILeadedParty is gone — re-verify whether a bare-ctor army's "
            + "null AiBehaviorObject can still crash the map UI before relaxing the unconditional disband.");

        // The public front door, and the two live callers that reach it with an AI-led army:
        // MobileParty.GetBehaviorText() drives the map party tooltip, KingdomArmyItemVM the
        // kingdom Armies tab.
        Assert.IsNotNull(typeof(Army).GetMethod("GetLongTermBehaviorText", new[] { typeof(bool) }),
            "Army.GetLongTermBehaviorText is gone.");
        Assert.IsNotNull(typeof(MobileParty).GetMethod("GetBehaviorText", Type.EmptyTypes),
            "MobileParty.GetBehaviorText is gone — it is the party-tooltip path into the unguarded switch.");
    }

    /// <summary>
    /// <c>AiBehaviorObject</c> must stay settable — the adapter seeds it precisely because
    /// <c>Gather()</c> is skipped, and several vanilla members dereference it with no null guard.
    /// A <c>Settlement</c> must remain assignable to it.
    /// </summary>
    [TestMethod]
    public void AiBehaviorObject_IsAnIMapPointAndSettable()
    {
        var prop = typeof(Army).GetProperty("AiBehaviorObject");

        Assert.IsNotNull(prop, "Army.AiBehaviorObject is gone.");
        Assert.AreEqual(typeof(IMapPoint), prop.PropertyType);
        Assert.IsTrue(prop.CanWrite, "Army.AiBehaviorObject is no longer settable — the seed that keeps a "
            + "bare-ctor army out of vanilla's unguarded dereferences is gone with it.");
        Assert.IsTrue(prop.PropertyType.IsAssignableFrom(typeof(Settlement)),
            "a Settlement is no longer an IMapPoint — re-pick the seed anchor.");
    }

    /// <summary>
    /// The most reachable of the unguarded readers, and the one that makes seeding mandatory rather
    /// than merely prudent.
    /// <c>LordConversationsCampaignBehavior.conversation_lord_tell_objective_gathering_on_condition</c>
    /// reads <c>partyBelongedTo.Army.AiBehaviorObject.Name</c> gated ONLY on
    /// <c>Army != null &amp;&amp; Army.IsWaitingForArmyMembers()</c> — no <c>ArmyType</c> check, unlike its
    /// three sibling conditions. And <c>IsWaitingForArmyMembers()</c> returns TRUE FOREVER for a
    /// bare-ctor army, because <c>_armyGatheringStartTime</c> stays 0 and is only ever set by
    /// <c>CheckAndSetArmyGatheringTime</c>, which itself requires <c>AiBehaviorObject is
    /// Settlement</c>. So with a null objective, talking to any lord in the army is an
    /// unconditional CTD — and this feature's own wait menu offers exactly that.
    /// </summary>
    [TestMethod]
    public void ConversationGatheringCondition_StillExists_AndStillGatesOnIsWaitingForArmyMembers()
    {
        var behavior = typeof(Campaign).Assembly.GetType(
            "TaleWorlds.CampaignSystem.CampaignBehaviors.LordConversationsCampaignBehavior");

        Assert.IsNotNull(behavior, "LordConversationsCampaignBehavior is gone.");
        Assert.IsNotNull(
            behavior.GetMethod("conversation_lord_tell_objective_gathering_on_condition"),
            "conversation_lord_tell_objective_gathering_on_condition is gone — re-verify whether a "
            + "bare-ctor army's AiBehaviorObject can still crash an ordinary lord conversation.");

        // The gate that is true forever for an army we build without Gather().
        Assert.IsNotNull(typeof(Army).GetMethod("IsWaitingForArmyMembers", Type.EmptyTypes),
            "Army.IsWaitingForArmyMembers is gone — the reachability analysis for the seed needs redoing.");
    }

    /// <summary>
    /// Source-level pin for the seed itself, because the adapter cannot be executed here. Both
    /// guards must survive: the seed keeps a leaked army harmless, the disband keeps it from
    /// leaking. Losing either one alone restores a crash class.
    /// </summary>
    [TestMethod]
    public void CreatedArmy_IsSeededWithABehaviorObject()
    {
        var source = File.ReadAllText(FromRepoRoot("Main/Adapters/ArmyMembershipAdapter.cs"));

        StringAssert.Contains(source, "AiBehaviorObject =",
            "the created army no longer gets a seeded AiBehaviorObject — vanilla dereferences that "
            + "field with no null guard in Army.GetLongTermBehaviorTextForAILeadedParty, "
            + "Army.GetNotificationText and LordConversationsCampaignBehavior.");
        StringAssert.Contains(source, "SeedBehaviorObject",
            "SeedBehaviorObject was removed or renamed.");
    }

    /// <summary>
    /// The disband call, and the two properties the adapter reads to tell an army that is still
    /// standing from one vanilla already dispersed — <c>DisperseInternal</c> nulls
    /// <c>Kingdom</c> and every member's <c>Army</c>, then resets its own re-entry flag, so a second
    /// call would re-fire <c>OnArmyDispersed</c> rather than be absorbed by that guard.
    /// </summary>
    [TestMethod]
    public void DisbandAndLivenessSurface_StillPresent()
    {
        var disband = typeof(DisbandArmyAction).GetMethod(
            "ApplyByObjectiveFinished", BindingFlags.Public | BindingFlags.Static, null,
            new[] { typeof(Army) }, null);

        Assert.IsNotNull(disband,
            "DisbandArmyAction.ApplyByObjectiveFinished(Army) is gone — the army raised for a battle has no "
            + "ordinary vanilla way to end, and leaving it standing plants a null-AiBehaviorObject crash.");

        var kingdom = typeof(Army).GetProperty("Kingdom");
        Assert.IsNotNull(kingdom, "Army.Kingdom is gone — the already-dispersed check has no signal.");
        Assert.AreEqual(typeof(Kingdom), kingdom.PropertyType);

        var leader = typeof(Army).GetProperty("LeaderParty");
        Assert.IsNotNull(leader, "Army.LeaderParty is gone.");
        Assert.AreEqual(typeof(MobileParty), leader.PropertyType);

        Assert.IsNotNull(
            typeof(Army).GetMethod("AddPartyToMergedParties", new[] { typeof(MobileParty) }),
            "Army.AddPartyToMergedParties is gone — it is the half of the join that sets AttachedTo, "
            + "without which IsInSameArmyAsPlayer stays false and the player spawns behind the line again.");
    }

    /// <summary>
    /// Source-level pin for the decision itself, because the adapter cannot be executed here.
    ///
    /// The first revision left the created army standing whenever another lord had attached to it,
    /// reasoning that it had become a real army. It had not — it carries a null
    /// <c>AiBehaviorObject</c> forever, <c>Army.CheckInactivity</c> DECREMENTS the inactivity
    /// counter for besieging/raiding/defending leaders so it never times out, and
    /// <c>_aiBehaviorObject</c> is <c>[SaveableField(16)]</c> so it survives every reload. A stray
    /// army raised for one skirmish became a permanent crash on the kingdom screen.
    /// </summary>
    [TestMethod]
    public void LeaveArmy_DisbandsWhatItRaised_WithNoFollowerCountCarveOut()
    {
        var source = File.ReadAllText(FromRepoRoot("Main/Adapters/ArmyMembershipAdapter.cs"));

        StringAssert.Contains(source, "DisbandArmyAction.ApplyByObjectiveFinished",
            "the adapter no longer disbands the army it raised.");
        Assert.IsFalse(source.Contains("others"),
            "a follower-count carve-out is back in ArmyMembershipAdapter: an army we raised must be "
            + "disbanded whether or not other lords joined it, because its null AiBehaviorObject crashes "
            + "Army.GetLongTermBehaviorTextForAILeadedParty and nothing else will ever clean it up.");
    }

    private static string FromRepoRoot(string relPath)
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            var candidate = Path.Combine(dir, relPath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
                return candidate;
            dir = Directory.GetParent(dir)?.FullName;
        }

        Assert.Fail($"Could not locate {relPath} from the test working directory.");
        return null;
    }
}
