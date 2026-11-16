using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CoopInterop;
using TAOM.Features.PrisonerRecruitment.Models;
using TaleWorlds.CampaignSystem;

namespace TAOM.Tests.Features.PrisonerRecruitment;

/// <summary>
/// #1371: when a co-op guest recruits prisoners, the server applies the morale cost, and there
/// <c>Hero.MainHero</c> is the server's own hero. The model must ask the player query, or the guest pays the AI
/// rule while the guest's party screen showed the player rule.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")] // builds engine Heroes
public class TaomPrisonerRecruitmentPlayerTests
{
    [DataTestMethod]
    [DataRow(PlayerControl.Player, true)]
    [DataRow(PlayerControl.NotPlayer, false)]
    // Unknown is not a player here, as in TaomPregnancyModel: an unreadable registry keeps the AI rule.
    [DataRow(PlayerControl.Unknown, false)]
    public void IsPlayerLeader_AnswersFromTheQuery(PlayerControl ofLeader, bool expected)
    {
        var leader = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
        var players = Substitute.For<IPlayerControlQuery>();
        players.OfHero(leader).Returns(ofLeader);

        Assert.AreEqual(expected, TaomPrisonerRecruitmentCalculationModel.IsPlayerLeader(leader, players));
    }
}
