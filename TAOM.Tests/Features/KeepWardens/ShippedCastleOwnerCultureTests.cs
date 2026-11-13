using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Xsl;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Core;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.KeepWardens;

/// <summary>
/// Keep Wardens (#888) reads the owner culture's three patrol templates. TAOM gives party templates to every culture except
/// <see cref="CultureDataFixture.HideoutOnlyCultures"/> and <see cref="CultureDataFixture.SettlementlessBanditCultures"/>
/// (see <see cref="CulturePartyTemplateTests"/>), so a castle owned by a clan of one of those would find no template. The
/// settlements are the live, unversioned TAOM_Map file; the clans are TAOM's own <c>clans.xml</c> plus vanilla's
/// <c>spclans.xml</c> run through TAOM's <c>spclans.xslt</c>, the way the engine loads them (as ShippedClanSideCoverageTests does). Inconclusive on a machine without the install.
/// </summary>
[TestClass]
[TestCategory("LiveInstall")]
public class ShippedCastleOwnerCultureTests
{
    private static string GameDir()
    {
        var env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        return string.IsNullOrWhiteSpace(env) ? @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord" : env!;
    }

    private static string Id(string? reference) => CultureDataFixture.StripPrefix(reference ?? string.Empty);

    /// <summary>Applies a stylesheet to a document the way MBObjectManager.ApplyXslt does.</summary>
    private static XDocument Transform(XDocument source, string stylesheet)
    {
        var xslt = CompiledXslt.Get(stylesheet, XsltLoadStyle.IgnoreDtdNoResolver);

        using var input = source.CreateReader();
        using var buffer = new StringWriter();
        using (var output = XmlWriter.Create(buffer, new XmlWriterSettings { ConformanceLevel = ConformanceLevel.Auto }))
            xslt.Transform(input, output);
        return XDocument.Parse(buffer.ToString());
    }

    private static XDocument LoadVanillaClans(string path)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null, IgnoreWhitespace = true };
        using var reader = XmlReader.Create(path, settings);
        var vanilla = XDocument.Load(reader);
        return Transform(vanilla, Path.Combine(CultureDataFixture.ModuleDataPath(), "spclans.xslt"));
    }

    private static void AddClans(Dictionary<string, string> cultures, XDocument document)
    {
        foreach (var clan in document.Descendants("Faction"))
        {
            var id = (string?)clan.Attribute("id");
            var culture = (string?)clan.Attribute("culture");
            if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(culture))
                cultures[id!] = Id(culture);
        }
    }

    [TestMethod]
    public void NoCastle_IsOwnedByAClan_OfAHideoutOnlyOrSettlementlessCulture()
    {
        var settlements = Path.Combine(GameDir(), "Modules", "TAOM_Map", "ModuleData", "settlements.xml");
        var vanillaClans = Path.Combine(GameDir(), "Modules", "SandBox", "ModuleData", "spclans.xml");
        if (!File.Exists(settlements) || !File.Exists(vanillaClans))
            Assert.Inconclusive("TAOM_Map or SandBox is not installed on this machine; the castle owners cannot be read here.");

        var clanCultures = new Dictionary<string, string>(StringComparer.Ordinal);
        AddClans(clanCultures, LoadVanillaClans(vanillaClans));
        AddClans(clanCultures, XDocument.Load(Path.Combine(CultureDataFixture.ModuleDataPath(), "characters", "clans.xml")));

        var castles = XDocument.Load(settlements).Descendants("Settlement")
            .Where(s => s.Descendants("Town").Any(t => (string?)t.Attribute("is_castle") == "true"))
            .ToList();
        Assert.IsTrue(castles.Count > 0, "the castle query found nothing: the settlements.xml shape changed");

        var offenders = new List<string>();
        foreach (var castle in castles)
        {
            var settlementId = (string?)castle.Attribute("id");
            var owner = Id((string?)castle.Attribute("owner"));
            if (!clanCultures.TryGetValue(owner, out var culture))
                offenders.Add($"{settlementId}: owner clan '{owner}' is in neither clans.xml nor spclans.xml");
            else if (CultureDataFixture.HideoutOnlyCultures.Contains(culture) || CultureDataFixture.SettlementlessBanditCultures.Contains(culture))
                offenders.Add($"{settlementId}: owner clan '{owner}' has culture '{culture}', which has no patrol templates");
        }

        Assert.AreEqual(0, offenders.Count, string.Join("\n", offenders));
    }
}
