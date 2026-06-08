using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Music;

namespace TAOM.Tests.Features.Music;

[TestClass]
public class NoRepeatShufflePickerTests
{
    [TestMethod]
    public void Pick_ReturnsNullForEmptyCandidateList()
    {
        var picker = new NoRepeatShufflePicker();

        var pick = picker.Pick("battle:gondor", new List<MusicTrackDefinition>(), true, 8);

        Assert.IsNull(pick);
    }

    [TestMethod]
    public void Pick_ReturnsSingleCandidate()
    {
        var picker = new NoRepeatShufflePicker();
        var tracks = Tracks("one");

        var pick = picker.Pick("battle:gondor", tracks, true, 8);

        Assert.AreSame(tracks[0], pick);
    }

    [TestMethod]
    public void Pick_DeterministicModeRoundsRobinByEventName()
    {
        var picker = new NoRepeatShufflePicker();
        var tracks = Tracks("track_c", "track_a", "track_b");

        Assert.AreEqual("track_a", picker.Pick("battle:gondor", tracks, false, 0).EventName);
        Assert.AreEqual("track_b", picker.Pick("battle:gondor", tracks, false, 0).EventName);
        Assert.AreEqual("track_c", picker.Pick("battle:gondor", tracks, false, 0).EventName);
        Assert.AreEqual("track_a", picker.Pick("battle:gondor", tracks, false, 0).EventName);
    }

    [TestMethod]
    public void Pick_ShuffleModeDoesNotRepeatBeforeCandidateCycleCompletes()
    {
        var picker = new NoRepeatShufflePicker(new System.Random(4));
        var tracks = Tracks("track_a", "track_b", "track_c");
        var picked = new HashSet<string>();

        picked.Add(picker.Pick("battle:gondor", tracks, true, 1).EventName);
        picked.Add(picker.Pick("battle:gondor", tracks, true, 1).EventName);
        picked.Add(picker.Pick("battle:gondor", tracks, true, 1).EventName);

        Assert.AreEqual(3, picked.Count);
    }

    [TestMethod]
    public void Pick_ShuffleModeAvoidsImmediateCycleBoundaryRepeatWhenPossible()
    {
        var picker = new NoRepeatShufflePicker(new System.Random(7));
        var tracks = Tracks("track_a", "track_b", "track_c");
        MusicTrackDefinition previous = null;

        for (var i = 0; i < 12; i++)
        {
            var current = picker.Pick("battle:gondor", tracks, true, 1);
            if (previous != null)
                Assert.AreNotEqual(previous.EventName, current.EventName);

            previous = current;
        }
    }

    [TestMethod]
    public void Pick_CandidateSetChangeResetsRoundRobinState()
    {
        var picker = new NoRepeatShufflePicker();
        var firstSet = Tracks("track_a", "track_b");
        var secondSet = Tracks("track_c", "track_d");

        Assert.AreEqual("track_a", picker.Pick("battle:gondor", firstSet, false, 0).EventName);
        Assert.AreEqual("track_b", picker.Pick("battle:gondor", firstSet, false, 0).EventName);
        Assert.AreEqual("track_c", picker.Pick("battle:gondor", secondSet, false, 0).EventName);
    }

    private static List<MusicTrackDefinition> Tracks(params string[] eventNames)
    {
        var tracks = new List<MusicTrackDefinition>();
        foreach (var eventName in eventNames)
        {
            tracks.Add(new MusicTrackDefinition(
                MusicBucket.Battle,
                "gondor",
                eventName,
                $"taom/battle_music/gondor/{eventName}.ogg",
                $"D:/TAOM/Main/_Module/ModuleSounds/taom/battle_music/gondor/{eventName}.ogg"));
        }

        return tracks;
    }
}
