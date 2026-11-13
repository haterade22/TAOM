using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Adapters;

/// <summary>
/// #929: the one-button popup test (main party, no encounter, map not at a menu, no inquiry open) is
/// written once, in <c>MapPopupGate</c>. No other Main file may re-spell it, and the three former
/// copies must delegate. The drift scan recognises the spelling the three copies used (an
/// <c>IsAnyInquiryActive()</c> call plus a <c>PlayerEncounter.Current ==/!= null</c> test); an
/// <c>is null</c> or <c>PlayerEncounter.IsActive</c> form would pass it unseen.
/// </summary>
[TestClass]
public class MapPopupGateTests
{
    private const string GateFile = "MapPopupGate.cs";

    private static readonly Regex EncounterTest =
        new Regex(@"PlayerEncounter\.Current\s*[!=]=\s*null");

    [TestMethod]
    public void NoFileOutsideTheGate_RespellsTheFourConditionTest()
    {
        var offenders = RepoPaths.CSharpFiles()
            .Where(p => Path.GetFileName(p) != GateFile)
            .Where(p => RepoPaths.MentionsOutsideComments(p, "IsAnyInquiryActive()"))
            .Where(p => EncounterTest.IsMatch(RepoPaths.StripComments(RepoPaths.Text(p))))
            .Select(p => Path.GetFileName(p))
            .ToList();

        Assert.AreEqual(0, offenders.Count,
            "These files re-spell the popup test instead of calling MapPopupGate.IsMapFree: "
            + string.Join(", ", offenders));
    }

    [DataTestMethod]
    [DataRow("Main/Adapters/HunterPartyAdapter.cs")]
    [DataRow("Main/Features/WarChronicle/Events/ChroniclePresenter.cs")]
    [DataRow("Main/Adapters/HiddenHostsHintAdapter.cs")]
    public void TheThreeCallers_DelegateToTheGate(string path)
    {
        var src = RepoPaths.ReadSource(path, stripComments: true);
        StringAssert.Contains(src, "MapPopupGate.IsMapFree");
    }

    [TestMethod]
    public void TheGate_ChecksMainPartyBeforeTheEncounter()
    {
        var src = RepoPaths.ReadSource("Main/Adapters/MapPopupGate.cs", stripComments: true);
        var party = src.IndexOf("MainParty", System.StringComparison.Ordinal);
        var encounter = src.IndexOf("PlayerEncounter.Current", System.StringComparison.Ordinal);
        Assert.IsTrue(party >= 0 && encounter > party,
            "PlayerEncounter.Current throws with no campaign, so the MainParty check must come first");
    }
}

/// <summary>
/// The gate outside a campaign: the test process has no campaign, so a gate that read
/// <c>PlayerEncounter.Current</c> first, or joined its terms with <c>||</c>, throws here instead of answering false.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class MapPopupGateNoCampaignTests
{
    [TestMethod]
    public void IsMapFree_NoCampaign_ReturnsFalse() =>
        Assert.IsFalse(TAOM.Adapters.MapPopupGate.IsMapFree());
}
