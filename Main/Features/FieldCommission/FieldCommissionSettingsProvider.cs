using System.Collections.Generic;
using TAOM.Core.Validation;
using TAOM.Features.FieldCommission.Domain;

namespace TAOM.Features.FieldCommission;

/// <summary>
/// Layers the player's MCM choices over the pack author's JSON defaults for Battlefield Promotions.
///
/// It implements <see cref="IFieldCommissionConfigProvider"/> — the same interface it decorates —
/// rather than exposing a parallel scalar surface like the other <c>*SettingsProvider</c> classes.
/// That is deliberate: the merit service, the offer-flow service, the cheats and the campaign
/// behaviour already read six fields off one <see cref="FieldCommissionConfig"/>, and
/// <c>GetConfig()</c> is called from <c>CampaignEvents.TickEvent</c>. Keeping the interface means
/// every consumer picks up live MCM values with no constructor change, and caching on the snapshot
/// means the tick path allocates nothing while the player is not touching sliders.
///
/// <c>TaomSettings.Instance</c> is read on every call rather than captured, which is what makes the
/// MCM properties honestly <c>RequireRestart = false</c> — the JSON file still needs a full
/// application restart (the decorated provider is a <c>Lazy</c> singleton), but the knobs do not.
/// </summary>
public sealed class FieldCommissionSettingsProvider : IFieldCommissionConfigProvider
{
    // Slider bounds. These must match the ranges declared on the TaomSettings properties; the MCM UI
    // already constrains the player, so these exist for the case where a hand-edited MCM json carries
    // an out-of-range value (MCM deserializes it verbatim — the attribute range is a UI hint, not a
    // load-time clamp).
    internal const float MinRatioThreshold = 0.1f;
    internal const float MaxRatioThreshold = 3.0f;
    internal const int MinMeritPerKill = 1;
    internal const int MaxMeritPerKill = 10;
    internal const int MinMeritThreshold = 1;
    internal const int MaxMeritThreshold = 100;
    internal const int MinRetainerAllowance = 0;
    internal const int MaxRetainerAllowance = 10;
    internal const int MinMaxOffersPerBattle = 1;
    internal const int MaxMaxOffersPerBattle = 20;

    private readonly IFieldCommissionConfigProvider _jsonProvider;

    private FieldCommissionConfig _merged;
    private FieldCommissionMcmSnapshot _mergedFrom;
    private FieldCommissionConfig _mergedJsonSource;

    public FieldCommissionSettingsProvider(IFieldCommissionConfigProvider jsonProvider)
    {
        _jsonProvider = jsonProvider;
    }

    public FieldCommissionConfig GetConfig() => GetConfig(Capture());

    /// <summary>Test seam — takes the snapshot as data so the merge is exercisable without MCM loaded.</summary>
    internal FieldCommissionConfig GetConfig(FieldCommissionMcmSnapshot snapshot)
    {
        var json = _jsonProvider?.GetConfig();

        // Rebuild on a changed snapshot OR a changed JSON instance. The second term matters because
        // the decorated provider is Lazy: the very first GetConfig() here can run before the file has
        // been read, and a config swapped underneath us must not be masked by a stale cache.
        if (_merged == null || !snapshot.Equals(_mergedFrom) || !ReferenceEquals(json, _mergedJsonSource))
        {
            _merged = Merge(json, snapshot);
            _mergedFrom = snapshot;
            _mergedJsonSource = json;
        }

        return _merged;
    }

    /// <summary>
    /// Pure merge: MCM value where the player set one, JSON value otherwise, clamped either way.
    /// Every clamp is a positive-requirement gate — a non-finite ratio reverts to the JSON value
    /// rather than propagating NaN into <c>IsBattleEligible</c>, per the engine-float rule.
    /// </summary>
    internal static FieldCommissionConfig Merge(FieldCommissionConfig json, FieldCommissionMcmSnapshot mcm)
    {
        var source = json ?? new FieldCommissionConfig();

        // Each surface validates only its OWN input. The slider bounds below exist to contain a
        // hand-edited MCM json; running the JSON value through them too would narrow a config the
        // JSON provider already validated on its own, stricter terms — `ratioThreshold: 0` is
        // explicitly legal there ("never eligible") and would come back out of a 0.1-floored clamp
        // as 0.1, quietly re-enabling the thing the pack author turned off. Same shape as the
        // JSON-vs-MCM invariant split that shipped in CombatMechanics.
        return new FieldCommissionConfig
        {
            Enabled = mcm.Enabled ?? source.Enabled,
            RatioThreshold = mcm.RatioThreshold.HasValue
                ? SettingClamp.Clamp(mcm.RatioThreshold, source.RatioThreshold, MinRatioThreshold, MaxRatioThreshold)
                : source.RatioThreshold,
            MeritPerKill = mcm.MeritPerKill.HasValue
                ? SettingClamp.Clamp(mcm.MeritPerKill, source.MeritPerKill, MinMeritPerKill, MaxMeritPerKill)
                : source.MeritPerKill,
            MeritThreshold = mcm.MeritThreshold.HasValue
                ? SettingClamp.Clamp(mcm.MeritThreshold, source.MeritThreshold, MinMeritThreshold, MaxMeritThreshold)
                : source.MeritThreshold,
            RetainerAllowance = mcm.RetainerAllowance.HasValue
                ? SettingClamp.Clamp(mcm.RetainerAllowance, source.RetainerAllowance, MinRetainerAllowance, MaxRetainerAllowance)
                : source.RetainerAllowance,
            MaxOffersPerBattle = mcm.MaxOffersPerBattle.HasValue
                ? SettingClamp.Clamp(mcm.MaxOffersPerBattle, source.MaxOffersPerBattle, MinMaxOffersPerBattle, MaxMaxOffersPerBattle)
                : source.MaxOffersPerBattle,

            Diagnostics = mcm.Diagnostics ?? source.Diagnostics,

            // JSON-only (advanced). Carried through by reference — a merge that rebuilt these from
            // compiled defaults would silently re-admit races the pack author excluded.
            SkillPointsPerLevel = source.SkillPointsPerLevel,
            AllowedRaceNames = source.AllowedRaceNames ?? new List<string>(),
        };
    }

    /// <summary>
    /// Reads the MCM statics. Separated from <see cref="Merge"/> so nothing above this line needs MCM
    /// loaded; <c>TaomSettings.Instance</c> is null in the test host and whenever MCM fails to load.
    /// </summary>
    private static FieldCommissionMcmSnapshot Capture() => new FieldCommissionMcmSnapshot(
        TaomSettings.Instance?.EnableFieldCommission,
        TaomSettings.Instance?.FieldCommissionRatioThreshold,
        TaomSettings.Instance?.FieldCommissionMeritPerKill,
        TaomSettings.Instance?.FieldCommissionMeritThreshold,
        TaomSettings.Instance?.FieldCommissionRetainerAllowance,
        TaomSettings.Instance?.FieldCommissionMaxOffersPerBattle,
        TaomSettings.Instance?.EnableFieldCommissionDiagnostics);
}
