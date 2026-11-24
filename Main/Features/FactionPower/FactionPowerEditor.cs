using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.FactionPower;

/// <summary>
/// Shows and changes the faction power values of the current campaign (the <c>taom.faction_power</c> console command). A value
/// is checked before it is written; a bad one changes nothing. Every peer of a co-op session refuses to write: a client does not
/// own the save, and Coop does not send a host's change to the clients. A write that sticks also drops the cached party size
/// limits, so the party and garrison limits move at once.
/// </summary>
public sealed class FactionPowerEditor
{
    private readonly IFactionPowerSettings _settings;
    private readonly IFactionPowerCatalog _catalog;
    private readonly IModLogger _logger;
    private readonly ICoopSessionProvider _coop;
    private readonly IPartySizeCacheInvalidator _partySizeCache;

    public FactionPowerEditor(
        IFactionPowerSettings settings, IFactionPowerCatalog catalog, IModLogger logger,
        ICoopSessionProvider coop, IPartySizeCacheInvalidator partySizeCache)
    {
        _settings = settings;
        _catalog = catalog;
        _logger = logger;
        _coop = coop;
        _partySizeCache = partySizeCache;
    }

    public string List()
    {
        var entries = _catalog.Entries;
        if (entries.Count == 0) return "No realm cultures were found in factions.json.";

        var sb = new StringBuilder("Faction power per realm culture (percent, 100 is normal):");
        foreach (var entry in entries)
            sb.Append('\n').Append(entry.CultureId).Append(" [").Append(entry.Side).Append("] = ").Append(_settings.Percent(entry.CultureId));
        return sb.ToString();
    }

    public string Set(string culture, string percentText)
    {
        var refusal = Refusal();
        if (refusal != null) return refusal;

        var entry = _catalog.Entries.FirstOrDefault(e => string.Equals(e.CultureId, culture?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (entry == null) return $"Unknown culture '{culture}'. Use list to see them.";
        if (!int.TryParse(percentText?.Trim(), NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var percent) || !FactionPowerMath.IsValidPercent(percent))
            return $"'{percentText}' is not a whole percent between {FactionPowerMath.MinPercent} and {FactionPowerMath.MaxPercent}.";

        if (!_settings.TrySet(entry.CultureId, percent)) return $"Could not write {entry.CultureId} into this campaign.";
        _partySizeCache.InvalidateAll();
        _logger.LogInfo($"FactionPower.Changed: {entry.CultureId}={percent} (console)");
        return $"{entry.CultureId} is now {percent} percent for this campaign. Save the game to keep it.";
    }

    public string Preset(string presetId)
    {
        var refusal = Refusal();
        if (refusal != null) return refusal;

        var values = FactionPowerPresets.Resolve(presetId, _catalog.Entries, _settings.Snapshot().Keys.ToList());
        if (values == null) return $"Unknown preset '{presetId}'. Presets: {string.Join(", ", FactionPowerPresets.Ids)}.";
        if (values.Count == 0) return "Nothing to change.";

        foreach (var pair in values)
            if (!_settings.TrySet(pair.Key, pair.Value)) return $"Could not write {pair.Key} into this campaign.";
        _partySizeCache.InvalidateAll();
        _logger.LogInfo($"FactionPower.Changed: preset={presetId.Trim().ToLowerInvariant()} ({values.Count} realms, console)");
        return $"Preset {presetId.Trim().ToLowerInvariant()} applied to {values.Count} realms. Save the game to keep it.";
    }

    private string? Refusal()
    {
        if (!CoopSessionPolicy.MayWriteSaveBackedState(_coop.IsCoopClient))
            return "Refused: a co-op client does not change the host's faction power.";
        if (_coop.IsSessionActive)
            return "Refused: during a co-op session the clients would keep the old values. Change them in single player.";
        return null;
    }
}
