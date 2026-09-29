using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.ArmourAcquisition.Domain;

namespace TAOM.Features.ArmourAcquisition;

/// <summary>
/// Owns the current game's armour classes (docs/features/armour-acquisition.md). SubModule calls
/// <see cref="ApplyGating"/> from OnGameInitializationFinished on every game init, beside MonsterSize:
/// that hook runs after the items load (Campaign.InitializeDefaultCampaignObjects, LoadXML("Items")) and
/// before a new game's workshops fill their item cache (WorkshopsCampaignBehavior.FillItemsInAllCategories,
/// from OnNewGameCreatedPartialFollowUp), so a flipped piece never enters a workshop's production. Every
/// call rebuilds the state from the new game's items: nothing here outlives a game, and nothing is saved.
/// </summary>
public sealed class ArmourGateService : IArmourGateService
{
    private const string Tag = "[ArmourAcquisition]";

    private static readonly IReadOnlyDictionary<string, ArmourClassEntry> NoEntries = new Dictionary<string, ArmourClassEntry>();

    private readonly IArmourItemCatalogAdapter _catalog;
    private readonly IArmourClassTableProvider _table;
    private readonly IArmourAcquisitionConfigProvider _config;
    private readonly IArmourAcquisitionSettingsProvider _settings;
    private readonly IModLogger _logger;

    private IReadOnlyDictionary<string, ArmourClass> _classById = new Dictionary<string, ArmourClass>();
    private Dictionary<string, ArmourItemRecord> _records = new(StringComparer.Ordinal);
    private IReadOnlyDictionary<string, ArmourClassEntry> _entries = NoEntries;

    public ArmourGateService(
        IArmourItemCatalogAdapter catalog,
        IArmourClassTableProvider table,
        IArmourAcquisitionConfigProvider config,
        IArmourAcquisitionSettingsProvider settings,
        IModLogger logger)
    {
        _catalog = catalog;
        _table = table;
        _config = config;
        _settings = settings;
        _logger = logger;
    }

    public bool IsActive { get; private set; }

    public void ApplyGating(bool isCampaign)
    {
        Clear();
        // A custom battle, the editor or a shader-precompile game has no market, workshop or loot that reads
        // the flag: its items keep their XML values, and the next campaign init applies the gate afresh.
        if (!isCampaign)
            return;

        var flipped = new List<string>();
        try
        {
            var items = _catalog.ReadItems();
            foreach (var item in items)
                if (!string.IsNullOrEmpty(item.ItemId))
                    _records[item.ItemId] = item;
            _entries = _table.GetEntries();

            var enabled = _settings.IsEnabled;
            var canWrite = _catalog.CanWriteMerchandise;
            if (enabled && !canWrite)
                _logger.LogError($"{Tag} ItemObject.NotMerchandise's setter did not resolve (an engine update?); "
                                 + "gating is OFF for this game and markets and loot behave as before.");

            var plan = ArmourGatePlanner.Plan(items, _entries, _config.GetConfig().NamedWeapons, enabled && canWrite);
            _classById = plan.ClassById;

            foreach (var id in plan.ToFlip)
                if (_catalog.SetNotMerchandise(id, true))
                    flipped.Add(id);
            IsActive = enabled && canWrite;

            Report(plan, flipped.Count);
        }
        catch (Exception ex)
        {
            // Half a gate is worse than none: pieces out of loot with markets ungated. Undo what was flipped.
            var restored = 0;
            foreach (var id in flipped)
            {
                try
                {
                    if (_catalog.SetNotMerchandise(id, false))
                        restored++;
                }
                catch (Exception)
                {
                    // Counted: the log line below names how many stayed flipped.
                }
            }
            Clear();
            _logger.LogError($"{Tag} Gating failed at game init ({ex.GetType().Name}: {ex.Message}); "
                             + $"{restored} of {flipped.Count} flipped piece(s) restored, and markets and loot behave as before for this game.");
        }
    }

    public ArmourClass? GetClass(string itemId) =>
        itemId != null && _classById.TryGetValue(itemId, out var cls) ? cls : null;

    public string? GetNext(string itemId)
    {
        if (itemId == null || !_entries.TryGetValue(itemId, out var entry) || entry.NextItemId == null)
            return null;
        return _records.ContainsKey(entry.NextItemId) ? entry.NextItemId : null;
    }

    public ArmourItemRecord? GetRecord(string itemId) =>
        itemId != null && _records.TryGetValue(itemId, out var record) ? record : null;

    public string GetName(string itemId) => _catalog.GetName(itemId);

    public IReadOnlyList<string> GetPieces(ArmourClass cls, string? cultureId, ArmourSlot? slot = null) =>
        _classById
            .Where(kv => kv.Value == cls
                         && _entries.ContainsKey(kv.Key)
                         && (cultureId == null
                             || string.Equals(GetRecord(kv.Key)?.CultureId, cultureId, StringComparison.OrdinalIgnoreCase))
                         && (slot == null || GetRecord(kv.Key)?.Slot == slot))
            .Select(kv => kv.Key)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

    public bool IsEligibleForMarket(string itemId, int townLevel)
    {
        if (!IsActive || itemId == null || !_records.TryGetValue(itemId, out var record))
            return true;
        if (!record.IsMerchandise)
            return false;
        if (!_classById.TryGetValue(itemId, out var cls))
            return true;
        // Heavy, elite and lord pieces each need their armoury level (Mike, 2026-09-27: heavy 1, elite 2, lord
        // and all 3); light and medium need none; a named piece is never sold.
        return cls != ArmourClass.Named && townLevel >= _config.GetConfig().RequiredLevel(cls);
    }

    private void Clear()
    {
        IsActive = false;
        _classById = new Dictionary<string, ArmourClass>();
        _records = new Dictionary<string, ArmourItemRecord>(StringComparer.Ordinal);
        _entries = NoEntries;
    }

    private void Report(ArmourGatePlan plan, int flipped)
    {
        if (plan.StaleTableIds.Count > 0)
            _logger.LogWarning($"{Tag} {plan.StaleTableIds.Count} class-table row(s) name no loaded item "
                               + $"({string.Join(", ", plan.StaleTableIds.Take(8))}); regenerate: python tools/generate_armour_classes.py --apply");
        if (plan.MissingNamedWeapons.Count > 0)
            _logger.LogWarning($"{Tag} named weapon(s) not loaded: {string.Join(", ", plan.MissingNamedWeapons)}");

        var counts = plan.ClassById.Values.GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());
        string Count(ArmourClass c) => $"{ArmourClassRules.ToId(c)} {(counts.TryGetValue(c, out var n) ? n : 0)}";
        var state = IsActive ? $"ON, {flipped} of {plan.ToFlip.Count} gated piece(s) taken out of markets and loot" : "OFF";
        _logger.LogInfo($"{Tag} Gating {state}. {plan.ClassById.Count} piece(s) classed ({plan.FromTable} from the table, "
                        + $"{plan.FromEngineTier} by engine tier): {Count(ArmourClass.Light)}, {Count(ArmourClass.Medium)}, "
                        + $"{Count(ArmourClass.Heavy)}, {Count(ArmourClass.Elite)}, {Count(ArmourClass.Lord)}, "
                        + $"{Count(ArmourClass.Civilian)}, {Count(ArmourClass.Named)}.");
    }
}
