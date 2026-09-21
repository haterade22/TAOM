using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Elephant;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.Elephant;

/// <summary>
/// A howdah seat must be empty before anything deactivates it, or the game hangs (#627, 2026-09-19).
/// <c>UsableMissionObject.IsDeactivated</c>'s setter runs <c>while (HasAIMovingTo) MovingAgent.StopUsingGameObject();</c>
/// (v1.5.3, UsableMissionObject.cs:129-133), and <c>UsableMachine.OnMissionEnded</c> sets that on every standing point.
/// TAOM seats an archer with <c>AddMovingAgent</c> only, never <c>AIMoveToGameObjectEnable</c> (the native path that
/// sends agents climbing after an unreachable seat), so <c>StopUsingGameObject</c> cannot clear <c>MovingAgent</c> and
/// that loop never exits. It hung two battles on 2026-09-19, both as the battle ended with archers still aboard, and a
/// hang dump named the stack. Both types therefore release first, and this pins that they still do.
/// </summary>
[TestClass]
public class HowdahSeatReleaseTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static MethodInfo DeclaredOnMissionEnded(Type type)
    {
        MethodInfo method = type.GetMethod("OnMissionEnded",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly,
            binder: null, types: Type.EmptyTypes, modifiers: null);
        Assert.IsNotNull(method,
            $"{type.Name} no longer overrides OnMissionEnded, so vanilla's deactivation runs against an occupied seat " +
            "and the mission-end loop hangs the game.");
        return method;
    }

    private static List<string> CalleeNames(MethodBase method)
    {
        byte[] il = method.GetMethodBody()?.GetILAsByteArray();
        Assert.IsNotNull(il, $"{method.Name} has no readable body, so this gate cannot vouch for it.");
        return IlCallScanner.ExtractCalledMethods(method, il).Select(m => m.Name).ToList();
    }

    [TestMethod]
    public void TheMachine_EmptiesEverySeat_BeforeVanillaDeactivatesThem()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        List<string> callees = CalleeNames(DeclaredOnMissionEnded(typeof(TaomHowdahMachine)));
        CollectionAssert.Contains(callees, "ReleaseAllSeats",
            "TaomHowdahMachine.OnMissionEnded must release the seats before it calls base: " + string.Join(", ", callees));
        CollectionAssert.Contains(callees, "OnMissionEnded",
            "the override must still call base.OnMissionEnded so vanilla's own deactivation happens.");
    }

    [TestMethod]
    public void TheSeat_ReleasesItsArcher_WhenTheMissionEnds()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        List<string> callees = CalleeNames(DeclaredOnMissionEnded(typeof(TaomHowdahStandingPoint)));
        CollectionAssert.Contains(callees, "ReleaseAgent",
            "TaomHowdahStandingPoint.OnMissionEnded must release its own archer: " + string.Join(", ", callees));
    }

    [TestMethod]
    public void TheMachine_AlsoEmptiesItsSeats_WhenVanillaDisablesIt()
    {
        // UsableMachine.Disable ends in the same IsDeactivated setter as OnMissionEnded and would spin the same way.
        // Nothing reaches it today (it needs a DestructableComponent on the howdah entity), which is why guarding it
        // now costs three lines instead of another hang dump later (#627, engine review E3).
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        MethodInfo disable = typeof(TaomHowdahMachine).GetMethod("Disable",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly,
            binder: null, types: Type.EmptyTypes, modifiers: null);
        Assert.IsNotNull(disable, "TaomHowdahMachine must override Disable; vanilla's deactivates occupied seats");
        CollectionAssert.Contains(CalleeNames(disable), "ReleaseAllSeats",
            "Disable must empty the seats before calling base");
    }

    [TestMethod]
    public void AReleasedArcher_GetsItsOrdinaryBehaviourBack()
    {
        // The seat flattens GoToPos and Melee to zero while seated, and OverrideBehaviorParams latches the set to
        // Overriden. Without restoring it, an archer released when its elephant dies cannot advance or defend itself
        // until its formation happens to re-apply a movement order (#627, engine review E2).
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        MethodInfo release = typeof(TaomHowdahStandingPoint).GetMethod("ReleaseAgent",
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.IsNotNull(release, "TaomHowdahStandingPoint.ReleaseAgent is gone");
        List<string> calls = CalleeNames(release);
        CollectionAssert.Contains(calls, "RestoreOrdinaryCombatStance", "the named inverse of ApplyCrewCombatStance");
        CollectionAssert.Contains(calls, "TryAttachToFormation", "vanilla's re-attach also clears the detachment");
        CollectionAssert.Contains(calls, "DisableScriptedMovement", "or the archer stands where the howdah used to be");

        MethodInfo restore = typeof(TaomHowdahStandingPoint).GetMethod("RestoreOrdinaryCombatStance",
            BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly);
        Assert.IsNotNull(restore, "the inverse of ApplyCrewCombatStance is gone");
        List<string> restoreCalls = CalleeNames(restore);
        CollectionAssert.Contains(restoreCalls, "SetBehaviorValueSet", "a released archer keeps the crew curves otherwise");
        CollectionAssert.Contains(restoreCalls, "SetDetachableFromFormation", "a released archer is an ordinary unit again");
    }

    [TestMethod]
    public void TheSeat_StillRegistersItsArcherWithAddMovingAgent_WhichIsWhyTheGuardExists()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        MethodInfo onUse = typeof(TaomHowdahStandingPoint).GetMethod("OnUse",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.IsNotNull(onUse, "TaomHowdahStandingPoint no longer overrides OnUse.");

        List<string> callees = CalleeNames(onUse);
        bool registersMovingAgent = callees.Contains("AddMovingAgent");
        bool goesThroughVanilla = callees.Contains("AIMoveToGameObjectEnable") || callees.Contains("UseGameObject");

        // If the seat ever starts using the vanilla registration, MovingAgent becomes clearable by
        // StopUsingGameObject and the releases above stop being load-bearing. Read this test before deleting them.
        Assert.IsTrue(registersMovingAgent || goesThroughVanilla,
            "the seat registers its archer through neither path: re-read why the mission-end release exists.");
        if (goesThroughVanilla)
            Assert.Inconclusive("The seat now uses the vanilla registration; the mission-end release may be redundant.");
    }
}
