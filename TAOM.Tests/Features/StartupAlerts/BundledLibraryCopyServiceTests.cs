using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.StartupAlerts;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.StartupAlerts;

/// <summary>
/// The startup check for separate copies of the four libraries TAOM.Dependencies provides. A copy
/// counts by its folder on disk, not by its launcher tick: the launcher stores ticks by module Id,
/// so a stub and a Workshop copy of one Id can't be told apart there. TAOM's own alias stubs name
/// TAOM.Dependencies.dll and are never reported.
/// </summary>
[TestClass]
public class BundledLibraryCopyServiceTests
{
    private const string ModulesRoot = @"C:\Game\Modules";
    private const string WorkshopRoot = @"C:\Steam\steamapps\workshop\content\261550";

    private static string RealModule(string id, string name = "Harmony", string version = "v2.4.2.248", string dll = "Bannerlord.Harmony.dll") =>
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<Module>\n" +
        $"  <Id value=\"{id}\" />\n  <Name value=\"{name}\" />\n  <Version value=\"{version}\" />\n" +
        "  <SubModules>\n    <SubModule>\n" +
        $"      <Name value=\"{name}\" />\n      <DLLName value=\"{dll}\" />\n" +
        "      <SubModuleClassType value=\"Bannerlord.Harmony.SubModule\" />\n" +
        "    </SubModule>\n  </SubModules>\n</Module>";

    private static string RepoStub(string id) =>
        File.ReadAllText(RepoPaths.RepoPath("Stubs", id, "_Module", "SubModule.xml"));

    private static IModuleFolderReader Reader(Dictionary<string, string?> modules, Dictionary<string, string?>? workshop = null)
    {
        var reader = Substitute.For<IModuleFolderReader>();
        reader.ListFolders(ModulesRoot).Returns(modules.Keys.Select(k => Path.Combine(ModulesRoot, k)).ToList());
        reader.ListFolders(WorkshopRoot).Returns((workshop ?? new()).Keys.Select(k => Path.Combine(WorkshopRoot, k)).ToList());
        foreach (var pair in modules) reader.ReadSubModuleXml(Path.Combine(ModulesRoot, pair.Key)).Returns(pair.Value);
        foreach (var pair in workshop ?? new()) reader.ReadSubModuleXml(Path.Combine(WorkshopRoot, pair.Key)).Returns(pair.Value);
        return reader;
    }

    // ── Classify ────────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Classify_RealHarmony_ReturnsTheCopyWithNameVersionFolderAndPlace()
    {
        var copy = BundledLibraryCopyService.Classify(@"C:\W\2859188632", RealModule("Bannerlord.Harmony"), inWorkshop: true);

        Assert.IsNotNull(copy);
        Assert.AreEqual("Bannerlord.Harmony", copy!.Id);
        Assert.AreEqual("Harmony", copy.Name);
        Assert.AreEqual("v2.4.2.248", copy.Version);
        Assert.AreEqual(@"C:\W\2859188632", copy.Folder);
        Assert.IsTrue(copy.InWorkshop);
    }

