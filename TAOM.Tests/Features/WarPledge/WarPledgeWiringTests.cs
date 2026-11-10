using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Diplomacy;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.Execution;
using TAOM.Features.WarChronicle.Effects;
using TAOM.Features.WarChronicle.Ledger;
using TAOM.Features.WarChronicle.Rally;
using TAOM.Features.WarOfTheRingMomentum;
using TAOM.Features.WarPledge;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.WarPledge;

/// <summary>
/// #1113 wiring. The pledge store's reset lives in <c>WarPledgeBehavior</c>'s constructor, so it holds only
/// while the module builds a fresh behavior inside the declaration's factory, once per campaign start. A cached
/// or shared instance would run the constructor once per process and carry one campaign's pledges into the next
/// with every behavior test green, so the pin is the exact shape and the cheapest regressions are rows it must
/// reject. The second half pins that the container hands the one store to the alignment and diplomacy
/// services: without it a pledge is saved but changes no side and no war.
/// </summary>
[TestClass]
public class WarPledgeWiringTests
{
    private const string ModulePath = "Main/Features/WarPledge/WarPledgeModule.cs";

    private static readonly Regex InlinePerCampaign =
        new Regex(@"CampaignBehaviorDecl\.Of\(\s*r\s*=>\s*new\s+WarPledgeBehavior\(");

    private static readonly Regex AnyConstruction = new Regex(@"new\s+WarPledgeBehavior\(");

    private static bool IsBuiltInlinePerCampaign(string source) =>
        InlinePerCampaign.Matches(source).Count == 1 && AnyConstruction.Matches(source).Count == 1;

