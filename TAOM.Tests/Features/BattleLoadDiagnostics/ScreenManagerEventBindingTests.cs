using System;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.ScreenSystem;

namespace TAOM.Tests.Features.BattleLoadDiagnostics;

// MemoryStationSampler rides two PUBLIC STATIC EVENTS on the engine's ScreenManager instead of a
// Harmony patch. That is the whole reason the feature needs no patch category and no per-frame
// hook — but it also means an engine bump that renames or removes either event would silently
// produce a log with no [MemStation] lines at all, which reads exactly like "no growth happened".
//
// These name the exact members so a drift failure says WHICH engine event moved. Same role and
// same category as Patch43LoadPhaseBindingTests.
[TestClass]
public class ScreenManagerEventBindingTests
{
    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ScreenManager_ExposesOnPushScreenEvent_WithScreenBaseHandlerSignature()
        => AssertScreenEvent("OnPushScreen");

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ScreenManager_ExposesOnPopScreenEvent_WithScreenBaseHandlerSignature()
        => AssertScreenEvent("OnPopScreen");

    private static void AssertScreenEvent(string eventName)
    {
        var evt = typeof(ScreenManager).GetEvent(eventName, BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(evt, $"ScreenManager.{eventName} did not resolve — the engine moved it.");

        var invoke = evt!.EventHandlerType?.GetMethod("Invoke");
        Assert.IsNotNull(invoke, $"ScreenManager.{eventName} has no Invoke on its handler type.");

        var parameters = invoke!.GetParameters();
        Assert.AreEqual(1, parameters.Length, $"ScreenManager.{eventName} handler arity changed.");
        Assert.AreEqual(
            typeof(ScreenBase),
            parameters[0].ParameterType,
            $"ScreenManager.{eventName} handler parameter type changed.");
        Assert.AreEqual(typeof(void), invoke.ReturnType);
    }
}
