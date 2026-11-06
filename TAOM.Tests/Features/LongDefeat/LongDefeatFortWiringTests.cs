using System;
using System.IO;
using System.Linq;
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

/// <summary>
/// The lines of the gate work that no test can run without a mission (phase3-plan.md Slice 5, D17 to D19), read off the source, and the one wiring a test can run: the host hands the director the field it was
/// given as the fortification adapter too (the real field implements both interfaces on one object). The in-game proof is the <c>gate-level</c>, <c>gate-repair</c> and <c>gate-attacked</c> lines.
/// </summary>
[TestClass]
public class LongDefeatFortWiringTests
{
    private const string Gates = "Main/Adapters/LongDefeatBattlefieldAdapter.Gates.cs";
    private const string Field = "Main/Adapters/LongDefeatBattlefieldAdapter.cs";
    private const string Effects = "Main/Adapters/LongDefeatEffectsAdapter.cs";
    private const string Host = "Main/Features/LongDefeat/LongDefeatRunHost.cs";
    private const string Effect = "Main/Features/LongDefeat/LongDefeatDirector.Effects.cs";

    private static string Source(string path) => RepoPaths.ReadSource(path, stripComments: true);

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

    private static void InOrder(string body, params string[] parts)
    {
        var last = -1;
        foreach (var part in parts)
        {
            var at = body.IndexOf(part, StringComparison.Ordinal);
            Assert.IsTrue(at > last, $"'{part}' is missing or out of order in:\n{body}");
            last = at;
        }
    }

    // ---- the host ----

    [TestMethod]
    public void TheHost_HandsTheDirectorTheSameFieldAsTheFortAdapter_SoTheGatePointsAreChecked()
    {
        var plan = new RunPlan("helms", new[] { new GarrisonRow("t", FormationSlot.Infantry, 1) },
            new WavePlan(new[] { new WaveSpec(110, new[] { new WaveTroop("a", FormationSlot.Infantry, 1) }) }, new WaveScaling(10, 300)),
            new ShopPlan(0, Array.Empty<ShopOffer>()), RunRules.Default, RunOptions.Default);
        var field = new FakeLongDefeatField();
        var outcomes = new RecordingOutcomeLog();
        var host = new LongDefeatRunHost(plan, new LongDefeatSession(), Substitute.For<IModLogger>(), outcomes, new FakeLongDefeatMenu(), "m1", () => field);
        MissionThreadGuard.ResetForTests();

        host.MarkStartDue();
        host.Step(0.1f, () => 1f);

        Assert.IsTrue(outcomes.Has("skip", LongDefeatChecks.FortPointsId), "the boot asked the field, as the fort adapter, about the recorded points: " + string.Join("\n", outcomes.Lines));
        MissionThreadGuard.ResetForTests();
    }

    [TestMethod]
    public void TheHost_PassesTheFieldAsBothInterfaces_AndTheRealFieldImplementsBoth()
    {
        StringAssert.Contains(Source(Host), "field as ILongDefeatFortAdapter");
        Assert.IsTrue(typeof(ILongDefeatFortAdapter).IsAssignableFrom(typeof(LongDefeatBattlefieldAdapter)));
        Assert.IsTrue(typeof(ILongDefeatBattlefieldAdapter).IsAssignableFrom(typeof(LongDefeatBattlefieldAdapter)));
    }

    // ---- the apply order ----

    [TestMethod]
    public void Apply_WritesTheHammerFirst_ThenTheGateLevel_ThenTheRepair_ThenTheBatches_AndTheCareAfterThem()
    {
        var body = Body(Source(Effect), "private void Apply(RunEffects fx)");

        InOrder(body, "SetEngineHitPoints(engines)", "ApplyGateLevel(gateLevel)", "ApplyGateRepair(restore)", "SpawnBatch(batch, false", "SpawnBatch(batch, true", "ApplyFieldCare(");
    }

    // ---- the pins of the plan's Slice 5 row ----

    [TestMethod]
    public void NoGateRead_UsesTheActiveList_AFallenGateHasLeftIt()
    {
        var gates = Source(Gates);

        Assert.IsFalse(gates.Contains("ActiveMissionObjects"), "a destroyed gate is disabled and leaves the active list (Mission.cs:3060, MissionObject.cs:280), so the active list cannot show it");
        StringAssert.Contains(Body(gates, "public IReadOnlyList<LdGateFacts> ReadGates()"), "_gateSlots");
        StringAssert.Contains(Body(gates, "public LdGateWrite RepairGates("), "_gateSlots");
    }

