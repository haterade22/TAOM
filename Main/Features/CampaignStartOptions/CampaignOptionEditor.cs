using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using IPartySizeCacheInvalidator = TAOM.Adapters.IPartySizeCacheInvalidator;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.CampaignStartOptions;

/// <summary>
/// Shows and changes the start choices the current campaign pinned (the <c>taom.campaign_option</c> console command).
/// Every value is validated before it is written into the campaign's own option store; a bad value changes nothing.
/// A campaign that never pinned (an older save) has nothing to change: it follows MCM. A co-op client refuses to write
/// (the pinned values are save-backed and the host owns them). A write that sticks also drops the cached party size
/// limits, so Troop Weight and Large AI armies apply at once, as the MCM path does.
/// </summary>
public sealed class CampaignOptionEditor
{
    private enum Kind { Flag, Day1, Day2, Alignment }

    private sealed class Entry
    {
        public Entry(string name, Kind kind, string key, Func<ICampaignStartOptions, string?> pinned, Func<StartDefaults, string?> mcm)
        {
            Name = name;
            Kind = kind;
            Key = key;
            Pinned = pinned;
            Mcm = mcm;
        }

        public string Name { get; }
        public Kind Kind { get; }
        public string Key { get; }
        public Func<ICampaignStartOptions, string?> Pinned { get; }
        public Func<StartDefaults, string?> Mcm { get; }
    }

    private static string? Show(bool? value) => value.HasValue ? (value.Value ? "on" : "off") : null;

    private static readonly Entry[] Entries =
    {
        new Entry("war_of_the_ring", Kind.Flag, CampaignStartOptionKeys.WarOfTheRingKey, c => Show(c.WarOfTheRing), d => Show(d.WarOfTheRing)),
        new Entry("war_day1", Kind.Day1, CampaignStartOptionKeys.Day1Key, c => c.WarPhaseDays?.Phase1Day.ToString(CultureInfo.InvariantCulture), d => d.Phase1Day.ToString(CultureInfo.InvariantCulture)),
        new Entry("war_day2", Kind.Day2, CampaignStartOptionKeys.Day2Key, c => c.WarPhaseDays?.Phase2Day.ToString(CultureInfo.InvariantCulture), d => d.Phase2Day.ToString(CultureInfo.InvariantCulture)),
        new Entry("troop_weight", Kind.Flag, CampaignStartOptionKeys.TroopWeightKey, c => Show(c.TroopWeight), d => Show(d.TroopWeight)),
        new Entry("war_can_be_won", Kind.Flag, CampaignStartOptionKeys.VictoryKey, c => Show(c.MomentumVictory), d => Show(d.MomentumVictory)),
        new Entry("eye_hunters", Kind.Flag, CampaignStartOptionKeys.EyeOfSauronKey, c => Show(c.EyeOfSauron), d => Show(d.EyeOfSauron)),
        new Entry("large_ai_armies", Kind.Flag, CampaignStartOptionKeys.AiPartyScalingKey, c => Show(c.AiPartyScaling), d => Show(d.AiPartyScaling)),
        new Entry("alignment", Kind.Alignment, CampaignStartOptionKeys.AlignmentKey, c => PresetName(c.AlignmentPreset), d => Describe(d.Alignment)),
    };

    private static readonly (string Name, string Id)[] Presets =
    {
        ("standard", CampaignStartOptionKeys.AlignmentStandardItem),
        ("strict", CampaignStartOptionKeys.AlignmentStrictItem),
        ("relaxed", CampaignStartOptionKeys.AlignmentRelaxedItem),
    };

    private readonly CampaignStartOptionsStore _store;
    private readonly ICampaignStartDefaults _defaults;
    private readonly IModLogger _logger;
    private readonly ICoopSessionProvider _coop;
    private readonly IPartySizeCacheInvalidator _partySizeCache;

    public CampaignOptionEditor(
        CampaignStartOptionsStore store, ICampaignStartDefaults defaults, IModLogger logger,
        ICoopSessionProvider coop, IPartySizeCacheInvalidator partySizeCache)
    {
        _store = store;
        _defaults = defaults;
        _logger = logger;
        _coop = coop;
        _partySizeCache = partySizeCache;
    }

