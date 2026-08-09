using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.AutoResolveDiagnostics;

namespace TAOM.Tests.Features.AutoResolveDiagnostics;

/// <summary>
/// The provider's `??` fallback and the compiled MCM default must agree. When they drift, the
/// feature behaves one way before the settings file is written and another way after — a bug that
/// reproduces only on a fresh install, which is the hardest kind to be told about.
/// </summary>
[TestClass]
public class AutoResolveDiagnosticsSettingsProviderTests
{
    [TestMethod]
    public void IsEnabled_WithoutMcm_FallsBackToOn()
    {
        // TaomSettings.Instance is null outside the game, which is exactly the fresh-install path.
        Assert.IsTrue(new AutoResolveDiagnosticsSettingsProvider().IsEnabled);
    }

    [TestMethod]
    public void CompiledDefault_MatchesTheProviderFallback()
    {
        Assert.IsTrue(new TaomSettings().LogAutoResolvedBattles,
            "TaomSettings.LogAutoResolvedBattles default must match the provider's ?? fallback");
    }

    [TestMethod]
    public void CensusIsEnabled_WithoutMcm_FallsBackToOff()
    {
        // Deliberately the opposite default from the battle log. The census is static per build —
        // measured on a live session it was 8,341 of 17,622 log lines, 47% of the file, written
        // identically every launch. One capture serves until troop data or the balance config
        // changes, so it is opt-in; the battle log stays opt-out because it records sessions that
        // have already happened.
        Assert.IsFalse(new AutoResolveDiagnosticsSettingsProvider().IsCensusEnabled);
    }

    [TestMethod]
    public void CensusCompiledDefault_MatchesTheProviderFallback()
    {
        Assert.IsFalse(new TaomSettings().LogAutoResolveTroopCensus,
            "TaomSettings.LogAutoResolveTroopCensus default must match the provider's ?? fallback");
    }
}
