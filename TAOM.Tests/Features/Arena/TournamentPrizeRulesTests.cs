using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Arena;
using TAOM.Features.ArmourAcquisition.Domain;

namespace TAOM.Tests.Features.Arena;

/// <summary>
/// Which items a tournament may hand out: light, medium or heavy class and below, never anything the XML keeps
/// off the market (the troll gear), never elite, lord or named. A small tournament draws the regular band
/// (light and medium), a big one the elite band (heavy). Weapons, shields and harness have no table class and
/// are judged by engine tier, the same split the armour fallback uses.
/// </summary>
[TestClass]
public class TournamentPrizeRulesTests
{
    // (int)ItemObject.Tier, Tier1 = 0 in v1.5.3.
    private const int Tier3Index = 2;
    private const int Tier4Index = 3;
    private const int Tier5Index = 4;

    // --- PrizeClass ---

    [TestMethod]
    public void PrizeClass_TableClassGiven_WinsOverEngineTier()
    {
        Assert.AreEqual(ArmourClass.Lord, TournamentPrizeRules.PrizeClass(ArmourClass.Lord, Tier3Index));
    }

    [TestMethod]
    public void PrizeClass_Tier4WeaponWithNoClass_IsHeavy()
    {
        Assert.AreEqual(ArmourClass.Heavy, TournamentPrizeRules.PrizeClass(null, Tier4Index));
    }

    [TestMethod]
    public void PrizeClass_Tier5WeaponWithNoClass_IsElite()
    {
        Assert.AreEqual(ArmourClass.Elite, TournamentPrizeRules.PrizeClass(null, Tier5Index));
    }

    // --- Fits: the regular band ---

    [TestMethod]
    public void Fits_RegularBand_LightMerchandise_Accepted()
    {
        Assert.IsTrue(TournamentPrizeRules.Fits(PrizeBand.Regular, ArmourClass.Light, 2.5f, xmlMerchandise: true));
    }

    [TestMethod]
    public void Fits_RegularBand_MediumMerchandise_Accepted()
    {
        Assert.IsTrue(TournamentPrizeRules.Fits(PrizeBand.Regular, ArmourClass.Medium, 3.2f, xmlMerchandise: true));
    }

    [TestMethod]
    public void Fits_RegularBand_CivilianMerchandise_Accepted()
    {
        Assert.IsTrue(TournamentPrizeRules.Fits(PrizeBand.Regular, ArmourClass.Civilian, 2.5f, xmlMerchandise: true));
    }

    [TestMethod]
    public void Fits_RegularBand_BelowJunkFloor_Refused()
    {
        Assert.IsFalse(TournamentPrizeRules.Fits(PrizeBand.Regular, ArmourClass.Light, 1.9f, xmlMerchandise: true));
    }

    [TestMethod]
    public void Fits_RegularBand_AtJunkFloor_Accepted()
    {
        Assert.IsTrue(TournamentPrizeRules.Fits(PrizeBand.Regular, ArmourClass.Light, 2f, xmlMerchandise: true));
    }

    [TestMethod]
    public void Fits_RegularBand_Heavy_Refused()
    {
        // Heavy belongs to the big tournaments' band; a small one stays light and medium.
        Assert.IsFalse(TournamentPrizeRules.Fits(PrizeBand.Regular, ArmourClass.Heavy, 4f, xmlMerchandise: true));
    }

    [TestMethod]
    public void Fits_RegularBand_NaNTier_Refused()
    {
        Assert.IsFalse(TournamentPrizeRules.Fits(PrizeBand.Regular, ArmourClass.Light, float.NaN, xmlMerchandise: true));
    }

    // --- Fits: the elite band ---

    [TestMethod]
    public void Fits_EliteBand_HeavyMerchandise_Accepted()
    {
        // The armour gate flips heavy pieces to NotMerchandise at game init; the caller passes the XML
        // value, so a gate-flipped heavy piece is still a prize.
        Assert.IsTrue(TournamentPrizeRules.Fits(PrizeBand.Elite, ArmourClass.Heavy, 4.2f, xmlMerchandise: true));
    }

    [DataTestMethod]
    [DataRow(ArmourClass.Light)]
    [DataRow(ArmourClass.Medium)]
    [DataRow(ArmourClass.Civilian)]
    public void Fits_EliteBand_BelowHeavy_Refused(ArmourClass cls)
    {
        Assert.IsFalse(TournamentPrizeRules.Fits(PrizeBand.Elite, cls, 3.4f, xmlMerchandise: true));
    }

    // --- Fits: refused in both bands ---

    [DataTestMethod]
    [DataRow(PrizeBand.Regular)]
    [DataRow(PrizeBand.Elite)]
    public void Fits_EliteClass_RefusedInEveryBand(PrizeBand band)
    {
        Assert.IsFalse(TournamentPrizeRules.Fits(band, ArmourClass.Elite, 5f, xmlMerchandise: true));
    }

    [DataTestMethod]
    [DataRow(PrizeBand.Regular)]
    [DataRow(PrizeBand.Elite)]
    public void Fits_LordClass_RefusedInEveryBand(PrizeBand band)
    {
        Assert.IsFalse(TournamentPrizeRules.Fits(band, ArmourClass.Lord, 5.5f, xmlMerchandise: true));
    }

