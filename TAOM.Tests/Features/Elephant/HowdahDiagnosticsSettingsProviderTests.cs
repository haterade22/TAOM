using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.Elephant;

namespace TAOM.Tests.Features.Elephant;

/// <summary>
/// The howdah diagnostics toggle is on by default while the platform is being tested (Mike, 2026-09-19, #627). The
/// provider's fallback (MCM absent, a fresh install) must agree with the compiled MCM default, or the log behaves one
/// way before the settings file exists and another way after.
/// </summary>
[TestClass]
public class HowdahDiagnosticsSettingsProviderTests
{
    [TestMethod]
    public void IsEnabled_WithoutMcm_FallsBackToOn()
    {
        Assert.IsTrue(new HowdahDiagnosticsSettingsProvider().IsEnabled);
    }

    [TestMethod]
    public void CompiledDefault_MatchesTheProviderFallback()
    {
        Assert.IsTrue(new TaomSettings().EnableHowdahDiagnostics,
            "TaomSettings.EnableHowdahDiagnostics default must match the provider's ?? fallback");
    }
}
