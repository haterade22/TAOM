using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TAOM.Features.LongDefeat.Domain;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Adapters;

/// <summary>
/// The gates (phase3-plan.md Slice 5, D17 to D19). A gate level writes the gate's own maximum and hit points; a repair is vanilla's <c>CastleGate.OnMissionReset</c>, found by name, followed by
/// <c>SetAutoOpenState(false)</c>. The gates are read from the objects held since the boot, which survive a fall: a destroyed gate has left <c>Mission.ActiveMissionObjects</c> (it is disabled in the same call,
/// "destructionComponent.GameEntity.GetFirstScriptOfType&lt;UsableMachine&gt;()?.Disable();", Mission.cs:3060), so the active list can no longer show it. The Hammer writes only rams and towers
/// (<c>LongDefeatEffectsAdapter.SetEngineHitPoints</c>) and nothing here touches one.
/// </summary>
internal sealed partial class LongDefeatBattlefieldAdapter : ILongDefeatFortAdapter
{
    /// <summary>A visible gate with its destructible, its vanilla maximum (read at the boot before any hit) and the number of damage states its string names.</summary>
    private sealed record GateSlot(CastleGate Gate, DestructableComponent Component, float VanillaMax, int States);

    private List<GateSlot> _gateSlots = new();
    private List<CastleGate> _hiddenGates = new();
    private MethodInfo? _gateReset;

    // Taken once at the boot, before the first hit and before the Hammer: the destructible's maximum is then the prefab's own (15000 at Helm's Deep, props_siege_gates_outer.xml:2944).
    // CastleGate.OnMissionReset is "protected internal override" (CastleGate.cs:341), so only reflection reaches it; the lookup is by name, and a binding row pins it (R10).
    private void CaptureGates(IReadOnlyList<CastleGate> visible, IReadOnlyList<CastleGate> hidden)
    {
        _gateSlots = new List<GateSlot>();
        foreach (var gate in visible)
        {
            var component = gate.GameEntity.GetFirstScriptOfType<DestructableComponent>();
            if (component == null)
            {
                _logger.LogWarning($"[LongDefeat][Boot] a visible gate at {At(gate.GameEntity.GlobalPosition)} has no destructible; the gate rows cannot act on it");
                continue;
            }

            _gateSlots.Add(new GateSlot(gate, component, component.MaxHitPoint, StatesOf(component)));
        }

        _hiddenGates = hidden.ToList();
        _gateReset = AccessTools.DeclaredMethod(typeof(CastleGate), "OnMissionReset");
        if (_gateReset == null)
            _logger.LogWarning("[LongDefeat][Boot] CastleGate.OnMissionReset was not found: a gate repair cannot reset the door");
        _logger.LogInfo($"[LongDefeat][Boot] gates {_gateSlots.Count} visible, vanilla maxima [{string.Join(", ", _gateSlots.Select(s => Num(s.VanillaMax)))}], states [{string.Join(", ", _gateSlots.Select(s => s.States))}], hidden {_hiddenGates.Count}");
    }

    private void ClearGates()
    {
        _gateSlots = new List<GateSlot>();
        _hiddenGates = new List<CastleGate>();
        _gateReset = null;
        _planeLogged = false;
    }

    // DestructableComponent builds its states by "DestructionStates.Replace(" ", string.Empty).Split(new char[1] { ',' })" (DestructableComponent.cs:174-176) and CalculateNextDestructionLevel steps by
    // MaxHitPoint / _destructionStates.Length (:405-418), so the count is read from the same public string.
    private static int StatesOf(DestructableComponent component) =>
        string.IsNullOrEmpty(component.DestructionStates) ? 0 : component.DestructionStates.Replace(" ", string.Empty).Split(',').Length;

    private static LdGateFacts FactsOf(GateSlot slot)
    {
        var c = slot.Component;
        return new LdGateFacts(GateMath.StateIndex(c.HitPoint, c.MaxHitPoint, slot.States), c.HitPoint, c.MaxHitPoint, slot.VanillaMax, c.IsDestroyed, slot.Gate.IsGateOpen);
    }

