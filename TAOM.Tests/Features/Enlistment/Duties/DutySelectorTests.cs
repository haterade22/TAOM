using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.Enlistment.Duties;
using TAOM.Features.TroopProgression;

namespace TAOM.Tests.Features.Enlistment.Duties;

[TestClass]
public class DutySelectorTests
{
    private IRandomProvider _random = null!;
    private DutySelector _selector = null!;

    [TestInitialize]
    public void Setup()
    {
        _random = Substitute.For<IRandomProvider>();
        _selector = new DutySelector(_random);
    }

    private static ServiceProgressSnapshot Progress(ServiceRank rank = ServiceRank.Recruit, int trust = 0, ServiceAssignment assignment = ServiceAssignment.Infantry) =>
        new ServiceProgressSnapshot { Rank = rank, Trust = trust, Assignment = assignment };

    private static DutyDefinition Field(string id, GateSpec gates = null) => new DutyDefinition { Id = id, Gates = gates ?? new GateSpec() };

    private static InteractiveDutyDefinition Interactive(string id, GateSpec gates = null) => new InteractiveDutyDefinition { Id = id, Gates = gates ?? new GateSpec() };

    [TestMethod]
    public void SelectOffer_NullDuties_ReturnsNoOffer()
    {
        var result = _selector.SelectOffer(null, Progress(), new ArmyRhythmSnapshot(), new List<string>(), pressure: false);
        Assert.IsFalse(result.HasOffer);
    }

    [TestMethod]
    public void SelectOffer_NoEligibleRows_ReturnsNoOffer()
    {
        var duties = new EnlistmentDutiesConfig
        {
            FieldDuties = new List<DutyDefinition> { Field("only_field", new GateSpec { MinRank = ServiceRank.Sergeant }) },
        };

        var result = _selector.SelectOffer(duties, Progress(rank: ServiceRank.Recruit), new ArmyRhythmSnapshot(), new List<string>(), pressure: false);

        Assert.IsFalse(result.HasOffer);
    }

    [TestMethod]
    public void SelectOffer_OnlyFieldEligible_ReturnsThatFieldDuty()
    {
        var duties = new EnlistmentDutiesConfig
        {
            FieldDuties = new List<DutyDefinition> { Field("recon_sweep") },
        };
        _random.Next(Arg.Any<int>()).Returns(0);

        var result = _selector.SelectOffer(duties, Progress(), new ArmyRhythmSnapshot(), new List<string>(), pressure: false);

        Assert.IsNotNull(result.FieldDuty);
        Assert.AreEqual("recon_sweep", result.FieldDuty.Id);
        Assert.IsNull(result.InteractiveDuty);
    }

    [TestMethod]
    public void SelectOffer_OnlyInteractiveEligible_ReturnsThatInteractiveDuty()
    {
        var duties = new EnlistmentDutiesConfig
        {
            InteractiveDuties = new List<InteractiveDutyDefinition> { Interactive("night_patrol") },
        };
        _random.Next(Arg.Any<int>()).Returns(0);

        var result = _selector.SelectOffer(duties, Progress(), new ArmyRhythmSnapshot(), new List<string>(), pressure: false);

        Assert.IsNotNull(result.InteractiveDuty);
        Assert.AreEqual("night_patrol", result.InteractiveDuty.Id);
        Assert.IsNull(result.FieldDuty);
    }

    [TestMethod]
    public void SelectOffer_RollWithinFieldWeightBand_PicksField()
    {
        var duties = new EnlistmentDutiesConfig
        {
            FieldDuties = new List<DutyDefinition> { Field("field_a") },
            InteractiveDuties = new List<InteractiveDutyDefinition> { Interactive("interactive_a") },
        };
        // Both baseline weight 1 -> total weight 2. Roll 0 falls in the field's [0,1) band.
        _random.Next(2).Returns(0);

        var result = _selector.SelectOffer(duties, Progress(), new ArmyRhythmSnapshot(), new List<string>(), pressure: false);

        Assert.AreEqual("field_a", result.FieldDuty?.Id);
    }

    [TestMethod]
    public void SelectOffer_RollWithinInteractiveWeightBand_PicksInteractive()
    {
        var duties = new EnlistmentDutiesConfig
        {
            FieldDuties = new List<DutyDefinition> { Field("field_a") },
            InteractiveDuties = new List<InteractiveDutyDefinition> { Interactive("interactive_a") },
        };
        // Total weight 2. Roll 1 falls in the interactive's [1,2) band.
        _random.Next(2).Returns(1);

        var result = _selector.SelectOffer(duties, Progress(), new ArmyRhythmSnapshot(), new List<string>(), pressure: false);

        Assert.AreEqual("interactive_a", result.InteractiveDuty?.Id);
    }

