using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using TaleWorlds.Library;
using TAOM.Adapters;
using TAOM.Core.Collections;
using TAOM.Core.Logging;
using TAOM.Features.MixedFormations.Models;

namespace TAOM.Features.MixedFormations;

public sealed class FormationLayoutService : IFormationLayoutService
{
    private const int MinUnitsOfMinorityClass = 5;
    private const float MinPercentOfMinorityClass = 0.2f;
    private const int MinTotalUnits = 10;

    private readonly IMixedFormationsSettingsProvider _settings;
    private readonly ILayoutPositioner _positioner;
    private readonly IModLogger _logger;

    // Each formation's layout, with the adapter the main thread passed when it set it. Written only under
    // _lock, together with the slot cache; read without it by FindLaidOutFormation, because Patch30 asks
    // for every unit of every formation on the worker threads (ConcurrentDictionary reads take no lock).
    // Keys compare by reference: Formation.GetHashCode dereferences its Team
    // (docs/reviews/lessons/adapters-taleworlds-api.md, 2026-09-26).
    private readonly ConcurrentDictionary<object, (IFormationAdapter Formation, FormationLayoutType Layout)> _layouts =
        new(ReferenceIdentity.Instance);
    private readonly Dictionary<object, SlotAssignment> _assignmentCache = new(ReferenceIdentity.Instance);

    // Patch30 throws counted this mission (D6: the first in full, the rest as a count at mission end).
    private int _fallbacks;

    public IFormationAdapter? FindLaidOutFormation(object formationKey) =>
        formationKey != null && _layouts.TryGetValue(formationKey, out var entry)
            && entry.Layout != FormationLayoutType.Vanilla
            ? entry.Formation
            : null;

    // Codex review #35 finding 2 (MEDIUM): Bannerlord runs Formation positioning across worker
    // threads (Formation.MovementOrderPositionLock; Mission.IsFormationUnitPositionAvailableAuxMT uses
    // TWSharedMutexReadLock(Scene.PhysicsAndRayCastLock); the "MT" suffix on
    // CreateNewOrderWorldPositionMT etc. denotes multi-threaded helpers). Patch30 fires from
    // those threads, so EnsureAssignment cache writes + ByAgentIndex inserts could race against
    // OnMissionTick-driven CycleLayouts/ApplyDefaultsToFormations writes from the main thread.
    // Every layout write, the slot cache and SlotAssignment.ByAgentIndex hold this lock, and so
    // does the slot lookup for a laid-out formation; a formation with no layout is answered by
    // FindLaidOutFormation without it.
    private readonly object _lock = new();

    public FormationLayoutService(
        IMixedFormationsSettingsProvider settings,
        ILayoutPositioner positioner,
        IModLogger logger)
    {
        _settings = settings;
        _positioner = positioner;
        _logger = logger;
    }

    public FormationLayoutType GetLayout(IFormationAdapter formation)
    {
        if (formation == null) return FormationLayoutType.Vanilla;
        return _layouts.TryGetValue(formation.FormationKey, out var entry)
            ? entry.Layout
            : FormationLayoutType.Vanilla;
    }

    public void SetLayout(IFormationAdapter formation, FormationLayoutType layout)
    {
        if (formation == null) return;
        lock (_lock)
        {
            _layouts[formation.FormationKey] = (formation, layout);
            // Layout changed → assignment cache for this formation is stale. Drop it; will rebuild lazily.
            _assignmentCache.Remove(formation.FormationKey);
        }
    }

