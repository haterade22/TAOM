using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Migration;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.Execution;

/// <summary>
/// Pins any TAOM copy of a party tuple prefab to the installed vanilla file apart from the two edits the 1.5.x line makes there
/// (#1031). The 1.4.x line ships no copy: both were identical to v1.4.8 and were deleted (#1051), so vanilla loads. A port that
/// brings a copy back (for example #902's TaomPrisonerBanners include) is compared element by element from then on.
/// </summary>
[TestClass]
public class PartyTupleVanillaParityTests
{
    [DataTestMethod]
    [TestCategory("BindingVerification")]
    [DataRow("PartyTroopTuple.xml")]
    [DataRow("PartyTroopTupleLeft.xml")]
    public void EachTupleCopy_MatchesVanilla_ApartFromTaomsTwoEdits(string file)
    {
        var taomPath = RepoPath("Main", "_Module", "GUI", "PreFabs", "Party", file);
        if (!File.Exists(taomPath))
            return; // no TAOM copy: the vanilla file loads, which is the parity this test exists for
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game install unavailable: " + string.Join("; ", GameAssemblies.Diagnostics));
        var vanillaPath = Path.Combine(GameAssemblies.GameDir, "Modules", "SandBox", "GUI", "Prefabs", "Party", file);
        if (!File.Exists(vanillaPath))
            Assert.Inconclusive("vanilla prefab not found at " + vanillaPath);

        CollectionAssert.AreEqual(Flatten(vanillaPath), Flatten(taomPath), file);
    }

    // Every element except the two 1.5.x edits: the #902 include and the zero transition durations (Shadowfax Screens).
    private static List<string> Flatten(string path)
    {
        var doc = new XmlDocument();
        doc.Load(path);
        return doc.SelectNodes("//*[not(self::TaomPrisonerBanners)]")!.Cast<XmlElement>()
            .Select(e => e.Name + " " + string.Join(" ", e.Attributes.Cast<XmlAttribute>()
                .Where(a => !(e.Name == "VisualDefinition" && a.Name == "TransitionDuration"))
                .OrderBy(a => a.Name).Select(a => a.Name + "=" + a.Value)))
            .ToList();
    }
}
