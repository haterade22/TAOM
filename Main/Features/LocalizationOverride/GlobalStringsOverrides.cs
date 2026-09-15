using System.Collections.Generic;
using System.IO;
using System.Xml;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace TAOM.Features.LocalizationOverride;

/// <summary>
/// Makes TAOM's <c>ModuleData/global_strings.xml</c> rows win over Native's in the main-menu
/// <c>Module.CurrentModule.GlobalTextManager</c>.
///
/// <para>
/// That manager is filled once, by <c>GameTextManager.LoadDefaultTexts</c> at module init, from the
/// literal path <c>ModuleData/global_strings.xml</c> of every module in load order. No merge, no XSLT,
/// no SubModule.xml <c>GameText</c> node reaches it (those feed the separate per-Game manager). Each
/// row goes through <c>GameText.AddVariationWithId</c>, which APPENDS a same-id variation whose text
/// differs, and <c>GetVariation</c> is a first-match scan. So a TAOM row that reuses a vanilla
/// variation id (<c>str_campaign_starting_options_item_name.sturgia</c>, "Dale") trails Native's
/// "Sturgia" and is never returned. The engine's replace primitive is the public
/// <c>GameText.SetVariationWithId</c>; <see cref="Apply"/> re-applies every TAOM row through it.
/// </para>
///
/// <para>
/// <see cref="Parse"/> mirrors <c>LoadFromXML</c>: the <c>id</c> attribute splits once on '.', the
/// first segment is the GameText id, the second (or "") the variation. Rows carrying <c>&lt;tags&gt;</c>
/// are applied with an empty tag list; TAOM ships none.
/// </para>
/// </summary>
public static class GlobalStringsOverrides
{
    public static IReadOnlyList<(string Id, string Variation, string Text)> ParseFromFile(string path)
        => Parse(File.ReadAllText(path));

    public static IReadOnlyList<(string Id, string Variation, string Text)> Parse(string xml)
    {
        var rows = new List<(string Id, string Variation, string Text)>();
        var doc = new XmlDocument();
        doc.LoadXml(xml);
        var nodes = doc.SelectNodes("/strings/string");
        if (nodes == null)
            return rows;

        foreach (XmlNode node in nodes)
        {
            var fullId = node.Attributes?["id"]?.Value;
            var text = node.Attributes?["text"]?.Value;
            if (string.IsNullOrEmpty(fullId) || text == null)
                continue;

            var parts = fullId.Split('.');
            rows.Add((parts[0], parts.Length > 1 ? parts[1] : "", text));
        }
        return rows;
    }

    /// <summary>Returns the number of rows applied.</summary>
    public static int Apply(GameTextManager manager, IEnumerable<(string Id, string Variation, string Text)> rows)
    {
        var applied = 0;
        foreach (var (id, variation, text) in rows)
        {
            // No CacheTokens() here, unlike LoadFromXML: the cache is an optimisation that refills
            // lazily on first render, and skipping it keeps this callable before MBTextManager has a
            // language (the unit tests run it that way).
            manager.AddGameText(id).SetVariationWithId(variation, new TextObject(text), new List<GameTextManager.ChoiceTag>());
            applied++;
        }
        return applied;
    }
}
