using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.MarshalsWord;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>
/// Marshal's Word's wiring: the layer rule (the Domain folder names no engine type), the module's declarations, what the container resolves,
/// and the source-text pins of the view, whose lifecycle callbacks need a live mission.
/// </summary>
[TestClass]
public class MarshalsWordWiringTests
{
    private const string ViewPath = "Client/Features/MarshalsWord/MarshalView.cs";

    private static Container BuildContainer()
    {
        var container = new Container();
        new MarshalsWordModule().RegisterServices(container);
        container.RegisterInstance(Substitute.For<IModLogger>());
        return container;
    }

    // ---- The layer rule of build-spec section 2 ----

    [TestMethod]
    public void Domain_HoldsNoTaleWorldsReference()
    {
        var files = Directory.GetFiles(RepoPaths.RepoPath("Main", "Features", "MarshalsWord", "Domain"), "*.cs");
        Assert.IsTrue(files.Length >= 6, "the Domain folder holds the pure rules; the scan found only " + files.Length);

        foreach (var file in files)
        {
            var code = RepoPaths.StripComments(File.ReadAllText(file));
            Assert.IsFalse(Regex.IsMatch(code, @"\busing\s+TaleWorlds"), Path.GetFileName(file) + " has a using TaleWorlds: the Domain is BCL only");
            Assert.IsFalse(Regex.IsMatch(code, @"(?<!"")\bTaleWorlds\."), Path.GetFileName(file) + " names a TaleWorlds type: the Domain is BCL only");
            Assert.IsFalse(Regex.IsMatch(code, @"\busing\s+(SandBox|HarmonyLib|TAOM\.Adapters)"), Path.GetFileName(file) + " reaches past the BCL");
            Assert.IsFalse(Regex.IsMatch(code, @"\bAgent\.Index\b"), Path.GetFileName(file) + " keys on Agent.Index");
        }
    }

    // ---- The module ----

