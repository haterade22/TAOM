using System;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.SignatureStrikes.Hooks;

/// <summary>
/// Plays a strike's module sound once, at the attacker's head (#645). Each name's id is looked up
/// once per mission and logged (Native's module_sounds.xml header notes the index can be cached),
/// so a player log tells "the wrong sound" from "the right sound, inaudible". A name nothing
/// registers is expected to answer -1 (the engine's null sound id; the native contract is not
/// provable from managed code): the attacker then gives the engine's Yell voice instead, and the
/// miss is warned once per mission. Main thread only: the runner calls it from <c>OnMissionTick</c>.
/// </summary>
public sealed class StrikeSoundPlayer
{
    private readonly IModLogger _logger;
    private readonly Dictionary<string, int> _ids = new Dictionary<string, int>(StringComparer.Ordinal);

    public StrikeSoundPlayer(IModLogger logger) => _logger = logger;

    public void Clear() => _ids.Clear();

    /// <summary>Plays <paramref name="sound"/> for this strike and returns what played, for the
    /// strike's log line.</summary>
    public string Play(Mission mission, Agent attacker, string? sound)
    {
        if (string.IsNullOrEmpty(sound))
            return "none";

        if (!_ids.TryGetValue(sound!, out var id))
        {
            id = SoundEvent.GetEventIdFromString(sound);
            _ids[sound!] = id;
            if (id < 0)
                _logger.LogWarning($"[SignatureStrikes] module sound '{sound}' is not registered (module_sounds.xml); the attacker yells instead");
            else
                _logger.LogInfo($"[SignatureStrikes] module sound '{sound}' resolved to event id {id}");
        }

        if (id >= 0)
        {
            // An engine float handed straight to native, gated as CustomAttacksUtils keeps a
            // non-finite position out of MakeSound. What native does with one is unproven (the
            // spider AV that guard was written for traced to HandleBlowAux instead,
            // rca-spider-dismount-on-hit-2026-06-15.md), so this is a defence: no position, no
            // sound (csharp-architecture.md "Engine-Float Decision Gates").
            var position = attacker.GetEyeGlobalPosition();
            if (!FiniteFloatValidator.IsFinite(position.x)
                || !FiniteFloatValidator.IsFinite(position.y)
                || !FiniteFloatValidator.IsFinite(position.z))
                return "none (non-finite position)";

            // The one-shot call Native's module_sounds.xml documents.
            mission.MakeSound(id, position, false, true, -1, -1);
            return sound!;
        }

        attacker.MakeVoice(SkinVoiceManager.VoiceType.Yell, SkinVoiceManager.CombatVoiceNetworkPredictionType.NoPrediction);
        return "yell (sound not registered)";
    }
}
