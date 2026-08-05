using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.Enlistment.Duties;

namespace TAOM.Tests.Features.Enlistment.Duties;

[TestClass]
public class DutyGateEvaluatorTests
{
    private static readonly HashSet<string> NoContexts = new HashSet<string>();

    [TestMethod]
    public void IsEligible_NullGates_ReturnsTrue()
    {
        Assert.IsTrue(DutyGateEvaluator.IsEligible(null, ServiceRank.Recruit, 0, NoContexts));
    }

    [TestMethod]
    public void IsEligible_RankBelowMinimum_ReturnsFalse()
    {
        var gates = new GateSpec { MinRank = ServiceRank.Veteran };
        Assert.IsFalse(DutyGateEvaluator.IsEligible(gates, ServiceRank.Soldier, 0, NoContexts));
    }

    [TestMethod]
    public void IsEligible_RankAtOrAboveMinimum_ReturnsTrue()
    {
        var gates = new GateSpec { MinRank = ServiceRank.Veteran };
        Assert.IsTrue(DutyGateEvaluator.IsEligible(gates, ServiceRank.Veteran, 0, NoContexts));
    }

    [TestMethod]
    public void IsEligible_TrustBelowMinimum_ReturnsFalse()
    {
        var gates = new GateSpec { MinTrust = 5 };
        Assert.IsFalse(DutyGateEvaluator.IsEligible(gates, ServiceRank.Recruit, 4, NoContexts));
    }

    [TestMethod]
    public void IsEligible_TrustAboveMaximum_ReturnsFalse()
    {
        var gates = new GateSpec { MaxTrust = 20 };
        Assert.IsFalse(DutyGateEvaluator.IsEligible(gates, ServiceRank.Recruit, 21, NoContexts));
    }

    [TestMethod]
    public void IsEligible_RequiredContextMissing_ReturnsFalse()
    {
        var gates = new GateSpec { RequiredContexts = new List<string> { "siege" } };
        Assert.IsFalse(DutyGateEvaluator.IsEligible(gates, ServiceRank.Recruit, 0, NoContexts));
    }

    [TestMethod]
    public void IsEligible_RequiredContextPresent_ReturnsTrue()
    {
        var gates = new GateSpec { RequiredContexts = new List<string> { "siege" } };
        var active = new HashSet<string> { "siege" };
        Assert.IsTrue(DutyGateEvaluator.IsEligible(gates, ServiceRank.Recruit, 0, active));
    }

    [TestMethod]
    public void IsEligible_ExcludedContextPresent_ReturnsFalse()
    {
        var gates = new GateSpec { ExcludedContexts = new List<string> { "siege" } };
        var active = new HashSet<string> { "siege" };
        Assert.IsFalse(DutyGateEvaluator.IsEligible(gates, ServiceRank.Recruit, 0, active));
    }

    [TestMethod]
    public void IsEligible_NoContextRequirement_ReturnsTrue()
    {
        var gates = new GateSpec();
        Assert.IsTrue(DutyGateEvaluator.IsEligible(gates, ServiceRank.Recruit, 0, NoContexts));
    }

    [TestMethod]
    public void AffinityWeight_NoAffinityList_ReturnsBaseline()
    {
        var gates = new GateSpec();
        Assert.AreEqual(DutyGateEvaluator.BaselineWeight, DutyGateEvaluator.AffinityWeight(gates, ServiceAssignment.Cavalry));
    }

    [TestMethod]
    public void AffinityWeight_AssignmentInAffinityList_ReturnsPreferred()
    {
        var gates = new GateSpec { AssignmentAffinity = new List<ServiceAssignment> { ServiceAssignment.Cavalry } };
        Assert.AreEqual(DutyGateEvaluator.PreferredAssignmentWeight, DutyGateEvaluator.AffinityWeight(gates, ServiceAssignment.Cavalry));
    }

    [TestMethod]
    public void AffinityWeight_AssignmentNotInAffinityList_ReturnsBaseline()
    {
        var gates = new GateSpec { AssignmentAffinity = new List<ServiceAssignment> { ServiceAssignment.Cavalry } };
        Assert.AreEqual(DutyGateEvaluator.BaselineWeight, DutyGateEvaluator.AffinityWeight(gates, ServiceAssignment.Infantry));
    }

    [TestMethod]
    public void AffinityWeight_NullGates_ReturnsBaseline()
    {
        Assert.AreEqual(DutyGateEvaluator.BaselineWeight, DutyGateEvaluator.AffinityWeight(null, ServiceAssignment.Infantry));
    }

    [TestMethod]
    public void ActiveContexts_NullSnapshot_ReturnsEmptySet()
    {
        var contexts = DutyGateEvaluator.ActiveContexts(null);
        Assert.AreEqual(0, contexts.Count);
    }

    [TestMethod]
    public void ActiveContexts_AllFlagsSet_ReturnsAllSixContexts()
    {
        var rhythm = new ArmyRhythmSnapshot
        {
            SiegePressure = true,
            Naval = true,
            Blockade = true,
            InArmy = true,
            QuietGarrison = true,
            Marching = true,
        };

        var contexts = DutyGateEvaluator.ActiveContexts(rhythm);

        CollectionAssert.AreEquivalent(
            new[] { "siege", "naval", "blockade", "army", "garrison", "march" },
            new List<string>(contexts));
    }

    [TestMethod]
    public void ActiveContexts_NoFlagsSet_ReturnsEmptySet()
    {
        var contexts = DutyGateEvaluator.ActiveContexts(new ArmyRhythmSnapshot());
        Assert.AreEqual(0, contexts.Count);
    }
}
