using System.Collections.Generic;
using System.Xml;

namespace TAOM.Features.LocalizationOverride;

/// <summary>
/// Parses taom_module_strings.xml to extract English localization overrides.
///
/// Entries whose text attribute starts with {=VANILLA_ID} (where VANILLA_ID is NOT
/// prefixed with "taom_") are treated as overrides for hardcoded C# strings.
/// The vanilla ID is extracted and mapped to the override text (everything after the
/// closing brace).
/// </summary>
public static class LocalizationOverrideLoader
{
    public static Dictionary<string, string> ParseOverrides(string xml)
    {
        var overrides = new Dictionary<string, string>();
        var doc = new XmlDocument();
        doc.LoadXml(xml);

        var nodes = doc.SelectNodes("//string[@text]");
        if (nodes == null)
            return overrides;

        foreach (XmlNode node in nodes)
        {
            string text = node.Attributes?["text"]?.Value;
            if (text == null || text.Length <= 2 || text[0] != '{' || text[1] != '=')
                continue;

            int end = text.IndexOf('}', 2);
            if (end < 0)
                continue;

            string id = text.Substring(2, end - 2);

            if (id == "!" || id == "*")
                continue;

            if (id.StartsWith("taom_"))
                continue;

            string overrideText = text.Substring(end + 1);
            overrides[id] = overrideText;
        }

        return overrides;
    }

    public static Dictionary<string, string> ParseOverridesFromFile(string filePath)
    {
        string xml = System.IO.File.ReadAllText(filePath);
        return ParseOverrides(xml);
    }
}
