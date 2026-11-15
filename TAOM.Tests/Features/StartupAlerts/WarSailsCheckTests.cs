using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.StartupAlerts;

namespace TAOM.Tests.Features.StartupAlerts;

/// <summary>
/// The War Sails alert reads the loaded module ids. War Sails ships with the game for DLC owners, so being
/// installed must not trigger it; only being loaded does.
/// </summary>
[TestClass]
public class WarSailsCheckTests
{
    [TestMethod]
    public void IsLoaded_NavalDlcInTheLoadedList_ReturnsTrue()
    {
        Assert.IsTrue(WarSailsCheck.IsLoaded(new[] { "TAOM.Dependencies", "Native", "SandBoxCore", "NavalDLC", "TAOM" }));
    }

    [TestMethod]
    public void IsLoaded_IdInOtherCase_ReturnsTrue()
    {
        Assert.IsTrue(WarSailsCheck.IsLoaded(new[] { "navaldlc" }));
    }

    [TestMethod]
    public void IsLoaded_TaomReleaseOrderWithoutNavalDlc_ReturnsFalse()
    {
        Assert.IsFalse(WarSailsCheck.IsLoaded(new[]
        {
            "TAOM.Dependencies", "Native", "SandBoxCore", "Sandbox", "StoryMode", "CustomBattle", "BirthAndDeath",
            "TAOM_Map", "LOTRLOME_Armory", "TAOM",
        }));
    }

    [TestMethod]
    public void IsLoaded_IdThatOnlyContainsNavalDlc_ReturnsFalse()
    {
        Assert.IsFalse(WarSailsCheck.IsLoaded(new[] { "NavalDLC.Extras", "MyNavalDLCPatch" }));
    }

    [TestMethod]
    public void IsLoaded_EmptyList_ReturnsFalse()
    {
        Assert.IsFalse(WarSailsCheck.IsLoaded(new string[0]));
    }

    [TestMethod]
    public void WarSailsModuleId_IsTheIdTaomDeclaresIncompatible()
    {
        var subModule = System.Xml.Linq.XDocument.Load(TAOM.Tests.Infrastructure.RepoPaths.RepoPath("Main", "_Module", "SubModule.xml"));
        var incompatible = System.Linq.Enumerable.Select(subModule.Root!.Element("IncompatibleModules")!.Elements("Module"), m => (string)m.Attribute("Id")!);
        CollectionAssert.Contains(System.Linq.Enumerable.ToList(incompatible), WarSailsCheck.WarSailsModuleId);
    }
}
