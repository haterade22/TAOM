using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CreatureSiegeRole;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.CreatureSiegeRole;

/// <summary>
/// The REAL <c>siege/creature_siege_role.json</c> the module ships, read the way the game reads it (the synthetic cases are in
/// CreatureSiegeRoleConfigProviderTests). It ships a PROVISIONAL multiplier of 2.0: the in-game measurement of one troll's hit
/// on a gate (RG-G) sets the final value, and the plan's target is that a few hill trolls break a 15,000 HP gate in 60 to 90
/// seconds. It is deliberately not the 4 another mod uses.
/// </summary>
[TestClass]
public class ShippedCreatureSiegeRoleConfigTests
{
    private static string ModuleData => RepoPaths.RepoPath("Main", "_Module", "ModuleData");

    private static string ShippedFile => Path.Combine(ModuleData, "siege", "creature_siege_role.json");

    [TestMethod]
    public void TheShippedFile_Exists_InTheSiegeFolderTheProviderReads()
    {
        Assert.IsTrue(File.Exists(ShippedFile),
            "Main/_Module/ModuleData/siege/creature_siege_role.json is missing, so every install falls back to the default with a warning");
    }

    [TestMethod]
    public void TheShippedFile_IsOneObject_WithTheNumberKey_AtTheProvisionalTwo()
    {
        // The literal key is pinned here independently of the provider's own constant.
        var root = JToken.Parse(File.ReadAllText(ShippedFile));

        Assert.IsInstanceOfType(root, typeof(JObject));
        var json = (JObject)root;
        CollectionAssert.AreEqual(new[] { "gateDamageMultiplier" }, json.Properties().Select(p => p.Name).ToArray());
        Assert.IsTrue(json["gateDamageMultiplier"]!.Type is JTokenType.Float or JTokenType.Integer);
        Assert.AreEqual(2.0, json["gateDamageMultiplier"]!.Value<double>());
    }

    [TestMethod]
    public void TheShippedFile_LoadsThroughTheProvider_AsTwo_WithNoWarning()
    {
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(ModuleData);
        var logger = Substitute.For<IModLogger>();

        var provider = new CreatureSiegeRoleConfigProvider(paths, logger);

        Assert.AreEqual(2f, provider.GateDamageMultiplier);
        logger.DidNotReceive().LogWarning(Arg.Any<string>());
        logger.DidNotReceive().LogError(Arg.Any<string>());
    }
}