    public IReadOnlyList<LdGateFacts> ReadGates() => _gateSlots.Select(FactsOf).ToList();

    // "MaxHitPoint" is a public field and "HitPoint"'s setter clamps at 0 ("_hitPoint = TaleWorlds.Library.MathF.Max(value, 0f);", DestructableComponent.cs:117), so a broken gate stays broken. The hit points keep the gate's
    // share less one point (GateMath.KeepShare): the next hit computes its damage-state index from them, and a next hit that computes index 0 on a gate stored at 1 or more reads
    // "_destructionStates[_currentStateIndex - 1]" with -1 and throws (DestructableComponent.cs:504, E9b); a smaller fall only shows an earlier damage state.
    public LdGateWrite SetGateLevel(float factor)
    {
        var changes = new List<LdGateChange>();
        var valid = factor > 0f && !float.IsInfinity(factor);
        foreach (var slot in _gateSlots)
        {
            var before = FactsOf(slot);
            var touched = valid && slot.VanillaMax > 0f;
            if (touched)
            {
                var oldMax = slot.Component.MaxHitPoint;
                var newMax = slot.VanillaMax * factor;
                slot.Component.MaxHitPoint = newMax;
                slot.Component.HitPoint = GateMath.KeepShare(slot.Component.HitPoint, oldMax, newMax);
            }

            changes.Add(Changed(slot, before, touched, false));
        }

        return Written(changes);
    }

    // The maximum is written first, because the reset reads it: "_hitPoint = MaxHitPoint; _currentStateIndex = 0;" (DestructableComponent.cs:278-283). CastleGate.OnMissionReset runs the destructible's own
    // reset through "DestructibleComponentOnMissionReset?.Invoke();" and then "CollectGameEntities(calledFromOnInit: false);" (CastleGate.cs:341-348), which binds the door entities again; the destructible's
    // Reset() alone would leave "_door" and the colliders on the hidden damage-state entity (E2, E9). UsableMachine.OnMissionReset enables a machine that was disabled (the fall disabled it, E3). The reset leaves
    // the gate's navmesh open ("SetGateNavMeshState(isEnabled: true)" in OnDestroyed, CastleGate.cs:1051), so SetAutoOpenState(false) closes it: "SetGateNavMeshState(isEnabled: false)" on a closed gate
    // (CastleGate.cs:587-602). SetAbilityOfFaces is not called: this gate's destructible names no navmesh prefab, so it would do nothing (D17). A gate is closed only after its reset ran: OnDestroyed leaves
    // State at Closed (CastleGate.cs:1036-1052), so SetAutoOpenState(false) on a gate still destroyed would shut the breach's navmesh faces with no door in it. With no reset found a repair acts on no gate.
    public LdGateWrite RepairGates(float factor)
    {
        var changes = new List<LdGateChange>();
        var valid = factor > 0f && !float.IsInfinity(factor);
        foreach (var slot in _gateSlots)
        {
            var before = FactsOf(slot);
            var touched = valid && _gateReset != null && slot.VanillaMax > 0f && before.MissingShare >= GateMath.MinMissingShare;
            if (touched)
            {
                slot.Component.MaxHitPoint = slot.VanillaMax * factor;
                if (ResetGate(slot.Gate) && !slot.Component.IsDestroyed)
                    slot.Gate.SetAutoOpenState(false);
            }

            changes.Add(Changed(slot, before, touched, true));
        }

        return Written(changes);
    }

    private bool ResetGate(CastleGate gate)
    {
        try
        {
            _gateReset?.Invoke(gate, null);
            return _gateReset != null;
        }
        catch (TargetInvocationException ex)
        {
            _logger.LogWarning($"[LongDefeat][Gates] CastleGate.OnMissionReset threw {ex.InnerException?.GetType().Name}: {ex.InnerException?.Message}");
            return false;
        }
    }