    [TestMethod]
    public void FeatureModules_ListTheModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<WarPledgeModule>().Count(),
            "WarPledgeModule must be listed exactly once in Main/Composition/FeatureModules.cs");
    }

    [TestMethod]
    public void Module_OwnsSaveData_SoAFaultFailsClosed() => Assert.IsTrue(new WarPledgeModule().OwnsSaveData);

    [TestMethod]
    public void IoC_DoesNotRegisterTheFeatureByHand()
    {
        var src = RepoPaths.ReadSource("Main/IoC.cs", stripComments: true);

        Assert.IsFalse(src.Contains("RegisterWarPledgeFeature"),
            "Main/IoC.cs registers the feature by hand AND through its module: every service gets a second default registration");
    }

    [TestMethod]
    public void Module_BuildsTheBehaviorInlineAsTheFactoryBody()
    {
        var src = RepoPaths.ReadSource(ModulePath, stripComments: true);

        Assert.IsTrue(IsBuiltInlinePerCampaign(src),
            "WarPledgeModule must build WarPledgeBehavior with new as the CampaignBehaviorDecl factory's body, exactly "
            + "once: its constructor is the per-campaign reset of the pledge store.");
    }

    [TestMethod]
    public void ProductionSource_NoOtherFileConstructsOrRegistersTheBehavior()
    {
        var offenders = RepoPaths.CSharpFiles()
            .Where(path => !IsFile(path, "WarPledgeModule.cs") && !IsFile(path, "WarPledgeBehavior.cs"))
            .Where(path => RepoPaths.MentionsOutsideComments(path, "WarPledgeBehavior", wholeWord: true))
            .ToList();

        Assert.AreEqual(0, offenders.Count,
            "Only WarPledgeModule may construct WarPledgeBehavior; elsewhere could hand the campaign a shared instance: "
            + string.Join(", ", offenders));
    }

    [DataTestMethod]
    [DataRow("CampaignBehaviorDecl.Of(r => _cached ??= new WarPledgeBehavior(a, b)),")]
    [DataRow("private static readonly WarPledgeBehavior Shared = new WarPledgeBehavior(a, b); CampaignBehaviorDecl.Of(r => Shared),")]
    [DataRow("CampaignBehaviorDecl.Of(r => Make()), static WarPledgeBehavior Make() => _c ??= new WarPledgeBehavior(a, b);")]
    [DataRow("CampaignBehaviorDecl.Of(r => new WarPledgeBehavior(a, b)), var x = new WarPledgeBehavior(a, b);")]
    [DataRow("CampaignBehaviorDecl.Of(r => r.Resolve<WarPledgeBehavior>()),")]
    public void Predicate_RejectsACachedOrSharedConstruction(string mutant)
    {
        Assert.IsFalse(IsBuiltInlinePerCampaign(mutant), mutant);
    }

    [TestMethod]
    public void Predicate_AcceptsTheInlineConstructionAcrossLines()
    {
        Assert.IsTrue(IsBuiltInlinePerCampaign("CampaignBehaviorDecl.Of(r =>\n    new WarPledgeBehavior(\n a, b)),"));
    }

    // The production lines the container test below copies: a factory lambda there would not pass the store.
    [TestMethod]
    public void HandWiredFeatures_RegisterBothReadersByType_SoTheContainerPassesTheStore()
    {
        StringAssert.Contains(
            RepoPaths.ReadSource("Main/Features/Execution/ExecutionIoC.cs", stripComments: true),
            "container.Register<IAlignmentService, AlignmentService>(Reuse.Singleton);");
        StringAssert.Contains(
            RepoPaths.ReadSource("Main/Features/Diplomacy/DiplomacyIoC.cs", stripComments: true),
            "container.Register<IDiplomacyService, DiplomacyService>(Reuse.Singleton);");
    }

    [TestMethod]
    public void Container_HandsTheModulesStoreToTheAlignmentAndDiplomacyServices()
    {
        using var container = ReadersContainer();
        new WarPledgeModule().RegisterServices(container);

        var store = container.Resolve<WarPledgeStore>();
        var alignment = container.Resolve<IAlignmentService>();
        var diplomacy = container.Resolve<IDiplomacyService>();
        Assert.AreEqual(FactionSide.Neutral, alignment.GetKingdomSide("cardolan"), "precondition");
        store.Pledge("cardolan", FactionSide.Free);

        Assert.AreSame(store, container.Resolve<WarPledgeStore>(), "one store for the process");
        Assert.AreEqual(FactionSide.Free, alignment.GetKingdomSide("cardolan"));
        Assert.AreEqual(AllianceTier.Hostile, diplomacy.GetRelationshipTier("cardolan", "empire_s"));
    }

    // The store is an optional argument, so a container without the module still builds both readers.
    [TestMethod]
    public void Container_WithoutTheModule_StillBuildsBothReaders()
    {
        using var container = ReadersContainer();

        Assert.AreEqual(FactionSide.Neutral, container.Resolve<IAlignmentService>().GetKingdomSide("cardolan"));
        Assert.AreEqual(AllianceTier.Neutral, container.Resolve<IDiplomacyService>().GetRelationshipTier("cardolan", "empire_s"));
    }

    [TestCategory("RequiresGame")]
    [TestMethod]
    public void Module_RegistersTheServiceGraph_AndTheBehaviorResolves()
    {
        using var container = ReadersContainer();
        container.RegisterInstance(Substitute.For<IPathService>());
        container.RegisterInstance(Substitute.For<IOutcomeLog>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        container.RegisterInstance(Substitute.For<IPlayerControlQuery>());
        container.RegisterInstance(Substitute.For<IMomentumQueryService>());
        container.RegisterInstance(Substitute.For<IMomentumSettingsProvider>());
        container.RegisterInstance(Substitute.For<IWarOfTheRingService>());
        container.RegisterInstance(Substitute.For<IKingdomWarSnapshotAdapter>());
        container.RegisterInstance(Substitute.For<IWarEffectService>());
        container.Register<WarBaselineService>(Reuse.Singleton);
        container.Register<RallyTierStore>(Reuse.Singleton);
        container.Register<WarLedgerService>(Reuse.Singleton);
        var module = new WarPledgeModule();

        module.RegisterServices(container);

        var decl = module.CampaignBehaviors.Single();
        Assert.AreEqual(typeof(WarPledgeBehavior), decl.BehaviorType);
        Assert.IsInstanceOfType(decl.Create(container), typeof(WarPledgeBehavior));
        Assert.AreNotSame(decl.Create(container), decl.Create(container), "a fresh behavior per campaign start");
        Assert.AreSame(container.Resolve<WarPledgeService>(), container.Resolve<WarPledgeService>());
        Assert.IsInstanceOfType(container.Resolve<IWarPledgeWorldAdapter>(), typeof(WarPledgeWorldAdapter));
        Assert.IsInstanceOfType(container.Resolve<IWarPledgeSettings>(), typeof(WarPledgeSettingsProvider));
        Assert.IsInstanceOfType(container.Resolve<IWarPledgeConfigProvider>(), typeof(WarPledgeConfigProvider));
    }

    // Both readers registered the way ExecutionIoC and DiplomacyIoC register them, over fake tables.
    private static Container ReadersContainer()
    {
        var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IAllianceAdapter>());

        var alignmentConfig = Substitute.For<IAlignmentConfigProvider>();
        alignmentConfig.LoadAlignments().Returns(new Dictionary<string, string>
        {
            { "cardolan", "neutral" }, { "empire_s", "evil" },
        });
        container.RegisterInstance(alignmentConfig);
        var diplomacyConfig = Substitute.For<IDiplomacyConfigProvider>();
        diplomacyConfig.LoadConfig().Returns(new DiplomacyConfig { Relationships = new List<KingdomRelationship>() });
        container.RegisterInstance(diplomacyConfig);

        container.Register<IAlignmentService, AlignmentService>(Reuse.Singleton);
        container.Register<IDiplomacyService, DiplomacyService>(Reuse.Singleton);
        return container;
    }

    private static bool IsFile(string path, string fileName) =>
        string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase);
}
