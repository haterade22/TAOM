using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CheatMenu;
using TAOM.Features.CheatMenu.Battle;
using TAOM.Features.CoopInterop;

namespace TAOM.Tests.Features.CheatMenu;

/// <summary>
/// The battle cheats behind the Esc-menu list and the taom.cheat_battle_* commands (BattleCheatService), on a substitute
/// adapter: co-op refused before any adapter call, then a running battle required, then each action's count; spawns copy only
/// the non-hero soldiers already on that side, within 1..200.
/// </summary>
[TestClass]
public class BattleCheatServiceTests
{
    private IBattleCheatAdapter _battle = null!;
    private ICoopSessionProvider _coop = null!;
    private BattleCheatService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _battle = Substitute.For<IBattleCheatAdapter>();
        _battle.ReadState().Returns(BattleCheatPolicyTests.On());
        _coop = Substitute.For<ICoopSessionProvider>();
        _coop.IsAuthority.Returns(true);
        _service = new BattleCheatService(_battle, _coop, Substitute.For<IDedicatedServerProvider>(), Substitute.For<IModLogger>());
    }

    private static readonly BattleTroop[] Allies = { new("a", false), new("lord_hero", true), new("b", false) };

    // ---- co-op ----

    [TestMethod]
    public void EveryBattleCheat_InACoopSession_IsRefusedAndTouchesNothing()
    {
        _coop.IsSessionActive.Returns(true);

        Assert.AreEqual(CheatOutcomeKind.CoopRefused, _service.HealSelf().Kind);
        Assert.AreEqual(CheatOutcomeKind.CoopRefused, _service.HealSide().Kind);
        Assert.AreEqual(CheatOutcomeKind.CoopRefused, _service.RoutEnemies().Kind);
        Assert.AreEqual(CheatOutcomeKind.CoopRefused, _service.KillEnemies().Kind);
        Assert.AreEqual(CheatOutcomeKind.CoopRefused, _service.SpawnTroops(true, 10).Kind);
        Assert.AreEqual(CheatOutcomeKind.CoopRefused, _service.SpawnTroops(false, 10).Kind);
        Assert.AreEqual(0, _battle.ReceivedCalls().Count(), "a refused cheat reads and writes nothing");
    }

    [TestMethod]
    public void EveryMenuEntry_InACoopSession_IsRefused()
    {
        _coop.IsSessionActive.Returns(true);

        foreach (var action in BattleCheatOpener.Actions)
            Assert.AreEqual(CheatOutcomeKind.CoopRefused, _service.Run(action).Kind, action.ToString());
        Assert.AreEqual(0, _battle.ReceivedCalls().Count());
    }

    // ---- no battle ----

    [DataTestMethod]
    [DataRow(false, false, true, DisplayName = "no mission")]
    [DataRow(true, true, true, DisplayName = "ended or retreated")]
    [DataRow(true, false, false, DisplayName = "no player team")]
    public void EveryBattleCheat_OutsideARunningBattle_NoBattleAndNoWrite(bool running, bool ending, bool playerTeam)
    {
        _battle.ReadState().Returns(new BattleCheatState(running, ending, playerTeam));

        foreach (var action in BattleCheatOpener.Actions)
            Assert.AreEqual(CheatOutcomeKind.NoBattle, _service.Run(action).Kind, action.ToString());
        _battle.DidNotReceive().HealMainAgent();
        _battle.DidNotReceive().HealPlayerSide();
        _battle.DidNotReceive().RoutEnemies();
        _battle.DidNotReceive().KillEnemies();
        _battle.DidNotReceiveWithAnyArgs().SpawnTroops(default!, default, default, default);
        _battle.DidNotReceiveWithAnyArgs().SetImmortal(default, default);
        Assert.IsFalse(_service.GodModeOn);
    }

    // ---- heal, rout, kill ----

    [TestMethod]
    public void HealSelf_NoLivingMainAgent_NothingToDo()
    {
        _battle.HealMainAgent().Returns(0);

        Assert.AreEqual(CheatOutcomeKind.NothingToDo, _service.HealSelf().Kind);
    }

    [TestMethod]
    public void HealSelf_RiderAndMount_DoneWithTwo()
    {
        _battle.HealMainAgent().Returns(2);

        var outcome = _service.HealSelf();

        Assert.AreEqual(CheatOutcomeKind.Done, outcome.Kind);
        Assert.AreEqual(2f, outcome.Number);
    }

    [TestMethod]
    public void HealSide_Healed_NumberIsTheAgentsHealed()
    {
        _battle.HealPlayerSide().Returns(57);

        Assert.AreEqual(57f, _service.HealSide().Number);
        Assert.AreEqual(CheatOutcomeKind.Done, _service.HealSide().Kind);
    }

    [TestMethod]
    public void RoutEnemies_Routed_NumberIsTheAgentsRouted()
    {
        _battle.RoutEnemies().Returns(40);

        var outcome = _service.RoutEnemies();

        Assert.AreEqual(CheatOutcomeKind.Done, outcome.Kind);
        Assert.AreEqual(40f, outcome.Number);
    }

    [TestMethod]
    public void KillEnemies_Killed_NumberIsTheAgentsKilled()
    {
        _battle.KillEnemies().Returns(33);

        var outcome = _service.KillEnemies();

        Assert.AreEqual(CheatOutcomeKind.Done, outcome.Kind);
        Assert.AreEqual(33f, outcome.Number);
    }

    [TestMethod]
    public void KillEnemies_NoEnemyLeft_NothingToDo()
    {
        _battle.KillEnemies().Returns(0);

        Assert.AreEqual(CheatOutcomeKind.NothingToDo, _service.KillEnemies().Kind);
    }

    // ---- spawn ----

    [TestMethod]
    public void SpawnTroops_Allies_CopiesTheNonHeroTroopsRoundRobinOnTheRingInTurn()
    {
        _battle.SideTroops(true).Returns(Allies);
        _battle.SpawnTroops(Arg.Any<string>(), Arg.Any<int>(), true, Arg.Any<int>()).Returns(c => c.ArgAt<int>(1));

        var outcome = _service.SpawnTroops(playerSide: true, 10);

        Assert.AreEqual(CheatOutcomeKind.Done, outcome.Kind);
        Assert.AreEqual(10f, outcome.Number);
        _battle.Received(1).SpawnTroops("a", 5, true, 0);
        _battle.Received(1).SpawnTroops("b", 5, true, 5);
        _battle.DidNotReceive().SpawnTroops("lord_hero", Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<int>());
    }

    [TestMethod]
    public void SpawnTroops_Enemies_AsksTheEnemySide()
    {
        _battle.SideTroops(false).Returns(new[] { new BattleTroop("orc", false) });
        _battle.SpawnTroops("orc", 10, false, 0).Returns(10);

        var outcome = _service.Run(BattleCheatAction.SpawnEnemies);

        Assert.AreEqual(CheatOutcomeKind.Done, outcome.Kind);
        _battle.Received(1).SpawnTroops("orc", 10, false, 0);
        _battle.DidNotReceive().SideTroops(true);
    }

    [TestMethod]
    public void SpawnTroops_NobodyOnThatSide_NothingToDoAndNoSpawn()
    {
        _battle.SideTroops(false).Returns(new BattleTroop[0]);

        Assert.AreEqual(CheatOutcomeKind.NothingToDo, _service.SpawnTroops(false, 10).Kind);
        _battle.DidNotReceiveWithAnyArgs().SpawnTroops(default!, default, default, default);
    }

    [TestMethod]
    public void SpawnTroops_OnlyHeroesOnThatSide_NothingToDoAndNoSpawn()
    {
        _battle.SideTroops(true).Returns(new[] { new BattleTroop("lord_hero", true) });

        Assert.AreEqual(CheatOutcomeKind.NothingToDo, _service.SpawnTroops(true, 10).Kind);
        _battle.DidNotReceiveWithAnyArgs().SpawnTroops(default!, default, default, default);
    }

    [TestMethod]
    public void SpawnTroops_TheSpawnerPlacedNone_NothingToDo()
    {
        _battle.SideTroops(true).Returns(Allies);
        _battle.SpawnTroops(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<int>()).Returns(0);

        Assert.AreEqual(CheatOutcomeKind.NothingToDo, _service.SpawnTroops(true, 10).Kind);
    }

    [DataTestMethod]
    [DataRow(1)]
    [DataRow(200)]
    public void SpawnTroops_CountAtTheLimits_Spawns(int count)
    {
        _battle.SideTroops(true).Returns(new[] { new BattleTroop("a", false) });
        _battle.SpawnTroops("a", count, true, 0).Returns(count);

        Assert.AreEqual(CheatOutcomeKind.Done, _service.SpawnTroops(true, count).Kind);
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(-5)]
    [DataRow(201)]
    public void SpawnTroops_CountPastTheLimits_BadAmountAndTouchesNothing(int count)
    {
        var outcome = _service.SpawnTroops(true, count);

        Assert.AreEqual(CheatOutcomeKind.BadAmount, outcome.Kind);
        Assert.AreEqual(200f, outcome.Number);
        Assert.AreEqual(0, _battle.ReceivedCalls().Count());
    }

    // ---- the menu's entries ----

    [TestMethod]
    public void Run_EachEntry_CallsItsOwnAdapterAction()
    {
        _battle.SideTroops(Arg.Any<bool>()).Returns(new[] { new BattleTroop("a", false) });

        _service.Run(BattleCheatAction.HealSelf);
        _battle.Received(1).HealMainAgent();
        _service.Run(BattleCheatAction.HealSide);
        _battle.Received(1).HealPlayerSide();
        _service.Run(BattleCheatAction.Rout);
        _battle.Received(1).RoutEnemies();
        _service.Run(BattleCheatAction.Kill);
        _battle.Received(1).KillEnemies();
        _service.Run(BattleCheatAction.SpawnAllies);
        _battle.Received(1).SpawnTroops("a", BattleCheatService.MenuSpawnCount, true, 0);
        _service.Run(BattleCheatAction.SpawnEnemies);
        _battle.Received(1).SpawnTroops("a", BattleCheatService.MenuSpawnCount, false, 0);
        _service.Run(BattleCheatAction.GodMode);
        Assert.IsTrue(_service.GodModeOn);
    }

    // ---- god mode ----

    private static readonly GodModeAgent[] RiderAndHorse =
    {
        new(1, AgentMortality.Mortal), new(2, AgentMortality.Mortal),
    };

    private static readonly GodModeAgent[] RiderAndHorseHeld =
    {
        new(1, AgentMortality.Immortal), new(2, AgentMortality.Immortal),
    };

    private void HeldReadsAs(AgentMortality mortality) =>
        _battle.ReadAgents(Arg.Any<IReadOnlyList<GodModeAgent>>())
            .Returns(c => c.ArgAt<IReadOnlyList<GodModeAgent>>(0).Select(a => a with { Mortality = mortality }).ToList());

    [TestMethod]
    public void GodMode_InACoopSession_RefusedAndTouchesNothing()
    {
        _coop.IsSessionActive.Returns(true);

        Assert.AreEqual(CheatOutcomeKind.CoopRefused, _service.ToggleGodMode().Kind);
        Assert.AreEqual(CheatOutcomeKind.CoopRefused, _service.SetGodMode(true).Kind);
        _service.TickGodMode();
        Assert.IsFalse(_service.GodModeOn);
        Assert.AreEqual(0, _battle.ReceivedCalls().Count());
    }

    [TestMethod]
    public void GodMode_TurnedOn_MakesTheRiderAndHorseImmortal()
    {
        _battle.GodModeTargets().Returns(RiderAndHorse);

        var outcome = _service.ToggleGodMode();

        Assert.AreEqual(CheatOutcomeKind.Done, outcome.Kind);
        Assert.AreEqual(1f, outcome.Number, "1 = now on");
        Assert.IsTrue(_service.GodModeOn);
        _battle.Received(1).SetImmortal(1, true);
        _battle.Received(1).SetImmortal(2, true);
        _battle.DidNotReceive().SetImmortal(Arg.Any<int>(), false);
    }

    [TestMethod]
    public void GodMode_TurnedOff_SetsBothBackToMortal()
    {
        _battle.GodModeTargets().Returns(RiderAndHorse);
        _service.ToggleGodMode();
        HeldReadsAs(AgentMortality.Immortal);
        _battle.ClearReceivedCalls();

        var outcome = _service.ToggleGodMode();

        Assert.AreEqual(CheatOutcomeKind.Done, outcome.Kind);
        Assert.AreEqual(0f, outcome.Number, "0 = now off");
        Assert.IsFalse(_service.GodModeOn);
        _battle.Received(1).SetImmortal(1, false);
        _battle.Received(1).SetImmortal(2, false);
        _battle.DidNotReceive().SetImmortal(Arg.Any<int>(), true);
    }

    [TestMethod]
    public void GodMode_SetOnTwice_StaysOnAndReleasesNothing()
    {
        _battle.GodModeTargets().Returns(RiderAndHorse);
        _service.SetGodMode(true);
        _battle.GodModeTargets().Returns(RiderAndHorseHeld);
        HeldReadsAs(AgentMortality.Immortal);
        _battle.ClearReceivedCalls();

        var second = _service.SetGodMode(true);

        Assert.IsTrue(_service.GodModeOn);
        Assert.AreEqual(1f, second.Number);
        _battle.DidNotReceiveWithAnyArgs().SetImmortal(default, default);
    }

    [TestMethod]
    public void GodMode_OutsideARunningBattle_NoBattleAndStaysOff()
    {
        _battle.ReadState().Returns(BattleCheatPolicyTests.On() with { Ending = true });

        Assert.AreEqual(CheatOutcomeKind.NoBattle, _service.SetGodMode(true).Kind);
        Assert.IsFalse(_service.GodModeOn);
        _battle.DidNotReceiveWithAnyArgs().SetImmortal(default, default);
    }

    [TestMethod]
    public void TickGodMode_Off_ReadsNothing()
    {
        _service.TickGodMode();

        Assert.AreEqual(0, _battle.ReceivedCalls().Count(), "an idle god mode costs nothing per frame");
    }

    [TestMethod]
    public void TickGodMode_NewHorse_ProtectsItAndReleasesTheOldOne()
    {
        _battle.GodModeTargets().Returns(RiderAndHorse);
        _service.SetGodMode(true);
        HeldReadsAs(AgentMortality.Immortal);
        _battle.GodModeTargets().Returns(new[] { new GodModeAgent(1, AgentMortality.Immortal), new GodModeAgent(5, AgentMortality.Mortal) });
        _battle.ClearReceivedCalls();

        _service.TickGodMode();

        _battle.Received(1).SetImmortal(5, true);
        _battle.Received(1).SetImmortal(2, false);
        _battle.DidNotReceive().SetImmortal(1, Arg.Any<bool>());
    }

    [TestMethod]
    public void TickGodMode_HeldHorseGoneOrItsSlotReused_LetGoWithoutATouch()
    {
        _battle.GodModeTargets().Returns(RiderAndHorse);
        _service.SetGodMode(true);
        // The adapter leaves out a held agent that is gone or whose index now holds another agent.
        _battle.ReadAgents(Arg.Any<IReadOnlyList<GodModeAgent>>()).Returns(new[] { new GodModeAgent(1, AgentMortality.Immortal) });
        _battle.GodModeTargets().Returns(new[] { new GodModeAgent(1, AgentMortality.Immortal) });
        _service.TickGodMode();
        _battle.ClearReceivedCalls();

        _service.SetGodMode(false);

        _battle.Received(1).SetImmortal(1, false);
        _battle.DidNotReceive().SetImmortal(2, Arg.Any<bool>());
    }

    [TestMethod]
    public void TickGodMode_BattleEnded_TurnsOffAndReleases()
    {
        _battle.GodModeTargets().Returns(RiderAndHorse);
        _service.SetGodMode(true);
        HeldReadsAs(AgentMortality.Immortal);
        _battle.ReadState().Returns(BattleCheatPolicyTests.On() with { Ending = true });
        _battle.ClearReceivedCalls();

        _service.TickGodMode();

        Assert.IsFalse(_service.GodModeOn);
        _battle.Received(1).SetImmortal(1, false);
        _battle.Received(1).SetImmortal(2, false);
        _battle.ClearReceivedCalls();
        _service.TickGodMode();
        Assert.AreEqual(0, _battle.ReceivedCalls().Count(), "nothing held after the release");
    }

    [TestMethod]
    public void TickGodMode_NextMission_StartsOffAndTouchesNoAgentOfIt()
    {
        _battle.ReadState().Returns(BattleCheatPolicyTests.On() with { MissionKey = 11 });
        _battle.GodModeTargets().Returns(RiderAndHorse);
        _service.SetGodMode(true);
        _battle.ReadState().Returns(BattleCheatPolicyTests.On() with { MissionKey = 22 });
        HeldReadsAs(AgentMortality.Immortal);
        _battle.ClearReceivedCalls();

        _service.TickGodMode();

        Assert.IsFalse(_service.GodModeOn);
        _battle.DidNotReceiveWithAnyArgs().SetImmortal(default, default);
        _battle.DidNotReceiveWithAnyArgs().ReadAgents(default!);
    }

    [TestMethod]
    public void ToggleGodMode_InTheNextMission_TurnsItOn()
    {
        _battle.ReadState().Returns(BattleCheatPolicyTests.On() with { MissionKey = 11 });
        _battle.GodModeTargets().Returns(RiderAndHorse);
        _service.SetGodMode(true);
        _battle.ReadState().Returns(BattleCheatPolicyTests.On() with { MissionKey = 22 });
        _battle.ClearReceivedCalls();

        var outcome = _service.ToggleGodMode();

        Assert.AreEqual(1f, outcome.Number, "a stale 'on' from the last battle must not make the first toggle turn it off");
        _battle.DidNotReceiveWithAnyArgs().ReadAgents(default!);
        _battle.Received(1).SetImmortal(1, true);
    }
}
