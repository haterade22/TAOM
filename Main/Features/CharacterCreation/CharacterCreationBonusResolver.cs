using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TAOM.Core.Logging;

namespace TAOM.Features.CharacterCreation;

internal static class CharacterCreationBonusResolver
{
    private static readonly Dictionary<string, Func<SkillObject>> SkillMap =
        new Dictionary<string, Func<SkillObject>>(StringComparer.OrdinalIgnoreCase)
        {
            ["OneHanded"] = () => DefaultSkills.OneHanded,
            ["TwoHanded"] = () => DefaultSkills.TwoHanded,
            ["Polearm"] = () => DefaultSkills.Polearm,
            ["Bow"] = () => DefaultSkills.Bow,
            ["Crossbow"] = () => DefaultSkills.Crossbow,
            ["Throwing"] = () => DefaultSkills.Throwing,
            ["Riding"] = () => DefaultSkills.Riding,
            ["Athletics"] = () => DefaultSkills.Athletics,
            ["Crafting"] = () => DefaultSkills.Crafting,
            ["Scouting"] = () => DefaultSkills.Scouting,
            ["Tactics"] = () => DefaultSkills.Tactics,
            ["Roguery"] = () => DefaultSkills.Roguery,
            ["Charm"] = () => DefaultSkills.Charm,
            ["Leadership"] = () => DefaultSkills.Leadership,
            ["Trade"] = () => DefaultSkills.Trade,
            ["Steward"] = () => DefaultSkills.Steward,
            ["Medicine"] = () => DefaultSkills.Medicine,
            ["Engineering"] = () => DefaultSkills.Engineering,
        };

    private static readonly Dictionary<string, Func<CharacterAttribute>> AttributeMap =
        new Dictionary<string, Func<CharacterAttribute>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Vigor"] = () => DefaultCharacterAttributes.Vigor,
            ["Control"] = () => DefaultCharacterAttributes.Control,
            ["Endurance"] = () => DefaultCharacterAttributes.Endurance,
            ["Cunning"] = () => DefaultCharacterAttributes.Cunning,
            ["Social"] = () => DefaultCharacterAttributes.Social,
            ["Intelligence"] = () => DefaultCharacterAttributes.Intelligence,
        };

    internal static SkillObject[] ResolveSkills(
        string[]? skillNames,
        IModLogger logger,
        bool logNullResolution,
        IReadOnlyDictionary<string, Func<SkillObject>>? skillMap = null)
    {
        if (skillNames == null || skillNames.Length == 0)
            return Array.Empty<SkillObject>();

        skillMap ??= SkillMap;
        var resolved = new List<SkillObject>();
        foreach (var name in skillNames)
        {
            if (skillMap.TryGetValue(name, out var factory))
            {
                var skill = factory();
                if (skill != null)
                    resolved.Add(skill);
                else if (logNullResolution)
                    logger.LogWarning($"Skill '{name}' resolved to null — game may not be initialized");
            }
            else
            {
                logger.LogWarning($"Unknown skill name: '{name}'");
            }
        }
        return resolved.ToArray();
    }

    internal static CharacterAttribute? ResolveAttribute(
        string? attributeName,
        IModLogger logger,
        bool logNullResolution,
        IReadOnlyDictionary<string, Func<CharacterAttribute>>? attributeMap = null)
    {
        if (string.IsNullOrEmpty(attributeName))
            return null;

        attributeMap ??= AttributeMap;
        if (attributeMap.TryGetValue(attributeName, out var factory))
        {
            var attribute = factory();
            if (attribute == null && logNullResolution)
                logger.LogWarning($"Attribute '{attributeName}' resolved to null — game may not be initialized");
            return attribute;
        }

        logger.LogWarning($"Unknown attribute name: '{attributeName}'");
        return null;
    }
}
