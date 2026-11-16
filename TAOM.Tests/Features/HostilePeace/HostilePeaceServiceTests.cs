using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.Diplomacy;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.Execution;
using TAOM.Features.HostilePeace;

namespace TAOM.Tests.Features.HostilePeace;

/// <summary>
/// The rule behind the three peacetime blocks (docs/features/hostile-peace.md). The pair is the owner's report: the Woodland Realm
/// (<c>mirkwood</c>) and Dol Guldur (<c>dolguldur</c>), a Hostile row in <c>diplomacy.json</c>; Lothlorien is its Permanent ally and
/// Dorwinion stands for a realm with no row (Neutral). Each block is asked on its own so a test fails when that block's rule is gone.
/// </summary>
[TestClass]
public class HostilePeaceServiceTests
{
    private const string Mirkwood = "mirkwood";
    private const string DolGuldur = "dolguldur";
    private const string Lothlorien = "lothlorien";
    private const string Dorwinion = "dorwinion";

    private IDiplomacyService _diplomacy = null!;
    private IHostilePeaceSettingsProvider _settings = null!;
    private HostilePeaceService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _diplomacy = Substitute.For<IDiplomacyService>();
        _diplomacy.GetRelationshipTier(Arg.Any<string>(), Arg.Any<string>()).Returns(AllianceTier.Neutral);
        _diplomacy.GetRelationshipTier(Mirkwood, DolGuldur).Returns(AllianceTier.Hostile);
        _diplomacy.GetRelationshipTier(DolGuldur, Mirkwood).Returns(AllianceTier.Hostile);
        _diplomacy.GetRelationshipTier(Mirkwood, Lothlorien).Returns(AllianceTier.Permanent);

        _settings = Substitute.For<IHostilePeaceSettingsProvider>();
        _settings.BlockCaravanTrade.Returns(true);
        _settings.BlockTradeAgreements.Returns(true);
        _settings.LimitTownAccess.Returns(true);
        _settings.AllowPlayerAlliancesWithAnyone.Returns(false);

        // Sides as in execution/alignment.json: a listed realm by its id, a player-founded kingdom (unlisted id) by its culture.
        _alignment = Substitute.For<IAlignmentService>();
        _alignment.ResolveSide(Arg.Any<string>(), Arg.Any<string>()).Returns(FactionSide.Neutral);
        _alignment.ResolveSide(Mirkwood, Arg.Any<string>()).Returns(FactionSide.Free);
        _alignment.ResolveSide(Lothlorien, Arg.Any<string>()).Returns(FactionSide.Free);
        _alignment.ResolveSide(DolGuldur, Arg.Any<string>()).Returns(FactionSide.Evil);
        _alignment.ResolveSide(PlayerFounded, "gondor").Returns(FactionSide.Free);
        _alignment.ResolveSide(PlayerFounded, "mordor").Returns(FactionSide.Evil);

