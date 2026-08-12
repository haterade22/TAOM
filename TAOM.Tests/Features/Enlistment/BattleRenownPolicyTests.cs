using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// Field report 3: "you do not earn enough renown from battles".
///
/// The report is literally true and the cause is structural rather than a bad number. TAOM grants
/// zero renown of its own, and the vanilla share the player would otherwise receive is computed from
/// his party's contribution to the battle — an enlisted player IS a party of one hero, so his share
/// rounds to nothing no matter how well he fights. Renown has to be granted for the service, or it
/// does not arrive.
///
/// The award is deliberately a small flat base plus the merit band's own figure, so it scales with
/// how the player actually fought (the band is already scored from kills, damage and survival) and
/// stays in one config file with every other payout.
/// </summary>
[TestClass]
public class BattleRenownPolicyTests
{
    private static readonly ProgressionTables Tables = new ProgressionTables
    {
        BattleWinRenown = 2,
        BattleLossRenown = 1,
    };

    [TestMethod]
    public void Victory_PaysTheWinBase()
        => Assert.AreEqual(2, BattleRenownPolicy.Compute(won: true, bandRenown: 0, Tables));

    [TestMethod]
    public void Defeat_StillPaysSomething()
        // Losing alongside your lord is still service rendered, and a defeat the player fought
        // through is exactly when the feature should not feel like it ignored him.
        => Assert.AreEqual(1, BattleRenownPolicy.Compute(won: false, bandRenown: 0, Tables));

    [TestMethod]
    public void MeritBandRenownAddsToTheBase()
        => Assert.AreEqual(7, BattleRenownPolicy.Compute(won: true, bandRenown: 5, Tables));

    [TestMethod]
    public void NoBandSample_StillPaysTheBase()
        // A battle the accumulator never sampled (joined late, no geometry) has no band. The base
        // must not depend on it.
        => Assert.AreEqual(2, BattleRenownPolicy.Compute(won: true, bandRenown: 0, Tables));

    [TestMethod]
    public void NegativeConfigIsClampedRatherThanSubtracting()
        // Config is hand-edited JSON. A negative renown award would silently strip the player's
        // clan renown on every battle, which is worse than the bug being fixed.
        => Assert.AreEqual(0, BattleRenownPolicy.Compute(
            won: true, bandRenown: -50, new ProgressionTables { BattleWinRenown = 2 }));

    [TestMethod]
    public void ZeroConfigDisablesTheAwardEntirely()
        // The off switch: a server or player who wants vanilla's contribution-only renown sets both
        // to 0 and gets no grant at all, rather than a floor they cannot remove.
        => Assert.AreEqual(0, BattleRenownPolicy.Compute(
            won: true, bandRenown: 0, new ProgressionTables { BattleWinRenown = 0, BattleLossRenown = 0 }));
}
