using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CheatMenu.Hooks;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.CheatMenu;

/// <summary>
/// The battle cheat menu's wiring (source pins, comments stripped): Patch152 targets the single-player Esc menu's item list,
/// asks the opener whether to add the item, closes the Esc menu before the list opens; the console commands call the same
/// service methods; <c>taom.spawn_troops</c> and the menu's spawns share one spawner. The decisions themselves are tested in
/// BattleCheatPolicyTests and BattleCheatServiceTests.
/// </summary>
[TestClass]
public class BattleCheatWiringTests
{
    private const string PatchPath = "Client/Features/CheatMenu/Hooks/Patch152_BattleCheatEscapeMenu.cs";

    private static string Src(string path) => RepoPaths.ReadSource(path, stripComments: true);

    [TestMethod]
    public void Patch152_TargetsTheEscapeMenuItemsInItsOwnCategory()
    {
        var src = Src(PatchPath);

        StringAssert.Contains(src, "[HarmonyPatch(typeof(MissionGauntletSingleplayerEscapeMenu), \"GetEscapeMenuItems\")]");
        StringAssert.Contains(src, "[HarmonyPatchCategory(CheatMenuModule.BattlePatchCategory)]");
        StringAssert.Contains(src, "public static void Postfix(MissionGauntletSingleplayerEscapeMenu __instance, List<EscapeMenuItemVM> __result)");
    }

    [TestMethod]
    public void Patch152_InsertsOnlyWhereTheOpenerSays()
    {
        var src = Src(PatchPath);
        var decide = src.IndexOf("BattleCheatOpener.EscapeItemIndex(opener.IsShown,", StringComparison.Ordinal);
        var guard = src.IndexOf("if (index < 0) return;", StringComparison.Ordinal);
        var insert = src.IndexOf("__result.Insert(index,", StringComparison.Ordinal);

        Assert.IsTrue(decide >= 0, "the item's place must come from EscapeItemIndex with the opener's IsShown");
        Assert.IsTrue(guard > decide && insert > guard, "no insert before the not-shown / already-there check");
        Assert.AreEqual(1, Regex.Matches(src, @"__result\.(Insert|Add)\(").Count, "one item, one insert");
    }

    [TestMethod]
    public void Patch152_AClickClosesTheEscMenuThenOpensTheList_InsideTheGuard()
    {
        var src = Src(PatchPath);

        StringAssert.Matches(src, new Regex(@"CheatMenuMenus\.Guarded\(""battle_open"", \(\) =>\s*\{\s*if \(menu\.IsActive\) menu\.OnEscape\(\);\s*opener\.TryOpen\(\);"));
    }

    [TestMethod]
    public void Opener_AListPick_RunsTheServiceInsideTheGuard() =>
        StringAssert.Contains(Src("Main/Features/CheatMenu/Battle/BattleCheatOpener.cs"),
            "CheatMenuMenus.Guarded(\"battle_\" + action, () => _host.ShowOutcome(action, _service.Run(action)))");

    [TestMethod]
    public void ConsoleCommands_CallTheSameService()
    {
        var src = Src("Main/Features/CheatMenu/Cheats/BattleCheatCheats.cs");

        foreach (var call in new[]
                 {
                     "Service().HealSelf()", "Service().HealSide()", "Service().RoutEnemies()", "Service().KillEnemies()",
                     "Service().SpawnTroops(ally, count)", "Service().ToggleGodMode()", "Service().SetGodMode(true)",
                     "Service().SetGodMode(false)",
                 })
            StringAssert.Contains(src, call);
        Assert.AreEqual(5, Regex.Matches(src, @"TaomConsole\.RunInMission\(").Count, "every battle command runs behind the mission gate");
    }

    [TestMethod]
    public void GodMode_TheBattleViewTicksIt_OutsideTheKeyGuard()
    {
        var src = Src("Client/Features/CheatMenu/UI/BattleCheatMenuView.cs");
        var tick = src.IndexOf("base.OnMissionScreenTick(dt);", StringComparison.Ordinal);
        var god = src.IndexOf("TickGodMode();", StringComparison.Ordinal);
        var keyGuard = src.IndexOf("if (_failed ||", StringComparison.Ordinal);

        Assert.IsTrue(tick >= 0 && god > tick && keyGuard > god, "god mode ticks every frame before, and apart from, the key reader's guard");
        StringAssert.Contains(src, "_godMode?.TickGodMode();");
        StringAssert.Contains(src, "_godModeFailed = true;");
    }

    [TestMethod]
    public void GodMode_UsesTheEnginesImmortalState_AndOnlyInSetImmortal()
    {
        var src = Src("Main/Adapters/BattleCheatAdapter.cs");

        StringAssert.Contains(src, "agent.SetMortalityState(immortal ? Agent.MortalityState.Immortal : Agent.MortalityState.Mortal);");
        Assert.AreEqual(1, Regex.Matches(src, @"SetMortalityState\(").Count, "god mode writes mortality in one place");
        Assert.IsFalse(src.Contains("ToggleInvulnerable"), "never vanilla's toggle: it would undo the game's own flag");
        StringAssert.Contains(src, "MissionKey: RuntimeHelpers.GetHashCode(mission)");
        StringAssert.Contains(src, "RuntimeHelpers.GetHashCode(agent) == held.Identity", "a reused index never passes for the held agent (#592)");
    }

    [TestMethod]
    public void SpawnTroopsAndTheMenu_ShareOneSpawner()
    {
        var console = Src("Main/Features/DevConsole/Cheats/MissionSpawnCheats.cs");
        var adapter = Src("Main/Adapters/BattleCheatAdapter.cs");

        StringAssert.Contains(console, "MissionTroopSpawner.Spawn(args[0], count, isPlayerSide)");
        StringAssert.Contains(adapter, "MissionTroopSpawner.Spawn(troopId, count, playerSide, ringStart)");
        Assert.IsFalse(console.Contains("SpawnTroop("), "taom.spawn_troops spawns only through MissionTroopSpawner");
        Assert.IsFalse(adapter.Contains("SpawnTroop("), "the menu spawns only through MissionTroopSpawner");
    }
}

/// <summary>
/// Harmony injects by NAME, so Patch152's postfix must keep the names <c>__instance</c> and <c>__result</c>; its parameter types
/// live in TAOM.Client's game references, which the game folders provide.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class BattleCheatPatchBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestMethod]
    public void Patch152Postfix_BindsInstanceAndResultByName()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var postfix = typeof(Patch152_BattleCheatEscapeMenu).GetMethod("Postfix");

        Assert.IsNotNull(postfix);
        CollectionAssert.AreEqual(new[] { "__instance", "__result" }, postfix!.GetParameters().Select(p => p.Name).ToArray());
        Assert.AreEqual("TaleWorlds.MountAndBlade.GauntletUI.Mission.Singleplayer.MissionGauntletSingleplayerEscapeMenu",
            postfix.GetParameters()[0].ParameterType.FullName);
    }
}
