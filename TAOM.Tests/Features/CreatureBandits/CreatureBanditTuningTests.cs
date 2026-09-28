using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CreatureBandits;
using TAOM.Features.Spider;
using TaleWorlds.Core;

// Creature Bandits (#692): the creature's own numbers, C# defaults that the MCM "Creature Bandits" options override.
// Mike's first cut (2026-09-28): 200 HP; a bite hits 1 soldier, a pounce 2, a swipe up to 3 at half damage; only a crit
// knocks a soldier down. Every value is clamped, so a bad MCM entry cannot break a strike.

namespace TAOM.Tests.Features.CreatureBandits;

[TestClass]
public class CreatureBanditTuningTests
{
    [TestMethod]
    public void Defaults_AreMikesFirstCut()
    {
        var t = CreatureBanditTuning.Defaults;
        Assert.AreEqual(200f, t.HitPoints);
        Assert.AreEqual(1, t.Strikes.Bite.MaxTargets);
        Assert.AreEqual(2, t.Strikes.Pounce.MaxTargets);
        Assert.AreEqual(3, t.Strikes.Swipe.MaxTargets);
        Assert.AreEqual(1f, t.Strikes.Bite.DamageMultiplier);
        Assert.AreEqual(1f, t.Strikes.Pounce.DamageMultiplier);
        Assert.AreEqual(0.5f, t.Strikes.Swipe.DamageMultiplier);
        Assert.IsTrue(t.Strikes.Bite.KnockdownOnCritOnly && t.Strikes.Pounce.KnockdownOnCritOnly && t.Strikes.Swipe.KnockdownOnCritOnly);
        Assert.AreEqual(SpiderConfig.PounceCooldownSeconds, t.PounceCooldownSeconds);
        Assert.AreEqual(SpiderConfig.SideAttackCooldownSeconds, t.SwipeCooldownSeconds);
    }

    [TestMethod]
    public void From_ClampsEveryValue()
    {
        var t = CreatureBanditTuning.From(hitPoints: float.NaN, biteTargets: 0, pounceTargets: 99, swipeTargets: -1,
            bitePercent: -10, pouncePercent: 1000, swipePercent: 50, knockdownOnCritOnly: false,
            pounceCooldown: 0f, swipeCooldown: float.PositiveInfinity);

        Assert.AreEqual(CreatureBanditTuning.Defaults.HitPoints, t.HitPoints, "non-finite HP falls back to the default");
        Assert.AreEqual(1, t.Strikes.Bite.MaxTargets);
        Assert.AreEqual(CreatureBanditTuning.MaxTargetsCap, t.Strikes.Pounce.MaxTargets);
        Assert.AreEqual(1, t.Strikes.Swipe.MaxTargets);
        Assert.AreEqual(0f, t.Strikes.Bite.DamageMultiplier);
        Assert.AreEqual(CreatureBanditTuning.MaxDamagePercent / 100f, t.Strikes.Pounce.DamageMultiplier);
        Assert.AreEqual(0.5f, t.Strikes.Swipe.DamageMultiplier);
        Assert.IsFalse(t.Strikes.Swipe.KnockdownOnCritOnly);
        Assert.AreEqual(CreatureBanditTuning.MinCooldownSeconds, t.PounceCooldownSeconds);
        Assert.AreEqual(CreatureBanditTuning.Defaults.SwipeCooldownSeconds, t.SwipeCooldownSeconds);
    }

    [TestMethod]
    public void From_HitPointsStayInRange()
    {
        Assert.AreEqual(CreatureBanditTuning.MinHitPoints, CreatureBanditTuning.From(1f, 1, 1, 1, 100, 100, 100, true, 5f, 2f).HitPoints);
        Assert.AreEqual(CreatureBanditTuning.MaxHitPoints, CreatureBanditTuning.From(1e6f, 1, 1, 1, 100, 100, 100, true, 5f, 2f).HitPoints);
    }

    [TestMethod]
    public void Resistances_DefaultToHalfDamageFromMissiles_FullFromMelee()
    {
        // The playtest's weakness was archers (a 120 HP spider died in 3 to 5 s); melee starts neutral.
        var t = CreatureBanditTuning.Defaults;
        Assert.AreEqual(0.5f, t.TakenFactor(isMissile: true, DamageTypes.Pierce));
        Assert.AreEqual(1f, t.TakenFactor(isMissile: false, DamageTypes.Cut));
        Assert.AreEqual(1f, t.TakenFactor(isMissile: false, DamageTypes.Pierce));
        Assert.AreEqual(1f, t.TakenFactor(isMissile: false, DamageTypes.Blunt));
    }

