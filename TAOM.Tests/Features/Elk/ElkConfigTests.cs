using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Elk;
using TAOM.Features.MonsterSize;
using TAOM.Features.WarRam;

// Great elk attack wiring, pinned (#636). The elk mesh is skinned to the vanilla horse_skeleton, so its
// Monster (taom_elk in LOTRLOME_Armory) names the war ram's action set as_war_ram and its antler charge
// plays the ram's head-butt, act_war_ram_butt: the same head-down clip lowers the elk's antlers. These
// tests keep the C# side on that shared action and set, keep the cooldown longer than the clip, and keep
// the elk's Monster id apart from the ram's so the two mission behaviors never attach a tree to one agent.

namespace TAOM.Tests.Features.Elk;

[TestClass]
[TestCategory("LiveInstall")]
public class ElkConfigTests
{
    /// <summary>The ram's compiled master act_war_ram_butt: clip frames 1..105 at 30 fps, butt plus the head-down hold.</summary>
    private const double AntlerChargeClipSeconds = 105.0 / 30.0;

    [TestMethod]
    public void ElkMonsterId_MatchesTheArmoryMonster()
    {
        Assert.AreEqual("taom_elk", ElkConfig.ElkMonsterId);
    }

    [TestMethod]
    public void ElkMonsterId_DiffersFromTheWarRam_SoOnlyOneTreeAttachesPerAgent()
    {
        // Both mission behaviors attach by Monster.StringId. A shared id would give every elk (or every ram)
        // two trees, two cooldowns and two antler charges per window.
        Assert.AreNotEqual(WarRamConfig.WarRamMonsterId, ElkConfig.ElkMonsterId);
    }

    [TestMethod]
    public void AttackActionName_IsTheRamHeadButt_TheElkPlaysAsAnAntlerCharge()
    {
        Assert.AreEqual("act_war_ram_butt", ElkConfig.AttackActionName);
    }

    [TestMethod]
    public void ActionSetId_IsTheArmorySetTheElkMonsterNames()
    {
        // The drift guard looks this set up by id at mission start, so it must match action_set="as_war_ram"
        // on the live lotr_monster_elk.xml.
        Assert.AreEqual("as_war_ram", ElkConfig.ActionSetId);
    }

    [TestMethod]
    public void AllFourProfileSlots_HoldTheSameAttack_SoIsAttackMeansMidCharge()
    {
        Assert.AreEqual(ElkConfig.AttackActionName, ElkConfig.AttackAltActionName);
        Assert.AreEqual(ElkConfig.AttackActionName, ElkConfig.SideSlotLeftActionName);
        Assert.AreEqual(ElkConfig.AttackActionName, ElkConfig.SideSlotRightActionName);
    }

    [TestMethod]
    public void AntlerCharge_IsOne60BluntBlow()
    {
        // Mike, 2026-09-23: first "40 blunt and 20 piercing"; then, shown that with armour ignored and the blunt part
        // lethal two blows land exactly like one 60, he chose "one 60 blunt blow". A Bannerlord blow has one type.
        Assert.AreEqual(60, ElkConfig.AttackDamage);
        Assert.AreEqual(TaleWorlds.Core.DamageTypes.Blunt, ElkConfig.AttackDamageType);
    }

    [TestMethod]
    public void AntlerCharge_HitsOneEnemy_NotEveryoneInTheRadius()
    {
        // The ram's #618 rule, carried over: one victim per charge, the enemy the elk faces most squarely.
        Assert.IsTrue(ElkConfig.AttackSingleTarget);
    }

    [TestMethod]
    public void AttackCooldown_OutlastsTheClip_IncludingTheHold()
    {
        Assert.IsTrue(ElkConfig.AttackCooldownSeconds > AntlerChargeClipSeconds,
            $"cooldown {ElkConfig.AttackCooldownSeconds}s must exceed the {AntlerChargeClipSeconds:F2}s clip or the tree restarts the charge mid-hold");
    }

