using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.SeatOfSeeing;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>
/// Stages S4 and S5 as pins: the gate, the two morale models' refusal, the order of the hero adapter's steps and the tick's place in the session.
/// The adapter and the models need a live mission, so what must exist, and in which order, is pinned by its text.
/// </summary>
[TestClass]
public class SeatFightWiringTests
{
    private static string Source(string path) => Regex.Replace(RepoPaths.ReadSource(path, stripComments: true), @"\s+", " ");

    [TestInitialize]
    public void Setup()
    {
        SeatPresence.Set(false);
        SeatPresence.SetHeroFightsOn(false);
    }

    [TestCleanup]
    public void Cleanup()
    {
        SeatPresence.Set(false);
        SeatPresence.SetHeroFightsOn(false);
    }

    // ---- The gate ----

    [TestMethod]
    public void HeroFightsOn_IsItsOwnFlag_AndLeavingTheSeatDoesNotClearIt()
    {
        SeatPresence.Set(true);
        SeatPresence.SetHeroFightsOn(true);

        SeatPresence.Set(false);

        Assert.IsTrue(SeatPresence.HeroFightsOn, "the hero stays on the AI for one more tick after the leave");
        Assert.IsFalse(SeatPresence.IsSeated);
    }

    [TestMethod]
    public void HeroFightsOn_SeatedAloneDoesNotRaiseIt()
    {
        SeatPresence.Set(true);

        Assert.IsFalse(SeatPresence.HeroFightsOn);
    }

    [DataTestMethod]
    [DataRow(true, true, true)]
    [DataRow(true, false, false)]
    [DataRow(false, true, false)]
    [DataRow(false, false, false)]
    public void RefusesPanic_NeedsTheGateAndTheMainAgent(bool gate, bool mainAgent, bool refuses)
    {
        SeatPresence.SetHeroFightsOn(gate);

        Assert.AreEqual(refuses, SeatPresence.RefusesPanic(mainAgent));
    }

    [TestMethod]
    public void BothMoraleModels_AskTheSeatsRefusalOfThePanic()
    {
        var battle = Source("Main/Features/CultureDoctrine/Models/TaomBattleMoraleModel.cs");
        var custom = Source("Main/Features/CultureDoctrine/Models/TaomCustomBattleMoraleModel.cs");

        StringAssert.Contains(battle, "CanPanicDueToMorale(Agent agent) => !SeatPresence.RefusesPanic(agent.IsMainAgent) && ");
        StringAssert.Contains(custom, "CanPanicDueToMorale(Agent agent) => !SeatPresence.RefusesPanic(agent.IsMainAgent) && ");
    }

    // ---- The patches ----

    [TestMethod]
    public void TheBoundaryPrefix_ClearsTheOutsideAnswer_OnlyWhenTheSeatHoldsTheCountdownForTheMainAgent()
    {
        var prefix = Source("Client/Features/SeatOfSeeing/Hooks/Patch140_BoundaryCountdown.cs");

        StringAssert.Contains(prefix, "if (isAgentOutside && SeatPresence.HoldsBoundaryCountdown(agent != null && agent.IsMainAgent)) isAgentOutside = false;");
    }

    [TestMethod]
    public void TheOrderPanelPatch_GivesTheTranspilerTheLoggerTheModuleHandedOver()
    {
        var patch = Source("Client/Features/SeatOfSeeing/Hooks/Patch140_OrderPanelCheck.cs");

        StringAssert.Contains(patch, "SeatOrderPanelTranspiler.LetTheHandedHeroOpenIt(instructions, SeatPatchLog.Logger);");
    }

    [TestMethod]
    public void InitializeStatics_HandsTheContainersLoggerToThePatchLog_AndCanRunTwice()
    {
        using var container = new Container();
        var logger = Substitute.For<IModLogger>();
        container.RegisterInstance(logger);
        var module = new SeatOfSeeingModule();
        SeatPatchLog.Logger = null;
        try
        {
            module.InitializeStatics(container);
            module.InitializeStatics(container);

            Assert.AreSame(logger, SeatPatchLog.Logger);
        }
        finally
        {
            SeatPatchLog.Logger = null;
        }
    }

    // ---- The hero adapter ----

    [TestMethod]
    public void TheHandBack_ClearsTheDelegates_ThenStopsUsingTheObject_ThenChangesTheController_ThenClearsTheAiState()
    {
        var adapter = Source("Main/Adapters/SeatHeroAdapter.cs");
        var hand = adapter.Substring(adapter.IndexOf("private static void HandBack", StringComparison.Ordinal));

        var order = new[]
        {
            "agent.OnAgentWieldedItemChange = null;", "agent.OnAgentMountedStateChanged = null;",
            "agent.StopUsingGameObject(isSuccessful: false, Agent.StopUsingGameObjectFlags.None);",
            "agent.Controller = AgentControllerType.Player;", "agent.AIStateFlags = Agent.AIStateFlag.None;", "agent.ClearTargetFrame();",
        }.Select(t => hand.IndexOf(t, StringComparison.Ordinal)).ToList();

        CollectionAssert.AreEqual(order.OrderBy(i => i).ToList(), order, "the setter removes the AI components as it runs, and the cleanup that needs the AI is skipped after that");
        Assert.IsTrue(order.All(i => i >= 0));
    }

