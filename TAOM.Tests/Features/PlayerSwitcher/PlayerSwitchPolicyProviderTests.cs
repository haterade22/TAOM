using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.PlayerSwitcher;
using TAOM.Features.PlayerSwitcher.Domain;

namespace TAOM.Tests.Features.PlayerSwitcher;

/// <summary>
/// Issue #514. The provider is the only place TaomSettings.Instance is read for this feature.
/// Reading MCM is untestable offline and follows the house pattern used by
/// AlignmentDesertionSettingsProvider, so the settings read sits behind an overridable seam and
/// what is proven here is the session latch: once a reflection probe fails, the feature must stay
/// off for the rest of the session rather than leaving a campaign half-swapped.
/// </summary>
[TestClass]
public class PlayerSwitchPolicyProviderTests
{
    /// <summary>Stands in for MCM so the latch can be exercised with no game running.</summary>
    private sealed class StubProvider : PlayerSwitchPolicyProvider
    {
        private readonly PlayerSwitchPolicy _settings;

        public StubProvider(IModLogger logger, PlayerSwitchPolicy settings) : base(logger)
            => _settings = settings;

        protected override PlayerSwitchPolicy ReadSettings() => _settings;
    }

    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup() => _logger = Substitute.For<IModLogger>();

    [TestMethod]
    public void TheProviderReturnsWhateverTheSettingsSay()
    {
        var sut = new StubProvider(_logger, PlayerSwitchPolicy.Default);

        Assert.IsTrue(sut.Current.Enabled);
        Assert.IsTrue(sut.Current.IncludeWanderers);
        Assert.IsFalse(sut.Current.AllowLoreLockedHeroes);
        Assert.IsFalse(sut.Current.TransferStartingGold);
    }

    [TestMethod]
    public void DisablingForTheSession_LatchesTheFeatureOff()
    {
        var sut = new StubProvider(_logger, PlayerSwitchPolicy.Default);

        sut.DisableForSession("probe failed");

        Assert.IsFalse(sut.Current.Enabled);
    }

    [TestMethod]
    public void TheLatchSurvivesSettingsStillReportingEnabled()
    {
        var sut = new StubProvider(_logger, PlayerSwitchPolicy.Default);

        sut.DisableForSession("probe failed");

        Assert.IsFalse(sut.Current.Enabled, "MCM saying yes must not undo a failed probe");
        Assert.IsFalse(sut.Current.IncludeWanderers);
        Assert.IsFalse(sut.Current.AllowLoreLockedHeroes);
    }

    [TestMethod]
    public void DisablingForTheSession_IsLoggedOnceWithTheReason()
    {
        var sut = new StubProvider(_logger, PlayerSwitchPolicy.Default);

        sut.DisableForSession("Campaign.PlayerDefaultFaction has no setter");
        sut.DisableForSession("Campaign.PlayerDefaultFaction has no setter");

        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("PlayerDefaultFaction")));
    }

    [TestMethod]
    public void ADisabledSettingIsRespectedWithoutTheLatch()
    {
        var sut = new StubProvider(_logger, PlayerSwitchPolicy.Disabled);

        Assert.IsFalse(sut.Current.Enabled);
    }

    // #704: the themed faction screen owns the picking in a character creation where it is shown, and
    // only in that one. A creation where it is switched off gets Player Switcher back.

    [TestMethod]
    public void SetSuppressedForCharacterCreation_TurnsTheFeatureOffWhileSet()
    {
        var sut = new StubProvider(_logger, PlayerSwitchPolicy.Default);

        sut.SetSuppressedForCharacterCreation(true);

        Assert.IsFalse(sut.Current.Enabled);
    }

    [TestMethod]
    public void SetSuppressedForCharacterCreation_Lifted_BringsTheFeatureBack()
    {
        var sut = new StubProvider(_logger, PlayerSwitchPolicy.Default);
        sut.SetSuppressedForCharacterCreation(true);

        sut.SetSuppressedForCharacterCreation(false);

        Assert.IsTrue(sut.Current.Enabled);
    }

    [TestMethod]
    public void SetSuppressedForCharacterCreation_IsNoFailureAndLogsNoWarning()
    {
        var sut = new StubProvider(_logger, PlayerSwitchPolicy.Default);

        sut.SetSuppressedForCharacterCreation(true);

        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void SetSuppressedForCharacterCreation_LiftedAfterAFailedProbe_KeepsTheLatch()
    {
        var sut = new StubProvider(_logger, PlayerSwitchPolicy.Default);
        sut.DisableForSession("probe failed");

        sut.SetSuppressedForCharacterCreation(false);

        Assert.IsFalse(sut.Current.Enabled, "lifting a suppression must not undo a failed probe");
    }
}