    public Vec2? ComputeUnitPlanePosition(IFormationAdapter formation, int agentIndex, bool agentIsRanged)
    {
        if (formation == null) return null;
        // Lock-free: almost no formation has a layout, and this is asked for each of their units on the
        // engine's worker threads.
        if (FindLaidOutFormation(formation.FormationKey) == null) return null;
        if (!_settings.IsEnabled) return null;
        if (!formation.IsHolding) return null;
        if (!formation.OrderPositionIsValid) return null;
        // Cross-feature handshake: SmartCavalryAI (Patch31) owns cavalry formation
        // positioning during charge maneuvers. Skip Patch30 even if a layout was
        // manually assigned to a cavalry formation via the cycle hotkey.
        // The evaluating read, as before plan 032. FormationQuerySystem clears the class-ratio queries' lifetimes
        // without re-evaluating them when units change formation (Formation.OnMassUnitTransferEnd, TransferUnits and
        // Split call QuerySystem.Expire()), so a cached flag would answer from the old composition while the
        // CavalryUnitRatio that UnitPitch reads below refreshes the same sync group, this flag included: the call
        // would return a position the evaluating read refuses, or refuse one it gives. While the group is fresh
        // this read is a clock check.
        if (formation.RepresentativeIsCavalry) return null;

        // Apart from the lock-free FindLaidOutFormation lookup above, this method's dictionary work (the layout
        // re-read, the slot cache and the slot assignment) happens under the lock, and the slot is captured there.
        // The pitch, direction and order position are read live after it: the lock cannot protect engine state, and
        // UnitPitch can re-evaluate the class-ratio queries, writing their cache (see the gate above).
        (int row, int file) slot;
        lock (_lock)
        {
            // Re-read under the lock: the lock-free lookup above can be one call stale.
            if (!_layouts.TryGetValue(formation.FormationKey, out var entry)) return null;
            var layout = entry.Layout;
            if (layout == FormationLayoutType.Vanilla) return null;

            var assignment = EnsureAssignmentLocked(formation, layout);
            if (assignment == null) return null;

            if (!assignment.ByAgentIndex.TryGetValue(agentIndex, out slot))
            {
                slot = _positioner.AssignNextSlot(assignment, new FormationUnit(agentIndex, agentIsRanged));
                assignment.Assign(agentIndex, agentIsRanged, slot);
            }
        }

        // Convert slot (row, file) to local-space offset, then transform by formation direction
        // and add formation order position to get the final plane position. Runs outside the lock.
        var unitInterval = LayoutPositioner.UnitPitch(formation);
        var localOffset = new Vec2(slot.file * unitInterval, -slot.row * unitInterval);
        var direction = formation.Direction;
        var rotated = direction.TransformToParentUnitF(localOffset);
        return formation.OrderPosition + rotated;
    }

    public (FormationLayoutType newLayout, int affectedCount) CycleLayouts(IReadOnlyList<IFormationAdapter> formations)
    {
        var lastLayout = FormationLayoutType.Vanilla;
        var affected = 0;

        lock (_lock)
        {
            foreach (var formation in formations)
            {
                if (formation == null || formation.CountOfUnits == 0) continue;
                if (!_layouts.TryGetValue(formation.FormationKey, out var current)) continue;

                var next = NextLayout(current.Layout);
                _layouts[formation.FormationKey] = (formation, next);
                _assignmentCache.Remove(formation.FormationKey);
                lastLayout = next;
                affected++;
            }
        }

        if (affected > 0 && _settings.IsDebugMode)
            _logger.LogDebug($"[MixedFormations] cycled {affected} formation(s) → {lastLayout}");

        return (lastLayout, affected);
    }

    public int ApplyDefaultsToFormations(IReadOnlyList<IFormationAdapter> formations)
    {
        if (!_settings.IsEnabled) return 0;
        var defaultLayout = _settings.DefaultLayout;
        if (defaultLayout == FormationLayoutType.Vanilla) return 0;

        var assigned = 0;
        // IsMixedFormation iterates formation.Units (read of underlying TaleWorlds collection,
        // owned by the engine). It's called inside the lock so the auto-apply pass cannot interleave
        // with a worker-thread slot build for the same formation. The Units read is read-only against
        // the engine's data; safe to call under our lock.
        lock (_lock)
        {
            foreach (var formation in formations)
            {
                if (formation == null || formation.CountOfUnits < 2) continue;
                if (_layouts.ContainsKey(formation.FormationKey)) continue;
                if (!IsMixedFormationInternal(formation)) continue;

                _layouts[formation.FormationKey] = (formation, defaultLayout);
                assigned++;
            }
        }

        if (assigned > 0 && _settings.IsDebugMode)
            _logger.LogDebug($"[MixedFormations] auto-assigned default layout {defaultLayout} to {assigned} formation(s)");

        return assigned;
    }

