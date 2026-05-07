using System.Collections.Generic;
using TaleWorlds.Library;
using TAOM.Adapters;
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

    private readonly Dictionary<object, FormationLayoutType> _layoutByFormation = new();
    private readonly Dictionary<object, SlotAssignment> _assignmentCache = new();

    // Codex review #35 finding 2 (MEDIUM): Bannerlord runs Formation positioning across worker
    // threads (Formation.OrderPositionLock; Mission.IsFormationUnitPositionAvailableAuxMT uses
    // TWSharedMutexReadLock(Scene.PhysicsAndRayCastLock); the "MT" suffix on
    // CreateNewOrderWorldPositionMT etc. denotes multi-threaded helpers). Patch30 fires from
    // those threads, so EnsureAssignment cache writes + ByAgentIndex inserts could race against
    // OnMissionTick-driven CycleLayouts/ApplyDefaultsToFormations writes from the main thread.
    // All dict + SlotAssignment.ByAgentIndex mutations now hold this lock. Reads on the hot
    // path also lock briefly to ensure consistency. Single uncontended lock acquisition is ~25ns
    // on x86, well below the per-call budget.
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
        lock (_lock)
        {
            return _layoutByFormation.TryGetValue(formation.FormationKey, out var layout)
                ? layout
                : FormationLayoutType.Vanilla;
        }
    }

    public void SetLayout(IFormationAdapter formation, FormationLayoutType layout)
    {
        if (formation == null) return;
        lock (_lock)
        {
            _layoutByFormation[formation.FormationKey] = layout;
            // Layout changed → assignment cache for this formation is stale. Drop it; will rebuild lazily.
            _assignmentCache.Remove(formation.FormationKey);
        }
    }

    public Vec2? ComputeUnitPlanePosition(IFormationAdapter formation, int agentIndex, bool agentIsRanged)
    {
        if (formation == null) return null;
        if (!_settings.IsEnabled) return null;
        if (!formation.IsHolding) return null;
        if (!formation.OrderPositionIsValid) return null;
        // Cross-feature handshake: SmartCavalryAI (Patch31) owns cavalry formation
        // positioning during charge maneuvers. Skip Patch30 even if a layout was
        // manually assigned to a cavalry formation via the cycle hotkey.
        if (formation.RepresentativeIsCavalry) return null;

        // The dict reads + writes happen under the lock; the math after is pure and uses
        // already-captured values, so it doesn't need the lock.
        (int row, int file) slot;
        lock (_lock)
        {
            if (!_layoutByFormation.TryGetValue(formation.FormationKey, out var layout))
                return null;
            if (layout == FormationLayoutType.Vanilla) return null;

            var assignment = EnsureAssignmentLocked(formation, layout);
            if (assignment == null) return null;

            if (!assignment.ByAgentIndex.TryGetValue(agentIndex, out slot))
            {
                slot = _positioner.AssignNextSlot(assignment, new FormationUnit(agentIndex, agentIsRanged));
                assignment.ByAgentIndex[agentIndex] = slot;
            }
        }

        // Convert slot (row, file) to local-space offset, then transform by formation direction
        // and add formation order position to get the final plane position. Pure math; lock-free.
        var unitInterval = System.Math.Max(1f, formation.Interval + 1f);
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
                if (!_layoutByFormation.TryGetValue(formation.FormationKey, out var current)) continue;

                var next = NextLayout(current);
                _layoutByFormation[formation.FormationKey] = next;
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
        // owned by the engine). It's called inside the lock so a concurrent SetLayout/CycleLayouts
        // from the worker-thread Prefix can't interleave with the auto-apply pass. The Units read
        // is read-only against the engine's data; safe to call under our lock.
        lock (_lock)
        {
            foreach (var formation in formations)
            {
                if (formation == null || formation.CountOfUnits < 2) continue;
                if (_layoutByFormation.ContainsKey(formation.FormationKey)) continue;
                if (!IsMixedFormationInternal(formation)) continue;

                _layoutByFormation[formation.FormationKey] = defaultLayout;
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

    public void OnMissionEnd()
    {
        int formationCount;
        lock (_lock)
        {
            formationCount = _layoutByFormation.Count;
            _layoutByFormation.Clear();
            _assignmentCache.Clear();
        }
        if (formationCount > 0)
            _logger.LogInfo($"[MixedFormations] mission ended — cleared {formationCount} formation(s)");
    }

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
