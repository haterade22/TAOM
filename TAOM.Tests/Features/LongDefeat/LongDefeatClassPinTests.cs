using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// The engine-facing lines of the class kit that no test can run without a mission (phase2-plan.md Slice 5), read off the source: the override goes on the build data before
/// the spawn, the lord is the player's troop on the player's side, a foot class spawns with no horse, slot names go through the engine's own mapping and fit test, and the
/// kit starts from the lord's first battle set. The in-game proof is the <c>class</c> check.
/// </summary>
[TestClass]
public class LongDefeatClassPinTests
{
    private const string Player = "Main/Adapters/LongDefeatBattlefieldAdapter.Player.cs";
    private const string Field = "Main/Adapters/LongDefeatBattlefieldAdapter.cs";

    private static string Body(string source, string marker)
    {
        var at = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.IsTrue(at >= 0, marker + " not found");
        var next = source.IndexOf("\n    public ", at + marker.Length, StringComparison.Ordinal);
        var nextPrivate = source.IndexOf("\n    private ", at + marker.Length, StringComparison.Ordinal);
        var end = new[] { next, nextPrivate }.Where(i => i > 0).DefaultIfEmpty(source.Length).Min();
        return source.Substring(at, end - at);
    }

    [TestMethod]
    public void Spawn_PutsTheKitOnTheBuildData_BeforeTheSpawn_WithNoHorsesUnlessTheClassRides()
    {
        var spawn = Body(RepoPaths.ReadSource(Field, stripComments: true), "private Agent Spawn(");

        var data = spawn.IndexOf("GetAgentBuildDataToSpawnTroop(", StringComparison.Ordinal);
        var equipment = spawn.IndexOf(".Equipment(kit)", StringComparison.Ordinal);
        var horses = spawn.IndexOf(".NoHorses(!mounted)", StringComparison.Ordinal);
        var engineSpawn = spawn.IndexOf("SpawnTroopWithAgentBuildData(", StringComparison.Ordinal);
        Assert.IsTrue(data >= 0 && equipment > data && horses > data && engineSpawn > equipment && engineSpawn > horses, "the kit is set after the build data exists and before the spawn");
        StringAssert.Contains(spawn, "if (kit != null)", "no kit leaves the spawn exactly as the wave soldiers' spawn is");
    }

    [TestMethod]
    public void SpawnPlayer_SpawnsThePlayersTroop_OnThePlayersSide_ForTheStartAndForARespawn()
    {
        var source = RepoPaths.ReadSource(Player, stripComments: true);
        var body = Body(source, "public bool SpawnPlayer(");

        StringAssert.Contains(body, "Game.Current?.PlayerTroop");
        Assert.AreEqual(2, body.Split(new[] { "Spawn(player, team, true," }, StringSplitOptions.None).Length - 1, "both spawns pass isPlayerSide true");
        Assert.AreEqual(2, body.Split(new[] { "kit: kit)" }, StringSplitOptions.None).Length - 1, "both spawns carry the kit");
    }

    [TestMethod]
    public void TheKit_StartsFromTheLordsFirstBattleSet_AndMapsSlotNamesThroughTheEngine()
    {
        var source = RepoPaths.ReadSource(Player, stripComments: true);
        var build = Body(source, "private Equipment BuildKit(");

        StringAssert.Contains(build, "FirstBattleEquipment");
        StringAssert.Contains(build, ".Clone()");
        StringAssert.Contains(source, "Equipment.GetEquipmentIndexFromOldEquipmentIndexName(");
        StringAssert.Contains(build, "Equipment.IsItemFitsToSlot(");
        StringAssert.Contains(build, "IsRideable");
        Assert.IsFalse(source.Contains("Enum.Parse"), "Item0 is not a member of EquipmentIndex; the engine's own mapping handles the old names");
    }

    [TestMethod]
    public void ALordWithNoBattleSet_StartsTheKitFromAnEmptyBattleSet_NeverFromNull()
    {
        var source = RepoPaths.ReadSource(Player, stripComments: true);
        var build = Body(source, "private Equipment BuildKit(");

        // Mission.cs:4108 clones the override first; with none, a hero's FirstBattleEquipment.Clone() (Mission.cs:4284-4287) throws on null, so a null kit is never passed on.
        StringAssert.Contains(build, "new Equipment(Equipment.EquipmentType.Battle)");
        Assert.IsFalse(build.Contains("return null"), "no battle set is not a reason to spawn without the override");
    }

    [TestMethod]
    public void TheFactsAreReadAtTheSpawn_FromTheSpawnEquipmentOfTheLord_NotTheKitItBuilt()
    {
        var source = RepoPaths.ReadSource(Player, stripComments: true);
        var read = Body(source, "private LdKitFacts ReadBack(");

        StringAssert.Contains(read, "SpawnEquipment");
        StringAssert.Contains(read, "HasMount");
        Assert.IsFalse(read.Contains("BuildKit("), "the check must see what the engine made, not what was asked for");
        StringAssert.Contains(Body(source, "public bool SpawnPlayer("), "ReadBack(");
    }

    [TestMethod]
    public void TheAdapter_HoldsNoAgentForTheLord_SoARecycledSlotIsNeverRead()
    {
        var source = RepoPaths.ReadSource(Player, stripComments: true);

        // Agent slots recycle; a held Agent answers IsActive for whoever now fills the slot. The facts are read once, in the frame of the spawn, and kept instead.
        Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(source, @"private\s+Agent\??\s+_"), "no Agent field in the player partial");
        StringAssert.Contains(Body(source, "public LdKitFacts ReadKit("), "_kitFacts");
    }

    [TestMethod]
    public void TheLauncher_IsNotChangedForClasses_ItStaysUnderTheLineBudget()
    {
        var launcher = RepoPaths.ReadSource("Main/Features/LongDefeat/Hooks/LongDefeatMissionLauncher.cs", stripComments: false);

        Assert.IsTrue(launcher.Split('\n').Length <= 145, "the launcher grew; the plan keeps it from growing");
        Assert.IsFalse(launcher.Contains("Loadout"), "the class is resolved in the catalog, not the launcher");
    }
}
