using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.TrollBruteForce;

namespace TAOM.Tests.Features.TrollBruteForce;

/// <summary>
/// The [TrollClips] line: once per Monster and action in a mission, which clip the action played and whether it is
/// one of TAOM's troll clips or a vanilla one. It exists to show in game that the self-keyed troll swings play
/// (2026-09-26): a crash-free battle proves only that their melee attack table rows exist.
/// </summary>
[TestClass]
public class TrollClipLogTests
{
    [TestMethod]
    public void FirstPlay_ANewAction_ReturnsTheLineNamingTheClip()
    {
        var log = new TrollClipLog();

        string? line = log.FirstPlay("hill_troll", "act_release_overswing_2h", "anim_hill_troll_release_overswing_2h");

        Assert.AreEqual("[TrollClips] hill_troll: act_release_overswing_2h -> anim_hill_troll_release_overswing_2h " +
            "(troll clip, melee table)", line);
    }

    [TestMethod]
    public void FirstPlay_TheSameMonsterAndActionAgain_ReturnsNull()
    {
        var log = new TrollClipLog();
        log.FirstPlay("hill_troll", "act_walk_forward_unarmed", "anim_hill_troll_walk1");

        Assert.IsNull(log.FirstPlay("hill_troll", "act_walk_forward_unarmed", "anim_hill_troll_walk1"));
    }

    [TestMethod]
    public void FirstPlay_TheSameActionOnTheOtherTroll_IsLoggedForEach()
    {
        var log = new TrollClipLog();
        log.FirstPlay("hill_troll", "act_ready_overswing_2h", "anim_hill_troll_ready_overswing_2h");

        Assert.IsNotNull(log.FirstPlay("cave_troll", "act_ready_overswing_2h", "ready_overswing_2h"));
    }

    [TestMethod]
    public void FirstPlay_AVanillaClip_IsMarkedVanilla()
    {
        var log = new TrollClipLog();

        string? line = log.FirstPlay("cave_troll", "act_quick_blocked_slashleft_2h", "quick_blocked_slashleft_2h");

        StringAssert.EndsWith(line, "(vanilla clip, melee table)");
    }

    [TestMethod]
    public void FirstPlay_AVanillaClipNamedAnim_IsStillVanilla()
    {
        // vanilla ships clips such as anim_cutscene_break_chains_short: only TAOM's troll prefixes mark a troll clip
        var log = new TrollClipLog();

        string? line = log.FirstPlay("hill_troll", "act_cutscene_break_chains_short", "anim_cutscene_break_chains_short");

        StringAssert.EndsWith(line, "(vanilla clip)");
    }

    [TestMethod]
    public void FirstPlay_ACaveTrollFabClip_IsATrollClip()
    {
        var log = new TrollClipLog();

        StringAssert.EndsWith(log.FirstPlay("cave_troll", "act_idle_2h_1", "anim_troll_combat_idle1"), "(troll clip)");
    }

    [TestMethod]
    public void FirstPlay_NoClipBound_SaysSo()
    {
        var log = new TrollClipLog();

        Assert.AreEqual("[TrollClips] hill_troll: act_swim_idle -> - (no clip)",
            log.FirstPlay("hill_troll", "act_swim_idle", null));
    }

    [TestMethod]
    public void FirstPlay_ActNoneOrNoAction_ReturnsNull()
    {
        var log = new TrollClipLog();

        Assert.IsNull(log.FirstPlay("hill_troll", "act_none", null));
        Assert.IsNull(log.FirstPlay("hill_troll", null, "anything"));
        Assert.IsNull(log.FirstPlay("hill_troll", "", "anything"));
    }

    [TestMethod]
    public void Clear_AfterAMission_LogsTheSameActionAgain()
    {
        var log = new TrollClipLog();
        log.FirstPlay("hill_troll", "act_release_overswing_2h", "anim_hill_troll_release_overswing_2h");

        log.Clear();

        Assert.IsNotNull(log.FirstPlay("hill_troll", "act_release_overswing_2h", "anim_hill_troll_release_overswing_2h"));
    }
}
