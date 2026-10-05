using System;
using System.Linq;
using System.Reflection;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Domain;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.Arena;
using TAOM.Features.ArmourAcquisition;
using TAOM.Features.CoopInterop;
using TAOM.Features.TournamentRewards;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.TournamentRewards;

/// <summary>
/// Wiring regression guard for Tournament Rewards. One TournamentRewardsService is shared by every entry point:
/// Patch96's bet postfix reaches it through IoC, the Arena TournamentService reads it for every winner's renown
/// and influence, TournamentJoinService writes the skill choice that TournamentSkillAwardService consumes, and the
/// behavior writes the hero count and clears both at each session launch. A transient registration would drop the
/// hero bonus and every skill award with every unit test green, because each test builds its own instance by
/// hand. Arena's hand-registered TournamentService also needs this module's service: drop the module and
/// IoC.Resolve of ITournamentService throws at campaign start.
/// </summary>
[TestClass]
public class TournamentRewardsWiringTests
{
    private const string BehaviorPath = "Main/Features/TournamentRewards/TournamentRewardsBehavior.cs";

    // Everything the module and Arena's TournamentService need that neither registers: the kernel's services, the
    // enlistment feature's XP adapter, the co-op feature's server probe and the armour gate.
    private static Container BuildContainer()
    {
        var container = new Container();
        container.RegisterInstance(Substitute.For<IPathService>());
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IHeroSkillXpAdapter>());
        container.RegisterInstance(Substitute.For<IDedicatedServerProvider>());
        container.RegisterInstance(Substitute.For<IRaceManager>());
        container.RegisterInstance(Substitute.For<IArmourGateService>());
        ArenaIoC.RegisterArenaFeature(container);
        new TournamentRewardsModule().RegisterServices(container);
        return container;
    }

    [TestMethod]
    public void FeatureModules_ListTheTournamentRewardsModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<TournamentRewardsModule>().Count(),
            "TournamentRewardsModule must be listed exactly once in Main/Composition/FeatureModules.cs, or Patch96 "
            + "and the behavior are never applied (or applied twice) and Arena's TournamentService cannot resolve.");
    }

    [TestMethod]
    public void Module_DeclaresPatch96AtGameInit()
    {
        var categories = new TournamentRewardsModule().PatchCategories;

        Assert.AreEqual(1, categories.Count);
        Assert.AreEqual("Patch96_TournamentRewards", categories[0].Category);
        Assert.AreEqual(ApplyPhase.GameInit, categories[0].Phase,
            "Patch96 targets campaign and SandBox types, which are not loaded earlier");
    }

    [TestMethod]
    public void Module_DeclaresTheRewardsBehavior()
    {
        var decls = new TournamentRewardsModule().CampaignBehaviors;

        Assert.AreEqual(1, decls.Count);
        Assert.AreEqual(typeof(TournamentRewardsBehavior), decls[0].BehaviorType);
    }

    [TestMethod]
    public void ModuleAndArena_RegisterTheServiceGraph_AndShareOneRewardsService()
    {
        using var container = BuildContainer();

        Assert.IsNotNull(container.Resolve<ITournamentService>(), "Arena's service needs the module's rewards service");
        Assert.IsNotNull(container.Resolve<TournamentJoinService>());
        Assert.IsNotNull(container.Resolve<TournamentSkillAwardService>());
        Assert.AreSame(container.Resolve<TournamentRewardsService>(), container.Resolve<TournamentRewardsService>(),
            "the hero count and the skill choice only reach their readers through one shared instance");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void BehaviorDecl_BuildsTheBehaviorOverTheSharedServices()
    {
        // Building a CampaignBehaviorBase runs engine code.
        using var container = BuildContainer();
        var decl = new TournamentRewardsModule().CampaignBehaviors.Single();

        var behavior = decl.Create(container);

        Assert.IsInstanceOfType(behavior, typeof(TournamentRewardsBehavior));
        Assert.AreSame(container.Resolve<TournamentRewardsService>(), FieldOf(behavior, "_rewards"));
        Assert.AreSame(container.Resolve<TournamentSkillAwardService>(), FieldOf(behavior, "_award"));
    }

    [TestMethod]
    public void Behavior_ResetsTheServiceAtEverySessionLaunch()
    {
        // csharp-architecture.md: a singleton holding per-campaign state is reset from OnSessionLaunched, which a
        // brand-new campaign reaches although it never calls SyncData.
        var src = RepoPaths.ReadSource(BehaviorPath, stripComments: true);

        StringAssert.Contains(src, "CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched)");
        StringAssert.Contains(src, "_rewards.ResetForNewSession()");
    }

    [TestMethod]
    public void Behavior_NotesTheHeroCountFirst_FromTheTournamentFinishedListener()
    {
        // The model reads the count noted here when vanilla's TournamentFinished handler asks for the winner's
        // renown and influence, so the count is noted before anything else the handler does, and the handler is a
        // listener of the same public event (the order is explained on the behavior).
        var src = RepoPaths.ReadSource(BehaviorPath, stripComments: true);

        StringAssert.Contains(src, "CampaignEvents.TournamentFinished.AddNonSerializedListener(this, OnTournamentFinished)");
        var noted = src.IndexOf("_rewards.NoteTournamentFinished(", StringComparison.Ordinal);
        var awarded = src.IndexOf("_award.OnTournamentFinished(", StringComparison.Ordinal);
        Assert.IsTrue(noted >= 0, "the behavior no longer notes the finished tournament's hero count");
        Assert.IsTrue(noted < awarded, "the hero count must be noted before the skill award runs");
    }

    private static object? FieldOf(object instance, string name) =>
        instance.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(instance);
}
