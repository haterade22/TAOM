using System;
using System.Collections.Generic;
using TAOM.Features.AdvancedCombat;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.RaceAbilities.Hooks;

/// <summary>
/// An outline in the profile's colour on each active soldier nearest the camera (up to maxGlowing), and sparks on
/// the soldier who fires and every third kinsman who joins him. The service and the ledger decide; this class
/// talks to the engine, on the main thread only (#634): the activator, the ticker and the deaths handler, the last
/// inline in the engine's removal callback when it arrives on the main thread. The engine's outline setters check
/// nothing, so every write goes through <see cref="Paint"/>'s guard. Each call catches its own failures and reports
/// once per battle on its own line, so a visual never aborts a rally, a phase change or a death. ADR-008.
/// </summary>
public sealed class RaceAbilityVisuals
{
    private readonly RaceAbilityRuntime _runtime;
    private readonly RaceAbilityGlowLedger<Agent> _ledger = new RaceAbilityGlowLedger<Agent>();
    private readonly List<(Agent Key, uint Color)> _paint = new List<(Agent Key, uint Color)>();
    private readonly List<Agent> _clear = new List<Agent>();
    private int? _burstId;
    private bool _failureLogged;

    public RaceAbilityVisuals(RaceAbilityRuntime runtime) => _runtime = runtime;

    // How many soldiers wear an outline now, for taom.print_race_abilities.
    public int LitCount => _ledger.LitCount;

    // Switched on in MCM, and the battle UI not hidden: vanilla's own outline views stand down for Hide Battle UI.
    private bool Shown => _runtime.Settings.Glow && !BannerlordConfig.HideBattleUI;

    // Lights the active soldiers nearest the camera and clears everyone who dropped out: pushed out by nearer ones,
    // switched off or hidden. Paints again every pass, because an equipment rebuild (a new banner bearer) replaces
    // the meshes the outline was painted on.
    public void Refresh()
    {
        MissionThreadGuard.NoteCall("RaceAbilityVisuals.Refresh", _runtime.Warn);
        try
        {
            var config = _runtime.Resolver.VisualsConfig;
            var on = Shown && config.MaxGlowing > 0;
            if (on)
            {
                var camera = Mission.Current.GetCameraFrame().origin;
                foreach (var pair in _runtime.Store.Entries)
                {
                    var color = _runtime.Service.GlowFor(pair.Value);
                    var agent = pair.Key;
                    if (color.HasValue && agent.IsActive() && AgentSlotIdentity.IsCurrentOccupant(agent))
                        _ledger.Offer(agent, color.Value, agent.Position.DistanceSquared(camera));
                }
            }
            _ledger.Settle(on ? config.MaxGlowing : 0, _paint, _clear);
            foreach (var agent in _clear)
                Paint(agent, null, false);
            foreach (var lit in _paint)
                Paint(lit.Key, lit.Color, config.SeeThrough);
        }
        catch (Exception ex)
        {
            ReportFailure(nameof(Refresh), ex);
        }
        finally
        {
            _paint.Clear();
            _clear.Clear();
        }
    }

    // One burst of the configured effect at the soldier's chest, fired and forgotten as vanilla's own bursts are.
    public void Burst(Agent agent)
    {
        try
        {
            if (!Shown)
                return;
            var id = _burstId ??= ResolveBurst();
            if (id < 0)
                return;
            var frame = MatrixFrame.Identity;
            frame.origin = agent.GetChestGlobalPosition();
            Mission.Current.Scene.CreateBurstParticle(id, frame);
        }
        catch (Exception ex)
        {
            ReportFailure(nameof(Burst), ex);
        }
    }

    // A soldier whose outline must go now, his window over or his body fallen: cleared while the body is still his,
    // so a spent soldier is dark at once and a corpse never keeps it.
    public void Forget(Agent agent)
    {
        try
        {
            if (_ledger.Forget(agent))
                Paint(agent, null, false);
        }
        catch (Exception ex)
        {
            ReportFailure(nameof(Forget), ex);
        }
    }

    // Mission end: drops every reference. The mission's agents are being torn down, so no native call.
    public void Clear()
    {
        _ledger.Clear();
        _paint.Clear();
        _clear.Clear();
        _failureLogged = false;
    }

    // Vanilla's guard (not deleted, valid visuals) plus slot identity: a deleted agent's handle answers for the
    // slot's next tenant (#592). Null clears; a colour paints, see-through or not.
    private static void Paint(Agent agent, uint? color, bool seeThrough)
    {
        if (!AgentSlotIdentity.IsCurrentOccupant(agent) || agent.State == AgentState.Deleted)
            return;
        var visuals = agent.AgentVisuals;
        if (visuals != null && visuals.IsValid())
            visuals.SetContourColor(color, seeThrough);
    }

    // Once per process: the engine registers its effects with the game and answers -1 for a name it does not know,
    // which turns the sparks off with one warning.
    private int ResolveBurst()
    {
        var name = _runtime.Resolver.VisualsConfig.Burst;
        if (name.Length == 0)
            return -1;
        var id = ParticleSystemManager.GetRuntimeIdByName(name);
        if (id < 0)
            _runtime.Logger.LogWarning($"[RaceAbilities] visuals.burst '{name}' is not a particle effect the engine knows; no sparks");
        return id;
    }

    private void ReportFailure(string site, Exception ex)
    {
        if (_failureLogged)
            return;
        _failureLogged = true;
        _runtime.Logger.LogError($"[RaceAbilities] visuals {site} threw {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
    }
}
