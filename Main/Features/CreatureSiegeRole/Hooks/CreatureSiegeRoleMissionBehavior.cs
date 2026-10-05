using System;
using TAOM.Adapters;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.CreatureSiegeRole.Domain;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CreatureSiegeRole.Hooks;

/// <summary>
/// The mission boundary of the creature siege role (docs/features/creature-siege-role.md): decides in <c>AfterStart</c> whether
/// this battle is a wall battle the role acts in, and if so ticks the role's reconcile. Everything else is
/// <see cref="CreatureSiegeRoleService"/>. MUST be <c>: MissionLogic</c> (rca-looter-battle-nre-2026-05-24.md).
///
/// Lifecycle, read from the installed v1.5.4 <c>Mission</c> (the rule in harmony-patches.md): <c>AddMissionBehavior</c> calls only
/// <c>OnCreated</c> (Mission.cs:4699); <c>OnBehaviorInitialize</c> never reaches a behavior TAOM adds, because it runs over the
/// behaviors already listed (:3827) before the submodules add TAOM's (:3831), so there is no override of it; <c>AfterStart</c>
/// does (:3841), after every <c>EarlyStart</c> has set the team AI type, and before the first agent spawns. The whole of
/// <c>AfterStart</c> sits in a try: an exception leaving it restarts the mission load every frame (#699).
///
/// <c>OnMissionTick</c> is the one place a native write happens, and never overlaps the agent tick (<c>Mission.OnPreTick</c> waits
/// for it), so the reconcile needs no lock. The role clears its snapshot by token in both <c>OnEndMission</c> and
/// <c>OnRemoveBehavior</c>; <c>OnCreated</c> clears any record an earlier faulted mission left.
/// </summary>
public sealed class CreatureSiegeRoleMissionBehavior : MissionLogic
{
    private const string TickSite = "CreatureSiegeRoleMissionBehavior.OnMissionTick";

    private readonly CreatureSiegeRoleSettingsProvider _settings;
    private readonly CreatureSiegeRoleConfigProvider _config;
    private readonly IRaceManager _raceManager;
    private readonly IModLogger _logger;
    private readonly Action<string> _reportOffThread;

    // Set only by a successful activation. Null in every battle the role does not act in, so the tick costs a thread check.
    private CreatureSiegeRoleService? _service;

    public CreatureSiegeRoleMissionBehavior(CreatureSiegeRoleSettingsProvider settings, CreatureSiegeRoleConfigProvider config,
        IRaceManager raceManager, IModLogger logger)
    {
        _settings = settings;
        _config = config;
        _raceManager = raceManager;
        _logger = logger;
        // Made once: the tripwire is called every tick.
        _reportOffThread = logger.LogWarning;
    }

    public override void OnCreated()
    {
        base.OnCreated();
        CreatureSiegeSnapshot.Clear();
    }

    public override void AfterStart()
    {
        base.AfterStart();
        try
        {
            var service = new CreatureSiegeRoleService(new CreatureSiegeMissionAdapter(Mission), _raceManager,
                _config.GateDamageMultiplier, _logger);
            if (service.TryActivate(_settings.IsEnabled))
                _service = service;
        }
        catch (Exception ex)
        {
            // Inert, and clear by token whatever the activation published before it threw.
            _service = null;
            CreatureSiegeSnapshot.ClearIf(Mission);
            _logger.LogError(CreatureSiegeReport.ActivationFault(Mission?.SceneName ?? "an unknown scene", ex));
        }
    }

    public override void OnMissionTick(float dt)
    {
        if (MissionThreadGuard.NoteCall(TickSite, _reportOffThread))
            return;

        _service?.Tick();
    }

    protected override void OnEndMission()
    {
        base.OnEndMission();
        Stop();
    }

    public override void OnRemoveBehavior()
    {
        Stop();
        base.OnRemoveBehavior();
    }

    private void Stop()
    {
        _service?.Shutdown();
        _service = null;
    }
}
