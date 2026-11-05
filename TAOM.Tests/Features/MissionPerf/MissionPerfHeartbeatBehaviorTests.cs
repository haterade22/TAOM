using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.MissionPerf.Hooks;

namespace TAOM.Tests.Features.MissionPerf;

/// <summary>
/// The heartbeat's inner guard: the busy-time reads (<c>Utilities.GetMainFps</c>) throw in a test process,
/// where the engine's utility interface is null, and that must cost one <c>na</c> field, never the
/// heartbeat. Constructs a <c>MissionLogic</c>, so it needs the game assemblies.
/// </summary>
[TestCategory("RequiresGame")]
[TestClass]
public class MissionPerfHeartbeatBehaviorTests
{
    [TestMethod]
    public void OnMissionTick_WhenTheBusyTimeReadThrows_DoesNotDisableTheHeartbeat()
    {
        var logger = Substitute.For<IModLogger>();
        var sut = new MissionPerfHeartbeatBehavior(logger);
        sut.OnCreated();

        // The first tick only stamps the clock; the second records a frame and reads the busy time.
        sut.OnMissionTick(0.016f);
        sut.OnMissionTick(0.016f);

        var errors = logger.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IModLogger.LogError))
            .Select(c => (string)c.GetArguments()[0]!)
            .ToArray();
        Assert.AreEqual(0, errors.Length, string.Join(" | ", errors));
    }
}
