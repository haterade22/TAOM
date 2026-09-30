using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Core;
using TAOM.Adapters;
using TAOM.Features.SiegeDismount.Models;

namespace TAOM.Tests.Adapters;

/// <summary>
/// The siege dismount adapters outside a campaign. <c>Hero.MainHero</c> (<c>CharacterObject.PlayerCharacter.HeroObject</c>)
/// and <c>MobileParty.MainParty</c> (<c>Campaign.Current.MainParty</c>) throw inside their own getters there, before a
/// <c>?.</c> after them can help; that NRE escaped <c>SiegeDismountMissionBehavior.AfterStart</c> and a Custom Battle
/// siege never finished loading (2026-09-30, Edoras; RCA <c>docs/reviews/rca-siege-dismount-custom-battle-2026-09-30.md</c>).
/// A Custom Battle has a <c>Game</c> whose <c>PlayerTroop</c> is a <c>BasicCharacterObject</c>:
/// <see cref="HeroBattleEquipmentOf_CustomBattleTroop_ReturnsNull"/> runs that cast. The test process has no Game and no
/// campaign at all, so the other tests prove only that every adapter member survives that state without throwing.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class SiegeDismountAdaptersNoCampaignTests
{
    [TestMethod]
    public void HeroBattleEquipmentOf_CustomBattleTroop_ReturnsNull()
    {
        // CustomBattleHelper.StartGame sets Game.PlayerTroop to a BasicCharacterObject; a hard cast throws here.
        Assert.IsNull(PlayerMountAdapter.HeroBattleEquipmentOf(new BasicCharacterObject()));
    }

    [TestMethod]
    public void HasMount_NoGame_ReturnsFalse()
    {
        Assert.IsFalse(new PlayerMountAdapter().HasMount());
    }

    [TestMethod]
    public void Capture_NoGame_ReturnsAnEmptySnapshot()
    {
        Assert.IsFalse(new PlayerMountAdapter().Capture().HasMount);
    }

    [TestMethod]
    public void Clear_NoGame_DoesNotThrow()
    {
        new PlayerMountAdapter().Clear();
    }

    [TestMethod]
    public void Restore_NoGame_DoesNotThrow()
    {
        new PlayerMountAdapter().Restore(new MountSnapshot("horse", "harness"));
    }

    [TestMethod]
    public void Deposit_NoCampaign_DoesNotThrow()
    {
        new PartyMountInventoryAdapter().Deposit(new MountSnapshot("horse", "harness"));
    }

    [TestMethod]
    public void Withdraw_NoCampaign_DoesNotThrow()
    {
        new PartyMountInventoryAdapter().Withdraw(new MountSnapshot("horse", "harness"));
    }
}
