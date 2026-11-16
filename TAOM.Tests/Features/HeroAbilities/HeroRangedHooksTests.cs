using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HeroAbilities.Domain;
using TAOM.Features.HeroAbilities.Hooks;
using TaleWorlds.MountAndBlade;

namespace TAOM.Tests.Features.HeroAbilities;

// Both stat models call HeroRangedHooks.ApplyStats for every agent on every stat update (#1361). An agent the roster
// does not list must keep every value the models wrote before this pass. The bare agents below have no native side, so
// a call to an engine getter on them (IsMount reads the native flags, Agent.cs:644) would throw.
[TestClass]
[TestCategory("RequiresGame")]
public class HeroRangedHooksTests
{
    private HeroRangedRoster<Agent>? _saved;

    [TestInitialize]
    public void Setup()
    {
        _saved = HeroRangedHooks.Roster;
        HeroRangedHooks.Roster = null;
    }

    [TestCleanup]
    public void Cleanup() => HeroRangedHooks.Roster = _saved;

    private static Agent BareAgent() => (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));

    private static AgentDrivenProperties Props() => new AgentDrivenProperties
    {
        AiShooterError = 0.3f,
        WeaponInaccuracy = 0.02f,
        AiRangerLeadErrorMax = 0.5f,
        WeaponRotationalAccuracyPenaltyInRadians = 0.1f,
        SwingSpeedMultiplier = 0.9f,
    };

    private static void AssertUnchanged(AgentDrivenProperties p)
    {
        Assert.AreEqual(0.3f, p.AiShooterError);
        Assert.AreEqual(0.02f, p.WeaponInaccuracy);
        Assert.AreEqual(0.5f, p.AiRangerLeadErrorMax);
        Assert.AreEqual(0.1f, p.WeaponRotationalAccuracyPenaltyInRadians);
        Assert.AreEqual(0.9f, p.SwingSpeedMultiplier);
    }

    private static HeroRangedConfig Kit(bool zeroAimError = true, bool pierceShields = true)
        => new HeroRangedConfig { ZeroAimError = zeroAimError, PierceShields = pierceShields };

    [TestMethod]
    public void ApplyFor_NoRoster_Unchanged()
    {
        var p = Props();

        HeroRangedHooks.ApplyFor<object>(null, new object(), isMount: false, p);

        AssertUnchanged(p);
    }

    [TestMethod]
    public void ApplyFor_KeyNotListed_Unchanged()
    {
        var roster = new HeroRangedRoster<object>();
        roster.Add(new object(), Kit());
        var p = Props();

        HeroRangedHooks.ApplyFor(roster, new object(), isMount: false, p);

        AssertUnchanged(p);
    }

    [TestMethod]
    public void ApplyFor_NullKey_Unchanged()
    {
        var roster = new HeroRangedRoster<object>();
        roster.Add(new object(), Kit());
        var p = Props();

        HeroRangedHooks.ApplyFor<object>(roster, null, isMount: false, p);

        AssertUnchanged(p);
    }

    [TestMethod]
    public void ApplyFor_Mount_Unchanged()
    {
        var hero = new object();
        var roster = new HeroRangedRoster<object>();
        roster.Add(hero, Kit());
        var p = Props();

        HeroRangedHooks.ApplyFor(roster, hero, isMount: true, p);

        AssertUnchanged(p);
    }

    [TestMethod]
    public void ApplyFor_ZeroAimErrorFalse_Unchanged()
    {
        var hero = new object();
        var roster = new HeroRangedRoster<object>();
        roster.Add(hero, Kit(zeroAimError: false));
        var p = Props();

        HeroRangedHooks.ApplyFor(roster, hero, isMount: false, p);

        AssertUnchanged(p);
    }

    [TestMethod]
    public void ApplyFor_Listed_Zeroes()
    {
        var hero = new object();
        var roster = new HeroRangedRoster<object>();
        roster.Add(hero, Kit());
        var p = Props();

        HeroRangedHooks.ApplyFor(roster, hero, isMount: false, p);

        Assert.AreEqual(0f, p.AiShooterError);
        Assert.AreEqual(0f, p.WeaponInaccuracy);
        Assert.AreEqual(0f, p.AiRangerLeadErrorMax);
        Assert.AreEqual(0f, p.WeaponRotationalAccuracyPenaltyInRadians);
        Assert.AreEqual(0.9f, p.SwingSpeedMultiplier, "only the aim properties change");
    }

    // The entry both models call: a plain agent never reaches a native getter and keeps every value.
    [TestMethod]
    public void ApplyStats_NoRoster_PlainAgentUnchanged()
    {
        var p = Props();

        HeroRangedHooks.ApplyStats(BareAgent(), p);

        AssertUnchanged(p);
    }

    [TestMethod]
    public void ApplyStats_RosterWithoutTheAgent_PlainAgentUnchanged()
    {
        HeroRangedHooks.Roster = new HeroRangedRoster<Agent>();
        HeroRangedHooks.Roster.Add(BareAgent(), Kit());
        var p = Props();

        HeroRangedHooks.ApplyStats(BareAgent(), p);

        AssertUnchanged(p);
    }

    [TestMethod]
    public void ApplyStats_NullAgent_Unchanged()
    {
        HeroRangedHooks.Roster = new HeroRangedRoster<Agent>();
        HeroRangedHooks.Roster.Add(BareAgent(), Kit());
        var p = Props();

        HeroRangedHooks.ApplyStats(null!, p);

        AssertUnchanged(p);
    }

    [TestMethod]
    public void PiercesShields_NoRosterOrNullOrUnlisted_False()
    {
        var hero = BareAgent();
        Assert.IsFalse(HeroRangedHooks.PiercesShields(hero), "no roster");

        HeroRangedHooks.Roster = new HeroRangedRoster<Agent>();
        HeroRangedHooks.Roster.Add(hero, Kit());

        Assert.IsFalse(HeroRangedHooks.PiercesShields(null));
        Assert.IsFalse(HeroRangedHooks.PiercesShields(BareAgent()));
    }

    [TestMethod]
    public void PiercesShields_ListedWithTheFlag_True_WithoutIt_False()
    {
        var hero = BareAgent();
        var other = BareAgent();
        HeroRangedHooks.Roster = new HeroRangedRoster<Agent>();
        HeroRangedHooks.Roster.Add(hero, Kit(pierceShields: true));
        HeroRangedHooks.Roster.Add(other, Kit(pierceShields: false));

        Assert.IsTrue(HeroRangedHooks.PiercesShields(hero));
        Assert.IsFalse(HeroRangedHooks.PiercesShields(other));
    }
}
