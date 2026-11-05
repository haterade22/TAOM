using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.NameplateCull;
using TAOM.Features.NameplateCull.Models;

namespace TAOM.Tests.Features.NameplateCull;

[TestClass]
public class NameplateCullServiceTests
{
    private INameplateCullSettingsProvider _settings = null!;
    private INameplateCullAdapter _adapter = null!;
    private IModLogger _logger = null!;
    private NameplateCullService _sut = null!;
    private readonly object _vm = new object();

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<INameplateCullSettingsProvider>();
        _settings.CullEnabled.Returns(true);
        _adapter = Substitute.For<INameplateCullAdapter>();
        _adapter.Initialize().Returns((string?)null);
        _adapter.UpdateCulled(Arg.Any<object>()).Returns(new CullCounts(10, 90));
        _logger = Substitute.For<IModLogger>();
        _sut = new NameplateCullService(_settings, _adapter, _logger);
    }

    private List<string> Infos() => Lines(nameof(IModLogger.LogInfo));

    private List<string> Warnings() => Lines(nameof(IModLogger.LogWarning));

    private List<string> Errors() => Lines(nameof(IModLogger.LogError));

    private List<string> Lines(string method) =>
        _logger.ReceivedCalls().Where(c => c.GetMethodInfo().Name == method)
            .Select(c => (string)c.GetArguments()[0]!).ToList();

    // ---- TryUpdate: when the cull runs ----

    [TestMethod]
    public void TryUpdate_InstalledAndOn_RunsTheCulledUpdateAndReturnsTrue()
    {
        _sut.Install();

        var ran = _sut.TryUpdate(_vm);

        Assert.IsTrue(ran);
        _adapter.Received(1).UpdateCulled(_vm);
    }

    [TestMethod]
    public void TryUpdate_BeforeInstall_ReturnsFalseAndTouchesNothing()
    {
        var ran = _sut.TryUpdate(_vm);

        Assert.IsFalse(ran, "not bound yet: vanilla runs");
        _adapter.DidNotReceive().UpdateCulled(Arg.Any<object>());
    }

    [TestMethod]
    public void TryUpdate_ToggleOff_ReturnsFalseAndTouchesNothing()
    {
        _sut.Install();
        _settings.CullEnabled.Returns(false);

        var ran = _sut.TryUpdate(_vm);

        Assert.IsFalse(ran);
        _adapter.DidNotReceive().UpdateCulled(Arg.Any<object>());
    }

    [TestMethod]
    public void TryUpdate_ToggleSwitchedOffAndOnAgain_FollowsTheToggleLive()
    {
        _sut.Install();
        _settings.CullEnabled.Returns(false);
        Assert.IsFalse(_sut.TryUpdate(_vm));

        _settings.CullEnabled.Returns(true);

        Assert.IsTrue(_sut.TryUpdate(_vm));
    }

    [TestMethod]
    public void TryUpdate_TheSettingsThrow_ReturnsFalseWithoutSwitchingOff()
    {
        var throwing = false;
        _settings.CullEnabled.Returns(_ => throwing ? throw new InvalidOperationException("mcm") : true);
        _sut.Install();
        throwing = true;
        Assert.IsFalse(_sut.TryUpdate(_vm));
        throwing = false;

        Assert.IsTrue(_sut.TryUpdate(_vm), "a read that failed once is not a reason to stay off");
    }

    [TestMethod]
    public void TryUpdate_InstallFoundAMissingEngineMember_NeverRunsTheCull()
    {
        _adapter.Initialize().Returns("SettlementNameplatesVM._mapCamera is missing");
        _sut.Install();

        var ran = _sut.TryUpdate(_vm);

        Assert.IsFalse(ran);
        _adapter.DidNotReceive().UpdateCulled(Arg.Any<object>());
    }

    // ---- TryUpdate: the error path ----

    [TestMethod]
    public void TryUpdate_TheAdapterThrows_ReturnsFalseSoVanillaRuns()
    {
        _sut.Install();
        _adapter.UpdateCulled(Arg.Any<object>()).Throws(new InvalidOperationException("boom"));

        var ran = _sut.TryUpdate(_vm);

        Assert.IsFalse(ran, "vanilla re-runs the whole update");
    }

    [TestMethod]
    public void TryUpdate_TheAdapterThrows_LogsOneErrorWithTheExceptionAndSwitchesOff()
    {
        _sut.Install();
        var boom = new InvalidOperationException("boom in the cull");
        _adapter.UpdateCulled(Arg.Any<object>()).Throws(boom);

        _sut.TryUpdate(_vm);
        _sut.TryUpdate(_vm);
        _sut.TryUpdate(_vm);

        var errors = Errors();
        Assert.AreEqual(1, errors.Count, "one line, not one per frame");
        StringAssert.StartsWith(errors[0], "[NameplateCull] OFF after an error");
        StringAssert.Contains(errors[0], "boom in the cull");
        _adapter.Received(1).UpdateCulled(Arg.Any<object>());
    }

    [TestMethod]
    public void TryUpdate_AfterAnError_StaysOffEvenWhenTheToggleIsFlippedBackOn()
    {
        _sut.Install();
        _adapter.UpdateCulled(Arg.Any<object>()).Throws(new InvalidOperationException("boom"));
        _sut.TryUpdate(_vm);
        _adapter.UpdateCulled(Arg.Any<object>()).Returns(new CullCounts(1, 1));

        Assert.IsFalse(_sut.TryUpdate(_vm), "off for the session, whatever the toggle says");
    }

    [TestMethod]
    public void TryUpdate_TheLoggerThrowsWhileSwitchingOff_StillReturnsFalse()
    {
        _sut.Install();
        _adapter.UpdateCulled(Arg.Any<object>()).Throws(new InvalidOperationException("boom"));
        _logger.When(l => l.LogError(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("log"));

        var ran = _sut.TryUpdate(_vm);

        Assert.IsFalse(ran);
    }

    // ---- The window line: every WindowFrames culled map frames (about five minutes at 60 fps) ----

    // Frames that reach Record are the ones TryUpdate culled; toggle-off frames, menus over the map and battles never get there.
    private bool RunFrames(int count)
    {
        var ran = false;
        for (var i = 0; i < count; i++)
            ran = _sut.TryUpdate(_vm);
        return ran;
    }

    private List<string> WindowLines() => Infos().Where(l => l.Contains("culled map frames")).ToList();

    [TestMethod]
    public void TryUpdate_FirstFrame_WritesNoWindowLine()
    {
        _sut.Install();

        RunFrames(1);

        Assert.AreEqual(0, WindowLines().Count);
    }

    [TestMethod]
    public void TryUpdate_AWindowOfCulledFrames_WritesOneLineWithTheCounts()
    {
        _sut.Install();
        RunFrames(NameplateCullService.WindowFrames - 1);
        Assert.AreEqual(0, WindowLines().Count, "not yet");

        RunFrames(1);

        Assert.AreEqual(1, WindowLines().Count);
        Assert.AreEqual("[NameplateCull] 18000 culled map frames: 180000 nameplate updates run, 1620000 skipped (90.0 percent)",
            WindowLines()[0]);
    }

    [TestMethod]
    public void TryUpdate_AfterALine_TheNextWindowStartsFromZero()
    {
        _sut.Install();
        RunFrames(NameplateCullService.WindowFrames);      // line 1
        _adapter.UpdateCulled(Arg.Any<object>()).Returns(new CullCounts(1, 3));

        RunFrames(NameplateCullService.WindowFrames - 1);
        Assert.AreEqual(1, WindowLines().Count, "the first line only");
        RunFrames(1);

        Assert.AreEqual(2, WindowLines().Count);
        Assert.AreEqual("[NameplateCull] 18000 culled map frames: 18000 nameplate updates run, 54000 skipped (75.0 percent)",
            WindowLines()[1]);
    }

    [TestMethod]
    public void TryUpdate_ToggleOffFrames_AreNotCounted()
    {
        _sut.Install();
        RunFrames(100);
        _settings.CullEnabled.Returns(false);
        RunFrames(40_000);                   // map frames with the cull off never reach the window
        _settings.CullEnabled.Returns(true);

        RunFrames(NameplateCullService.WindowFrames - 101);
        Assert.AreEqual(0, WindowLines().Count, "17,999 culled frames so far");
        RunFrames(1);

        var line = WindowLines().Single();
        StringAssert.Contains(line, " 18000 culled map frames:");
    }

    [TestMethod]
    public void TryUpdate_TheLoggerThrowsOnTheWindowLine_StillReturnsTrue()
    {
        _sut.Install();
        RunFrames(NameplateCullService.WindowFrames - 1);
        _logger.When(l => l.LogInfo(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("log"));

        var ran = _sut.TryUpdate(_vm);

        Assert.IsTrue(ran, "the update already ran: a failed log must not hand the frame to vanilla a second time");
        Assert.AreEqual(0, Errors().Count, "and it does not switch the cull off");
    }

    // ---- Install ----

    [TestMethod]
    public void Install_ToggleOn_WritesTheOnLine()
    {
        _sut.Install();

        CollectionAssert.AreEqual(
            new[] { "[NameplateCull] ON: the campaign map skips hidden settlement nameplates (toggle on)" },
            Infos());
        Assert.AreEqual(0, Warnings().Count);
    }

    [TestMethod]
    public void Install_ToggleOff_SaysSo()
    {
        _settings.CullEnabled.Returns(false);

        _sut.Install();

        CollectionAssert.AreEqual(
            new[] { "[NameplateCull] installed, toggle off: every settlement nameplate updates as in the vanilla game" },
            Infos());
    }

    [TestMethod]
    public void Install_AnEngineMemberIsMissing_WarnsWithTheReasonAndWritesNoOnLine()
    {
        _adapter.Initialize().Returns("SettlementNameplateVM._worldPos is missing");

        _sut.Install();

        CollectionAssert.AreEqual(
            new[] { "[NameplateCull] OFF: SettlementNameplateVM._worldPos is missing; the vanilla nameplate update runs" },
            Warnings());
        Assert.AreEqual(0, Infos().Count);
    }

    [TestMethod]
    public void Install_TheAdapterThrows_WarnsAndStaysVanilla()
    {
        _adapter.Initialize().Throws(new TypeLoadException("no such type"));

        _sut.Install();

        var warnings = Warnings();
        Assert.AreEqual(1, warnings.Count);
        StringAssert.StartsWith(warnings[0], "[NameplateCull] OFF: ");
        StringAssert.Contains(warnings[0], "no such type");
        Assert.IsFalse(_sut.TryUpdate(_vm));
    }

    [TestMethod]
    public void Install_TheSettingsThrow_StillWritesALineAndBinds()
    {
        _settings.CullEnabled.Throws(new InvalidOperationException("mcm"));

        _sut.Install();

        Assert.AreEqual(1, Infos().Count);
        _adapter.Received(1).Initialize();
    }

    [TestMethod]
    public void Install_TheLoggerThrows_DoesNotThrowAndStillBinds()
    {
        _logger.When(l => l.LogInfo(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("log"));

        _sut.Install();
        _settings.CullEnabled.Returns(true);

        Assert.IsTrue(_sut.TryUpdate(_vm));
    }
}