    private LdGateChange Changed(GateSlot slot, LdGateFacts before, bool touched, bool countNear)
    {
        var near = countNear && touched ? DefendersNear(slot.Gate.GameEntity.GlobalPosition) : 0;
        return new LdGateChange(before, FactsOf(slot), slot.Gate.IsDisabled, slot.Gate.State == CastleGate.GateState.Closed, near, touched);
    }

    private LdGateWrite Written(List<LdGateChange> changes) => new(changes, _hiddenGates.Count, _hiddenGates.Count(h => !h.IsDestroyed), _gateReset != null);

    private int DefendersNear(Vec3 at) =>
        _mission.DefenderTeam.ActiveAgents.Count(a => a.IsHuman && a.IsActive() && (a.Position - at).Length <= GateRules.NearRadius);

    // The forward axis is the plane's normal and the keep only turns it round (GateGeometry); the box is also limited to the gate's height, so the wall walk above the door does not count. -1 when no standing gate had a
    // plane, so a sample that measured nothing is not a clean 0. The normal is logged once per run, because the keep at Helm's Deep is the defenders' reinforcement frame, about 69 m from the gate (A17).
    public int AttackersBehindGates(LdPoint? inside)
    {
        if (inside is not { } keep)
            return -1;

        var measured = false;
        var total = 0;
        List<LdPoint>? attackers = null;
        foreach (var slot in _gateSlots)
        {
            if (slot.Component.IsDestroyed)
                continue;

            var frame = slot.Gate.GameEntity.GetGlobalFrame();
            var at = Point(frame.origin);
            if (GateGeometry.PlaneOf(at, frame.rotation.f.x, frame.rotation.f.y, keep) is not { } plane)
                continue;

            LogPlane(at, plane, frame.rotation.f.x, frame.rotation.f.y, keep);
            measured = true;
            attackers ??= _mission.AttackerTeam.ActiveAgents.Where(a => a.IsHuman && a.IsActive() && !a.IsRunningAway).Select(a => Point(a.Position)).ToList();
            total += attackers.Count(a => GateGeometry.IsBehind(at, plane, a, GateRules.BehindDepth, GateRules.HalfWidth, GateRules.HeightBand));
        }

        return measured ? total : -1;
    }

    private bool _planeLogged;

    private void LogPlane(LdPoint gate, GatePlane plane, float forwardX, float forwardY, LdPoint keep)
    {
        if (_planeLogged)
            return;

        _planeLogged = true;
        _logger.LogInfo($"[LongDefeat][Gates] gate plane at ({Num(gate.X)}, {Num(gate.Y)}, {Num(gate.Z)}): normal ({Unit(plane.Nx)}, {Unit(plane.Ny)}), frame forward ({Unit(forwardX)}, {Unit(forwardY)}), keep ({Num(keep.X)}, {Num(keep.Y)}); "
            + $"the inside lies {Num(GateRules.BehindDepth)} m behind it, {Num(GateRules.HalfWidth)} m either side, {Num(GateRules.HeightBand)} m up or down");
    }

    private static string Unit(float v) => v.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

    public IReadOnlyList<LdPointFacts> PointsUsable(IReadOnlyList<LdPlace> points) => points.Select(SallyPointUsable).ToList();

    // TeamAISiegeDefender builds ArcherPositions from the scene's archer_position entities (TeamAISiegeDefender.cs:22-25) and TacticDefendCastle asks for min(count, 8 - melee) ranged formations (:284-289, E21),
    // so the count on the boot line is the engine's own. The units show which formation indexes are free for the posts (A23).
    public string DescribeArcherPositions()
    {
        var team = _mission.DefenderTeam;
        var positions = (team?.TeamAI as TeamAISiegeDefender)?.ArcherPositions.Count ?? -1;
        var formations = team?.FormationsIncludingEmpty.Select(f => $"{(int)f.FormationIndex}:{f.CountOfUnits}") ?? Enumerable.Empty<string>();
        return $"archer_positions={positions} formations={string.Join(",", formations)}";
    }
}
