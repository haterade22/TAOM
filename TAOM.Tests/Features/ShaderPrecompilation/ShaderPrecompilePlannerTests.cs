using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ShaderPrecompilation;
using TAOM.Features.ShaderPrecompilation.Domain;

namespace TAOM.Tests.Features.ShaderPrecompilation;

[TestClass]
public class ShaderPrecompilePlannerTests
{
    private static string[] Roster(int n) => Enumerable.Range(1, n).Select(i => $"char_{i:D4}").ToArray();

    private static readonly string[] TwoScenes = { "taom_mordor_battle_003_forceatmo", "taom_rohan_battle_001_forceatmo" };

    // ---- character batches ---- //

    [TestMethod]
    public void BuildPlan_NullScenes_ReturnsOnlyCharacterBatches()
    {
        var plan = ShaderPrecompilePlanner.BuildPlan(Roster(3), null);
        Assert.AreEqual(1, plan.Count);
        Assert.AreEqual(PrecompileItemKind.CharacterBattle, plan[0].Kind);
        Assert.AreEqual(ShaderPrecompilePlanner.CharacterBattleScene, plan[0].SceneId);
    }

    [TestMethod]
    public void BuildPlan_RosterSmallerThanBatch_SingleBatchHoldsWholeRoster()
    {
        var plan = ShaderPrecompilePlanner.BuildPlan(Roster(7), null, batchSize: 10);
        Assert.AreEqual(1, plan.Count);
        CollectionAssert.AreEqual(Roster(7), plan[0].CharacterIds.ToList());
        Assert.AreEqual(0, plan[0].BatchIndex);
        Assert.AreEqual(1, plan[0].BatchCount);
    }

    [TestMethod]
    public void BuildPlan_RosterExactMultipleOfBatch_NoEmptyTrailingBatch()
    {
        var plan = ShaderPrecompilePlanner.BuildPlan(Roster(20), null, batchSize: 10);
        Assert.AreEqual(2, plan.Count);
        Assert.AreEqual(10, plan[0].CharacterIds.Count);
        Assert.AreEqual(10, plan[1].CharacterIds.Count);
    }

    [TestMethod]
    public void BuildPlan_RosterNotMultipleOfBatch_LastBatchIsPartial()
    {
        var plan = ShaderPrecompilePlanner.BuildPlan(Roster(23), null, batchSize: 10);
        Assert.AreEqual(3, plan.Count);
        Assert.AreEqual(10, plan[0].CharacterIds.Count);
        Assert.AreEqual(10, plan[1].CharacterIds.Count);
        Assert.AreEqual(3, plan[2].CharacterIds.Count);
        CollectionAssert.AreEqual(new[] { "char_0021", "char_0022", "char_0023" }, plan[2].CharacterIds.ToList());
    }

    [TestMethod]
    public void BuildPlan_CharacterBatches_PartitionRosterInOrder_NoDuplicates()
    {
        var roster = Roster(45);
        var plan = ShaderPrecompilePlanner.BuildPlan(roster, null, batchSize: 10);
        var all = plan.SelectMany(p => p.CharacterIds).ToList();
        CollectionAssert.AreEqual(roster, all, "every character exactly once, in roster order");
    }

