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

namespace TAOM.Tests.Features.MarriageAlignment;

/// <summary>
/// The marriage gate keys on culture and treats an unclassified culture as Neutral, which means
/// "may marry anyone". That is a silent permit, not a visible failure, so a culture added to
/// lords.xml without a matching execution/alignment.json entry would quietly reopen exactly the
/// hole this feature closes (Boromir marrying a Misty Mountain orc, issue #542).
/// </summary>
/// <remarks>
/// This test is the reason the runtime rule has no kingdom fallback: rather than papering over an
/// unclassified culture at runtime with a second lookup that has its own defection semantics, the
/// coverage gap is made impossible at build time. Reads alignment.json through the real
/// <see cref="AlignmentConfigProvider"/> because <see cref="AlignmentService.GetCultureSide"/>
/// cannot distinguish "explicitly neutral" from "missing" - both return Neutral.
/// </remarks>
[TestClass]
public class ShippedCultureAlignmentCoverageTests
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
    /// Derived from lords.xml rather than hardcoded, so a newly-authored culture is covered
    /// automatically. A hardcoded list would itself go stale.
    /// </summary>
    private static List<string> CultureIdsUsedByLords()
    {
        var xml = File.ReadAllText(Path.Combine(ModuleDataPath, "characters", "lords.xml"));
        return Regex.Matches(xml, "culture\\s*=\\s*\"Culture\\.([A-Za-z_0-9]+)\"")
            .Cast<Match>()
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    [TestMethod]
    public void LordsXml_DeclaresCultures_SoTheGuardBelowIsMeaningful()
    {
        var cultures = CultureIdsUsedByLords();

        // 22 as of 2026-09-06. The floor guards against a regex that silently matches nothing,
        // which would make the coverage assertion below vacuously pass.
        Assert.IsTrue(cultures.Count >= 15,
            $"Expected at least 15 distinct lord cultures in lords.xml, found {cultures.Count}. " +
            "The extraction regex has probably gone stale.");
    }

    [TestMethod]
    public void EveryCultureUsedByALord_IsClassifiedInAlignmentJson()
    {
        var alignments = ShippedAlignments();
        var unclassified = CultureIdsUsedByLords()
            .Where(id => !alignments.ContainsKey(id))
            .ToList();

        Assert.AreEqual(0, unclassified.Count,
            "These cultures seed lords but have no execution/alignment.json entry, so they resolve " +
            "to Neutral and their heroes may marry across the Free/Evil line unchecked: " +
            string.Join(", ", unclassified));
    }

    [TestMethod]
    public void ShippedAlignments_ClassifyBothSidesOfTheReportedPairing()
    {
        var alignments = ShippedAlignments();

        // The exact pairing from issue #542. If either side ever drifts to neutral or drops out,
        // the feature stops blocking the case it was written for.
        Assert.IsTrue(alignments.ContainsKey("gondor"), "gondor must stay classified");
        Assert.IsTrue(alignments.ContainsKey("mistymountainorcs"), "mistymountainorcs must stay classified");
        Assert.IsTrue(StringComparer.OrdinalIgnoreCase.Equals(alignments["gondor"], "free"),
            $"gondor must stay Free, was '{alignments["gondor"]}'");
        Assert.IsTrue(StringComparer.OrdinalIgnoreCase.Equals(alignments["mistymountainorcs"], "evil"),
            $"mistymountainorcs must stay Evil, was '{alignments["mistymountainorcs"]}'");
    }
}
