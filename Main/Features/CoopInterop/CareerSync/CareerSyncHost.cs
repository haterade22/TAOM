using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;

namespace TAOM.Features.CoopInterop.CareerSync;

/// <summary>
/// The co-op server's side of career sync (docs/features/coop-interop.md, "Career sync"). The server's career book is
/// what CareerPersistenceBehavior saves for every hero, so a career stored here is in the server's next save and in the
/// save every joining player loads. A player's report is checked with <see cref="CareerSyncRules"/> against the hero
/// the sender's connection controls (TAOM.CoopCompat resolves it; the id in the report is only compared). The relay
/// sends what <see cref="Handle"/> returns, and passes on <see cref="StoredCareerChanged"/> for changes the server
/// itself makes (a death clear, a tree migration).
/// </summary>
public sealed class CareerSyncHost
{
    private readonly ICareerDataService _data;
    private readonly ICareerRegistry _registry;
    private readonly ICareerPassiveService _passives;
    private readonly ICareerQuestConfigProvider _quests;
    private readonly ICareerSyncHeroAdapter _heroes;
    private readonly CoopSettingsProvider _settings;
    private readonly IModLogger _logger;
    private readonly Dictionary<string, long> _lastSequence = new(StringComparer.Ordinal);
    private HashSet<string>? _questFlags;
    private bool _offLogged;

    public CareerSyncHost(ICareerDataService data, ICareerRegistry registry, ICareerPassiveService passives,
        ICareerQuestConfigProvider quests, ICareerSyncHeroAdapter heroes, CoopSettingsProvider settings, IModLogger logger)
    {
        _data = data;
        _registry = registry;
        _passives = passives;
        _quests = quests;
        _heroes = heroes;
        _settings = settings;
        _logger = logger;
        _data.HeroCareerChanged += OnLocalChange;
    }

    /// <summary>A change this process made to its own career book, with the hero's last handled sequence.</summary>
    public event Action<CareerSyncAnswer>? StoredCareerChanged;

    /// <param name="senderHeroId">The hero the sender's current connection controls, or "" when it controls none.</param>
    public CareerSyncOutcome Handle(string senderHeroId, CareerSyncSnapshot sent, long sequence, bool joinReport)
    {
        if (!_settings.CareerSync)
        {
            if (!_offLogged) _logger.LogInfo("[CareerSync] a career report arrived, but career sync is switched off on this server.");
            _offLogged = true;
            return new CareerSyncOutcome(CareerSyncRefusal.SwitchedOff, null, null);
        }

        if (string.IsNullOrEmpty(senderHeroId) || sent == null || sent.HeroId != senderHeroId)
        {
            _logger.LogWarning($"[CareerSync] ignored a career for '{sent?.HeroId}' from a connection that plays '{senderHeroId}'.");
            return new CareerSyncOutcome(CareerSyncRefusal.NotYourHero, null, null);
        }

        _lastSequence[senderHeroId] = sequence;
        var stored = Stored(senderHeroId);

        if (joinReport && _data.HasCareer(senderHeroId))
        {
            return stored!.SameAs(sent)
                ? new CareerSyncOutcome(CareerSyncRefusal.None, null, null)
                : new CareerSyncOutcome(CareerSyncRefusal.None, new CareerSyncAnswer(stored, sequence, false, true), null);
        }

        var hero = _heroes.Find(senderHeroId);
        var refusal = hero == null
            ? CareerSyncRefusal.UnknownHero
            : CareerSyncRules.Check(sent, _registry, hero.Level, _questFlags ??= CareerSyncRules.QuestFlags(_quests.LoadQuests()));
        if (refusal != CareerSyncRefusal.None)
        {
            _logger.LogWarning($"[CareerSync] refused the career '{sent.CareerId}' for '{senderHeroId}': {refusal}.");
            var copy = stored ?? new CareerSyncSnapshot(senderHeroId, null, Array.Empty<string>(), Array.Empty<int>(), Array.Empty<string>());
            var known = !string.IsNullOrEmpty(stored?.CareerId);
            return new CareerSyncOutcome(refusal, new CareerSyncAnswer(copy, sequence, true, known), null);
        }

        if (!string.IsNullOrEmpty(sent.CareerId) && !_registry.IsEligible(sent.CareerId!, hero!))
            _logger.LogWarning($"[CareerSync] '{sent.CareerId}' is not eligible for '{senderHeroId}' by the server's rules; stored all the same.");

        _data.ApplyHeroCareer(sent.ToData());
        _passives.RefreshCache(_data, _registry);
        _logger.LogInfo($"[CareerSync] stored '{sent.CareerId}' for '{senderHeroId}' ({sent.Choices.Count} choices).");
        return new CareerSyncOutcome(CareerSyncRefusal.None, null, new CareerSyncAnswer(sent, sequence, false, true));
    }

    /// <summary>
    /// The stored career of every hero a player reported or the server changed in this process, for a player who just
    /// said hello: a change passed on while that player loaded reached only the players who had said hello. Sequence 0:
    /// a client counts its reports from 1 in each process, so its own hero's entry never beats its join report.
    /// </summary>
    public IReadOnlyList<CareerSyncAnswer> Baseline()
    {
        var answers = new List<CareerSyncAnswer>();
        if (!_settings.CareerSync) return answers;

        foreach (var heroId in _lastSequence.Keys)
        {
            if (Stored(heroId) is { } stored)
                answers.Add(new CareerSyncAnswer(stored, 0, false, true));
        }
        return answers;
    }

    private CareerSyncSnapshot? Stored(string heroId) =>
        _data.GetAllData().TryGetValue(heroId, out var entry) && entry != null ? CareerSyncSnapshot.Of(entry) : null;

    private void OnLocalChange(string heroId)
    {
        if (string.IsNullOrEmpty(heroId) || !_settings.CareerSync) return;

        var stored = Stored(heroId);
        if (stored == null) return;

        _lastSequence.TryGetValue(heroId, out var sequence);
        _lastSequence[heroId] = sequence;
        StoredCareerChanged?.Invoke(new CareerSyncAnswer(stored, sequence, false, true));
    }
}
