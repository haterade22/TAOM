using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Elk;
using TAOM.Features.WarRam;

// Great elk attack wiring, pinned (#636). The elk mesh is skinned to the vanilla horse_skeleton, so its
// Monster (taom_elk in LOTRLOME_Armory) names the war ram's action set as_war_ram and its antler charge
// plays the ram's head-butt, act_war_ram_butt: the same head-down clip lowers the elk's antlers. These
// tests keep the C# side on that shared action and set, keep the cooldown longer than the clip, and keep
// the elk's Monster id apart from the ram's so the two mission behaviors never attach a tree to one agent.

namespace TAOM.Tests.Features.Elk;

[TestClass]
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
    public void AntlerReach_GrowsWithTheBody()
    {
        // Tuned at 1.0x as the ram's 1.5 m trigger and 2 m radius, both measured from the elk's CENTER. At 2x the
        // antlers sit about twice as far forward, so an unscaled reach would only ever hit what stands under the
        // chest (Mike, 2026-09-23: "x2 the size"). Same rule as ElephantConfig's trample reach.
        Assert.AreEqual(1.5f * ElkConfig.AuthoredScale, ElkConfig.AttackTriggerRange, 0.001f);
        Assert.AreEqual(2f * ElkConfig.AuthoredScale, ElkConfig.AttackRadius, 0.001f);
    }

    [TestMethod]
    public void TheElkItem_DeclaresTheScaleTheReachIsTunedFor()
    {
        // The reach above silently encodes taom_elk_a's body_length (the engine scales the mount by body_length / 100
        // at build). Change one without the other and the antler charge swings short or strikes air, with no error
        // anywhere. The item lives in the unversioned Armory, so this reads the live install and is Inconclusive
        // where the Armory is absent.
        string? env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        string horses = Path.Combine(string.IsNullOrWhiteSpace(env)
                ? @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord" : env,
            "Modules", "LOTRLOME_Armory", "ModuleData", "LOTRLOME_items", "LOTRAOM_horses.xml");
        if (!File.Exists(horses))
            Assert.Inconclusive("LOTRLOME_Armory not installed on this machine; the item cannot be checked here.");

        var item = XDocument.Load(horses).Descendants("Item").FirstOrDefault(i => (string?)i.Attribute("id") == "taom_elk_a");
        Assert.IsNotNull(item, "the taom_elk_a Horse item is missing from the Armory");
        string? bodyLength = item!.Descendants("Horse").FirstOrDefault()?.Attribute("body_length")?.Value;
        Assert.AreEqual(((int)Math.Round(ElkConfig.AuthoredScale * 100f)).ToString(), bodyLength,
            "taom_elk_a's body_length no longer matches ElkConfig.AuthoredScale: change both together, because the " +
            "antler charge's reach is derived from the constant");
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
