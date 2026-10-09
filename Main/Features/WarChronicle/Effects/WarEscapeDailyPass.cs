using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.WarChronicle.Effects;

/// <summary>
/// The daily extra escape roll (docs/features/war-chronicle.md, Part A): for every kingdom whose
/// <see cref="WarEffectKind.PrisonerEscape"/> multiplier is above one, each captured lord that is eligible
/// takes one extra roll and may escape. Host only; <c>WarChronicleTickService</c> calls it after the
/// effects were expired and re-baked. A throwing engine read or a throwing escape is logged and skipped,
/// so it can never fail the campaign's daily tick or the ledger after it.
/// </summary>
public sealed class WarEscapeDailyPass
{
    private readonly IPrisonerEscapeAdapter _adapter;
    private readonly IWarEffectService _effects;
    private readonly WarEscapeService _escape;
    private readonly IModLogger _logger;

    public WarEscapeDailyPass(
        IPrisonerEscapeAdapter adapter, IWarEffectService effects, WarEscapeService escape, IModLogger logger)
    {
        _adapter = adapter;
        _effects = effects;
        _escape = escape;
        _logger = logger;
    }

    /// <summary>Runs the pass; returns how many lords escaped.</summary>
    public int Run()
    {
        IReadOnlyList<PrisonerEscapeSnapshot> lords;
        try
        {
            var kingdoms = BoostedKingdoms();
            if (kingdoms.Count == 0)
                return 0;
            lords = _adapter.GetCapturedLords(kingdoms);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[WarChronicle] Escape pass skipped ({ex.GetType().Name}: {ex.Message}).");
            return 0;
        }

        var escaped = 0;
        foreach (var lord in lords)
        {
            if (TryEscape(lord))
                escaped++;
        }

        return escaped;
    }

    private List<string> BoostedKingdoms()
    {
        var kingdoms = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var effect in _effects.Snapshot())
        {
            if (effect.Kind != WarEffectKind.PrisonerEscape || !seen.Add(effect.KingdomId))
                continue;
            if (_effects.GetMultiplier(effect.KingdomId, WarEffectKind.PrisonerEscape) > 1f)
                kingdoms.Add(effect.KingdomId);
        }

        return kingdoms;
    }

    private bool TryEscape(PrisonerEscapeSnapshot lord)
    {
        try
        {
            var multiplier = _effects.GetMultiplier(lord.KingdomId, WarEffectKind.PrisonerEscape);
            if (!_escape.IsEligible(lord, multiplier))
                return false;
            if (!_escape.ShouldEscape(lord, multiplier, _adapter.NextRoll()))
                return false;
            if (!_adapter.Escape(lord.HeroId))
                return false;

            _logger.LogInfo($"[WarChronicle] Escape: {lord.HeroId} of {lord.KingdomId} broke out (escape multiplier {multiplier:0.00}).");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[WarChronicle] Escape of {lord.HeroId} failed ({ex.GetType().Name}: {ex.Message}).");
            return false;
        }
    }
}
