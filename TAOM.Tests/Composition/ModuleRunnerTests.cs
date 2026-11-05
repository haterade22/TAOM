using System;
using System.Collections.Generic;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

// DryIoc and NSubstitute both export an `Arg` type (the DischargeConsequenceServiceTests.cs
// precedent). Every `Arg` here is an NSubstitute argument matcher.
using Arg = NSubstitute.Arg;
using TAOM.Composition;
using TAOM.Core.Logging;

namespace TAOM.Tests.Composition;

/// <summary>
/// Pins the feature-module runner's isolation policy: list order, parked modules registered but
/// otherwise skipped, a throwing module logged and skipped for the rest of the session while the
/// next module still runs, save-owning modules failing closed in the steps that decide whether their
/// SyncData runs, categories applied only in their own phase through the kernel's guarded applier,
/// and one fault summary per report point.
/// </summary>
[TestClass]
public class ModuleRunnerTests
{
    private IModLogger _logger = null!;
    private List<string> _calls = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _calls = new List<string>();
    }

    private ModuleRunner Runner(params TaomFeatureModule[] modules) => new(modules, () => _logger);

    private RecordingModule Module(string id) => new(id, _calls);

    [TestMethod]
    public void RegisterServices_VisitsEveryModuleInListOrder_ParkedIncluded()
    {
        var parked = Module("P");
        parked.Parked = true;
        using var container = new Container();

        Runner(Module("A"), parked, Module("B")).RegisterServices(container);

        CollectionAssert.AreEqual(new[] { "A:register", "P:register", "B:register" }, _calls);
    }

    [TestMethod]
    public void InitializeStatics_SkipsParkedModules()
    {
        var parked = Module("P");
        parked.Parked = true;
        using var container = new Container();

        Runner(Module("A"), parked, Module("B")).InitializeStatics(container);

        CollectionAssert.AreEqual(new[] { "A:statics", "B:statics" }, _calls);
    }

    [TestMethod]
    public void AModuleThatThrows_IsLoggedAndFaulted_AndTheNextModuleStillRuns()
    {
        var a = Module("A");
        a.ThrowIn = "register";
        using var container = new Container();
        var runner = Runner(a, Module("B"));

        runner.RegisterServices(container);

        CollectionAssert.AreEqual(new[] { "A:register", "B:register" }, _calls);
        _logger.Received(1).LogError(Arg.Is<string>(s =>
            s.Contains("[Module] A failed in service registration") && s.Contains("A broke in register")));
        Assert.IsTrue(runner.IsFaulted("A"));
        Assert.IsFalse(runner.IsFaulted("B"));
    }

    [TestMethod]
    public void AFaultedModule_IsSkippedInEveryLaterStep()
    {
        var a = Module("A");
        a.ThrowIn = "register";
        using var container = new Container();
        var runner = Runner(a, Module("B"));
        runner.RegisterServices(container);
        _calls.Clear();

        runner.InitializeStatics(container);
        runner.RunPhase(ApplyPhase.GameInit, _ => true, container);
        runner.RunCampaignStart(m => _calls.Add(m.Id + ":step"));

        CollectionAssert.AreEqual(new[] { "B:statics", "B:phase:GameInit", "B:step" }, _calls);
    }

    [TestMethod]
    public void ASaveOwningModule_FailsClosed_InAFailClosedStep()
    {
        var a = Module("A");
        a.OwnsSave = true;
        a.ThrowIn = "statics";
        using var container = new Container();
        var runner = Runner(a, Module("B"));

        var ex = Assert.ThrowsException<InvalidOperationException>(() => runner.InitializeStatics(container));

        StringAssert.Contains(ex.Message, "A broke in statics");
        CollectionAssert.AreEqual(new[] { "A:statics" }, _calls, "Nothing after a fail-closed throw may run.");
        _logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("[Module] A failed in static initialisation")));
    }

    [TestMethod]
    public void ASaveOwningModule_IsIsolated_InAFailOpenStep()
    {
        var a = Module("A");
        a.OwnsSave = true;
        a.ThrowIn = "phase";
        using var container = new Container();
        var runner = Runner(a, Module("B"));

        runner.RunPhase(ApplyPhase.MainMenu, _ => true, container);

        CollectionAssert.AreEqual(new[] { "A:phase:MainMenu", "B:phase:MainMenu" }, _calls);
        Assert.IsTrue(runner.IsFaulted("A"));
    }

    // Review of plan 018 (Codex P2, lens 2, lens 5 F1): the faulted-module skip ran before the
    // fail-closed rethrow, so a save owner that faulted in a fail-open step was silently left out of
    // the next campaign, whose next save then dropped its data.
    [TestMethod]
    public void ASaveOwningModule_ThatFaultedInAFailOpenStep_FailsClosedAtTheNextCampaignStart()
    {
        var a = Module("A");
        a.OwnsSave = true;
        a.ThrowIn = "phase";
        using var container = new Container();
        var runner = Runner(a, Module("B"));
        runner.RunPhase(ApplyPhase.MainMenu, _ => true, container);
        _calls.Clear();

        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => runner.RunCampaignStart(m => _calls.Add(m.Id + ":step")));

        StringAssert.Contains(ex.Message, "A");
        StringAssert.Contains(ex.Message, "campaign start");
        Assert.AreEqual(0, _calls.Count, "Nothing after a fail-closed throw may run.");
    }

    [TestMethod]
    public void ASaveOwningModule_ThatFailedCampaignStart_FailsClosedAgainOnTheRetry()
    {
        var a = Module("A");
        a.OwnsSave = true;
        var runner = Runner(a, Module("B"));
        Assert.ThrowsException<InvalidOperationException>(() => runner.RunCampaignStart(
            m => { if (m.Id == "A") throw new InvalidOperationException("A broke"); }));
        _calls.Clear();

        Assert.ThrowsException<InvalidOperationException>(
            () => runner.RunCampaignStart(m => _calls.Add(m.Id + ":step")));
        Assert.AreEqual(0, _calls.Count);
    }

    // Lens 5 F3: a parked module's behavior never runs, so its SyncData never runs either; failing
    // closed over it would stop TAOM loading for a feature that is switched off.
    [TestMethod]
    public void AParkedSaveOwningModule_IsIsolated_InServiceRegistration()
    {
        var parked = Module("P");
        parked.Parked = true;
        parked.OwnsSave = true;
        parked.ThrowIn = "register";
        using var container = new Container();
        var runner = Runner(parked, Module("B"));

        runner.RegisterServices(container);

        CollectionAssert.AreEqual(new[] { "P:register", "B:register" }, _calls);
        Assert.IsTrue(runner.IsFaulted("P"));
    }

    [TestMethod]
    public void RunPhase_AppliesOnlyThatPhasesCategories_InOrder_ThenCallsOnPhase()
    {
        var a = Module("A");
        a.Categories.Add(new PatchCategoryDecl("Cat_Load", ApplyPhase.ProcessLoad));
        a.Categories.Add(new PatchCategoryDecl("Cat_Init1", ApplyPhase.GameInit));
        a.Categories.Add(new PatchCategoryDecl("Cat_Init2", ApplyPhase.GameInit));
        using var container = new Container();

        Runner(a).RunPhase(ApplyPhase.GameInit, category => { _calls.Add("apply:" + category); return true; }, container);

        CollectionAssert.AreEqual(new[] { "apply:Cat_Init1", "apply:Cat_Init2", "A:phase:GameInit" }, _calls);
    }

    [TestMethod]
    public void RunPhase_AFailedCategory_DoesNotFaultTheModule()
    {
        var a = Module("A");
        a.Categories.Add(new PatchCategoryDecl("Cat_Init", ApplyPhase.GameInit));
        using var container = new Container();
        var runner = Runner(a);

        runner.RunPhase(ApplyPhase.GameInit, _ => false, container);

        Assert.IsFalse(runner.IsFaulted("A"), "A failed category is the applier's to report, not a module fault.");
        CollectionAssert.AreEqual(new[] { "A:phase:GameInit" }, _calls);
    }

    [TestMethod]
    public void RunPhase_SkipsParkedModules()
    {
        var parked = Module("P");
        parked.Parked = true;
        parked.Categories.Add(new PatchCategoryDecl("Cat_Init", ApplyPhase.GameInit));
        using var container = new Container();

        Runner(parked).RunPhase(ApplyPhase.GameInit, category => { _calls.Add("apply:" + category); return true; }, container);

        Assert.AreEqual(0, _calls.Count);
    }

    [TestMethod]
    public void TakeFaultSummary_IsNull_WhenNothingFailed()
    {
        using var container = new Container();
        var runner = Runner(Module("A"));
        runner.RegisterServices(container);

        Assert.IsNull(runner.TakeFaultSummary());
    }

    [TestMethod]
    public void TakeFaultSummary_NamesEachFaultedModuleWithItsStep_ThenClears()
    {
        var a = Module("A");
        a.ThrowIn = "register";
        var b = Module("B");
        b.ThrowIn = "phase";
        using var container = new Container();
        var runner = Runner(a, b);

        runner.RegisterServices(container);
        runner.RunPhase(ApplyPhase.ProcessLoad, _ => true, container);

        Assert.AreEqual(
            "TAOM: feature modules failed and are off this session: A (service registration), B (ProcessLoad). "
            + "The TAOM log names the cause.",
            runner.TakeFaultSummary());
        Assert.IsNull(runner.TakeFaultSummary());
    }

    [TestMethod]
    public void AThrowingLogger_DoesNotBreakTheStep()
    {
        _logger.When(l => l.LogError(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("log down"));
        var a = Module("A");
        a.ThrowIn = "register";
        using var container = new Container();

        Runner(a, Module("B")).RegisterServices(container);

        CollectionAssert.AreEqual(new[] { "A:register", "B:register" }, _calls);
    }

    [TestMethod]
    public void BaseModule_DefaultsToAnEnabledModuleThatDeclaresNothing()
    {
        var module = new EmptyModule();
        using var container = new Container();

        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData);
        Assert.AreEqual(0, module.PatchCategories.Count);
        Assert.AreEqual(0, module.CampaignBehaviors.Count);
        Assert.AreEqual(0, module.GameModels.Count);
        Assert.AreEqual(0, module.MissionBehaviors.Count);
        module.RegisterServices(container);
        module.InitializeStatics(container);
        module.OnPhase(ApplyPhase.GameInit, container);
    }

    private sealed class EmptyModule : TaomFeatureModule
    {
        public override string Id => "Empty";
    }

    private sealed class RecordingModule : TaomFeatureModule
    {
        private readonly string _id;
        private readonly List<string> _calls;

        internal RecordingModule(string id, List<string> calls)
        {
            _id = id;
            _calls = calls;
        }

        internal bool Parked;
        internal bool OwnsSave;
        internal string? ThrowIn;
        internal readonly List<PatchCategoryDecl> Categories = new();

        public override string Id => _id;
        public override string? ParkedReason => Parked ? "test" : null;
        public override bool OwnsSaveData => OwnsSave;
        public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;

        public override void RegisterServices(IRegistrator registrator) => Record("register");
        public override void InitializeStatics(IResolver resolver) => Record("statics");
        public override void OnPhase(ApplyPhase phase, IResolver resolver) => Record("phase:" + phase);

        private void Record(string what)
        {
            _calls.Add(_id + ":" + what);
            if (ThrowIn != null && what.StartsWith(ThrowIn, StringComparison.Ordinal))
                throw new InvalidOperationException(_id + " broke in " + what);
        }
    }
}
