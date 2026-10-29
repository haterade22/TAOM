using System;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.StagedSiege.Hooks;

/// <summary>
/// The mission boundary of the staged siege (docs/features/staged-siege.md): arms the service in <c>EarlyStart</c>, installs the
/// staged tactic in <c>AfterStart</c>, ticks the reconcile and ends the mission when the last point falls. Everything else is
/// <see cref="StagedSiegeService"/>, and the engine work beyond a delegation is <see cref="StagedSiegeMissionEdge"/>. MUST be
/// <c>: MissionLogic</c> (rca-looter-battle-nre-2026-05-24.md).
///
/// Why <c>EarlyStart</c> and not <c>AfterStart</c> (v1.5.6 <c>Mission</c>, :3821-3835): a behavior TAOM adds meets
/// <c>OnBehaviorInitialize</c> never, then every <c>EarlyStart</c>, then every <c>AfterStart</c>; the vanilla siege spawn handler
/// calls <c>InitWithSinglePhase</c> from its own <c>AfterStart</c>, and the Harmony prefix there needs the service already armed.
/// The tactic waits for <c>AfterStart</c> because the team AI is set up by the other <c>EarlyStart</c>s. There is no
/// <c>OnBehaviorInitialize</c> override. Each override sits in a try: an exception leaving a start callback restarts the mission
/// load every frame (#699). <c>OnMissionTick</c> never overlaps the agent tick, so the service needs no lock; it first drains
/// <see cref="Commands"/>, because a console command runs on its own thread and only enqueues (a forced stage, open gates and
/// the show flags are applied here, on the main thread, before the service ticks). <c>OnCreated</c> clears any record an earlier
/// faulted mission left.
/// </summary>
public sealed class StagedSiegeMissionBehavior : MissionLogic
{
    private const string TickSite = "StagedSiegeMissionBehavior.OnMissionTick";

    private readonly StagedSiegeSettingsProvider _settings;
    private readonly StagedSiegeSceneCatalog _catalog;
    private readonly IModLogger _logger;
    private readonly Action<string> _reportOffThread;
    private readonly StagedSiegeMissionEdge _edge;

    public StagedSiegeMissionBehavior(StagedSiegeSettingsProvider settings, StagedSiegeSceneCatalog catalog, IModLogger logger)
    {
        _settings = settings;
        _catalog = catalog;
        _logger = logger;
        _edge = new StagedSiegeMissionEdge(logger);
        // Made once: the tripwire is called every tick.
        _reportOffThread = logger.LogWarning;
    }

    /// <summary>Built in <c>EarlyStart</c> for every battle; armed only when the rules say this is a staged siege.</summary>
    internal StagedSiegeService? Service { get; private set; }

    /// <summary>The console's requests, applied at the start of the next mission tick.</summary>
    internal StagedSiegeCommandQueue Commands { get; } = new();

    public override void OnCreated()
    {
        base.OnCreated();
        StagedSiegeSlot.ClearArmed();
    }

    public override void EarlyStart()
    {
        base.EarlyStart();
        try
        {
            var adapter = new StagedSiegeMissionAdapter(Mission, team => new TacticStagedObjective(team));
            var service = new StagedSiegeService(adapter, _catalog, _logger);
            Service = service;
            service.TryArm(_settings.IsEnabled);
        }
        catch (Exception ex)
        {
            Service = null;
            StagedSiegeSlot.ClearIf(Mission);
            _edge.Fault("EarlyStart", ex);
        }
    }

    public override void AfterStart()
    {
        base.AfterStart();
        try
        {
            Service?.InstallTactics();
        }
        catch (Exception ex)
        {
            _edge.Fault("AfterStart", ex);
        }
    }

    public override void OnMissionTick(float dt)
    {
        if (MissionThreadGuard.NoteCall(TickSite, _reportOffThread))
            return;

        _edge.DrainCommands(Commands, Service, Mission);
        Service?.Tick();
    }

    public override void OnAgentBuild(Agent agent, Banner banner) => _edge.NoteAgentBuilt(Service, agent);

    public override bool MissionEnded(ref MissionResult missionResult) => _edge.TryEnd(Service, Mission, ref missionResult);

    public override void OnMissionResultReady(MissionResult missionResult)
    {
        base.OnMissionResultReady(missionResult);
        _edge.LogResultReady(Service, missionResult);
    }

    protected override void OnEndMission()
    {
        base.OnEndMission();
        _edge.Shutdown(Service);
    }

    public override void OnRemoveBehavior()
    {
        _edge.Shutdown(Service);
        base.OnRemoveBehavior();
    }
}
