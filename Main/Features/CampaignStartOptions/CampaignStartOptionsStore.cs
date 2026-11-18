using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.CampaignStartOptions;

/// <summary>
/// Reads the options a campaign pinned at its start (saved with the campaign by the engine, no TAOM save
/// definer) for the features that follow them. No marker key means "follow MCM, then JSON", exactly as
/// before, which is what every older save and every campaign started some other way gets. The snapshot
/// holds values only, never an engine object. <see cref="CampaignStartSession"/> writes the pin.
/// </summary>
public sealed class CampaignStartOptionsStore : ICampaignStartOptions
{
    private static readonly Snapshot Empty = new Snapshot(false, null, null, null, null, null, null, null, null);

    private readonly IAdvancedStartDataAdapter _data;
    private readonly IModLogger _logger;

    // Immutable, replaced whole: a reader on the AI worker thread sees the old or the new, never a mix.
    private volatile Snapshot? _snapshot;

    public CampaignStartOptionsStore(IAdvancedStartDataAdapter data, IModLogger logger)
    {
        _data = data;
        _logger = logger;
    }

    public bool IsPinned => Current.IsPinned;
    public bool? WarOfTheRing => Current.WarOfTheRing;
    public (int Phase1Day, int Phase2Day)? WarPhaseDays => Current.WarPhaseDays;
    public bool? TroopWeight => Current.TroopWeight;
    public bool? MomentumVictory => Current.MomentumVictory;
    public bool? EyeOfSauron => Current.EyeOfSauron;
    public bool? AiPartyScaling => Current.AiPartyScaling;
    public string? AlignmentPreset => Current.AlignmentPreset;
    public AlignmentRules? AlignmentRules => Current.Rules;

    /// <summary>
    /// Writes one pinned key into the campaign (the console command), then takes a new snapshot. A key the
    /// pin has not written yet (a campaign pinned by an older build) is added. A campaign that never pinned
    /// stays unpinned. False when nothing changed.
    /// </summary>
    public bool TrySet<T>(string key, T value)
    {
        if (!IsPinned) return false;
        if (!_data.Set(key, value) && !_data.Add(key, CampaignStartOptionKeys.PinnedCategory, value)) return false;
        Reload();
        return true;
    }

    /// <summary>Forgets the snapshot and takes a new one from the campaign now in place (if any).</summary>
    public void Reload()
    {
        _snapshot = null;
        _ = Current;
    }

    private Snapshot Current
    {
        get
        {
            var snapshot = _snapshot;
            if (snapshot != null) return snapshot;

            // No campaign yet: answer "nothing pinned" without remembering it, or a read before the
            // campaign exists would hide the values it pins a moment later.
            if (!_data.IsAvailable) return Empty;
            return _snapshot = Build();
        }
    }

    // A save is untrusted input: every value is checked, and a bad one falls back to "not pinned" for
    // that setting (MCM, then JSON) with one warning.
    private Snapshot Build()
    {
        if (!_data.TryGet<int>(CampaignStartOptionKeys.VersionKey, out var version))
            return Empty;

        if (version > CampaignStartOptionKeys.CurrentVersion)
            _logger.LogWarning(
                $"CampaignStartOptions: this campaign was pinned by a newer TAOM (version {version}, this build reads {CampaignStartOptionKeys.CurrentVersion}); reading the keys it knows");

        bool? wotr = _data.TryGet<bool>(CampaignStartOptionKeys.WarOfTheRingKey, out var w) ? w : (bool?)null;
        bool? weight = _data.TryGet<bool>(CampaignStartOptionKeys.TroopWeightKey, out var t) ? t : (bool?)null;

        bool? victory = _data.TryGet<bool>(CampaignStartOptionKeys.VictoryKey, out var v) ? v : (bool?)null;
        bool? eye = _data.TryGet<bool>(CampaignStartOptionKeys.EyeOfSauronKey, out var e) ? e : (bool?)null;
        bool? scaling = _data.TryGet<bool>(CampaignStartOptionKeys.AiPartyScalingKey, out var s) ? s : (bool?)null;

        string? preset = null;
        if (_data.TryGet<string>(CampaignStartOptionKeys.AlignmentKey, out var a))
        {
            if (AlignmentRules.IsKnownPreset(a))
                preset = a;
            else
                _logger.LogWarning($"CampaignStartOptions: pinned alignment preset '{a}' is unknown; using the MCM or JSON alignment rules");
        }

        (int, int)? days = null;
        if (_data.TryGet<int>(CampaignStartOptionKeys.Day1Key, out var d1) && _data.TryGet<int>(CampaignStartOptionKeys.Day2Key, out var d2))
        {
            if (CampaignStartOptionsResolver.IsValidPair(d1, d2))
                days = (d1, d2);
            else
                _logger.LogWarning($"CampaignStartOptions: pinned War of the Ring days {d1}/{d2} are outside 1 to 365 or not in order; using the MCM or JSON days");
        }

        return new Snapshot(true, wotr, days, weight, victory, eye, scaling, preset, AlignmentRules.ForPreset(preset));
    }

    private sealed class Snapshot
    {
        public Snapshot(
            bool isPinned, bool? warOfTheRing, (int, int)? warPhaseDays, bool? troopWeight,
            bool? momentumVictory, bool? eyeOfSauron, bool? aiPartyScaling, string? alignmentPreset,
            AlignmentRules? rules)
        {
            Rules = rules;
            MomentumVictory = momentumVictory;
            EyeOfSauron = eyeOfSauron;
            AiPartyScaling = aiPartyScaling;
            AlignmentPreset = alignmentPreset;
            IsPinned = isPinned;
            WarOfTheRing = warOfTheRing;
            WarPhaseDays = warPhaseDays;
            TroopWeight = troopWeight;
        }

        public bool IsPinned { get; }
        public bool? WarOfTheRing { get; }
        public (int Phase1Day, int Phase2Day)? WarPhaseDays { get; }
        public bool? TroopWeight { get; }
        public bool? MomentumVictory { get; }
        public bool? EyeOfSauron { get; }
        public bool? AiPartyScaling { get; }
        public string? AlignmentPreset { get; }
        public AlignmentRules? Rules { get; }
    }
}