    [TestMethod]
    public void SelectOffer_PreferredAssignmentWeightsHigher_RollLandsOnPreferredDuty()
    {
        var duties = new EnlistmentDutiesConfig
        {
            FieldDuties = new List<DutyDefinition>
            {
                Field("baseline", new GateSpec()),
                Field("preferred", new GateSpec { AssignmentAffinity = new List<ServiceAssignment> { ServiceAssignment.Cavalry } }),
            },
        };
        // baseline weight=1, preferred weight=3 -> total 4. Roll 2 falls past baseline's [0,1) into preferred's [1,4).
        _random.Next(4).Returns(2);

        var result = _selector.SelectOffer(duties, Progress(assignment: ServiceAssignment.Cavalry), new ArmyRhythmSnapshot(), new List<string>(), pressure: false);

        Assert.AreEqual("preferred", result.FieldDuty?.Id);
    }

    [TestMethod]
    public void SelectOffer_RecentDutyExcludedWithoutPressure_IsFilteredOut()
    {
        var duties = new EnlistmentDutiesConfig
        {
            FieldDuties = new List<DutyDefinition> { Field("recent_one") },
        };

        var result = _selector.SelectOffer(duties, Progress(), new ArmyRhythmSnapshot(), new List<string> { "recent_one" }, pressure: false);

        Assert.IsFalse(result.HasOffer);
    }

    [TestMethod]
    public void SelectOffer_RecentDutyIncludedUnderPressure_IsStillEligible()
    {
        var duties = new EnlistmentDutiesConfig
        {
            FieldDuties = new List<DutyDefinition> { Field("recent_one") },
        };
        _random.Next(Arg.Any<int>()).Returns(0);

        var result = _selector.SelectOffer(duties, Progress(), new ArmyRhythmSnapshot(), new List<string> { "recent_one" }, pressure: true);

        Assert.AreEqual("recent_one", result.FieldDuty?.Id);
    }

    [TestMethod]
    public void SelectIncident_NullPool_ReturnsNull()
    {
        Assert.IsNull(_selector.SelectIncident(null, Progress(), new ArmyRhythmSnapshot()));
    }

    [TestMethod]
    public void SelectIncident_IneligibleRow_IsSkipped()
    {
        var incidents = new List<IncidentDefinition>
        {
            new IncidentDefinition { Id = "gated", Chance = 1f, Gates = new GateSpec { MinRank = ServiceRank.Sergeant } },
        };

        var result = _selector.SelectIncident(incidents, Progress(rank: ServiceRank.Recruit), new ArmyRhythmSnapshot());

        Assert.IsNull(result);
    }

    [TestMethod]
    public void SelectIncident_RollBelowChanceThreshold_ReturnsThatIncident()
    {
        var incidents = new List<IncidentDefinition> { new IncidentDefinition { Id = "pay_delay", Chance = 0.22f } };
        _random.Next(1000).Returns(0);

        var result = _selector.SelectIncident(incidents, Progress(), new ArmyRhythmSnapshot());

        Assert.AreEqual("pay_delay", result?.Id);
    }

    [TestMethod]
    public void SelectIncident_RollAboveChanceThreshold_FallsThroughToNull()
    {
        var incidents = new List<IncidentDefinition> { new IncidentDefinition { Id = "pay_delay", Chance = 0.22f } };
        _random.Next(1000).Returns(999);

        var result = _selector.SelectIncident(incidents, Progress(), new ArmyRhythmSnapshot());

        Assert.IsNull(result);
    }

    [TestMethod]
    public void SelectIncident_FirstEligibleFails_SecondPasses_ReturnsSecond()
    {
        var incidents = new List<IncidentDefinition>
        {
            new IncidentDefinition { Id = "first", Chance = 0.1f },
            new IncidentDefinition { Id = "second", Chance = 0.9f },
        };
        _random.Next(1000).Returns(500); // fails "first" (threshold 100), passes "second" (threshold 900)

        var result = _selector.SelectIncident(incidents, Progress(), new ArmyRhythmSnapshot());

        Assert.AreEqual("second", result?.Id);
    }
}
