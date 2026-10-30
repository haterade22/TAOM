using System.Linq;
using System.Text.RegularExpressions;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.SeasonedHosts;
using TAOM.Features.SeasonedHosts.Hooks;
using TAOM.Features.SeasonedHosts.Models;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.SeasonedHosts;

/// <summary>Wiring guard for Seasoned Hosts (#890): the module line, its three model slots, its one patch category and its empty behavior and save footprint.</summary>
[TestClass]
public class SeasonedHostsWiringTests
{
    private const string ModelsDir = "Main/Features/SeasonedHosts/Models/";

    [TestMethod]
    public void FeatureModules_ListTheSeasonedHostsModuleOnce() =>
        Assert.AreEqual(1, FeatureModules.All.OfType<SeasonedHostsModule>().Count());

    [TestMethod]
    public void TheModule_IsNamedSeasonedHosts_AndIsNotParked_AndOwnsNoSaveData()
    {
        var module = new SeasonedHostsModule();

        Assert.AreEqual("SeasonedHosts", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData, "the XP lives in vanilla's rosters and hero developers; this module persists nothing");
    }

    [TestMethod]
    public void TheModule_DeclaresNoBehavior() =>
        Assert.AreEqual(0, new SeasonedHostsModule().CampaignBehaviors.Count);

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheModule_DeclaresThreeCampaignModels_EachExtendingItsDefaultModel()
    {
        var models = new SeasonedHostsModule().GameModels;

        Assert.AreEqual(3, models.Count);
        Assert.IsTrue(models.All(m => m.Target == ModelTarget.Campaign), "heroes and parties exist only in a campaign");
        AssertSlot(models, typeof(GenericXpModel), typeof(TaomGenericXpModel), typeof(DefaultGenericXpModel));
        AssertSlot(models, typeof(PartyTrainingModel), typeof(TaomPartyTrainingModel), typeof(DefaultPartyTrainingModel));
        AssertSlot(models, typeof(CharacterDevelopmentModel), typeof(TaomCharacterDevelopmentModel), typeof(DefaultCharacterDevelopmentModel));
    }

    private static void AssertSlot(System.Collections.Generic.IReadOnlyList<GameModelDecl> models, System.Type slot, System.Type model, System.Type baseType)
    {
        var decl = models.Single(m => m.SlotType == slot);
        Assert.AreEqual(model, decl.ModelType);
        Assert.AreEqual(baseType, decl.ModelType.BaseType, "extend the model SandBox installs, so every other rule keeps vanilla's answer");
    }

    [TestMethod]
    public void TheModule_DeclaresOnePatchCategory_AppliedAtGameInit()
    {
        var category = new SeasonedHostsModule().PatchCategories.Single();

        Assert.AreEqual("Patch118_SeasonedHostsStewardXp", category.Category);
        Assert.AreEqual(ApplyPhase.GameInit, category.Phase);
        Assert.AreEqual(SeasonedHostsModule.PatchCategory, category.Category);
    }

    [DataTestMethod]
    [DataRow("TaomGenericXpModel.cs", 1)]
    [DataRow("TaomPartyTrainingModel.cs", 2)]
    [DataRow("TaomCharacterDevelopmentModel.cs", 1)]
    public void NoOverrideBody_InTheModels_HoldsAnIf(string file, int overrides)
    {
        var src = ReadSource(ModelsDir + file, stripComments: true);

        Assert.IsFalse(Regex.IsMatch(src, @"\b(if|switch|foreach|for|while)\s*\("), "an override body must hold no branch or loop");
        Assert.IsFalse(Regex.IsMatch(src, @"\s\?\s"), "no conditional expression in a model");
        Assert.AreEqual(overrides, Regex.Matches(src, @"\bpublic override\b").Count);
    }

    [TestMethod]
    public void TheTrainingModel_AddsOneFactorToBase_AndNeverSetsTheBase()
    {
        var src = ReadSource(ModelsDir + "TaomPartyTrainingModel.cs", stripComments: true);

        Assert.AreEqual(1, Regex.Matches(src, @"\.AddFactor\(").Count, "one factor helper serves both members");
        StringAssert.Contains(src, "base.GetEffectiveDailyExperience(");
        StringAssert.Contains(src, "base.CalculateXpGainFromBattles(");
        Assert.IsFalse(src.Contains(".Add("), "a bonus is a factor, never a flat add");
    }

