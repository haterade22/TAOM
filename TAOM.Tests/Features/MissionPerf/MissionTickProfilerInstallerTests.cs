using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.BattleLoadDiagnostics;
using TAOM.Features.MissionPerf;
using TAOM.Features.MissionPerf.Hooks;

namespace TAOM.Tests.Features.MissionPerf;

/// <summary>
/// The Patch97 installer's decisions: off applies nothing and says so, and <c>Installed</c> needs the
/// category applied AND both <c>Mission.OnTick</c> sites swapped. The category apply is a fake that sets
/// the site counts the real transpilers would. Binds the private <c>Mission.WaitTickCompletion</c>, so it
/// needs the game assemblies.
/// </summary>
[TestCategory("RequiresGame")]
[TestClass]
public class MissionTickProfilerInstallerTests
{
    private IModLogger _logger = null!;
    private IBattleLoadDiagnosticsSettingsProvider _settings = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _settings = Substitute.For<IBattleLoadDiagnosticsSettingsProvider>();
        ResetStatics();
    }

    [TestCleanup]
    public void Cleanup() => ResetStatics();

    // Plan 041 added Patch98's statics beside these; every one goes back so no state leaks between classes.
    private static void ResetStatics()
    {
        HitchProbeInstaller.ResetForTests();
        HitchProbeHooks.ResetForTests();
        MissionTickProfilerHooks.ResetForTests();
        MissionAttributionInstaller.ResetForTests();
    }

    // Since plan 041 the installer hands over to Patch98 after Patch97; that category applies cleanly here,
    // so these cases exercise the success path rather than the probe installer's catch (review 041).
    private static Func<string, bool> Apply(bool result, int onTickSites, int onPreTickSites) => category =>
    {
        if (category == HitchProbeInstaller.Category)
            return true;
        Assert.AreEqual(MissionTickProfilerInstaller.Category, category);
        MissionTickProfilerHooks.OnTickSites = onTickSites;
        MissionTickProfilerHooks.OnPreTickSites = onPreTickSites;
        return result;
    };

    [TestMethod]
    public void InstallIfEnabled_ToggleOff_LogsTheOffLineAndAppliesNothing()
    {
        _settings.TickProfilerEnabled.Returns(false);
        var applied = 0;

        MissionTickProfilerInstaller.InstallIfEnabled(_settings, _logger, _ => { applied++; return true; });

        Assert.AreEqual(0, applied);
        _logger.Received(1).LogInfo(TickProfileLines.OffLine);
        Assert.IsNull(MissionTickProfilerHooks.Profiler);
        Assert.IsFalse(MissionTickProfilerHooks.Installed);
    }

    [TestMethod]
    public void InstallIfEnabled_AllSitesSwapped_IsInstalledAndLogsTheInstallLine()
    {
        _settings.TickProfilerEnabled.Returns(true);

        MissionTickProfilerInstaller.InstallIfEnabled(_settings, _logger, Apply(true, 2, 2));

        Assert.IsTrue(MissionTickProfilerHooks.Installed);
        Assert.IsNotNull(MissionTickProfilerHooks.Profiler);
        Assert.IsNotNull(MissionTickProfilerHooks.WaitTickCompletionCall, "The private wait binds on the installed engine.");
        _logger.Received(1).LogInfo(TickProfileLines.BuildInstallLine(true, 2, 2, 2, AllocationCounter.Available));
        _logger.DidNotReceiveWithAnyArgs().LogError(default!);
    }

    [TestMethod]
    public void InstallIfEnabled_CategoryFails_IsNotInstalled()
    {
        _settings.TickProfilerEnabled.Returns(true);

        MissionTickProfilerInstaller.InstallIfEnabled(_settings, _logger, Apply(false, 0, 0));

        Assert.IsFalse(MissionTickProfilerHooks.Installed);
        _logger.Received(1).LogInfo(TickProfileLines.BuildInstallLine(false, 0, 0, 2, AllocationCounter.Available));
    }

    [TestMethod]
    public void InstallIfEnabled_AnOnTickSiteMissing_IsNotInstalled()
    {
        _settings.TickProfilerEnabled.Returns(true);

        MissionTickProfilerInstaller.InstallIfEnabled(_settings, _logger, Apply(true, 0, 2));

        Assert.IsFalse(MissionTickProfilerHooks.Installed);
    }

    [TestMethod]
    public void InstallIfEnabled_OnlyTheOnPreTickRewriteBailed_StillInstalls()
    {
        _settings.TickProfilerEnabled.Returns(true);

        MissionTickProfilerInstaller.InstallIfEnabled(_settings, _logger, Apply(true, 2, 0));

        Assert.IsTrue(MissionTickProfilerHooks.Installed, "Patch98's OnPreTick prefix holds the frame boundary, so measuring continues.");
    }

    [TestMethod]
    public void InstallIfEnabled_ApplyThrows_LogsOneFaultAndIsNotInstalled()
    {
        _settings.TickProfilerEnabled.Returns(true);
        var boom = new InvalidOperationException("apply broke");

        MissionTickProfilerInstaller.InstallIfEnabled(_settings, _logger, _ => throw boom);

        Assert.IsFalse(MissionTickProfilerHooks.Installed);
        _logger.Received(1).LogError(TickProfileLines.BuildFault("install", boom));
    }
}
