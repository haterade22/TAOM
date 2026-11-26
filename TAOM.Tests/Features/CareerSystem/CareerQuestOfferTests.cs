using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Features.CareerSystem.Quests;

namespace TAOM.Tests.Features.CareerSystem;

/// <summary>
/// A tier quest is offered only for a tier the hero has not already opened, by level or by an earlier quest:
/// the career screen opens a tier on either.
/// </summary>
[TestClass]
public class CareerQuestOfferTests
{
    private ICareerQuestService _quests = null!;
    private CareerQuestDefinition _tier2 = null!;

    [TestInitialize]
    public void Setup()
    {
        _quests = Substitute.For<ICareerQuestService>();
        _tier2 = new CareerQuestDefinition("q2", "c", 2, "", "", null!, null!);
        _quests.GetQuestFor("c", 2).Returns(_tier2);
    }

    [TestMethod]
    public void TierOpenByLevel_IsNotOffered()
    {
        _quests.IsTierUnlocked(20, 1, "h").Returns(true);
        _quests.IsTierUnlocked(20, 2, "h").Returns(true);

        Assert.IsNull(CareerQuestOffers.NextOffer(_quests, "c", 20, "h", _ => false));
    }

    [TestMethod]
    public void TierStillClosed_IsOffered()
    {
        _quests.IsTierUnlocked(20, 1, "h").Returns(true);

        Assert.AreSame(_tier2, CareerQuestOffers.NextOffer(_quests, "c", 20, "h", _ => false));
    }

    [TestMethod]
    public void DeclinedQuest_IsNotOffered()
    {
        _quests.IsTierUnlocked(20, 1, "h").Returns(true);

        Assert.IsNull(CareerQuestOffers.NextOffer(_quests, "c", 20, "h", id => id == "q2"));
    }
}
