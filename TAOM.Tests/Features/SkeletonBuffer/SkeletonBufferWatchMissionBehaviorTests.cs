// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), frame-buffer-guard and frame-buffer-watch.
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.CoopInterop;
using TAOM.Features.SkeletonBuffer;
using TAOM.Features.SkeletonBuffer.Hooks;
using TAOM.Tests.Features.LoadTimeStamps;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.SkeletonBuffer;

/// <summary>
/// The thin entry point: the engine's <c>AfterStart</c>, <c>OnMissionTick</c> and <c>OnRemoveBehavior</c> reach the
/// watch service. The service is sealed, so the tests drive the real one over faked adapters and read what it asked of
/// them. The behavior derives from an engine type, so the class needs the game assemblies; with no <c>Mission</c> set
/// the behavior counts zero agents.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class SkeletonBufferWatchMissionBehaviorTests
{
    private const long Global = 0x7FF600D9D160;
    private const long Pointer = 0x1D000000000;

    private static bool _gameLoaded;
    private ISkeletonBufferGuardService _guard = null!;
    private ISkeletonBufferMemoryAdapter _memory = null!;
    private ISkeletonBufferSettingsProvider _settings = null!;
    private RecordingLogger _logger = null!;
    private SkeletonBufferWatchMissionBehavior _sut = null!;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestInitialize]
    public void Setup()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        _guard = Substitute.For<ISkeletonBufferGuardService>();
        _guard.Target.Returns(new SkeletonBufferTarget(Global, pool1Guarded: true, pool1CounterAddress: 0));
        _memory = Substitute.For<ISkeletonBufferMemoryAdapter>();
        _memory.ReadInt64(Global).Returns(Pointer);
        _memory.ReadInt32(Arg.Any<long>()).Returns(1234);
        _settings = Substitute.For<ISkeletonBufferSettingsProvider>();
        _settings.WatchEnabled.Returns(true);
        _logger = new RecordingLogger();
        var watch = new SkeletonBufferWatchService(_guard, _memory, Substitute.For<ISkeletonBufferEngineAdapter>(), _settings,
            Substitute.For<IDedicatedServerProvider>(), _logger);
        _sut = new SkeletonBufferWatchMissionBehavior(watch);
    }

    [TestMethod]
    public void AfterStart_BeginsTheWatch()
    {
        _sut.AfterStart();

        _ = _guard.Received(1).Target;
        _ = _settings.Received(1).WatchEnabled;
    }

    [TestMethod]
    public void OnMissionTick_BeforeAfterStart_ReadsNothing()
    {
        _sut.OnMissionTick(0.016f);

        _memory.DidNotReceiveWithAnyArgs().ReadInt64(default);
    }

    [TestMethod]
    public void OnMissionTick_AfterAfterStart_TicksTheWatch()
    {
        _sut.AfterStart();

        _sut.OnMissionTick(0.016f);

        _memory.Received(1).ReadInt64(Global);
    }

    [TestMethod]
    public void OnRemoveBehavior_EndsTheWatchWithItsPeakLineOnce()
    {
        _sut.AfterStart();
        _sut.OnMissionTick(0.016f);

        _sut.OnRemoveBehavior();
        _sut.OnRemoveBehavior();

        Assert.AreEqual(1, _logger.Lines.Count(l => l.Contains("mission peak: 1234 of 65536")));
    }

    [TestMethod]
    public void OnMissionTick_ANewPeak_AsksForTheAgentCountAndWithNoMissionGetsZero()
    {
        _sut.AfterStart();
        _sut.OnMissionTick(0.016f);

        _sut.OnRemoveBehavior();

        StringAssert.Contains(_logger.Lines.Single(l => l.Contains("mission peak")), ", 0 agents,");
    }
}
