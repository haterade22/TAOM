using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace TAOM.CoopCompat.LoadOrder;

/// <summary>
/// Which modules a co-op load order check compares, and where two load orders first differ. Coop's join check
/// compares the module lists but not their order, and the engine merges module XML in load order, so a later
/// module's items or troops replace an earlier one's. No game state.
/// </summary>
public static class LoadOrderRules
{
    /// <summary>Most module ids a hello carries.</summary>
    public const int MaxModules = 256;

    /// <summary>Longest module id that is sent or accepted, and longest module name shown.</summary>
    public const int MaxIdLength = 128;

    /// <summary>TAOM's own block and the mods TAOM's order places inside it: always compared.</summary>
    public static readonly IReadOnlyList<string> AlwaysCompared = new[]
    {
        "TAOM.Dependencies", "LOTRLOME_Armory", "DualWield", "TAOM_Map", "TAOM", "TAOM_AIO_Compat",
    };

    /// <summary>SubModule.xml data kinds that define items, troops, cultures, factions or places. Text kinds do not count.</summary>
    public static readonly IReadOnlyCollection<string> ObjectData = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Items", "NPCCharacters", "EquipmentRosters", "CraftingPieces", "CraftingTemplates", "ItemModifiers", "ItemModifierGroups",
        "WeaponDescriptions", "Monsters", "SPCultures", "BasicCultures", "Factions", "Kingdoms", "Heroes", "partyTemplates",
        "Settlements", "SkillSets", "BannerIcons", "BodyProperties",
    };

    public static bool IsObjectData(string? xmlName) => !string.IsNullOrEmpty(xmlName) && ObjectData.Contains(xmlName!);

    /// <summary>True when a SubModule.xml text lists object data in its &lt;Xmls&gt;. Missing or malformed text is false.</summary>
    public static bool DeclaresObjectData(string? subModuleXml)
    {
        if (string.IsNullOrEmpty(subModuleXml)) return false;
        try
        {
            var document = new XmlDocument { XmlResolver = null };
            document.LoadXml(subModuleXml);
            var names = document.SelectNodes("/Module/Xmls/XmlNode/XmlName/@id");
            return names != null && names.Cast<XmlNode>().Any(n => IsObjectData(n.Value));
        }
        catch (XmlException)
        {
            return false;
        }
    }

    /// <summary>This game's ids to send, in load order: an empty or overlong id is left out so it cannot get the list refused.</summary>
    public static string[] LocalOrder(IEnumerable<ModuleEntry> modules) =>
        (modules ?? Enumerable.Empty<ModuleEntry>())
            .Select(m => m?.Id)
            .Where(id => !string.IsNullOrEmpty(id) && id!.Length <= MaxIdLength)
            .Take(MaxModules)
            .ToArray()!;

    /// <summary>Why a received order is refused, or null when it can be compared.</summary>
    public static string? Problem(string[]? ids)
    {
        if (ids == null) return "no list";
        if (ids.Length > MaxModules) return "more than " + MaxModules + " modules";
        if (ids.Any(id => string.IsNullOrEmpty(id) || id.Length > MaxIdLength)) return "a module id is empty or longer than " + MaxIdLength;
        return null;
    }

    /// <summary>
    /// The modules whose order matters, decided from the host's modules: the TAOM block, plus every other
    /// non-official module that ships object data. Official modules keep the game's fixed order.
    /// </summary>
    public static HashSet<string> Compared(IEnumerable<ModuleEntry> hostModules)
    {
        var compared = new HashSet<string>(AlwaysCompared, StringComparer.Ordinal);
        foreach (var module in hostModules ?? Enumerable.Empty<ModuleEntry>())
        {
            if (module != null && !module.IsOfficial && module.HasObjectData && !string.IsNullOrEmpty(module.Id)) compared.Add(module.Id);
        }

        return compared;
    }

    /// <summary>
    /// Cuts both orders to the compared modules that both sides have and finds the first place where they differ.
    /// False when they match. On a difference, on the host <paramref name="hostFirst"/> loads before
    /// <paramref name="hostSecond"/>, and for the player after it.
    /// </summary>
    public static bool TryFindDifference(IList<string> hostOrder, IList<string> playerOrder, ISet<string> compared,
        out string? hostFirst, out string? hostSecond)
    {
        hostFirst = null;
        hostSecond = null;

        var hostAll = hostOrder ?? Array.Empty<string>();
        var playerAll = playerOrder ?? Array.Empty<string>();
        var hostIds = new HashSet<string>(hostAll, StringComparer.Ordinal);
        var playerIds = new HashSet<string>(playerAll, StringComparer.Ordinal);

        var host = hostAll.Where(id => compared.Contains(id) && playerIds.Contains(id)).Distinct().ToList();
        var player = playerAll.Where(id => compared.Contains(id) && hostIds.Contains(id)).Distinct().ToList();

        for (var i = 0; i < host.Count && i < player.Count; i++)
        {
            if (string.Equals(host[i], player[i], StringComparison.Ordinal)) continue;

            hostFirst = host[i];
            hostSecond = player[i];
            return true;
        }

        return false;
    }

    /// <summary>A module's name with its id, clipped to <see cref="MaxIdLength"/>.</summary>
    public static string Describe(string id, string? name)
    {
        var text = string.IsNullOrWhiteSpace(name) || name == id ? id : name + " (" + id + ")";
        return text.Length > MaxIdLength ? text.Substring(0, MaxIdLength) : text;
    }
}
