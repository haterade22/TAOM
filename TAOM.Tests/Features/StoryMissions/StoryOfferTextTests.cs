using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.StoryMissions;
using TAOM.Features.StoryMissions.Domain;

namespace TAOM.Tests.Features.StoryMissions;

/// <summary>
/// The story offer's lines (#1105 slice 4, #1326): the popup asks about the sides the player may take, and the menu reports the result
/// for the side he took. Each line is its own key, so a translation of one never shows for another.
/// </summary>
[TestClass]
public class StoryOfferTextTests
{
    private static readonly StoryChoices DefendOnly = new(true, false);
    private static readonly StoryChoices AttackOnly = new(false, true);
    private static readonly StoryChoices Both = new(true, true);

    [TestMethod]
    public void PopupBody_FollowsTheSidesOnOffer()
    {
        StringAssert.StartsWith(StoryOfferText.PopupBody(DefendOnly), "{=taom_story_offer_body}");
        StringAssert.StartsWith(StoryOfferText.PopupBody(AttackOnly), "{=taom_story_offer_body_attack}");
        StringAssert.StartsWith(StoryOfferText.PopupBody(Both), "{=taom_story_offer_body_both}");
    }

    [TestMethod]
    public void PopupAccept_AnAttackerMarches_EveryoneElseRides()
    {
        StringAssert.StartsWith(StoryOfferText.PopupAccept(DefendOnly), "{=taom_story_offer_ride}");
        StringAssert.StartsWith(StoryOfferText.PopupAccept(Both), "{=taom_story_offer_ride}");
        StringAssert.StartsWith(StoryOfferText.PopupAccept(AttackOnly), "{=taom_story_offer_march}");
    }

    [TestMethod]
    public void Menu_BeforeTheMission_NamesTheEnemyOnlyToADefender()
    {
        StringAssert.StartsWith(StoryOfferText.Menu(DefendOnly, StorySide.Defending, null), "{=taom_story_offer_menu}");
        StringAssert.StartsWith(StoryOfferText.Menu(AttackOnly, StorySide.Defending, null), "{=taom_story_offer_menu_host}");
        StringAssert.StartsWith(StoryOfferText.Menu(Both, StorySide.Defending, null), "{=taom_story_offer_menu_host}");
    }

    [TestMethod]
    public void Menu_AfterADefence_FollowsTheAppliedResult()
    {
        StringAssert.StartsWith(StoryOfferText.Menu(DefendOnly, StorySide.Defending, StoryResult.Held), "{=taom_story_offer_won}");
        StringAssert.StartsWith(StoryOfferText.Menu(DefendOnly, StorySide.Defending, StoryResult.Fell), "{=taom_story_offer_lost}");
        StringAssert.StartsWith(StoryOfferText.Menu(DefendOnly, StorySide.Defending, StoryResult.Failed), "{=taom_story_offer_undecided}", "a failed transfer gave nothing away");
        StringAssert.StartsWith(StoryOfferText.Menu(DefendOnly, StorySide.Defending, StoryResult.Undecided), "{=taom_story_offer_undecided}");
        StringAssert.StartsWith(StoryOfferText.Menu(DefendOnly, StorySide.Defending, StoryResult.Unchanged), "{=taom_story_offer_none}");
    }

    [TestMethod]
    public void Menu_AfterAnAttack_ReportsItFromTheAttackersSide()
    {
        StringAssert.StartsWith(StoryOfferText.Menu(AttackOnly, StorySide.Attacking, StoryResult.Fell), "{=taom_story_offer_stormed}");
        StringAssert.StartsWith(StoryOfferText.Menu(AttackOnly, StorySide.Attacking, StoryResult.Held), "{=taom_story_offer_repulsed}");
        StringAssert.StartsWith(StoryOfferText.Menu(AttackOnly, StorySide.Attacking, StoryResult.Undecided), "{=taom_story_offer_undecided}");
        StringAssert.StartsWith(StoryOfferText.Menu(AttackOnly, StorySide.Attacking, StoryResult.Unchanged), "{=taom_story_offer_none}");
    }

    [TestMethod]
    public void Menu_NoOffer_SaysTheMomentHasPassed()
    {
        StringAssert.StartsWith(StoryOfferText.Menu(null, StorySide.Defending, null), "{=taom_story_offer_none}");
        StringAssert.StartsWith(StoryOfferText.Menu(null, StorySide.Attacking, StoryResult.Held), "{=taom_story_offer_none}");
    }

    [TestMethod]
    public void AHeroStory_HasItsOwnLines_AndNamesNoFortress()
    {
        // #1105 slice 3: the Breaking of the Fellowship has no walls and no {TARGET}.
        var both = new StoryChoices(true, true);
        var lines = new[]
        {
            StoryOfferText.PopupBody(both, hero: true),
            StoryOfferText.PopupBody(new StoryChoices(true, false), hero: true),
            StoryOfferText.PopupBody(new StoryChoices(false, true), hero: true),
            StoryOfferText.PopupAccept(both, hero: true),
            StoryOfferText.Menu(both, StorySide.Defending, null, hero: true),
            StoryOfferText.Menu(both, StorySide.Defending, StoryResult.HeroLives, hero: true),
            StoryOfferText.Menu(both, StorySide.Attacking, StoryResult.HeroLives, hero: true),
            StoryOfferText.Menu(both, StorySide.Defending, StoryResult.HeroDies, hero: true),
            StoryOfferText.Menu(both, StorySide.Attacking, StoryResult.HeroDies, hero: true),
            StoryOfferText.Menu(both, StorySide.Defending, StoryResult.Undecided, hero: true),
        };

        Assert.AreEqual(lines.Length, lines.Distinct().Count(), "each state has its own line");
        foreach (var line in lines)
        {
            Assert.IsFalse(line.Contains("{TARGET}"), line);
            StringAssert.StartsWith(line, "{=taom_story_breaking_", line);
        }

        Assert.AreNotEqual(StoryOfferText.PopupBody(both), StoryOfferText.PopupBody(both, hero: true));
    }
}
