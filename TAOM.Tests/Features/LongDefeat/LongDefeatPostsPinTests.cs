using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// Source pins for the posted archers (phase3-plan.md Slice 7; D20, D30a, spike S6). The engine side cannot run here, so these pin what the plan says it must do: the hold re-asserts AI control and
/// membership once a second, an archer is found by reference and never by <c>Agent.Index</c>, no registry role is made for him, a held post archer may still shoot (no <c>NoAttack</c>), the shot callback is
/// guarded by a want flag, and the fill comes after the boon and before every batch.
/// </summary>
[TestClass]
public class LongDefeatPostsPinTests
{
    private const string Adapter = "Main/Adapters/LongDefeatBattlefieldAdapter.Posts.cs";
    private const string Effects = "Main/Features/LongDefeat/LongDefeatDirector.Effects.cs";
    private const string Logic = "Main/Features/LongDefeat/Hooks/LongDefeatMissionLogic.cs";
    private const string Host = "Main/Features/LongDefeat/LongDefeatRunHost.cs";

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

        Assert.Fail(marker + " has no end");
        return string.Empty;
    }

    [TestMethod]
    public void HoldPosts_ReassertsAiControl_Membership_AndTheScriptedPosition()
    {
        var source = Source(Adapter);
        var hold = Body(source, "public LdPostsHold HoldPosts()");

        StringAssert.Contains(hold, "ControlFormation(formation, variant, false)");
        StringAssert.Contains(hold, "MoveStrangersOut(formation)");
        StringAssert.Contains(Body(source, "private int MoveStrangersOut("), "stranger.Formation = ranged");
        StringAssert.Contains(hold, "archer.Agent.Formation = formation");
        StringAssert.Contains(hold, "Hold(archer, variant)");

        var control = Body(source, "private static bool ControlFormation(");
        StringAssert.Contains(control, "SetControlledByAI(false)");
        StringAssert.Contains(control, "SetControlledByAI(true, true)", "variant C: the engine's own not-splittable switch (Formation.cs:808-834)");
        StringAssert.Contains(control, "IsAIOwned");
    }

    [TestMethod]
    public void TheFill_MovesStrangersOutBeforeItSpawns_AndNeverGivesUpOnTheFormation()
    {
        // The tactic can split units into the posts formation between the hold's re-asserts: the fill clears it as the hold does, instead of refusing after the player paid.
        var source = Source(Adapter);
        var fill = Body(source, "public LdPostsFilled FillPosts(");

        Assert.IsTrue(fill.IndexOf("MoveStrangersOut(formation)", StringComparison.Ordinal) is var moved && moved >= 0 && moved < fill.IndexOf("SpawnPostArcher(", StringComparison.Ordinal), "strangers leave before the first spawn");
        Assert.IsFalse(source.Contains("PostsFormationFree"), "a boot guard runs before any defender stands, so index 4 is always empty then");
        Assert.IsFalse(source.Contains("Blocked"), "the fill does not give up and take the tower rows off sale");
    }

    [TestMethod]
    public void TheArchers_AreKeptByReference_NeverByAgentIndex_AndMakeNoRegistryRole()
    {
        var source = Source(Adapter);

        Assert.IsFalse(source.Contains(".Index"), "a post archer is a held Agent gated with IsLive; Agent.Index recycles (csharp-architecture.md)");
        Assert.IsFalse(source.Contains("_registry"), "no registry role for a post archer (D20)");
        StringAssert.Contains(source, "IsLive(_posted[i].Agent)");
        StringAssert.Contains(source, "Dictionary<Agent, PostArcher>");
    }

    [TestMethod]
    public void AHeldPostArcher_MayStillShoot_VariantBAddsOnlyTheSpeedLimit()
    {
        var hold = Body(Source(Adapter), "private void Hold(PostArcher archer, PostsVariant variant)");

        Assert.IsFalse(hold.Contains("NoAttack"), "A18: an archer held without NoAttack still shoots (the spike's question)");
        StringAssert.Contains(hold, "Agent.AIScriptedFrameFlags.ConsiderRotation");
        StringAssert.Contains(hold, "if (variant == PostsVariant.B)");
        StringAssert.Contains(hold, "SetMaximumSpeedLimit(0f, false)");
        Assert.IsFalse(hold.Contains("SetControlledByAI"), "the formation's switch is ControlFormation's, once, not the archer's");
    }

    [TestMethod]
    public void TheShotCount_IsALookupByReference_AndOnlyForAPostArcher()
    {
        var source = Source(Adapter);
        var note = Body(source, "public void NotePostShot(object shooter)");

        StringAssert.Contains(note, "_postedByAgent.TryGetValue(agent, out var archer)");
        Assert.IsFalse(note.Contains("_mission"), "the shot callback touches no mission state");
        Assert.IsFalse(source.Contains("lock ("), "the shot callback is declared not multithread callable (Mission.cs:4982), so every caller is on the main thread");
    }

    [TestMethod]
    public void TheFill_SwitchesTheFormationOnce_AfterTheSpawns()
    {
        var source = Source(Adapter);
        var fill = Body(source, "public LdPostsFilled FillPosts(");

        Assert.IsFalse(Body(source, "private PostArcher? SpawnPostArcher(").Contains("ControlFormation("), "a spawn does not switch the formation");
        Assert.IsTrue(fill.LastIndexOf("SpawnPostArcher(", StringComparison.Ordinal) < fill.IndexOf("ControlFormation(formation, variant, true)", StringComparison.Ordinal), "one switch for the fill, after the last spawn");
    }

    [TestMethod]
    public void TheLogicsShotCallback_AsksTheWantBeforeForwarding()
    {
        var logic = Body(Source(Logic), "public override void OnAgentShootMissile(");

        Assert.IsTrue(logic.IndexOf("_host.WantsPostShots", StringComparison.Ordinal) < logic.IndexOf("_host.OnPostShot(", StringComparison.Ordinal));
        StringAssert.Contains(Source(Host), "public bool WantsPostShots => _director?.WantsPostShots ?? false;");
        StringAssert.Contains(Source("Main/Features/LongDefeat/LongDefeatDirector.Posts.cs"), "private bool _postShots;");
    }

    [TestMethod]
    public void TheFill_ComesAfterTheBoon_AndBeforeEveryBatch()
    {
        var apply = Body(Source(Effects), "private void Apply(RunEffects fx)");

        var gate = apply.IndexOf("ApplyGateRepair(", StringComparison.Ordinal);
        var boon = apply.IndexOf("ApplyBoonToStanding(", StringComparison.Ordinal);
        var fill = apply.IndexOf("ApplyFillPosts(", StringComparison.Ordinal);
        var batches = apply.IndexOf("SpawnBatch(batch, false", StringComparison.Ordinal);
        var care = apply.IndexOf("ApplyFieldCare(", StringComparison.Ordinal);
        var volley = apply.IndexOf("FireVolleys(", StringComparison.Ordinal);
        Assert.IsTrue(gate >= 0 && gate < boon && boon < fill && fill < batches && batches < care && care < volley,
            $"order gate {gate}, boon {boon}, fill {fill}, batches {batches}, care {care}, volleys {volley}: a post archer spawned beside a health pick is scaled once, the posts come before any pack, and the volleys fire last");
    }

    [TestMethod]
    public void TheVariantIsAConstantTheRunLogs_AndTheEnvironmentNamesAnother()
    {
        Assert.AreEqual(PostsVariant.A, PostsVariants.Default, "the plan's first choice");
        Assert.AreEqual("TAOM_LD_POSTS_VARIANT", PostsVariants.EnvironmentName);
        var choose = Body(Source(Adapter), "private PostsVariant ChooseVariant()");
        StringAssert.Contains(choose, "Environment.GetEnvironmentVariable(PostsVariants.EnvironmentName)");
        StringAssert.Contains(choose, "LogInfo($\"[LongDefeat][Posts] hold variant");
    }

    [TestMethod]
    public void ThePostsFormation_IsTheSkirmisherClass_AndTheSpawnTakesAPlaceWithItsFacing()
    {
        var source = Source(Adapter);

        StringAssert.Contains(source, "private const FormationClass PostsClass = FormationClass.Skirmisher;");
        StringAssert.Contains(source, "Spawn(troop, _mission.DefenderTeam, true, PostsClass, true, 1, 0, false, place: place)");
        StringAssert.Contains(source, "formation.SetPositioning(");
    }

    [TestMethod]
    public void TheDomain_NamesNoEngineType_ForThePosts()
    {
        foreach (var file in new[] { "Main/Features/LongDefeat/Domain/LdPosts.cs", "Main/Features/LongDefeat/Domain/PostsWatch.cs", "Main/Features/LongDefeat/Domain/SurvivalRun.Posts.cs" })
        {
            var source = Source(file);
            Assert.IsFalse(source.Contains("using TaleWorlds"), file + " names an engine namespace (ADR-007)");
        }
    }
}
