using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BattleLoadDiagnostics;
using TAOM.Features.NameplateCull;

namespace TAOM.Tests.Features.NameplateCull;

/// <summary>
/// The cull's toggle is read every campaign-map frame. On the 1.5.x line these pins live in
/// CampaignHotPathSettingsProvidersTests (plan 037), which 1.4.x does not have yet (WP123 Step C2 moves them there).
/// </summary>
[TestClass]
public class NameplateCullSettingsProviderTests
{
    [TestMethod]
    public void NameplateCull_ReadsThroughTheCachedSettings()
    {
        var settings = new BattleLoadDiagnosticsSettings();
        var sut = new NameplateCullSettingsProvider(settings);
        Assert.IsTrue(sut.CullEnabled);

        settings.CullHiddenNameplates = false;

        Assert.IsFalse(sut.CullEnabled);
    }

    // No-MCM pin: BattleLoadDiagnosticsSettings.Instance is null in the test host (MCM is never initialised).
    [TestMethod]
    public void NameplateCull_NoMcm_CullDefaultsOn()
        => Assert.IsTrue(new NameplateCullSettingsProvider().CullEnabled);
}
