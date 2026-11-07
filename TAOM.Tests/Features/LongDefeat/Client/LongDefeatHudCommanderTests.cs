using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client.Features.LongDefeat;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Client;

/// <summary>The HUD strip's commander line (phase2-plan.md section 5): the rank and the next ability's countdown while he stands, a line when he falls, and nothing between waves or once he left.</summary>
[TestClass]
public class LongDefeatHudCommanderTests
{
    private static CommanderAbilityView Ability(string name, float left, bool running = false) => new("{=TAOM_LD_Test_" + name + "}" + name, left, running);

    private static RunView View(CommanderView? commander, RunPhase phase = RunPhase.Wave) =>
        new(phase, 4, 3, 100, 30f, 40, 0, 30, 0f, new List<ShopOffer>()) { Commander = commander };

    private static CommanderView Captain(CommanderOutcome outcome = CommanderOutcome.Standing, bool up = true, params CommanderAbilityView[] abilities) =>
        new("{=TAOM_LD_Rank_Captain}Captain", outcome, up, abilities);

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void WhileHeStands_TheLineNamesHisRankAndHisSoonestAbilityWithItsSeconds()
    {
        var view = View(Captain(CommanderOutcome.Standing, true, Ability("Summon", 41.2f), Ability("Cry", 11.1f)));

        Assert.AreEqual("Captain: Cry in 12 s", LongDefeatHudText.CommanderLine(view));
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void ARunningWarCry_ShowsAsNow()
    {
        var view = View(Captain(CommanderOutcome.Standing, true, Ability("Cry", 55f, running: true), Ability("Summon", 4f)));

        Assert.AreEqual("Captain: Cry, now", LongDefeatHudText.CommanderLine(view));
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void AKilledCommander_HasFallen()
    {
        Assert.AreEqual("Captain has fallen", LongDefeatHudText.CommanderLine(View(Captain(CommanderOutcome.Killed))));
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void ACommanderWhoWasFelledOrLeft_IsNoLongerShown()
    {
        Assert.AreEqual(string.Empty, LongDefeatHudText.CommanderLine(View(Captain(CommanderOutcome.Felled))));
        Assert.AreEqual(string.Empty, LongDefeatHudText.CommanderLine(View(Captain(CommanderOutcome.Left))));
    }

    [TestMethod]
    public void BeforeHeSpawns_BetweenWaves_AndWithNone_TheLineIsEmpty()
    {
        Assert.AreEqual(string.Empty, LongDefeatHudText.CommanderLine(View(Captain(CommanderOutcome.Standing, false, Ability("Cry", 20f)))));
        Assert.AreEqual(string.Empty, LongDefeatHudText.CommanderLine(View(null)));
        Assert.AreEqual(string.Empty, LongDefeatHudText.CommanderLine(View(Captain(CommanderOutcome.Standing, true, Ability("Cry", 20f)), RunPhase.Intermission)));
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void AStandingCommanderWithNoAbility_ShowsJustHisRank()
    {
        Assert.AreEqual("Captain", LongDefeatHudText.CommanderLine(View(Captain())));
    }
}
