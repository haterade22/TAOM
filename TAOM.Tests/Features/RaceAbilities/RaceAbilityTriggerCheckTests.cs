using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.RaceAbilities;
using TAOM.Features.RaceAbilities.Domain;

namespace TAOM.Tests.Features.RaceAbilities;

[TestClass]
public class RaceAbilityTriggerCheckTests
{
    [TestMethod]
    public void TryParseKind_CommaList_False() =>
        Assert.IsFalse(RaceAbilityTriggerCheck.TryParseKind("EnemyWithin,CavalryClosing", out _));

    [TestMethod]
    public void TryParseKind_ExactNameAnyCase_True()
    {
        Assert.IsTrue(RaceAbilityTriggerCheck.TryParseKind(" enemieswithin ", out var kind));
        Assert.AreEqual(RaceAbilityTriggerKind.EnemiesWithin, kind);
    }

    [TestMethod]
    public void Validate_BadRange_Skipped()
    {
        var warnings = new List<string>();
        var trigger = new RaceAbilityTrigger { Kind = "EnemyWithin", Range = 41f };

        Assert.IsNull(RaceAbilityTriggerCheck.Validate(trigger, "f", warnings.Add));
        CollectionAssert.AreEqual(new[] { "f.range=41 must be a finite value in (0,40], trigger skipped" }, warnings);
    }

    [TestMethod]
    public void Validate_NaNRange_Skipped() =>
        Assert.IsNull(RaceAbilityTriggerCheck.Validate(new RaceAbilityTrigger { Kind = "EnemyWithin", Range = float.NaN }, "f", _ => { }));

    [TestMethod]
    public void Validate_KinWithinRangeAbove15_Skipped() =>
        Assert.IsNull(RaceAbilityTriggerCheck.Validate(new RaceAbilityTrigger { Kind = "KinWithin", Range = 20f, Count = 2 }, "f", _ => { }));

    [TestMethod]
    public void ValidateAll_KeepsGoodDropsBadAndNames_Index()
    {
        var warnings = new List<string>();
        var list = new List<RaceAbilityTrigger>
        {
            new RaceAbilityTrigger { Kind = "EnemiesWithin", Range = 10f, Count = 3 },
            new RaceAbilityTrigger { Kind = "Nope" },
        };

        var result = RaceAbilityTriggerCheck.ValidateAll(list, "anyOf", warnings.Add);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(RaceAbilityTriggerKind.EnemiesWithin, result[0].ParsedKind);
        CollectionAssert.AreEqual(new[] { "anyOf[1].kind='Nope' is not a known trigger kind, skipped" }, warnings);
    }

    [TestMethod]
    public void ValidateAll_Null_Empty() =>
        Assert.AreEqual(0, RaceAbilityTriggerCheck.ValidateAll(null, "f", _ => { }).Count);
}
