using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HeroAbilities.Hooks;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Tests.Features.HeroAbilities;

// The zero-aim-error pass (#1361, decision R6): the ten properties the engine's two stat models write for aim error
// (AgentStatCalculateModel.cs:170, 198, 220-223, 235; CustomBattleAgentStatCalculateModel.cs:235-309, 359) become 0,
// and none of the other 88 changes. AgentDrivenProperties holds a float[98] (AgentDrivenProperties.cs:1178).
[TestClass]
public class HeroPrecisionApplierTests
{
    private static readonly HashSet<DrivenProperty> TheTen = new()
    {
        DrivenProperty.AiShooterError,
        DrivenProperty.AiShooterErrorWoRangeUpdate,
        DrivenProperty.AiRangerLeadErrorMin,
        DrivenProperty.AiRangerLeadErrorMax,
        DrivenProperty.AiRangerVerticalErrorMultiplier,
        DrivenProperty.AiRangerHorizontalErrorMultiplier,
        DrivenProperty.WeaponInaccuracy,
        // The properties WeaponMaxMovementAccuracyPenalty and WeaponMaxUnsteadyAccuracyPenalty (AgentDrivenProperties.cs:84-100).
        DrivenProperty.WeaponWorstMobileAccuracyPenalty,
        DrivenProperty.WeaponWorstUnsteadyAccuracyPenalty,
        DrivenProperty.WeaponRotationalAccuracyPenaltyInRadians,
    };

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Apply_ZeroesTheTen_AndNothingElse()
    {
        var props = new AgentDrivenProperties();
        for (var i = 0; i < (int)DrivenProperty.Count; i++)
            props.SetStat((DrivenProperty)i, 1f + i);

        HeroPrecisionApplier.Apply(props);

        Assert.AreEqual(10, TheTen.Count);
        for (var i = 0; i < (int)DrivenProperty.Count; i++)
        {
            var property = (DrivenProperty)i;
            var expected = TheTen.Contains(property) ? 0f : 1f + i;
            Assert.AreEqual(expected, props.GetStat(property), property.ToString());
        }
    }

    // The precision check counts the ten that are not 0; a NaN is not 0, so it fails the check.
    [TestMethod]
    [TestCategory("RequiresGame")]
    public void CountNonZero_CountsOnlyTheTen_AndNaN()
    {
        var props = new AgentDrivenProperties();
        for (var i = 0; i < (int)DrivenProperty.Count; i++)
            props.SetStat((DrivenProperty)i, 1f + i);
        HeroPrecisionApplier.Apply(props);

        Assert.AreEqual(0, HeroPrecisionApplier.CountNonZero(props), "the other 88 are not counted");

        props.SetStat(DrivenProperty.WeaponInaccuracy, 0.001f);
        props.SetStat(DrivenProperty.AiShooterError, float.NaN);
        Assert.AreEqual(2, HeroPrecisionApplier.CountNonZero(props));
    }
}