    [TestMethod]
    public void TheHandBack_WritesThePlayerController_InAFinally_SoAStepBeforeItCannotStrandTheHeroOnTheAi()
    {
        var adapter = Source("Main/Adapters/SeatHeroAdapter.cs");
        var hand = adapter.Substring(adapter.IndexOf("private static void HandBack", StringComparison.Ordinal));

        StringAssert.Contains(hand, "try { agent.OnAgentWieldedItemChange = null; agent.OnAgentMountedStateChanged = null; "
                                    + "agent.StopUsingGameObject(isSuccessful: false, Agent.StopUsingGameObjectFlags.None); } finally { agent.Controller = AgentControllerType.Player;");
    }

    [TestMethod]
    public void TheHandBack_StopsARetreatThatSurvivedTheFlip_RightAfterTheControllerWrite()
    {
        var adapter = Source("Main/Adapters/SeatHeroAdapter.cs");
        var hand = adapter.Substring(adapter.IndexOf("private static void HandBack", StringComparison.Ordinal));

        StringAssert.Contains(hand, "agent.Controller = AgentControllerType.Player; if (agent.IsRetreating()) agent.StopRetreating(); agent.AIStateFlags");
    }

    [TestMethod]
    public void TheRetreatCheck_StopsTheComponentThenTheAgent_AndAnswersWhatTheAgentReadsAfterwards()
    {
        var adapter = Source("Main/Adapters/SeatHeroAdapter.cs");
        var check = adapter.Substring(adapter.IndexOf("public bool StopHeroRetreat()", StringComparison.Ordinal));

        StringAssert.Contains(check, "agent.StopRetreatingMoraleComponent(); if (agent.IsRetreating()) agent.StopRetreating(); return !agent.IsRetreating();");
    }

    [TestMethod]
    public void TheHandOver_StopsUsingTheObject_BeforeTheFlip_AndReadsAndWritesNoMorale()
    {
        var adapter = Source("Main/Adapters/SeatHeroAdapter.cs");
        var hand = adapter.Substring(adapter.IndexOf("private static void HandToTheAi", StringComparison.Ordinal),
            adapter.IndexOf("private static void HandBack", StringComparison.Ordinal) - adapter.IndexOf("private static void HandToTheAi", StringComparison.Ordinal));

        Assert.IsTrue(hand.IndexOf("agent.StopUsingGameObject(isSuccessful: false);", StringComparison.Ordinal) < hand.IndexOf("agent.Controller = AgentControllerType.AI;", StringComparison.Ordinal),
            "a use cut short is not a success: the pick-up of a dropped weapon is not granted by it");
        Assert.IsFalse(hand.Contains("agent.StopUsingGameObject();"));
        Assert.IsFalse(Regex.IsMatch(adapter, @"\b(GetMorale|SetMorale|ChangeMorale|CommonAIComponent)\b"),
            "a Player hero has no CommonAIComponent: a write of the -1 that GetMorale answers clamps to 0, and the morale refusal then pins him at 0.01 instead of the 50 the new component holds");
        Assert.IsFalse(adapter.Contains("OnMainAgentChanged"), "the flip is never made inside a main agent change handler");
        StringAssert.Contains(hand, "agent.SetAgentFlags(agent.GetAgentFlags() & ~AgentFlag.CanRide);");
        StringAssert.Contains(hand, "if (mission.Mode != MissionMode.Battle) return;");
    }

    [TestMethod]
    public void TheHandOver_LeavesARetreatOrderToTheRetreatCheck()
    {
        var adapter = Source("Main/Adapters/SeatHeroAdapter.cs");

        StringAssert.Contains(adapter, "if (order.OrderEnum != MovementOrder.MovementOrderEnum.Retreat) order.OnUnitJoinOrLeave(formation, agent, true);");
    }

    [TestMethod]
    public void EveryWriterOfTheHeroAdapter_MarksTheThreadGuard()
    {
        var adapter = Source("Main/Adapters/SeatHeroAdapter.cs");

        foreach (var site in new[] { "StandFast", "BeginFighting", "EndFighting", "StopHeroRetreat" })
            StringAssert.Contains(adapter, "MissionThreadGuard.NoteCall(\"SeatHeroAdapter." + site + "\", _logger.LogError);");
    }

