using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// The lines of the tower volley (phase3-plan.md Slice 8) that no test can run without a mission, read off the source: a tower's arrows are owned by one of that tower's posted archers and no one else, the fire
/// hands back each missile's index (what a hit carries, so two towers' windows tell their arrows apart), the hit hooks pass that index on, and no object is named to ignore. The scene check reads the live
/// Helm's Deep scene for the reason none is: its high towers are static entities with no script, so there is no <c>MissionObject</c> to name.
/// </summary>
[TestClass]
public class LongDefeatTowerVolleyPinTests
{
    private const string Volley = "Main/Adapters/LongDefeatBattlefieldAdapter.Volley.cs";
    private const string Hooks = "Main/Features/LongDefeat/Hooks/LongDefeatVolleyHooks.cs";

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

    [TestMethod]
    public void ATowersOwner_IsChosenAmongThatTowersPostedArchersOnly_AfterTheDeadAreDropped()
    {
        var choose = Body(Source(Volley), "private Agent? ChooseVolleyOwner(");

        StringAssert.Contains(choose, "towerId.Length > 0");
        StringAssert.Contains(choose, "PruneDead().Where(p => p.TowerId == towerId)", "only that tower's archers, and a dead or recycled handle is dropped before any native read");
        StringAssert.Contains(choose, "VolleyOwnerRule.Choose(", "the same rule as the Elven Volley: live, AI-controlled, never the main agent");
        Assert.IsFalse(choose.Contains("MainAgent ??"), "the main agent is not a fallback owner");
    }

    [TestMethod]
    public void TheFire_HandsBackTheIndexOfEveryMissileItAdded()
    {
        var fire = Body(Source(Volley), "public LdVolleyFire FireVolley(");

        StringAssert.Contains(fire, "var missile = _mission.AddCustomMissile(owner,");
        StringAssert.Contains(fire, "indices.Add(missile.Index)");
        StringAssert.Contains(fire, "multiplier, indices)");
    }

    [TestMethod]
    public void TheFire_NamesNoObjectToIgnore_ForAWallOrATower()
    {
        var fire = Body(Source(Volley), "public LdVolleyFire FireVolley(");

        StringAssert.Contains(fire, "shot.Speed, shot.Speed, false, null)");
        Assert.IsFalse(fire.Contains("Component"), "Fords passes its FordsTowerComponent; a Long Defeat tower has no MissionObject (see the scene test)");
        Assert.IsFalse(fire.Contains("MissionObject"));
    }

    [TestMethod]
    public void TheHitHooks_PassTheMissileIndexOn_ForBothTheHitAndTheCancelledHit()
    {
        var hooks = Source(Hooks);

        StringAssert.Contains(Body(hooks, "internal static void AgentHit("), "collisionData.MissileVelocity.Length, collisionData.AffectorWeaponSlotOrMissileIndex)");
        StringAssert.Contains(Body(hooks, "internal static void MissileHit("), "host.IsVolleyItem, collisionData.AffectorWeaponSlotOrMissileIndex)");
    }

    // The reason FireVolley names no object to ignore for a tower. The live scene is unversioned (TAOM_Map) and its geometry is reworked in the Kit (2026-11-06 the
    // towertop and walltower entities gave way to sm_helms_dep_castle_tower_a), so the test reads every entity named as a tower, and is Inconclusive where none is.
    [TestMethod]
    [TestCategory("LiveInstall")]
    public void HelmsDeepsHighTowers_AreStaticEntitiesWithNoScript_SoThereIsNoMissionObjectToIgnore()
    {
        var env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        var game = string.IsNullOrWhiteSpace(env) ? @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord" : env;
        var scene = Path.Combine(game, "Modules", "TAOM_Map", "SceneObj", "taom_rohan_castle_helms_deep_forceatmo", "scene.xscene");
        if (!File.Exists(scene))
            Assert.Inconclusive("the TAOM_Map Helm's Deep scene is not installed here; the towers cannot be read.");

        var towers = XDocument.Load(scene).Descendants("game_entity")
            .Where(e => ((string?)e.Attribute("name") ?? "").IndexOf("tower", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        if (towers.Count == 0)
            Assert.Inconclusive("the installed Helm's Deep scene names no tower entity; re-read which entity a tower volley could hit.");

        foreach (var tower in towers)
            Assert.IsNull(tower.Element("scripts"), $"{(string?)tower.Attribute("name")} carries a script, so it may be a MissionObject and FireVolley should name it for a tower's arrow");
    }
}
