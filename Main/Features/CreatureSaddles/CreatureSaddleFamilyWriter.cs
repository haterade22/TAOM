using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Core;
using TAOM.Core.Logging;

namespace TAOM.Features.CreatureSaddles;

/// <summary>
/// Engine side of the creature saddle families: writes <see cref="CreatureSaddleFamilies"/> onto a Monster or a harness
/// right after the engine read it from XML. Both <c>FamilyType</c> setters are private (v1.5.7 <c>Monster.cs:86</c>,
/// <c>ArmorComponent.cs:87</c>), set only from the <c>family_type</c> attribute; cached once, never per call
/// (ReflectionSiteBindingTests pins them). A missing setter logs one error and leaves the data's family 1, which is
/// vanilla's behaviour; Patch153 still keeps the cloth reins off these mounts.
/// </summary>
public static class CreatureSaddleFamilyWriter
{
    internal static readonly MethodInfo? MonsterFamilySetter =
        AccessTools.PropertySetter(typeof(Monster), nameof(Monster.FamilyType));

    internal static readonly MethodInfo? ArmorFamilySetter =
        AccessTools.PropertySetter(typeof(ArmorComponent), nameof(ArmorComponent.FamilyType));

    private static IModLogger? _logger;
    private static bool _reportedMissing;

    internal static void Initialize(IModLogger? logger) => _logger = logger;

    public static void ApplyToMonster(Monster? monster)
    {
        var family = CreatureSaddleFamilies.MonsterFamily(monster?.StringId);
        if (monster == null || family == null || monster.FamilyType == family.Value)
            return;
        if (!Write(MonsterFamilySetter, monster, family.Value))
            return;
        _logger?.LogInfo($"[CreatureSaddles] Monster '{monster.StringId}' takes harness family {family.Value}");
    }

    public static void ApplyToItem(ItemObject? item)
    {
        var family = CreatureSaddleFamilies.HarnessFamily(item?.StringId);
        var armor = item?.ArmorComponent;
        if (armor == null || family == null || armor.FamilyType == family.Value)
            return;
        if (!Write(ArmorFamilySetter, armor, family.Value))
            return;
        _logger?.LogInfo($"[CreatureSaddles] harness '{item!.StringId}' takes family {family.Value}");
    }

    private static bool Write(MethodInfo? setter, object target, int family)
    {
        if (setter == null)
        {
            if (!_reportedMissing)
            {
                _reportedMissing = true;
                _logger?.LogError("[CreatureSaddles] a FamilyType setter is missing in this engine: creature mounts keep the horse saddle family");
            }
            return false;
        }

        setter.Invoke(target, new object[] { family });
        return true;
    }
}
