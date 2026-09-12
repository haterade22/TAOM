using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.Execution;

namespace TAOM.Tests.Features.WandererAllegiance;

/// <summary>
/// The hire rule keys on the wanderer's culture and treats an unclassified culture as Neutral,
/// which means "serves anyone". That is a silent permit, not a visible failure, so a wanderer
/// culture added to either wanderer XML without an <c>execution/alignment.json</c> entry would
/// quietly reopen the hole this feature closes. Same shape and reasoning as
/// <c>ShippedCultureAlignmentCoverageTests</c> for lords.
/// </summary>
[TestClass]
public class WandererCultureAlignmentCoverageTests
{
    private static string ModuleDataPath => Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            @"..\..\..\..\Main\_Module\ModuleData"));

    private static Dictionary<string, string> ShippedAlignments()
    {
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(ModuleDataPath);
        return new AlignmentConfigProvider(pathService, Substitute.For<IModLogger>()).LoadAlignments();
    }

    /// <summary>
    /// Every NPCCharacter in both files is a wanderer (210 templates and 17 named companions as of
    /// 2026-09-12), but the occupation is still checked so a non-wanderer added later is not counted.
    /// </summary>
    private static List<string> WandererCultures(params string[] relativePath)
    {
        var xml = File.ReadAllText(Path.Combine(new[] { ModuleDataPath }.Concat(relativePath).ToArray()));
        return Regex.Matches(xml, "<NPCCharacter\\b([^>]*)>")
            .Cast<Match>()
            .Select(m => m.Groups[1].Value)
            .Where(attrs => attrs.Contains("occupation=\"Wanderer\""))
            .Select(attrs => Regex.Match(attrs, "culture\\s*=\\s*\"Culture\\.([A-Za-z_0-9]+)\""))
            .Where(m => m.Success)
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> TemplateWandererCultures() => WandererCultures("taom_wanderers.xml");

    private static List<string> NamedCompanionCultures() => WandererCultures("named_companions", "named_companions.xml");

    [TestMethod]
    public void WandererXmls_DeclareCultures_SoTheGuardBelowIsMeaningful()
    {
        // 20 and 7 as of 2026-09-12. The floors guard against a regex that silently matches nothing,
        // which would make the coverage assertions below vacuously pass.
        var templates = TemplateWandererCultures();
        var named = NamedCompanionCultures();

        Assert.IsTrue(templates.Count >= 15,
            $"Expected at least 15 distinct wanderer cultures in taom_wanderers.xml, found {templates.Count}. The extraction regex has probably gone stale.");
        Assert.IsTrue(named.Count >= 5,
            $"Expected at least 5 distinct named-companion cultures, found {named.Count}. The extraction regex has probably gone stale.");
    }

    [TestMethod]
    public void EveryTemplateWandererCulture_IsClassifiedInAlignmentJson()
    {
        var alignments = ShippedAlignments();
        var unclassified = TemplateWandererCultures().Where(id => !alignments.ContainsKey(id)).ToList();

        Assert.AreEqual(0, unclassified.Count,
            "These cultures seed tavern wanderers but have no execution/alignment.json entry, so they resolve " +
            "to Neutral and their wanderers will serve either side unchecked: " + string.Join(", ", unclassified));
    }

    [TestMethod]
    public void EveryNamedCompanionCulture_IsClassifiedInAlignmentJson()
    {
        var alignments = ShippedAlignments();
        var unclassified = NamedCompanionCultures().Where(id => !alignments.ContainsKey(id)).ToList();

        Assert.AreEqual(0, unclassified.Count,
            "These cultures are carried by named companions but have no execution/alignment.json entry: " +
            string.Join(", ", unclassified));
    }

    [TestMethod]
    public void ShippedAlignments_ClassifyTheReportedCompanions()
    {
        // Aragorn (gondor), Legolas (mirkwood), Gimli (erebor) are the report; Maztog and BlackRose
        // (isengard) are the mirror case. If any drifts to neutral or drops out, the feature stops
        // covering the case it was written for.
        var alignments = ShippedAlignments();

        foreach (var free in new[] { "gondor", "mirkwood", "erebor" })
        {
            Assert.IsTrue(alignments.ContainsKey(free), $"{free} must stay classified");
            Assert.IsTrue(StringComparer.OrdinalIgnoreCase.Equals(alignments[free], "free"), $"{free} must stay Free, was '{alignments[free]}'");
        }

        Assert.IsTrue(alignments.ContainsKey("isengard"), "isengard must stay classified");
        Assert.IsTrue(StringComparer.OrdinalIgnoreCase.Equals(alignments["isengard"], "evil"), $"isengard must stay Evil, was '{alignments["isengard"]}'");
    }
}
