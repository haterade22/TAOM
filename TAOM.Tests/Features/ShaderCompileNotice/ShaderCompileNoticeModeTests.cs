using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ShaderCompileNotice;

namespace TAOM.Tests.Features.ShaderCompileNotice;

[TestClass]
public class ShaderCompileNoticeModeTests
{
    private const string Line = "[15:09:12.001] compile_shader: $BASE/Shaders/Sources/pbr_metallic_gbuffer.rs, main_ps, ps_5_0, 3, 1, 2, 1.\n";

    private static ShaderCompileProgress ProgressWith(int compiles)
    {
        var progress = new ShaderCompileProgress();
        for (int i = 0; i < compiles; i++) progress.Feed(Line);
        return progress;
    }

    [TestMethod]
    public void StartPosition_Startup_ReadsTheLogFromItsStart()
    {
        Assert.AreEqual(0L, ShaderCompileNoticeMode.Startup.StartPosition(1_540_232L));
    }

    [TestMethod]
    public void StartPosition_Battle_SkipsEverythingAlreadyInTheLog()
    {
        Assert.AreEqual(1_540_232L, ShaderCompileNoticeMode.Battle.StartPosition(1_540_232L));
    }

    [TestMethod]
    public void StartPosition_BattleWithNoLogYet_StartsAtZero()
    {
        Assert.AreEqual(0L, ShaderCompileNoticeMode.Battle.StartPosition(-1L));
    }

    [TestMethod]
    public void ShouldShow_StartupBelowTenCompiles_StaysHiddenHoweverLong()
    {
        Assert.IsFalse(ShaderCompileNoticeMode.Startup.ShouldShow(ProgressWith(9), TimeSpan.FromMinutes(5)));
    }

    [TestMethod]
    public void ShouldShow_StartupAtTenCompiles_ShowsAtOnce()
    {
        Assert.IsTrue(ShaderCompileNoticeMode.Startup.ShouldShow(ProgressWith(10), TimeSpan.Zero));
    }

    [TestMethod]
    public void ShouldShow_BattleBeforeOneSecond_StaysHiddenEvenWithCompiles()
    {
        Assert.IsFalse(ShaderCompileNoticeMode.Battle.ShouldShow(ProgressWith(40), TimeSpan.FromMilliseconds(999)));
    }

    [TestMethod]
    public void ShouldShow_BattleAfterOneSecond_ShowsWithNoCompileCountedYet()
    {
        Assert.IsTrue(ShaderCompileNoticeMode.Battle.ShouldShow(ProgressWith(0), TimeSpan.FromSeconds(1)));
    }

    [TestMethod]
    public void ShouldCloseIdle_Startup_NeverClosesForSilence()
    {
        Assert.IsFalse(ShaderCompileNoticeMode.Startup.ShouldCloseIdle(shown: true, TimeSpan.FromMinutes(10)));
    }

    [TestMethod]
    public void ShouldCloseIdle_BattleShownAndSilentFor29Seconds_StaysOpen()
    {
        Assert.IsFalse(ShaderCompileNoticeMode.Battle.ShouldCloseIdle(shown: true, TimeSpan.FromSeconds(29)));
    }

    [TestMethod]
    public void ShouldCloseIdle_BattleShownAndSilentFor30Seconds_Closes()
    {
        Assert.IsTrue(ShaderCompileNoticeMode.Battle.ShouldCloseIdle(shown: true, TimeSpan.FromSeconds(30)));
    }

    [TestMethod]
    public void ShouldCloseIdle_BattleNotShownYet_StaysOpen()
    {
        Assert.IsFalse(ShaderCompileNoticeMode.Battle.ShouldCloseIdle(shown: false, TimeSpan.FromMinutes(10)));
    }

    [TestMethod]
    public void Startup_KeepsItsBehaviour()
    {
        var mode = ShaderCompileNoticeMode.Startup;

        Assert.AreEqual("ShaderNotice", mode.LogTag);
        Assert.IsTrue(mode.LowerPriority);
        Assert.IsTrue(mode.UseRememberedTotal);
        Assert.AreEqual(TimeSpan.FromMinutes(30), mode.GiveUpAfter);
    }

    [TestMethod]
    public void Battle_LeavesPriorityAndTotalAloneAndGivesUpSooner()
    {
        var mode = ShaderCompileNoticeMode.Battle;

        Assert.AreEqual("BattleShaderNotice", mode.LogTag);
        Assert.IsFalse(mode.LowerPriority);
        Assert.IsFalse(mode.UseRememberedTotal);
        Assert.AreEqual(TimeSpan.FromMinutes(15), mode.GiveUpAfter);
    }
}
