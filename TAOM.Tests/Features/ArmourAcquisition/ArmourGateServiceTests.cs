using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.ArmourAcquisition;
using TAOM.Features.ArmourAcquisition.Domain;

namespace TAOM.Tests.Features.ArmourAcquisition;

/// <summary>
/// The gate service runs at every game init and owns this game's classes. In a campaign with the feature on
/// it marks heavy, elite, lord and named pieces NotMerchandise (the engine's own switch for workshops, loot,
/// prizes, plunder and hideouts) and answers the marketplace's per-town question; with it off, outside a
/// campaign, or when the engine setter is gone, it changes nothing and every market behaves as before.
/// </summary>
[TestClass]
public class ArmourGateServiceTests
{
    private IArmourItemCatalogAdapter _catalog = null!;
    private IArmourClassTableProvider _table = null!;
    private IArmourAcquisitionConfigProvider _config = null!;
    private IArmourAcquisitionSettingsProvider _settings = null!;
    private IModLogger _logger = null!;
    private ArmourGateService _service = null!;

    private static ArmourItemRecord Armour(string id, int tier = 0, bool merch = true, string culture = "gondor",
        ArmourSlot slot = ArmourSlot.Body, int value = 100) =>
        new(id, slot, tier, merch, culture, value);

    [TestInitialize]
    public void Setup()
    {
        _catalog = Substitute.For<IArmourItemCatalogAdapter>();
        _catalog.CanWriteMerchandise.Returns(true);
        _catalog.SetNotMerchandise(Arg.Any<string>(), Arg.Any<bool>()).Returns(true);
        _table = Substitute.For<IArmourClassTableProvider>();
        _config = Substitute.For<IArmourAcquisitionConfigProvider>();
        _config.GetConfig().Returns(ArmourAcquisitionConfig.Default);
        _settings = Substitute.For<IArmourAcquisitionSettingsProvider>();
        _settings.IsEnabled.Returns(true);
        _logger = Substitute.For<IModLogger>();
        _service = new ArmourGateService(_catalog, _table, _config, _settings, _logger);
    }

    private void Given(ArmourItemRecord[] items, params (string id, ArmourClass cls, string? next)[] rows)
    {
        _catalog.ReadItems().Returns(items);
        _table.GetEntries().Returns(rows.ToDictionary(r => r.id, r => new ArmourClassEntry(r.id, r.cls, r.next), StringComparer.Ordinal));
    }

    [TestMethod]
    public void ApplyGating_Enabled_MarksOnlyGatedPiecesNotMerchandise()
    {
        Given(new[] { Armour("light_a"), Armour("heavy_a") }, ("light_a", ArmourClass.Light, null), ("heavy_a", ArmourClass.Heavy, null));

        _service.ApplyGating(isCampaign: true);

        _catalog.Received(1).SetNotMerchandise("heavy_a", true);
        _catalog.DidNotReceive().SetNotMerchandise("light_a", Arg.Any<bool>());
        Assert.IsTrue(_service.IsActive);
    }

    [TestMethod]
    public void ApplyGating_Disabled_ChangesNothingAndIsInactive()
    {
        _settings.IsEnabled.Returns(false);
        Given(new[] { Armour("heavy_a") }, ("heavy_a", ArmourClass.Heavy, null));

        _service.ApplyGating(isCampaign: true);

        _catalog.DidNotReceive().SetNotMerchandise(Arg.Any<string>(), Arg.Any<bool>());
        Assert.IsFalse(_service.IsActive);
        Assert.AreEqual(ArmourClass.Heavy, _service.GetClass("heavy_a"), "classes stay readable with gating off");
    }

    [TestMethod]
    public void ApplyGating_NotACampaign_ReadsNothingAndClearsThePreviousGame()
    {
        Given(new[] { Armour("heavy_a") }, ("heavy_a", ArmourClass.Heavy, null));
        _service.ApplyGating(isCampaign: true);
        _catalog.ClearReceivedCalls();

        _service.ApplyGating(isCampaign: false);

        _catalog.DidNotReceive().ReadItems();
        _catalog.DidNotReceive().SetNotMerchandise(Arg.Any<string>(), Arg.Any<bool>());
        Assert.IsFalse(_service.IsActive);
        Assert.IsNull(_service.GetClass("heavy_a"));
    }

