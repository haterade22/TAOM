using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Dependencies.Foundation;
using TAOM.Features.StartupAlerts;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Infrastructure.Dependencies;

/// <summary>
/// With War Sails ticked the launcher drops TAOM (its IncompatibleModules entry) but still starts TAOM.Dependencies,
/// TAOM_Map and the Armory. TAOM.Dependencies then tells the player why. These pin who gets the notice, and that the
/// ids and texts match the module files they mirror.
/// </summary>
[TestClass]
public class MissingTaomCheckTests
{
    private static readonly string[] Vanilla = { "Native", "SandBoxCore", "Sandbox", "StoryMode", "CustomBattle", "BirthAndDeath" };

    private static string[] With(params string[] ids) => Vanilla.Concat(ids).ToArray();

    [TestMethod]
    public void Evaluate_OwnersWarSailsStart_TaomDroppedNavalDlcLoaded_ReturnsWarSails()
    {
        // rgl_log_11688 (2026-11-15 20:31), line 61: the launcher's list after it dropped TAOM.
        Assert.AreEqual(MissingTaomReason.WarSails,
            MissingTaomCheck.Evaluate(With("TAOM.Dependencies", "TAOM_Map", "LOTRLOME_Armory", "DualWield", "NavalDLC")));
    }

    [TestMethod]
    public void Evaluate_TaomLoaded_ReturnsNone_EvenWithWarSails()
    {
        // TAOM's own startup alert covers War Sails loaded alongside TAOM (BLSE, a hand-edited list).
        Assert.AreEqual(MissingTaomReason.None,
            MissingTaomCheck.Evaluate(With("TAOM.Dependencies", "TAOM_Map", "LOTRLOME_Armory", "TAOM", "NavalDLC")));
    }

    [TestMethod]
    public void Evaluate_NormalTaomStart_ReturnsNone()
    {
        Assert.AreEqual(MissingTaomReason.None,
            MissingTaomCheck.Evaluate(With("TAOM.Dependencies", "TAOM_Map", "LOTRLOME_Armory", "TAOM")));
    }

    [TestMethod]
    public void Evaluate_TaomNotTickedWithoutWarSails_ReturnsNotLoaded()
    {
        Assert.AreEqual(MissingTaomReason.NotLoaded,
            MissingTaomCheck.Evaluate(With("TAOM.Dependencies", "TAOM_Map", "LOTRLOME_Armory")));
    }

    [TestMethod]
    public void Evaluate_OnlyTheMapWithoutDependencies_StillCounts()
    {
        Assert.AreEqual(MissingTaomReason.NotLoaded, MissingTaomCheck.Evaluate(With("TAOM_Map")));
    }

    [TestMethod]
    public void Evaluate_DependenciesWithoutTheMap_ReturnsNone_ItServesOtherPlaysetsThroughTheStubs()
    {
        Assert.AreEqual(MissingTaomReason.None, MissingTaomCheck.Evaluate(With("TAOM.Dependencies", "NavalDLC")));
        Assert.AreEqual(MissingTaomReason.None, MissingTaomCheck.Evaluate(With("TAOM.Dependencies", "Bannerlord.Harmony")));
    }

    [TestMethod]
    public void Evaluate_NoTaomModuleAtAll_ReturnsNone()
    {
        Assert.AreEqual(MissingTaomReason.None, MissingTaomCheck.Evaluate(With("NavalDLC")));
    }

    [TestMethod]
    public void Evaluate_IdsInOtherCase_AreMatched()
    {
        Assert.AreEqual(MissingTaomReason.None, MissingTaomCheck.Evaluate(new[] { "taom.dependencies", "taom" }));
        Assert.AreEqual(MissingTaomReason.WarSails, MissingTaomCheck.Evaluate(new[] { "taom_map", "navaldlc" }));
    }

    [TestMethod]
    public void Evaluate_IdsThatOnlyContainTaom_DoNotCountAsTaom()
    {
        Assert.AreEqual(MissingTaomReason.NotLoaded, MissingTaomCheck.Evaluate(new[] { "TAOM_Map", "TAOM_CheatPanel", "TAOM.Extras" }));
    }

    [TestMethod]
    public void Evaluate_NullOrEmpty_ReturnsNone()
    {
        Assert.AreEqual(MissingTaomReason.None, MissingTaomCheck.Evaluate(null!));
        Assert.AreEqual(MissingTaomReason.None, MissingTaomCheck.Evaluate(new string[0]));
    }

    [TestMethod]
    public void ModuleIds_MatchTheShippedSubModuleFiles()
    {
        Assert.AreEqual(MissingTaomCheck.TaomModuleId, ModuleId("Main", "_Module", "SubModule.xml"));
        Assert.AreEqual(MissingTaomCheck.DependenciesModuleId, ModuleId("Dependencies", "_Module", "SubModule.xml"));
    }

    [TestMethod]
    public void WarSailsModuleId_IsTheOneTaomDeclaresIncompatible_AndTaomsOwnAlertUses()
    {
        Assert.AreEqual(WarSailsCheck.WarSailsModuleId, MissingTaomCheck.WarSailsModuleId);
        var subModule = XDocument.Load(RepoPaths.RepoPath("Main", "_Module", "SubModule.xml"));
        var incompatible = subModule.Root!.Element("IncompatibleModules")!.Elements("Module").Select(m => (string)m.Attribute("Id")!).ToList();
        CollectionAssert.Contains(incompatible, MissingTaomCheck.WarSailsModuleId);
    }

    [TestMethod]
    public void NoticeTexts_HaveNoLanguageRows_TheyShowOnlyWhenTaomsStringsDidNotLoad()
    {
        var source = File.ReadAllText(RepoPaths.RepoPath("Dependencies", "MissingTaomNotice.cs"));
        var rows = XDocument.Load(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "taom_module_strings.xml"))
            .Root!.Elements("string").ToDictionary(e => (string)e.Attribute("id")!, e => (string)e.Attribute("text")!);

        Assert.IsFalse(source.Contains("{=taom_missing_taom"), "the notice texts carry a TAOM key nobody can translate");
        Assert.IsFalse(rows.Keys.Any(k => k.StartsWith("taom_missing_taom")), "a dead row for the missing-TAOM notice");
    }

    private static string ModuleId(params string[] path) =>
        (string)XDocument.Load(RepoPaths.RepoPath(path)).Root!.Element("Id")!.Attribute("value")!;
}
