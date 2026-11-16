using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Domain;
using TAOM.Core.Logging;

namespace TAOM.Features.OrcBattleVoice;

/// <summary>
/// Gives male adult AI agents of the plain <c>orc</c> race the Uruk battle voice (<c>uruk_01</c>: grunts, pain, death and
/// yells) instead of the vanilla human male voices their Armory skin lists (<c>male_02</c> to <c>male_08</c>).
/// TAOM-only route C of docs/features/kingdom-voices.md: the voice is set on the agent's visuals at spawn and
/// again after every in-place skin rebuild, and the Armory's skins.xml is not touched. It covers every male
/// adult orc agent in any mission (soldiers, townsfolk, notables, heroes), not only soldiers.
/// <para>
/// The engine hands out voice definitions only as opaque native indices
/// (<c>SkinVoiceManager.GetVoiceDefinitionListWithMonsterSoundAndCollisionInfoClassName</c>) and has no
/// name-to-index lookup. So the first orc of a mission is probed: each <c>human</c>-class index is set on it in
/// turn and the definition name the agent then reports (<c>Agent.GetAgentVoiceDefinition</c>) is recorded, until
/// <c>uruk_01</c> answers. Every later orc gets that index with one call. If no index answers, the probed orc
/// goes back to the definition it was built with at the seed pitch. If the report does not follow the setter,
/// probing stops and the orc keeps whatever the last set gave it. Either way a Fail line is written and the
/// feature stands down for the mission.
/// </para>
/// <para>
/// Left alone: other races, female orcs (the adult female orc skin lists vanilla <c>female_*</c>, and
/// <c>uruk_01</c> is a male voice), the player's own agent (its voice was chosen in character creation),
/// children (below <see cref="AdultAge"/>), an agent without <c>AgentVisuals</c> is skipped, and orcs already on
/// <c>uruk_01</c>. The same call is safe to repeat on one agent: it does nothing while the agent reports
/// <c>uruk_01</c> and sets it again after a rebuild reverted it. One instance per mission, main thread only.
/// </para>
/// </summary>
public sealed class OrcBattleVoiceService
{
    internal const string CheckId = "taom.orc-battle-voice.apply";
    internal const string OrcRace = "orc";
    internal const string UrukVoice = "uruk_01";
    internal const string SoundClass = "human";

    // Vanilla's adult age (a hero comes of age at 18; v1.5.7 Agent.cs:5456 uses 14f only for the adult body
    // underwear). Orc child townsfolk in Misty Mountain Orcs towns are below it and keep their own voice.
    internal const float AdultAge = 18f;

    // 1 / 2^31, the factor vanilla uses to turn a seed into a mount's voice pitch
    // (v1.5.7 MountVisualCreator.SetVoiceDefinition: `float voicePitch = (float)num2 * 4.656613E-10f;`).
    private const double SeedToPitch = 1.0 / 2147483648.0;

    private const string Tag = "[OrcBattleVoice]";

    private readonly IVoiceDefinitionCatalogAdapter _catalog;
    private readonly IRaceManager _races;
    private readonly IOutcomeLog _outcomes;
    private readonly IModLogger _logger;
    private readonly Dictionary<string, int> _indexByName = new(StringComparer.Ordinal);

    private bool _probed;
    private bool _unavailable;
    private bool _passed;
    private bool _mismatchReported;
    private bool _ended;
    private int _urukIndex = -1;

    public OrcBattleVoiceService(IVoiceDefinitionCatalogAdapter catalog, IRaceManager races, IOutcomeLog outcomes, IModLogger logger)
    {
        _catalog = catalog;
        _races = races;
        _outcomes = outcomes;
        _logger = logger;
    }

    /// <summary>
    /// The voice pitch for an orc, in [0, 1], from its body properties seed, the way vanilla makes a mount's.
    /// Computed in long so <see cref="int.MinValue"/> cannot overflow the absolute value.
    /// </summary>
    public static float PitchFor(int seed) => (float)Math.Min(1.0, Math.Abs((long)seed) * SeedToPitch);

    /// <summary>
    /// Handles one freshly built agent, or one whose skin vanilla rebuilt in place (the engine's OnEquipItemsFromSpawnEquipment listener call). Repeating it
    /// on one agent is safe. An engine exception propagates to the mission behavior.
    /// </summary>
    public void OnAgentBuild(IOrcBattleVoiceAgent agent)
    {
        if (_ended || _unavailable || agent == null) return;
        if (!agent.IsHuman || agent.IsFemale || agent.IsPlayerControlled || !agent.HasVisuals) return;
        if (!(agent.Age >= AdultAge)) return; // a NaN age is not an adult
        if (!string.Equals(_races.GetRaceNameFromId(agent.Race), OrcRace, StringComparison.Ordinal)) return;

        var built = agent.VoiceDefinitionName;
        if (string.Equals(built, UrukVoice, StringComparison.Ordinal)) return;

        float pitch = PitchFor(agent.BodyPropertiesSeed);
        if (!_probed && !Probe(agent, built, pitch)) return;

        agent.SetVoiceDefinition(_urukIndex, pitch);
        Report(agent.VoiceDefinitionName, built);
    }

    /// <summary>Stops the service; a spawn after the mission end changes nothing.</summary>
    public void End() => _ended = true;

    private bool Probe(IOrcBattleVoiceAgent agent, string built, float pitch)
    {
        _probed = true;
        var indices = _catalog.GetDefinitionIndices(SoundClass) ?? Array.Empty<int>();
        bool readbackFollows = true;

        foreach (int index in indices)
        {
            agent.SetVoiceDefinition(index, pitch);
            var name = agent.VoiceDefinitionName;
            if (string.IsNullOrEmpty(name)) continue;
            if (_indexByName.ContainsKey(name))
            {
                // Definitions are merged by name, so two indices never share one: the report is not the setter's.
                readbackFollows = false;
                break;
            }
            _indexByName[name] = index;
            if (string.Equals(name, UrukVoice, StringComparison.Ordinal))
            {
                _urukIndex = index;
                return true;
            }
        }

        _unavailable = true;
        if (readbackFollows && _indexByName.TryGetValue(built, out int original))
            agent.SetVoiceDefinition(original, pitch);

        string reason = indices.Length == 0 ? "no-human-voices" : readbackFollows ? "uruk-voice-missing" : "readback-not-following";
        string detail = string.Format(System.Globalization.CultureInfo.InvariantCulture, "reason={0} probed={1} named={2} voice={3}",
            reason, indices.Length, _indexByName.Count, UrukVoice);
        _outcomes.Fail(CheckId, reason, detail);
        _logger.LogWarning($"{Tag} orc voice not set for this mission: {detail}");
        return false;
    }

    private void Report(string reported, string built)
    {
        if (string.Equals(reported, UrukVoice, StringComparison.Ordinal))
        {
            if (_passed) return;
            _passed = true;
            _outcomes.Pass(CheckId, "", string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "voice={0} index={1} built={2}", UrukVoice, _urukIndex, built));
            return;
        }

        if (_mismatchReported) return;
        _mismatchReported = true;
        _outcomes.Fail(CheckId, "set-not-taken", string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "reason=set-not-taken index={0} reported={1}", _urukIndex, reported));
    }
}