    [TestMethod]
    public void ApplyGating_SetterMissing_StaysInactiveAndLogsAnError()
    {
        _catalog.CanWriteMerchandise.Returns(false);
        Given(new[] { Armour("heavy_a") }, ("heavy_a", ArmourClass.Heavy, null));

        _service.ApplyGating(isCampaign: true);

        Assert.IsFalse(_service.IsActive);
        _catalog.DidNotReceive().SetNotMerchandise(Arg.Any<string>(), Arg.Any<bool>());
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("NotMerchandise")));
    }

    [TestMethod]
    public void ApplyGating_CatalogThrows_DoesNotThrowAndStaysInactive()
    {
        _catalog.ReadItems().Throws(new InvalidOperationException("no object manager"));

        _service.ApplyGating(isCampaign: true);

        Assert.IsFalse(_service.IsActive);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("no object manager")));
    }

    [TestMethod]
    public void ApplyGating_FailsAfterFlipping_RestoresTheFlippedPiecesAndStaysInactive()
    {
        // Half a gate (pieces out of loot, markets ungated) is worse than none.
        Given(new[] { Armour("heavy_a"), Armour("elite_a") }, ("heavy_a", ArmourClass.Heavy, null), ("elite_a", ArmourClass.Elite, null));
        _logger.When(l => l.LogInfo(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("boom"));

        _service.ApplyGating(isCampaign: true);

        _catalog.Received(1).SetNotMerchandise("heavy_a", false);
        _catalog.Received(1).SetNotMerchandise("elite_a", false);
        Assert.IsFalse(_service.IsActive);
        Assert.IsNull(_service.GetClass("heavy_a"));
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("2 of 2 flipped piece(s) restored")));
    }

    [TestMethod]
    public void ApplyGating_SecondGame_RebuildsFromTheNewCatalog()
    {
        Given(new[] { Armour("heavy_a") }, ("heavy_a", ArmourClass.Heavy, null));
        _service.ApplyGating(isCampaign: true);

        _settings.IsEnabled.Returns(false);
        Given(new[] { Armour("other_b") }, ("other_b", ArmourClass.Elite, null));
        _service.ApplyGating(isCampaign: true);

        Assert.IsFalse(_service.IsActive);
        Assert.IsNull(_service.GetClass("heavy_a"), "the previous game's items are gone");
        Assert.AreEqual(ArmourClass.Elite, _service.GetClass("other_b"));
    }

    [TestMethod]
    public void ApplyGating_LogsASummaryWithTheCounts()
    {
        Given(new[] { Armour("heavy_a"), Armour("elite_a") }, ("heavy_a", ArmourClass.Heavy, null), ("elite_a", ArmourClass.Elite, null));

        _service.ApplyGating(isCampaign: true);

        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("2 piece(s)") && s.Contains("heavy 1") && s.Contains("elite 1")));
    }

    [TestMethod]
    public void ApplyGating_StaleTableRows_AreWarned()
    {
        Given(new[] { Armour("a") }, ("a", ArmourClass.Light, null), ("retired_b", ArmourClass.Heavy, null));

        _service.ApplyGating(isCampaign: true);

        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("retired_b")));
    }

    [TestMethod]
    public void GetNext_ReturnsTheTableLink()
    {
        Given(new[] { Armour("a"), Armour("b") }, ("a", ArmourClass.Medium, "b"), ("b", ArmourClass.Heavy, null));
        _service.ApplyGating(isCampaign: true);

        Assert.AreEqual("b", _service.GetNext("a"));
        Assert.IsNull(_service.GetNext("b"));
        Assert.IsNull(_service.GetNext("unknown"));
    }

    [TestMethod]
    public void GetNext_LinkToAnUnloadedPiece_IsNull()
    {
        Given(new[] { Armour("a") }, ("a", ArmourClass.Medium, "retired_b"));
        _service.ApplyGating(isCampaign: true);

        Assert.IsNull(_service.GetNext("a"));
    }

    [TestMethod]
    public void GetPieces_FiltersByClassAndCulture_SortedById()
    {
        Given(new[] { Armour("z_lord", culture: "gondor"), Armour("a_lord", culture: "gondor"), Armour("m_lord", culture: "mordor"), Armour("e", culture: "gondor") },
            ("z_lord", ArmourClass.Lord, null), ("a_lord", ArmourClass.Lord, null), ("m_lord", ArmourClass.Lord, null), ("e", ArmourClass.Elite, null));
        _service.ApplyGating(isCampaign: true);

        CollectionAssert.AreEqual(new[] { "a_lord", "z_lord" }, _service.GetPieces(ArmourClass.Lord, "gondor").ToArray());
        Assert.AreEqual(3, _service.GetPieces(ArmourClass.Lord, null).Count);
    }

    [TestMethod]
    public void GetPieces_NeverAPieceClassedOnlyByItsEngineTier()
    {
        // A vanilla Calradian piece the table does not list is classed by engine tier for the gate, but it is
        // no culture's lord kit.
        Given(new[] { Armour("table_elite", culture: "battania"), Armour("battania_noble_armor", tier: 5, culture: "battania") },
            ("table_elite", ArmourClass.Elite, null));
        _service.ApplyGating(isCampaign: true);

        Assert.AreEqual(ArmourClass.Elite, _service.GetClass("battania_noble_armor"), "the gate still governs it");
        CollectionAssert.AreEqual(new[] { "table_elite" }, _service.GetPieces(ArmourClass.Elite, "battania").ToArray());
    }

    [TestMethod]
    public void GetPieces_WithASlot_ListsOnlyThePiecesWornThere()
    {
        // The lord's gear ladder (#693) awards one slot per rung.
        Given(new[] { Armour("helm_lord", slot: ArmourSlot.Head), Armour("gauntlet_lord", slot: ArmourSlot.Hand), Armour("chest_lord") },
            ("helm_lord", ArmourClass.Lord, null), ("gauntlet_lord", ArmourClass.Lord, null), ("chest_lord", ArmourClass.Lord, null));
        _service.ApplyGating(isCampaign: true);

        CollectionAssert.AreEqual(new[] { "helm_lord" }, _service.GetPieces(ArmourClass.Lord, "gondor", ArmourSlot.Head).ToArray());
        Assert.AreEqual(3, _service.GetPieces(ArmourClass.Lord, "gondor", null).Count, "no slot means any slot");
        Assert.AreEqual(0, _service.GetPieces(ArmourClass.Lord, "gondor", ArmourSlot.None).Count,
            "None is the slot of a piece that is not character armour, never a wildcard");
    }

    [TestMethod]
    public void GetName_AsksTheCatalog()
    {
        _catalog.GetName("heavy_a").Returns("Fountain Guard Armour");

        Assert.AreEqual("Fountain Guard Armour", _service.GetName("heavy_a"));
    }

    [TestMethod]
    public void IsEligibleForMarket_Inactive_IsAlwaysTrue()
    {
        _settings.IsEnabled.Returns(false);
        Given(new[] { Armour("lord_a") }, ("lord_a", ArmourClass.Lord, null));
        _service.ApplyGating(isCampaign: true);

        Assert.IsTrue(_service.IsEligibleForMarket("lord_a", 0));
    }

    [DataTestMethod]
    [DataRow(ArmourClass.Light, 0, true)]
    [DataRow(ArmourClass.Medium, 0, true)]
    [DataRow(ArmourClass.Civilian, 0, true)]
    [DataRow(ArmourClass.Heavy, 0, false)]
    [DataRow(ArmourClass.Heavy, 1, true)]
    [DataRow(ArmourClass.Elite, 1, false)]
    [DataRow(ArmourClass.Elite, 2, true)]
    [DataRow(ArmourClass.Lord, 2, false)]
    [DataRow(ArmourClass.Lord, 3, true)]
    [DataRow(ArmourClass.Named, 3, false)]
    public void IsEligibleForMarket_Active_FollowsTheClassGate(ArmourClass cls, int townLevel, bool expected)
    {
        // Mike, 2026-09-27: heavy 1, elite and heavy 2, lord and all 3; named never.
        Given(new[] { Armour("p") }, ("p", cls, null));
        _service.ApplyGating(isCampaign: true);

        Assert.AreEqual(expected, _service.IsEligibleForMarket("p", townLevel));
    }

    [TestMethod]
    public void IsEligibleForMarket_Active_ItemTheXmlMarkedNotMerchandise_NeverQualifies()
    {
        // The ranged ladders and starter kits are is_merchandise="false" to stay out of shops.
        Given(new[] { Armour("starter_x", merch: false), new ArmourItemRecord("ladder_bow", ArmourSlot.None, 3, false, "gondor", 50) },
            ("starter_x", ArmourClass.Light, null));
        _service.ApplyGating(isCampaign: true);

        Assert.IsFalse(_service.IsEligibleForMarket("starter_x", 3));
        Assert.IsFalse(_service.IsEligibleForMarket("ladder_bow", 3));
    }

    [TestMethod]
    public void IsEligibleForMarket_Active_LoadedMerchandiseTheGateDoesNotGovern_IsTrue()
    {
        Given(new[] { Armour("a"), new ArmourItemRecord("grain", ArmourSlot.None, 0, true, null, 10) }, ("a", ArmourClass.Light, null));
        _service.ApplyGating(isCampaign: true);

        Assert.IsTrue(_service.IsEligibleForMarket("grain", 0));
    }

    [TestMethod]
    public void IsEligibleForMarket_Active_UnknownItem_IsTrue()
    {
        Given(new[] { Armour("a") }, ("a", ArmourClass.Light, null));
        _service.ApplyGating(isCampaign: true);

        Assert.IsTrue(_service.IsEligibleForMarket("not_loaded", 0));
    }

    [TestMethod]
    public void IsEligibleForMarket_BeforeAnyGameInit_IsTrue()
    {
        Assert.IsTrue(_service.IsEligibleForMarket("anything", 0));
    }
}
