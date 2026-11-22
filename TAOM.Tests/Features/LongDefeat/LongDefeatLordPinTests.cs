using System;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// The wiring and the order of the death rule's engine half (phase4-plan.md Slice 4, spike S12) that the director tests over the fake field cannot see, pinned from source: the hand-back precedes the spawn as in
/// vanilla's own swap, and a refused spawn gives the soldier back (R24), nothing is keyed on <c>Agent.Index</c>, the bot's path asks the engine's own test before the take, no adapter call is made from a removal callback, and the host hands
/// the director the one field as the lord adapter too. Each pin names the edit that breaks it.
/// </summary>
[TestClass]
public class LongDefeatLordPinTests
{
    private const string Lord = "Main/Adapters/LongDefeatBattlefieldAdapter.Lord.cs";
    private const string Player = "Main/Adapters/LongDefeatBattlefieldAdapter.Player.cs";
    private const string Main = "Main/Adapters/LongDefeatBattlefieldAdapter.cs";
    private const string Director = "Main/Features/LongDefeat/LongDefeatDirector.cs";
    private const string DirectorLord = "Main/Features/LongDefeat/LongDefeatDirector.Lord.cs";
    private const string Effects = "Main/Features/LongDefeat/LongDefeatDirector.Effects.cs";
    private const string Host = "Main/Features/LongDefeat/LongDefeatRunHost.cs";

    private static string Source(string path) => RepoPaths.ReadSource(path, stripComments: true).Replace("\r\n", "\n");

    private static string Body(string source, string marker)
    {
        var at = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.IsTrue(at >= 0, marker + " not found");
        var open = source.IndexOf('{', at);
        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            depth += source[i] == '{' ? 1 : source[i] == '}' ? -1 : 0;
            if (depth == 0)
                return source.Substring(at, i - at + 1);
        }