    [TestMethod]
    public void FeatureModules_ListTheMarshalModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<MarshalsWordModule>().Count());
        StringAssert.Contains(RepoPaths.ReadSource("Main/Composition/FeatureModules.cs", stripComments: true),
            "new Features.MarshalsWord.MarshalsWordModule()");
    }

    [TestMethod]
    public void TheModule_IsNotParked_OwnsNoSaveData_AndAddsNoMissionBehavior()
    {
        var module = new MarshalsWordModule();

        Assert.AreEqual("MarshalsWord", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData);
        Assert.AreEqual(0, module.MissionBehaviors.Count, "the view is a [DefaultView] in TAOM.Client, built by the engine");
        Assert.AreEqual(0, module.CampaignBehaviors.Count);
        Assert.AreEqual(0, module.GameModels.Count);
    }

    [TestMethod]
    public void TheModule_AppliesPatch141AtTheFirstMission_BecauseMovementOrderReadsMissionCurrent()
    {
        var decls = new MarshalsWordModule().PatchCategories;

        Assert.AreEqual(1, decls.Count);
        Assert.AreEqual("Patch141_MarshalsWord", decls[0].Category);
        Assert.AreEqual("Patch141_MarshalsWord", MarshalsWordModule.PatchCategory);
        Assert.AreEqual(ApplyPhase.FirstMission, decls[0].Phase);
    }

    [TestMethod]
    public void TheSingleOwnerFiles_AreNotEditedForThisFeature()
    {
        Assert.IsFalse(RepoPaths.ReadSource("Main/IoC.cs", stripComments: true).Contains("MarshalsWord"));
        Assert.IsFalse(RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true).Contains("MarshalsWord"));
    }

    [TestMethod]
    public void EveryServiceTheModuleDeclares_ResolvesFromItsOwnRegistrations()
    {
        using var container = BuildContainer();

        Assert.IsInstanceOfType(container.Resolve<IMarshalSettingsProvider>(), typeof(MarshalSettingsProvider));
        Assert.IsInstanceOfType(container.Resolve<IMarshalOrderAdapter>(), typeof(MarshalOrderAdapter));
        Assert.IsNotNull(container.Resolve<MarshalHotKeys>());
    }

    [TestMethod]
    public void TheOrderAdapter_IsBuiltPerResolve_BecauseItHoldsTheMissionsControllerAndEvents()
    {
        using var container = BuildContainer();

        Assert.AreNotSame(container.Resolve<IMarshalOrderAdapter>(), container.Resolve<IMarshalOrderAdapter>());
        Assert.AreSame(container.Resolve<MarshalHotKeys>(), container.Resolve<MarshalHotKeys>());
        Assert.AreSame(container.Resolve<IMarshalSettingsProvider>(), container.Resolve<IMarshalSettingsProvider>());
    }

    [TestMethod]
    public void NoScreenAdapterIsRegistered_ItIsBuiltPerMissionByTheView()
    {
        using var container = BuildContainer();

        Assert.AreEqual(0, container.GetServiceRegistrations().Count(r => r.ServiceType.Name == "IMarshalScreenAdapter"));
        StringAssert.Contains(RepoPaths.ReadSource(ViewPath, stripComments: true), "new MarshalScreenAdapter(mission)");
    }

    // ---- The view ----

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void MarshalView_IsADefaultView_OfTheMissionViewBase_WithOneAttributeAndNoConstructorArguments()
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var view = typeof(TAOM.Client.ClientSubModule).Assembly.GetType("TAOM.Client.Features.MarshalsWord.MarshalView");
        Assert.IsNotNull(view, "MarshalView must live in TAOM.Client");
        var attributes = view!.GetCustomAttributesData().Where(a => a.AttributeType.Namespace != "System.Runtime.CompilerServices").ToList();
        Assert.AreEqual(1, attributes.Count, "the engine reads a default view's attributes and wants exactly one: " + string.Join(",", attributes.Select(a => a.AttributeType.Name)));
        Assert.AreEqual("DefaultView", attributes[0].AttributeType.Name, "a view TAOM adds in OnMissionBehaviorInitialize never registers and never ticks (#970)");
        var bases = BaseChain(view).Select(t => t.Name).ToList();
        CollectionAssert.Contains(bases, "MissionView");
        Assert.IsNotNull(view.GetConstructor(Type.EmptyTypes), "a default view needs a public parameterless constructor");
    }

    [TestMethod]
    public void TheView_ReadsTheKeysInTheScreenTick_NeverInTheMissionTick()
    {
        var code = RepoPaths.ReadSource(ViewPath, stripComments: true);

        StringAssert.Contains(ViewMember("public override void OnMissionScreenTick(float dt)"), "Down(_keys.Queue)");
        Assert.IsFalse(ViewMember("public override void OnMissionTick(float dt)").Contains("IsKey"),
            "fast time repeats OnMissionTick several times in one frame (MissionState.cs:171-198)");
        StringAssert.Contains(code, "TaleWorlds.InputSystem.Input.IsKeyDown(key)");
    }

    [TestMethod]
    public void TheView_ReadsNoKey_WhenThereIsNoLiveSession_AndOnlyWithThePanelOpen()
    {
        var screenTick = ViewMember("public override void OnMissionScreenTick(float dt)");

        StringAssert.Contains(screenTick, "if (session == null || !session.IsLive || _keys == null) return;");
        StringAssert.Contains(screenTick, "var open = Mission.IsOrderMenuOpen;");
        StringAssert.Contains(screenTick, "open && (_trialQueue || Down(_keys.Queue))");
        StringAssert.Contains(screenTick, "open && (_trialWidths || Down(_keys.KeepWidths))");
    }

    [TestMethod]
    public void TheView_DrivesTheSessionFromTheMissionTick_AndTellsItOfAModeChange()
    {
        StringAssert.Contains(ViewMember("public override void OnMissionTick(float dt)"), "base.OnMissionTick(dt); _session?.OnMissionTick();");
        StringAssert.Contains(ViewMember("public override void OnMissionModeChange(MissionMode oldMissionMode, bool atStart)"),
            "base.OnMissionModeChange(oldMissionMode, atStart); _session?.OnModeChange();");
    }

    [TestMethod]
    public void TheView_BuildsItsSessionOnce_InTheScreenInitialize_AndNeverInATick()
    {
        var code = RepoPaths.ReadSource(ViewPath, stripComments: true);

        Assert.AreEqual(1, Regex.Matches(code, @"new MarshalSession\(").Count, "one concrete session, built once");
        StringAssert.Contains(ViewMember("public override void OnMissionScreenInitialize()"), "new MarshalSession(");
        Assert.IsFalse(ViewMember("public override void OnMissionTick(float dt)").Contains("new MarshalSession("));
        Assert.IsFalse(ViewMember("public override void OnMissionScreenTick(float dt)").Contains("new MarshalSession("));
    }

    [TestMethod]
    public void TheView_EndsTheSessionFromBothTeardownPaths_AndKeepsTheBaseCalls()
    {
        // MissionState.OnFinalize reaches RemoveMissionBehavior with no EndMission, and MissionView overrides OnRemoveBehavior, so the base call stays.
        StringAssert.Contains(ViewMember("public override void OnMissionScreenFinalize()"), "base.OnMissionScreenFinalize(); EndSession();");
        StringAssert.Contains(ViewMember("public override void OnRemoveBehavior()"), "base.OnRemoveBehavior(); EndSession();");
    }

    [TestMethod]
    public void TheView_EndingTheSession_IsTerminal_BecauseTheScreenCanStillTickARemovedView()
    {
        // RemoveMissionBehavior leaves the view in the screen's list (Mission.cs:4708-4725), and the mission still calls OnMissionTick after
        // OnMissionScreenFinalize (MissionScreen.cs:4241-4245, Mission.cs:3751-3754).
        StringAssert.Contains(ViewMember("private void EndSession()"), "_session?.Finish(); _session = null; }");
    }

    [TestMethod]
    public void TheView_GivesTheTrialOneMemberPerHeldKey_AndTheSessionToRead()
    {
        var code = RepoPaths.ReadSource(ViewPath, stripComments: true);

        StringAssert.Contains(code, "internal void TrialHoldQueue(bool held) => _trialQueue = held;");
        StringAssert.Contains(code, "internal void TrialHoldWidths(bool held) => _trialWidths = held;");
        StringAssert.Contains(code, "internal MarshalSession? Session => _session;");
    }

    [TestMethod]
    public void TheSessionsFinish_ClearsTheHooks_OnlyWhenItOwnsThem_AndOnEveryPath()
    {
        var code = RepoPaths.ReadSource("Client/Features/MarshalsWord/MarshalSession.cs", stripComments: true);

        Assert.AreEqual(2, Regex.Matches(code, @"\bTeardown\(\);|Swallow\(Teardown\)").Count, "Finish and Fault share one teardown");
        StringAssert.Contains(code, "var owner = MarshalHooks.IsOwnedBy(this);");
        StringAssert.Contains(code, "_orders.DirectOrder -= _queue.OnDirectOrder;");
        StringAssert.Contains(code, "_orders.PlayerTeamChanged -= OnPlayerTeamChanged;");
    }

    [TestMethod]
    public void TheArrayLayout_TestsAiControlAsTheCommitSeesIt()
    {
        const string path = "Main/Features/MarshalsWord/Hooks/MarshalArrayLayout.cs";

        // OrderController.BeforeSetOrder (v1.5.6, line 724) lifts a company off AI control only when it has a player owner, and it runs in the
        // commit but not in the preview. So a company is refused when it is AI controlled with no owner, and a plain IsAIControlled would
        // let the preview draw vanilla's flags and the commit lay out the array.
        StringAssert.Contains(RepoPaths.ReadSource(path, stripComments: true), "!(f.IsAIControlled && f.PlayerOwner == null)");
    }

    [TestMethod]
    public void TheView_KeepsTheFeatureOffInANetworkSession_BecauseTheSeatPolicyDoesNotAskIt()
    {
        // SeatMissionPolicy.Decide computes Orders from the mission kind alone (SeatMissionPolicy.cs:36-39), so this clause is the whole gate that
        // keeps Marshal's Word off a co-op session. The doc's four questions and the CoopSettingsRelevance reasons rest on it.
        var mayRun = ViewMember("private static bool MayRun(Mission mission, IModLogger logger)");

        StringAssert.Contains(mayRun, "return SeatMissionPolicy.Decide(facts).Orders && !facts.NetworkSession;");
        StringAssert.Contains(ViewMember("public override void OnMissionScreenInitialize()"), "() => MayRun(mission, logger)");
    }

    private static string ViewMember(string signature)
    {
        var code = RepoPaths.ReadSource(ViewPath, stripComments: true);
        var start = code.IndexOf(signature, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "MarshalView has no " + signature);
        var next = Regex.Match(code.Substring(start + signature.Length), @"\n\s+(public|private|internal) ");
        var length = next.Success ? next.Index + signature.Length : code.Length - start;
        return Regex.Replace(code.Substring(start, length), @"\s+", " ");
    }

    private static System.Collections.Generic.IEnumerable<Type> BaseChain(Type type)
    {
        for (var t = type.BaseType; t != null; t = t.BaseType)
            yield return t;
    }
}