    [TestMethod]
    public void BuildPlan_CharacterBatches_CarrySequentialIndexAndTotalCount()
    {
        var plan = ShaderPrecompilePlanner.BuildPlan(Roster(25), null, batchSize: 10);
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, plan.Select(p => p.BatchIndex).ToList());
        Assert.IsTrue(plan.All(p => p.BatchCount == 3));
        Assert.AreEqual("Troops batch 2/3 (10 characters)", plan[1].Description);
    }

    [TestMethod]
    public void BuildPlan_ScenePasses_FollowAllCharacterBatches_AndCarryNoCharacterIds()
    {
        var plan = ShaderPrecompilePlanner.BuildPlan(Roster(15), TwoScenes, batchSize: 10);
        Assert.AreEqual(4, plan.Count);
        Assert.AreEqual(PrecompileItemKind.CharacterBattle, plan[0].Kind);
        Assert.AreEqual(PrecompileItemKind.CharacterBattle, plan[1].Kind);
        Assert.AreEqual(PrecompileItemKind.ScenePass, plan[2].Kind);
        Assert.AreEqual(TwoScenes[0], plan[2].SceneId);
        Assert.AreEqual(TwoScenes[1], plan[3].SceneId);
        Assert.AreEqual(0, plan[2].CharacterIds.Count);
        Assert.AreEqual(0, plan[3].CharacterIds.Count);
    }

    [TestMethod]
    public void BuildPlan_EmptyOrNullRoster_ReturnsScenesOnly()
    {
        var fromEmpty = ShaderPrecompilePlanner.BuildPlan(Array.Empty<string>(), TwoScenes);
        var fromNull = ShaderPrecompilePlanner.BuildPlan(null, TwoScenes);
        Assert.AreEqual(2, fromEmpty.Count);
        Assert.AreEqual(2, fromNull.Count);
        Assert.IsTrue(fromEmpty.All(p => p.Kind == PrecompileItemKind.ScenePass));
        Assert.IsTrue(fromNull.All(p => p.Kind == PrecompileItemKind.ScenePass));
    }

    [TestMethod]
    public void BuildPlan_DropsBlankAndDuplicateCharacterIds_FirstWins()
    {
        var plan = ShaderPrecompilePlanner.BuildPlan(new[] { "a", " ", "b", null, "a", " b ", "c" }, null, batchSize: 10);
        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, plan[0].CharacterIds.ToList());
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void BuildPlan_BatchSizeBelowOne_Throws()
    {
        ShaderPrecompilePlanner.BuildPlan(Roster(3), null, batchSize: 0);
    }

    [TestMethod]
    public void CountBatches_RoundsUp_AndIsZeroForAnEmptyRoster()
    {
        Assert.AreEqual(0, ShaderPrecompilePlanner.CountBatches(0, 10));
        Assert.AreEqual(1, ShaderPrecompilePlanner.CountBatches(1, 10));
        Assert.AreEqual(1, ShaderPrecompilePlanner.CountBatches(10, 10));
        Assert.AreEqual(2, ShaderPrecompilePlanner.CountBatches(11, 10));
    }

    // ---- bootstrap plan + batch slicing (the roster is only readable inside the first battle) ---- //

    [TestMethod]
    public void BuildBootstrapPlan_FirstItemIsCharacterBattleWithNoIds_ThenScenesInOrder()
    {
        var plan = ShaderPrecompilePlanner.BuildBootstrapPlan(TwoScenes);
        Assert.AreEqual(3, plan.Count);
        Assert.AreEqual(PrecompileItemKind.CharacterBattle, plan[0].Kind);
        Assert.AreEqual(0, plan[0].CharacterIds.Count);
        Assert.AreEqual(0, plan[0].BatchCount, "the batch count is unknown until the roster is discovered");
        Assert.AreEqual(ShaderPrecompilePlanner.CharacterBattleScene, plan[0].SceneId);
        Assert.AreEqual(TwoScenes[0], plan[1].SceneId);
        Assert.AreEqual(TwoScenes[1], plan[2].SceneId);
    }

    [TestMethod]
    public void BuildBootstrapPlan_NullScenes_ReturnsOnlyTheBootstrapBatch()
    {
        var plan = ShaderPrecompilePlanner.BuildBootstrapPlan(null);
        Assert.AreEqual(1, plan.Count);
        Assert.AreEqual(PrecompileItemKind.CharacterBattle, plan[0].Kind);
    }

    [TestMethod]
    public void SliceBatch_MatchesBuildPlanBatchAtSameIndex()
    {
        var roster = new[] { "a", "b", " ", "c", "a", "d", "e", "f", "g" };
        var plan = ShaderPrecompilePlanner.BuildPlan(roster, null, batchSize: 3);
        for (int i = 0; i < plan.Count; i++)
            CollectionAssert.AreEqual(plan[i].CharacterIds.ToList(), ShaderPrecompilePlanner.SliceBatch(roster, i, 3).ToList(), $"batch {i}");
    }

    [TestMethod]
    public void SliceBatch_PastTheEnd_ReturnsEmpty()
    {
        Assert.AreEqual(0, ShaderPrecompilePlanner.SliceBatch(Roster(5), 3, 3).Count);
        Assert.AreEqual(0, ShaderPrecompilePlanner.SliceBatch(null, 0, 3).Count);
    }

    // ---- scene passes ---- //

    [TestMethod]
    public void BuildPlan_CharacterBattleIsFirst_ThenScenePassesInOrder()
    {
        var plan = ShaderPrecompilePlanner.BuildPlan(Roster(1), TwoScenes);
        Assert.AreEqual(3, plan.Count);
        Assert.AreEqual(PrecompileItemKind.CharacterBattle, plan[0].Kind);
        Assert.AreEqual(PrecompileItemKind.ScenePass, plan[1].Kind);
        Assert.AreEqual("taom_mordor_battle_003_forceatmo", plan[1].SceneId);
        Assert.AreEqual("taom_rohan_battle_001_forceatmo", plan[2].SceneId);
    }

    [TestMethod]
    public void BuildPlan_DedupsScenes_CaseInsensitive_And_SkipsBlanks()
    {
        var plan = ShaderPrecompilePlanner.BuildPlan(Roster(1), new[] { "scene_a", "  ", "SCENE_A", "scene_b", null });
        var scenePasses = plan.Where(p => p.Kind == PrecompileItemKind.ScenePass).Select(p => p.SceneId).ToList();
        CollectionAssert.AreEqual(new[] { "scene_a", "scene_b" }, scenePasses);
    }
}

