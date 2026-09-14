using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CompanionTactics.BattleActionBar;
using TAOM.Features.CompanionTactics.BattleActionBar.Models;

namespace TAOM.Tests.Features.CompanionTactics.BattleActionBar;

[TestClass]
public class TroopStanceManagerTests
{
    private TroopStanceManager _sut = null!;

    [TestInitialize]
    public void Setup() => _sut = new TroopStanceManager();

    [TestMethod]
    public void GetStance_NeverSet_ReturnsNone()
    {
        Assert.AreEqual(TroopStance.None, _sut.GetStance(0));
    }

    [TestMethod]
    public void SetStance_ThenGet_ReturnsSetStance()
    {
        _sut.SetStance(formationIndex: 0, TroopStance.PikeWall);

        Assert.AreEqual(TroopStance.PikeWall, _sut.GetStance(0));
    }

    [TestMethod]
    public void SetStance_SameStanceTwice_ClearsToggleBehavior()
    {
        // Source's behavior: setting the same stance toggles off.
        _sut.SetStance(0, TroopStance.PikeWall);
        _sut.SetStance(0, TroopStance.PikeWall);

        Assert.AreEqual(TroopStance.None, _sut.GetStance(0));
    }

    [TestMethod]
    public void SetStance_DifferentStance_Replaces()
    {
        _sut.SetStance(0, TroopStance.PikeWall);
        _sut.SetStance(0, TroopStance.Testudo);

        Assert.AreEqual(TroopStance.Testudo, _sut.GetStance(0));
    }

    [TestMethod]
    public void ClearStance_RemovesStance()
    {
        _sut.SetStance(0, TroopStance.PikeWall);

        _sut.ClearStance(0);

        Assert.AreEqual(TroopStance.None, _sut.GetStance(0));
    }

    [TestMethod]
    public void ClearAllStances_RemovesAllFormationStates()
    {
        _sut.SetStance(0, TroopStance.PikeWall);
        _sut.SetStance(1, TroopStance.Testudo);
        _sut.SetStance(2, TroopStance.LineCharge);

        _sut.ClearAllStances();

        Assert.AreEqual(TroopStance.None, _sut.GetStance(0));
        Assert.AreEqual(TroopStance.None, _sut.GetStance(1));
        Assert.AreEqual(TroopStance.None, _sut.GetStance(2));
    }

    [TestMethod]
    public void SetStance_PerFormationIsolation_StatesIndependent()
    {
        _sut.SetStance(0, TroopStance.PikeWall);
        _sut.SetStance(1, TroopStance.Testudo);

        Assert.AreEqual(TroopStance.PikeWall, _sut.GetStance(0));
        Assert.AreEqual(TroopStance.Testudo, _sut.GetStance(1));
    }

    [TestMethod]
    public void ClearStance_OnUnsetFormation_NoOp()
    {
        _sut.ClearStance(99);

        Assert.AreEqual(TroopStance.None, _sut.GetStance(99));
    }

    // #595: Patch35 clears stances from Formation.SetMovementOrder, which the engine calls on its
    // asynchronous agent tick for AI-controlled player formations, while the action bar reads and
    // writes on the main thread. The lock is what makes that safe.
    [TestMethod]
    public void ConcurrentSetGetClear_FromSeveralThreads_NeverThrows()
    {
        var threads = new Thread[8];
        Exception failure = null;
        for (int t = 0; t < threads.Length; t++)
        {
            int seed = t;
            threads[t] = new Thread(() =>
            {
                try
                {
                    for (int n = 0; n < 5000; n++)
                    {
                        int formation = (n + seed) % 8;
                        switch (n % 4)
                        {
                            case 0: _sut.SetStance(formation, TroopStance.PikeWall); break;
                            case 1: _sut.GetStance(formation); break;
                            case 2: _sut.ClearStance(formation); break;
                            default: _sut.SetStance(formation, TroopStance.Testudo); break;
                        }
                    }
                }
                catch (Exception e) { failure = e; }
            });
        }
        foreach (var th in threads) th.Start();
        foreach (var th in threads) th.Join();

        Assert.IsNull(failure, failure?.ToString());
        _sut.ClearAllStances();
        for (int f = 0; f < 8; f++) Assert.AreEqual(TroopStance.None, _sut.GetStance(f));
    }
}
