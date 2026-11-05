using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.FiefManagement;

/// <summary>
/// The one map-menu gate (from the 1.5.x #789 change, without its Fiefs button): F6 and the field camp's
/// "Make camp" query both ask MapMenuGate, which mirrors vanilla MapScreen.OnFrameTick's suppression set
/// (v1.4.8 MapScreen.cs:1355) plus the escape menu and a live menu context. Source pins, comments stripped.
/// </summary>
[TestClass]
public class MapMenuGateWiringTests
{
    private const string GateFile = "Main/Adapters/MapMenuGate.cs";
    private const string F6File = "Main/Features/FiefManagement/Hooks/Patch36_MapScreenF6.cs";

    [TestMethod]
    public void TheMapModalGuardList_IsWrittenOnce()
    {
        // Two copies of this list drifted apart (F6 and the field camp's query). One gate now; every other
        // file delegates. IsMapIncidentActive is the member only the list names.
        var mainDir = RepoPath("Main");
        var holders = Directory.EnumerateFiles(mainDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                        && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .Where(f => StripComments(File.ReadAllText(f)).Contains("IsMapIncidentActive"))
            .Select(f => Path.GetFullPath(f).Substring(Path.GetFullPath(RepoPath()).Length).TrimStart('\\', '/').Replace('\\', '/'))
            .OrderBy(f => f)
            .ToList();

        CollectionAssert.AreEqual(new[] { GateFile }, holders,
            "Only MapMenuGate.cs may name the map's modal flags. Holders: " + string.Join(", ", holders));
    }

    [DataTestMethod]
    [DataRow(F6File)]
    [DataRow("Main/Features/FieldCamp/UI/MapScreenCampMenuActivationQuery.cs")]
    public void TheGateConsumers_DelegateToMapMenuGate(string file)
    {
        StringAssert.Contains(ReadSource(file, stripComments: true), "MapMenuGate.IsMapClearForMenu()");
    }

    [TestMethod]
    public void TheGate_MirrorsVanillasFullSet_PlusTheStaleFlagAndEscapeMenuCases()
    {
        var source = ReadSource(GateFile, stripComments: true);

        foreach (var term in new[]
                 {
                     "ActiveState is MapState", "CurrentMenuContext", "IsInMenu", "IsInBattleSimulation", "IsInArmyManagement",
                     "IsMarriageOfferPopupActive", "IsHeirSelectionPopupActive", "IsMapCheatsActive", "IsMapIncidentActive",
                     "IsOverlayContextMenuEnabled", "IsEncyclopediaOpen", "IsEscapeMenuOpened",
                     "GetMapScreenActionIsEnabledWithReason",
                 })
            StringAssert.Contains(source, term, "MapMenuGate lost a guard: " + term);

        // The helper reads Hero.MainHero and MobileParty.MainParty unguarded, so it must come after the map-state check.
        Assert.IsTrue(source.IndexOf("ActiveState is MapState") < source.IndexOf("GetMapScreenActionIsEnabledWithReason"),
            "The helper must run after the MapState check.");
    }

    [TestMethod]
    public void F6_ChecksTheKeyFirst_AndKeepsTheEnlistedAttachedRefusal()
    {
        var source = ReadSource(F6File, stripComments: true);
        var key = source.IndexOf("IsF6Pressed", System.StringComparison.Ordinal);
        var gate = source.IndexOf("MapMenuGate.IsMapClearForMenu()", System.StringComparison.Ordinal);

        Assert.IsTrue(key >= 0 && gate > key, "F6 must test the key before the gate, so the gate runs only on a press.");
        StringAssert.Contains(source, "EnlistmentState.EnlistedAttached", "F6 must still refuse while enlisted-attached.");
    }
}
