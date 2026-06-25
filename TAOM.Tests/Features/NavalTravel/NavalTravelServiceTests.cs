using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.NavalTravel;

namespace TAOM.Tests.Features.NavalTravel;

[TestClass]
public class NavalTravelServiceTests
{
    private INavalTravelSettingsProvider _settings = null!;
    private NavalTravelService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<INavalTravelSettingsProvider>();
        _settings.IsEnabled.Returns(true);
        _settings.ApplyToPlayer.Returns(true);
        _settings.ApplyToAi.Returns(true);
        _settings.EmbarkThresholdDistance.Returns(0.5f);
        _settings.NavalTerrainTypeIds.Returns(new[] { 8, 10, 11, 18, 19, 23, 24, 25 });
        _settings.RenderBoatVisual.Returns(true);
        _settings.BoatMeshName.Returns("boat_sail_on");
        _settings.BoatScale.Returns(0.4f);
        _sut = new NavalTravelService(_settings);
    }

    // --- CanPartySail: one row per (master, player, ai, isPlayerParty) decision cell ---
    [DataTestMethod]
    [DataRow(false, true, true, true, false)]   // master off → no one sails
    [DataRow(false, true, true, false, false)]
    [DataRow(true, true, true, true, true)]     // all on → player sails
    [DataRow(true, true, true, false, true)]    // all on → AI sails
    [DataRow(true, false, true, true, false)]   // player gate off → player can't
    [DataRow(true, false, true, false, true)]   // player off, AI on → AI independent
    [DataRow(true, true, false, true, true)]    // AI off, player on → player independent
    [DataRow(true, true, false, false, false)]  // AI gate off → AI can't
    public void CanPartySail_MatchesGateMatrix(bool master, bool player, bool ai, bool isPlayer, bool expected)
    {
        _settings.IsEnabled.Returns(master);
        _settings.ApplyToPlayer.Returns(player);
        _settings.ApplyToAi.Returns(ai);

        Assert.AreEqual(expected, _sut.CanPartySail(isPlayer));
    }

    // --- HasNavalCapability: full gate incl. at-sea grace + army-leader inheritance ---
    // Params: master, applyToPlayer, applyToAi, isMainParty, isAtSea, attachedLeaderCanSail, expected
    [DataTestMethod]
    // At-sea grace: a party already sailing keeps capability regardless of toggles (reach land, never strand).
    [DataRow(false, false, false, false, true, false, true)]  // disabled, AI party, AT SEA → still capable
    [DataRow(true, false, false, true, true, false, true)]    // enabled, player-gate off, main, AT SEA mid-voyage → capable
    // Disabled on land → no capability (explicit disabled gate even if a leader flag leaks true).
    [DataRow(false, true, true, true, false, false, false)]   // disabled, main, on land → false
    [DataRow(false, true, true, false, false, true, false)]   // disabled, attached, on land, leader-flag true → false
    // Enabled, player gate (main party never inherits from a leader).
    [DataRow(true, true, true, true, false, false, true)]     // main, player on → true
    [DataRow(true, false, true, true, false, true, false)]    // main, player off, leader can sail → false (no inheritance for main)
    // Enabled, AI gate + army-leader inheritance for attached parties.
    [DataRow(true, true, false, false, false, true, true)]    // attached, AI off, leader can sail → true (inheritance)
    [DataRow(true, true, false, false, false, false, false)]  // attached, AI off, no capable leader → false
    [DataRow(true, true, true, false, false, false, true)]    // attached, AI on → true (own gate)
    public void HasNavalCapability_MatchesGateMatrix(
        bool master, bool player, bool ai, bool isMain, bool isAtSea, bool leaderCanSail, bool expected)
    {
        _settings.IsEnabled.Returns(master);
        _settings.ApplyToPlayer.Returns(player);
        _settings.ApplyToAi.Returns(ai);

        Assert.AreEqual(expected, _sut.HasNavalCapability(isMain, isAtSea, leaderCanSail));
    }

    // --- IsNavalTerrain: membership of the configured water-terrain set ---
    [DataTestMethod]
    [DataRow(8, true)]    // Lake
    [DataRow(10, true)]   // Water
    [DataRow(11, true)]   // River
    [DataRow(18, true)]   // CoastalSea
    [DataRow(19, true)]   // OpenSea
    [DataRow(1, false)]   // Plain
    [DataRow(4, false)]   // Forest
    [DataRow(7, false)]   // Mountain
    [DataRow(20, false)]  // Beach (not in default naval set)
    [DataRow(999, false)] // unknown id
    public void IsNavalTerrain_MatchesConfiguredSet(int terrainId, bool expected)
    {
        Assert.AreEqual(expected, _sut.IsNavalTerrain(terrainId));
    }

    [TestMethod]
    public void IsNavalTerrain_NullSet_ReturnsFalse()
    {
        // Terrain ids are snapshotted at construction, so build a service whose settings expose no set.
        var settings = Substitute.For<INavalTravelSettingsProvider>();
        settings.NavalTerrainTypeIds.Returns((IReadOnlyList<int>)null!);
        var sut = new NavalTravelService(settings);

        Assert.IsFalse(sut.IsNavalTerrain(10));
    }

    [TestMethod]
    public void IsEnabled_PassesThroughSettings()
    {
        _settings.IsEnabled.Returns(false);
        Assert.IsFalse(_sut.IsEnabled);

        _settings.IsEnabled.Returns(true);
        Assert.IsTrue(_sut.IsEnabled);
    }

    [TestMethod]
    public void EmbarkThresholdDistance_PassesThroughSettings()
    {
        _settings.EmbarkThresholdDistance.Returns(1.25f);

        Assert.AreEqual(1.25f, _sut.EmbarkThresholdDistance);
    }

    // --- ShouldSuppressAtSeaLandRescue: Patch57 skips the crashing vanilla at-sea→land rescue tick
    // (native AV on TAOM_Map, #120) whenever the feature is enabled; runs vanilla when off. ---
    [DataTestMethod]
    [DataRow(true, true)]    // feature enabled → suppress (a party can be at sea → the native pathfind would AV)
    [DataRow(false, false)]  // feature off → nothing is ever at sea → behavior inert → leave vanilla running
    public void ShouldSuppressAtSeaLandRescue_MatchesEnabledGate(bool enabled, bool expected)
    {
        _settings.IsEnabled.Returns(enabled);

        Assert.AreEqual(expected, _sut.ShouldSuppressAtSeaLandRescue);
    }

    // --- ShouldRenderBoat: boat icon shown only when enabled + render-on + at sea + not transitioning ---
    [DataTestMethod]
    [DataRow(true, true, true, false, true)]    // enabled, render-on, at sea, not transitioning → boat
    [DataRow(true, true, true, true, false)]    // transitioning → no boat (base shows the figure then)
    [DataRow(true, true, false, false, false)]  // not at sea → no boat
    [DataRow(false, true, true, false, false)]  // master off → no boat
    [DataRow(true, false, true, false, false)]  // render-visual toggle off → no boat (movement still works)
    public void ShouldRenderBoat_MatchesMatrix(bool master, bool render, bool atSea, bool inTransition, bool expected)
    {
        _settings.IsEnabled.Returns(master);
        _settings.RenderBoatVisual.Returns(render);

        Assert.AreEqual(expected, _sut.ShouldRenderBoat(atSea, inTransition));
    }

    [TestMethod]
    public void BoatMeshName_And_BoatScale_PassThroughSettings()
    {
        _settings.BoatMeshName.Returns("custom_boat");
        _settings.BoatScale.Returns(0.7f);

        Assert.AreEqual("custom_boat", _sut.BoatMeshName);
        Assert.AreEqual(0.7f, _sut.BoatScale);
    }
}