        _service = new HostilePeaceService(_diplomacy, _alignment, _settings);
    }

    private const string PlayerFounded = "player_faction";
    private IAlignmentService _alignment = null!;

    // ---- D: the player's alliances ----

    [TestMethod]
    public void BlocksPlayerAlliance_PlayerRulesAFreeRealm_CannotAllyTheShadow()
    {
        Assert.IsTrue(_service.BlocksPlayerAlliance(Mirkwood, "mirkwood", DolGuldur, "dolguldur"));
    }

    [TestMethod]
    public void BlocksPlayerAlliance_SameSide_Allows()
    {
        Assert.IsFalse(_service.BlocksPlayerAlliance(Mirkwood, "mirkwood", Lothlorien, "lothlorien"));
    }

    [TestMethod]
    public void BlocksPlayerAlliance_NeutralRealm_AlliesWithAnyone()
    {
        Assert.IsFalse(_service.BlocksPlayerAlliance(Mirkwood, "mirkwood", Dorwinion, "dorwinion"));
    }

    [DataTestMethod]
    [DataRow("gondor", DolGuldur, true)]
    [DataRow("gondor", Lothlorien, false)]
    [DataRow("mordor", Mirkwood, true)]
    [DataRow("mordor", DolGuldur, false)]
    public void BlocksPlayerAlliance_PlayerFoundedKingdom_TakesItsCulturesSide(string culture, string other, bool expected)
    {
        Assert.AreEqual(expected, _service.BlocksPlayerAlliance(PlayerFounded, culture, other, other));
    }

    [TestMethod]
    public void BlocksPlayerAlliance_PlayerFoundedKingdomWithNoSide_AlliesWithAnyone()
    {
        Assert.IsFalse(_service.BlocksPlayerAlliance(PlayerFounded, "some_unlisted_culture", DolGuldur, "dolguldur"));
    }

    [TestMethod]
    public void BlocksPlayerAlliance_AllowWithAnyoneOn_KeepsTheOldFreedom()
    {
        _settings.AllowPlayerAlliancesWithAnyone.Returns(true);

        Assert.IsFalse(_service.BlocksPlayerAlliance(Mirkwood, "mirkwood", DolGuldur, "dolguldur"));
    }

    [DataTestMethod]
    [DataRow(null, DolGuldur)]
    [DataRow(Mirkwood, "")]
    [DataRow(Mirkwood, Mirkwood)]
    public void BlocksPlayerAlliance_BlankOrSameKingdom_NeverBlocks(string? player, string? other)
    {
        Assert.IsFalse(_service.BlocksPlayerAlliance(player, "mirkwood", other, "dolguldur"));
    }

    [TestMethod]
    public void BlocksPlayerAlliance_IgnoresTheTradeSwitches()
    {
        _settings.BlockCaravanTrade.Returns(false);
        _settings.BlockTradeAgreements.Returns(false);
        _settings.LimitTownAccess.Returns(false);

        Assert.IsTrue(_service.BlocksPlayerAlliance(Mirkwood, "mirkwood", DolGuldur, "dolguldur"));
    }

    // ---- The pair ----

    [DataTestMethod]
    [DataRow(Mirkwood, DolGuldur, true)]
    [DataRow(DolGuldur, Mirkwood, true)]
    [DataRow(Mirkwood, Lothlorien, false)]
    [DataRow(Mirkwood, Dorwinion, false)]
    [DataRow(Mirkwood, Mirkwood, false)]
    [DataRow(null, DolGuldur, false)]
    [DataRow(Mirkwood, "", false)]
    public void IsHostilePair_ReadsTheHostileTierOnly(string? a, string? b, bool expected)
    {
        Assert.AreEqual(expected, _service.IsHostilePair(a, b));
    }

    [TestMethod]
    public void IsHostilePair_SameRealm_NeverAsksTheTable()
    {
        // A realm against itself: the player's own fiefs and own caravans must never read as enemies, whatever the table says.
        _diplomacy.GetRelationshipTier(Mirkwood, Mirkwood).Returns(AllianceTier.Hostile);

        Assert.IsFalse(_service.IsHostilePair(Mirkwood, Mirkwood));
    }

    // ---- A: caravans ----

    [TestMethod]
    public void BlocksCaravanTrade_HostilePairAtPeace_Blocks()
    {
        Assert.IsTrue(_service.BlocksCaravanTrade(Mirkwood, DolGuldur, atWar: false));
    }

    [TestMethod]
    public void BlocksCaravanTrade_HostilePairAtWar_LeavesItToVanilla()
    {
        Assert.IsFalse(_service.BlocksCaravanTrade(Mirkwood, DolGuldur, atWar: true));
    }

    [DataTestMethod]
    [DataRow(Lothlorien)]
    [DataRow(Dorwinion)]
    public void BlocksCaravanTrade_NotHostile_TradesAsBefore(string town)
    {
        Assert.IsFalse(_service.BlocksCaravanTrade(Mirkwood, town, atWar: false));
    }

    [TestMethod]
    public void BlocksCaravanTrade_SwitchOff_TradesAsBefore()
    {
        _settings.BlockCaravanTrade.Returns(false);

        Assert.IsFalse(_service.BlocksCaravanTrade(Mirkwood, DolGuldur, atWar: false));
    }

    // ---- B: trade agreements ----

    [TestMethod]
    public void BlocksTradeAgreement_HostilePair_Blocks()
    {
        Assert.IsTrue(_service.BlocksTradeAgreement(Mirkwood, DolGuldur));
    }

    [DataTestMethod]
    [DataRow(Lothlorien)]
    [DataRow(Dorwinion)]
    public void BlocksTradeAgreement_NotHostile_Allows(string other)
    {
        Assert.IsFalse(_service.BlocksTradeAgreement(Mirkwood, other));
    }

    [TestMethod]
    public void BlocksTradeAgreement_SwitchOff_Allows()
    {
        _settings.BlockTradeAgreements.Returns(false);

        Assert.IsFalse(_service.BlocksTradeAgreement(Mirkwood, DolGuldur));
    }

    // ---- C: the player's access ----

    [TestMethod]
    public void LimitsPlayerAccess_HostilePairAtPeace_Limits()
    {
        Assert.IsTrue(_service.LimitsPlayerAccess(Mirkwood, DolGuldur, atWar: false, playerOwnsSettlement: false));
    }

    [TestMethod]
    public void LimitsPlayerAccess_AtWar_LeavesItToVanilla()
    {
        Assert.IsFalse(_service.LimitsPlayerAccess(Mirkwood, DolGuldur, atWar: true, playerOwnsSettlement: false));
    }

    [TestMethod]
    public void LimitsPlayerAccess_PlayersOwnSettlement_NeverLimits()
    {
        Assert.IsFalse(_service.LimitsPlayerAccess(Mirkwood, DolGuldur, atWar: false, playerOwnsSettlement: true));
    }

    [DataTestMethod]
    [DataRow(Lothlorien)]
    [DataRow(Dorwinion)]
    public void LimitsPlayerAccess_NotHostile_Allows(string settlementRealm)
    {
        Assert.IsFalse(_service.LimitsPlayerAccess(Mirkwood, settlementRealm, atWar: false, playerOwnsSettlement: false));
    }

    [TestMethod]
    public void LimitsPlayerAccess_SwitchOff_Allows()
    {
        _settings.LimitTownAccess.Returns(false);

        Assert.IsFalse(_service.LimitsPlayerAccess(Mirkwood, DolGuldur, atWar: false, playerOwnsSettlement: false));
    }

    // ---- C: crime with a Hostile realm stops short of war ----

    [TestMethod]
    public void CapsCrimeBelowWar_HostilePairAtPeace_PlayerLeads_Caps()
    {
        Assert.IsTrue(_service.CapsCrimeBelowWar(Mirkwood, DolGuldur, atWar: false, playerLeadsRealm: true));
    }

    [TestMethod]
    public void CapsCrimeBelowWar_PlayerDoesNotLead_LeavesItToVanilla()
    {
        // Vanilla declares war by crime only for a ruler, so there is no war to prevent.
        Assert.IsFalse(_service.CapsCrimeBelowWar(Mirkwood, DolGuldur, atWar: false, playerLeadsRealm: false));
    }

    [TestMethod]
    public void CapsCrimeBelowWar_AtWar_LeavesItToVanilla()
    {
        Assert.IsFalse(_service.CapsCrimeBelowWar(Mirkwood, DolGuldur, atWar: true, playerLeadsRealm: true));
    }

    [TestMethod]
    public void CapsCrimeBelowWar_SwitchOff_LeavesItToVanilla()
    {
        _settings.LimitTownAccess.Returns(false);

        Assert.IsFalse(_service.CapsCrimeBelowWar(Mirkwood, DolGuldur, atWar: false, playerLeadsRealm: true));
    }

    [DataTestMethod]
    [DataRow(Lothlorien)]
    [DataRow(Dorwinion)]
    public void CapsCrimeBelowWar_NotHostile_LeavesItToVanilla(string crimeRealm)
    {
        Assert.IsFalse(_service.CapsCrimeBelowWar(Mirkwood, crimeRealm, atWar: false, playerLeadsRealm: true));
    }

    [TestMethod]
    public void CapsCrimeBelowWar_SameRealm_LeavesItToVanilla()
    {
        Assert.IsFalse(_service.CapsCrimeBelowWar(Mirkwood, Mirkwood, atWar: false, playerLeadsRealm: true));
    }

    // ---- The switches are independent ----

    [TestMethod]
    public void EachSwitch_TurnsOffOnlyItsOwnBlock()
    {
        _settings.BlockCaravanTrade.Returns(false);

        Assert.IsFalse(_service.BlocksCaravanTrade(Mirkwood, DolGuldur, atWar: false));
        Assert.IsTrue(_service.BlocksTradeAgreement(Mirkwood, DolGuldur), "the trade agreement block followed the caravan switch");
        Assert.IsTrue(_service.LimitsTownAccess(Mirkwood, DolGuldur, atWar: false), "the access block followed the caravan switch");
    }
}
