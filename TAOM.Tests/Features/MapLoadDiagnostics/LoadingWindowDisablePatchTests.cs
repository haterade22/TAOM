using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Engine;
using TAOM.Core.Logging;
using TAOM.Features.MapLoadDiagnostics;
using TAOM.Features.MapLoadDiagnostics.Hooks;

namespace TAOM.Tests.Features.MapLoadDiagnostics;

/// <summary>
/// The Disable postfix must log only a real lower. The engine clears the flag before any postfix
/// runs, so the pre-call value travels from a Prefix through Harmony's <c>__state</c>; Harmony binds
/// that parameter by exact name, so its shape is pinned here as well as its behaviour.
/// </summary>
[TestClass]
public class LoadingWindowDisablePatchTests
{
    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        MapLoadTracer.Initialize(_logger);
    }

    [TestCleanup]
    public void Cleanup() => MapLoadTracer.Initialize(null!);

    [TestMethod]
    public void Prefix_Signature_IsOutBoolNamedState()
    {
        var prefix = typeof(LoadingWindow_Disable_Patch).GetMethod("Prefix", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(prefix, "LoadingWindow_Disable_Patch has no public static Prefix.");
        var parameters = prefix!.GetParameters();
        Assert.AreEqual(1, parameters.Length);
        Assert.AreEqual("__state", parameters[0].Name);
        Assert.IsTrue(parameters[0].IsOut, "__state must be an out parameter on the Prefix.");
        Assert.AreEqual(typeof(bool).MakeByRefType(), parameters[0].ParameterType);
    }

    [TestMethod]
    public void Postfix_Signature_TakesStateByValue()
    {
        var postfix = typeof(LoadingWindow_Disable_Patch).GetMethod("Postfix", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(postfix, "LoadingWindow_Disable_Patch has no public static Postfix.");
        var parameters = postfix!.GetParameters();
        Assert.AreEqual(1, parameters.Length);
        Assert.AreEqual("__state", parameters[0].Name);
        Assert.AreEqual(typeof(bool), parameters[0].ParameterType);
    }

    [TestMethod]
    public void Postfix_WhenTheWindowWasAlreadyDown_LogsNothing()
    {
        LoadingWindow_Disable_Patch.Postfix(__state: false);

        _logger.DidNotReceiveWithAnyArgs().LogInfo(default!);
    }

    [TestMethod]
    public void Postfix_WhenTheWindowWasUpAndIsNowDown_LogsOneLoweredLineWithCallers()
    {
        // Never raised in the test host: no LoadingWindowManager exists, so Enable cannot set it.
        Assert.IsFalse(LoadingWindow.IsLoadingWindowActive, "precondition: engine flag is down");

        LoadingWindow_Disable_Patch.Postfix(__state: true);

        // TraceWithCallers skips itself and the Postfix, so a helper between them would show up as
        // the first caller; a fallback chain would lose the caller information entirely.
        _logger.ReceivedWithAnyArgs(1).LogInfo(default!);
        _logger.Received(1).LogInfo(Arg.Is<string>(s =>
            s.Contains("LOADING-WINDOW lowered") && s.Contains("callers: ")
            && !s.Contains("callers: <none>") && !s.Contains("callers: <unavailable>")
            && !s.Contains("callers: LoadingWindow_Disable_Patch.")));
    }

    [TestMethod]
    public void PrefixThenPostfix_WhenTheWindowIsAlreadyDown_CapturesFalseAndLogsNothing()
    {
        Assert.IsFalse(LoadingWindow.IsLoadingWindowActive, "precondition: engine flag is down");

        LoadingWindow_Disable_Patch.Prefix(out var state);
        LoadingWindow_Disable_Patch.Postfix(state);

        Assert.IsFalse(state, "the Prefix must capture the engine flag, not a constant");
        _logger.DidNotReceiveWithAnyArgs().LogInfo(default!);
    }
}
