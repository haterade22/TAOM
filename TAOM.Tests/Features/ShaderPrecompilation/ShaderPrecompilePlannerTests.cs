using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ShaderPrecompilation;
using TAOM.Features.ShaderPrecompilation.Domain;

namespace TAOM.Tests.Features.ShaderPrecompilation;

[TestClass]
public class ShaderPrecompilePlannerTests
{
    [TestMethod]
    public void BuildPlan_NullScenes_ReturnsOnlyCharacterBattle()
    {
        var plan = ShaderPrecompilePlanner.BuildPlan(null);
        Assert.AreEqual(1, plan.Count);
        Assert.AreEqual(PrecompileItemKind.CharacterBattle, plan[0].Kind);
        Assert.AreEqual(ShaderPrecompilePlanner.CharacterBattleScene, plan[0].SceneId);
    }

    [TestMethod]
    public void BuildPlan_CharacterBattleIsFirst_ThenScenePassesInOrder()
    {
        var plan = ShaderPrecompilePlanner.BuildPlan(new[] { "taom_mordor_battle_003_forceatmo", "taom_rohan_battle_001_forceatmo" });
        Assert.AreEqual(3, plan.Count);
        Assert.AreEqual(PrecompileItemKind.CharacterBattle, plan[0].Kind);
        Assert.AreEqual(PrecompileItemKind.ScenePass, plan[1].Kind);
        Assert.AreEqual("taom_mordor_battle_003_forceatmo", plan[1].SceneId);
        Assert.AreEqual("taom_rohan_battle_001_forceatmo", plan[2].SceneId);
    }

    [TestMethod]
    public void BuildPlan_DedupsScenes_CaseInsensitive_And_SkipsBlanks()
    {
        var plan = ShaderPrecompilePlanner.BuildPlan(new[] { "scene_a", "  ", "SCENE_A", "scene_b", null });
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
    public void DefaultScenes_IncludesTheCrashScene()
    {
        CollectionAssert.Contains(PrecompileSceneProvider.DefaultScenes.ToList(), "taom_mordor_battle_003_forceatmo");
    }
}
