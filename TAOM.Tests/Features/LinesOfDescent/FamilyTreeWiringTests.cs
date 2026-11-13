using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Features.LinesOfDescent;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LinesOfDescent;

/// <summary>Wiring pins for Lines of Descent (#905): the module line, its empty footprint, the container graph and the text keys.</summary>
[TestClass]
public class FamilyTreeWiringTests
{
    [TestMethod]
    public void FeatureModules_ListTheModuleOnce_AfterTheOathsLine()
    {
        var all = FeatureModules.All.ToList();
        Assert.AreEqual(1, all.OfType<FamilyTreeModule>().Count());
        var oaths = all.FindIndex(m => m.Id == "OathsOfService");
        Assert.AreEqual(oaths + 1, all.FindIndex(m => m is FamilyTreeModule));
    }

    [TestMethod]
    public void TheModule_IsNamedFamilyTree_NotParked_SavesNothing_AndDeclaresNoBehaviors()
    {
        var module = new FamilyTreeModule();
        Assert.AreEqual("LinesOfDescent", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData);
        Assert.AreEqual(0, module.CampaignBehaviors.Count);
        Assert.AreEqual(0, module.PatchCategories.Count, "no Harmony: the feature is two mixins, two prefab insertions and a popup");
        Assert.AreEqual(0, module.GameModels.Count);
        Assert.AreEqual(0, module.MissionBehaviors.Count);
    }

    [TestMethod]
    public void TheModulesServices_ResolveFromARealContainer()
    {
        using var container = new Container();
        new FamilyTreeModule().RegisterServices(container);
        Assert.IsInstanceOfType(container.Resolve<IHeroFamilyAdapter>(), typeof(HeroFamilyAdapter));
        Assert.IsInstanceOfType(container.Resolve<IFamilyTreeService>(), typeof(FamilyTreeService));
        Assert.AreSame(container.Resolve<IFamilyTreeService>(), container.Resolve<IFamilyTreeService>());
    }

    [TestMethod]
    public void EveryTextKey_IsDefinedOnce_AcrossMainAndClient()
    {
        var keys = new List<string>();
        var pattern = new Regex(@"\{=(taom_lod_\w+)\}");
        foreach (var file in RepoPaths.CSharpFiles("Main", "Client"))
            keys.AddRange(pattern.Matches(RepoPaths.Text(file)).Cast<Match>().Select(m => m.Groups[1].Value));
        Assert.IsTrue(keys.Count >= 18, "the text file defines the whole table, found " + keys.Count);
        var dupes = keys.GroupBy(k => k).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.AreEqual(0, dupes.Count, "keys defined twice: " + string.Join(", ", dupes));
    }

    [TestMethod]
    public void TheServicesStayEngineFree()
    {
        foreach (var file in new[] { "FamilyTreeService.cs", "RelationPathFinder.cs", "FamilyTreeTypes.cs", "IFamilyTreeService.cs" })
        {
            var src = RepoPaths.ReadSource("Main/Features/LinesOfDescent/" + file, stripComments: true);
            StringAssert.DoesNotMatch(src, new Regex(@"using TaleWorlds"), file + " must not touch the engine (ADR-007)");
        }
    }
}
