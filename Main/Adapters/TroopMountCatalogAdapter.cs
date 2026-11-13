using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using TAOM.Features.Arachnophobia;

namespace TAOM.Adapters;

/// <summary>
/// Boundary implementation of <see cref="ITroopMountCatalogAdapter"/> (docs/features/arachnophobia-mode.md). A campaign
/// registers CharacterObject and a Custom Battle registers BasicCharacterObject. <c>BasicCharacterObject</c> finds the
/// troops in both, because MBObjectManager matches a non-sealed type with IsAssignableFrom (MBObjectManager.cs:590, :755).
/// </summary>
public class TroopMountCatalogAdapter : ITroopMountCatalogAdapter
{
    // Both setters are private (v1.5.6 CharacterObject.cs:320, BasicCharacterObject.cs:120). Cached once, never per call.
    private static readonly MethodInfo? UpgradeTargetsSetter =
        AccessTools.PropertySetter(typeof(CharacterObject), nameof(CharacterObject.UpgradeTargets));

    private static readonly MethodInfo? IsObsoleteSetter =
        AccessTools.PropertySetter(typeof(BasicCharacterObject), nameof(BasicCharacterObject.IsObsolete));

    public bool ItemExists(string itemId) => MBObjectManager.Instance.GetObject<ItemObject>(itemId) != null;

    public IReadOnlyList<TroopMountRecord> ReadRiders(string monsterId)
    {
        var riders = new List<TroopMountRecord>();
        foreach (BasicCharacterObject troop in MBObjectManager.Instance.GetObjectTypeList<BasicCharacterObject>())
        {
            var itemIds = new List<string>();
            foreach (Equipment equipment in troop.BattleEquipments)
            {
                ItemObject? mount = equipment?[EquipmentIndex.Horse].Item;
                if (mount?.HorseComponent?.Monster?.StringId == monsterId && !itemIds.Contains(mount!.StringId))
                    itemIds.Add(mount.StringId);
            }
            if (itemIds.Count > 0)
                riders.Add(new TroopMountRecord(troop.StringId, troop.IsHero, itemIds));
        }
        return riders;
    }

    public int ReplaceMount(string troopId, string spiderItemId, string wargItemId, string harnessItemId)
    {
        MBObjectManager manager = MBObjectManager.Instance;
        BasicCharacterObject? troop = manager.GetObject<BasicCharacterObject>(troopId);
        ItemObject? warg = manager.GetObject<ItemObject>(wargItemId);
        ItemObject? harness = manager.GetObject<ItemObject>(harnessItemId);
        if (troop == null || warg == null || harness == null)
            return 0;

        // Every battle set: a spawned agent draws each slot from its own random set (Equipment.cs:566-593).
        int written = 0;
        foreach (Equipment equipment in troop.BattleEquipments)
        {
            if (equipment?[EquipmentIndex.Horse].Item?.StringId != spiderItemId)
                continue;
            equipment[EquipmentIndex.Horse] = new EquipmentElement(warg);
            equipment[EquipmentIndex.HorseHarness] = new EquipmentElement(harness);
            written++;
        }
        return written;
    }

    public IReadOnlyList<TroopUpgradeRecord> ReadUpgrades()
    {
        var records = new List<TroopUpgradeRecord>();
        foreach (CharacterObject troop in MBObjectManager.Instance.GetObjectTypeList<CharacterObject>())
        {
            if (!troop.IsHero && troop.UpgradeTargets is { Length: > 0 } targets)
                records.Add(new TroopUpgradeRecord(troop.StringId, targets.Where(t => t != null).Select(t => t.StringId).ToList()));
        }
        return records;
    }

    public bool SetUpgradeTargets(string troopId, IReadOnlyCollection<string> keptTargetIds)
    {
        CharacterObject? troop = MBObjectManager.Instance.GetObject<CharacterObject>(troopId);
        if (UpgradeTargetsSetter == null || troop == null)
            return false;
        CharacterObject[] kept = troop.UpgradeTargets.Where(t => t != null && keptTargetIds.Contains(t.StringId)).ToArray();
        UpgradeTargetsSetter.Invoke(troop, new object[] { kept });
        return true;
    }

    public void HideFromEncyclopedia(string troopId)
    {
        CharacterObject? troop = MBObjectManager.Instance.GetObject<CharacterObject>(troopId);
        if (troop != null)
            troop.HiddenInEncyclopedia = true;
    }

    public bool MarkObsolete(string troopId)
    {
        BasicCharacterObject? troop = MBObjectManager.Instance.GetObject<BasicCharacterObject>(troopId);
        if (IsObsoleteSetter == null || troop == null)
            return false;
        IsObsoleteSetter.Invoke(troop, new object[] { true });
        return true;
    }
}
