using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Arena;
using TAOM.Features.ArmourAcquisition;
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
    private const int Tier1Index = 0;
    private const int Tier3Index = 2;
    private const int Tier4Index = 3;
    private const int Tier5Index = 4;

    private const int CheapValue = 100;
    private const int WorthAPrize = 2000;

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

    // --- Fits: the regular band, an item the class table does not list ---

    [TestMethod]
    public void Fits_RegularBand_NoTableClass_LightByTier_Accepted()
    {
        Assert.IsTrue(TournamentPrizeRules.Fits(PrizeBand.Regular, null, Tier1Index, 2.5f, xmlMerchandise: true, itemValue: CheapValue));
    }

    [TestMethod]
    public void Fits_RegularBand_NoTableClass_MediumByTier_Accepted()
    {
        Assert.IsTrue(TournamentPrizeRules.Fits(PrizeBand.Regular, null, Tier3Index, 3.2f, xmlMerchandise: true, itemValue: CheapValue));
    }

    [TestMethod]
    public void Fits_RegularBand_NoTableClass_BelowJunkFloor_Refused()
    {
        Assert.IsFalse(TournamentPrizeRules.Fits(PrizeBand.Regular, null, Tier1Index, 1.9f, xmlMerchandise: true, itemValue: WorthAPrize));
    }

    [TestMethod]
    public void Fits_RegularBand_NoTableClass_AtJunkFloor_Accepted()
    {
        Assert.IsTrue(TournamentPrizeRules.Fits(PrizeBand.Regular, null, Tier1Index, 2f, xmlMerchandise: true, itemValue: CheapValue));
    }

    [TestMethod]
    public void Fits_RegularBand_NoTableClass_Heavy_Refused()
    {
        Assert.IsFalse(TournamentPrizeRules.Fits(PrizeBand.Regular, null, Tier4Index, 4f, xmlMerchandise: true, itemValue: WorthAPrize));
    }

    [TestMethod]
    public void Fits_RegularBand_NoTableClass_NaNTierf_Refused()
    {
        Assert.IsFalse(TournamentPrizeRules.Fits(PrizeBand.Regular, null, Tier1Index, float.NaN, xmlMerchandise: true, itemValue: WorthAPrize));
    }

    [TestMethod]
    public void Fits_RegularBand_NoTableClass_WeaponAtTier1_Refused()
    {
        Assert.IsFalse(TournamentPrizeRules.Fits(PrizeBand.Regular, null, Tier1Index, 1f, xmlMerchandise: true, itemValue: WorthAPrize));
    }

    // --- Fits: the regular band, an armour piece the class table lists ---

    [TestMethod]
    public void Fits_RegularBand_TableMedium_WorthEnough_LowTierf_Accepted()
    {
        Assert.IsTrue(TournamentPrizeRules.Fits(PrizeBand.Regular, ArmourClass.Medium, Tier1Index, 0.8f, xmlMerchandise: true, itemValue: 765));
    }

    [TestMethod]
    public void Fits_RegularBand_TableLight_Worth162_Refused()
    {
        Assert.IsFalse(TournamentPrizeRules.Fits(PrizeBand.Regular, ArmourClass.Light, Tier1Index, 0.3f, xmlMerchandise: true, itemValue: 162));
    }

    [TestMethod]
    public void Fits_RegularBand_TableMedium_Worth400_Refused()
    {
        Assert.IsFalse(TournamentPrizeRules.Fits(PrizeBand.Regular, ArmourClass.Medium, Tier1Index, 1.4f, xmlMerchandise: true, itemValue: 400));
    }

    [DataTestMethod]
    [DataRow(ArmourClass.Light)]
    [DataRow(ArmourClass.Medium)]
    [DataRow(ArmourClass.Civilian)]
    public void Fits_RegularBand_TableClass_AtMinimumValue_Accepted(ArmourClass cls)
    {
        Assert.IsTrue(TournamentPrizeRules.Fits(PrizeBand.Regular, cls, Tier1Index, 0.3f, xmlMerchandise: true,
            itemValue: TournamentPrizeRules.RegularMinTableArmourValue));
    }

    [DataTestMethod]
    [DataRow(ArmourClass.Light)]
    [DataRow(ArmourClass.Medium)]
    [DataRow(ArmourClass.Civilian)]
    public void Fits_RegularBand_TableClass_JustBelowMinimumValue_Refused(ArmourClass cls)
    {
        Assert.IsFalse(TournamentPrizeRules.Fits(PrizeBand.Regular, cls, Tier1Index, 0.3f, xmlMerchandise: true,
            itemValue: TournamentPrizeRules.RegularMinTableArmourValue - 1));
    }

    [TestMethod]
    public void Fits_RegularBand_TableHeavy_Refused()
    {
        Assert.IsFalse(TournamentPrizeRules.Fits(PrizeBand.Regular, ArmourClass.Heavy, Tier1Index, 0.8f, xmlMerchandise: true, itemValue: WorthAPrize));
    }

    [TestMethod]
    public void Fits_RegularBand_TableMedium_XmlNonMerchandise_Refused()
    {
        Assert.IsFalse(TournamentPrizeRules.Fits(PrizeBand.Regular, ArmourClass.Medium, Tier1Index, 0.8f, xmlMerchandise: false, itemValue: WorthAPrize));
    }

    [TestMethod]
    public void Fits_RegularBand_TableClass_NaNTierf_Accepted()
    {
        Assert.IsTrue(TournamentPrizeRules.Fits(PrizeBand.Regular, ArmourClass.Medium, Tier1Index, float.NaN, xmlMerchandise: true, itemValue: WorthAPrize));
    }

    [TestMethod]
    public void Fits_RegularBand_TableClass_BeatsTheEngineTier()
    {
        Assert.IsTrue(TournamentPrizeRules.Fits(PrizeBand.Regular, ArmourClass.Light, Tier5Index, 5f, xmlMerchandise: true, itemValue: WorthAPrize));
    }

    // --- Fits: the elite band ---

    [TestMethod]
    public void Fits_EliteBand_TableHeavy_AnyValue_Accepted()
    {
        Assert.IsTrue(TournamentPrizeRules.Fits(PrizeBand.Elite, ArmourClass.Heavy, Tier1Index, 4.2f, xmlMerchandise: true, itemValue: CheapValue));
    }

    [TestMethod]
    public void Fits_EliteBand_NoTableClass_Tier4_Accepted()
    {
        Assert.IsTrue(TournamentPrizeRules.Fits(PrizeBand.Elite, null, Tier4Index, 4f, xmlMerchandise: true, itemValue: CheapValue));
    }

    [DataTestMethod]
    [DataRow(ArmourClass.Light)]
    [DataRow(ArmourClass.Medium)]
    [DataRow(ArmourClass.Civilian)]
    public void Fits_EliteBand_BelowHeavy_Refused(ArmourClass cls)
    {
        Assert.IsFalse(TournamentPrizeRules.Fits(PrizeBand.Elite, cls, Tier1Index, 3.4f, xmlMerchandise: true, itemValue: WorthAPrize));
    }

    // --- Fits: refused in both bands ---

    [DataTestMethod]
    [DataRow(PrizeBand.Regular)]
    [DataRow(PrizeBand.Elite)]
    public void Fits_EliteClass_RefusedInEveryBand(PrizeBand band)
    {
        Assert.IsFalse(TournamentPrizeRules.Fits(band, ArmourClass.Elite, Tier5Index, 5f, xmlMerchandise: true, itemValue: WorthAPrize));
    }

    [DataTestMethod]
    [DataRow(PrizeBand.Regular)]
    [DataRow(PrizeBand.Elite)]
    public void Fits_LordClass_RefusedInEveryBand(PrizeBand band)
    {
        Assert.IsFalse(TournamentPrizeRules.Fits(band, ArmourClass.Lord, Tier5Index, 5.5f, xmlMerchandise: true, itemValue: WorthAPrize));
    }

    [DataTestMethod]
    [DataRow(PrizeBand.Regular)]
    [DataRow(PrizeBand.Elite)]
    public void Fits_NamedClass_RefusedInEveryBand(PrizeBand band)
    {
        Assert.IsFalse(TournamentPrizeRules.Fits(band, ArmourClass.Named, Tier3Index, 3f, xmlMerchandise: true, itemValue: WorthAPrize));
    }

    [DataTestMethod]
    [DataRow(PrizeBand.Regular, ArmourClass.Light)]
    [DataRow(PrizeBand.Elite, ArmourClass.Heavy)]
    public void Fits_XmlNonMerchandise_RefusedInEveryBand(PrizeBand band, ArmourClass cls)
    {
        Assert.IsFalse(TournamentPrizeRules.Fits(band, cls, Tier3Index, 3f, xmlMerchandise: false, itemValue: WorthAPrize));
    }

    // --- Weapons through Fits: no table class, judged by engine tier ---

    [TestMethod]
    public void Fits_Tier5Weapon_RefusedInEveryBand()
    {
        Assert.IsFalse(TournamentPrizeRules.Fits(PrizeBand.Regular, null, Tier5Index, 5f, xmlMerchandise: true, itemValue: WorthAPrize));
        Assert.IsFalse(TournamentPrizeRules.Fits(PrizeBand.Elite, null, Tier5Index, 5f, xmlMerchandise: true, itemValue: WorthAPrize));
    }

    [TestMethod]
    public void Fits_Tier4Weapon_EliteBandOnly()
    {
        Assert.IsFalse(TournamentPrizeRules.Fits(PrizeBand.Regular, null, Tier4Index, 4f, xmlMerchandise: true, itemValue: WorthAPrize));
        Assert.IsTrue(TournamentPrizeRules.Fits(PrizeBand.Elite, null, Tier4Index, 4f, xmlMerchandise: true, itemValue: WorthAPrize));
    }

    [TestMethod]
    public void Fits_Tier3Weapon_RegularBandOnly()
    {
        Assert.IsTrue(TournamentPrizeRules.Fits(PrizeBand.Regular, null, Tier3Index, 3f, xmlMerchandise: true, itemValue: WorthAPrize));
        Assert.IsFalse(TournamentPrizeRules.Fits(PrizeBand.Elite, null, Tier3Index, 3f, xmlMerchandise: true, itemValue: WorthAPrize));
    }

    // --- Table membership from the gate service reaches Fits ---

    [TestMethod]
    public void GateToPrize_UntabledBodyArmourAtTier0_StaysUnderTheTierfFloor()
    {
        var gate = GateWith(
            new[] { new ArmourItemRecord("untabled_body", ArmourSlot.Body, 0, true, "gondor", 900) },
            new Dictionary<string, ArmourClassEntry>());

        var tableClass = gate.GetTableClass("untabled_body");

        Assert.AreEqual(ArmourClass.Light, gate.GetClass("untabled_body"), "the gate classes it by engine tier");
        Assert.IsNull(tableClass);
        Assert.IsFalse(TournamentPrizeRules.Fits(PrizeBand.Regular, tableClass, Tier1Index, 0.8f, xmlMerchandise: true, itemValue: 900));
    }

    [TestMethod]
    public void GateToPrize_TabledBodyArmourAtTier0_IsJudgedByClassAndValue()
    {
        var gate = GateWith(
            new[] { new ArmourItemRecord("tabled_body", ArmourSlot.Body, 0, true, "gondor", 900) },
            new Dictionary<string, ArmourClassEntry> { ["tabled_body"] = new("tabled_body", ArmourClass.Light, null) });

        var tableClass = gate.GetTableClass("tabled_body");

        Assert.AreEqual(ArmourClass.Light, tableClass);
        Assert.IsTrue(TournamentPrizeRules.Fits(PrizeBand.Regular, tableClass, Tier1Index, 0.8f, xmlMerchandise: true, itemValue: 900));
    }

    [TestMethod]
    public void GateToPrize_NamedWeaponOutsideTheTable_IsNeverAPrize()
    {
        var gate = GateWith(
            new[] { new ArmourItemRecord("anduril", ArmourSlot.None, 3, true, "gondor", 50000) },
            new Dictionary<string, ArmourClassEntry>());

        var tableClass = gate.GetTableClass("anduril");

        Assert.AreEqual(ArmourClass.Named, tableClass);
        Assert.IsFalse(TournamentPrizeRules.Fits(PrizeBand.Elite, tableClass, Tier4Index, 4f, xmlMerchandise: true, itemValue: 50000));
        Assert.IsFalse(TournamentPrizeRules.Fits(PrizeBand.Regular, tableClass, Tier3Index, 3f, xmlMerchandise: true, itemValue: 50000));
    }

    private static ArmourGateService GateWith(ArmourItemRecord[] items, Dictionary<string, ArmourClassEntry> entries)
    {
        var catalog = Substitute.For<IArmourItemCatalogAdapter>();
        catalog.CanWriteMerchandise.Returns(true);
        catalog.ReadItems().Returns(items);
        var table = Substitute.For<IArmourClassTableProvider>();
        table.GetEntries().Returns(entries);
        var config = Substitute.For<IArmourAcquisitionConfigProvider>();
        config.GetConfig().Returns(ArmourAcquisitionConfig.Default);
        var settings = Substitute.For<IArmourAcquisitionSettingsProvider>();
        settings.IsEnabled.Returns(true);
        var gate = new ArmourGateService(catalog, table, config, settings, Substitute.For<IModLogger>());
        gate.ApplyGating(isCampaign: true);
        return gate;
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

    // --- AdvertisedBand: the band the alternatives at Join are drawn from ---

    [DataTestMethod]
    // The table's class decides, whatever the engine tier says.
    [DataRow(ArmourClass.Light, Tier4Index, PrizeBand.Regular)]
    [DataRow(ArmourClass.Medium, Tier4Index, PrizeBand.Regular)]
    [DataRow(ArmourClass.Civilian, Tier4Index, PrizeBand.Regular)]
    [DataRow(ArmourClass.Heavy, Tier3Index, PrizeBand.Elite)]
    // Above heavy draws the elite band too (Mike, 2026-10-04): a save from before the pools were capped at heavy,
    // or vanilla's fallback list when the elite pool is empty, can advertise such a prize, and its alternatives
    // should sit near it.
    [DataRow(ArmourClass.Elite, Tier4Index, PrizeBand.Elite)]
    [DataRow(ArmourClass.Lord, Tier4Index, PrizeBand.Elite)]
    [DataRow(ArmourClass.Named, Tier4Index, PrizeBand.Elite)]
    // No table class (a weapon, shield or harness): the engine tier implies one.
    [DataRow(null, Tier3Index, PrizeBand.Regular)]
    [DataRow(null, Tier4Index, PrizeBand.Elite)]
    [DataRow(null, Tier5Index, PrizeBand.Elite)]
    public void AdvertisedBand_ByClassAndTier_PicksTheBand(ArmourClass? tableClass, int engineTierIndex, PrizeBand expected)
    {
        Assert.AreEqual(expected, TournamentPrizeRules.AdvertisedBand(tableClass, engineTierIndex));
    }

    [DataTestMethod]
    [DataRow(ArmourClass.Heavy)]
    [DataRow(null)]
    public void AdvertisedBand_UnknownTier_DrawsTheRegularBand(ArmourClass? tableClass)
    {
        // No tier was read (null), so the prize cannot be called heavy, whatever the table says.
        Assert.AreEqual(PrizeBand.Regular, TournamentPrizeRules.AdvertisedBand(tableClass, engineTierIndex: null));
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

    // --- PickChoices: the three prizes offered at Join ---

    private static readonly string[] Pool = { "a", "b", "c", "d", "e", "f", "g" };

    [TestMethod]
    public void PickChoices_ReturnsThreeDistinctItemsIncludingTheAdvertisedPrize()
    {
        var picks = TournamentPrizeRules.PickChoices(Pool, "d", "town_G1:1234");

        Assert.AreEqual(3, picks.Count);
        Assert.AreEqual(3, picks.Distinct().Count());
        Assert.AreEqual("d", picks[0], "the advertised prize is listed first");
        Assert.IsTrue(picks.All(Pool.Contains));
    }

    [TestMethod]
    public void PickChoices_SameSeed_SameThree()
    {
        // Reopening the join menu must not re-roll the alternatives.
        CollectionAssert.AreEqual(
            TournamentPrizeRules.PickChoices(Pool, "d", "town_G1:1234").ToList(),
            TournamentPrizeRules.PickChoices(Pool.Reverse().ToArray(), "d", "town_G1:1234").ToList());
    }

    [TestMethod]
    public void PickChoices_DifferentTournament_UsuallyDifferentAlternatives()
    {
        var seen = new System.Collections.Generic.HashSet<string>();
        for (var i = 0; i < 20; i++)
            seen.Add(string.Join(",", TournamentPrizeRules.PickChoices(Pool, "d", "town_G1:" + i).Skip(1)));

        Assert.IsTrue(seen.Count > 1, "the seed must vary the alternatives between tournaments");
    }

    [TestMethod]
    public void PickChoices_AdvertisedPrizeOutsideThePool_StillOfferedFirst()
    {
        // A banner prize, or vanilla's fallback list, is not in TAOM's band pool.
        var picks = TournamentPrizeRules.PickChoices(Pool, "banner_x", "s");

        Assert.AreEqual("banner_x", picks[0]);
        Assert.AreEqual(3, picks.Count);
    }

    [TestMethod]
    public void PickChoices_PoolSmallerThanThree_OffersWhatThereIs()
    {
        var picks = TournamentPrizeRules.PickChoices(new[] { "d", "e" }, "d", "s");

        CollectionAssert.AreEqual(new[] { "d", "e" }, picks.ToList());
    }

    [TestMethod]
    public void PickChoices_EmptyPool_OffersOnlyTheAdvertisedPrize()
    {
        CollectionAssert.AreEqual(new[] { "d" }, TournamentPrizeRules.PickChoices(new string[0], "d", "s").ToList());
    }

    [TestMethod]
    public void PickChoices_PoolWithDuplicates_NeverRepeatsAnItem()
    {
        var picks = TournamentPrizeRules.PickChoices(new[] { "e", "e", "e", "f" }, "d", "s");

        Assert.AreEqual(picks.Count, picks.Distinct().Count());
    }

    [TestMethod]
    public void PickChoices_NullOrEmptyIdsInPool_Skipped()
    {
        var picks = TournamentPrizeRules.PickChoices(new string[] { null!, "", "e", "f" }, "d", "s");

        CollectionAssert.AreEquivalent(new[] { "d", "e", "f" }, picks.ToList());
        Assert.AreEqual("d", picks[0]);
    }

    [TestMethod]
    public void PreferCulture_NothingFits_ReturnsEmpty()
    {
        var none = new (string Id, string? Culture)[0];

        Assert.AreEqual(0, TournamentPrizeRules.PreferCulture(none, "gondor", i => i.Culture).Count);
    }
}
