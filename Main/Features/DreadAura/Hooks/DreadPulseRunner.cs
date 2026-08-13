using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Features.DreadAura.Domain;

namespace TAOM.Features.DreadAura.Hooks;

/// <summary>
/// Executes one dread pulse: query the enemies near a source, then drain each eligible target.
/// Split from <see cref="DreadAuraMissionLogic"/>, which is left owning only the gate and the
/// schedule (ADR-002).
///
/// Boundary class. It touches <c>Agent</c> directly and delegates every decision to
/// <see cref="IDreadAuraService"/>; nothing here decides how much morale to remove.
/// </summary>
public sealed class DreadPulseRunner
{
    private readonly IDreadAuraService _service;
    private readonly IDreadAuraSettingsProvider _settings;

    // Reused across pulses. Every Mission.GetNearby* overload Clear()s the list it is handed, so
    // one instance field keeps the pulse allocation-free.
    private readonly MBList<Agent> _nearbyBuffer = new MBList<Agent>();

    public DreadPulseRunner(IDreadAuraService service, IDreadAuraSettingsProvider settings)
    {
        _service = service;
        _settings = settings;
    }

    public void Clear() => _nearbyBuffer.Clear();

    /// <param name="elapsed">Seconds since THIS source last pulsed, already bounded by the
    /// scheduler's catch-up ceiling.</param>
    public void Run(
        Mission mission,
        DreadSourceTracker.DreadSource source,
        float elapsed,
        Team playerTeam,
        bool affectsPlayerTroops)
    {
        var sourceTeam = source.Agent.Team;
        var moraleModel = MissionGameModels.Current?.BattleMoraleModel;
        if (sourceTeam == null || moraleModel == null)
            return;

        // Read LIVE, once per pulse, never snapshotted onto the tracked source: the MCM sliders
        // must affect wraiths already on the field, not only ones that spawn after the change.
        var radius = _settings.Radius;
        var innerRadius = _settings.InnerRadius;
        var moralePerSecond = _settings.MoralePerSecond;

        var sourcePosition = source.Agent.Position.AsVec2;

        // Enemy filtering happens NATIVE-side, so allies never enter the managed loop at all. That
        // is also why "a wraith's own kind are unaffected" needs no explicit guard.
        _nearbyBuffer.Clear();
        mission.GetNearbyEnemyAgents(sourcePosition, radius, sourceTeam, _nearbyBuffer);

        var voicePlayed = false;

        foreach (var target in _nearbyBuffer)
        {
            // GetNearbyAgentsAux adds its `DotNetObject.GetManagedObjectWithId(...) as Agent`
            // result UNCONDITIONALLY, so a reclaimed id lands in this list as null. The gate's
            // null check is load-bearing, not defensive decoration.
            if (!DreadAgentGate.CanAffect(target))
                continue;

            if (!affectsPlayerTroops && playerTeam != null && ReferenceEquals(target.Team, playerTeam))
                continue;

            // CALL the registered morale model, never override it: this yields
            // moralePerSecond / max(1, GetMoraleResistance()), so tier and hero resistance scale
            // dread at exact parity with how the engine scales kill-morale. Overriding
            // CalculateMoraleChangeToCharacter would be wrong, because the CALLER applies the sign
            // and an override would equally shrink the morale a tough troop GAINS from kills.
            var context = new DreadDrainContext(
                moraleModel.CalculateMoraleChangeToCharacter(target, moralePerSecond),
                (target.Position.AsVec2 - sourcePosition).LengthSquared,
                radius,
                innerRadius,
                target.Character?.Race,
                target.GetMorale(),
                elapsed);

            var drain = _service.ComputeDrain(in context);
            if (!(drain > 0f))
                continue;

            target.ChangeMorale(-drain);

            if (!voicePlayed && _service.ShouldPlayFearVoice(MBRandom.RandomFloat))
            {
                // Degrades to silence, not an error, for a race with no Fear clip bound.
                target.MakeVoice(
                    SkinVoiceManager.VoiceType.Fear,
                    SkinVoiceManager.CombatVoiceNetworkPredictionType.NoPrediction);
                voicePlayed = true;
            }
        }
    }
}