    [TestMethod]
    public void ABeginThatThrowsAfterTheFlip_PutsTheControllerBackBeforeItRethrows()
    {
        var adapter = Source("Main/Adapters/SeatHeroAdapter.cs");

        StringAssert.Contains(adapter, "catch { if (agent!.Controller == AgentControllerType.AI) HandBack(agent); throw; }");
    }

    [TestMethod]
    public void TheHandBack_RunsOnlyForAHeroWhoReadsOnTheAi_ThroughTheSameReadAsTheSessionsDebtCheck()
    {
        var adapter = Source("Main/Adapters/SeatHeroAdapter.cs");

        StringAssert.Contains(adapter, "public void EndFighting() { MissionThreadGuard.NoteCall(\"SeatHeroAdapter.EndFighting\", _logger.LogError); "
                                       + "if (HeroOnAi()) HandBack(Mission.Current.MainAgent); }");
    }

    // ---- The session ----

    [TestMethod]
    public void TheMissionTick_RunsTheHandOffBeforeItsEarlyReturnOnAFault()
    {
        var session = Source("Client/Features/SeatOfSeeing/SeatSession.cs");
        var tick = session.Substring(session.IndexOf("internal void OnMissionTick()", StringComparison.Ordinal));

        Assert.IsTrue(tick.IndexOf("FightTick();", StringComparison.Ordinal) < tick.IndexOf("if (Faulted) return;", StringComparison.Ordinal),
            "a leave or a fault still hands the hero back");
    }

    [TestMethod]
    public void TheTakeIsMadeOnlyFromThePress_NeverFromAMainAgentChange()
    {
        var session = Source("Client/Features/SeatOfSeeing/SeatSession.cs");
        var view = Source("Client/Features/SeatOfSeeing/SeatView.cs");

        Assert.AreEqual(1, Regex.Matches(session, @"_screen\.TryTake\(\)").Count);
        Assert.IsFalse(Regex.IsMatch(view, @"TryTake|TakeControlOfAgent|PlayerOwner"), "a player who never uses the Seat must see vanilla");
        var changed = session.Substring(session.IndexOf("internal void OnMainAgentChanged()", StringComparison.Ordinal), 120);
        StringAssert.Contains(changed, "_standFastOwed = true;");
    }

    [TestMethod]
    public void TheScreenAdapterTake_HandsTheCommandOverRightAfterTheTake_ThroughTheControllerVanillaSets()
    {
        var adapter = Source("Client/Adapters/SeatScreenAdapter.cs");

        Assert.IsTrue(adapter.IndexOf("_mission.TakeControlOfAgent(soldier);", StringComparison.Ordinal)
                      < adapter.IndexOf("HandOverTheCommand(soldier)", StringComparison.Ordinal));
        StringAssert.Contains(adapter, "controller.Owner = owner;");
        StringAssert.Contains(adapter, "var controller = team.PlayerOrderController;");
        Assert.IsFalse(adapter.Contains("SetPlayerCanTakeControlOfAnotherAgentWhenDead"), "it arms vanilla's held Kick key on the followed agent");
        Assert.IsFalse(adapter.Contains("MasterOrderController"));
        StringAssert.Contains(adapter, "if (previous == null || (AgentSlotIdentity.IsCurrentOccupant(previous) && previous.IsActive())) continue;");
    }

    [TestMethod]
    public void TheScreenAdapterTake_ReadsTheMainPartyThroughTheCampaign_NeverThroughThePartyBaseGetter()
    {
        var adapter = Source("Client/Adapters/SeatScreenAdapter.cs");

        StringAssert.Contains(adapter, "Campaign.Current?.MainParty?.Party");
        Assert.IsFalse(adapter.Contains("PartyBase.MainParty"));
        StringAssert.Contains(adapter, "agent.Origin is HowdahCrewAgentOrigin or MumakilCrewAgentOrigin");
        StringAssert.Contains(adapter, "agent.Formation != null);");
    }

    // ---- The two delegates the hand-back clears (a binding test) ----

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheAgentDelegatesTheHandBackClears_ArePublicActionFields_ThatTheHumanAiComponentSubscribes()
    {
        var agent = typeof(TaleWorlds.MountAndBlade.Agent);
        foreach (var name in new[] { "OnAgentWieldedItemChange", "OnAgentMountedStateChanged" })
        {
            var field = agent.GetField(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(field, name + " must be a public instance field: the hand-back assigns null to it");
            Assert.AreEqual(typeof(Action), field!.FieldType, name);
        }

        // The component's constructor combines onto both (HumanAIComponent.cs:136-139); this pins that it still has the constructor taking an agent.
        Assert.IsNotNull(typeof(TaleWorlds.MountAndBlade.HumanAIComponent).GetConstructor(new[] { agent }));
    }
}