    [TestMethod]
    public void TakenFactor_MissileRuleCoversEveryMissile_MeleeUsesItsDamageType()
    {
        var t = CreatureBanditTuning.From(200f, 1, 2, 3, 100, 100, 50, true, 5f, 2f,
            missilePercent: 40, cutPercent: 80, piercePercent: 90, bluntPercent: 120);
        Assert.AreEqual(0.4f, t.TakenFactor(true, DamageTypes.Cut), 1e-6f, "a thrown axe is a missile first");
        Assert.AreEqual(0.4f, t.TakenFactor(true, DamageTypes.Blunt), 1e-6f);
        Assert.AreEqual(0.8f, t.TakenFactor(false, DamageTypes.Cut), 1e-6f);
        Assert.AreEqual(0.9f, t.TakenFactor(false, DamageTypes.Pierce), 1e-6f);
        Assert.AreEqual(1.2f, t.TakenFactor(false, DamageTypes.Blunt), 1e-6f);
        Assert.AreEqual(1f, t.TakenFactor(false, DamageTypes.Invalid), "an unknown type is untouched");
    }

    [TestMethod]
    public void TakenFactor_VanillasBluntRule_MakesChargesKicksAndBashesBlunt()
    {
        // Vanilla computes a charge, kick, bash, hilt hit or bare hand as Blunt through a local it never writes back
        // (MissionCombatMechanicsHelper.GetAttackCollisionResults:200); the struct still carries the weapon's type.
        var t = CreatureBanditTuning.From(200f, 1, 2, 3, 100, 100, 50, true, 5f, 2f,
            missilePercent: 40, cutPercent: 80, piercePercent: 90, bluntPercent: 120);
        Assert.AreEqual(1.2f, t.TakenFactor(false, DamageTypes.Cut, bluntByRule: true), 1e-6f);
        Assert.AreEqual(1.2f, t.TakenFactor(false, DamageTypes.Pierce, bluntByRule: true), 1e-6f);
        Assert.AreEqual(0.4f, t.TakenFactor(true, DamageTypes.Cut, bluntByRule: true), 1e-6f, "a missile stays a missile");
    }

    [TestMethod]
    public void TakenFactor_PercentsAreClamped()
    {
        var t = CreatureBanditTuning.From(200f, 1, 2, 3, 100, 100, 50, true, 5f, 2f,
            missilePercent: -5, cutPercent: 999, piercePercent: 100, bluntPercent: 100);
        Assert.AreEqual(0f, t.TakenFactor(true, DamageTypes.Pierce));
        Assert.AreEqual(CreatureBanditTuning.MaxTakenPercent / 100f, t.TakenFactor(false, DamageTypes.Cut));
    }

    [TestMethod]
    public void McmGroup_IsTopLevel_OutsideTheCombatMechanicsMasterToggle()
    {
        // "Combat Mechanics" promises that its master switch makes everything below it inert, and none of these reads
        // fold that switch (deep-review 2026-09-28, lens 5). MCM splits group paths on '/', so a top-level group is
        // the only honest place. MCMv5 is runtime-only here, so the attribute is read by name.
        var groups = typeof(TAOM.Features.TaomSettings)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(p => p.Name.StartsWith("CreatureBandit", System.StringComparison.Ordinal))
            .Select(p => (p.Name, Group: p.GetCustomAttributes(inherit: false)
                .Where(a => a.GetType().Name == "SettingPropertyGroupAttribute")
                .Select(a => (string)a.GetType().GetProperty("GroupName")!.GetValue(a))
                .Single()))
            .ToList();

        Assert.AreEqual(15, groups.Count, "the brood switch and one option per tuned value");
        foreach (var (name, group) in groups)
            Assert.AreEqual("Creature Bandits", group, $"{name} sits under '{group}'");

        // MCM creates a group once, from the first property it meets in declaration order, and ignores a GroupOrder on
        // any later one (SettingsUtils.GetGroupFor, MCMv5): the order must sit on the group's first property.
        var first = typeof(TAOM.Features.TaomSettings)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .First(p => p.Name.StartsWith("CreatureBandit", System.StringComparison.Ordinal));
        var firstGroup = first.GetCustomAttributes(inherit: false).Single(a => a.GetType().Name == "SettingPropertyGroupAttribute");
        Assert.AreEqual(53, (int)firstGroup.GetType().GetProperty("GroupOrder")!.GetValue(firstGroup),
            $"{first.Name} opens the group, so it carries the GroupOrder");
    }
}
