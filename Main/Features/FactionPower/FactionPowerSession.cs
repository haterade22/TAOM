using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.FactionPower;

/// <summary>
/// Runs at <c>CampaignStartSession.Begin</c>. A new campaign pins the start screen's choice (its preset and moved sliders, which the
/// engine wrote under the <see cref="FactionPowerScreenKeys"/> keys) into the stored percents, then every campaign reloads the
/// store. A loaded save is never pinned. Standard, the default, writes nothing, so a campaign started without the screen stays at 100.
/// </summary>
public sealed class FactionPowerSession : IFactionPowerSession
{
    private readonly IAdvancedStartDataAdapter _data;
    private readonly IFactionPowerCatalog _catalog;
    private readonly IFactionPowerSettings _settings;
    private readonly IModLogger _logger;

    public FactionPowerSession(
        IAdvancedStartDataAdapter data, IFactionPowerCatalog catalog, IFactionPowerSettings settings, IModLogger logger)
    {
        _data = data;
        _catalog = catalog;
        _settings = settings;
        _logger = logger;
    }

    public void Begin(bool isNewCampaign)
    {
        if (isNewCampaign && _data.IsAvailable) Pin();
        _settings.Reload();
    }

    private void Pin()
    {
        var entries = _catalog.Entries;
        _data.TryGet<string>(FactionPowerScreenKeys.PresetOption, out var preset);

        var sliders = new Dictionary<string, int>();
        foreach (var entry in entries)
            if (_data.TryGet<int>(FactionPowerScreenKeys.SliderKey(entry.CultureId), out var percent))
                sliders[entry.CultureId] = percent;

        var values = FactionPowerStartResolver.Resolve(preset, sliders, entries, _logger.LogWarning);
        if (values.Count == 0) return;

        var failed = values.Where(pair => !_settings.TrySet(pair.Key, pair.Value)).Select(pair => pair.Key).ToList();
        var table = string.Join(" ", values.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key}={p.Value}"));
        if (failed.Count == 0)
            _logger.LogInfo($"FactionPower.Pinned: preset={preset} {table}");
        else
            _logger.LogWarning($"FactionPower.PinFailed: preset={preset} could not write {string.Join(",", failed)} ({table})");
    }
}
