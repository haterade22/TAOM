using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Core;

namespace TAOM.Tests.Infrastructure.Localization;

/// <summary>
/// The lord dialog offers "political philosophy" only when <c>GameTexts.TryGetText</c> finds
/// <c>str_political_philosophy_&lt;hero&gt;_for_&lt;faction leader&gt;</c>, and its longer branch
/// also the <c>_b</c> and <c>_c</c> ids (v1.5.7 <c>LordConversationsCampaignBehavior</c> :77-87).
/// TAOM reuses vanilla lord ids, so vanilla's 50 speeches about the Empire and Calradia would play
/// for a Middle-earth lord (reported by Silas He, #1439).
///
/// <para>
/// TAOM's <c>XmlMergeService.MergeFast</c>, which replaces the engine's merge loop, applies a
/// stylesheet to the whole document merged so far (XmlMergeService.cs:79-88). So
/// <c>module_strings.xslt</c>, which loads after SandBox, can drop them from
/// <c>world_lore_strings.xml</c> (<c>wanderer_strings.xml</c> holds three more, inside a comment).
/// This runs it over that file from the install, and skips when there is none.
/// </para>
/// </summary>
[TestClass]
[TestCategory("LiveInstall")]
public class PoliticalPhilosophyStringsTests
{
    private const string Prefix = "str_political_philosophy_";
    private const string DefaultGameDir = @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord";

    private static string? SandBoxModuleData()
    {
        foreach (var root in new[] { Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR"), DefaultGameDir })
        {
            if (string.IsNullOrEmpty(root)) continue;
            var path = Path.Combine(root, "Modules", "SandBox", "ModuleData");
            if (File.Exists(Path.Combine(path, "world_lore_strings.xml"))) return path;
        }
        return null;
    }

    private static XDocument Transform(string sourceXml, string stylesheet)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
        var xslt = CompiledXslt.Get(stylesheet, XsltLoadStyle.IgnoreDtdNoResolver);
        using var input = XmlReader.Create(sourceXml, settings);
        using var buffer = new StringWriter();
        using (var output = XmlWriter.Create(buffer, new XmlWriterSettings { ConformanceLevel = ConformanceLevel.Auto }))
            xslt.Transform(input, output);
        return XDocument.Parse(buffer.ToString());
    }

    [TestMethod]
    public void ModuleStringsXslt_DropsEveryVanillaPoliticalPhilosophy_AndKeepsTheRest()
    {
        const string file = "world_lore_strings.xml";
        var vanilla = SandBoxModuleData();
        if (vanilla == null)
            Assert.Inconclusive("Bannerlord install not found; the stylesheet has nothing to run against here.");

        var source = Path.Combine(vanilla!, file);
        string Id(XElement s) => (string?)s.Attribute("id") ?? "";
        var before = XDocument.Load(source).Descendants("string").ToList();
        var philosophies = before.Count(s => Id(s).StartsWith(Prefix, StringComparison.Ordinal));
        Assert.IsTrue(philosophies > 0, $"{file} holds no {Prefix} string; the vanilla data moved");

        var after = Transform(source, Path.Combine(CultureDataFixture.ModuleDataPath(), "module_strings.xslt"))
            .Descendants("string").ToList();

        CollectionAssert.AreEqual(new string[0],
            after.Select(Id).Where(id => id.StartsWith(Prefix, StringComparison.Ordinal)).ToArray(),
            "a vanilla political philosophy survives module_strings.xslt");
        Assert.AreEqual(before.Count - philosophies, after.Count, "module_strings.xslt dropped other strings too");
    }
}
