using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// Standing is one of only two things a merit band pays that the player cannot get anywhere else,
/// and the band ladder decides who gets it purely by score. So the question these pin is not "does
/// the scorer add up" (<see cref="BattleMeritLeftFieldTests"/> owns that) but "who clears the line
/// where trust starts", answered against the SHIPPED config rather than a synthetic ladder.
///
/// It exists because that line moved. Paying trust from the `solid` band put two non-fighting shapes
/// over it at once, and neither was visible from the diff: a maximum-kill walkout banks 45 because
/// `leftFieldPenalty` only ever cancelled the survival weight and the four other terms survive it,
/// and a player who stands inside his own line all battle banks survival + cohesion = 40 with no
/// kills and no engagement at all. The second only became reachable when the cohesion fallback
/// stopped scoring zero for an enlisted player who has no formation captain (#443).
///
/// The invariant, stated once so a future tuner cannot move a boundary without meeting it: **no
/// score attainable without fighting may reach a band that pays trust.**
/// </summary>
[TestClass]
public class MeritTrustFloorTests
{
    private static readonly string ConfigPath = Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..",
            "Main", "_Module", "ModuleData", "enlistment", "enlistment_config.json"));

    private static JObject ShippedConfig()
    {
        Assert.IsTrue(File.Exists(ConfigPath), $"enlistment_config.json not found at {ConfigPath}");
        return JObject.Parse(File.ReadAllText(ConfigPath));
    }

    private static MeritScoringConfig Scoring()
    {
        var raw = ShippedConfig()["meritScoring"];
        Assert.IsNotNull(raw, "meritScoring block missing");
        return raw!.ToObject<MeritScoringConfig>()!;
    }

    /// <summary>
    /// The lowest <c>minScore</c> among bands that pay trust. Everything below this line is free of
    /// standing; everything at or above it has to be earned by fighting.
    /// </summary>
    private static int LowestTrustPayingScore()
    {
        var bands = (JArray)ShippedConfig()["meritBands"]!;
        var paying = bands
            .Where(b => ((int?)b["trust"] ?? 0) > 0)
            .Select(b => (int)b["minScore"]!)
            .ToList();

        Assert.IsTrue(paying.Count > 0,
            "no shipped merit band pays trust, so fighting well cannot raise standing at all — that "
            + "is the #520 bug, not a passing test");
        return paying.Min();
    }

    [TestMethod]
    public void Score_MaximumWalkout_StaysBelowEveryTrustPayingBand()
    {
        var scoring = Scoring();

        // Everything a departing player can still bank. LeftTheField zeroes ONLY survival
        // (BattleMeritScorer line 91), so kills, cohesion, proximity, engagement and role fit all
        // survive the exit and only then does leftFieldPenalty come off the total.
        var score = BattleMeritScorer.Score(
            new MeritSample
            {
                Kills = int.MaxValue,          // clamped to killCountCap by the scorer
                SurvivalRatio = 1f,
                CohesionRatio = 1f,
                CommanderProximityRatio = 1f,
                EngagementRatio = 1f,
                RoleFit = true,
                LeftTheField = true,
            },
            scoring);

        Assert.IsTrue(score < LowestTrustPayingScore(),
            $"the best possible walkout scores {score}, at or above the {LowestTrustPayingScore()} "
            + "where trust starts. Quitting a fight would raise the player's standing. Either lower "
            + "the band that pays trust, raise meritScoring.leftFieldPenalty, or withhold band trust "
            + "when the sample says LeftTheField.");
    }

    [TestMethod]
    public void Score_StoodInTheLineAndDidNothing_StaysBelowEveryTrustPayingBand()
    {
        var scoring = Scoring();

        // Presence, not participation: never knocked down, and an ally inside cohesionDistance on
        // every sample tick, which for a soldier parked anywhere in his own formation is simply
        // true. No kills, no engagement, no proximity to the commander, no role fit.
        var score = BattleMeritScorer.Score(
            new MeritSample { SurvivalRatio = 1f, CohesionRatio = 1f },
            scoring);

        Assert.IsTrue(score < LowestTrustPayingScore(),
            $"standing in the line and doing nothing scores {score}, at or above the "
            + $"{LowestTrustPayingScore()} where trust starts. Surviving plus being near somebody is "
            + "presence, not service, and it must not pay standing.");
    }

    [TestMethod]
    public void Score_FoughtAndSurvived_DoesReachATrustPayingBand()
    {
        var scoring = Scoring();

        // The other half of the floor, and the one that matters: after the two assertions above, a
        // soldier who actually fights must still clear the line. Without this, "no free trust" could
        // be satisfied by paying nobody, which is the bug #520 was filed for.
        var score = BattleMeritScorer.Score(
            new MeritSample
            {
                Kills = 1,
                SurvivalRatio = 1f,
                CohesionRatio = 1f,
                EngagementRatio = 0.5f,
                RoleFit = true,
            },
            scoring);

        Assert.IsTrue(score >= LowestTrustPayingScore(),
            $"an infantry soldier who held formation, stayed engaged half the battle, took a kill "
            + $"and came out alive scores {score}, below the {LowestTrustPayingScore()} where trust "
            + "starts. The merit path is then not an earner and standing rests entirely on the duty "
            + "dice again.");
    }
}
