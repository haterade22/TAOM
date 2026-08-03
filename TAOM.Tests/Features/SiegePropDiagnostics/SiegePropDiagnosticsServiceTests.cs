using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.SiegePropDiagnostics;
using TAOM.Features.SiegePropDiagnostics.Models;

namespace TAOM.Tests.Features.SiegePropDiagnostics;

[TestClass]
public class SiegePropDiagnosticsServiceTests
{
    private ISiegePropDiagnosticsSettingsProvider _settings = null!;
    private SiegePropDiagnosticsService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<ISiegePropDiagnosticsSettingsProvider>();
        _settings.IsEnabled.Returns(true);
        _settings.IsVerbose.Returns(true);
        _sut = new SiegePropDiagnosticsService(_settings);
    }

    /// <summary>A pile that is working: item resolves, ammo left, points active, probe succeeded.</summary>
    private static SiegePropSnapshot HealthyPile() => new()
    {
        Id = 1,
        Kind = SiegePropKind.RockPile,
        ScriptType = "StonePile",
        EntityName = "throwable_rock_pile",
        GivenItemId = "boulder",
        GivenItemResolves = true,
        AmmoCount = 12,
        StartingAmmoCount = 12,
        StandingPointCount = 10,
        AmmoPickupPointCount = 8,
        PlayerProbeValid = true,
        NearestPointDistanceSquared = 1.0f,
        InteractionDistanceSquared = 4.0f,
        NearestGroundHeightDelta = 0.2f,
    };

    private static SiegePropSnapshot HealthyBarrel() => new()
    {
        Id = 2,
        Kind = SiegePropKind.AmmoBarrel,
        ScriptType = "ArrowBarrel",
        EntityName = "arrow_barrel",
        GivenItemId = null,
        GivenItemResolves = false,
        StandingPointCount = 1,
        AmmoPickupPointCount = 0,
        PlayerProbeValid = true,
        NearestPointDistanceSquared = 1.0f,
        InteractionDistanceSquared = 4.0f,
        NearestGroundHeightDelta = 0.1f,
    };

    // ---------------------------------------------------------------- healthy

    [TestMethod]
    public void Diagnose_ProbeSucceeded_ReturnsHealthy()
    {
        var result = _sut.Diagnose(HealthyPile());

        Assert.AreEqual(SiegePropDiagnosis.Healthy, result);
    }

    [TestMethod]
    public void Diagnose_BarrelWithNoAmmoPickupPoints_ReturnsHealthy()
    {
        // Vanilla's arrow_barrel prefab tags none of its points `ammopickup`; AmmoBarrelBase
        // iterates every StandingPoint instead. Flagging that would be a false positive.
        var result = _sut.Diagnose(HealthyBarrel());

        Assert.AreEqual(SiegePropDiagnosis.Healthy, result);
    }

    [TestMethod]
    public void Diagnose_BarrelWithNoGivenItem_ReturnsHealthy()
    {
        // Barrels hand out no item at all, so an unresolvable id is meaningless for them.
        var snapshot = HealthyBarrel();
        snapshot.GivenItemId = null;
        snapshot.GivenItemResolves = false;

        Assert.AreEqual(SiegePropDiagnosis.Healthy, _sut.Diagnose(snapshot));
    }

    // ------------------------------------------------------------ scene faults

    [TestMethod]
    public void Diagnose_NoStandingPoints_ReturnsNoStandingPoints()
    {
        var snapshot = HealthyPile();
        snapshot.StandingPointCount = 0;
        snapshot.AmmoPickupPointCount = 0;
        snapshot.PlayerProbeValid = false;

        Assert.AreEqual(SiegePropDiagnosis.NoStandingPoints, _sut.Diagnose(snapshot));
    }

    [TestMethod]
    public void Diagnose_RockPileWithoutAmmoPickupPoints_ReturnsNoAmmoPickupPoints()
    {
        var snapshot = HealthyPile();
        snapshot.AmmoPickupPointCount = 0;
        snapshot.PlayerProbeValid = false;

        Assert.AreEqual(SiegePropDiagnosis.NoAmmoPickupPoints, _sut.Diagnose(snapshot));
    }

    [TestMethod]
    public void Diagnose_RockPileWithUnresolvableItemId_ReturnsItemIdUnresolved()
    {
        // The boulder_carry class of fault: silently disables the pile for player and AI alike.
        var snapshot = HealthyPile();
        snapshot.GivenItemId = "boulder_carry";
        snapshot.GivenItemResolves = false;
        snapshot.PlayerProbeValid = false;

        Assert.AreEqual(SiegePropDiagnosis.ItemIdUnresolved, _sut.Diagnose(snapshot));
    }

    [TestMethod]
    public void Diagnose_UnresolvableItemIdTakesPrecedenceOverProbeSuccess()
    {
        // A pile cannot really be usable with a null given item; trust the data fault over the probe.
        var snapshot = HealthyPile();
        snapshot.GivenItemResolves = false;
        snapshot.PlayerProbeValid = true;

        Assert.AreEqual(SiegePropDiagnosis.ItemIdUnresolved, _sut.Diagnose(snapshot));
    }

    // ------------------------------------------------------------- prop state

    [TestMethod]
    public void Diagnose_MachineDisabled_ReturnsMachineDisabled()
    {
        var snapshot = HealthyPile();
        snapshot.MachineIsDisabled = true;
        snapshot.PlayerProbeValid = false;

        Assert.AreEqual(SiegePropDiagnosis.MachineDisabled, _sut.Diagnose(snapshot));
    }

    [TestMethod]
    public void Diagnose_RockPileOutOfBoulders_ReturnsAmmoExhausted()
    {
        var snapshot = HealthyPile();
        snapshot.AmmoCount = 0;
        snapshot.DeactivatedPointCount = 8;
        snapshot.PlayerProbeValid = false;

        Assert.AreEqual(SiegePropDiagnosis.AmmoExhausted, _sut.Diagnose(snapshot));
    }

    [TestMethod]
    public void Diagnose_AllPointsDeactivatedWithAmmoRemaining_ReturnsAllPointsDeactivated()
    {
        var snapshot = HealthyPile();
        snapshot.AmmoCount = 5;
        snapshot.DeactivatedPointCount = 8;
        snapshot.PlayerProbeValid = false;

        Assert.AreEqual(SiegePropDiagnosis.AllPointsDeactivated, _sut.Diagnose(snapshot));
    }

    [TestMethod]
    public void Diagnose_BarrelIsNeverAmmoExhausted()
    {
        // AmmoBarrelBase has no ammo counter at all; a zero here is just an unset field.
        var snapshot = HealthyBarrel();
        snapshot.AmmoCount = 0;
        snapshot.PlayerProbeValid = false;
        snapshot.DisabledForPlayerPointCount = 1;

        Assert.AreEqual(SiegePropDiagnosis.AllPointsDisabledForPlayer, _sut.Diagnose(snapshot));
    }

    // ------------------------------------------------------------ player side

    [TestMethod]
    public void Diagnose_PlayerMounted_ReturnsPlayerMounted()
    {
        var snapshot = HealthyPile();
        snapshot.PlayerIsMounted = true;
        snapshot.PlayerProbeValid = false;
        snapshot.DisabledForPlayerPointCount = 8;

        Assert.AreEqual(SiegePropDiagnosis.PlayerMounted, _sut.Diagnose(snapshot));
    }

    [TestMethod]
    public void Diagnose_AllPointsDisabledForPlayer_ReturnsAllPointsDisabledForPlayer()
    {
        var snapshot = HealthyPile();
        snapshot.PlayerProbeValid = false;
        snapshot.DisabledForPlayerPointCount = 8;

        Assert.AreEqual(SiegePropDiagnosis.AllPointsDisabledForPlayer, _sut.Diagnose(snapshot));
    }

    [TestMethod]
    public void Diagnose_AllPointsOccupied_ReturnsAllPointsOccupied()
    {
        var snapshot = HealthyPile();
        snapshot.PlayerProbeValid = false;
        snapshot.OccupiedPointCount = 8;

        Assert.AreEqual(SiegePropDiagnosis.AllPointsOccupied, _sut.Diagnose(snapshot));
    }

    [TestMethod]
    public void Diagnose_GroundHeightDeltaAtOrAboveThreshold_ReturnsGroundHeightMismatch()
    {
        var snapshot = HealthyPile();
        snapshot.PlayerProbeValid = false;
        snapshot.NearestGroundHeightDelta = 1.5f;

        Assert.AreEqual(SiegePropDiagnosis.GroundHeightMismatch, _sut.Diagnose(snapshot));
    }

    [TestMethod]
    public void Diagnose_PlayerBeyondInteractionDistance_ReturnsPlayerOutOfRange()
    {
        var snapshot = HealthyPile();
        snapshot.PlayerProbeValid = false;
        snapshot.NearestPointDistanceSquared = 25.0f;
        snapshot.InteractionDistanceSquared = 4.0f;

        Assert.AreEqual(SiegePropDiagnosis.PlayerOutOfRange, _sut.Diagnose(snapshot));
    }

    [TestMethod]
    public void Diagnose_ProbeFailedWithNoKnownCause_ReturnsUnknownProbeFailure()
    {
        var snapshot = HealthyPile();
        snapshot.PlayerProbeValid = false;

        Assert.AreEqual(SiegePropDiagnosis.UnknownProbeFailure, _sut.Diagnose(snapshot));
    }

    // ------------------------------------------------- NaN gates (engine floats)

    [TestMethod]
    public void Diagnose_NaNGroundHeightDelta_DoesNotReportGroundHeightMismatch()
    {
        // NaN >= 1.5 is false. Written as a positive requirement so a corrupt float cannot
        // silently pass a gate (csharp-architecture.md engine-float rule).
        var snapshot = HealthyPile();
        snapshot.PlayerProbeValid = false;
        snapshot.NearestGroundHeightDelta = float.NaN;

        Assert.AreEqual(SiegePropDiagnosis.UnknownProbeFailure, _sut.Diagnose(snapshot));
    }

    [TestMethod]
    public void Diagnose_NaNDistance_DoesNotReportOutOfRange()
    {
        var snapshot = HealthyPile();
        snapshot.PlayerProbeValid = false;
        snapshot.NearestPointDistanceSquared = float.NaN;
        snapshot.InteractionDistanceSquared = 4.0f;

        Assert.AreEqual(SiegePropDiagnosis.UnknownProbeFailure, _sut.Diagnose(snapshot));
    }

    [TestMethod]
    public void Diagnose_NullDistances_DoNotThrow()
    {
        var snapshot = HealthyPile();
        snapshot.PlayerProbeValid = false;
        snapshot.NearestPointDistanceSquared = null;
        snapshot.InteractionDistanceSquared = null;
        snapshot.NearestGroundHeightDelta = null;

        Assert.AreEqual(SiegePropDiagnosis.UnknownProbeFailure, _sut.Diagnose(snapshot));
    }

    // ----------------------------------------------------------------- report

    [TestMethod]
    public void BuildReport_Disabled_ReturnsEmpty()
    {
        _settings.IsEnabled.Returns(false);

        var lines = _sut.BuildReport("empire_town_h", true, new[] { HealthyPile() });

        Assert.AreEqual(0, lines.Count);
    }

    [TestMethod]
    public void BuildReport_NoPropsInScene_SaysSoExplicitly()
    {
        var lines = _sut.BuildReport("taom_rohan_castle_helms_deep_forceatmo", true, new SiegePropSnapshot[0]);

        Assert.IsTrue(lines.Any(l => l.Contains("no resupply props")),
            "Expected an explicit no-props line, got: " + string.Join(" | ", lines));
    }

    [TestMethod]
    public void BuildReport_CountsRockPilesAndBarrelsSeparately()
    {
        var lines = _sut.BuildReport("empire_town_h", true, new[] { HealthyPile(), HealthyBarrel() });

        Assert.IsTrue(lines.Any(l => l.Contains("rockPiles=1") && l.Contains("barrels=1")),
            "Expected a summary with per-kind counts, got: " + string.Join(" | ", lines));
    }

    [TestMethod]
    public void BuildReport_NotVerbose_StillReportsFaultyProps()
    {
        _settings.IsVerbose.Returns(false);
        var broken = HealthyPile();
        broken.GivenItemResolves = false;
        broken.PlayerProbeValid = false;

        var lines = _sut.BuildReport("empire_town_h", true, new[] { HealthyPile(), broken });

        Assert.IsTrue(lines.Any(l => l.Contains(nameof(SiegePropDiagnosis.ItemIdUnresolved))),
            "A fault must be reported even when not verbose: " + string.Join(" | ", lines));
        Assert.IsFalse(lines.Any(l => l.Contains(nameof(SiegePropDiagnosis.Healthy))),
            "Healthy props must be omitted when not verbose: " + string.Join(" | ", lines));
    }

    [TestMethod]
    public void BuildReport_Verbose_ReportsHealthyPropsToo()
    {
        var lines = _sut.BuildReport("empire_town_h", true, new[] { HealthyPile() });

        Assert.IsTrue(lines.Any(l => l.Contains(nameof(SiegePropDiagnosis.Healthy))),
            "Verbose mode should list healthy props: " + string.Join(" | ", lines));
    }

    [TestMethod]
    public void BuildReport_IncludesSceneNameAndSiegeFlag()
    {
        var lines = _sut.BuildReport("empire_town_h", true, new[] { HealthyPile() });

        Assert.IsTrue(lines.Any(l => l.Contains("empire_town_h") && l.Contains("siege=True")),
            "Expected scene and siege flag in the header: " + string.Join(" | ", lines));
    }

    [TestMethod]
    public void BuildReport_NonSiegeMission_StillReports()
    {
        // Piles exist in town scenes outside sieges too; the report should not silently skip them.
        var lines = _sut.BuildReport("empire_town_h", false, new[] { HealthyPile() });

        Assert.IsTrue(lines.Count > 0);
    }
}
