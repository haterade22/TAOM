using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ImeGuard;
using TAOM.Features.ImeGuard.Domain;
using TAOM.Tests.Features.LoadTimeStamps;

namespace TAOM.Tests.Features.ImeGuard;

/// <summary>
/// The IME guard (#1447, ported from Silas He's v1.5.6 fix package): while the game window is in front and no text
/// field has focus, the window's input context is detached, so a CJK IME does not take hotkeys. Every other state
/// puts it back.
/// </summary>
[TestClass]
public class ImeGuardServiceTests
{
    private static readonly IntPtr Game = new(0x1000);
    private static readonly IntPtr OtherGameWindow = new(0x2000);

    [DataTestMethod]
    [DataRow(true, true, false, true)]
    [DataRow(false, true, false, false)]
    [DataRow(true, false, false, false)]
    [DataRow(true, true, true, false)]
    public void ShouldDetach_OnlyWhenOnInFrontAndNotTyping(bool enabled, bool ownForeground, bool textFocus, bool expected)
        => Assert.AreEqual(expected, ImeGuardRules.ShouldDetach(enabled, ownForeground, textFocus));

    [TestMethod]
    public void Tick_InFrontWithNoTextFocus_DetachesOnce()
    {
        var (service, window, _) = Build();

        service.Tick();
        service.Tick();

        CollectionAssert.AreEqual(new[] { "detach 4096" }, window.Calls);
    }

    [TestMethod]
    public void Tick_TextFieldTakesFocus_Reattaches()
    {
        var (service, window, _) = Build();
        service.Tick();

        window.State = new ImeWindowState(Game, ownForeground: true, textFocus: true);
        service.Tick();
        service.Tick();

        CollectionAssert.AreEqual(new[] { "detach 4096", "reattach" }, window.Calls);
    }

    [TestMethod]
    public void Tick_GameLeavesTheForeground_Reattaches()
    {
        var (service, window, _) = Build();
        service.Tick();

        window.State = new ImeWindowState(IntPtr.Zero, ownForeground: false, textFocus: false);
        service.Tick();

        CollectionAssert.AreEqual(new[] { "detach 4096", "reattach" }, window.Calls);
    }

    [TestMethod]
    public void Tick_SwitchTurnedOff_ReattachesAndStaysAttached()
    {
        var (service, window, settings) = Build();
        service.Tick();

        settings.Enabled = false;
        service.Tick();
        service.Tick();

        CollectionAssert.AreEqual(new[] { "detach 4096", "reattach" }, window.Calls);
    }

    [TestMethod]
    public void Tick_AnotherGameWindowInFront_MovesTheDetachToIt()
    {
        var (service, window, _) = Build();
        service.Tick();

        window.State = new ImeWindowState(OtherGameWindow, ownForeground: true, textFocus: false);
        service.Tick();

        CollectionAssert.AreEqual(new[] { "detach 4096", "reattach", "detach 8192" }, window.Calls);
    }

    [TestMethod]
    public void Tick_SwitchOff_MakesNoWindowCalls()
    {
        var (service, window, settings) = Build(out var logger);
        settings.Enabled = false;
        window.ThrowOnRead = true;

        service.Tick();

        Assert.AreEqual(0, window.Calls.Count);
        Assert.AreEqual(0, logger.Lines.Count, "the window was read while the switch was off");
    }

    [TestMethod]
    public void Tick_FailureWhileDetached_ReattachesBeforeItWarns()
    {
        var (service, window, _) = Build(out var logger);
        service.Tick();
        logger.OnLog = _ => window.Calls.Add("log");

        window.ThrowOnRead = true;
        service.Tick();

        CollectionAssert.AreEqual(new[] { "detach 4096", "reattach", "log" }, window.Calls);
    }

    // The client adapter's window thread hook holds a managed delegate; at exit it must go before the CLR's last GC
    // (Module.FinalizeModule: FinalizeSubModulesBases, then Common.MemoryCleanupGC).
    [TestMethod]
    public void Shutdown_RemovesTheAdapterHookAndStopsTheGuard()
    {
        var (service, window, _) = Build();
        service.Tick();

        service.Shutdown();
        service.Tick();

        CollectionAssert.AreEqual(new[] { "detach 4096", "shutdown" }, window.Calls);
    }

    [TestMethod]
    public void Restore_WhenNothingIsDetached_DoesNothing()
    {
        var (service, window, _) = Build();

        service.Restore();

        Assert.AreEqual(0, window.Calls.Count);
    }

    [TestMethod]
    public void Tick_AdapterThrows_ReattachesWarnsOnceAndStopsForTheSession()
    {
        var (service, window, _) = Build(out var logger);
        service.Tick();

        window.ThrowOnRead = true;
        service.Tick();
        service.Tick();
        window.ThrowOnRead = false;
        service.Tick();

        CollectionAssert.AreEqual(new[] { "detach 4096", "reattach" }, window.Calls);
        Assert.AreEqual(1, logger.Lines.Count(l => l.StartsWith("WARN ")));
    }

    private static (ImeGuardService, FakeWindow, FakeSettings) Build() => Build(out _);

    private static (ImeGuardService, FakeWindow, FakeSettings) Build(out RecordingLogger logger)
    {
        var window = new FakeWindow { State = new ImeWindowState(Game, ownForeground: true, textFocus: false) };
        var settings = new FakeSettings();
        logger = new RecordingLogger();
        return (new ImeGuardService(window, settings, logger), window, settings);
    }

    private sealed class FakeSettings : IImeGuardSettingsProvider
    {
        public bool Enabled { get; set; } = true;
    }

    private sealed class FakeWindow : IImeWindowAdapter
    {
        public ImeWindowState State { get; set; }
        public bool ThrowOnRead { get; set; }
        public List<string> Calls { get; } = new();

        public ImeWindowState Read() => ThrowOnRead ? throw new InvalidOperationException("read failed") : State;

        public void Detach(IntPtr window) => Calls.Add("detach " + window.ToInt64());

        public void Reattach() => Calls.Add("reattach");

        public void Shutdown() => Calls.Add("shutdown");
    }
}