    [TestMethod]
    public void AntlerReach_IsTheRamsAtOneX_AndScalesWithTheLiveBody()
    {
        // The ram's 1.5 m trigger and 2 m radius, both measured from the elk's CENTER, at 1.0x. The shared nodes
        // multiply them by the elk's live agent scale (ReachScalesWithBody), which comes from the Monster's
        // taom_body_length (docs/features/monster-size.md), so the antlers strike what they visibly reach at any size
        // and a resize is one XML edit (the 2026-09-23 resizes each needed a C# constant and a redeploy).
        Assert.AreEqual(1.5f, ElkConfig.AttackTriggerRange, 0.001f);
        Assert.AreEqual(2f, ElkConfig.AttackRadius, 0.001f);
        Assert.IsTrue(ElkConfig.ReachScalesWithBody);
    }

    [TestMethod]
    public void ElkProfile_PassesTheReachFlag()
    {
        // The profile's reachScalesWithBody defaults to false and the profile cannot be built in a unit test (it creates
        // ActionIndexCaches), so a dropped argument would compile and leave the 1.1x elk scanning at the 1.0x ranges.
        string? combat = ReadProjectSource("Main", "Features", "Elk", "ElkCombat.cs");
        if (combat == null)
            Assert.Inconclusive("Main/Features/Elk/ElkCombat.cs not found: run from the repo root");
        StringAssert.Contains(combat, "reachScalesWithBody: ElkConfig.ReachScalesWithBody");
    }

    private static string? ReadProjectSource(params string[] relativeParts)
    {
        string? dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            string candidate = Path.Combine(new[] { dir }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
            dir = Directory.GetParent(dir)?.FullName;
        }
        return null;
    }

    [TestMethod]
    public void TheElkMonster_DeclaresItsSize_AndItsItemHoldsTheSchemaPlaceholder()
    {
        // One place for the size (Mike: "the monster xml should control the size of the animal"): the Monster's
        // taom_body_length, which MonsterSizeService copies into the item at game init. Items.xsd requires body_length
        // on <Horse>, so the item keeps the neutral placeholder; any other value there would read as a second size.
        // Both live in the unversioned Armory, so this reads the live install and is Inconclusive where it is absent.
        string? env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        string armory = Path.Combine(string.IsNullOrWhiteSpace(env)
                ? @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord" : env, "Modules", "LOTRLOME_Armory", "ModuleData");
        if (!Directory.Exists(armory))
            Assert.Inconclusive("LOTRLOME_Armory not installed on this machine; the Monster cannot be checked here.");

        var monster = XDocument.Load(Path.Combine(armory, "Monsters", "LOTR", "lotr_monster_elk.xml")).Descendants("Monster")
            .SingleOrDefault(m => (string?)m.Attribute("id") == ElkConfig.ElkMonsterId);
        Assert.IsNotNull(monster, "Monster taom_elk is missing from lotr_monster_elk.xml");
        string? size = (string?)monster!.Attribute(MonsterSizeConfig.AttributeName);
        Assert.IsTrue(MonsterSizeService.TryParseBodyLength(size, out _),
            $"taom_elk must declare {MonsterSizeConfig.AttributeName} as a whole number from " +
            $"{MonsterSizeConfig.MinBodyLength} to {MonsterSizeConfig.MaxBodyLength}, found \"{size}\"");

        var item = XDocument.Load(Path.Combine(armory, "LOTRLOME_items", "LOTRAOM_horses.xml")).Descendants("Item")
            .SingleOrDefault(i => (string?)i.Attribute("id") == "taom_elk_a");
        Assert.IsNotNull(item, "the taom_elk_a Horse item is missing from the Armory");
        Assert.AreEqual(MonsterSizeConfig.ItemPlaceholderBodyLength.ToString(), (string?)item!.Descendants("Horse").Single().Attribute("body_length"),
            "taom_elk_a must keep body_length at the placeholder: the size lives on Monster taom_elk, so resize it there");
    }

    [TestMethod]
    public void AttackTriggerRange_StaysInsideTheRadius()
    {
        // ElephantLikeEngageDecorator scans ONCE at AttackRadius and filters by AttackTriggerRange, so a trigger
        // range past the radius is unreachable and the elk would never commit on its stated reach.
        Assert.IsTrue(ElkConfig.AttackTriggerRange <= ElkConfig.AttackRadius,
            $"trigger range {ElkConfig.AttackTriggerRange} must not exceed the radius {ElkConfig.AttackRadius}");
    }
}