    [TestMethod]
    public void Classify_EachOfTheFourIds_IsReported()
    {
        // Literal list: the four ids TAOM.Dependencies provides (Stubs/<ID>/_Module/SubModule.xml).
        foreach (var id in new[] { "Bannerlord.Harmony", "Bannerlord.ButterLib", "Bannerlord.UIExtenderEx", "Bannerlord.MBOptionScreen" })
            Assert.IsNotNull(BundledLibraryCopyService.Classify(@"C:\M\" + id, RealModule(id), inWorkshop: false), id);
    }

    [TestMethod]
    public void Classify_IdInOtherCase_IsReported()
    {
        Assert.IsNotNull(BundledLibraryCopyService.Classify(@"C:\M\x", RealModule("bannerlord.harmony"), inWorkshop: false));
    }

    [TestMethod]
    public void Classify_EachRepoAliasStub_IsNotReported()
    {
        foreach (var id in new[] { "Bannerlord.Harmony", "Bannerlord.ButterLib", "Bannerlord.UIExtenderEx", "Bannerlord.MBOptionScreen" })
            Assert.IsNull(BundledLibraryCopyService.Classify(@"C:\M\" + id, RepoStub(id), inWorkshop: false), id);
    }

    [TestMethod]
    public void Classify_StubDllNameInOtherCase_IsNotReported()
    {
        Assert.IsNull(BundledLibraryCopyService.Classify(@"C:\M\x", RealModule("Bannerlord.Harmony", dll: "taom.dependencies.DLL"), inWorkshop: false));
    }

    [TestMethod]
    public void Classify_OtherModuleId_IsNotReported()
    {
        Assert.IsNull(BundledLibraryCopyService.Classify(@"C:\M\x", RealModule("Bannerlord.Harmony.Extensions"), inWorkshop: true));
        Assert.IsNull(BundledLibraryCopyService.Classify(@"C:\M\x", RealModule("TAOM.Dependencies"), inWorkshop: false));
    }

    [TestMethod]
    public void Classify_MalformedXml_ReturnsNullWithoutThrowing()
    {
        Assert.IsNull(BundledLibraryCopyService.Classify(@"C:\M\x", "<Module><Id value=\"Bannerlord.Harmony\"", inWorkshop: false));
    }

    [TestMethod]
    public void Classify_NoIdElement_ReturnsNull()
    {
        Assert.IsNull(BundledLibraryCopyService.Classify(@"C:\M\x", "<Module><Name value=\"Harmony\" /></Module>", inWorkshop: false));
    }

    [TestMethod]
    public void Classify_NoNameOrVersion_FallsBackToTheIdAndAnEmptyVersion()
    {
        var copy = BundledLibraryCopyService.Classify(@"C:\M\x", "<Module><Id value=\"Bannerlord.ButterLib\" /></Module>", inWorkshop: false);

        Assert.IsNotNull(copy);
        Assert.AreEqual("Bannerlord.ButterLib", copy!.Name);
        Assert.AreEqual(string.Empty, copy.Version);
    }

    // ── FindCopies ──────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void FindCopies_StubsInModulesAndRealCopiesInWorkshop_ReportsOnlyTheWorkshopCopies()
    {
        var reader = Reader(
            new() { ["Bannerlord.Harmony"] = RepoStub("Bannerlord.Harmony"), ["Native"] = RealModule("Native") },
            new() { ["2859188632"] = RealModule("Bannerlord.Harmony"), ["2859238197"] = RealModule("Bannerlord.MBOptionScreen", "Mod Configuration Menu v5", "v5.13.1") });

        var copies = new BundledLibraryCopyService(reader).FindCopies(ModulesRoot, WorkshopRoot);

        CollectionAssert.AreEqual(new[] { "Bannerlord.Harmony", "Bannerlord.MBOptionScreen" }, copies.Select(c => c.Id).ToArray());
        Assert.IsTrue(copies.All(c => c.InWorkshop));
    }

    [TestMethod]
    public void FindCopies_RealCopyInTheModulesFolder_IsReportedAsNotWorkshop()
    {
        var reader = Reader(new() { ["Bannerlord.ButterLib"] = RealModule("Bannerlord.ButterLib", "ButterLib", "v2.12.0") });

        var copy = new BundledLibraryCopyService(reader).FindCopies(ModulesRoot, WorkshopRoot).Single();

        Assert.AreEqual(Path.Combine(ModulesRoot, "Bannerlord.ButterLib"), copy.Folder);
        Assert.IsFalse(copy.InWorkshop);
    }

    [TestMethod]
    public void FindCopies_OnlyStubs_ReportsNothing()
    {
        var reader = Reader(new() { ["Bannerlord.Harmony"] = RepoStub("Bannerlord.Harmony"), ["Bannerlord.ButterLib"] = RepoStub("Bannerlord.ButterLib") });

        Assert.AreEqual(0, new BundledLibraryCopyService(reader).FindCopies(ModulesRoot, WorkshopRoot).Count);
    }

    [TestMethod]
    public void FindCopies_FolderWithoutSubModuleXml_IsSkipped()
    {
        var reader = Reader(new() { ["Empty"] = null }, new() { ["123"] = null });

        Assert.AreEqual(0, new BundledLibraryCopyService(reader).FindCopies(ModulesRoot, WorkshopRoot).Count);
    }

    [TestMethod]
    public void FindCopies_NoWorkshopFolder_ScansOnlyModules()
    {
        var reader = Reader(new() { ["Bannerlord.Harmony"] = RealModule("Bannerlord.Harmony") });

        var copies = new BundledLibraryCopyService(reader).FindCopies(ModulesRoot, workshopFolder: null);

        Assert.AreEqual(1, copies.Count);
        reader.DidNotReceive().ListFolders(WorkshopRoot);
    }

    [TestMethod]
    public void FindCopies_OneFolderThrows_TheOthersAreStillScanned()
    {
        var reader = Reader(new() { ["Bad"] = "x", ["Bannerlord.Harmony"] = RealModule("Bannerlord.Harmony") });
        reader.ReadSubModuleXml(Path.Combine(ModulesRoot, "Bad")).Returns(_ => throw new IOException("locked"));

        var copies = new BundledLibraryCopyService(reader).FindCopies(ModulesRoot, WorkshopRoot);

        Assert.AreEqual("Bannerlord.Harmony", copies.Single().Id);
    }

    [TestMethod]
    public void FindCopies_ListingARootThrows_TheOtherRootIsStillScanned()
    {
        var reader = Reader(new(), new() { ["2859188632"] = RealModule("Bannerlord.Harmony") });
        reader.ListFolders(ModulesRoot).Returns(_ => throw new UnauthorizedAccessException());

        var copies = new BundledLibraryCopyService(reader).FindCopies(ModulesRoot, WorkshopRoot);

        Assert.AreEqual("Bannerlord.Harmony", copies.Single().Id);
    }

    // ── WorkshopContentFolder ───────────────────────────────────────────────────────────────

    [TestMethod]
    public void WorkshopContentFolder_SteamGameRoot_IsTheLibrarysBannerlordWorkshopFolder()
    {
        Assert.AreEqual(
            @"C:\STEAM\steamapps\workshop\content\261550",
            BundledLibraryCopyService.WorkshopContentFolder(@"C:\STEAM\steamapps\common\Mount & Blade II Bannerlord\"));
    }

    [TestMethod]
    public void WorkshopContentFolder_ForwardSlashesWithoutTrailingSlash_GivesTheSameFolder()
    {
        Assert.AreEqual(
            @"C:\STEAM\steamapps\workshop\content\261550",
            BundledLibraryCopyService.WorkshopContentFolder("C:/STEAM/steamapps/common/Mount & Blade II Bannerlord"));
    }
}
