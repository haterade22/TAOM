using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.MapViewRelease;

namespace TAOM.Tests.Features.MapViewRelease;

[TestClass]
public class MapViewReleaseServiceTests
{
    private IMapViewReleaseSettingsProvider _settings = null!;
    private IMapViewReleaseAdapter _adapter = null!;
    private IModLogger _logger = null!;
    private MapViewReleaseService _sut = null!;
    private readonly object _mapLayer = new object();
    private readonly object _otherLayer = new object();

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<IMapViewReleaseSettingsProvider>();
        _settings.ReleaseEnabled.Returns(true);
        _settings.ReleaseInterval.Returns(3);
        _adapter = Substitute.For<IMapViewReleaseAdapter>();
        _adapter.IsCampaignRunning.Returns(true);
        _adapter.IsMapSceneLayer(_mapLayer).Returns(true);
        _adapter.IsMapSceneLayer(_otherLayer).Returns(false);
        _logger = Substitute.For<IModLogger>();
        _sut = new MapViewReleaseService(_settings, _adapter, _logger);
    }

    // A cover is the map layer deactivating when a screen opens over it; the release itself waits for the layer's return.
    private void Deactivate(int times)
    {
        for (var i = 0; i < times; i++) _sut.OnSceneLayerDeactivated(_mapLayer);
    }

    // A cover and the return to the map, `times` over.
    private void Cover(int times)
    {
        for (var i = 0; i < times; i++)
        {
            _sut.OnSceneLayerDeactivated(_mapLayer);
            _sut.OnSceneLayerActivated(_mapLayer);
        }
    }

    private List<string> Lines(string method) =>
        _logger.ReceivedCalls().Where(c => c.GetMethodInfo().Name == method)
            .Select(c => (string)c.GetArguments()[0]!).ToList();

    private List<string> Infos() => Lines(nameof(IModLogger.LogInfo));

    private List<string> Errors() => Lines(nameof(IModLogger.LogError));

    // ---- The checks every other layer meets first ----

    [DataTestMethod]
    [DataRow(nameof(MapViewReleaseService.OnSceneLayerDeactivated))]
    [DataRow(nameof(MapViewReleaseService.OnSceneLayerActivated))]
    public void SceneLayerEdges_AllocateNoClosureObject_SinceTheyRunForEverySceneLayerTheGameChanges(string name)
    {
        // A lambda over a local makes the compiler build a display-class object when the scope is entered, before the first
        // early return, so every layer change would allocate. The log lambda may capture `this` only.
        var method = typeof(MapViewReleaseService).GetMethod(name)!;
        var il = method.GetMethodBody()!.GetILAsByteArray()!;
        var created = TAOM.Tests.Migration.IlCallScanner.ExtractCalledMethods(method, il)
            .Where(m => m.IsConstructor && m.DeclaringType != null && m.DeclaringType.Name.Contains("DisplayClass"))
            .ToList();

        Assert.AreEqual(0, created.Count, "closure classes created: " + string.Join(", ", created.Select(m => m.DeclaringType!.Name)));
    }

    [TestMethod]
    public void OnSceneLayerDeactivated_ToggleOff_TouchesNoEngineMember()
    {
        _settings.ReleaseEnabled.Returns(false);

        Cover(5);

        _ = _adapter.DidNotReceive().IsCampaignRunning;
        _adapter.DidNotReceive().IsMapSceneLayer(Arg.Any<object>());
        _adapter.DidNotReceive().ReleaseSceneView(Arg.Any<object>());
    }

    [TestMethod]
    public void OnSceneLayerDeactivated_NoCampaign_NeverAsksAboutTheMapScreen()
    {
        _adapter.IsCampaignRunning.Returns(false);

        Cover(5);

        _adapter.DidNotReceive().IsMapSceneLayer(Arg.Any<object>());
        _adapter.DidNotReceive().ReleaseSceneView(Arg.Any<object>());
    }

    [TestMethod]
    public void OnSceneLayerDeactivated_ALayerThatIsNotTheMaps_ReleasesNothingAndCountsNothing()
    {
        for (var i = 0; i < 10; i++) _sut.OnSceneLayerDeactivated(_otherLayer);

        _adapter.DidNotReceive().ReleaseSceneView(Arg.Any<object>());

        // none of those counted: the map's own covers start from one
        Cover(2);
        _adapter.DidNotReceive().ReleaseSceneView(Arg.Any<object>());
        Cover(1);
        _adapter.Received(1).ReleaseSceneView(_mapLayer);
    }

    // ---- The interval ----

    [TestMethod]
    public void CoverAndReturn_TheMapLayer_ReleasesOnEveryNthCover()
    {
        Cover(2);
        _adapter.DidNotReceive().ReleaseSceneView(Arg.Any<object>());

        Cover(1);
        _adapter.Received(1).ReleaseSceneView(_mapLayer);

        Cover(3);
        _adapter.Received(2).ReleaseSceneView(_mapLayer);
    }

    [TestMethod]
    public void CoverAndReturn_IntervalChangedLive_AppliesAtTheNextCover()
    {
        Cover(2);
        _settings.ReleaseInterval.Returns(2);

        Cover(1);

        _adapter.Received(1).ReleaseSceneView(_mapLayer);
    }

    [TestMethod]
    public void OnSceneLayerDeactivated_ToggleSwitchedOffThenOn_CountsOnlyTheCoversWhileOn()
    {
        Cover(2);
        _settings.ReleaseEnabled.Returns(false);
        Cover(10);
        _settings.ReleaseEnabled.Returns(true);
        _adapter.DidNotReceive().ReleaseSceneView(Arg.Any<object>());

        Cover(1);

        _adapter.Received(1).ReleaseSceneView(_mapLayer);
    }

    // ---- The release waits for the map's own return ----

    [TestMethod]
    public void OnSceneLayerDeactivated_ADueCover_ReleasesNothingAndLogsNothing()
    {
        Deactivate(3);

        _adapter.DidNotReceive().ReleaseSceneView(Arg.Any<object>());
        Assert.AreEqual(0, Infos().Count);
    }

    [TestMethod]
    public void OnSceneLayerActivated_TheDueLayerComesBack_ReleasesOnceAndLogs()
    {
        Deactivate(3);

        _sut.OnSceneLayerActivated(_mapLayer);

        _adapter.Received(1).ReleaseSceneView(Arg.Is<object>(o => ReferenceEquals(o, _mapLayer)));
        CollectionAssert.AreEqual(new[] { "[MapViewRelease] released 1 times, map covered 3 times" }, Infos());

        _sut.OnSceneLayerActivated(_mapLayer);
        _adapter.Received(1).ReleaseSceneView(Arg.Any<object>());
    }

    [TestMethod]
    public void OnSceneLayerActivated_NothingDue_DoesNothingAndMakesNoAdapterCall()
    {
        Deactivate(2);   // counted, not due
        _adapter.ClearReceivedCalls();

        _sut.OnSceneLayerActivated(_mapLayer);
        _sut.OnSceneLayerActivated(_otherLayer);

        Assert.AreEqual(0, _adapter.ReceivedCalls().Count());
        Assert.AreEqual(0, Infos().Count);
    }

    [TestMethod]
    public void OnSceneLayerActivated_AnotherLayerWhileACampaignRuns_KeepsTheReleaseDue()
    {
        Deactivate(3);

        _sut.OnSceneLayerActivated(_otherLayer);
        _adapter.DidNotReceive().ReleaseSceneView(Arg.Any<object>());

        _sut.OnSceneLayerActivated(_mapLayer);
        _adapter.Received(1).ReleaseSceneView(_mapLayer);
    }

    [TestMethod]
    public void OnSceneLayerActivated_AnotherLayerAfterTheCampaignEnded_DropsTheDueRelease()
    {
        Deactivate(3);
        _adapter.IsCampaignRunning.Returns(false);

        _sut.OnSceneLayerActivated(_otherLayer);
        _adapter.IsCampaignRunning.Returns(true);
        _sut.OnSceneLayerActivated(_mapLayer);

        _adapter.DidNotReceive().ReleaseSceneView(Arg.Any<object>());
    }

    [TestMethod]
    public void OnSceneLayerActivated_ANewMapLayerWhileAnOldOneIsDue_DropsTheOldMark()
    {
        // Save/Load opened from the map is a cover; loading builds a new map screen with a new scene layer.
        var newMapLayer = new object();
        _adapter.IsMapSceneLayer(newMapLayer).Returns(true);
        Deactivate(3);

        _sut.OnSceneLayerActivated(newMapLayer);
        _sut.OnSceneLayerActivated(_mapLayer);

        _adapter.DidNotReceive().ReleaseSceneView(Arg.Any<object>());
    }

    [TestMethod]
    public void OnSceneLayerActivated_ToggleSwitchedOffBetweenCoverAndReturn_ReleasesNothing()
    {
        Deactivate(3);
        _settings.ReleaseEnabled.Returns(false);

        _sut.OnSceneLayerActivated(_mapLayer);
        _settings.ReleaseEnabled.Returns(true);
        _sut.OnSceneLayerActivated(_mapLayer);

        _adapter.DidNotReceive().ReleaseSceneView(Arg.Any<object>());
        Assert.AreEqual(0, Infos().Count);
    }

    [TestMethod]
    public void OnSceneLayerActivated_ADroppedDueRelease_IsNotCountedAsReleased()
    {
        Deactivate(3);                              // release 1 is due...
        _settings.ReleaseEnabled.Returns(false);
        _sut.OnSceneLayerActivated(_mapLayer);      // ...and dropped: the toggle went off before the return
        _settings.ReleaseEnabled.Returns(true);

        Cover(3);                                   // the next due cover releases

        CollectionAssert.AreEqual(new[] { "[MapViewRelease] released 1 times, map covered 6 times" }, Infos());
        _adapter.Received(1).ReleaseSceneView(_mapLayer);
    }

    [TestMethod]
    public void OnSceneLayerActivated_TheLayerIsNoLongerTheMaps_ReleasesNothing()
    {
        Deactivate(3);
        _adapter.IsMapSceneLayer(_mapLayer).Returns(false);

        _sut.OnSceneLayerActivated(_mapLayer);

        _adapter.DidNotReceive().ReleaseSceneView(Arg.Any<object>());
    }

    [TestMethod]
    public void OnSceneLayerActivated_TheReleaseThrows_LogsOneErrorAndSwitchesOff()
    {
        _adapter.When(a => a.ReleaseSceneView(Arg.Any<object>())).Do(_ => throw new InvalidOperationException("boom in ClearAll"));
        Deactivate(3);

        _sut.OnSceneLayerActivated(_mapLayer);
        Cover(9);

        var errors = Errors();
        Assert.AreEqual(1, errors.Count);
        StringAssert.Contains(errors[0], "boom in ClearAll");
        _adapter.Received(1).ReleaseSceneView(Arg.Any<object>());
        Assert.AreEqual(0, Infos().Count, "no release line for a release that threw");
    }

    [TestMethod]
    public void OnSceneLayerActivated_TheLayerTestThrows_SwitchesOff()
    {
        Deactivate(3);
        _adapter.IsMapSceneLayer(Arg.Any<object>()).Throws(new TypeLoadException("MapScreen"));

        _sut.OnSceneLayerActivated(_mapLayer);
        _sut.OnSceneLayerActivated(_mapLayer);

        Assert.AreEqual(1, Errors().Count);
    }

    // ---- The log ----

    [TestMethod]
    public void CoverAndReturn_FirstRelease_LogsTheCounts()
    {
        Cover(3);

        CollectionAssert.AreEqual(new[] { "[MapViewRelease] released 1 times, map covered 3 times" }, Infos());
    }

    [TestMethod]
    public void CoverAndReturn_NextReleasesAreQuietUntilTheTwentieth()
    {
        Cover(3);                // release 1: logged
        Cover(3 * 18);           // releases 2 to 19
        Assert.AreEqual(1, Infos().Count, "releases 2 to 19 write nothing");

        Cover(3);                // release 20

        var infos = Infos();
        Assert.AreEqual(2, infos.Count);
        Assert.AreEqual("[MapViewRelease] released 20 times, map covered 60 times", infos[1]);
    }

    [TestMethod]
    public void CoverAndReturn_TheLoggerThrows_StillReleasesAndKeepsGoing()
    {
        _logger.When(l => l.LogInfo(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("log"));

        Cover(6);

        _adapter.Received(2).ReleaseSceneView(_mapLayer);
        Assert.AreEqual(0, Errors().Count, "a failed log line is not a reason to switch off");
    }

    // ---- The error path ----

    [TestMethod]
    public void CoverAndReturn_TheReleaseThrows_LogsOneErrorAndSwitchesOff()
    {
        _adapter.When(a => a.ReleaseSceneView(Arg.Any<object>())).Do(_ => throw new InvalidOperationException("boom in ClearAll"));

        Cover(3);          // the release that throws
        Cover(9);          // would have released three more times

        var errors = Errors();
        Assert.AreEqual(1, errors.Count, "one line, not one per cover");
        StringAssert.StartsWith(errors[0], "[MapViewRelease] OFF after an error, the map view is no longer released for the rest of this game launch: ");
        StringAssert.Contains(errors[0], "boom in ClearAll");
        _adapter.Received(1).ReleaseSceneView(Arg.Any<object>());
    }

    [TestMethod]
    public void CoverAndReturn_AfterAnError_NothingIsAskedOfTheEngineAgain()
    {
        _adapter.When(a => a.ReleaseSceneView(Arg.Any<object>())).Do(_ => throw new InvalidOperationException("boom"));
        Cover(3);
        _adapter.ClearReceivedCalls();

        _sut.OnSceneLayerDeactivated(_otherLayer);
        Cover(3);

        Assert.AreEqual(0, _adapter.ReceivedCalls().Count());
    }

    [TestMethod]
    public void OnSceneLayerDeactivated_TheLayerTestThrows_SwitchesOff()
    {
        _adapter.IsMapSceneLayer(Arg.Any<object>()).Throws(new TypeLoadException("MapScreen"));

        _sut.OnSceneLayerDeactivated(_mapLayer);
        _sut.OnSceneLayerDeactivated(_mapLayer);

        Assert.AreEqual(1, Errors().Count);
        _adapter.Received(1).IsMapSceneLayer(Arg.Any<object>());
    }

    [TestMethod]
    public void OnSceneLayerDeactivated_TheSettingsThrow_SwitchesOff()
    {
        var throwing = true;
        _settings.ReleaseEnabled.Returns(_ => throwing ? throw new InvalidOperationException("mcm") : true);

        _sut.OnSceneLayerDeactivated(_mapLayer);
        throwing = false;
        Cover(3);

        Assert.AreEqual(1, Errors().Count);
        _adapter.DidNotReceive().ReleaseSceneView(Arg.Any<object>());
    }

    [TestMethod]
    public void CoverAndReturn_TheLoggerThrowsWhileSwitchingOff_DoesNotThrow()
    {
        _adapter.When(a => a.ReleaseSceneView(Arg.Any<object>())).Do(_ => throw new InvalidOperationException("boom"));
        _logger.When(l => l.LogError(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("log"));

        Cover(3);

        _adapter.Received(1).ReleaseSceneView(Arg.Any<object>());
    }

    // ---- Install ----

    [TestMethod]
    public void LogInstall_ToggleOn_WritesTheOnLineWithTheInterval()
    {
        _settings.ReleaseInterval.Returns(20);

        _sut.LogInstall();

        CollectionAssert.AreEqual(
            new[] { "[MapViewRelease] ON: the campaign map's view releases its render targets once per 20 covers (toggle on)" },
            Infos());
    }

    [TestMethod]
    public void LogInstall_IntervalOne_SaysEveryCover()
    {
        _settings.ReleaseInterval.Returns(1);

        _sut.LogInstall();

        CollectionAssert.AreEqual(
            new[] { "[MapViewRelease] ON: the campaign map's view releases its render targets on every cover (toggle on)" },
            Infos());
    }

    [TestMethod]
    public void LogInstall_ToggleOff_SaysSo()
    {
        _settings.ReleaseEnabled.Returns(false);

        _sut.LogInstall();

        CollectionAssert.AreEqual(
            new[] { "[MapViewRelease] installed, toggle off: closed menus leave the map's render targets alone as in the vanilla game" },
            Infos());
    }

    [TestMethod]
    public void LogInstall_TheSettingsThrow_DoesNotThrow()
    {
        _settings.ReleaseEnabled.Throws(new InvalidOperationException("mcm"));

        _sut.LogInstall();

        Assert.AreEqual(1, Infos().Count);
    }

    [TestMethod]
    public void LogInstall_TheLoggerThrows_DoesNotThrow()
    {
        _logger.When(l => l.LogInfo(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("log"));

        _sut.LogInstall();
    }
}
