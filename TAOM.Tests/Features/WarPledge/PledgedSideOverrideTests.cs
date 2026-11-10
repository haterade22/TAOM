using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Diplomacy;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.Execution;
using TAOM.Features.WarPledge;

namespace TAOM.Tests.Features.WarPledge;

/// <summary>
/// #1113: a pledge changes a realm's side and its relationship tiers at the source. Both services are the
/// real ones over one shared store, so the tier tests read the side the alignment service really gives.
/// <c>cardolan</c> is both a kingdom id and a culture id, which is why the override sits in
/// <c>GetKingdomSide</c> and never in the lookup the culture shares.
/// </summary>
[TestClass]
public class PledgedSideOverrideTests
{
    private WarPledgeStore _store = null!;
    private AlignmentService _alignment = null!;
    private DiplomacyService _diplomacy = null!;

    [TestInitialize]
    public void SetUp()
    {
        _store = new WarPledgeStore();

        var alignmentConfig = Substitute.For<IAlignmentConfigProvider>();
        alignmentConfig.LoadAlignments().Returns(new Dictionary<string, string>
        {
            { "empire_w", "free" },
            { "lindon", "free" },
            { "empire_s", "evil" },
            { "goblin", "evil" },
            { "cardolan", "neutral" },
            { "rhudaur", "neutral" },
            { "shaghana", "neutral" },
            { "dorwinion", "neutral" },
        });
        _alignment = new AlignmentService(alignmentConfig, Substitute.For<IModLogger>(), _store);

        var diplomacyConfig = Substitute.For<IDiplomacyConfigProvider>();
        diplomacyConfig.LoadConfig().Returns(new DiplomacyConfig
        {
            Relationships = new List<KingdomRelationship>
            {
                new() { KingdomA = "empire_w", KingdomB = "empire_s", Tier = AllianceTier.Hostile },
                new() { KingdomA = "goblin", KingdomB = "shaghana", Tier = AllianceTier.Natural },
                new() { KingdomA = "rhudaur", KingdomB = "lindon", Tier = AllianceTier.Permanent },
                new() { KingdomA = "cardolan", KingdomB = "dorwinion", Tier = AllianceTier.Natural },
            },
        });
        _diplomacy = new DiplomacyService(
            diplomacyConfig, Substitute.For<IAllianceAdapter>(), _alignment, Substitute.For<IModLogger>(), _store);
    }

    [TestMethod]
    public void GetKingdomSide_PledgedRealm_ReturnsThePledgedSide()
    {
        _store.Pledge("cardolan", FactionSide.Free);
        _store.Pledge("rhudaur", FactionSide.Evil);

        Assert.AreEqual(FactionSide.Free, _alignment.GetKingdomSide("cardolan"));
        Assert.AreEqual(FactionSide.Evil, _alignment.GetKingdomSide("rhudaur"));
    }

    [TestMethod]
    public void GetKingdomSide_RealmWithoutAPledge_ReadsTheTable()
    {
        _store.Pledge("cardolan", FactionSide.Free);

        Assert.AreEqual(FactionSide.Neutral, _alignment.GetKingdomSide("dorwinion"));
        Assert.AreEqual(FactionSide.Evil, _alignment.GetKingdomSide("empire_s"));
    }

    // The culture "cardolan" is not the realm "cardolan": a soldier sided by culture stays Neutral.
    [TestMethod]
    public void GetCultureSide_IdOfAPledgedRealm_StaysStatic()
    {
        _store.Pledge("cardolan", FactionSide.Free);

        Assert.AreEqual(FactionSide.Neutral, _alignment.GetCultureSide("cardolan"));
    }

    [TestMethod]
    public void ResolveSide_PledgedRealm_ReturnsThePledgedSide()
    {
        _store.Pledge("cardolan", FactionSide.Free);

        Assert.AreEqual(FactionSide.Free, _alignment.ResolveSide("cardolan", "cardolan"));
    }

    [TestMethod]
    public void ResolveSide_NoFactionAndTheCultureOfAPledgedRealm_StaysNeutral()
    {
        _store.Pledge("cardolan", FactionSide.Free);

        Assert.AreEqual(FactionSide.Neutral, _alignment.ResolveSide(string.Empty, "cardolan"));
        Assert.AreEqual(FactionSide.Neutral, _alignment.ResolveSide(null!, "cardolan"));
    }

    [TestMethod]
    public void AreSameAlignment_PledgedRealmAndAMemberOfItsSide_IsTrue()
    {
        _store.Pledge("cardolan", FactionSide.Free);

        Assert.IsTrue(_alignment.AreSameAlignment("cardolan", "empire_w"));
        Assert.IsFalse(_alignment.AreSameAlignment("cardolan", "empire_s"));
    }

    [TestMethod]
    public void GetKingdomSide_AfterTheSessionReset_ReadsTheTableAgain()
    {
        _store.Pledge("cardolan", FactionSide.Free);

        _store.ResetForNewSession();

        Assert.AreEqual(FactionSide.Neutral, _alignment.GetKingdomSide("cardolan"));
    }

    [TestMethod]
    public void GetRelationshipTier_PledgedRealmAgainstTheOtherSide_IsHostile()
    {
        _store.Pledge("cardolan", FactionSide.Free);

        Assert.AreEqual(AllianceTier.Hostile, _diplomacy.GetRelationshipTier("cardolan", "empire_s"));
        Assert.AreEqual(AllianceTier.Hostile, _diplomacy.GetRelationshipTier("goblin", "cardolan"));
    }

