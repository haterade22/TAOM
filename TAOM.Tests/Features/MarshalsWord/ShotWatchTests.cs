using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>The shot and shooter counter of the volley control (build-spec 11), over plain objects.</summary>
[TestClass]
public class ShotWatchTests
{
    [TestMethod]
    public void Note_CountsEveryShot_AndEachShooterOnce()
    {
        var watch = new ShotWatch<object>();
        var a = new object();
        var b = new object();

        watch.Note(a);
        watch.Note(b);
        watch.Note(a);
        watch.Note(a);

        Assert.AreEqual(4, watch.Shots);
        Assert.AreEqual(2, watch.Shooters);
    }

    [TestMethod]
    public void Note_ShootersAreToldApartByReference_NotByValue()
    {
        var watch = new ShotWatch<string>();

        watch.Note(new string('x', 3));
        watch.Note(new string('x', 3));

        Assert.AreEqual(2, watch.Shooters);
    }

    [TestMethod]
    public void Reset_EmptiesTheCountsAndTheShooters()
    {
        var watch = new ShotWatch<object>();
        watch.Note(new object());

        watch.Reset();

        Assert.AreEqual(0, watch.Shots);
        Assert.AreEqual(0, watch.Shooters);
        Assert.AreEqual(0, watch.Overflow);
    }

    [TestMethod]
    public void Note_PastTheTableOfShooters_StillCountsTheShots_AndCountsTheOverflow()
    {
        var watch = new ShotWatch<object>();
        for (var i = 0; i < ShotWatch<object>.Capacity + 5; i++)
            watch.Note(new object());

        Assert.AreEqual(ShotWatch<object>.Capacity + 5, watch.Shots);
        Assert.AreEqual(ShotWatch<object>.Capacity, watch.Shooters);
        Assert.AreEqual(5, watch.Overflow);
    }

    [TestMethod]
    public void Note_FromManyThreadsAtOnce_LosesNoShot_AndCountsEachShooterOnce()
    {
        var watch = new ShotWatch<object>();
        var shooters = new object[40];
        for (var i = 0; i < shooters.Length; i++)
            shooters[i] = new object();

        Parallel.For(0, 4000, i => watch.Note(shooters[i % shooters.Length]));

        Assert.AreEqual(4000, watch.Shots);
        Assert.AreEqual(40, watch.Shooters);
    }
}