    [TestMethod]
    public void TheDevelopmentModel_OverridesOnlyTheLearningRate_AndRaisesItsMinimum()
    {
        var src = ReadSource(ModelsDir + "TaomCharacterDevelopmentModel.cs", stripComments: true);

        StringAssert.Contains(src, "override ExplainedNumber CalculateLearningRate(");
        StringAssert.Contains(src, "base.CalculateLearningRate(");
        StringAssert.Contains(src, ".LimitMin(_service.LearningRateFloor(");
        Assert.IsFalse(src.Contains("LimitMax"), "the floor must not cap the rate");
    }

    [TestMethod]
    public void TheSkillXpModel_MultipliesBase_NeverReplacesIt()
    {
        var src = ReadSource(ModelsDir + "TaomGenericXpModel.cs", stripComments: true);

        StringAssert.Contains(src, "base.GetXpMultiplier(hero) * _service.SkillXpMultiplier(");
    }

    [TestMethod]
    public void TheStewardPatch_StaysUnderTheEntryPointLineBudget() =>
        Assert.IsTrue(ReadSource("Main/Features/SeasonedHosts/Hooks/Patch118_ProjectStewardXp.cs", stripComments: true).Split('\n').Length < 150,
            "ADR-002: an entry point stays under 150 lines");

    private static Container BuildContainer()
    {
        var container = new Container();
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        container.RegisterInstance(Substitute.For<IPlayerControlQuery>());
        container.RegisterInstance(Substitute.For<IModLogger>());
        new SeasonedHostsModule().RegisterServices(container);
        return container;
    }

    [TestMethod]
    public void RegisterServices_ResolvesTheServiceTheAdapterAndTheSettingsProvider()
    {
        using var container = BuildContainer();

        var errors = container.Validate(typeof(SeasonedHostsService), typeof(TAOM.Adapters.ISeasonedHostsAdapter), typeof(ISeasonedHostsSettingsProvider));

        Assert.AreEqual(0, errors.Length, string.Join("; ", errors.Select(e => e.Value.Message)));
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheModelDeclarations_BuildTheXpAndTrainingModelsOverTheSharedService()
    {
        using var container = BuildContainer();
        // The training model also takes the career passives (TroopTraining shares the one PartyTrainingModel slot); the
        // career module registers that service in the real container.
        container.RegisterInstance(Substitute.For<TAOM.Features.CareerSystem.ICareerPassiveService>());
        var models = new SeasonedHostsModule().GameModels;

        Assert.IsInstanceOfType(models.Single(m => m.SlotType == typeof(GenericXpModel)).Create(container), typeof(TaomGenericXpModel));
        Assert.IsInstanceOfType(models.Single(m => m.SlotType == typeof(PartyTrainingModel)).Create(container), typeof(TaomPartyTrainingModel));
    }

    /// <summary>Vanilla's <c>DefaultCharacterDevelopmentModel</c> constructor reads campaign data, so without a campaign the factory resolves the service and then stops inside the engine base constructor.</summary>
    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheDevelopmentModelDeclaration_ResolvesTheServiceAndReachesTheModelConstructor()
    {
        using var container = BuildContainer();
        var decl = new SeasonedHostsModule().GameModels.Single(m => m.SlotType == typeof(CharacterDevelopmentModel));

        var thrown = Assert.ThrowsException<System.NullReferenceException>(() => decl.Create(container));

        StringAssert.Contains(thrown.StackTrace, "TaomCharacterDevelopmentModel..ctor");
    }

    [TestMethod]
    public void InitializeStatics_HandsThePatchItsServiceAndLogger()
    {
        using var container = BuildContainer();
        var settings = Substitute.For<ISeasonedHostsSettingsProvider>();
        settings.NoStewardXp.Returns(true);
        container.RegisterInstance(settings, IfAlreadyRegistered.Replace);

        new SeasonedHostsModule().InitializeStatics(container);

        Assert.IsFalse(Patch118_ProjectStewardXp.Prefix());
    }
}
