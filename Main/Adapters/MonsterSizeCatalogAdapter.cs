using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using TAOM.Core.Logging;
using TAOM.Features.MonsterSize;

namespace TAOM.Adapters;

/// <summary>
/// Boundary implementation of <see cref="IMonsterSizeCatalogAdapter"/> (docs/features/monster-size.md). The Monster
/// object keeps none of TAOM's attribute (Monster.Deserialize reads only its own), so the size is read back from the
/// merged Monsters XML for the current game type, the set the engine loaded, which also carries every module's
/// override of a Monster.
/// </summary>
public class MonsterSizeCatalogAdapter : IMonsterSizeCatalogAdapter
{
    // HorseComponent.BodyLength has a private setter (v1.5.3 HorseComponent.cs:33). Cached once, never per call.
    private static readonly MethodInfo? BodyLengthSetter =
        AccessTools.PropertySetter(typeof(HorseComponent), nameof(HorseComponent.BodyLength));

    // ItemObject caches Effectiveness at load (ItemObject.cs:112, set from the private CalculateEffectiveness() at
    // :476/:671), and a mount's includes body_length (:945). Recomputed after the write so the tournament simulator,
    // its one reader (CharacterObject.GetSimulationAttackPower), rates the mount as when the item carried the size.
    private static readonly MethodInfo? CalculateEffectiveness = AccessTools.Method(typeof(ItemObject), "CalculateEffectiveness");
    private static readonly MethodInfo? EffectivenessSetter =
        AccessTools.PropertySetter(typeof(ItemObject), nameof(ItemObject.Effectiveness));

    private readonly IModLogger _logger;

    public MonsterSizeCatalogAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<KeyValuePair<string, string>> ReadDeclaredSizes()
    {
        // skipValidation: the engine validated these files (and printed the "not declared" line for TAOM's attribute)
        // when it loaded them; this re-read is TAOM's own and should not print it a second time. The game-type filter
        // is the engine's own (Game.LoadBasicFiles -> LoadXML passes GameType.GameTypeStringId), so a Custom Battle
        // re-reads exactly the Monster files it loaded, not SandBox's campaign-only one. The engine still prints one
        // "opening <path>" line per file.
        string? gameType = Game.Current?.GameType?.GameTypeStringId;
        XmlDocument merged = gameType == null
            ? MBObjectManager.GetMergedXmlForManaged("Monsters", skipValidation: true)
            : MBObjectManager.GetMergedXmlForManaged("Monsters", skipValidation: true, ignoreGameTypeInclusionCheck: false, gameType: gameType);
        var declared = new List<KeyValuePair<string, string>>();
        foreach (XmlNode node in merged.GetElementsByTagName("Monster"))
        {
            if (node is XmlElement monster && monster.HasAttribute(MonsterSizeConfig.AttributeName))
                declared.Add(new KeyValuePair<string, string>(monster.GetAttribute("id"), monster.GetAttribute(MonsterSizeConfig.AttributeName)));
        }
        return declared;
    }

    public IReadOnlyList<HorseItemRecord> ReadHorseItems()
    {
        var items = new List<HorseItemRecord>();
        foreach (ItemObject item in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
        {
            HorseComponent? horse = item?.HorseComponent;
            if (horse != null)
                items.Add(new HorseItemRecord(item!.StringId, horse.Monster?.StringId, horse.BodyLength));
        }
        return items;
    }

    public bool SetBodyLength(string itemId, int bodyLength)
    {
        ItemObject? item = MBObjectManager.Instance.GetObject<ItemObject>(itemId);
        HorseComponent? horse = item?.HorseComponent;
        if (BodyLengthSetter == null || horse == null)
            return false;
        BodyLengthSetter.Invoke(horse, new object[] { bodyLength });
        // The size is applied from here on, so a failed recompute is reported, never thrown: only the tournament
        // simulator's rating of this mount lags (ReflectionSiteBindingTests pins all three targets offline).
        if (CalculateEffectiveness == null || EffectivenessSetter == null)
        {
            _logger.LogWarning($"[MonsterSize] {itemId}: ItemObject.CalculateEffectiveness or its setter is gone after an engine " +
                               "update; the tournament rating keeps the old size.");
            return true;
        }
        try
        {
            EffectivenessSetter.Invoke(item, new[] { CalculateEffectiveness.Invoke(item, null) });
        }
        catch (Exception ex)
        {
            // MethodInfo.Invoke wraps the real failure in a TargetInvocationException.
            Exception cause = ex.InnerException ?? ex;
            _logger.LogWarning($"[MonsterSize] {itemId}: resized, but its Effectiveness was not recomputed ({cause.GetType().Name}: " +
                               $"{cause.Message}); the tournament rating keeps the old size.");
        }
        return true;
    }
}
