using System;
using System.Collections.Generic;
using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Composition;
using TAOM.Core.Logging;

namespace TAOM.Tests.Composition;

/// <summary>
/// The engine-facing half of the runner, driven through the overloads that take the runner and the
/// resolver (review of plan 018, lens 4 F2). CampaignGameStarter only stores its two constructor
/// arguments and AddBehavior appends to a list, and BasicGameStarter is parameterless (installed
/// v1.5.3), so the starters are real: campaign behaviors reach a campaign starter in list order, a
/// module whose factory throws adds nothing, a Custom Battle starter gets no campaign behavior, the
/// campaign-start step fails closed for a save owner, and startup faults wait for the main-menu inquiry.
/// </summary>
[TestClass]
public class FeatureModuleHooksTests
{
    private IModLogger _logger = null!;
    private Container _container = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _container = new Container();
    }

    [TestCleanup]
    public void Cleanup() => _container.Dispose();

    private ModuleRunner Runner(params TaomFeatureModule[] modules) => new(modules, () => _logger);

    [TestMethod]
    public void CampaignStart_AddsEveryModulesBehaviors_InListOrder()
    {
        var starter = new CampaignGameStarter(null, null);

        FeatureModuleHooks.AddGameStartContent(Runner(new ProbeModule("A", "A1", "A2"), new ProbeModule("B", "B1")),
            _container, starter);

        CollectionAssert.AreEqual(new[] { "A1", "A2", "B1" },
            starter.CampaignBehaviors.Cast<ProbeBehavior>().Select(b => b.Name).ToArray());
    }

    [TestMethod]
    public void CampaignStart_AModuleWhoseFactoryThrows_AddsNothing_AndTheNextModuleStillAdds()
    {
        var starter = new CampaignGameStarter(null, null);
        var broken = new ProbeModule("A", "A1", "A2") { ThrowOn = "A2" };
        var runner = Runner(broken, new ProbeModule("B", "B1"));

        FeatureModuleHooks.AddGameStartContent(runner, _container, starter);

        CollectionAssert.AreEqual(new[] { "B1" },
            starter.CampaignBehaviors.Cast<ProbeBehavior>().Select(b => b.Name).ToArray(),
            "A1 was built before A2 threw, and must not be handed to the engine.");
        Assert.IsTrue(runner.IsFaulted("A"));
    }

    [TestMethod]
    public void CampaignStart_ASaveOwnerWhoseFactoryThrows_FailsClosed()
    {
        var starter = new CampaignGameStarter(null, null);
        var owner = new ProbeModule("A", "A1") { ThrowOn = "A1", Owns = true };

        Assert.ThrowsException<InvalidOperationException>(
            () => FeatureModuleHooks.AddGameStartContent(Runner(owner, new ProbeModule("B", "B1")), _container, starter));
        Assert.AreEqual(0, starter.CampaignBehaviors.Count);
    }

    [TestMethod]
    public void CustomBattleStart_NeverBuildsACampaignBehavior()
    {
        var owner = new ProbeModule("A", "A1") { ThrowOn = "A1", Owns = true };
        var runner = Runner(owner);

        FeatureModuleHooks.AddGameStartContent(runner, _container, new BasicGameStarter());

        Assert.IsFalse(runner.IsFaulted("A"), "A Custom Battle starter must not build campaign behaviors.");
    }

    [TestMethod]
    public void MissionStart_HandsEachBehaviorToTheKernelAdder_AndIsolatesAThrowingModule()
    {
        var added = new List<MissionBehavior>();
        var broken = new ProbeModule("A") { MissionThrows = true };
        var runner = Runner(broken, new ProbeModule("B") { MissionBehaviorCount = 1 });

        FeatureModuleHooks.AddMissionBehaviors(runner, _container, null!, added.Add);

        Assert.AreEqual(1, added.Count);
        Assert.IsInstanceOfType(added[0], typeof(ProbeMissionBehavior));
        Assert.IsTrue(runner.IsFaulted("A"));
    }

    [TestMethod]
    public void AProcessLoadFault_IsHeld_ThenShownInTheMainMenuInquiry()
    {
        var runner = Runner(new ProbeModule("A") { PhaseThrows = true });
        var shown = new List<string>();
        Action<InquiryData, bool, bool> capture = (data, _, _) => shown.Add(data.Text);
        InformationManager.OnShowInquiry += capture;
        try
        {
            FeatureModuleHooks.RunPhase(runner, _container, ApplyPhase.ProcessLoad, _ => true);
            Assert.AreEqual(0, shown.Count, "Nothing receives a notice in OnSubModuleLoad.");

            FeatureModuleHooks.RunPhase(runner, _container, ApplyPhase.MainMenu, _ => true);
        }
        finally
        {
            InformationManager.OnShowInquiry -= capture;
        }

        Assert.AreEqual(1, shown.Count);
        StringAssert.Contains(shown[0], "A (ProcessLoad)");
    }

    [TestMethod]
    public void NoRunnerOrNoResolver_IsANoOp()
    {
        var starter = new CampaignGameStarter(null, null);

        FeatureModuleHooks.AddGameStartContent(null, _container, starter);
        FeatureModuleHooks.AddGameStartContent(Runner(new ProbeModule("A", "A1")), null, starter);

        Assert.AreEqual(0, starter.CampaignBehaviors.Count);
    }

    private sealed class ProbeBehavior : CampaignBehaviorBase
    {
        internal ProbeBehavior(string name) : base(name) => Name = name;

        internal string Name { get; }

        public override void RegisterEvents() { }

        public override void SyncData(IDataStore dataStore) { }
    }

    private sealed class ProbeMissionBehavior : MissionLogic
    {
    }

    private sealed class ProbeModule : TaomFeatureModule
    {
        private readonly string _id;
        private readonly string[] _behaviorNames;

        internal ProbeModule(string id, params string[] behaviorNames)
        {
            _id = id;
            _behaviorNames = behaviorNames;
        }

        internal string? ThrowOn;
        internal bool Owns;
        internal bool PhaseThrows;
        internal bool MissionThrows;
        internal int MissionBehaviorCount;

        public override string Id => _id;

        public override bool OwnsSaveData => Owns;

        public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors =>
            _behaviorNames.Select(name => CampaignBehaviorDecl.Of(_ =>
                name == ThrowOn ? throw new InvalidOperationException(name + " broke") : new ProbeBehavior(name))).ToList();

        public override IReadOnlyList<MissionBehaviorDecl> MissionBehaviors => MissionThrows
            ? new[] { MissionBehaviorDecl.Of<ProbeMissionBehavior>((_, _) => throw new InvalidOperationException("mission broke")) }
            : Enumerable.Range(0, MissionBehaviorCount)
                .Select(_ => MissionBehaviorDecl.Of((_, _) => new ProbeMissionBehavior())).ToList();

        public override void OnPhase(ApplyPhase phase, IResolver resolver)
        {
            if (PhaseThrows) throw new InvalidOperationException(_id + " broke in " + phase);
        }
    }
}
