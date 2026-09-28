using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using TAOM.Core.Logging;
using TAOM.Features.ArmourAcquisition.Domain;

namespace TAOM.Adapters;

/// <summary>
/// Boundary implementation of <see cref="IArmourItemCatalogAdapter"/> (docs/features/armour-acquisition.md).
/// </summary>
public class ArmourItemCatalogAdapter : IArmourItemCatalogAdapter
{
    // ItemObject.NotMerchandise has a private setter (v1.5.3 ItemObject.cs:154), set only from the XML
    // is_merchandise attribute (:431, :501). Cached once, never per call; ReflectionSiteBindingTests pins it.
    private static readonly MethodInfo? NotMerchandiseSetter =
        AccessTools.PropertySetter(typeof(ItemObject), nameof(ItemObject.NotMerchandise));

    private readonly IModLogger _logger;

    public ArmourItemCatalogAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public bool CanWriteMerchandise => NotMerchandiseSetter != null;

    public IReadOnlyList<ArmourItemRecord> ReadItems()
    {
        var all = MBObjectManager.Instance?.GetObjectTypeList<ItemObject>();
        if (all == null)
            return Array.Empty<ArmourItemRecord>();

        var records = new List<ArmourItemRecord>(all.Count);
        var unreadable = 0;
        foreach (var item in all)
        {
            if (item == null || string.IsNullOrEmpty(item.StringId))
                continue;
            try
            {
                var isArmour = IsCharacterArmour(item.ItemType);
                // ItemObject.Tier asks the item value model (Tierf), and it can be -1 for a Tierf under 0.5
                // ((ItemTiers)(Round(Tierf) - 1), ItemObject.cs:178); only armour needs it, for the fallback class.
                var tier = isArmour ? (int)item.Tier : 0;
                records.Add(new ArmourItemRecord(item.StringId, isArmour, tier, !item.NotMerchandise,
                    item.Culture?.StringId, item.Value));
            }
            catch (Exception ex)
            {
                unreadable++;
                if (unreadable <= 3)
                    _logger.LogWarning($"[ArmourAcquisition] Item '{item.StringId}' could not be read ({ex.GetType().Name}: {ex.Message}); it is left ungoverned.");
            }
        }
        if (unreadable > 3)
            _logger.LogWarning($"[ArmourAcquisition] {unreadable} item(s) could not be read in all; they are left ungoverned.");
        return records;
    }

    public bool SetNotMerchandise(string itemId, bool notMerchandise)
    {
        if (NotMerchandiseSetter == null || string.IsNullOrEmpty(itemId))
            return false;
        var item = MBObjectManager.Instance?.GetObject<ItemObject>(itemId);
        if (item == null)
            return false;
        NotMerchandiseSetter.Invoke(item, new object[] { notMerchandise });
        return true;
    }

    public string GetName(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return string.Empty;
        return MBObjectManager.Instance?.GetObject<ItemObject>(itemId)?.Name?.ToString() ?? itemId;
    }

    private static bool IsCharacterArmour(ItemObject.ItemTypeEnum type) =>
        type == ItemObject.ItemTypeEnum.HeadArmor
        || type == ItemObject.ItemTypeEnum.BodyArmor
        || type == ItemObject.ItemTypeEnum.LegArmor
        || type == ItemObject.ItemTypeEnum.HandArmor
        || type == ItemObject.ItemTypeEnum.Cape;
}
