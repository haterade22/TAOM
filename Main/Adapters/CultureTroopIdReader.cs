using System;
using System.Collections.Generic;
using System.Xml;

namespace TAOM.Adapters;

/// <summary>
/// Reads each culture's troop references from a merged SPCultures document. A Custom Battle registers cultures as
/// plain <c>BasicCultureObject</c> (CustomGame.OnRegisterTypes, v1.5.3), whose deserializer reads no troop
/// attribute, so the troops TAOM uses for a formation's default are read back from the XML the engine loaded.
/// </summary>
internal static class CultureTroopIdReader
{
    private const string CharacterPrefix = "NPCCharacter.";

    public static Dictionary<string, CultureInfo> Read(XmlDocument? merged)
    {
        var cultures = new Dictionary<string, CultureInfo>(StringComparer.OrdinalIgnoreCase);
        if (merged == null)
            return cultures;

        foreach (XmlNode node in merged.GetElementsByTagName("Culture"))
        {
            if (node is not XmlElement culture || string.IsNullOrEmpty(culture.GetAttribute("id")))
                continue;

            var id = culture.GetAttribute("id");
            cultures[id] = new CultureInfo
            {
                Id = id,
                BasicTroopId = TroopId(culture, "basic_troop"),
                MeleeMilitiaTroopId = TroopId(culture, "melee_militia_troop"),
                RangedMilitiaTroopId = TroopId(culture, "ranged_militia_troop"),
                EliteBasicTroopId = TroopId(culture, "elite_basic_troop"),
                RangedEliteMilitiaTroopId = TroopId(culture, "ranged_elite_militia_troop")
            };
        }

        return cultures;
    }

    private static string? TroopId(XmlElement culture, string attribute)
    {
        var value = culture.GetAttribute(attribute);
        if (string.IsNullOrEmpty(value))
            return null;

        return value.StartsWith(CharacterPrefix, StringComparison.Ordinal) ? value.Substring(CharacterPrefix.Length) : value;
    }
}
