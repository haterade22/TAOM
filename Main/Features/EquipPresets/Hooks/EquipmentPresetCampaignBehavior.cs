using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;
using TAOM.Features.EquipPresets.Models;

namespace TAOM.Features.EquipPresets.Hooks;

/// <summary>
/// Per-save persistence for hero presets. SyncData round-trips the service's dictionary on
/// SaveBaseId 726900501 (registered by <see cref="PresetSaveableTypeDefiner"/>). On load, prunes
/// orphaned entries (heroes that no longer exist in the save).
///
/// Registered <c>Reuse.Singleton</c>: the same instance survives across campaigns within one
/// Bannerlord process — <see cref="OnNewGameCreated"/> resets cross-session state defensively.
/// </summary>
public sealed class EquipmentPresetCampaignBehavior : CampaignBehaviorBase
{
    private readonly IEquipmentPresetService _service;
    private readonly IModLogger _logger;

    public EquipmentPresetCampaignBehavior(IEquipmentPresetService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
        CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
    }

    public override void SyncData(IDataStore dataStore)
    {
        if (dataStore.IsSaving)
        {
            var snapshot = _service.SerializableState;
            dataStore.SyncData("EquipPresets_HeroPresets", ref snapshot);
        }
        else
        {
            Dictionary<string, List<HoNEquipmentPreset>>? snapshot = null;
            dataStore.SyncData("EquipPresets_HeroPresets", ref snapshot);
            _service.RestoreFromSerializableState(snapshot);
        }
    }

    private void OnNewGameCreated(CampaignGameStarter starter)
    {
        _service.RestoreFromSerializableState(null);
    }

    private void OnGameLoaded(CampaignGameStarter starter)
    {
        // Build live-id set per Entity State Matrix (csharp-architecture.md):
        //   - Alive + active           → keep
        //   - Companion in player party → keep (alive)
        //   - Captured / fugitive       → keep (state may resolve)
        //   - Dead                      → exclude (Hero.IsDead)
        //   - Disabled                  → exclude (Hero.IsDisabled)
        // Hero.IsActive captures (alive AND not disabled), so it's the right gate here.
        var live = new HashSet<string>(System.StringComparer.Ordinal);
        var heroes = Hero.AllAliveHeroes;
        if (heroes != null)
        {
            foreach (var h in heroes)
            {
                if (h?.StringId != null) live.Add(h.StringId);
            }
        }
        // Defensive: if AllAliveHeroes is empty (transient load state), don't prune anything.
        // PruneOrphanedEntries treats empty live-set as "don't know" and returns 0.
        var pruned = _service.PruneOrphanedEntries(live);
        if (pruned > 0)
            _logger.LogInfo($"[EquipPresets] OnGameLoaded pruned {pruned} orphaned preset bundle(s)");
    }
}