    public static string Names => string.Join(", ", Entries.Select(e => e.Name));

    private const string NotPinned =
        "This campaign pinned no start choices (an older save, or it was started some other way), so it follows the "
        + "TAOM mod options (MCM) as before. Nothing to change here.";

    public string List()
    {
        if (!_store.IsPinned) return NotPinned;

        var current = _defaults.Current();
        var sb = new StringBuilder("This campaign's start choices (the MCM value now is in brackets):");
        foreach (var entry in Entries)
        {
            var pinned = entry.Pinned(_store);
            sb.Append('\n').Append(entry.Name).Append(" = ").Append(pinned ?? "not pinned (follows MCM)");
            sb.Append("   [MCM: ").Append(entry.Mcm(current)).Append(']');
        }
        if (CampaignStartMismatch.Differs(_store, current))
            sb.Append("\nAt least one value differs from MCM: the campaign keeps its own.");
        return sb.ToString();
    }

    public string Set(string name, string value)
    {
        if (!_store.IsPinned) return NotPinned;

        if (!CoopSessionPolicy.MayWriteSaveBackedState(_coop.IsCoopClient))
            return "Refused: a co-op client does not change the host's campaign start choices.";

        var entry = Entries.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
        if (entry == null) return $"Unknown option '{name}'. Options: {Names}.";

        bool written;
        string shown;
        switch (entry.Kind)
        {
            case Kind.Flag:
                if (!TryParseFlag(value, out var flag)) return $"'{value}' is not on or off. Use on, off, true, false, 1 or 0.";
                written = _store.TrySet(entry.Key, flag);
                shown = flag ? "on" : "off";
                break;

            case Kind.Alignment:
                var preset = Presets.FirstOrDefault(p => string.Equals(p.Name, value, StringComparison.OrdinalIgnoreCase));
                if (preset.Name == null) return $"'{value}' is not an alignment preset. Use {string.Join(", ", Presets.Select(p => p.Name))}.";
                written = _store.TrySet(entry.Key, preset.Id);
                shown = preset.Name;
                break;

            default:
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var day))
                    return $"'{value}' is not a whole number of days.";
                var stored = _store.WarPhaseDays;
                var (d1, d2) = stored ?? CurrentDays();
                var (new1, new2) = entry.Kind == Kind.Day1 ? (day, d2) : (d1, day);
                if (!CampaignStartOptionsResolver.IsValidPair(new1, new2))
                    return $"Days must be between {CampaignStartOptionKeys.MinDay} and {CampaignStartOptionKeys.MaxDay}, "
                        + $"and war_day2 must come after war_day1 (this would give {new1} and {new2}). Change the other day first.";
                written = _store.TrySet(CampaignStartOptionKeys.Day1Key, new1) && _store.TrySet(CampaignStartOptionKeys.Day2Key, new2);
                shown = day.ToString(CultureInfo.InvariantCulture);
                break;
        }

        if (!written) return $"Could not write {entry.Name} into this campaign.";
        _partySizeCache.InvalidateAll();
        _logger.LogInfo($"CampaignStartOptions.Changed: {entry.Name}={shown} (console)");
        return $"{entry.Name} is now {shown} for this campaign. Save the game to keep it.";
    }

    private (int, int) CurrentDays()
    {
        var current = _defaults.Current();
        return (current.Phase1Day, current.Phase2Day);
    }

    private static bool TryParseFlag(string value, out bool flag)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "on": case "true": case "1": case "yes": flag = true; return true;
            case "off": case "false": case "0": case "no": flag = false; return true;
            default: flag = false; return false;
        }
    }

    private static string? PresetName(string? id) =>
        Presets.FirstOrDefault(p => p.Id == id).Name;

    private static string Describe(AlignmentRules rules) =>
        $"recruitment {Show(rules.RecruitmentBlock)}, marriage {Show(rules.MarriageBlock)}, desertion {Show(rules.Desertion)}, alliances with anyone {Show(rules.AllianceWithAnyone)}";
}
