using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FieldCamp.Domain;

namespace TAOM.Tests.Features.FieldCamp;

/// <summary>
/// Guards on the persisted camp record. The elapsed-time branch of BuildProgress dereferences
/// Campaign.Current and stays in-game territory; the degenerate-hours short-circuits and the
/// enum round-trip are what a unit test can and must pin.
/// </summary>
[TestClass]
public class CampStateTests
{
    [TestMethod]
    public void BuildProgress_ZeroBuildHours_ReturnsComplete()
    {
        var state = new CampState { BuildHours = 0f };

        Assert.AreEqual(1f, state.BuildProgress());
    }

    [TestMethod]
    public void BuildProgress_NegativeBuildHours_ReturnsComplete()
    {
        var state = new CampState { BuildHours = -3f };

        Assert.AreEqual(1f, state.BuildProgress());
    }

    [TestMethod]
    public void IsReady_ZeroBuildHours_True()
    {
        var state = new CampState { BuildHours = 0f };

        Assert.IsTrue(state.IsReady);
    }

    [TestMethod]
    public void TypeEnum_EveryCampType_RoundTripsThroughSavedInt()
    {
        foreach (CampType type in Enum.GetValues(typeof(CampType)))
        {
            var state = new CampState { TypeEnum = type };

            Assert.AreEqual((int)type, state.Type, $"saved int for {type}");
            Assert.AreEqual(type, state.TypeEnum, $"round-trip for {type}");
        }
    }

    [TestMethod]
    public void TypeEnum_SavedIntAssignedDirectly_ReadsBackAsEnum()
    {
        var state = new CampState { Type = (int)CampType.Lookout };

        Assert.AreEqual(CampType.Lookout, state.TypeEnum);
    }
}
