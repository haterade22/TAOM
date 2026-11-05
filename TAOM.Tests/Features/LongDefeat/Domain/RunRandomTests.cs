using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>
/// The vectors below are pinned in tools/tests/test_run_long_defeat.py as well, taken from an independent Python SplitMix64. Matching the
/// raw outputs alone would not stop the game and the leaderboard bot from drifting in how they derive a generator, so the start tuple is pinned too.
/// </summary>
[TestClass]
public class RunRandomTests
{
    private static ulong[] First3(ulong seed, int stream, int a, int b)
    {
        var random = RunRandom.For(seed, stream, a, b);
        return new[] { random.Next(), random.Next(), random.Next() };
    }

    [TestMethod]
    public void For_SeedZeroAllZero_IsTheReferenceSplitMix64Sequence()
    {
        CollectionAssert.AreEqual(new[] { 16294208416658607535UL, 7960286522194355700UL, 487617019471545679UL }, First3(0, 0, 0, 0));
    }

    [DataTestMethod]
    [DataRow(1UL, 1, 5, 0, 2680490473348048908UL, 12813384503896764240UL, 3276828284648647372UL)]
    [DataRow(1UL, 1, 10, 0, 7412692618947198593UL, 16684773309981356620UL, 15431304901105078789UL)]
    [DataRow(1UL, 1, 15, 0, 16936612977461133540UL, 8028821510459182801UL, 4634075737854962681UL)]
    [DataRow(1UL, 2, 0, 0, 17911839290282890590UL, 8196980753821780235UL, 8195237237126968761UL)]
    [DataRow(1UL, 3, 0, 0, 7862637804313477842UL, 13015481187462834606UL, 15212506146343009075UL)]
    [DataRow(9223372036854775807UL, 2, 7, 3, 6093248950766842696UL, 15789776932012360393UL, 7519895420366855887UL)]
    [DataRow(12345UL, 1, 40, 0, 10993538888499654729UL, 6960065003137395634UL, 11458948633771559195UL)]
    public void For_PinnedTuples_GiveThePinnedOutputs(ulong seed, int stream, int a, int b, ulong first, ulong second, ulong third)
    {
        CollectionAssert.AreEqual(new[] { first, second, third }, First3(seed, stream, a, b));
    }

    [TestMethod]
    public void For_DifferentStreamsOrIndexes_AreIndependent()
    {
        Assert.AreNotEqual(First3(1, 1, 5, 0)[0], First3(1, 1, 6, 0)[0]);
        Assert.AreNotEqual(First3(1, 1, 5, 0)[0], First3(1, 2, 5, 0)[0]);
        Assert.AreNotEqual(First3(1, 1, 5, 0)[0], First3(1, 1, 5, 1)[0]);
    }

    [TestMethod]
    public void For_TheSameTuple_RepeatsExactly()
    {
        CollectionAssert.AreEqual(First3(7, 2, 3, 4), First3(7, 2, 3, 4));
    }

    [TestMethod]
    public void Below_IsTheDrawModuloN()
    {
        var expected = (int)(2680490473348048908UL % 8UL);

        Assert.AreEqual(expected, RunRandom.For(1, 1, 5, 0).Below(8));
    }

    [TestMethod]
    public void Below_StaysInRange_ForEveryDraw()
    {
        var random = RunRandom.For(99, 1, 1, 0);
        for (var i = 0; i < 500; i++)
        {
            var value = random.Below(7);
            Assert.IsTrue(value >= 0 && value < 7, "draw " + i + " gave " + value);
        }
    }

    [TestMethod]
    public void Below_ZeroOrNegative_GivesZeroAndStillConsumesADraw()
    {
        var a = RunRandom.For(1, 1, 5, 0);
        Assert.AreEqual(0, a.Below(0));
        Assert.AreEqual(0, a.Below(-3));
        var b = RunRandom.For(1, 1, 5, 0);
        b.Next();
        b.Next();

        Assert.AreEqual(b.Next(), a.Next());
    }

    [TestMethod]
    public void StreamNumbers_AreThePlans()
    {
        Assert.AreEqual(1, RunRandom.AffixStream);
        Assert.AreEqual(2, RunRandom.OfferStream);
        Assert.AreEqual(3, RunRandom.ClassStream);
    }
}
