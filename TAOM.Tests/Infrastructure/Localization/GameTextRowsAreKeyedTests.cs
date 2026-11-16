using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Infrastructure.Localization;

/// <summary>
/// Every row of TAOM's GameText files opens with its <c>{=KEY}</c> (#1385). The engine loads a row's text
/// as a TextObject. Without a key, <c>GameTexts.FindText</c> keys its copy by the row id but keeps the text
/// cached from the English row at load, so the player sees English until the language changes in game.
/// taom_wotr_strings.xml was the one file without keys: its readers all use <c>{=key}English</c> literals,
/// but the bare rows looked untranslatable, and the CN team wrote a stylesheet to key 73 of them.
/// </summary>
[TestClass]
public class GameTextRowsAreKeyedTests
{
    private static readonly string ModuleRoot = Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Main", "_Module"));

    [TestMethod]
    public void EveryGameTextRow_OpensWithItsKey()
    {
        var paths = Regex.Matches(File.ReadAllText(Path.Combine(ModuleRoot, "SubModule.xml")),
                "<XmlName id=\"GameText\" path=\"([^\"]+)\"")
            .Cast<Match>()
            .Select(m => Path.Combine(ModuleRoot, "ModuleData", m.Groups[1].Value + ".xml"))
            .Where(File.Exists)
            .ToList();
        var offenders = new List<string>();
        var rows = 0;
        foreach (var file in paths)
        {
            foreach (var row in XDocument.Load(file).Descendants("string"))
            {
                rows++;
                var text = (string?)row.Attribute("text") ?? "";
                if (!text.StartsWith("{=", StringComparison.Ordinal))
                    offenders.Add($"{Path.GetFileName(file)}: {(string?)row.Attribute("id")}");
            }
        }

        Assert.IsTrue(paths.Count > 15 && rows > 10000, $"Only {paths.Count} GameText files and {rows} rows found; the scan is broken.");
        Assert.AreEqual(0, offenders.Count,
            "GameText rows with no {=KEY}; write text=\"{=<id>}English\":\n" + string.Join("\n", offenders.Take(40)));
    }
}
