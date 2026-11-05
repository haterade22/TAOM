using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.BattleLoadDiagnostics;
using TAOM.Features.LoadTimeStamps;
using TAOM.Features.LoadTimeStamps.Domain;

namespace TAOM.Tests.Features.LoadTimeStamps;

/// <summary>
/// The static entry the patches and SubModule.cs call: unwired, or with a service that throws, every
/// forwarder answers "off" and never throws into a load.
/// </summary>
[TestClass]
public class LoadTimeStampsHooksTests
{
    [TestCleanup]
    public void Cleanup()
    {
        LoadTimeStampsHooks.Initialize(null, null);
        LoadTimeStampsHooks.InitializeLoadXml(null);
        LoadTimeStampsHooks.InitializeLifecycle(null);
    }

    private static LoadStampDetailGate GateReturning(bool enabled, IModLogger? logger = null)
    {
        var settings = Substitute.For<IBattleLoadDiagnosticsSettingsProvider>();
        settings.LoadTimeStampsEnabled.Returns(enabled);
        return new LoadStampDetailGate(settings, logger ?? new RecordingLogger());
    }

    [TestMethod]
    public void DetailEnabled_Unwired_IsFalse()
    {
        LoadTimeStampsHooks.Initialize(null, null);

        Assert.IsFalse(LoadTimeStampsHooks.DetailEnabled);
    }

    [TestMethod]
    public void StartHook_Unwired_ReturnsNull()
    {
        LoadTimeStampsHooks.Initialize(null, null);

        Assert.IsNull(LoadTimeStampsHooks.StartHook("OnGameStart", "Campaign"));
    }

    [TestMethod]
    public void DetailEnabled_Wired_ReflectsTheToggle()
    {
        LoadTimeStampsHooks.Initialize(GateReturning(true), null);
        Assert.IsTrue(LoadTimeStampsHooks.DetailEnabled);

        LoadTimeStampsHooks.Initialize(GateReturning(false), null);
        Assert.IsFalse(LoadTimeStampsHooks.DetailEnabled);
    }

    [TestMethod]
    public void StartHook_WhenTheServiceThrows_ReturnsNull()
    {
        var throwingLogger = Substitute.For<IModLogger>();
        throwingLogger.When(l => l.LogInfo(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("disk full"));
        var gate = GateReturning(true, throwingLogger);
        LoadTimeStampsHooks.Initialize(gate, new HookStampService(gate, new FakeStampClock(), new RecordingLogger()));

        Assert.IsNull(LoadTimeStampsHooks.StartHook("OnGameStart", "Campaign"));
    }

    [TestMethod]
    public void LoadXmlForwarders_Unwired_DoNothingAndReturnNull()
    {
        LoadTimeStampsHooks.InitializeLoadXml(null);

        Assert.IsNull(LoadTimeStampsHooks.BeginLoadXml("Items", "Campaign"));
        LoadTimeStampsHooks.EndLoadXml(null, null);
        LoadTimeStampsHooks.MergeFinished(null, null);
        LoadTimeStampsHooks.LogLoadXmlSummary();
    }

    [TestMethod]
    public void LoadXmlForwarders_Wired_ReachTheService()
    {
        var logger = new RecordingLogger();
        LoadTimeStampsHooks.InitializeLoadXml(new LoadXmlStampService(new FakeStampClock(), logger));

        var call = LoadTimeStampsHooks.BeginLoadXml("Items", "Campaign");
        LoadTimeStampsHooks.MergeFinished(null, null);
        LoadTimeStampsHooks.EndLoadXml(call, null);
        LoadTimeStampsHooks.LogLoadXmlSummary();

        Assert.IsNotNull(call);
        Assert.AreEqual(2, logger.Lines.Count);
        StringAssert.StartsWith(logger.Lines[0], "INFO [LoadXml] id=Items files=0 ms=0.00 xslt=0 merge_ms=0.00 objects_ms=0.00 result=ok");
        StringAssert.StartsWith(logger.Lines[1], "INFO [LoadXml] summary game=Campaign calls=1 ");
    }

    [TestMethod]
    public void LifecycleForwarders_Unwired_DoNothingAndReturnNull()
    {
        LoadTimeStampsHooks.InitializeLifecycle(null);

        foreach (LifecycleDispatch dispatch in Enum.GetValues(typeof(LifecycleDispatch)))
            Assert.IsNull(LoadTimeStampsHooks.BeginDispatch(dispatch), dispatch.ToString());
        LoadTimeStampsHooks.EndDispatch(null, null);
    }

    [TestMethod]
    public void LifecycleForwarders_Wired_ReachTheService()
    {
        var adapter = new FakeCampaignListenerAdapter();
        var logger = new RecordingLogger();
        LoadTimeStampsHooks.InitializeLifecycle(new LifecycleTimingService(adapter, GateReturning(true), new FakeStampClock(), logger));

        var scope = LoadTimeStampsHooks.BeginDispatch(LifecycleDispatch.OnGameLoaded);
        LoadTimeStampsHooks.EndDispatch(scope, null);

        Assert.IsNotNull(scope);
        CollectionAssert.AreEqual(new[] { LifecycleEvent.OnGameLoaded }, adapter.Restored);
        Assert.AreEqual("INFO [Lifecycle] dispatch=OnGameLoaded ms=0.00 listeners_ms=0.00 result=ok", logger.Lines[logger.Lines.Count - 1]);
    }

    [TestMethod]
    public void LifecycleForwarders_WiredWithTheToggleOff_StillWriteTheDispatchLine()
    {
        var adapter = new FakeCampaignListenerAdapter();
        var logger = new RecordingLogger();
        LoadTimeStampsHooks.InitializeLifecycle(new LifecycleTimingService(adapter, GateReturning(false), new FakeStampClock(), logger));

        var scope = LoadTimeStampsHooks.BeginDispatch(LifecycleDispatch.OnGameLoaded);
        LoadTimeStampsHooks.EndDispatch(scope, null);

        Assert.IsNotNull(scope);
        Assert.AreEqual(0, adapter.Wrapped.Count);
        CollectionAssert.AreEqual(new[] { "INFO [Lifecycle] dispatch=OnGameLoaded ms=0.00 listeners_ms=none result=ok" }, logger.Lines);
    }

    [TestMethod]
    public void StartHook_Wired_ReturnsATimer()
    {
        var gate = GateReturning(true);
        LoadTimeStampsHooks.Initialize(gate, new HookStampService(gate, new FakeStampClock(), new RecordingLogger()));

        Assert.IsNotNull(LoadTimeStampsHooks.StartHook("OnGameStart", "Campaign"));
    }
}
