using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TAOM.Features.DevConsole;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.DevConsole;

/// <summary>The gate for commands that start a game (taom.fords_start): no game yet, cheat mode on.</summary>
[TestClass]
[TestCategory("RequiresGame")]
public class DevConsoleMainMenuGateTests
{
    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    [TestMethod]
    public void MainMenuVerdict_NoGameAndCheatsOn_IsAllowed()
    {
        Assert.IsNull(DevConsoleGuard.MainMenuVerdict(gameRunning: false, cheatMode: true));
    }

    [TestMethod]
    public void MainMenuVerdict_NoGameAndCheatsOff_IsTheVanillaCheatMessage()
    {
        Assert.AreEqual(CampaignCheats.CheatModeDisabled, DevConsoleGuard.MainMenuVerdict(gameRunning: false, cheatMode: false));
    }

    [TestMethod]
    public void MainMenuVerdict_AGameIsRunning_AsksForTheMainMenu_WhateverTheCheatFlag()
    {
        Assert.AreEqual(DevConsoleGuard.NotAtMainMenu, DevConsoleGuard.MainMenuVerdict(gameRunning: true, cheatMode: true));
        Assert.AreEqual(DevConsoleGuard.NotAtMainMenu, DevConsoleGuard.MainMenuVerdict(gameRunning: true, cheatMode: false));
    }

    [TestMethod]
    public void CheckMainMenuCheatUsage_InTheTestHost_HasNoGameAndCheatsOff()
    {
        var error = "";

        var allowed = DevConsoleGuard.CheckMainMenuCheatUsage(ref error);

        Assert.IsFalse(allowed);
        Assert.AreEqual(CampaignCheats.CheatModeDisabled, error);
    }
}