[TestClass]
public class PrecompileSceneProviderParseTests
{
    [TestMethod]
    public void ParseSceneList_DropsCommentsAndBlanks_Trims()
    {
        var text = "# header comment\n\n  taom_mordor_battle_003_forceatmo  \n# another\ntaom_rohan_battle_001_forceatmo\n";
        var scenes = PrecompileSceneProvider.ParseSceneList(text);
        CollectionAssert.AreEqual(
            new[] { "taom_mordor_battle_003_forceatmo", "taom_rohan_battle_001_forceatmo" },
            scenes.ToList());
    }

    [TestMethod]
    public void ParseSceneList_Dedups_CaseInsensitive_FirstWins()
    {
        var scenes = PrecompileSceneProvider.ParseSceneList("scene_x\nSCENE_X\nscene_y\n");
        CollectionAssert.AreEqual(new[] { "scene_x", "scene_y" }, scenes.ToList());
    }

    [TestMethod]
    public void ParseSceneList_EmptyOrNull_ReturnsEmpty()
    {
        Assert.AreEqual(0, PrecompileSceneProvider.ParseSceneList("").Count);
        Assert.AreEqual(0, PrecompileSceneProvider.ParseSceneList(null).Count);
    }

    [TestMethod]
    public void DefaultScenes_IncludesActiveSiegeScene()
    {
        // The baked fallback must still walk the live scene set, so a representative active siege
        // scene is present (mirrors precompile_scenes.txt).
        CollectionAssert.Contains(PrecompileSceneProvider.DefaultScenes.ToList(), "taom_gondor_castle_001_forceatmo");
    }

    [TestMethod]
    public void DefaultScenes_ExcludesDisabledCrashScenes()
    {
        // Regression guard for the fallback-drift bug: the baked DefaultScenes must mirror the live
        // precompile_scenes.txt, which disables the pbr_terrain vista-permutation crashers (6 Mordor +
        // 2 Rohan open-field + Helm's Deep). A missing/empty config falls back to DefaultScenes, so a
        // stale crasher here would resurrect a known process-crash on load (#287).
        var disabled = new[]
        {
            "taom_mordor_battle_001_forceatmo",
            "taom_mordor_battle_002_forceatmo",
            "taom_mordor_battle_003_forceatmo",
            "taom_mordor_battle_004_forceatmo",
            "taom_mordor_battle_black_gates_forceatmo",
            "taom_mordor_battle_dead_marshes_forceatmo",
            "taom_rohan_battle_001_forceatmo",
            "taom_rohan_battle_fords_of_isen_forceatmo",
            "taom_rohan_castle_helms_deep_forceatmo",
        };
        var defaults = PrecompileSceneProvider.DefaultScenes.ToList();
        foreach (var scene in disabled)
            CollectionAssert.DoesNotContain(defaults, scene, $"disabled crasher '{scene}' must not be in DefaultScenes");
    }
}
