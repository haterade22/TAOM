using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.StoryMissions;

namespace TAOM.Tests.Features.StoryMissions;

/// <summary>A story mission hosts a Long Defeat run; the run's own check lines must not land on the ids #1079's manifest reads.</summary>
[TestClass]
public class HostedRunOutcomeLogTests
{
    private readonly IOutcomeLog _inner = Substitute.For<IOutcomeLog>();

    private HostedRunOutcomeLog Sut() => new(_inner);

    [TestMethod]
    public void Pass_ALongDefeatId_IsWrittenUnderTheHostedPrefix()
    {
        Sut().Pass("taom.long-defeat.boot", "story1", "deployment_finished=true");

        _inner.Received(1).Pass("taom.story-missions.hosted.boot", "story1", "deployment_finished=true");
        _inner.DidNotReceive().Pass("taom.long-defeat.boot", Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void BootId_IsWhereTheLongDefeatsBootCheckLands()
    {
        Sut().Pass(LongDefeatChecks.BootId, "story1", "d");

        _inner.Received(1).Pass(HostedRunOutcomeLog.BootId, "story1", "d");
    }

    [TestMethod]
    public void Fail_ALongDefeatId_IsWrittenUnderTheHostedPrefix()
    {
        Sut().Fail("taom.long-defeat.end", "story1", "reason=fault");

        _inner.Received(1).Fail("taom.story-missions.hosted.end", "story1", "reason=fault");
    }

    [TestMethod]
    public void Skip_ALongDefeatId_IsWrittenUnderTheHostedPrefix()
    {
        Sut().Skip("taom.long-defeat.wave", "story1", "why");

        _inner.Received(1).Skip("taom.story-missions.hosted.wave", "story1", "why");
    }

    [TestMethod]
    public void Pass_AHyphenatedLongDefeatId_KeepsItsTail()
    {
        Sut().Pass("taom.long-defeat.best-run", "story1", "d");

        _inner.Received(1).Pass("taom.story-missions.hosted.best-run", "story1", "d");
    }

    [TestMethod]
    public void Pass_AnIdOfAnotherFeature_IsForwardedUnchanged()
    {
        Sut().Pass("taom.story-missions.ended", "story1", "d");
        Sut().Pass("taom.long-defeats.x", "story1", "d");

        _inner.Received(1).Pass("taom.story-missions.ended", "story1", "d");
        _inner.Received(1).Pass("taom.long-defeats.x", "story1", "d");
    }

    [TestMethod]
    public void EveryRewrittenId_StillMatchesTheOutcomeLogsIdPattern()
    {
        foreach (var id in new[] { "taom.long-defeat.boot", "taom.long-defeat.best-run", "taom.long-defeat.early-start" })
        {
            string? seen = null;
            var inner = Substitute.For<IOutcomeLog>();
            inner.When(i => i.Pass(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())).Do(c => seen = c.ArgAt<string>(0));

            new HostedRunOutcomeLog(inner).Pass(id, "story1", "d");

            Assert.IsTrue(OutcomeLog.IsValidId(seen), id + " -> " + seen);
        }
    }

    [TestMethod]
    public void Counter_Swallowed_AndWriteSummary_AreForwardedUnchanged()
    {
        var counter = new OutcomeCounter("taom.long-defeat.morale-hold");
        _inner.Counter("taom.long-defeat.morale-hold").Returns(counter);
        var ex = new InvalidOperationException();

        var got = Sut().Counter("taom.long-defeat.morale-hold");
        Sut().Swallowed("LongDefeat", "site", ex);
        Sut().WriteSummary("exit");

        Assert.AreSame(counter, got);
        _inner.Received(1).Swallowed("LongDefeat", "site", ex);
        _inner.Received(1).WriteSummary("exit");
    }
}