    [DataTestMethod]
    [DataRow(PrizeBand.Regular)]
    [DataRow(PrizeBand.Elite)]
    public void Fits_NamedClass_RefusedInEveryBand(PrizeBand band)
    {
        Assert.IsFalse(TournamentPrizeRules.Fits(band, ArmourClass.Named, 3f, xmlMerchandise: true));
    }

    [DataTestMethod]
    [DataRow(PrizeBand.Regular, ArmourClass.Light)]
    [DataRow(PrizeBand.Elite, ArmourClass.Heavy)]
    public void Fits_XmlNonMerchandise_RefusedInEveryBand(PrizeBand band, ArmourClass cls)
    {
        // The troll gear: is_merchandise="false" in the Armory XML keeps it out whatever its class.
        Assert.IsFalse(TournamentPrizeRules.Fits(band, cls, 3f, xmlMerchandise: false));
    }

    // --- Weapons through PrizeClass + Fits ---

    [TestMethod]
    public void Fits_Tier5WeaponWithNoClass_RefusedInEveryBand()
    {
        var cls = TournamentPrizeRules.PrizeClass(null, Tier5Index);

        Assert.IsFalse(TournamentPrizeRules.Fits(PrizeBand.Regular, cls, 5f, xmlMerchandise: true));
        Assert.IsFalse(TournamentPrizeRules.Fits(PrizeBand.Elite, cls, 5f, xmlMerchandise: true));
    }

    [TestMethod]
    public void Fits_Tier4WeaponWithNoClass_EliteBandOnly()
    {
        var cls = TournamentPrizeRules.PrizeClass(null, Tier4Index);

        Assert.IsFalse(TournamentPrizeRules.Fits(PrizeBand.Regular, cls, 4f, xmlMerchandise: true));
        Assert.IsTrue(TournamentPrizeRules.Fits(PrizeBand.Elite, cls, 4f, xmlMerchandise: true));
    }

    [TestMethod]
    public void Fits_Tier3WeaponWithNoClass_RegularBandOnly()
    {
        var cls = TournamentPrizeRules.PrizeClass(null, Tier3Index);

        Assert.IsTrue(TournamentPrizeRules.Fits(PrizeBand.Regular, cls, 3f, xmlMerchandise: true));
        Assert.IsFalse(TournamentPrizeRules.Fits(PrizeBand.Elite, cls, 3f, xmlMerchandise: true));
    }

    // --- XmlMerchandise: which merchandise flag the bands judge ---

    [TestMethod]
    public void XmlMerchandise_GateRecordSaysMerchandise_WinsOverTheGateFlippedLiveFlag()
    {
        // A heavy piece the armour gate flipped to NotMerchandise at game init is still a prize.
        Assert.IsTrue(TournamentPrizeRules.XmlMerchandise(recorded: true, liveNotMerchandise: true));
    }

    [TestMethod]
    public void XmlMerchandise_GateRecordSaysNotMerchandise_Refused()
    {
        Assert.IsFalse(TournamentPrizeRules.XmlMerchandise(recorded: false, liveNotMerchandise: false));
    }

    [DataTestMethod]
    [DataRow(false, true)]
    [DataRow(true, false)]
    public void XmlMerchandise_NoGateRecord_FallsBackToTheLiveFlag(bool liveNotMerchandise, bool expected)
    {
        // No record: the gate never read this game's items (a failed init), so the item's own flag decides.
        Assert.AreEqual(expected, TournamentPrizeRules.XmlMerchandise(recorded: null, liveNotMerchandise));
    }

    // --- PreferCulture: the town's culture first, never an empty list while anything fits ---

    private static readonly (string Id, string? Culture)[] Fitting =
    {
        ("gondor_helm", "gondor"), ("mordor_mace", "mordor"), ("gondor_sword", "gondor"), ("neutral_axe", null),
    };

    [TestMethod]
    public void PreferCulture_TownCultureHasItems_ReturnsOnlyThoseInOrder()
    {
        var pool = TournamentPrizeRules.PreferCulture(Fitting, "gondor", i => i.Culture);

        CollectionAssert.AreEqual(new[] { "gondor_helm", "gondor_sword" }, pool.Select(i => i.Id).ToArray());
    }

    [TestMethod]
    public void PreferCulture_TownCultureHasNone_ReturnsEveryFittingItem()
    {
        // The engine's prize roll indexes the list unguarded, so an empty culture pool must widen, not stay empty.
        var pool = TournamentPrizeRules.PreferCulture(Fitting, "lothlorien", i => i.Culture);

        Assert.AreEqual(Fitting.Length, pool.Count);
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void PreferCulture_NoTownCulture_ReturnsEveryFittingItem(string? cultureId)
    {
        Assert.AreEqual(Fitting.Length, TournamentPrizeRules.PreferCulture(Fitting, cultureId, i => i.Culture).Count);
    }

    [TestMethod]
    public void PreferCulture_NothingFits_ReturnsEmpty()
    {
        var none = new (string Id, string? Culture)[0];

        Assert.AreEqual(0, TournamentPrizeRules.PreferCulture(none, "gondor", i => i.Culture).Count);
    }
}
