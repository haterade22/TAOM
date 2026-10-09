using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Core.Logging;
using TAOM.Core.Validation;

namespace TAOM.Features.WarChronicle.Effects;

/// <summary>
/// The timed-effect registry (docs/features/war-chronicle.md, Part A). The rows are the source of
/// truth; every multiplier is baked from them with the MCM strength on Apply, RemoveSource, Expire and
/// Restore, and swapped in as one new dictionary, so <see cref="GetMultiplier"/> is a
/// lock-free dictionary lookup and an array read on a map that never changes under a reader.
/// A process-lifetime singleton holding campaign state: <see cref="ResetForNewSession"/> is called
/// from <c>WarChronicleBehavior</c>'s constructor.
/// </summary>
public sealed class WarEffectService : IWarEffectService
{
    private static readonly int KindCount = Enum.GetValues(typeof(WarEffectKind)).Cast<int>().Max() + 1;

    private readonly IWarChronicleSettingsProvider _settings;
    private readonly IModLogger _logger;
    private readonly List<WarEffect> _effects = new List<WarEffect>();
    private volatile Dictionary<string, float[]> _baked = new Dictionary<string, float[]>(StringComparer.Ordinal);

    public WarEffectService(IWarChronicleSettingsProvider settings, IModLogger logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public void Apply(WarEffect effect)
    {
        if (!TryUpsert(effect))
            return;
        Bake();
    }

    public void RemoveSource(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId))
            return;
        if (_effects.RemoveAll(e => string.Equals(e.SourceId, sourceId, StringComparison.Ordinal)) > 0)
            Bake();
    }

    public float GetMultiplier(string? kingdomId, WarEffectKind kind)
    {
        if (kingdomId == null || !_baked.TryGetValue(kingdomId, out var slots))
            return 1f;
        var index = (int)kind;
        return (uint)index < (uint)slots.Length ? slots[index] : 1f;
    }

    public void Expire(double nowHours)
    {
        if (!FiniteFloatValidator.IsFinite(nowHours))
            return;
        _effects.RemoveAll(e => e.EndTimeHours <= nowHours);
        Bake();
    }

    public IReadOnlyList<WarEffect> Snapshot() => new List<WarEffect>(_effects);

    public void RestoreFromSave(IEnumerable<WarEffect> effects)
    {
        _effects.Clear();
        if (effects != null)
        {
            foreach (var effect in effects)
                TryUpsert(effect);
        }

        Bake();
    }

    public void ResetForNewSession()
    {
        _effects.Clear();
        Bake();
    }

    private bool TryUpsert(WarEffect effect)
    {
        var problem = Validate(effect);
        if (problem != null)
        {
            _logger.LogWarning($"[WarChronicle] Rejected a war effect: {problem}.");
            return false;
        }

        var at = _effects.FindIndex(e => SameKey(e, effect));
        if (at >= 0)
            _effects[at] = effect;
        else
            _effects.Add(effect);
        return true;
    }

    private static string? Validate(WarEffect? effect)
    {
        if (effect == null)
            return "it is null";
        if (string.IsNullOrWhiteSpace(effect.SourceId))
            return "its source id is empty";
        if (string.IsNullOrWhiteSpace(effect.KingdomId))
            return $"{effect.SourceId} names no kingdom";
        if (!Enum.IsDefined(typeof(WarEffectKind), effect.Kind))
            return $"{effect.SourceId} has the undefined kind {(int)effect.Kind}";
        if (!FiniteFloatValidator.IsFinite(effect.Magnitude))
            return $"{effect.SourceId} has a non-finite magnitude";
        if (!FiniteFloatValidator.IsFinite(effect.EndTimeHours))
            return $"{effect.SourceId} has a non-finite end time";
        return null;
    }

    private static bool SameKey(WarEffect a, WarEffect b) =>
        a.Kind == b.Kind
        && string.Equals(a.SourceId, b.SourceId, StringComparison.Ordinal)
        && string.Equals(a.KingdomId, b.KingdomId, StringComparison.Ordinal);

    private void Bake()
    {
        var strength = WarEffectMath.SanitizeStrength(_settings.WarEffectStrength);

        var sums = new Dictionary<string, double[]>(StringComparer.Ordinal);
        foreach (var effect in _effects)
        {
            if (!sums.TryGetValue(effect.KingdomId, out var perKind))
            {
                perKind = new double[KindCount];
                sums[effect.KingdomId] = perKind;
            }

            perKind[(int)effect.Kind] += effect.Magnitude;
        }

        var baked = new Dictionary<string, float[]>(sums.Count, StringComparer.Ordinal);
        foreach (var pair in sums)
        {
            var slots = new float[KindCount];
            for (var kind = 0; kind < KindCount; kind++)
                slots[kind] = WarEffectMath.Clamp((WarEffectKind)kind, (float)(1d + strength * pair.Value[kind]));
            baked[pair.Key] = slots;
        }

        _baked = baked;
    }
}
