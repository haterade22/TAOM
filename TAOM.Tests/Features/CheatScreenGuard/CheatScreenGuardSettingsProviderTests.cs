using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BattleLoadDiagnostics;
using TAOM.Features.CheatScreenGuard;

namespace TAOM.Tests.Features.CheatScreenGuard;

[TestClass]
public class CheatScreenGuardSettingsProviderTests
{
    // No-MCM pin: BattleLoadDiagnosticsSettings.Instance is null in the test host (MCM is never initialised).
    [TestMethod]
    public void NoMcm_CheatListsAreOff()
        => Assert.IsFalse(new CheatScreenGuardSettingsProvider().CheatListsEnabled);

    [TestMethod]
    public void ReadsThroughTheCachedSettings()
    {
        var settings = new BattleLoadDiagnosticsSettings();
        var sut = new CheatScreenGuardSettingsProvider(settings);
        Assert.IsFalse(sut.CheatListsEnabled);

        settings.CheatModeListsAllItemsAndTroops = true;

        Assert.IsTrue(sut.CheatListsEnabled);
    }
}
