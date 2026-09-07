using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.Execution;

namespace TAOM.Tests.Features.Execution;

/// <summary>
/// The execution alignment rules place the player by kingdom id, falling back to his culture when
/// his clan has no kingdom (independent, mercenary, or enlisted — enlistment deliberately does not
/// join the commander's kingdom). That fallback is only as good as the culture table: a playable
/// culture missing from execution/alignment.json resolves Neutral, and a Neutral executor is nobody's
/// ally, so his own side stops approving of his executions.
/// </summary>
/// <remarks>
/// Covers TAOM's own playable cultures only. The six vanilla main cultures live in SandBoxCore,
/// outside this repo, and all six already carry entries; an engine bump that adds one is
/// /engine-bump's problem, not something a repo-only test can see. Reads alignment.json through the
/// real <see cref="AlignmentConfigProvider"/> because <see cref="AlignmentService.GetCultureSide"/>
/// cannot tell "explicitly neutral" from "missing" — both return Neutral. Sibling gate:
/// ShippedCultureAlignmentCoverageTests, which covers the cultures used by lords.xml.
/// </remarks>
[TestClass]
public class ShippedMainCultureAlignmentCoverageTests
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
    /// Parsed as XML rather than matched with a regex: the attribute sits several lines below the
    /// element name in taom_spcultures.xml, so a single-line pattern silently matches nothing and
    /// the guard passes vacuously.
    /// </summary>
    private static List<string> PlayableTaomCultureIds()
    {
        var doc = XDocument.Load(Path.Combine(ModuleDataPath, "taom_spcultures.xml"));

        return doc.Descendants()
            .Where(e => string.Equals(e.Name.LocalName, "Culture", StringComparison.OrdinalIgnoreCase))
            .Where(e => string.Equals((string)e.Attribute("is_main_culture"), "true", StringComparison.OrdinalIgnoreCase))
            .Select(e => (string)e.Attribute("id"))
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    [TestMethod]
    public void TaomSpCultures_DeclaresPlayableCultures_SoTheGuardBelowIsMeaningful()
    {
        var cultures = PlayableTaomCultureIds();

        Assert.IsTrue(cultures.Count > 0,
            "Found no is_main_culture cultures in taom_spcultures.xml — the coverage guard below " +
            "would pass vacuously. The file or its attribute naming has changed.");
    }

    [TestMethod]
    public void EveryPlayableTaomCulture_HasAnAlignmentEntry()
    {
        var alignments = ShippedAlignments();
        var missing = PlayableTaomCultureIds()
            .Where(id => !alignments.ContainsKey(id))
            .ToList();

        Assert.AreEqual(0, missing.Count,
            "These playable cultures have no execution/alignment.json entry, so a player of that " +
            "culture with no kingdom resolves Neutral and loses alignment-aware execution: " +
            string.Join(", ", missing));
    }

    [TestMethod]
    public void EveryPlayableTaomCulture_ResolvesTheSideItsEntryDeclares()
    {
        var alignments = ShippedAlignments();
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(ModuleDataPath);
        var service = new AlignmentService(
            new AlignmentConfigProvider(pathService, Substitute.For<IModLogger>()),
            Substitute.For<IModLogger>());

        foreach (var id in PlayableTaomCultureIds())
        {
            var declared = (FactionSide)Enum.Parse(typeof(FactionSide), alignments[id], ignoreCase: true);

            // With no kingdom to go on, ResolveSide must land on the culture's declared side.
            Assert.AreEqual(declared, service.ResolveSide("", id),
                $"Culture '{id}' does not resolve to its declared side through the kingdom-less path.");
        }
    }
}
