using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.SiegeForces;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.SiegeForces;

/// <summary>
/// The REAL <c>siege/siege_forces.json</c> the module ships, read the way the game reads it (the synthetic cases are in
/// SiegeForcesConfigProviderTests). It ships true: oversized troops start unticked, and a later flip reaches existing
/// installs because this is JSON, not an MCM value that json2 would persist.
/// </summary>
[TestClass]
public class ShippedSiegeForcesConfigTests
{
    private static string ModuleData => RepoPaths.RepoPath("Main", "_Module", "ModuleData");

    private static string ShippedFile => Path.Combine(ModuleData, "siege", "siege_forces.json");

    [TestMethod]
    public void TheShippedFile_Exists_InTheSiegeFolderTheProviderReads()
    {
        Assert.IsTrue(File.Exists(ShippedFile), "Main/_Module/ModuleData/siege/siege_forces.json is missing, so every install falls back to the default with a warning");
    }

    [TestMethod]
    public void TheShippedFile_IsOneObject_WithTheBooleanKey_SetToTrue()
    {
        // The literal key is pinned here independently of the provider's own constant.
        var root = JToken.Parse(File.ReadAllText(ShippedFile));

        Assert.IsInstanceOfType(root, typeof(JObject));
        var json = (JObject)root;
        CollectionAssert.AreEqual(new[] { "startOversizedUnticked" }, json.Properties().Select(p => p.Name).ToArray());
        Assert.AreEqual(JTokenType.Boolean, json["startOversizedUnticked"]!.Type);
        Assert.AreEqual(true, json["startOversizedUnticked"]!.Value<bool>());
    }

    [TestMethod]
    public void TheShippedFile_LoadsThroughTheProvider_AsTrue_WithNoWarning()
    {
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(ModuleData);
        var logger = Substitute.For<IModLogger>();

        var provider = new SiegeForcesConfigProvider(paths, logger);

        Assert.IsTrue(provider.StartOversizedUnticked);
        logger.DidNotReceive().LogWarning(Arg.Any<string>());
        logger.DidNotReceive().LogError(Arg.Any<string>());
    }
}
