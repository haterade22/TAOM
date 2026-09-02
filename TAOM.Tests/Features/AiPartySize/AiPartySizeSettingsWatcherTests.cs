using System.ComponentModel;
using MCM.Abstractions.Base;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.AiPartySize;

namespace TAOM.Tests.Features.AiPartySize;

/// <summary>
/// The decision half of the cache-invalidation watcher. The sweep itself touches MobileParty.All and
/// is verified in game; what is testable, and what actually goes wrong if written carelessly, is
/// WHICH notification triggers it. MCM raises two: LOADING_COMPLETE when it reads the json at startup,
/// and SAVE_TRIGGERED when the player presses Done having changed something. Only the second one
/// means anything here.
/// </summary>
[TestClass]
public class AiPartySizeSettingsWatcherTests
{
    [TestMethod]
    public void ShouldInvalidate_SaveTriggeredDuringACampaign_IsTrue()
        => Assert.IsTrue(AiPartySizeSettingsWatcher.ShouldInvalidate(
            BaseSettings.SaveTriggered, campaignActive: true));

    // Fires at startup, before any campaign exists. Sweeping there would dereference a null Campaign.
    [TestMethod]
    public void ShouldInvalidate_LoadingCompleteAtStartup_IsFalse()
        => Assert.IsFalse(AiPartySizeSettingsWatcher.ShouldInvalidate(
            BaseSettings.LoadingComplete, campaignActive: false));

    // Same event, but still no campaign: the main menu is a real state MCM can be opened from.
    [TestMethod]
    public void ShouldInvalidate_SaveTriggeredOutsideACampaign_IsFalse()
        => Assert.IsFalse(AiPartySizeSettingsWatcher.ShouldInvalidate(
            BaseSettings.SaveTriggered, campaignActive: false));

    [TestMethod]
    public void ShouldInvalidate_LoadingCompleteDuringACampaign_IsFalse()
        => Assert.IsFalse(AiPartySizeSettingsWatcher.ShouldInvalidate(
            BaseSettings.LoadingComplete, campaignActive: true));

    [TestMethod]
    public void ShouldInvalidate_UnrelatedOrMissingPropertyName_IsFalse()
    {
        Assert.IsFalse(AiPartySizeSettingsWatcher.ShouldInvalidate("AiLordPartySizeFactor", campaignActive: true));
        Assert.IsFalse(AiPartySizeSettingsWatcher.ShouldInvalidate(null, campaignActive: true));
        Assert.IsFalse(AiPartySizeSettingsWatcher.ShouldInvalidate("", campaignActive: true));
    }

    // Re-attaching must not stack a second handler, because a second campaign in the same process
    // runs the registration again. BaseSettings.PropertyChanged is virtual, so a stub can count the
    // add/remove calls and assert the guard rather than merely asserting no exception.
    [TestMethod]
    public void EnsureSubscribed_SameInstanceTwice_DoesNotStackHandlers()
    {
        var sut = new AiPartySizeSettingsWatcher(Substitute.For<IModLogger>());
        var settings = new CountingSettings();

        sut.EnsureSubscribed(settings);
        sut.EnsureSubscribed(settings);
        sut.EnsureSubscribed(settings);

        Assert.AreEqual(1, settings.Handlers, "exactly one handler after three attaches");
    }

    // If MCM ever hands back a different object, the old subscription must be dropped first or the
    // dead instance keeps a handler alive for the life of the process.
    [TestMethod]
    public void EnsureSubscribed_DifferentInstance_DetachesTheOldOne()
    {
        var sut = new AiPartySizeSettingsWatcher(Substitute.For<IModLogger>());
        var first = new CountingSettings();
        var second = new CountingSettings();

        sut.EnsureSubscribed(first);
        sut.EnsureSubscribed(second);

        Assert.AreEqual(0, first.Handlers, "the old settings object should have been detached");
        Assert.AreEqual(1, second.Handlers, "the new settings object should be attached exactly once");
    }

    // MCM absent, or not yet registered. Must not throw, must not subscribe, and must leave a
    // breadcrumb: the symptom of a silent miss here is indistinguishable from the bug this class
    // fixes, so a log line is the only way anyone could diagnose it.
    [TestMethod]
    public void EnsureSubscribed_NullSettings_DoesNotSubscribeAndWarns()
    {
        var logger = Substitute.For<IModLogger>();
        var sut = new AiPartySizeSettingsWatcher(logger);

        sut.EnsureSubscribed(null);
        sut.EnsureSubscribed(null);

        logger.Received(2).LogWarning(Arg.Is<string>(m => m.Contains("[AiPartySize]")));
    }

    // A null settings object must not wipe an attachment the watcher already holds.
    [TestMethod]
    public void EnsureSubscribed_NullAfterASuccessfulAttach_KeepsTheExistingHandler()
    {
        var sut = new AiPartySizeSettingsWatcher(Substitute.For<IModLogger>());
        var settings = new CountingSettings();

        sut.EnsureSubscribed(settings);
        sut.EnsureSubscribed(null);

        Assert.AreEqual(1, settings.Handlers, "a null attach must not detach the live subscription");
    }

    /// <summary>
    /// Minimal BaseSettings stub that counts subscribe/unsubscribe. BaseSettings declares
    /// PropertyChanged as `public virtual event`, so overriding the accessors is the supported way to
    /// observe attachment without reflection over a compiler-generated delegate field.
    /// </summary>
    private sealed class CountingSettings : BaseSettings
    {
        private PropertyChangedEventHandler _handlers;

        public int Handlers { get; private set; }

        public override string Id => "TAOM.Test.CountingSettings";
        public override string DisplayName => "Counting Settings";

        public override event PropertyChangedEventHandler PropertyChanged
        {
            add { _handlers += value; Handlers++; }
            remove { _handlers -= value; Handlers--; }
        }
    }
}
