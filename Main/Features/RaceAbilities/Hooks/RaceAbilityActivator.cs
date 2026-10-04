using TAOM.Features.AdvancedCombat;
using TAOM.Features.RaceAbilities.Domain;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.RaceAbilities.Hooks;

/// <summary>
/// Fires an ability: the soldier's own, then every ready kinsman's within the rally radius (the service
/// decides who qualifies), each scaled for its own tier and, for a kin-bonus ability, its own crowd. Refreshes
/// the soldier's stats and his horse's, holds his morale floor, shouts, and counts and logs the wave. Main
/// thread (the tree tick). Boundary code, game-tested (ADR-008).
/// </summary>
public sealed class RaceAbilityActivator
{
    private readonly RaceAbilityRuntime _runtime;
    private readonly MBList<Agent> _scratch = new MBList<Agent>();

    public RaceAbilityActivator(RaceAbilityRuntime runtime) => _runtime = runtime;

    public void Unleash(Agent initiator, RaceAbilityProfile profile, float now, RaceAbilityTriggerKind? trigger)
    {
        MissionThreadGuard.NoteCall("RaceAbilityActivator.Unleash", _runtime.Logger.LogWarning);
        var service = _runtime.Service;
        Activate(initiator, profile, now, cry: true);

        var rallied = 0;
        var team = initiator.Team;
        if (profile.RallyRadius > 0f && team != null)
        {
            _scratch.Clear();
            Mission.Current.GetNearbyAllyAgents(initiator.Position.AsVec2, profile.RallyRadius, team, _scratch);
            foreach (var kin in _scratch)
            {
                if (kin == null || kin == initiator)
                    continue;
                var recruit = service.IsRallyRecruit(kin.IsActive(), kin.IsAIControlled, kin.IsRetreating(), kin.Team == team,
                    ReferenceEquals(_runtime.ProfileOf(kin), profile),
                    service.IsOffCooldown(_runtime.Store.LastFiredAt(kin), now, profile.CooldownSeconds));
                if (!recruit)
                    continue;
                rallied++;
                Activate(kin, profile, now, cry: service.ShoutsOnRally(rallied));
            }
        }

        var playerSide = team?.IsPlayerAlly == true;
        _runtime.Waves.Record(profile.AbilityId, playerSide, now, 1 + rallied);
        var telemetry = _runtime.Telemetry;
        telemetry.Add(profile.AbilityId, RaceAbilityStat.Waves);
        telemetry.Add(profile.AbilityId, RaceAbilityStat.Activations, 1 + rallied);
        telemetry.Add(profile.AbilityId, RaceAbilityStat.Rallied, rallied);
        telemetry.AddTrigger(profile.AbilityId, trigger);
        if (_runtime.Settings.DebugLog || _runtime.WavesLogged++ < RaceAbilityRuntime.DetailedWaves)
            _runtime.Logger.LogInfo($"[RaceAbilities] {profile.AbilityId} by {initiator.Name} (tier {initiator.Character?.GetBattleTier()}, " +
                $"{(playerSide ? "player side" : "enemy side")}) at {now:0.0} s, trigger={trigger?.ToString() ?? "?"}, rallied={rallied}");
    }

    private void Activate(Agent agent, RaceAbilityProfile profile, float now, bool cry)
    {
        var service = _runtime.Service;
        var factor = service.MagnitudeFactor(agent.Character?.GetBattleTier() ?? 0, agent.IsHero, _runtime.Resolver.TierScaling);
        var kinBonus = profile.KinBonus == null
            ? 0f
            : service.KinBonusPercent(profile.KinBonus, _runtime.Sensor.CountKin(agent, profile, profile.KinBonus.Radius));
        var active = service.Scale(profile.Effects, factor, kinBonus);
        _runtime.Store.Activate(agent, profile, now, active, service.Scale(profile.Spent, factor));
        agent.UpdateAgentProperties();
        // The horse's speed rides its own stats, through its rider (#611).
        agent.MountAgent?.UpdateAgentProperties();
        TopUpMorale(_runtime, agent, active.MoraleFloor, profile.AbilityId);
        if (cry && _runtime.Settings.WarCries)
            WarCry(agent, profile.WarCry);
    }

    internal static void TopUpMorale(RaceAbilityRuntime runtime, Agent agent, float floor, string abilityId)
    {
        var topUp = runtime.Service.MoraleTopUp(agent.GetMorale(), floor);
        if (!(topUp > 0f))
            return;
        agent.ChangeMorale(topUp);
        runtime.Telemetry.Add(abilityId, RaceAbilityStat.MoraleRestored, (long)System.Math.Round(topUp));
    }

    private static void WarCry(Agent agent, string cry)
    {
        SkinVoiceManager.SkinVoiceType? voice = cry switch
        {
            "Yell" => SkinVoiceManager.VoiceType.Yell,
            "Charge" => SkinVoiceManager.VoiceType.Charge,
            "Victory" => SkinVoiceManager.VoiceType.Victory,
            "Grunt" => SkinVoiceManager.VoiceType.Grunt,
            _ => null,
        };
        // A race with no clip bound for the voice stays silent; that is the engine's own fallback.
        if (voice.HasValue)
            agent.MakeVoice(voice.Value, SkinVoiceManager.CombatVoiceNetworkPredictionType.NoPrediction);
    }
}
