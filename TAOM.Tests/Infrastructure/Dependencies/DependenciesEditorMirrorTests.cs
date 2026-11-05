using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Infrastructure.Dependencies;

/// <summary>
/// Pins the TAOM.Dependencies editor mirror (1.5.x 9985f2db3, #1047). The v1.4.8 Modding Kit looks for each
/// submodule's DLL in the module's own <c>bin\Win64_Shipping_wEditor</c> folder, then in its own bin
/// (<c>Module.LoadSubModules</c>), which holds no Harmony, MCMv5 or UIExtenderEx. With the folder empty
/// TAOM.dll cannot load in the Kit, so no Kit job (plans/121's banner atlas re-import among them) can open TAOM.
/// </summary>
[TestClass]
public class DependenciesEditorMirrorTests
{
    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\.."));

    private static XElement Target(string name) =>
        XDocument.Load(Path.Combine(RepoRoot, @"Dependencies\TAOM.Dependencies.csproj")).Descendants()
            .Single(t => t.Name.LocalName == "Target" && (string?)t.Attribute("Name") == name);

    [TestMethod]
    public void EditorMirror_CopiesTheAssembledClientBinIntoTheEditorBin()
    {
        var target = Target("MirrorWin64ShippingClientToEditor");

        Assert.AreEqual("PostBuildCopyToModules", (string?)target.Attribute("AfterTargets"),
            "The client bin is only populated once Bannerlord.BuildResources has copied into it.");
        var copy = target.Descendants("Copy").Single();
        StringAssert.Contains((string?)copy.Attribute("DestinationFolder") ?? "", "Win64_Shipping_wEditor",
            "Without it the Modding Kit finds no Harmony, MCMv5 or UIExtenderEx and TAOM fails to load there.");
        var source = target.Descendants("ItemGroup").Elements()
            .Single(i => $"@({i.Name.LocalName})" == (string?)copy.Attribute("SourceFiles"));
        StringAssert.Contains((string?)source.Attribute("Include") ?? "", @"bin\Win64_Shipping_Client\*.*");
    }

    [TestMethod]
    public void EditorMirror_IsSkippedByTheAgentBuild()
    {
        // Agents build with -p:DisableModuleCopy=true -p:ModuleId= and must never write into the game install.
        var condition = (string?)Target("MirrorWin64ShippingClientToEditor").Attribute("Condition") ?? "";

        StringAssert.Contains(condition, "$(DisableModuleCopy) != 'true'");
        StringAssert.Contains(condition, "$(ModuleId) != ''");
    }
}