    [TestMethod]
    public void TheDestructiblesReset_IsNeverCalledOnAGate_OnlyTheVanillaMissionResetByReflection()
    {
        var gates = Source(Gates);
        var field = Source(Field);

        Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(gates, @"\.Reset\s*\("), "DestructableComponent.Reset() alone leaves the door bound to the hidden damage-state entity (E2, E9)");
        Assert.AreEqual(1, System.Text.RegularExpressions.Regex.Matches(gates, @"AccessTools\.DeclaredMethod\(typeof\(CastleGate\), ""OnMissionReset""\)").Count);

        // The one Reset() in the Long Defeat adapters is the siege weapon's, at deployment: it is never given a gate.
        var resets = Directory.GetFiles(RepoPaths.RepoPath("Main", "Adapters"), "*LongDefeat*.cs").Where(f => System.Text.RegularExpressions.Regex.IsMatch(RepoPaths.StripComments(File.ReadAllText(f)), @"\.Reset\s*\(")).ToList();
        CollectionAssert.AreEqual(new[] { "LongDefeatBattlefieldAdapter.cs" }, resets.Select(Path.GetFileName).ToArray());
        StringAssert.Contains(Body(field, "private static void SetVanillaHitPoints("), "weapon?.GameEntity");
    }

    [TestMethod]
    public void TheRepair_WritesTheMaximum_ThenResetsByTheVanillaPath_ThenClosesTheGate_AndNeverCallsSetAbilityOfFaces()
    {
        var gates = Source(Gates);
        var repair = Body(gates, "public LdGateWrite RepairGates(");

        InOrder(repair, "MaxHitPoint =", "ResetGate(", "SetAutoOpenState(false)");
        Assert.IsFalse(gates.Contains("SetAbilityOfFaces"), "the gate's destructible names no navmesh prefab, so the call would do nothing (D17)");
    }

    [TestMethod]
    public void TheRepair_ClosesTheGateOnlyAfterTheResetRan_ANullOrThrowingResetLeavesTheBreachOpen()
    {
        // OnDestroyed leaves State at Closed (CastleGate.cs:1036-1052), so SetAutoOpenState(false) on a gate that was never reset would disable the breach's navmesh faces (:587-604) with no door in it.
        var gates = Source(Gates);
        var repair = Body(gates, "public LdGateWrite RepairGates(");

        StringAssert.Contains(repair, "_gateReset != null", "a repair with no reset found acts on no gate");
        InOrder(repair, "if (ResetGate(slot.Gate)", "!slot.Component.IsDestroyed", "SetAutoOpenState(false)");
        StringAssert.Contains(Body(gates, "private bool ResetGate("), "return false", "a thrown reset reports that it did not run");
    }

    [TestMethod]
    public void TheGateSample_AnswersMinusOne_WhenNoStandingGateHadAPlane()
    {
        var sample = Body(Source(Gates), "public int AttackersBehindGates(");

        StringAssert.Contains(sample, "measured ? total : -1");
        Assert.IsFalse(sample.Contains("return 0;"), "a sample with no keep or no plane measured nothing and must not read as a clean 0");
    }

    [TestMethod]
    public void TheRepair_WalksVisibleGatesOnly_TheHiddenOnesAreCountedNeverTouched()
    {
        var gates = Source(Gates);
        var repair = Body(gates, "public LdGateWrite RepairGates(");
        var level = Body(gates, "public LdGateWrite SetGateLevel(");

        Assert.IsFalse(repair.Contains("_hiddenGates") || level.Contains("_hiddenGates"), "the hidden inner gate stays broken");
        Assert.AreEqual(1, System.Text.RegularExpressions.Regex.Matches(gates, @"_hiddenGates\.Count\(h => !h\.IsDestroyed\)").Count, "the hidden gates are only counted, for the check");
        Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(gates, @"_hiddenGates[^;]*(SetAutoOpenState|ResetGate|MaxHitPoint|HitPoint\s*=)"));
    }

    [TestMethod]
    public void TheGateWrites_TouchNoRamAndNoTower_AndTheHammerWritesNoGate()
    {
        var gates = Source(Gates);
        foreach (var engine in new[] { "BatteringRam", "SiegeTower", "_rams", "_towers", "SiegeWeapon", "Harden" })
            Assert.IsFalse(gates.Contains(engine), "the gate writes (D19) must not reach " + engine);

        var hammer = Body(Source(Effects), "public LdEngineReport SetEngineHitPoints(");
        InOrder(hammer, "Harden(ram", "Harden(tower");
        Assert.IsFalse(hammer.Contains("MaxHitPoint =") || hammer.Contains("HitPoint =") || hammer.Contains("HitPoint *="), "the Hammer reads the gates for its log and writes none");
        Assert.AreEqual(2, System.Text.RegularExpressions.Regex.Matches(hammer, @"Harden\(").Count, "only rams and towers are hardened");
        StringAssert.Contains(Body(Source(Effects), "private static void Harden("), "weapon.GetSiegeEngineType()");
    }

    [TestMethod]
    public void TheLevelWrite_KeepsTheShareThroughGateMath_AndWritesTheMaximumFromTheBootsVanillaValue()
    {
        var level = Body(Source(Gates), "public LdGateWrite SetGateLevel(");

        InOrder(level, "VanillaMax * factor", "MaxHitPoint = newMax", "HitPoint = GateMath.KeepShare(");
        var capture = Body(Source(Field), "public int BreakHiddenGates()");
        StringAssert.Contains(capture, "CaptureGates(");
        StringAssert.Contains(Body(Source(Gates), "private void CaptureGates("), "component.MaxHitPoint");
    }

    [TestMethod]
    public void TheVanillaMaxima_AreReadAtTheBoot_BeforeTheHiddenGatesAreBroken()
    {
        var body = Body(Source(Field), "public int BreakHiddenGates()");

        InOrder(body, "CaptureGates(", "TryBreak(");
    }

    [TestMethod]
    public void TheDirectorsGateWrites_AreGuardedByTheFortAdapter_AndTheGatesReachTheRunsSnapshot()
    {
        var director = Source("Main/Features/LongDefeat/LongDefeatDirector.cs");

        StringAssert.Contains(director, "read.Gates");
        StringAssert.Contains(Source("Main/Adapters/LongDefeatBattlefieldAdapter.Diagnostics.cs"), "ReadGates()");
    }
    // ---- the pins of the plan's Slice 6 row ----

    private const string VolleyAdapter = "Main/Adapters/LongDefeatBattlefieldAdapter.Volley.cs";
    private const string VolleyDirector = "Main/Features/LongDefeat/LongDefeatDirector.Volley.cs";

    [TestMethod]
    public void Apply_FiresTheVolleysLast_AfterTheBatchesAndTheFieldCare()
    {
        var body = Body(Source(Effect), "private void Apply(RunEffects fx)");

        InOrder(body, "SpawnBatch(batch, true", "ApplyFieldCare(", "FireVolleys(fx.FireVolleys)", "Finish()");
    }

    [TestMethod]
    public void EveryOrder_IsAnsweredOnEveryPath_ExactlyOnce()
    {
        var body = Body(Source(VolleyDirector), "private void AnswerVolley(VolleyOrder order)");

        // Five ways out before the fire (no fort adapter, no item, no plan, no owner) answer through Withheld(order, ...); the one that fired answers through the run's VolleyFired. No path returns silently.
        Assert.AreEqual(4, System.Text.RegularExpressions.Regex.Matches(body, @"Withheld\(order,").Count, body);
        Assert.AreEqual(1, System.Text.RegularExpressions.Regex.Matches(body, @"_run\.VolleyFired\(order\.Source").Count);
        Assert.AreEqual(4, System.Text.RegularExpressions.Regex.Matches(body, @"Withheld\(order[^;]*;\s*return;").Count, "each withheld answer returns at once, so no second answer follows");
        Assert.IsTrue(Body(Source(VolleyDirector), "private void Withheld(").Contains("_run.VolleyWithheld(order.Source, why)"));
    }

    [TestMethod]
    public void TheBotsFire_IsACaseOfBuyFortification_ThatArmsTheVolley()
    {
        var director = Source("Main/Features/LongDefeat/LongDefeatDirector.cs");

        StringAssert.Contains(Body(director, "private void BuyFortification("), "case BotActionKind.Volley: ArmVolleyForBot()");
        StringAssert.Contains(Body(Source(VolleyDirector), "private void ArmVolleyForBot()"), "_run.ArmVolley()");
    }

    [TestMethod]
    public void TheVolleyHit_IsParkedOnTheMainThread_BehindItsVolatileFlag()
    {
        var source = Source(VolleyDirector);

        StringAssert.Contains(source, "private volatile bool _volleyHits;");
        var hit = Body(source, "public void OnVolleyHit(");
        InOrder(hit, "!_volleyHits", "_deferred.RunOrDefer(VolleyHitSite");
        Assert.IsFalse(hit.Contains("_run."), "the run holds plain dictionaries: the hit is booked in the parked callback, not here");
    }

    [TestMethod]
    public void TheWindow_IsFlushedAtTheClearAndAtTheRunsEnd_AndTickedByTheDirector()
    {
        StringAssert.Contains(Body(Source(Effect), "private void OnWaveCleared("), "CloseVolleyWatch(\"wave\")");
        StringAssert.Contains(Body(Source(Effect), "private void Finish()"), "FinishVolleys()");
        StringAssert.Contains(Body(Source(VolleyDirector), "private void FinishVolleys()"), "CloseVolleyWatch(\"run\")");
        StringAssert.Contains(Body(Source("Main/Features/LongDefeat/LongDefeatDirector.cs"), "public void Tick(float dt)"), "TickVolleyWatch(dt)");
    }

    [TestMethod]
    public void TheBotsLabel_IsClearedAtTheClear_ForTheClearDropsAnArmedShot()
    {
        StringAssert.Contains(Body(Source(Effect), "private void OnWaveCleared("), "_volleyByBot = false");
    }

    [TestMethod]
    public void TheTargetRead_IsNeverNarrowerThanTheAim_AndKeysNoAgentByIndex()
    {
        var read = Body(Source(VolleyAdapter), "public IReadOnlyList<LdVolleyTarget> ReadVolleyTargets(");

        StringAssert.Contains(read, "(chest - o).Length <= range", "the aim's own test: 3D from the nearest raised origin to the chest");
        StringAssert.Contains(read, "o.Z + originHeight");
        StringAssert.Contains(read, "agent.IsHuman");
        StringAssert.Contains(read, "_registry.TryGetKey(");
        Assert.IsFalse(Source(VolleyAdapter).Contains(".Index"), "never key on Agent.Index: indices recycle");
    }

    [TestMethod]
    public void TheFire_PassesTheItemsSpeedForBoth_AndNoObjectToIgnoreFromAWall()
    {
        var fire = Body(Source(VolleyAdapter), "public LdVolleyFire FireVolley(");

        StringAssert.Contains(fire, "shot.Speed, shot.Speed, false, null)");
        StringAssert.Contains(fire, "_mission.AllAgents.Count");
        StringAssert.Contains(fire, "GetWeaponDamageMultiplier(owner");
    }

    [TestMethod]
    public void TheFiresAddedCount_IsTheMissileListsGrowth_NotACounterOfTheCallsMade()
    {
        var fire = Body(Source(VolleyAdapter), "public LdVolleyFire FireVolley(");

        // A counter bumped per call always equals the shots planned, so the check's "added equals shots" leg could never fail; the engine's own list (Mission.cs:4970) is the observation.
        StringAssert.Contains(fire, "_mission.MissilesList.Count");
        Assert.IsFalse(fire.Contains("added++"), fire);
        var first = fire.IndexOf("_mission.MissilesList.Count", StringComparison.Ordinal);
        var call = fire.IndexOf("AddCustomMissile(", StringComparison.Ordinal);
        var last = fire.LastIndexOf("_mission.MissilesList.Count", StringComparison.Ordinal);
        Assert.IsTrue(first >= 0 && first < call && call < last, "the list is counted before the first missile and after the last: " + fire);
    }

    [TestMethod]
    public void TheVolleyItem_CarriesOnlyWhatTheAimReads()
    {
        StringAssert.Contains(Source("Main/Features/LongDefeat/Domain/LdFort.cs"), "internal sealed record LdVolleyItem(float Speed);");
        Assert.IsFalse(Source(VolleyAdapter).Contains("new LdVolleyItem(speed, thrust"), "thrust and reach are logged from locals; the item record keeps no field nobody reads");
    }

    [TestMethod]
    public void TheBootWarningForAFortPoint_SaysDropped_ForEveryPointTheTowersAndTheVolleyKeepOnlyUsable()
    {
        var gates = Source("Main/Features/LongDefeat/LongDefeatDirector.Gates.cs");

        StringAssert.Contains(gates, "is unusable and is dropped");
        Assert.IsFalse(gates.Contains("StartsWith(\"volley[\""), "the warning no longer tells a volley origin from a post: all are dropped");
        StringAssert.Contains(gates, "_run.LimitTowers(kept)");
        StringAssert.Contains(gates, "_run.LimitVolleyOrigins(");
    }

    [TestMethod]
    public void TheItemLookup_LogsThrustSpeedAndReachOnce_AndWarnsWhenTheReachFallsShort()
    {
        var lookup = Body(Source(VolleyAdapter), "private VolleyArrow? ArrowOf(");

        StringAssert.Contains(lookup, "_arrows.TryGetValue(itemId");
        StringAssert.Contains(lookup, "thrust damage");
        StringAssert.Contains(lookup, "FordsTowerGeometry.Reach(speed)");
        StringAssert.Contains(lookup, "reach < range");
    }

    [TestMethod]
    public void TheClearanceRay_StartsRaisedByTheOriginHeight_AndIsTwoMetresLong()
    {
        var clear = Body(Source(VolleyDirector), "private bool OriginClear(");

        StringAssert.Contains(clear, "origin.Z + height");
        StringAssert.Contains(clear, "ClearanceMetres");
        StringAssert.Contains(Source(VolleyDirector), "private const float ClearanceMetres = 2f;");
    }
}
