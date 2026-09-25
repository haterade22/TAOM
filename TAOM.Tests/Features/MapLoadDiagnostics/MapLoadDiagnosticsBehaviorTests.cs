using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.MapLoadDiagnostics;

namespace TAOM.Tests.Features.MapLoadDiagnostics;

[TestClass]
[TestCategory("RequiresGame")]
public class MapLoadDiagnosticsBehaviorTests
{
    [TestMethod]
    public void OnSessionLaunched_ResetsTheHeartbeatBaseline()
    {
        var heartbeat = Substitute.For<IMapLoadHeartbeatService>();
        var sut = new MapLoadDiagnosticsBehavior(heartbeat);

        sut.OnSessionLaunched(null);

        heartbeat.Received(1).ResetForNewSession();
    }
}