    [TestMethod]
    public void GetRelationshipTier_PledgedRealmAgainstItsOwnSide_KeepsTheStaticTier()
    {
        _store.Pledge("cardolan", FactionSide.Free);

        Assert.AreEqual(AllianceTier.Neutral, _diplomacy.GetRelationshipTier("cardolan", "empire_w"));
    }

    [TestMethod]
    public void GetRelationshipTier_PledgedRealmAgainstANeutralRealm_KeepsTheStaticTier()
    {
        _store.Pledge("cardolan", FactionSide.Free);

        Assert.AreEqual(AllianceTier.Natural, _diplomacy.GetRelationshipTier("cardolan", "dorwinion"));
        Assert.AreEqual(AllianceTier.Neutral, _diplomacy.GetRelationshipTier("cardolan", "shaghana"));
    }

    [TestMethod]
    public void GetRelationshipTier_StaticPermanentRow_WinsOverThePledge()
    {
        _store.Pledge("rhudaur", FactionSide.Evil);

        Assert.AreEqual(AllianceTier.Permanent, _diplomacy.GetRelationshipTier("rhudaur", "lindon"));
        Assert.AreEqual(AllianceTier.Permanent, _diplomacy.GetRelationshipTier("lindon", "rhudaur"));
    }

    [TestMethod]
    public void GetRelationshipTier_StaticNaturalRowWithTheOtherSide_BecomesHostile()
    {
        _store.Pledge("shaghana", FactionSide.Free);

        Assert.AreEqual(AllianceTier.Hostile, _diplomacy.GetRelationshipTier("shaghana", "goblin"));
    }

    [TestMethod]
    public void GetRelationshipTier_TwoPledgedRealmsOnOpposedSides_IsHostile()
    {
        _store.Pledge("cardolan", FactionSide.Free);
        _store.Pledge("rhudaur", FactionSide.Evil);

        Assert.AreEqual(AllianceTier.Hostile, _diplomacy.GetRelationshipTier("cardolan", "rhudaur"));
    }

    [TestMethod]
    public void GetRelationshipTier_TwoPledgedRealmsOnOneSide_KeepsTheStaticTier()
    {
        _store.Pledge("cardolan", FactionSide.Free);
        _store.Pledge("dorwinion", FactionSide.Free);

        Assert.AreEqual(AllianceTier.Natural, _diplomacy.GetRelationshipTier("cardolan", "dorwinion"));
    }

    // The override is for a pledged realm only: two native members with no row stay as the table says,
    // with or without a pledge elsewhere in the store.
    [TestMethod]
    public void GetRelationshipTier_TwoNativeMembersOfOpposedSidesWithoutARow_StayNeutral()
    {
        Assert.AreEqual(AllianceTier.Neutral, _diplomacy.GetRelationshipTier("lindon", "empire_s"));

        _store.Pledge("cardolan", FactionSide.Free);

        Assert.AreEqual(AllianceTier.Neutral, _diplomacy.GetRelationshipTier("lindon", "empire_s"));
        Assert.AreEqual(AllianceTier.Neutral, _diplomacy.GetRelationshipTier("empire_s", "lindon"));
    }

    [TestMethod]
    public void IsWarAllowed_PledgedRealmAndAMemberOfItsSide_IsFalse()
    {
        Assert.IsTrue(_diplomacy.IsWarAllowed("cardolan", "empire_w"), "precondition: allowed before the pledge");

        _store.Pledge("cardolan", FactionSide.Free);

        Assert.IsFalse(_diplomacy.IsWarAllowed("cardolan", "empire_w"));
        Assert.IsTrue(_diplomacy.IsWarAllowed("cardolan", "empire_s"));
    }

    [TestMethod]
    public void IsAllianceAllowed_PledgedRealmAndTheOtherSide_IsFalse()
    {
        Assert.IsTrue(_diplomacy.IsAllianceAllowed("cardolan", "empire_s"), "precondition: allowed before the pledge");

        _store.Pledge("cardolan", FactionSide.Free);

        Assert.IsFalse(_diplomacy.IsAllianceAllowed("cardolan", "empire_s"));
        Assert.IsTrue(_diplomacy.IsAllianceAllowed("cardolan", "empire_w"));
    }

    // Other features and other worktrees build both services with no store: that means no pledge.
    [TestMethod]
    public void BothServices_BuiltWithoutAStore_ReadTheTables()
    {
        var alignmentConfig = Substitute.For<IAlignmentConfigProvider>();
        alignmentConfig.LoadAlignments().Returns(new Dictionary<string, string> { { "cardolan", "neutral" }, { "empire_s", "evil" } });
        var alignment = new AlignmentService(alignmentConfig, Substitute.For<IModLogger>());
        var diplomacyConfig = Substitute.For<IDiplomacyConfigProvider>();
        diplomacyConfig.LoadConfig().Returns(new DiplomacyConfig { Relationships = new List<KingdomRelationship>() });
        var diplomacy = new DiplomacyService(
            diplomacyConfig, Substitute.For<IAllianceAdapter>(), alignment, Substitute.For<IModLogger>());

        Assert.AreEqual(FactionSide.Neutral, alignment.GetKingdomSide("cardolan"));
        Assert.AreEqual(AllianceTier.Neutral, diplomacy.GetRelationshipTier("cardolan", "empire_s"));
    }
}
