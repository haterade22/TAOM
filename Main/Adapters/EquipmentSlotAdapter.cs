using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;
using TAOM.Core.Logging;
using TAOM.Features.EquipPresets.Models;

namespace TAOM.Adapters;

/// <summary>
/// Concrete <see cref="IEquipmentSlotAdapter"/>. Read-only after the Codex review #2026-05-07
/// rewrite — the previous direct-mutation <c>ApplySlot</c> path was replaced by
/// <see cref="IInventoryScreenAdapter.LoadEquipment"/>'s <c>TransferCommand</c> flow.
///
/// <para><b>Dead-equipment guard (M6 fix):</b> <c>Hero.BattleEquipment</c> /
/// <c>CivilianEquipment</c> fall back to <c>Campaign.Current.DeadBattleEquipment</c> /
/// <c>DeadCivilianEquipment</c> when the backing field is null. Capture refuses to read from
/// the singleton and returns an empty list instead — otherwise a captured "preset" would
/// reflect dead-character defaults rather than the live hero's real loadout.</para>
/// </summary>
public sealed class EquipmentSlotAdapter : IEquipmentSlotAdapter
{
    private static readonly EquippedSlotSnapshot[] Empty = System.Array.Empty<EquippedSlotSnapshot>();

    private readonly IModLogger _logger;

    public EquipmentSlotAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public bool HeroExists(string heroStringId)
    {
        if (string.IsNullOrEmpty(heroStringId)) return false;
        var hero = MBObjectManager.Instance?.GetObject<Hero>(heroStringId);
        return hero != null && hero.IsActive;
    }

    public IReadOnlyList<EquippedSlotSnapshot> Capture(string heroStringId, bool civilian)
    {
        if (string.IsNullOrEmpty(heroStringId)) return Empty;
        var hero = MBObjectManager.Instance?.GetObject<Hero>(heroStringId);
        if (hero == null) return Empty;

        var equipment = civilian ? hero.CivilianEquipment : hero.BattleEquipment;
        if (equipment == null) return Empty;

        // M6 dead-equipment guard: refuse to capture from the shared fallback.
        var campaign = Campaign.Current;
        if (campaign != null)
        {
            var deadEquip = civilian ? campaign.DeadCivilianEquipment : campaign.DeadBattleEquipment;
            if (ReferenceEquals(equipment, deadEquip))
            {
                _logger.LogWarning($"[EquipPresets] Capture skipped for hero {heroStringId} — backing equipment is the shared dead-equipment singleton");
                return Empty;
            }
        }

        // Emit one snapshot per slot (12 total). Empty slots get empty ItemStringId so Load can
        // clear them — H4 fix (Codex review 2026-05-07): capture must include empty slots so a
        // "no shield" preset actually unequips the shield on load.
        var result = new List<EquippedSlotSnapshot>(12);
        for (int i = 0; i < 12; i++)
        {
            try
            {
                var element = equipment[i];
                if (element.IsEmpty)
                {
                    result.Add(new EquippedSlotSnapshot(i, string.Empty, string.Empty));
                    continue;
                }
                var itemId = element.Item?.StringId ?? string.Empty;
                var modifierId = element.ItemModifier?.StringId ?? string.Empty;
                result.Add(new EquippedSlotSnapshot(i, itemId, modifierId));
            }
            catch (Exception ex)
            {
                _logger.LogError($"[EquipPresets] Capture slot {i} failed for hero {heroStringId}: {ex.Message}");
                result.Add(new EquippedSlotSnapshot(i, string.Empty, string.Empty));
            }
        }
        return result;
    }
}
