using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Library;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.MixedFormations;
using TAOM.Features.MixedFormations.Models;

namespace TAOM.Tests.Features.MixedFormations;

[TestClass]
[TestCategory("RequiresGame")]
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
        // A laid-out formation keeps its layout until mission end, so the live IsEnabled read is what stops
        // the prefix placing it once the player turns the feature off mid-battle.
        var f = MakeFormation(8, 6);
        _sut.SetLayout(f, FormationLayoutType.InfantryFrontRangedBack);
        _settings.IsEnabled.Returns(false);

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

    [DataTestMethod]
    [DataRow(0.76f, 2f)]     // a human keeps the one-metre pitch: interval 1 + 1
    [DataRow(1.75f, 2.75f)]  // a troll-width formation (Patch92): interval 1 + its width
    [DataRow(1.6f, 2.6f)]    // at least a tenth mounted and not ordered to dismount: QuadrupedalRadius 0.8 x 2
    public void ComputeUnitPlanePosition_NeighbouringSlots_AreSpacedByTheWiderOfOneMetreAndTheUnitWidth(
        float unitDiameter, float expectedPitch)
    {
        var f = MakeFormation(8, 6);
        f.UnitDiameter.Returns(unitDiameter);
        _sut.SetLayout(f, FormationLayoutType.InfantryFrontRangedBack);

        var first = _sut.ComputeUnitPlanePosition(f, agentIndex: 0, agentIsRanged: false)!.Value;
        var second = _sut.ComputeUnitPlanePosition(f, agentIndex: 1, agentIsRanged: false)!.Value;

        Assert.AreEqual(expectedPitch, (second - first).Length, 1e-4f);
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
        // FormationLayoutService.ComputeUnitPlanePosition: `if (formation.RepresentativeIsCavalry) return null;`
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

    // -------- ForgetAgent (#595: the engine recycles a deleted agent's index) --------

    [TestMethod]
    public void ForgetAgent_ReturnsTheSlot_SoASameClassReplacementStandsWhereTheDeadUnitDid()
    {
        var f = MakeFormation(8, 6);
        _sut.SetLayout(f, FormationLayoutType.InfantryFrontRangedBack);
        var dead = _sut.ComputeUnitPlanePosition(f, agentIndex: 3, agentIsRanged: false);
        Assert.IsNotNull(dead);
        var again = _sut.ComputeUnitPlanePosition(f, agentIndex: 3, agentIsRanged: false);
        Assert.AreEqual(dead, again, "a cached slot is stable for the same live agent");

        _sut.ForgetAgent(3);
        var replacement = _sut.ComputeUnitPlanePosition(f, agentIndex: 99, agentIsRanged: false);

        Assert.AreEqual(dead, replacement, "the vacated melee slot goes to the next melee unit, not a fresh counter value");
    }

    [TestMethod]
    public void ForgetAgent_AVacatedMeleeSlot_IsNotGivenToARangedNewcomer()
    {
        var f = MakeFormation(8, 6);
        _sut.SetLayout(f, FormationLayoutType.InfantryFrontRangedBack);
        var dead = _sut.ComputeUnitPlanePosition(f, agentIndex: 3, agentIsRanged: false);
        _sut.ForgetAgent(3);

        var archer = _sut.ComputeUnitPlanePosition(f, agentIndex: 99, agentIsRanged: true);
        var later = _sut.ComputeUnitPlanePosition(f, agentIndex: 100, agentIsRanged: false);

        Assert.AreNotEqual(dead, archer, "a ranged newcomer must not stand in the infantry block");
        Assert.AreEqual(dead, later, "the vacated melee slot waits for the next melee unit");
    }

    [TestMethod]
    public void ForgetAgent_UnknownIndex_IsANoOp()
    {
        var f = MakeFormation(8, 6);
        _sut.SetLayout(f, FormationLayoutType.InfantryFrontRangedBack);
        var before = _sut.ComputeUnitPlanePosition(f, agentIndex: 0, agentIsRanged: false);

        _sut.ForgetAgent(12345);

        Assert.AreEqual(before, _sut.ComputeUnitPlanePosition(f, agentIndex: 0, agentIsRanged: false));
    }

    [TestMethod]
    public void ForgetAgent_ForgetsTheIndexInEveryCachedFormation()
    {
        var a = MakeFormation(8, 6);
        var b = MakeFormation(8, 6);
        _sut.SetLayout(a, FormationLayoutType.InfantryFrontRangedBack);
        _sut.SetLayout(b, FormationLayoutType.RangedFrontInfantryBack);
        var inA = _sut.ComputeUnitPlanePosition(a, agentIndex: 77, agentIsRanged: true);
        var inB = _sut.ComputeUnitPlanePosition(b, agentIndex: 77, agentIsRanged: true);

        _sut.ForgetAgent(77);

        Assert.AreEqual(inA, _sut.ComputeUnitPlanePosition(a, agentIndex: 78, agentIsRanged: true), "formation A returned the slot");
        Assert.AreEqual(inB, _sut.ComputeUnitPlanePosition(b, agentIndex: 78, agentIsRanged: true), "formation B returned the slot");
    }

    // Codex review 109 (2026-09-13): forgetting the mapping alone left the counters growing, so every
    // casualty's replacement took a row deeper and landed on the other class's rows.
    [TestMethod]
    public void RepeatedTurnover_KeepsEveryLiveUnitOnAUniqueSlotInsideTheInitialFootprint()
    {
        var f = MakeFormation(10, 10);
        _sut.SetLayout(f, FormationLayoutType.InfantryFrontRangedBack);
        var initial = new HashSet<Vec2>();
        for (var i = 0; i < 20; i++)
            initial.Add(_sut.ComputeUnitPlanePosition(f, i, agentIsRanged: i >= 10)!.Value);
        Assert.AreEqual(20, initial.Count, "the initial layout has no shared slots");

        // Each cycle one unit of each class dies and a fresh index of the same class replaces it.
        var live = new Dictionary<int, bool>();
        for (var i = 0; i < 20; i++) live[i] = i >= 10;
        var next = 1000;
        for (var cycle = 0; cycle < 100; cycle++)
        {
            int meleeVictim = -1, rangedVictim = -1;
            foreach (var pair in live)
            {
                if (!pair.Value && (meleeVictim < 0 || pair.Key < meleeVictim)) meleeVictim = pair.Key;
                if (pair.Value && (rangedVictim < 0 || pair.Key < rangedVictim)) rangedVictim = pair.Key;
            }
            _sut.ForgetAgent(meleeVictim); live.Remove(meleeVictim);
            _sut.ForgetAgent(rangedVictim); live.Remove(rangedVictim);
            live[next++] = false;
            live[next++] = true;

            var positions = new HashSet<Vec2>();
            foreach (var pair in live)
            {
                var pos = _sut.ComputeUnitPlanePosition(f, pair.Key, pair.Value)!.Value;
                Assert.IsTrue(positions.Add(pos), $"cycle {cycle}: two live units share slot {pos}");
                Assert.IsTrue(initial.Contains(pos), $"cycle {cycle}: unit {pair.Key} stands outside the initial footprint at {pos}");
            }
        }
    }

    // -------- Worker-thread fast path (plan 032) --------
    // Patch30 asks ComputeUnitPlanePosition for every unit of every formation on the engine's worker threads,
    // and almost no formation has a layout. Such a formation must be answered without the lock and without
    // reading the formation (RepresentativeIsCavalry re-evaluates FormationQuerySystem on the calling thread).

    [TestMethod]
    public void ComputeUnitPlanePosition_FormationWithoutLayout_ReadsNoFormationState()
    {
        var f = MakeFormation(8, 6);
        f.ClearReceivedCalls();

        Assert.IsNull(_sut.ComputeUnitPlanePosition(f, agentIndex: 0, agentIsRanged: false));

        CollectionAssert.AreEqual(new[] { "get_FormationKey" },
            f.ReceivedCalls().Select(c => c.GetMethodInfo().Name).Distinct().ToArray(),
            "a formation with no layout must cost one key lookup and nothing else");
        _ = _settings.DidNotReceive().IsEnabled;
    }

    // The lock-free lookup can be one call stale: mission end may clear the layouts between it and the lock.
    // The locked re-read is what then answers null instead of placing a unit of a cleared formation.
    [TestMethod]
    public void ComputeUnitPlanePosition_LayoutClearedAfterTheLockFreeLookup_ReturnsNull()
    {
        var f = MakeFormation(8, 6);
        _sut.SetLayout(f, FormationLayoutType.InfantryFrontRangedBack);
        _settings.IsEnabled.Returns(_ => { _sut.OnMissionEnd(); return true; });

        Assert.IsNull(_sut.ComputeUnitPlanePosition(f, agentIndex: 0, agentIsRanged: false));
    }

    [TestMethod]
    public void FindLaidOutFormation_NullKey_ReturnsNull()
    {
        Assert.IsNull(_sut.FindLaidOutFormation(null!));
    }

    // -------- Patch30's fallback accounting (D6: first occurrence in full, then a per-mission count) --------

    [TestMethod]
    public void FallbackLine_NamesTheConsequenceAndCarriesTheWholeException()
    {
        Assert.AreEqual(
            "[MixedFormations] Patch30 position prefix threw; this unit and every later failing one this mission fall "
            + "back to vanilla positioning (the first is logged in full, the count at mission end): "
            + "System.InvalidOperationException: boom",
            FormationLayoutService.FallbackLine(new InvalidOperationException("boom")));
    }

    [TestMethod]
    public void FallbackSummaryLine_CountsTheMissionsFallbacks()
    {
        Assert.AreEqual(
            "[MixedFormations] mission ended: 3 unit position(s) fell back to vanilla after a Patch30 throw",
            FormationLayoutService.FallbackSummaryLine(3));
    }

    [TestMethod]
    public void NoteFallback_FirstOfTheMissionInFull_RestCountedAtMissionEnd()
    {
        var first = new InvalidOperationException("first");

        _sut.NoteFallback(first);
        _sut.NoteFallback(new InvalidOperationException("second"));
        _sut.NoteFallback(new InvalidOperationException("third"));

        _logger.Received(1).LogWarning(Arg.Any<string>());
        _logger.Received(1).LogWarning(FormationLayoutService.FallbackLine(first));

        _sut.OnMissionEnd();

        _logger.Received(1).LogInfo(FormationLayoutService.FallbackSummaryLine(3));
    }

    [TestMethod]
    public void NoteFallback_AfterMissionEnd_LogsTheNextMissionsFirstInFull()
    {
        _sut.NoteFallback(new InvalidOperationException("mission one"));
        _sut.OnMissionEnd();
        _logger.ClearReceivedCalls();
        var next = new InvalidOperationException("mission two");

        _sut.NoteFallback(next);
        _sut.OnMissionEnd();

        _logger.Received(1).LogWarning(FormationLayoutService.FallbackLine(next));
        _logger.Received(1).LogInfo(FormationLayoutService.FallbackSummaryLine(1));
    }

    [TestMethod]
    public void OnMissionEnd_NoFallback_WritesNoFallbackSummary()
    {
        _sut.OnMissionEnd();

        _logger.DidNotReceive().LogInfo(Arg.Is<string>(s => s.Contains("fell back")));
    }

    // Formation.GetHashCode dereferences its Team (lessons/adapters-taleworlds-api.md, 2026-09-26), so the
    // layouts compare keys by reference and never call the key's own GetHashCode or Equals.
    private sealed class KeyWithThrowingOverrides
    {
        public override int GetHashCode() => throw new InvalidOperationException("GetHashCode read");
        public override bool Equals(object? obj) => throw new InvalidOperationException("Equals read");
    }

    [TestMethod]
    public void FindLaidOutFormation_ComparesKeysByReference()
    {
        var key = new KeyWithThrowingOverrides();
        var f = MakeFormation(8, 6, formationKey: key);

        _sut.SetLayout(f, FormationLayoutType.InfantryFrontRangedBack);

        Assert.AreSame(f, _sut.FindLaidOutFormation(key));
        Assert.IsNull(_sut.FindLaidOutFormation(new KeyWithThrowingOverrides()));
        Assert.IsNotNull(_sut.ComputeUnitPlanePosition(f, agentIndex: 0, agentIsRanged: false));
        Assert.AreEqual(FormationLayoutType.InfantryFrontRangedBack, _sut.GetLayout(f));
    }

    [TestMethod]
    public void ComputeUnitPlanePosition_FormationWithoutLayout_DoesNotWaitForTheLock()
    {
        var entered = new ManualResetEventSlim();
        var release = new ManualResetEventSlim();
        var positioner = Substitute.For<ILayoutPositioner>();
        positioner.BuildInitialAssignment(Arg.Any<IFormationAdapter>(), Arg.Any<FormationLayoutType>())
            .Returns(ci =>
            {
                entered.Set();
                release.Wait(TimeSpan.FromSeconds(30));
                return new SlotAssignment(ci.ArgAt<FormationLayoutType>(1), 4);
            });
        var sut = new FormationLayoutService(_settings, positioner, _logger);
        var laidOut = MakeFormation(8, 6);
        var plain = MakeFormation(8, 6);
        sut.SetLayout(laidOut, FormationLayoutType.InfantryFrontRangedBack);
        var holder = Task.Run(() => sut.ComputeUnitPlanePosition(laidOut, 0, false));
        try
        {
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(10)), "the holder never took the service lock");
            var plainCall = Task.Run(() => sut.ComputeUnitPlanePosition(plain, 0, false));
            Assert.IsTrue(plainCall.Wait(TimeSpan.FromSeconds(5)), "a formation with no layout waited for the service lock");
            Assert.IsNull(plainCall.Result);
        }
        finally
        {
            release.Set();
            holder.Wait(TimeSpan.FromSeconds(30));
        }
    }

    // A composition check, not an independent oracle: each unit of every layout stands at the slot LayoutPositioner
    // assigned it, spaced by LayoutPositioner.UnitPitch and rotated by the formation's direction. The expectation
    // reuses those production helpers, so this pins how the service composes them for one fixed fake geometry
    // (LayoutPositionerTests covers the slots). It says nothing about engine geometry or the cavalry flag's freshness.
    [DataTestMethod]
    [DataRow(FormationLayoutType.InfantryFrontRangedBack)]
    [DataRow(FormationLayoutType.RangedFrontInfantryBack)]
    [DataRow(FormationLayoutType.RangedWingsInfantryCenter)]
    [DataRow(FormationLayoutType.Checkerboard)]
    public void ComputeUnitPlanePosition_EveryLayout_PlacesEachUnitAtItsAssignedSlot(FormationLayoutType layout)
    {
        var f = MakeFormation(8, 6);
        f.UnitDiameter.Returns(0.76f);
        _sut.SetLayout(f, layout);
        var slots = new LayoutPositioner().BuildInitialAssignment(f, layout).ByAgentIndex;
        var pitch = LayoutPositioner.UnitPitch(f);
        foreach (var unit in f.Units)
        {
            var (row, file) = slots[unit.Index];
            var expected = f.OrderPosition + f.Direction.TransformToParentUnitF(new Vec2(file * pitch, -row * pitch));
            var actual = _sut.ComputeUnitPlanePosition(f, unit.Index, unit.IsRanged);
            Assert.IsNotNull(actual, $"{layout}: unit {unit.Index} got no position");
            Assert.AreEqual(expected.x, actual.Value.x, 1e-4f, $"{layout}: unit {unit.Index} x");
            Assert.AreEqual(expected.y, actual.Value.y, 1e-4f, $"{layout}: unit {unit.Index} y");
        }
    }

    // -------- The cavalry gate across an expired flag (Codex review of plan 032, finding 1) --------
    //
    // FormationQuerySystem clears its class-ratio queries' lifetimes without re-evaluating them when units change
    // formation (Formation.OnMassUnitTransferEnd, TransferUnits and Split call QuerySystem.Expire()). The next read of
    // any member of that sync group refreshes the whole group: an evaluating IsCavalryFormation, or the CavalryUnitRatio
    // that Formation.Interval and UnitDiameter read. A gate that read the cached flag would answer from the old
    // composition, and UnitPitch, a few lines later, would flip the flag with the position already on its way out.
    // This fake keeps the engine's rule: the flag it reports changes only when an evaluating member is read after an
    // expiry.
    private sealed class ExpiringCavalryQueries
    {
        public bool Cached;
        public bool Actual;
        public bool Expired;

        private void Refresh()
        {
            if (!Expired) return;
            Cached = Actual;
            Expired = false;
        }

        public IFormationAdapter Wire(IFormationAdapter formation)
        {
            formation.RepresentativeIsCavalry.Returns(_ => { Refresh(); return Cached; });
            formation.Interval.Returns(_ => { Refresh(); return 1f; });
            formation.UnitDiameter.Returns(_ => { Refresh(); return 0.76f; });
            return formation;
        }
    }

    [TestMethod]
    public void ComputeUnitPlanePosition_FlagExpiredAndNowCavalry_ReturnsNullOnTheSameCall()
    {
        var queries = new ExpiringCavalryQueries { Cached = false, Actual = false };
        var f = queries.Wire(MakeFormation(8, 6));
        _sut.SetLayout(f, FormationLayoutType.InfantryFrontRangedBack);
        Assert.IsNotNull(_sut.ComputeUnitPlanePosition(f, agentIndex: 0, agentIsRanged: false),
            "precondition: a laid-out infantry and archer formation is positioned");

        // A mass transfer brings in cavalry and expires the queries without re-evaluating them.
        queries.Actual = true;
        queries.Expired = true;

        Assert.IsNull(_sut.ComputeUnitPlanePosition(f, agentIndex: 0, agentIsRanged: false),
            "the call that finds the flag expired must see the refreshed answer and leave the formation to SmartCavalryAI");
    }

    [TestMethod]
    public void ComputeUnitPlanePosition_FlagExpiredAndNoLongerCavalry_PositionsTheUnitOnTheSameCall()
    {
        var queries = new ExpiringCavalryQueries { Cached = true, Actual = true };
        var f = queries.Wire(MakeFormation(8, 6));
        _sut.SetLayout(f, FormationLayoutType.InfantryFrontRangedBack);
        Assert.IsNull(_sut.ComputeUnitPlanePosition(f, agentIndex: 0, agentIsRanged: false),
            "precondition: a cavalry formation is left to SmartCavalryAI");

        // A transfer takes the cavalry out and expires the queries without re-evaluating them.
        queries.Actual = false;
        queries.Expired = true;

        Assert.IsNotNull(_sut.ComputeUnitPlanePosition(f, agentIndex: 0, agentIsRanged: false),
            "the call that finds the flag expired must see the refreshed answer and position the unit");
    }

    // -------- FindLaidOutFormation (plan 032) --------

    [TestMethod]
    public void FindLaidOutFormation_NoLayout_ReturnsNull()
    {
        Assert.IsNull(_sut.FindLaidOutFormation(MakeFormation(8, 6).FormationKey));
    }

    [TestMethod]
    public void FindLaidOutFormation_AfterApplyDefaults_ReturnsTheSameAdapter()
    {
        var f = MakeFormation(8, 6);

        Assert.AreEqual(1, _sut.ApplyDefaultsToFormations(new List<IFormationAdapter> { f }));

        Assert.AreSame(f, _sut.FindLaidOutFormation(f.FormationKey));
    }

    [TestMethod]
    public void FindLaidOutFormation_SetLayoutVanilla_DropsTheFormation()
    {
        var f = MakeFormation(8, 6);

        _sut.SetLayout(f, FormationLayoutType.Checkerboard);
        Assert.IsNotNull(_sut.FindLaidOutFormation(f.FormationKey));

        _sut.SetLayout(f, FormationLayoutType.Vanilla);
        Assert.IsNull(_sut.FindLaidOutFormation(f.FormationKey));
    }

    [TestMethod]
    public void FindLaidOutFormation_AfterCycleLayouts_StillReturnsTheFormation()
    {
        var f = MakeFormation(8, 6);
        _sut.SetLayout(f, FormationLayoutType.InfantryFrontRangedBack);

        _sut.CycleLayouts(new List<IFormationAdapter> { f });

        Assert.AreSame(f, _sut.FindLaidOutFormation(f.FormationKey));
    }

    [TestMethod]
    public void FindLaidOutFormation_SetVanillaThenCycle_ReturnsTheFormation()
    {
        var f = MakeFormation(8, 6);
        _sut.SetLayout(f, FormationLayoutType.Vanilla);
        Assert.IsNull(_sut.FindLaidOutFormation(f.FormationKey));

        _sut.CycleLayouts(new List<IFormationAdapter> { f });

        Assert.AreEqual(FormationLayoutType.InfantryFrontRangedBack, _sut.GetLayout(f));
        Assert.AreSame(f, _sut.FindLaidOutFormation(f.FormationKey), "a cycle from Vanilla must publish the formation");
        Assert.IsNotNull(_sut.ComputeUnitPlanePosition(f, agentIndex: 0, agentIsRanged: false),
            "a cycle from Vanilla gives a position at the base, and must still");
    }

    [TestMethod]
    public void FindLaidOutFormation_AfterOnMissionEnd_ReturnsNull()
    {
        var f = MakeFormation(8, 6);
        _sut.SetLayout(f, FormationLayoutType.InfantryFrontRangedBack);
        Assert.IsNotNull(_sut.FindLaidOutFormation(f.FormationKey));

        _sut.OnMissionEnd();

        Assert.IsNull(_sut.FindLaidOutFormation(f.FormationKey));
    }

}
