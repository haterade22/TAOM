using System;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>The wiring of the mission heroes' lines (#1569): the host hands the director the field as its voice, and the check line is built the way the judge reads it.</summary>
[TestClass]
public class LongDefeatHeroLinePinTests
{
    [TestMethod]
    public void TheHost_HandsTheDirectorTheSameInstanceAsTheVoiceAdapter()
    {
        var plan = new RunPlan("helms", new[] { new GarrisonRow("t", FormationSlot.Infantry, 1) },
            new WavePlan(new[] { new WaveSpec(110, new[] { new WaveTroop("a", FormationSlot.Infantry, 1) }) }, new WaveScaling(10, 300)),
            new ShopPlan(0, Array.Empty<ShopOffer>()), RunRules.Default, RunOptions.Default);
        var field = new FakeLongDefeatField();
        var host = new LongDefeatRunHost(plan, new LongDefeatSession(), Substitute.For<IModLogger>(), new RecordingOutcomeLog(), new FakeLongDefeatMenu().EndGame, "m1", () => field);
        MissionThreadGuard.ResetForTests();

        host.MarkStartDue();
        host.Step(0.1f, () => 1f);

        var director = typeof(LongDefeatRunHost).GetField("_director", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(host);
        Assert.IsNotNull(director, "the host built its director");
        var voice = typeof(LongDefeatDirector).GetField("_voice", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(director);
        Assert.AreSame(field, voice, "the voice adapter is the field itself, not a second object");
        MissionThreadGuard.ResetForTests();
    }

    [TestMethod]
    public void Line_Played_PassesAndNamesTheHeroAndTheFileOnly()
    {
        var verdict = LongDefeatHeroChecks.Line("legolas", "E:/Modules/TAOM/ModuleSounds/LOTR/Heroes/Legolas/legolas_backstory_a_01.ogg", null);

        Assert.AreEqual(CheckResult.Pass, verdict.Result);
        Assert.AreEqual("hero=legolas file=legolas_backstory_a_01.ogg", verdict.Detail);
    }

    [TestMethod]
    public void Line_WithAProblem_FailsAndCarriesIt()
    {
        var verdict = LongDefeatHeroChecks.Line("legolas", "C:\\m\\a_01.ogg", "refused");

        Assert.AreEqual(CheckResult.Fail, verdict.Result);
        Assert.AreEqual("hero=legolas file=a_01.ogg problem=refused", verdict.Detail);
    }

    [TestMethod]
    public void TheHeroLineId_IsTheOneTheManifestNames()
    {
        Assert.AreEqual("taom.long-defeat.hero-line", LongDefeatHeroChecks.LineId);
    }

    [TestMethod]
    public void TheAdapter_ImplementsTheVoiceInterface_AndMakesNoSoundOnADedicatedServer()
    {
        var source = RepoPaths.ReadSource("Main/Adapters/LongDefeatBattlefieldAdapter.HeroVoice.cs", stripComments: true);

        StringAssert.Contains(source, "partial class LongDefeatBattlefieldAdapter : ILongDefeatHeroVoiceAdapter");
        Assert.IsTrue(source.IndexOf("GameNetwork.IsDedicatedServer", StringComparison.Ordinal) < source.IndexOf("CreateEventFromExternalFile", StringComparison.Ordinal),
            "the dedicated-server check comes before the sound is created");
        Assert.IsFalse(source.Contains(".Release("), "Stop, never Release, on a playing event");
    }

    [TestMethod]
    public void Shutdown_StopsTheLineBeforeAnyStepThatCanThrow()
    {
        var source = RepoPaths.ReadSource("Main/Features/LongDefeat/LongDefeatDirector.cs", stripComments: true);
        var body = source.Substring(source.IndexOf("public void Shutdown()", StringComparison.Ordinal));
        var stop = body.IndexOf("StopHeroVoice();", StringComparison.Ordinal);
        var drain = body.IndexOf("_deferred.Drain();", StringComparison.Ordinal);

        Assert.IsTrue(stop >= 0 && drain >= 0, "Shutdown no longer holds the two calls this pin reads");
        Assert.IsTrue(stop < drain, "the host drops its director before Shutdown, so a throw in the drain, the report or a release would leave the line playing with nobody left to stop it");
    }
}
