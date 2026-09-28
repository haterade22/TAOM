using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Spider;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

// The strike cap through SpiderAttackService.StrikeArc (#692): a creature bandit's capped strike hits only the nearest
// enemies the radial strike reports, and an ally in the arc never uses a slot; the ridden spider's uncapped strike still
// hits every enemy in its arc. ProjectAgent is the observable: HandleSpiderTargetHit calls it for each struck target on
// foot. (SpiderAttack itself resolves its clip through the engine's action table, which a unit test does not have.)

namespace TAOM.Tests.Features.Spider;

[TestClass]
[TestCategory("RequiresGame")]
public class SpiderStrikeCapTests
{
    private SpiderAttackService _sut;

    [TestInitialize]
    public void Setup() => _sut = new SpiderAttackService(Substitute.For<IMissionAdapterFactory>(), Substitute.For<IModLogger>());

    private static IAgentAdapter Spider()
    {
        var spider = Substitute.For<IAgentAdapter>();
        spider.IsActive().Returns(true);
        spider.RiderAgent.Returns((IAgentAdapter)null);
        spider.Health.Returns(200);
        spider.Position.Returns(Vec3.Zero);
        spider.MovementVelocity.Returns(Vec2.Zero);
        return spider;
    }

    private static IAgentAdapter InArc(IAgentAdapter spider, float distance, bool ally = false)
    {
        var target = Substitute.For<IAgentAdapter>();
        target.IsActive().Returns(true);
        target.IsFadingOut().Returns(false);
        target.State.Returns(AgentState.Active);
        target.HasMount.Returns(false);
        target.IsMount.Returns(false);
        target.RiderAgent.Returns((IAgentAdapter)null);
        target.Position.Returns(new Vec3(distance, 0f, 0f));
        spider.IsSameTeam(target).Returns(ally);
        return target;
    }

    // The radial strike as the engine reports it: each agent in the arc, in the order given.
    private static Action<Action<IAgentAdapter, IAgentAdapter, sbyte>> Reports(IAgentAdapter spider, params IAgentAdapter[] inArc)
        => hit => { foreach (var target in inArc) hit(spider, target, 0); };

    private static bool Struck(IAgentAdapter target)
        => target.ReceivedCalls().Any(c => c.GetMethodInfo().Name == nameof(IAgentAdapter.ProjectAgent));

    [TestMethod]
    public void CappedStrike_HitsOnlyTheNearestEnemies_AnAllyNeverTakesASlot()
    {
        var spider = Spider();
        var ally = InArc(spider, 0.5f, ally: true);
        var far = InArc(spider, 3f);
        var nearest = InArc(spider, 1f);
        var second = InArc(spider, 2f);
        var farther = InArc(spider, 4f);
        var capTwo = new SpiderStrikeProfile(2, 1f, knockdownOnCritOnly: true);

        var (inArc, allies, _) = _sut.StrikeArc(spider, capTwo, countAllies: true, Reports(spider, ally, far, nearest, second, farther));

        Assert.AreEqual(5, inArc);
        Assert.AreEqual(1, allies);

        Assert.IsTrue(Struck(nearest), "nearest enemy struck");
        Assert.IsTrue(Struck(second), "second nearest struck");
        Assert.IsFalse(Struck(far) || Struck(farther), "the cap stops at two");
        Assert.IsFalse(Struck(ally), "an ally is never struck and never uses a slot");
    }

    // A cavalryman's horse: its side is its rider's (IsSameSide), and the arc may report the horse, the man, or both.
    private static IAgentAdapter HorseOf(IAgentAdapter rider, float distance)
    {
        var horse = Substitute.For<IAgentAdapter>();
        horse.IsActive().Returns(true);
        horse.IsFadingOut().Returns(false);
        horse.State.Returns(AgentState.Active);
        horse.HasMount.Returns(false);
        horse.IsMount.Returns(true);
        horse.RiderAgent.Returns(rider);
        horse.Position.Returns(new Vec3(distance, 0f, 0f));
        return horse;
    }

    [TestMethod]
    public void CappedStrike_ACavalrymanAndHisHorseInTheArc_UseOneSlot_TheRiderTakesIt()
    {
        var spider = Spider();
        var rider = InArc(spider, 2f);
        var horse = HorseOf(rider, 1f);
        var footman = InArc(spider, 3f);

        _sut.StrikeArc(spider, new SpiderStrikeProfile(2, 1f, knockdownOnCritOnly: true),
            countAllies: false, Reports(spider, horse, rider, footman));

        Assert.IsTrue(Struck(rider) && Struck(footman), "the rider and the next enemy take the two slots");
        Assert.IsFalse(Struck(horse), "the horse of a rider in the arc is not a second target");
    }

    [TestMethod]
    public void CappedStrike_AHorseWhoseRiderIsOutOfTheArc_IsATargetOfItsOwn()
    {
        var spider = Spider();
        var rider = InArc(spider, 30f);   // never reported by the arc
        var horse = HorseOf(rider, 1f);

        _sut.StrikeArc(spider, new SpiderStrikeProfile(1, 1f, knockdownOnCritOnly: true),
            countAllies: false, Reports(spider, horse));

        Assert.IsTrue(Struck(horse), "a horse alone in the arc is struck");
    }

    [TestMethod]
    public void CappedStrike_ALooseHorseNearer_NeverTakesTheSoldiersSlot()
    {
        // Codex 2026-09-28 F2: the arc reports every live agent, and a teamless loose horse is on nobody's side, so a
        // nearer one used to take a one-target bite from the soldier who opened the engage gate.
        var spider = Spider();
        var looseHorse = HorseOf(rider: null!, 1.0f);
        var soldier = InArc(spider, 1.4f);

        _sut.StrikeArc(spider, new SpiderStrikeProfile(1, 1f, knockdownOnCritOnly: true),
            countAllies: false, Reports(spider, looseHorse, soldier));

        Assert.IsTrue(Struck(soldier), "the soldier takes the bite");
        Assert.IsFalse(Struck(looseHorse), "a riderless mount is never a creature's target");
    }

    [TestMethod]
    public void RiddenStrike_HitsEveryEnemyInTheArc()
    {
        var spider = Spider();
        var enemies = new[] { InArc(spider, 3f), InArc(spider, 1f), InArc(spider, 2f), InArc(spider, 4f) };
        _sut.StrikeArc(spider, SpiderStrikeProfile.Ridden, countAllies: false, Reports(spider, enemies));

        foreach (var enemy in enemies)
            Assert.IsTrue(Struck(enemy), "the ridden spider's strike is uncapped");
    }
}
