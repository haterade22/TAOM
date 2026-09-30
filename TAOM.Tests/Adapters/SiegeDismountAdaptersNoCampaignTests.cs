using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Features.SiegeDismount.Models;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Adapters;

/// <summary>
/// The siege dismount adapters with no campaign. A Custom Battle has no main hero (its player troop is a
/// <c>BasicCharacterObject</c>, so <c>CharacterObject.PlayerCharacter</c> is null) and no main party, and
/// <c>Hero.MainHero</c> (<c>CharacterObject.PlayerCharacter.HeroObject</c>) and <c>MobileParty.MainParty</c>
/// (<c>Campaign.Current.MainParty</c>) throw inside their own getters there, before a <c>?.</c> after them can help.
/// That NRE escaped <c>SiegeDismountMissionBehavior.AfterStart</c> and the Custom Battle siege never finished
/// loading (2026-09-30, Edoras). The test process has neither a game nor a campaign, the same state for these
/// reads, so every adapter method must answer "nothing" instead of throwing.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class SiegeDismountAdaptersNoCampaignTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    [TestMethod]
    public void HasMount_NoCampaign_ReturnsFalse()
    {
        RequireGame();

        Assert.IsFalse(new PlayerMountAdapter().HasMount());
    }

    [TestMethod]
    public void Capture_NoCampaign_ReturnsAnEmptySnapshot()
    {
        RequireGame();

        Assert.IsFalse(new PlayerMountAdapter().Capture().HasMount);
    }

    [TestMethod]
    public void ClearAndRestore_NoCampaign_DoNothing()
    {
        RequireGame();
        var adapter = new PlayerMountAdapter();

        adapter.Clear();
        adapter.Restore(new MountSnapshot("horse", "harness"));
    }

    [TestMethod]
    public void DepositAndWithdraw_NoCampaign_DoNothing()
    {
        RequireGame();
        var adapter = new PartyMountInventoryAdapter();

        adapter.Deposit(new MountSnapshot("horse", "harness"));
        adapter.Withdraw(new MountSnapshot("horse", "harness"));
    }
}
