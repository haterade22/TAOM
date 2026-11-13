using System;
using System.IO;
using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Execution;
using TAOM.Features.SwornAnew;
using TAOM.Features.SwornAnew.Hooks;
using TAOM.Features.SwornAnew.Models;
using TAOM.Features.UncapturableHeroes;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.SwornAnew;

/// <summary>
/// Wiring guards for Sworn Anew (#869). Each piece fails silently when unwired: drop the module line and nothing runs; drop the
/// model declaration and a prisoner never defects; drop the statics handshake and the patch has no service; leave a dependency
/// unregistered and the first conversation throws inside a catch. The service and the behavior hold no state; the behavior is built
/// for each campaign because a campaign registers its events on the instance.
/// </summary>
[TestClass]
public class SwornAnewWiringTests
{
    private static Container BuildContainer()
    {
        var container = new Container();
        new SwornAnewModule().RegisterServices(container);
        // What the module takes from other features, which IoC.Configure registers for every game.
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IAlignmentService>());
        container.RegisterInstance(Substitute.For<IUncapturableRegistry>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        container.RegisterInstance(Substitute.For<IDedicatedServerProvider>());
        return container;
    }

    [TestMethod]
    public void FeatureModules_ListTheSwornAnewModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<SwornAnewModule>().Count());
        var src = RepoPaths.ReadSource("Main/Composition/FeatureModules.cs", stripComments: true);
        Assert.IsTrue(src.Contains("new Features.SwornAnew.SwornAnewModule()"));
    }

    [TestMethod]
    public void TheSingleOwnerFiles_AreNotEditedForThisFeature()
    {
        Assert.IsFalse(RepoPaths.ReadSource("Main/IoC.cs", stripComments: true).Contains("SwornAnew"));
        Assert.IsFalse(RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true).Contains("SwornAnew"));
    }

    [TestMethod]
    public void TheModule_IsNotParked_OwnsNoSaveData_AndAppliesOneProcessLoadCategory()
    {
        var module = new SwornAnewModule();

        Assert.AreEqual("SwornAnew", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData, "nothing is persisted");
        Assert.AreEqual(0, module.MissionBehaviors.Count);
        Assert.AreEqual(1, module.PatchCategories.Count);
        Assert.AreEqual("Patch116_SwornAnew", module.PatchCategories[0].Category);
        Assert.AreEqual(ApplyPhase.ProcessLoad, module.PatchCategories[0].Phase);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheModule_DeclaresTheDefectionModelForTheCampaign()
    {
        var models = new SwornAnewModule().GameModels.ToList();

        Assert.AreEqual(1, models.Count);
        Assert.AreEqual(typeof(DefaultDefectionModel), models[0].SlotType);
        Assert.AreEqual(typeof(TaomDefectionModel), models[0].ModelType);
        Assert.AreEqual(ModelTarget.Campaign, models[0].Target);
    }

    [TestMethod]
    public void TheModule_AddsOneCampaignBehavior_BuiltWithNew()
    {
        var module = new SwornAnewModule();
        var src = RepoPaths.ReadSource("Main/Features/SwornAnew/SwornAnewModule.cs", stripComments: true);

        Assert.AreEqual(1, module.CampaignBehaviors.Count);
        Assert.AreEqual(typeof(SwornAnewBehavior), module.CampaignBehaviors[0].BehaviorType);
        Assert.IsTrue(src.Contains("new SwornAnewBehavior("), "the behavior must be built per campaign");
        Assert.IsFalse(src.Contains("Resolve<SwornAnewBehavior>"));
    }

    [TestMethod]
    public void EveryServiceTheModuleDeclares_ResolvesFromItsOwnRegistrations()
    {
        using var container = BuildContainer();

        Assert.IsNotNull(container.Resolve<ISwornAnewSettingsProvider>());
        Assert.IsNotNull(container.Resolve<ISwornAnewAdapter>());
        Assert.IsNotNull(container.Resolve<SwornAnewService>());
    }

    [TestMethod]
    public void TheService_IsOneSharedInstance_ForTheModelThePatchAndTheBehavior()
    {
        using var container = BuildContainer();

        Assert.AreSame(container.Resolve<SwornAnewService>(), container.Resolve<SwornAnewService>());
    }

    [TestMethod]
    public void TheModule_DoesNotRegisterWhatOtherFeaturesOwn()
    {
        var src = RepoPaths.ReadSource("Main/Features/SwornAnew/SwornAnewModule.cs", stripComments: true);

        Assert.IsFalse(src.Contains("IUncapturableRegistry,"), "UncapturableHeroesIoC owns the registry");
        Assert.IsFalse(src.Contains("IAlignmentService,"));
        Assert.IsFalse(src.Contains("ICoopSessionProvider,"));
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheBehaviorDeclaration_BuildsAFreshBehaviorForEachCampaign()
    {
        using var container = BuildContainer();
        var decl = new SwornAnewModule().CampaignBehaviors[0];

        var first = decl.Create(container);
        var second = decl.Create(container);

        Assert.IsInstanceOfType(first, typeof(SwornAnewBehavior));
        Assert.AreNotSame(first, second, "a campaign registers its behaviors' events on the instance");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheModelDeclaration_BuildsTheModelOverTheSharedService()
    {
        using var container = BuildContainer();

        var model = new SwornAnewModule().GameModels[0].Create(container);

        Assert.IsInstanceOfType(model, typeof(TaomDefectionModel));
    }

    [TestMethod]
    public void InitializeStatics_HandsThePatchItsServiceAdapterAndLogger()
    {
        using var container = BuildContainer();

        new SwornAnewModule().InitializeStatics(container);

        // The postfix reads its statics; with them set, a vanilla-true answer reaches the service (a null adapter read keeps it true).
        var result = true;
        Patch116_PrisonerDefectionLine_Postfix.Postfix(ref result);
        Assert.IsTrue(result);
        var src = RepoPaths.ReadSource("Main/Features/SwornAnew/SwornAnewModule.cs", stripComments: true);
        StringAssert.Contains(src, "Patch116_PrisonerDefectionLine_Postfix.Initialize(");
    }

    [TestMethod]
    public void InitializeStatics_ThePostfixTakesTheLineAwayFromAPrisonerTheServiceRefuses()
    {
        // The whole chain through the container: the module's registrations, the statics handshake, the postfix, the service.
        using var container = BuildContainer();
        container.RegisterInstance(Substitute.For<IAlignmentService>(), IfAlreadyRegistered.Replace);
        container.Resolve<IAlignmentService>().ResolveSide(NSubstitute.Arg.Any<string>(), NSubstitute.Arg.Any<string>())
            .Returns(call => call.ArgAt<string>(0) == "k_evil" ? FactionSide.Evil : FactionSide.Free);
        var settings = Substitute.For<ISwornAnewSettingsProvider>();
        settings.Enabled.Returns(true);
        container.RegisterInstance(settings, IfAlreadyRegistered.Replace);
        var adapter = Substitute.For<ISwornAnewAdapter>();
        adapter.ReadConversationPartner().Returns(DefectionFactsFixture.EligiblePrisoner(
            lord: DefectionFactsFixture.Free, player: DefectionFactsFixture.Evil));
        container.RegisterInstance(adapter, IfAlreadyRegistered.Replace);

        new SwornAnewModule().InitializeStatics(container);
        var result = true;
        Patch116_PrisonerDefectionLine_Postfix.Postfix(ref result);

        Assert.IsFalse(result, "a Free lord is never offered a place in an Evil realm");
    }

    [TestMethod]
    public void NoOtherPatchTargetsTheDefectionBehavior()
    {
        // Patch116 is the only Harmony seam on LordDefectionCampaignBehavior; a second would have to be reconciled with its verdict.
        var files = RepoPaths.CSharpFiles()
            .Where(f => RepoPaths.StripComments(RepoPaths.Text(f)).Contains("LordDefectionCampaignBehavior"))
            .Select(Path.GetFileName)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        CollectionAssert.AreEqual(new[] { "Patch116_PrisonerDefectionLine_Postfix.cs" }, files);
    }

    [TestMethod]
    public void SwornAnew_ContainsExactlyOneHarmonyPatchClass()
    {
        var files = Directory.GetFiles(RepoPaths.RepoPath("Main", "Features", "SwornAnew"), "*.cs", SearchOption.AllDirectories);

        var patches = files.Where(f => RepoPaths.MentionsOutsideComments(f, "[HarmonyPatchCategory(")).Select(Path.GetFileName).ToList();

        CollectionAssert.AreEqual(new[] { "Patch116_PrisonerDefectionLine_Postfix.cs" }, patches);
    }

    [TestMethod]
    public void TheAdapter_SidesTheLordByTheirOwnClan_NeverTheirRealm()
    {
        // #1114: the lord's own people decide, as Kingdom Allegiance decides (#1111). Sided by the realm, an orc lord serving Gondor
        // read Free, so a Free player could turn them and an Evil player could not; a Free lord in a Neutral realm could swear to Mordor.
        var src = RepoPaths.ReadSource("Main/Adapters/SwornAnewAdapter.cs", stripComments: true);

        StringAssert.Contains(src, "LordSide: FactionSides.OfOwnClan(hero)");
        Assert.IsFalse(src.Contains("FactionSides.OfHero("), "the lord's side never comes from their realm");
        StringAssert.Contains(src, "PlayerSide: FactionSides.OfPlayer()");
    }
}
