using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Infrastructure.Dependencies;

/// <summary>
/// Bannerlord.BuildResources' CopyBinariesWindows deletes every .dll in
/// Modules\TAOM.Dependencies\bin\Win64_Shipping_Client (Basic.targets, CopyFolder Clean="true") and copies back
/// only the project's build output; the vendored third-party DLLs return only through CopyModule from
/// Dependencies\_Module\bin\Win64_Shipping_Client. On 2026-11-15 a deploy from a checkout without them (a sparse
/// checkout skipping *.dll) left the module unloadable ("Cannot load: Microsoft.Bcl.HashCode.dll").
/// FailOnMissingBundledDlls stops that deploy before the clean. These tests pin its wiring; the behaviour was
/// proven by building against a scratch game folder (PR body).
/// </summary>
[TestClass]
public class BundledDllDeployGuardTests
{
    private const string GuardName = "FailOnMissingBundledDlls";

    private static XElement Guard()
    {
        var csproj = XDocument.Load(RepoPaths.RepoPath("Dependencies", "TAOM.Dependencies.csproj"));
        var target = csproj.Descendants("Target").SingleOrDefault(t => (string?)t.Attribute("Name") == GuardName);
        Assert.IsNotNull(target, $"Dependencies/TAOM.Dependencies.csproj has no {GuardName} target.");
        return target!;
    }

    [TestMethod]
    public void Guard_RunsBeforePostBuildCopyToModules()
    {
        // Act
        var beforeTargets = (string?)Guard().Attribute("BeforeTargets");

        // Assert: PostBuildCopyToModules is the first PostBuildEvent target in Basic.targets, so a target
        // scheduled before it runs before CopyBinariesWindows' clean, whichever way that one is reached.
        Assert.AreEqual("PostBuildCopyToModules", beforeTargets);
    }

    [TestMethod]
    public void Guard_Condition_IsCopyBinariesWindowsOwnNotDisableModuleCopy()
    {
        // Act
        var condition = (string?)Guard().Attribute("Condition") ?? "";

        // Assert: CopyBinariesWindows cleans whenever ModuleId is set and the game folder exists, even with
        // DisableModuleCopy=true, so a guard gated on DisableModuleCopy would let that clean through.
        Assert.AreEqual("'$(ModuleId)' != '' AND Exists('$(GameFolder)')", condition);
    }

    [TestMethod]
    public void Guard_Query_FindsTheBundledDllsTheModuleLoads()
    {
        // Arrange
        var peek = Guard().Descendants("XmlPeek").SingleOrDefault();
        Assert.IsNotNull(peek, $"{GuardName} reads no SubModule.xml.");
        Assert.AreEqual(@"$(ProjectDir)_Module\SubModule.xml", (string?)peek!.Attribute("XmlInputPath"));
        var query = (string)peek.Attribute("Query")!;
        var subModule = XDocument.Load(RepoPaths.RepoPath("Dependencies", "_Module", "SubModule.xml"));

        // Act
        var names = ((IEnumerable<object>)subModule.XPathEvaluate(query))
            .OfType<XAttribute>().Select(a => a.Value).ToList();

        // Assert: the vendored DLL whose absence the engine reported, and the module's own DLL.
        CollectionAssert.Contains(names, "Microsoft.Bcl.HashCode.dll");
        CollectionAssert.Contains(names, "Bannerlord.ButterLib.dll");
        CollectionAssert.Contains(names, "TAOM.Dependencies.dll");
    }

    [TestMethod]
    public void Guard_MissingTest_AcceptsTheStagingFolderOrTheBuildOutput()
    {
        // Arrange
        var guard = Guard();
        var missing = guard.Descendants("_TaomDepsMissingDll").SingleOrDefault();
        Assert.IsNotNull(missing, $"{GuardName} collects no missing DLLs.");

        // Act
        var condition = (string)missing!.Attribute("Condition")!;
        var error = guard.Descendants("Error").SingleOrDefault();

        // Assert: vendored DLLs are staged under _Module, NuGet and project output sits in TargetDir.
        StringAssert.Contains(condition, @"!Exists('$(ProjectDir)_Module\bin\Win64_Shipping_Client\%(Identity)')");
        StringAssert.Contains(condition, "!Exists('$(TargetDir)%(Identity)')");
        Assert.IsNotNull(error, $"{GuardName} raises no error.");
        Assert.AreEqual("'@(_TaomDepsMissingDll)' != ''", (string?)error!.Attribute("Condition"));
    }
}