    public bool IsMixedFormation(IFormationAdapter formation)
    {
        // Public method — does not need to hold the lock; the work is reading formation.Units
        // (engine-owned collection) and computing thresholds. The answer can race with concurrent
        // composition changes, but the consumer (ApplyDefaultsToFormations) holds the lock for
        // the read-then-write itself.
        return IsMixedFormationInternal(formation);
    }

    public void ForgetAgent(int agentIndex)
    {
        lock (_lock)
        {
            // The slot goes back to its class's free list, so the replacement takes it instead of a
            // fresh counter value a row deeper (#595, Codex review 109).
            foreach (var assignment in _assignmentCache.Values)
                assignment.Forget(agentIndex);
        }
    }

    public void OnMissionEnd()
    {
        int formationCount;
        lock (_lock)
        {
            formationCount = _layouts.Count;
            _layouts.Clear();
            _assignmentCache.Clear();
        }
        if (formationCount > 0)
            _logger.LogInfo($"[MixedFormations] mission ended — cleared {formationCount} formation(s)");
        var fallbacks = Interlocked.Exchange(ref _fallbacks, 0);
        if (fallbacks > 0)
            _logger.LogInfo(FallbackSummaryLine(fallbacks));
    }

    public void NoteFallback(Exception ex)
    {
        if (Interlocked.Increment(ref _fallbacks) == 1)
            _logger.LogWarning(FallbackLine(ex));
    }

    internal static string FallbackLine(Exception ex) =>
        "[MixedFormations] Patch30 position prefix threw; this unit and every later failing one this mission fall "
        + $"back to vanilla positioning (the first is logged in full, the count at mission end): {ex}";

    internal static string FallbackSummaryLine(int count) =>
        $"[MixedFormations] mission ended: {count} unit position(s) fell back to vanilla after a Patch30 throw";

    private static bool IsMixedFormationInternal(IFormationAdapter formation)
    {
        if (formation == null) return false;

        // SmartCavalryAI (Patch31) owns cavalry formation layout — it issues SetPositioning
        // for charge lines that we'd otherwise overwrite per-unit via Patch30. Excluded by
        // RepresentativeIsCavalry so horse-archer formations (≥10 units, ≥20% ranged) which
        // would OTHERWISE qualify as "mixed" are not double-positioned.
        // See: codex-adversarial-smartcavalryai-2026-05-06 cross-feature finding.
        if (formation.RepresentativeIsCavalry) return false;

        var ranged = 0;
        var melee = 0;
        foreach (var unit in formation.Units)
        {
            if (unit.IsRanged) ranged++;
            else melee++;
        }

        var total = ranged + melee;
        if (total < MinTotalUnits) return false;

        var minority = System.Math.Min(ranged, melee);
        if (minority < MinUnitsOfMinorityClass) return false;
        if ((float)minority / total < MinPercentOfMinorityClass) return false;

        return true;
    }

    /// <summary>
    /// Caller MUST hold <see cref="_lock"/>. Returns the cached assignment for the formation+layout
    /// or builds a fresh one and caches it.
    /// </summary>
    private SlotAssignment? EnsureAssignmentLocked(IFormationAdapter formation, FormationLayoutType layout)
    {
        if (_assignmentCache.TryGetValue(formation.FormationKey, out var existing) && existing.Layout == layout)
            return existing;

        var fresh = _positioner.BuildInitialAssignment(formation, layout);
        _assignmentCache[formation.FormationKey] = fresh;
        return fresh;
    }

    private static FormationLayoutType NextLayout(FormationLayoutType current) => current switch
    {
        FormationLayoutType.InfantryFrontRangedBack => FormationLayoutType.RangedFrontInfantryBack,
        FormationLayoutType.RangedFrontInfantryBack => FormationLayoutType.RangedWingsInfantryCenter,
        FormationLayoutType.RangedWingsInfantryCenter => FormationLayoutType.Checkerboard,
        FormationLayoutType.Checkerboard => FormationLayoutType.InfantryFrontRangedBack,
        _ => FormationLayoutType.InfantryFrontRangedBack,
    };
}
