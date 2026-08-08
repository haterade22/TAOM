using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.Enlistment.Duties;
using TAOM.Features.TroopProgression;

namespace TAOM.Tests.Features.Enlistment.Duties;

[TestClass]
public class DutySelectorTests
{
    private IRandomProvider _random = null!;
    private IModLogger _logger = null!;
    private DutySelector _selector = null!;

    [TestInitialize]
    public void Setup()
    {
        _random = Substitute.For<IRandomProvider>();
        _logger = Substitute.For<IModLogger>();
        _selector = new DutySelector(_random, _logger);
    }

    /// <summary>Replays a fixed roll sequence so two runs of the same input are provably identical.</summary>
    private sealed class SequenceRandom : IRandomProvider
    {
        private readonly int[] _values;
        private int _index;

        public SequenceRandom(params int[] values) => _values = values;

        public int Next(int maxValue)
        {
            if (maxValue <= 0)
                return 0;
            var value = _values[_index % _values.Length];
            _index++;
            return value % maxValue;
        }
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

    // ---- GateSpec.Weight (per-row rarity) ----

    private static GateSpec Weighted(int weight) => new GateSpec { Weight = weight };

    [TestMethod]
    public void SelectOffer_AbsentWeight_DefaultsToOneAndKeepsUniformBands()
    {
        // Two ungated rows, neither declaring a weight -> total 2, one band each: exactly the
        // pre-weighting behaviour. This is the regression pin for "existing rows are unaffected".
        Assert.AreEqual(1, new GateSpec().Weight, "an absent weight must default to 1");
        var duties = new EnlistmentDutiesConfig
        {
            FieldDuties = new List<DutyDefinition> { Field("field_a"), Field("field_b") },
        };
        _random.Next(2).Returns(1);

        var result = _selector.SelectOffer(duties, Progress(), new ArmyRhythmSnapshot(), new List<string>(), pressure: false);

        Assert.AreEqual("field_b", result.FieldDuty?.Id);
    }

    [TestMethod]
    public void SelectOffer_WeightMultipliesAffinity_WidensThePreferredBand()
    {
        var duties = new EnlistmentDutiesConfig
        {
            FieldDuties = new List<DutyDefinition>
            {
                Field("routine", new GateSpec { Weight = 5 }),
                Field("rare", new GateSpec { Weight = 2, AssignmentAffinity = new List<ServiceAssignment> { ServiceAssignment.Cavalry } }),
            },
        };
        // routine = baseline 1 x 5 -> [0,5); rare = preferred 3 x 2 -> [5,11). Total 11.
        _random.Next(11).Returns(5);

        var result = _selector.SelectOffer(duties, Progress(assignment: ServiceAssignment.Cavalry), new ArmyRhythmSnapshot(), new List<string>(), pressure: false);

        Assert.AreEqual("rare", result.FieldDuty?.Id);
    }

    [TestMethod]
    public void SelectOffer_ZeroWeight_SkipsRowAndWarns()
    {
        var duties = new EnlistmentDutiesConfig
        {
            FieldDuties = new List<DutyDefinition> { Field("zero_weight_row", Weighted(0)), Field("kept") },
        };
        _random.Next(1).Returns(0); // only "kept" survives, so the total weight is 1

        var result = _selector.SelectOffer(duties, Progress(), new ArmyRhythmSnapshot(), new List<string>(), pressure: false);

        Assert.AreEqual("kept", result.FieldDuty?.Id);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("zero_weight_row")));
    }

    [TestMethod]
    public void SelectOffer_NegativeWeight_SkipsRowAndWarns()
    {
        var duties = new EnlistmentDutiesConfig
        {
            FieldDuties = new List<DutyDefinition> { Field("neg_weight_row", Weighted(-3)) },
        };

        var result = _selector.SelectOffer(duties, Progress(), new ArmyRhythmSnapshot(), new List<string>(), pressure: false);

        Assert.IsFalse(result.HasOffer);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("neg_weight_row")));
    }

    [TestMethod]
    public void SelectOffer_WeightAboveMaximum_SkipsRowAndWarns()
    {
        var duties = new EnlistmentDutiesConfig
        {
            FieldDuties = new List<DutyDefinition> { Field("huge_weight_row", Weighted(DutySelector.MaxSelectionWeight + 1)) },
        };

        var result = _selector.SelectOffer(duties, Progress(), new ArmyRhythmSnapshot(), new List<string>(), pressure: false);

        Assert.IsFalse(result.HasOffer);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("huge_weight_row")));
    }

    [TestMethod]
    public void SelectOffer_WeightAtMaximum_IsAccepted()
    {
        var duties = new EnlistmentDutiesConfig
        {
            FieldDuties = new List<DutyDefinition> { Field("at_the_cap", Weighted(DutySelector.MaxSelectionWeight)) },
        };
        _random.Next(DutySelector.MaxSelectionWeight).Returns(0);

        var result = _selector.SelectOffer(duties, Progress(), new ArmyRhythmSnapshot(), new List<string>(), pressure: false);

        Assert.AreEqual("at_the_cap", result.FieldDuty?.Id);
    }

    [TestMethod]
    public void SelectOffer_ZeroWeightOnInteractiveRow_SkipsRowAndWarns()
    {
        var duties = new EnlistmentDutiesConfig
        {
            InteractiveDuties = new List<InteractiveDutyDefinition> { Interactive("interactive_zero_row", Weighted(0)) },
        };

        var result = _selector.SelectOffer(duties, Progress(), new ArmyRhythmSnapshot(), new List<string>(), pressure: false);

        Assert.IsFalse(result.HasOffer);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("interactive_zero_row")));
    }

    [TestMethod]
    public void SelectOffer_SameUnusableRowSeenTwice_WarnsOnce()
    {
        var duties = new EnlistmentDutiesConfig
        {
            FieldDuties = new List<DutyDefinition> { Field("repeat_zero_row", Weighted(0)) },
        };

        _selector.SelectOffer(duties, Progress(), new ArmyRhythmSnapshot(), new List<string>(), pressure: false);
        _selector.SelectOffer(duties, Progress(), new ArmyRhythmSnapshot(), new List<string>(), pressure: false);

        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("repeat_zero_row")));
    }

    [TestMethod]
    public void SelectOffer_SameRollSequenceReplayed_ReturnsIdenticalPicks()
    {
        var expected = new[] { "one", "two", "two", "three", "three", "three" };

        var first = RunSixOffers();
        var second = RunSixOffers();

        CollectionAssert.AreEqual(expected, first, "weights 1/2/3 must map rolls 0-5 onto bands [0,1) [1,3) [3,6)");
        CollectionAssert.AreEqual(first, second, "replaying the same roll sequence must reproduce the same picks");

        string[] RunSixOffers()
        {
            var duties = new EnlistmentDutiesConfig
            {
                FieldDuties = new List<DutyDefinition>
                {
                    Field("one", Weighted(1)),
                    Field("two", Weighted(2)),
                    Field("three", Weighted(3)),
                },
            };
            var selector = new DutySelector(new SequenceRandom(0, 1, 2, 3, 4, 5), Substitute.For<IModLogger>());
            return Enumerable.Range(0, 6)
                .Select(_ => selector.SelectOffer(duties, Progress(), new ArmyRhythmSnapshot(), new List<string>(), pressure: false).FieldDuty?.Id ?? "")
                .ToArray();
        }
    }
}
