using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CheatMenu;
using TAOM.Features.CoopInterop;
using TAOM.Features.SpecialResources;

namespace TAOM.Tests.Features.CheatMenu;

/// <summary>
/// When a teleport may move the party (CheatMenuPolicy.CheckTeleport), one test per refusal, and when the menu shows and
/// opens (CheatMenuPolicy.IsMenuAvailable, CheatMenuOpener). Case 12 (14 Nov 2026) crashed on a party moved out of a village
/// whose menu stayed open, so a party inside a settlement must be refused, not moved.
/// </summary>
[TestClass]
public class CheatMenuPolicyTests
{
    private static TeleportState Free() => CheatMenuServiceTests.Free();

    [TestMethod]
    public void CheckTeleport_FreePartyOnLand_None() =>
        Assert.AreEqual(TeleportRefusal.None, CheatMenuPolicy.CheckTeleport(Free()));

    [TestMethod]
    public void CheckTeleport_NoCampaign_Refused() =>
        Assert.AreEqual(TeleportRefusal.NoCampaign, CheatMenuPolicy.CheckTeleport(Free() with { CampaignRunning = false }));

    [TestMethod]
    public void CheckTeleport_InMission_Refused() =>
        Assert.AreEqual(TeleportRefusal.InMission, CheatMenuPolicy.CheckTeleport(Free() with { InMission = true }));

    [TestMethod]
    public void CheckTeleport_InsideASettlement_Refused() =>
        Assert.AreEqual(TeleportRefusal.InSettlement, CheatMenuPolicy.CheckTeleport(Free() with { InSettlement = true }));

    [TestMethod]
    public void CheckTeleport_InABattleOrEncounter_Refused() =>
        Assert.AreEqual(TeleportRefusal.InBattleOrEncounter, CheatMenuPolicy.CheckTeleport(Free() with { InMapEventOrEncounter = true }));

    [TestMethod]
    public void CheckTeleport_InASiege_Refused() =>
        Assert.AreEqual(TeleportRefusal.InSiege, CheatMenuPolicy.CheckTeleport(Free() with { InSiege = true }));

    [TestMethod]
    public void CheckTeleport_InAnArmyItDoesNotLead_Refused() =>
        Assert.AreEqual(TeleportRefusal.InArmyNotLeader, CheatMenuPolicy.CheckTeleport(Free() with { InArmyNotLeader = true }));

    [TestMethod]
    public void CheckTeleport_Prisoner_Refused() =>
        Assert.AreEqual(TeleportRefusal.Prisoner, CheatMenuPolicy.CheckTeleport(Free() with { IsPrisoner = true }));

    [TestMethod]
    public void CheckTeleport_PartyParkedByEnlistment_Refused() =>
        Assert.AreEqual(TeleportRefusal.PartyParked, CheatMenuPolicy.CheckTeleport(Free() with { PartyActive = false }));

    [TestMethod]
    public void CheckTeleport_AtSea_Refused() =>
        Assert.AreEqual(TeleportRefusal.AtSea, CheatMenuPolicy.CheckTeleport(Free() with { AtSea = true }));

    [TestMethod]
    public void CheckTeleport_InAFieldCamp_Refused() =>
        Assert.AreEqual(TeleportRefusal.InFieldCamp, CheatMenuPolicy.CheckTeleport(Free() with { InFieldCamp = true }));

    [TestMethod]
    public void CheckTeleport_PrisonerInsideASettlement_NamesTheCaptivityFirst() =>
        Assert.AreEqual(TeleportRefusal.Prisoner,
            CheatMenuPolicy.CheckTeleport(Free() with { IsPrisoner = true, InSettlement = true }));

    [TestMethod]
    public void IsMenuAvailable_OnlyWithTheSwitchOnInSinglePlayer()
    {
        Assert.IsTrue(CheatMenuPolicy.IsMenuAvailable(enabled: true, singlePlayerAuthority: true));
        Assert.IsFalse(CheatMenuPolicy.IsMenuAvailable(enabled: false, singlePlayerAuthority: true));
        Assert.IsFalse(CheatMenuPolicy.IsMenuAvailable(enabled: true, singlePlayerAuthority: false));
    }

    // ---- opener ----