        throw new AssertFailedException(marker + " has no closing brace");
    }

    private static int At(string body, string part)
    {
        var at = body.IndexOf(part, StringComparison.Ordinal);
        Assert.IsTrue(at >= 0, $"'{part}' is not in:\n{body}");
        return at;
    }

    private static int Count(string text, string part) => Regex.Matches(text, Regex.Escape(part)).Count;

    // ---- the rise (D40, R24) ----

    [TestMethod]
    public void TheRise_FindsTheSoldier_HandsHimToTheAi_SpawnsTheLord_AndGivesHimBackWhenTheSpawnIsRefused()
    {
        var rise = Body(Source(Lord), "public LdRise RiseLord(");

        var found = At(rise, "var taken =");
        var handBack = At(rise, "HandBackSoldier(taken)");
        var spawn = At(rise, "SpawnPlayer(true, loadout, place)");
        var restore = At(rise, "taken.Controller = AgentControllerType.Player;");
        var refused = At(rise, "return LdRise.Refused;");
        var known = At(rise, "_knownMain = fresh;");
        Assert.IsTrue(found < handBack && handBack < spawn, "vanilla's own order (Mission.cs:4736-4738): the old body to the AI, then the new one to the player; the other way round leaves the formation's player flag false");
        Assert.IsTrue(spawn < restore && restore < refused, "a refused spawn gives the soldier back to the player before it answers, so a person is never left in a body he cannot leave");
        StringAssert.Contains(rise.Substring(spawn, restore - spawn), "if (!spawned", "the restore sits behind the spawn's failure");
        StringAssert.Contains(rise.Substring(spawn, restore - spawn), "handBack == LdHandBack.Done", "only a soldier this call handed to the AI is given back: under autoplay he was the AI's already");
        Assert.IsTrue(refused < known, "the field's known main body moves to the new lord, so a soldier who falls before the next read is no death of the player's");
        StringAssert.Contains(rise, "!ReferenceEquals(main, _lordWatch.Lord)", "the soldier is the live main agent that is not the lord's own body, found by reference");
        StringAssert.Contains(rise, "Live(fresh)", "the new body must stand, not only be returned");
        Assert.AreEqual(1, Count(Source(Lord), "Controller = AgentControllerType.AI"), "one place hands a body to the AI");
    }

    [TestMethod]
    public void TheHandBack_IsTheOnlyWriteOfControllerAi_AndIsGatedByLivenessAndAnUnchangedValue()
    {
        var hand = Body(Source(Lord), "private static LdHandBack HandBackSoldier(");

        var live = At(hand, "!Live(taken)");
        var noop = At(hand, "taken.Controller == AgentControllerType.AI");
        var write = At(hand, "taken.Controller = AgentControllerType.AI");
        Assert.IsTrue(live < noop && noop < write, "a dead soldier is never written, an AI-driven one is a no-op, then the write");
        StringAssert.Contains(hand, "return LdHandBack.Done;");
    }

    [TestMethod]
    public void TheLordAdapter_NeverKeysOnAnAgentIndex_OrHoldsAnAgentAcrossFrames()
    {
        var source = Source(Lord);

        Assert.IsFalse(Regex.IsMatch(source, @"\.Index\b"), "Agent.Index is handed to the next agent the engine builds");
        Assert.IsFalse(Regex.IsMatch(source, @"Dictionary<\s*int"), "no index-keyed store");
        Assert.IsFalse(Regex.IsMatch(source, @"private\s+(readonly\s+)?(volatile\s+)?Agent\??\s+_"), "the death rule holds its facts as numbers or by the lord watch, never in an Agent field of its own");
        foreach (var director in new[] { Source(DirectorLord), Source(Director) })
            Assert.IsFalse(Regex.IsMatch(director, @"Agent\.Index"), "the director keys nothing on an agent index");
    }

    [TestMethod]
    public void TheRiseAtTheDirector_ServesTheRunOnlyAfterASpawn_AndTheCheckIsReadAfterAdoption()
    {
        var rise = Body(Source(DirectorLord), "private void RiseLord(LordRiseOrder order)");

        var refused = At(rise, "if (!answer.Spawned)");
        var rose = At(rise, "_run.LordRose(");
        Assert.IsTrue(refused < rose, "a refused spawn answers the run with nothing");
        StringAssert.Contains(rise.Substring(refused, rose - refused), "return;");
        StringAssert.Contains(rise, "_lord.RiseLord(place, _plan.Loadout)");
        Assert.IsTrue(At(rise, "JudgeClass(true)") < rose, "the class is read back from the new body before the run is told");
        var tick = Body(Source(Director), "public void Tick(float dt)");
        Assert.IsTrue(At(tick, "AdoptIfNew();") < At(tick, "JudgeRise();") && At(tick, "JudgeRise();") < At(tick, "var fx = _run.Tick("), "the rise is read back after adoption ran, before the run's tick");
        Assert.IsTrue(At(tick, "Apply(fx);") < At(tick, "JudgeGeneralDown();"), "the seat is read back after the tick wrote it");
    }

    // ---- the bot's take-over (D44a) ----

    [TestMethod]
    public void TheBotsTakeover_AsksTheEnginesOwnTestBeforeTheTake_AndTakesOnlyTheAcceptedOne()
    {
        var take = Body(Source(Lord), "public LdTake TakeOverForBot(");

        var ask = At(take, "_mission.CanTakeControlOfAgent(agent)");
        var call = At(take, "_mission.TakeControlOfAgent(best)");
        Assert.IsTrue(ask < call, "the engine's own test comes first");
        Assert.AreEqual(1, Count(take, "_mission.TakeControlOfAgent("), "one take");
        StringAssert.Contains(take.Substring(ask, 140), "continue", "a refused soldier is skipped");
        Assert.IsTrue(At(take, "no soldier qualifies") < call, "with none qualifying nothing is taken");
        Assert.IsFalse(take.Contains("_lordWatch") || take.Contains("_knownMain") || take.Contains("_lastMain"), "the lord's last position is read as numbers, never from a held dead Agent");
        StringAssert.Contains(take, "_postedByAgent.ContainsKey(agent)", "a posted tower archer is never taken: the posts' hold would pin the bot's body to its tower");
        StringAssert.Contains(take, "DistanceFromLastLord(agent.Position)");
    }

    [TestMethod]
    public void TheLastLordPosition_IsKeptAsNumbers_ReadWhileTheMainAgentIsLive()
    {
        var read = Body(Source(Player), "public LdLordFacts ReadLord(");

        Assert.IsTrue(At(read, "!Live(main)") < At(read, "NoteLordAt(at)"), "only a live main agent's position is kept");
        var note = Body(Source(Lord), "private void NoteLordAt(");
        StringAssert.Contains(note, "_lordLastX = at.x;");
        Assert.IsFalse(Regex.IsMatch(Source(Lord), @"(Vec3|Agent)\??\s+_lordLast"), "numbers, not a Vec3 read from an agent, and never an Agent");
    }

    // ---- the seat (D41) ----

    [TestMethod]
    public void TheSeat_IsWrittenOnlyFromALiveBody_AndNullOtherwise()
    {
        var set = Body(Source(Lord), "public void SetGeneral(");

        StringAssert.Contains(set, "Agent? general = null;");
        StringAssert.Contains(set, "main != null && Live(main) ? main : null");
        StringAssert.Contains(set, "Live(pair.Agent) && ReferenceEquals(pair.Agent.Team, team)", "a hero is seated only on the player's own side: in a story attack the free heroes are the enemy");
        Assert.AreEqual(1, Count(set, "team.GeneralAgent ="), "one write, of the chosen live body or null");
        StringAssert.Contains(set, "team.GeneralAgent = general;");
        Assert.AreEqual(1, Count(Source(Main), "team.GeneralAgent = main;"), "the adoption's own write stays the only other one");
    }

    // ---- threads (R-threads) ----

    [TestMethod]
    public void NoLordAdapterCall_IsMadeFromARemovalCallback()
    {
        var director = Source(Director);
        var removed = Body(director, "public void OnAgentRemoved(");
        var fell = Body(director, "private void LordFell(");

        Assert.IsFalse(Regex.IsMatch(removed, @"_lord\??\."), "the removal callback may run off the main thread");
        Assert.IsFalse(Regex.IsMatch(fell, @"_lord\??\."), "LordFell runs inside the deferred removal");
        Assert.IsFalse(Regex.IsMatch(Body(Source(DirectorLord), "private void NoteLordFall("), @"_lord\??\."), "the fall is only noted; the seat and its check wait for the next tick");
        Assert.IsFalse(Regex.IsMatch(Source(Lord), @"OnAgentRemoved|OnAgentDeleted|OnAgentHit|OnScoreHit"), "the adapter's death-rule half reaches no engine callback");
    }

    [TestMethod]
    public void TheEffects_AreServedInTheirOrder_TheSeatFirst_ThePlayersTakeAndTheBlowAfterTheAttackers_TheRiseAfterTheClear()
    {
        var apply = Body(Source(Effects), "private void Apply(RunEffects fx)");

        var seat = At(apply, "if (fx.General is { } seat)");
        var defenders = At(apply, "SpawnBatch(batch, false");
        var attackers = At(apply, "SpawnBatch(batch, true");
        var take = At(apply, "if (fx.TakeOverForBot)");
        var fell = At(apply, "if (fx.FellLord)");
        var events = At(apply, "foreach (var e in fx.Events)");
        var rise = At(apply, "if (fx.RiseLord is { } rise)");
        Assert.IsTrue(seat < defenders && defenders < attackers && attackers < take && take < fell && fell < events && events < rise, "the order: seat, defenders, attackers, take-over, forced fall, the clear's events, the rise");
        StringAssert.Contains(apply.Substring(seat, 80), "WriteGeneral(seat)");
        StringAssert.Contains(Body(Source(DirectorLord), "private void WriteGeneral("), "_lord?.SetGeneral(seat)");
    }

    // ---- the fact the removal carries (D47) ----

    [TestMethod]
    public void TheForcedFall_IsAFactCarriedFromTheBlowToTheRun_NotAnInferenceFromTheAsk()
    {
        var classify = Body(Source("Main/Adapters/LongDefeatBattlefieldAdapter.Diagnostics.cs"), "public LdRemoval Classify(");
        var director = Source(Director);

        StringAssert.Contains(classify, "Forced: _lordWatch.IsForced(agent)");
        StringAssert.Contains(Body(director, "public void OnAgentRemoved("), "LordFell(removal.TroopId, removal.Forced)");
        StringAssert.Contains(Body(director, "private void LordFell("), "_run.PlayerDown(troopId, forced)");
        Assert.IsFalse(Source("Main/Features/LongDefeat/Domain/LordLife.cs").Contains("_forcedWave"), "the wave of the ask no longer decides");
        Assert.IsFalse(Source("Main/Features/LongDefeat/Domain/SurvivalRun.Lord.cs").Contains("ForcedFallDue(s.Dt, _stage == WaveStage.Fighting, _wave)"));
        var fell = Body(Source(Lord), "public bool FellLord(");
        Assert.IsTrue(At(fell, "MarkForced(lord)") < At(fell, "StrikeFelled(lord, owner)"), "marked before the blow, so the removal it raises is already known to be forced");
        Assert.IsTrue(At(fell, "StrikeFelled(lord, owner)") < At(fell, "if (lord.Health >= 1f)"), "the mark is withdrawn by the health managed code wrote, not by whether native has removed the body yet");
    }

    // ---- the end ----

    [TestMethod]
    public void TheRunsEnd_ReleasesTheFieldsHolds_FromFinishAndFromShutdown_AndTheFieldsClearDoesToo()
    {
        Assert.IsTrue(Body(Source(Effects), "private void Finish()").Contains("ReleaseLordHolds();"));
        Assert.IsTrue(Body(Source(Director), "public void Shutdown()").Contains("ReleaseLordHolds();"));
        var release = Body(Source(Lord), "public void ReleaseLord()");
        StringAssert.Contains(release, "_lordLastKnown = false;");
        StringAssert.Contains(release, "_lordWatch.ClearForced();");
        Assert.IsTrue(Body(Source(Main), "public void Clear()").Contains("ReleaseLord();"));
    }

    // ---- the host ----

    [TestMethod]
    public void TheHost_PassesTheFieldAsTheLordAdapterToo_AndTheRealFieldImplementsIt()
    {
        StringAssert.Contains(Source(Host), "field as ILongDefeatLordAdapter");
        Assert.IsTrue(typeof(ILongDefeatLordAdapter).IsAssignableFrom(typeof(LongDefeatBattlefieldAdapter)));
        Assert.IsTrue(typeof(ILongDefeatBattlefieldAdapter).IsAssignableFrom(typeof(LongDefeatBattlefieldAdapter)));
    }

    [TestMethod]
    public void TheHost_HandsTheDirectorTheSameInstanceAsTheLordAdapter()
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
        var lord = typeof(LongDefeatDirector).GetField("_lord", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(director);
        var fort = typeof(LongDefeatDirector).GetField("_fort", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(director);
        Assert.AreSame(field, lord, "the lord adapter is the field itself, not a second object");
        Assert.AreSame(field, fort, "and so is the fort adapter");
        MissionThreadGuard.ResetForTests();
    }
}
