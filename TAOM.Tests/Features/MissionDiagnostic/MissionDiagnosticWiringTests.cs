using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.MissionDiagnostic;

[TestClass]
public class MissionDiagnosticWiringTests
{
    // Every game init logs the date once the clock exists, the load that freezes before a mission included,
    // so the call sits before the once-per-process patch guard.
    [TestMethod]
    public void OnGameInitializationFinished_LogsTheCampaignDate_BeforeTheOncePerProcessGuard()
    {
        var src = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);
        int method = src.IndexOf("public override void OnGameInitializationFinished(", StringComparison.Ordinal);
        int call = src.IndexOf("IMissionDiagnosticService>()?.LogCampaignDate();", StringComparison.Ordinal);
        int guard = src.IndexOf("if (_gameInitPatchesApplied) return;", StringComparison.Ordinal);

        Assert.IsTrue(method >= 0 && call > method && guard > call, $"method={method} call={call} guard={guard}");
    }
}
