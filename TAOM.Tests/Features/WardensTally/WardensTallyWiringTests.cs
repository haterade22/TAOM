using System;
using System.IO;
using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.WarChronicle;
using TAOM.Features.WarChronicle.Effects;
using TAOM.Features.WardensTally;
using TAOM.Features.WardensTally.Hooks;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.WardensTally;

/// <summary>
/// Wiring guards for Warden's Tally (#903). Each piece fails silently when unwired: drop the module line and nothing runs; drop the
/// behavior declaration and the listener never exists; leave a dependency unregistered and the first dungeon menu throws inside the
/// hook's catch. The odds and the boost come from War Chronicle's registrations, which the module never repeats.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class WardensTallyWiringTests
{
    private static Container BuildContainer()
    {
        var container = new Container();
        new WardensTallyModule().RegisterServices(container);
        // What the module takes from other features: War Chronicle's registrations and the kernel's.
        WarChronicleIoC.RegisterWarChronicleFeature(container);
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        container.RegisterInstance(Substitute.For<IPlayerControlQuery>());
        return container;
    }

    [TestMethod]
    public void FeatureModules_ListTheWardensTallyModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<WardensTallyModule>().Count());
        var src = RepoPaths.ReadSource("Main/Composition/FeatureModules.cs", stripComments: true);
        Assert.IsTrue(src.Contains("new Features.WardensTally.WardensTallyModule()"));
    }

    [TestMethod]
    public void TheSingleOwnerFiles_AreNotEditedForThisFeature()
    {
        Assert.IsFalse(RepoPaths.ReadSource("Main/IoC.cs", stripComments: true).Contains("WardensTally"));
        Assert.IsFalse(RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true).Contains("WardensTally"));
    }

    [TestMethod]
    public void TheModule_IsNotParked_OwnsNoSaveData_AndDeclaresOneBehaviorAndNoPatch()
    {
        var module = new WardensTallyModule();

        Assert.AreEqual("WardensTally", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData, "nothing is persisted");
        Assert.AreEqual(1, module.CampaignBehaviors.Count);
        Assert.AreEqual(typeof(WardensTallyMenuBehavior), module.CampaignBehaviors[0].BehaviorType);
        Assert.AreEqual(0, module.PatchCategories.Count, "the menu is read from the menu-opened event, not patched");
        Assert.AreEqual(0, module.GameModels.Count);
        Assert.AreEqual(0, module.MissionBehaviors.Count);
    }

    [TestMethod]
    public void EveryServiceTheModuleDeclares_ResolvesWithWarChroniclesRegistrations()
    {
        using var container = BuildContainer();

        Assert.IsNotNull(container.Resolve<WardensTallySettingsProvider>());
        Assert.IsNotNull(container.Resolve<WardensTallyService>());
        Assert.IsNotNull(container.Resolve<WardensTallyMenuService>());
    }

    [TestMethod]
    public void TheBehaviorDeclaration_BuildsAFreshBehaviorOverTheContainersMenuService()
    {
        using var container = BuildContainer();
        var decl = new WardensTallyModule().CampaignBehaviors[0];

        var first = (WardensTallyMenuBehavior)decl.Create(container);
        var second = (WardensTallyMenuBehavior)decl.Create(container);

        Assert.AreNotSame(first, second, "a campaign registers its behaviors' events on the instance");
        Assert.AreSame(container.Resolve<WardensTallyMenuService>(), first.Service);
        Assert.AreSame(first.Service, second.Service);
    }

    [TestMethod]
    public void TheModule_DoesNotRegisterWhatWarChronicleOwns()
    {
        var src = RepoPaths.ReadSource("Main/Features/WardensTally/WardensTallyModule.cs", stripComments: true);

        Assert.IsFalse(src.Contains("IPrisonerEscapeAdapter,"));
        Assert.IsFalse(src.Contains("Register<WarEscapeService>"));
        Assert.IsFalse(src.Contains("IWarEffectService,"));
        Assert.IsFalse(src.Contains("ICoopSessionProvider,"));
    }

    [TestMethod]
    public void NoHarmonyPatchIsDeclaredForThisFeature()
    {
        var files = RepoPaths.CSharpFiles()
            .Where(f => f.Replace('\\', '/').Contains("/Features/WardensTally/"))
            .Where(f => RepoPaths.StripComments(File.ReadAllText(f)).Contains("HarmonyPatch"))
            .Select(Path.GetFileName)
            .ToList();

        CollectionAssert.AreEqual(new string[0], files);
    }

    [TestMethod]
    public void TheEntryPoint_StaysThin()
    {
        var lines = File.ReadAllLines(RepoPaths.RepoPath("Main", "Features", "WardensTally", "Hooks", "WardensTallyMenuBehavior.cs"));

        Assert.IsTrue(lines.Length < 150, "ADR-002: entry points stay under 150 lines, was " + lines.Length);
    }

    [TestMethod]
    public void NoServiceFileNamesAnEngineType_ButTheTextsAndTheMenuSeam()
    {
        // ADR-007: the pure service and its view hold no TaleWorlds type; only the texts (TextObject) name the engine.
        foreach (var name in new[] { "WardensTallyService.cs", "WardensTallyView.cs" })
        {
            var src = RepoPaths.ReadSource("Main/Features/WardensTally/" + name, stripComments: true);
            Assert.IsFalse(src.Contains("TaleWorlds"), name + " names an engine type");
        }
    }
}