    private static (CheatMenuOpener Opener, ICheatMenuSettingsProvider Settings, ICoopSessionProvider Coop, ICheatMenuHostAdapter Host) Opener()
    {
        var settings = Substitute.For<ICheatMenuSettingsProvider>();
        settings.EnableCheatMenu.Returns(true);
        var coop = Substitute.For<ICoopSessionProvider>();
        coop.IsAuthority.Returns(true);
        var service = new CheatMenuService(Substitute.For<ICheatWorldAdapter>(), Substitute.For<ISpecialResourceService>(), coop,
            Substitute.For<IDedicatedServerProvider>(), Substitute.For<IModLogger>(), new CheatPartyRules(),
            Substitute.For<IPartySizeCacheInvalidator>());
        var host = Substitute.For<ICheatMenuHostAdapter>();
        host.IsMapClearForMenu.Returns(true);
        return (new CheatMenuOpener(settings, service, host), settings, coop, host);
    }

    [TestMethod]
    public void Opener_SwitchOff_OffAndHiddenAndNotOpened()
    {
        var (opener, settings, _, host) = Opener();
        settings.EnableCheatMenu.Returns(false);

        Assert.AreEqual(CheatMenuAvailability.Off, opener.GetAvailability());
        Assert.IsFalse(opener.IsShown);
        Assert.IsFalse(opener.TryOpen());
        host.DidNotReceive().OpenMenu();
    }

    [TestMethod]
    public void Opener_Coop_CoopAndHidden()
    {
        var (opener, _, coop, host) = Opener();
        coop.IsSessionActive.Returns(true);

        Assert.AreEqual(CheatMenuAvailability.Coop, opener.GetAvailability());
        Assert.IsFalse(opener.IsShown);
        Assert.IsFalse(opener.TryOpen());
        host.DidNotReceive().OpenMenu();
    }

    [TestMethod]
    public void Opener_MapBusy_ShownButNotOpened()
    {
        var (opener, _, _, host) = Opener();
        host.IsMapClearForMenu.Returns(false);

        Assert.AreEqual(CheatMenuAvailability.MapBusy, opener.GetAvailability());
        Assert.IsTrue(opener.IsShown, "the button stays on the bar, disabled, while a menu or screen is open");
        Assert.IsFalse(opener.TryOpen());
        host.DidNotReceive().OpenMenu();
    }

    [TestMethod]
    public void Opener_Available_OpensTheMenu()
    {
        var (opener, _, _, host) = Opener();

        Assert.IsTrue(opener.TryOpen());
        host.Received(1).OpenMenu();
    }

    // ---- map key: the same gates as the button, and no key read while the menu is off ----

    [TestMethod]
    public void HotKey_SwitchOff_KeyNotReadAndNothingOpens()
    {
        var (opener, settings, _, host) = Opener();
        settings.EnableCheatMenu.Returns(false);
        var reads = 0;

        Assert.IsFalse(opener.TryOpenFromHotKey(() => { reads++; return true; }));
        Assert.AreEqual(0, reads, "the key is read while the switch is off");
        host.DidNotReceive().OpenMenu();
    }

    [TestMethod]
    public void HotKey_Coop_KeyNotReadAndNothingOpens()
    {
        var (opener, _, coop, host) = Opener();
        coop.IsSessionActive.Returns(true);
        var reads = 0;

        Assert.IsFalse(opener.TryOpenFromHotKey(() => { reads++; return true; }));
        Assert.AreEqual(0, reads, "the key is read in a co-op session");
        host.DidNotReceive().OpenMenu();
    }

    [TestMethod]
    public void HotKey_OnSinglePlayerMapFree_PressOpensTheMenu()
    {
        var (opener, _, _, host) = Opener();

        Assert.IsTrue(opener.TryOpenFromHotKey(() => true));
        host.Received(1).OpenMenu();
    }

    [TestMethod]
    public void HotKey_NotPressed_NothingOpens()
    {
        var (opener, _, _, host) = Opener();

        Assert.IsFalse(opener.TryOpenFromHotKey(() => false));
        host.DidNotReceive().OpenMenu();
    }

    [TestMethod]
    public void HotKey_MapBusy_PressOpensNothing()
    {
        var (opener, _, _, host) = Opener();
        host.IsMapClearForMenu.Returns(false);

        Assert.IsFalse(opener.TryOpenFromHotKey(() => true));
        host.DidNotReceive().OpenMenu();
    }
}
