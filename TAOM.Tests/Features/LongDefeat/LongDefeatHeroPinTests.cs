using System;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// Source pins for the heroes' field adapter (#1337). The engine side cannot run here, so these pin the rules the adapter's comments state: the heroes' group gets a standing Move order
/// at the wall (never a charge, which the engine turns into Stop when no enemy stands), only while the formation is AI controlled, so an order the player gave to group 8 stands; the
/// group's membership is repaired (strangers out, heroes back) with a hold that costs only reads when nothing is wrong; the held bodies are a copy-on-write array that readers take
/// without the lock; the engine's own spawn wields the first weapon slot, so the adapter draws no bow; and the group is read from the formation, not from the agents.
/// </summary>
[TestClass]
public class LongDefeatHeroPinTests
{
    private const string Adapter = "Main/Adapters/LongDefeatBattlefieldAdapter.Heroes.cs";

    private static string Source() => RepoPaths.ReadSource(Adapter, stripComments: true);

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

        Assert.Fail(marker + " has no end");
        return string.Empty;
    }

    private static int At(string text, string part)
    {
        var at = text.IndexOf(part, StringComparison.Ordinal);
        Assert.IsTrue(at >= 0, part + " not found");
        return at;
    }

    private const string Field = "Main/Adapters/LongDefeatBattlefieldAdapter.cs";

    [TestMethod]
    public void TheOnlyCharge_IsTheStalemateCharge()
    {
        var source = Source();
        var post = Body(source, "private void Post(");

        Assert.AreEqual(1, Regex.Matches(source, "MovementOrderCharge").Count, "a charge with no enemy standing becomes Stop (Formation.Tick, MovementOrder.GetSubstituteOrder)");
        Assert.IsTrue(Regex.IsMatch(post, @"if \(_defendersCharge\)\s*\{\s*formation\.SetMovementOrder\(MovementOrder\.MovementOrderCharge\);\s*return;\s*\}"), post);
    }

    [TestMethod]
    public void TheStalemateCharge_SetsTheFlagBeforeTheHandBack_AndTheTacticsReturnPostsTheHeroesFirst()
    {
        var field = RepoPaths.ReadSource(Field, stripComments: true);
        var charge = Body(field, "public void OrderCharge(");
        var restore = Body(field, "public void RestoreDefenderTactic(");

        Assert.IsTrue(At(charge, "_defendersCharge = true;") < At(charge, "DelegateCommandToAI()"), "the next hold then orders the charge");
        Assert.IsTrue(At(restore, "_defendersCharge = false;") < At(restore, "PostHunters();"));
        Assert.IsTrue(At(restore, "PostHunters();") < At(restore, "SetOnlyTactic("), "the wall order stands before the new tactic's first tick");
        StringAssert.Contains(Body(Source(), "private void ClearHeroes("), "_defendersCharge = false;");
    }

    [TestMethod]
    public void ThePost_TakesTheGroupOffAiFirst_ThenMovesItToThePlace_FacingThePlacesDirection()
    {
        var post = Body(Source(), "private void Post(");

        Assert.IsTrue(At(post, "formation.SetControlledByAI(false);") < At(post, "formation.SetMovementOrder("), "off AI control before anything else");
        StringAssert.Contains(post, "formation.SetMovementOrder(MovementOrder.MovementOrderMove(place.Position));");
        StringAssert.Contains(post, "formation.SetFacingOrder(FacingOrder.FacingOrderLookAtDirection(");
        StringAssert.Contains(post, "formation.SetMovementOrder(MovementOrder.MovementOrderStop);", "with no place the group stops");
        Assert.IsFalse(Source().Contains("AI.Side"), "the tactic names a lane's defender by the side");
    }

    [TestMethod]
    public void ThePost_SetsTheLine_FireAtWill_AndTheWideForm_AndNotesItsTime()
    {
        var post = Body(Source(), "private void Post(");

        StringAssert.Contains(post, "formation.SetArrangementOrder(ArrangementOrder.ArrangementOrderLine);", "a siege behaviour leaves a shield wall behind");
        StringAssert.Contains(post, "formation.SetFiringOrder(FiringOrder.FiringOrderFireAtWill);");
        StringAssert.Contains(post, "formation.SetFormOrder(FormOrder.FormOrderWide);");
        Assert.IsFalse(post.Contains("FormOrderCustom"), "a lane's width is for a line of forty");
        Assert.IsTrue(At(post, "MovementOrderMove(") < At(post, "_heroPostedAt = _mission.CurrentTime;"), "only the wall order starts the settle time");
    }

    [TestMethod]
    public void TheHeroesPlace_IsTheNearestWallLane_ElseTheGate_ElseTheSpawnPlace_ResolvedOnce()
    {
        var place = Body(Source(), "private SplitPlace? HeroesPlace(");

        Assert.IsTrue(Regex.IsMatch(place, @"NearestLane\(WallLanes\(gate\?\.Side\), _heroHome\)\s*\?\?\s*gate\s*\?\?\s*OwnPlace\("), place);
        Assert.IsFalse(place.Contains("WallPlace("), "the middle between two lanes is 20 m or more from each ladder");
        StringAssert.Contains(Body(Source(), "private static SplitPlace? NearestLane("), "DistanceSquared(at) < best.Position.AsVec2.DistanceSquared(at)");
        Assert.IsTrue(At(place, "if (_heroPlaceRead)") < At(place, "GatePlace()"), "resolved once per mission");
        StringAssert.Contains(Body(Source(), "private SplitPlace? OwnPlace("), "Usable(");
        StringAssert.Contains(Body(Source(), "private void ClearHeroes("), "_heroPlaceRead = false;");
    }

    [TestMethod]
    public void SpawnHero_MovesStrangersOutBeforeTheSpawn_AndPutsHeroesBackAfterTheBodyIsHeld()
    {
        var spawn = Body(Source(), "public LdHeroReport SpawnHero(");

        Assert.IsTrue(At(spawn, "MoveHeroStrangersOut(") < At(spawn, "formation.BeginSpawn("), "strangers out of an empty formation 7 before the spawn");
        Assert.IsTrue(At(spawn, "HoldHero(") < At(spawn, "PutHeroesBack("));
        Assert.IsTrue(Regex.IsMatch(spawn, @"formation\.BeginSpawn\(1, false\);\s*try\s*\{[^}]*\}\s*finally\s*\{\s*formation\.EndSpawn\(\);\s*\}"), "the spawn is closed even when it throws");
    }

    [TestMethod]
    public void SpawnHero_PostsTheGroup_OnlyWhileTheFormationIsAiControlled()
    {
        var spawn = Body(Source(), "public LdHeroReport SpawnHero(");

        Assert.AreEqual(1, Regex.Matches(spawn, @"Post\(formation\)").Count, "one order call in the spawn");
        Assert.IsTrue(Regex.IsMatch(spawn, @"if \(formation\.IsAIControlled\)\s*Post\(formation\);"), "the order is the body of the AI-controlled test");
        Assert.IsTrue(At(spawn, "Post(formation)") < At(spawn, "!formation.IsAIControlled"), "Held is read after the order");
    }

    [TestMethod]
    public void TheRepair_MovesStrangersOut_PutsHeroesBack_AndPostsOnlyAnAiControlledGroup()
    {
        var repair = Body(Source(), "private LdHuntersHold RepairHunters(");

        Assert.IsTrue(At(repair, "MoveHeroStrangersOut(") < At(repair, "PutHeroesBack("));
        Assert.IsTrue(Regex.IsMatch(repair, @"if \(formation\.IsAIControlled\)\s*Post\(formation\);"));
        Assert.AreEqual(1, Regex.Matches(repair, @"Post\(formation\)").Count);
    }

    [TestMethod]
    public void AStranger_GoesToTheRangedFormationWhenRanged_ElseToTheInfantry_AfterTheWholeFormationIsRead()
    {
        var strangers = Body(Source(), "private int MoveHeroStrangersOut(");

        StringAssert.Contains(strangers, "agent.IsRangedCached ? FormationClass.Ranged : FormationClass.Infantry");
        Assert.IsTrue(At(strangers, "formation.ApplyActionOnEachUnit(") < At(strangers, ".Formation = "), "collected first, then assigned");
    }

    [TestMethod]
    public void PutHeroesBack_SetsTheFormationOfEachLiveHeroOutsideIt()
    {
        var back = Body(Source(), "private static int PutHeroesBack(");

        StringAssert.Contains(back, "!ReferenceEquals(pair.Agent.Formation, formation)");
        StringAssert.Contains(back, "pair.Agent.Formation = formation;");
        StringAssert.Contains(back, "OnDuty(pair.Agent)");
    }

    [TestMethod]
    public void ARoutingHero_IsLeftAlone_ByTheHoldTheRepairAndTheRead()
    {
        var source = Source();

        StringAssert.Contains(source, "private static bool OnDuty(Agent agent) => Live(agent) && !agent.IsRunningAway;");
        foreach (var user in new[] { "public LdHuntersHold HoldHunters(", "private static int PutHeroesBack(", "public LdHeroesRead ReadHeroes(", "private void PostHunters(" })
            StringAssert.Contains(Body(source, user), "OnDuty(", user);
    }

    [TestMethod]
    public void HoldHunters_ReturnsNothing_WithoutAllocating_WhenTheGroupIsWhole()
    {
        var hold = Body(Source(), "public LdHuntersHold HoldHunters(");

        StringAssert.Contains(hold, "!formation.IsAIControlled && whole && formation.CountOfUnits == live");
        StringAssert.Contains(hold, "if (live == 0)");
        foreach (var costly in new[] { "new ", "lock", ".Any(", ".Where(", ".ToArray(", ".ToList(", ".Count(", "=>" })
            Assert.IsFalse(hold.Contains(costly), costly + " in the per-tick hold");
    }

    [TestMethod]
    public void TheHeldBodies_AreACopyOnWriteArray_WrittenUnderTheGate_AndReadWithoutIt()
    {
        var source = Source();

        StringAssert.Contains(source, "private volatile (string Id, Agent Agent)[] _heroes");
        foreach (var writer in new[] { "private void HoldHero(", "private bool TakeHero(", "private void ClearHeroes(" })
            StringAssert.Contains(Body(source, writer), "lock (_heroGate)", writer);
        foreach (var reader in new[] { "private bool IsHeldHero(", "public LdHuntersHold HoldHunters(", "public LdHeroesRead ReadHeroes(" })
            Assert.IsFalse(Body(source, reader).Contains("lock"), reader);
    }

    [TestMethod]
    public void TheAdapter_DrawsNoBow_TheEngineWieldsTheFirstWeaponSlotAtTheSpawn()
    {
        var source = Source();

        Assert.IsFalse(source.Contains("WieldRanged"), "Mission.SpawnTroopWithAgentBuildData runs WieldInitialWeapons, which takes the first weapon slot");
        Assert.IsFalse(source.Contains("TryToWieldWeaponInSlot"));
    }

    [TestMethod]
    public void ReadHeroes_ReadsTheGroupFromTheFormation_AndItsOwnOrderWithTheSplitsTolerance()
    {
        var read = Body(Source(), "public LdHeroesRead ReadHeroes(");

        StringAssert.Contains(read, "!formation.IsAIControlled");
        StringAssert.Contains(read, "formation.CountOfUnits");
        StringAssert.Contains(read, "formation.GetReadonlyMovementOrderReference()");
        StringAssert.Contains(read, "order.OrderEnum == MovementOrder.MovementOrderEnum.Move && order.GetPosition(formation).Distance(at.AsVec2) <= OwnOrderTolerance");
        StringAssert.Contains(read, "Math.Abs(position.z - at.z)", "a hero at the foot of the wall is not at a place on top of it");
        StringAssert.Contains(read, "own ? _mission.CurrentTime - _heroPostedAt : 0f");
        Assert.IsFalse(read.Contains("ActiveBehavior"), "a formation off AI control never ticks its AI");
        Assert.IsFalse(read.Contains("agent.IsAIControlled"), "every AI soldier answers true there");
    }
}
