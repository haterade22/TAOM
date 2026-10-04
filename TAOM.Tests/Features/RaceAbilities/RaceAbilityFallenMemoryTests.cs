using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.RaceAbilities;
using TAOM.Features.RaceAbilities.Domain;

// Who is remembered as fallen kin, for the KinFell trigger. Plain objects stand in for teams.

namespace TAOM.Tests.Features.RaceAbilities;

[TestClass]
public class RaceAbilityFallenMemoryTests
{
    private readonly object _team = new object();
    private readonly object _enemyTeam = new object();
    private readonly RaceAbilityProfile _berserk = new RaceAbilityProfile { AbilityId = "berserk" };
    private readonly RaceAbilityProfile _other = new RaceAbilityProfile { AbilityId = "other" };
    private readonly List<FallenSense> _into = new List<FallenSense>();
    private RaceAbilityFallenMemory<object> _sut = null!;

    [TestInitialize]
    public void Setup() => _sut = new RaceAbilityFallenMemory<object>();

    [TestMethod]
    public void SenseKin_SameTeamSameProfile_IsKin()
    {
        _sut.Remember(3f, 4f, _team, race: 5, _berserk, at: 100f);

        _sut.SenseKin(_team, _berserk, new HashSet<int>(), 0f, 0f, _into);

        Assert.AreEqual(1, _into.Count);
        Assert.AreEqual(5f, _into[0].Distance, 0.0001f);
        Assert.AreEqual(100f, _into[0].At, 0.0001f);
    }

    [TestMethod]
    public void SenseKin_OtherTeam_IsNotKin()
    {
        _sut.Remember(0f, 0f, _enemyTeam, race: 5, _berserk, at: 100f);

        _sut.SenseKin(_team, _berserk, new HashSet<int>(), 0f, 0f, _into);

        Assert.AreEqual(0, _into.Count);
    }

    [TestMethod]
    public void SenseKin_OtherProfile_IsKinOnlyThroughAKinRace()
    {
        _sut.Remember(0f, 0f, _team, race: 7, _other, at: 100f);

        _sut.SenseKin(_team, _berserk, new HashSet<int>(), 0f, 0f, _into);
        Assert.AreEqual(0, _into.Count);

        _sut.SenseKin(_team, _berserk, new HashSet<int> { 7 }, 0f, 0f, _into);
        Assert.AreEqual(1, _into.Count);
    }

    [TestMethod]
    public void Remember_PastCapacity_DropsTheOldest()
    {
        for (var i = 0; i < RaceAbilityFallenMemory<object>.Capacity + 1; i++)
            _sut.Remember(i, 0f, _team, race: 5, _berserk, at: i);

        Assert.AreEqual(RaceAbilityFallenMemory<object>.Capacity, _sut.Count);
        _sut.SenseKin(_team, _berserk, new HashSet<int>(), 0f, 0f, _into);
        Assert.AreEqual(1f, _into.Min(f => f.At), 0.0001f);
    }

    [TestMethod]
    public void Forget_DropsDeathsOlderThanTheMemory()
    {
        _sut.Remember(0f, 0f, _team, race: 5, _berserk, at: 100f);
        _sut.Remember(0f, 0f, _team, race: 5, _berserk, at: 120f);

        _sut.Forget(now: 100f + RaceAbilityFallenMemory<object>.MemorySeconds + 1f);

        Assert.AreEqual(1, _sut.Count);
    }

    [TestMethod]
    public void Forget_NaNTime_KeepsNothingStale()
    {
        _sut.Remember(0f, 0f, _team, race: 5, _berserk, at: 100f);

        _sut.Forget(float.NaN);

        Assert.AreEqual(0, _sut.Count);
    }

    [TestMethod]
    public void Clear_ForgetsEveryone()
    {
        _sut.Remember(0f, 0f, _team, race: 5, _berserk, at: 100f);

        _sut.Clear();

        Assert.AreEqual(0, _sut.Count);
    }
}
