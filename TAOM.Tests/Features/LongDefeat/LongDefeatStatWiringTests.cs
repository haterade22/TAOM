using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// The three lines that carry the Long Defeat's stat affixes into the engine and that no unit test can run without a mission (phase2-plan.md Slice 3):
/// the publish and the two stat-model calls. Read off the source, so deleting one fails here instead of leaving every hook test green.
/// </summary>
[TestClass]
public class LongDefeatStatWiringTests
{
    private const string EffectsAdapter = "Main/Adapters/LongDefeatEffectsAdapter.cs";
    private const string StatModel = "Main/Features/CultureDoctrine/Models/TaomCustomBattleAgentStatCalculateModel.cs";

    private static string BodyOf(string source, string marker)
    {
        var at = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.IsTrue(at >= 0, marker + " not found");
        var next = source.IndexOf("\n    public ", at + marker.Length, StringComparison.Ordinal);
        var nextPrivate = source.IndexOf("\n    private ", at + marker.Length, StringComparison.Ordinal);
        var end = new[] { next, nextPrivate }.Where(i => i > 0).DefaultIfEmpty(source.Length).Min();
        return source.Substring(at, end - at);
    }

    [TestMethod]
    public void TheEffectsAdapter_PublishesTheWholeTableOnce_ToBothHooks()
    {
        var source = RepoPaths.ReadSource(EffectsAdapter, stripComments: true);
        var publish = BodyOf(source, "public void PublishStats(");

        StringAssert.Contains(publish, "LongDefeatHookState.Publish(_mission, table)");
    }

    [TestMethod]
    public void TheCustomBattleStatModel_AppliesTheWavesStatsAfterTheRaceAbilities()
    {
        var source = RepoPaths.ReadSource(StatModel, stripComments: true);
        var update = BodyOf(source, "public override void UpdateAgentStats(");

        var abilities = update.IndexOf("RaceAbilityHooks.ApplyStats(agent, agentDrivenProperties)", StringComparison.Ordinal);
        var affixes = update.IndexOf("LongDefeatStatHooks.UpdateStats(agent, agentDrivenProperties)", StringComparison.Ordinal);
        Assert.IsTrue(abilities >= 0 && affixes > abilities, "the affixes' percentages apply to what the abilities left in the properties");
    }

    [TestMethod]
    public void TheCustomBattleStatModel_RaisesArmourAfterTheEngineHasWrittenIt()
    {
        var source = RepoPaths.ReadSource(StatModel, stripComments: true);
        var initialize = BodyOf(source, "public override void InitializeAgentStats(");

        var engine = initialize.IndexOf("base.InitializeAgentStats(", StringComparison.Ordinal);
        var armour = initialize.IndexOf("LongDefeatStatHooks.InitializeArmour(agent, agentDrivenProperties, agentBuildData)", StringComparison.Ordinal);
        Assert.IsTrue(engine >= 0 && armour > engine, "the base call writes the armour values the hide affix multiplies");
    }
}
