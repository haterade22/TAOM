using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Library;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.MixedFormations;
using TAOM.Features.MixedFormations.Models;

namespace TAOM.Tests.Features.MixedFormations;

[TestClass]
public class FormationLayoutServiceTests
{
    private IMixedFormationsSettingsProvider _settings = null!;
    private ILayoutPositioner _positioner = null!;
    private IModLogger _logger = null!;
    private FormationLayoutService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<IMixedFormationsSettingsProvider>();
        _positioner = new LayoutPositioner();
        _logger = Substitute.For<IModLogger>();

        _settings.IsEnabled.Returns(true);
        _settings.IsDebugMode.Returns(false);
        _settings.DefaultLayout.Returns(FormationLayoutType.InfantryFrontRangedBack);
        _settings.CycleHotkey.Returns("L");

        _sut = new FormationLayoutService(_settings, _positioner, _logger);
    }

    private static IFormationAdapter MakeFormation(int melee, int ranged,
        bool isHolding = true, bool orderValid = true, object? formationKey = null)
    {
        var f = Substitute.For<IFormationAdapter>();
        var units = new List<FormationUnit>();
        for (var i = 0; i < melee; i++) units.Add(new FormationUnit(i, isRanged: false));
        for (var i = 0; i < ranged; i++) units.Add(new FormationUnit(melee + i, isRanged: true));
        f.CountOfUnits.Returns(melee + ranged);
        f.Width.Returns(6f);
        f.Interval.Returns(1f);
        f.Units.Returns(units);
        f.IsHolding.Returns(isHolding);
        f.OrderPositionIsValid.Returns(orderValid);
        f.OrderPosition.Returns(new Vec2(100f, 200f));
        f.Direction.Returns(new Vec2(1f, 0f));
        f.FormationKey.Returns(formationKey ?? new object());
        return f;
    }

    // -------- ComputeUnitPlanePosition: gating paths --------

    [TestMethod]
    public void ComputeUnitPlanePosition_FeatureDisabled_ReturnsNull()
    {
        _settings.IsEnabled.Returns(false);
        var f = MakeFormation(8, 6);

        var result = _sut.ComputeUnitPlanePosition(f, agentIndex: 0, agentIsRanged: false);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void ComputeUnitPlanePosition_FormationNotHolding_ReturnsNull()
    {
        var f = MakeFormation(8, 6, isHolding: false);
        _sut.SetLayout(f, FormationLayoutType.InfantryFrontRangedBack);

        var result = _sut.ComputeUnitPlanePosition(f, agentIndex: 0, agentIsRanged: false);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void ComputeUnitPlanePosition_OrderPositionInvalid_ReturnsNull()
    {
        var f = MakeFormation(8, 6, orderValid: false);
        _sut.SetLayout(f, FormationLayoutType.InfantryFrontRangedBack);

        var result = _sut.ComputeUnitPlanePosition(f, agentIndex: 0, agentIsRanged: false);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void ComputeUnitPlanePosition_NoLayoutSet_ReturnsNull()
    {
        var f = MakeFormation(8, 6);
        // No SetLayout call → defaults to Vanilla → null

        var result = _sut.ComputeUnitPlanePosition(f, agentIndex: 0, agentIsRanged: false);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void ComputeUnitPlanePosition_LayoutVanilla_ReturnsNull()
    {
        var f = MakeFormation(8, 6);
        _sut.SetLayout(f, FormationLayoutType.Vanilla);

        var result = _sut.ComputeUnitPlanePosition(f, agentIndex: 0, agentIsRanged: false);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void ComputeUnitPlanePosition_AllConditionsMet_ReturnsTransformedPosition()
    {
        var f = MakeFormation(8, 6);
        _sut.SetLayout(f, FormationLayoutType.InfantryFrontRangedBack);

        var result = _sut.ComputeUnitPlanePosition(f, agentIndex: 0, agentIsRanged: false);

        Assert.IsNotNull(result);
        // The first melee unit at slot (0, -halfFiles) should be near OrderPosition (100, 200)
        Assert.IsTrue(result.Value.x is > 90f and < 110f, $"Expected near 100, got {result.Value.x}");
    }

    // -------- Layout get/set/cycle --------

    [TestMethod]
    public void GetLayout_NoSetLayout_ReturnsVanilla()
    {
        var f = MakeFormation(8, 6);

        Assert.AreEqual(FormationLayoutType.Vanilla, _sut.GetLayout(f));
    }

    [TestMethod]
    public void SetLayout_ThenGetLayout_ReturnsSetValue()
    {
        var f = MakeFormation(8, 6);

        _sut.SetLayout(f, FormationLayoutType.Checkerboard);

        Assert.AreEqual(FormationLayoutType.Checkerboard, _sut.GetLayout(f));
    }

    [TestMethod]
    public void SetLayout_DifferentFormations_TrackedIndependently()
    {
        var f1 = MakeFormation(8, 6, formationKey: new object());
        var f2 = MakeFormation(8, 6, formationKey: new object());

        _sut.SetLayout(f1, FormationLayoutType.InfantryFrontRangedBack);
        _sut.SetLayout(f2, FormationLayoutType.Checkerboard);

        Assert.AreEqual(FormationLayoutType.InfantryFrontRangedBack, _sut.GetLayout(f1));
        Assert.AreEqual(FormationLayoutType.Checkerboard, _sut.GetLayout(f2));
    }

    [TestMethod]
    public void CycleLayouts_UnassignedFormation_NotAffected()
    {
        var f = MakeFormation(8, 6);
        // No SetLayout → not in the dict → cycle skips it (matches dev's behavior)

        var (newLayout, affected) = _sut.CycleLayouts(new List<IFormationAdapter> { f });

        Assert.AreEqual(0, affected);
    }

    [TestMethod]
    public void CycleLayouts_AssignedFormation_AdvancesAndInvalidatesCache()
    {
        var f = MakeFormation(8, 6);
        _sut.SetLayout(f, FormationLayoutType.InfantryFrontRangedBack);

        var (newLayout, affected) = _sut.CycleLayouts(new List<IFormationAdapter> { f });

        Assert.AreEqual(1, affected);
        Assert.AreEqual(FormationLayoutType.RangedFrontInfantryBack, newLayout);
        Assert.AreEqual(FormationLayoutType.RangedFrontInfantryBack, _sut.GetLayout(f));
    }

    [TestMethod]
    public void CycleLayouts_FullCycle_ReturnsToInfantryFront()
    {
        var f = MakeFormation(8, 6);
        _sut.SetLayout(f, FormationLayoutType.InfantryFrontRangedBack);

        var formations = new List<IFormationAdapter> { f };
        _sut.CycleLayouts(formations); // → RangedFrontInfantryBack
        _sut.CycleLayouts(formations); // → RangedWingsInfantryCenter
        _sut.CycleLayouts(formations); // → Checkerboard
        var (final, _) = _sut.CycleLayouts(formations); // → InfantryFrontRangedBack

        Assert.AreEqual(FormationLayoutType.InfantryFrontRangedBack, final);
    }

    [TestMethod]
    public void CycleLayouts_EmptyFormation_Skipped()
    {
        var f = MakeFormation(0, 0);
        _sut.SetLayout(f, FormationLayoutType.InfantryFrontRangedBack);

        var (_, affected) = _sut.CycleLayouts(new List<IFormationAdapter> { f });

        Assert.AreEqual(0, affected);
    }

    // -------- IsMixedFormation thresholds --------

    [TestMethod]
    public void IsMixedFormation_AllMelee_False()
    {
        var f = MakeFormation(20, 0);

        Assert.IsFalse(_sut.IsMixedFormation(f));
    }

    [TestMethod]
    public void IsMixedFormation_AllRanged_False()
    {
        var f = MakeFormation(0, 20);

        Assert.IsFalse(_sut.IsMixedFormation(f));
    }

    [TestMethod]
    public void IsMixedFormation_TooSmall_False()
    {
        var f = MakeFormation(5, 4); // total 9, below MinTotalUnits=10

        Assert.IsFalse(_sut.IsMixedFormation(f));
    }

    [TestMethod]
    public void IsMixedFormation_MinorityTooSmall_False()
    {
        var f = MakeFormation(20, 4); // total 24, minority 4 < MinUnitsOfMinorityClass=5

        Assert.IsFalse(_sut.IsMixedFormation(f));
    }

    [TestMethod]
    public void IsMixedFormation_MinorityBelow20Percent_False()
    {
        var f = MakeFormation(50, 6); // total 56, minority 6, 6/56 = 10.7% < 20%

        Assert.IsFalse(_sut.IsMixedFormation(f));
    }

    [TestMethod]
    public void IsMixedFormation_BalancedAndLargeEnough_True()
    {
        var f = MakeFormation(8, 6); // total 14, minority 6 ≥ 5, 6/14 = 42.8% ≥ 20%

        Assert.IsTrue(_sut.IsMixedFormation(f));
    }

    // -------- SmartCavalryAI × MixedFormations handshake (#189 / #190) --------
    //
    // The two-feature contract: SmartCavalryAI owns cavalry formation behavior; MixedFormations
    // defers to SmartCavalryAI via two RepresentativeIsCavalry guards in FormationLayoutService.
    // These tests pin the guards so a refactor of either feature can't silently re-introduce the
    // P1 charge-line overwrite from the original Codex 2026-05-06 finding (already fixed; this is
    // regression coverage).
    //
    // Phase 6 #170 / Phase 7 #189 + #190 named these exact lines as the cross-feature contract.

    [TestMethod]
    public void ComputeUnitPlanePosition_CavalryFormation_ReturnsNull_HonoringSmartCavalryHandshake()
    {
        // FormationLayoutService.cs:74 — `if (formation.RepresentativeIsCavalry) return null;`
        // Even with all other gating conditions met (feature enabled, layout set, holding, valid),
        // a cavalry formation must short-circuit so SmartCavalryAI can own its positioning.
        var cavalry = MakeFormation(8, 6);
        cavalry.RepresentativeIsCavalry.Returns(true);
        _sut.SetLayout(cavalry, FormationLayoutType.InfantryFrontRangedBack);

        var result = _sut.ComputeUnitPlanePosition(cavalry, agentIndex: 0, agentIsRanged: false);

        Assert.IsNull(result, "Cavalry formation must yield null position so SmartCavalryAI owns the layout — " +
            "regressing this re-introduces the charge-line overwrite Codex 2026-05-06 caught.");
    }

    [TestMethod]
    public void IsMixedFormation_CavalryFormation_ReturnsFalse_HonoringSmartCavalryHandshake()
    {
        // FormationLayoutService.cs:191 — `if (formation.RepresentativeIsCavalry) return false;`
        // Even a horse-archer formation that meets the 20%/≥5 ranged-minority threshold must NOT
        // be classified as mixed — SmartCavalryAI handles horse-archer behavior.
        var horseArchers = MakeFormation(8, 6); // would be mixed if not cavalry
        horseArchers.RepresentativeIsCavalry.Returns(true);

        Assert.IsFalse(_sut.IsMixedFormation(horseArchers),
            "Cavalry formation must not be classified as mixed — horse-archer layout belongs to SmartCavalryAI.");
    }

    [TestMethod]
    public void CavalryHandshake_NonCavalry_DoesNotShortCircuit_BaselineAssertion()
    {
        // Sanity baseline: the cavalry guard does NOT short-circuit non-cavalry formations.
        // A regression that flips the guard polarity (returning null/false for *all* formations)
        // would silently disable MixedFormations entirely; this baseline test catches that.
        var infantry = MakeFormation(8, 6);
        infantry.RepresentativeIsCavalry.Returns(false);
        _sut.SetLayout(infantry, FormationLayoutType.InfantryFrontRangedBack);

        var posResult = _sut.ComputeUnitPlanePosition(infantry, agentIndex: 0, agentIsRanged: false);
        Assert.IsNotNull(posResult, "Non-cavalry formation must NOT short-circuit the cavalry guard.");

        Assert.IsTrue(_sut.IsMixedFormation(infantry),
            "Non-cavalry mixed formation must still be classified as mixed.");
    }

    // -------- ApplyDefaultsToFormations --------

    [TestMethod]
    public void ApplyDefaultsToFormations_FeatureDisabled_DoesNothing()
    {
        _settings.IsEnabled.Returns(false);
        var f = MakeFormation(8, 6);

        var assigned = _sut.ApplyDefaultsToFormations(new List<IFormationAdapter> { f });

        Assert.AreEqual(0, assigned);
        Assert.AreEqual(FormationLayoutType.Vanilla, _sut.GetLayout(f));
    }

    [TestMethod]
    public void ApplyDefaultsToFormations_DefaultIsVanilla_DoesNothing()
    {
        _settings.DefaultLayout.Returns(FormationLayoutType.Vanilla);
        var f = MakeFormation(8, 6);

        var assigned = _sut.ApplyDefaultsToFormations(new List<IFormationAdapter> { f });

        Assert.AreEqual(0, assigned);
    }

    [TestMethod]
    public void ApplyDefaultsToFormations_AlreadyAssigned_NotReplaced()
    {
        var f = MakeFormation(8, 6);
        _sut.SetLayout(f, FormationLayoutType.Checkerboard);

        var assigned = _sut.ApplyDefaultsToFormations(new List<IFormationAdapter> { f });

        Assert.AreEqual(0, assigned);
        Assert.AreEqual(FormationLayoutType.Checkerboard, _sut.GetLayout(f));
    }

    [TestMethod]
    public void ApplyDefaultsToFormations_MixedAndUnassigned_AssignsDefault()
    {
        var f = MakeFormation(8, 6);

        var assigned = _sut.ApplyDefaultsToFormations(new List<IFormationAdapter> { f });

        Assert.AreEqual(1, assigned);
        Assert.AreEqual(FormationLayoutType.InfantryFrontRangedBack, _sut.GetLayout(f));
    }

    [TestMethod]
    public void ApplyDefaultsToFormations_NotMixed_NotAssigned()
    {
        var f = MakeFormation(20, 0);

        var assigned = _sut.ApplyDefaultsToFormations(new List<IFormationAdapter> { f });

        Assert.AreEqual(0, assigned);
        Assert.AreEqual(FormationLayoutType.Vanilla, _sut.GetLayout(f));
    }

    // -------- OnMissionEnd --------

    [TestMethod]
    public void OnMissionEnd_ClearsAllPerFormationState()
    {
        var f1 = MakeFormation(8, 6, formationKey: new object());
        var f2 = MakeFormation(8, 6, formationKey: new object());
        _sut.SetLayout(f1, FormationLayoutType.InfantryFrontRangedBack);
        _sut.SetLayout(f2, FormationLayoutType.Checkerboard);

        _sut.OnMissionEnd();

        Assert.AreEqual(FormationLayoutType.Vanilla, _sut.GetLayout(f1));
        Assert.AreEqual(FormationLayoutType.Vanilla, _sut.GetLayout(f2));
    }

    [TestMethod]
    public void OnMissionEnd_AfterClear_ComputeReturnsNull()
    {
        var f = MakeFormation(8, 6);
        _sut.SetLayout(f, FormationLayoutType.InfantryFrontRangedBack);

        _sut.OnMissionEnd();
        var result = _sut.ComputeUnitPlanePosition(f, agentIndex: 0, agentIsRanged: false);

        Assert.IsNull(result);
    }

    // -------- Codex review #35 finding 2 (MEDIUM) — thread-safety regression --------
    // Engine threads Formation positioning queries (Mission.IsFormationUnitPositionAvailableAuxMT
    // uses TWSharedMutexReadLock; "MT" suffix on positioning helpers denotes multi-threaded helpers).
    // Service must lock all dict + SlotAssignment.ByAgentIndex mutations. These tests exercise the
    // lock paths for correctness; a true concurrency stress test is in-game integration only.

    [TestMethod]
    public void ConcurrentTaskBattery_SetLayoutAndCompute_DoesNotThrowOrCorruptCache()
    {
        var f = MakeFormation(8, 6);
        _sut.SetLayout(f, FormationLayoutType.InfantryFrontRangedBack);

        // Hammer the service with a battery of operations from multiple tasks. The lock should
        // serialize them; no exceptions, no negative slots, no missing mappings.
        var tasks = new System.Threading.Tasks.Task[8];
        for (var t = 0; t < tasks.Length; t++)
        {
            tasks[t] = System.Threading.Tasks.Task.Run(() =>
            {
                for (var i = 0; i < 100; i++)
                {
                    _ = _sut.ComputeUnitPlanePosition(f, agentIndex: i % 14, agentIsRanged: i % 2 == 0);
                    _ = _sut.GetLayout(f);
                    if (i % 10 == 0)
                        _sut.CycleLayouts(new List<IFormationAdapter> { f });
                }
            });
        }
        System.Threading.Tasks.Task.WaitAll(tasks);

        // No assertion on specific values — the test passes if WaitAll didn't throw and the service
        // is in a coherent state afterwards.
        var finalLayout = _sut.GetLayout(f);
        Assert.AreNotEqual(FormationLayoutType.Vanilla, finalLayout, "Layout should still be set after concurrent cycling");
    }

    [TestMethod]
    public void ComputeAndCycle_RapidSequentialAlternation_RemainsCoherent()
    {
        var f = MakeFormation(8, 6);
        _sut.SetLayout(f, FormationLayoutType.InfantryFrontRangedBack);

        for (var i = 0; i < 100; i++)
        {
            var result = _sut.ComputeUnitPlanePosition(f, agentIndex: 0, agentIsRanged: false);
            // After the first frame the cache builds an assignment and result is non-null.
            // After every cycle the cache invalidates and rebuilds — still non-null.
            Assert.IsNotNull(result, $"iteration {i} returned null");

            if (i % 5 == 0) _sut.CycleLayouts(new List<IFormationAdapter> { f });
        }
    }
}
