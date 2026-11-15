using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CheatMenu;
using TAOM.Features.CheatMenu.Battle;
using TAOM.Features.CoopInterop;

namespace TAOM.Tests.Features.CheatMenu;

/// <summary>
/// The battle cheat menu's pure rules (BattleCheatPolicy: when a battle is on, which troops a spawn copies), where its Esc-menu
/// item goes (BattleCheatOpener.EscapeItemIndex), and when the list shows and opens (BattleCheatOpener).
/// </summary>
[TestClass]
public class BattleCheatPolicyTests
{
    private const string Photo = "Photo Mode";
    private const string Cheats = "Cheats";

    internal static BattleCheatState On() => new(MissionRunning: true, Ending: false, HasPlayerTeam: true);

    // ---- battle state ----

    [TestMethod]
    public void IsBattleOn_RunningWithPlayerTeam_True() => Assert.IsTrue(BattleCheatPolicy.IsBattleOn(On()));

    [TestMethod]
    public void IsBattleOn_NoMission_False() => Assert.IsFalse(BattleCheatPolicy.IsBattleOn(On() with { MissionRunning = false }));

    [TestMethod]
    public void IsBattleOn_EndedOrRetreated_False() => Assert.IsFalse(BattleCheatPolicy.IsBattleOn(On() with { Ending = true }));

    [TestMethod]
    public void IsBattleOn_NoPlayerTeam_False() => Assert.IsFalse(BattleCheatPolicy.IsBattleOn(On() with { HasPlayerTeam = false }));

    // ---- spawn plan ----

    [TestMethod]
    public void PlanSpawn_SeveralTroops_RoundRobinOverDistinctInFirstSeenOrder()
    {
        var troops = new[] { new BattleTroop("a", false), new BattleTroop("b", false), new BattleTroop("a", false), new BattleTroop("c", false) };

        var plan = BattleCheatPolicy.PlanSpawn(troops, 10);

        CollectionAssert.AreEqual(new[] { ("a", 4), ("b", 3), ("c", 3) }, plan.ToArray());
    }

    [TestMethod]
    public void PlanSpawn_HeroOnTheSide_IsNeverCopied()
    {
        var troops = new[] { new BattleTroop("lord_hero", true), new BattleTroop("a", false) };

        var plan = BattleCheatPolicy.PlanSpawn(troops, 10);

        CollectionAssert.AreEqual(new[] { ("a", 10) }, plan.ToArray());
    }

    [TestMethod]
    public void PlanSpawn_OnlyHeroes_Empty() =>
        Assert.AreEqual(0, BattleCheatPolicy.PlanSpawn(new[] { new BattleTroop("lord_hero", true) }, 10).Count);

    [TestMethod]
    public void PlanSpawn_EmptySide_Empty() =>
        Assert.AreEqual(0, BattleCheatPolicy.PlanSpawn(Array.Empty<BattleTroop>(), 10).Count);

    [TestMethod]
    public void PlanSpawn_FewerTroopsThanKinds_NoZeroEntries()
    {
        var troops = new[] { new BattleTroop("a", false), new BattleTroop("b", false), new BattleTroop("c", false) };

        CollectionAssert.AreEqual(new[] { ("a", 1), ("b", 1) }, BattleCheatPolicy.PlanSpawn(troops, 2).ToArray());
    }

    // ---- the Esc-menu item ----

    private static readonly string[] Vanilla = { "Return to the Game", "Options", Photo, "Exit to Main Menu" };

    [TestMethod]
    public void EscapeItemIndex_Shown_JustBeforePhotoMode() =>
        Assert.AreEqual(2, BattleCheatOpener.EscapeItemIndex(true, Vanilla, Photo, Cheats));

    [TestMethod]
    public void EscapeItemIndex_NotShown_NoItem() =>
        Assert.AreEqual(-1, BattleCheatOpener.EscapeItemIndex(false, Vanilla, Photo, Cheats));

    [TestMethod]
    public void EscapeItemIndex_NoPhotoMode_AtTheEnd() =>
        Assert.AreEqual(2, BattleCheatOpener.EscapeItemIndex(true, new[] { "Return to the Game", "Options" }, Photo, Cheats));

    [TestMethod]
    public void EscapeItemIndex_AlreadyThere_NoSecondItem() =>
        Assert.AreEqual(-1, BattleCheatOpener.EscapeItemIndex(true, new[] { "Options", Cheats, Photo }, Photo, Cheats));

    // ---- opener ----

    private static (BattleCheatOpener Opener, ICheatMenuSettingsProvider Settings, ICoopSessionProvider Coop, IBattleCheatHostAdapter Host,
        IBattleCheatAdapter Battle) Opener()
    {
        var settings = Substitute.For<ICheatMenuSettingsProvider>();
        settings.EnableCheatMenu.Returns(true);
        var coop = Substitute.For<ICoopSessionProvider>();
        coop.IsAuthority.Returns(true);
        var battle = Substitute.For<IBattleCheatAdapter>();
        battle.ReadState().Returns(On());
        var service = new BattleCheatService(battle, coop, Substitute.For<IDedicatedServerProvider>(), Substitute.For<IModLogger>());
        var host = Substitute.For<IBattleCheatHostAdapter>();
        return (new BattleCheatOpener(settings, service, host), settings, coop, host, battle);
    }

    [TestMethod]
    public void Opener_SwitchOff_HiddenNotOpenedAndNoKeyRead()
    {
        var (opener, settings, _, host, _) = Opener();
        settings.EnableCheatMenu.Returns(false);
        var keyRead = false;

        Assert.IsFalse(opener.IsShown);
        Assert.IsFalse(opener.TryOpen());
        Assert.IsFalse(opener.TryOpenOnKey(() => { keyRead = true; return true; }));
        Assert.IsFalse(keyRead, "no key is read while the switch is off");
        host.DidNotReceiveWithAnyArgs().ShowList(default!, default!);
    }

    [TestMethod]
    public void Opener_Coop_HiddenNotOpenedAndNoKeyRead()
    {
        var (opener, _, coop, host, _) = Opener();
        coop.IsSessionActive.Returns(true);
        var keyRead = false;

        Assert.IsFalse(opener.IsShown);
        Assert.IsFalse(opener.TryOpen());
        Assert.IsFalse(opener.TryOpenOnKey(() => { keyRead = true; return true; }));
        Assert.IsFalse(keyRead);
        host.DidNotReceiveWithAnyArgs().ShowList(default!, default!);
    }

    [TestMethod]
    public void Opener_On_ShowsTheListOfEveryAction()
    {
        var (opener, _, _, host, _) = Opener();

        Assert.IsTrue(opener.IsShown);
        Assert.IsTrue(opener.TryOpen());
        host.Received(1).ShowList(Arg.Is<IReadOnlyList<BattleCheatAction>>(a => a.SequenceEqual(new[]
        {
            BattleCheatAction.HealSelf, BattleCheatAction.HealSide, BattleCheatAction.Rout, BattleCheatAction.Kill,
            BattleCheatAction.SpawnAllies, BattleCheatAction.SpawnEnemies,
        })), Arg.Any<Action<BattleCheatAction>>());
    }

    [TestMethod]
    public void Opener_OnKeyNotPressed_NoList()
    {
        var (opener, _, _, host, _) = Opener();

        Assert.IsFalse(opener.TryOpenOnKey(() => false));
        host.DidNotReceiveWithAnyArgs().ShowList(default!, default!);
    }

    [TestMethod]
    public void Opener_OnKeyPressed_ShowsTheList()
    {
        var (opener, _, _, host, _) = Opener();

        Assert.IsTrue(opener.TryOpenOnKey(() => true));
        host.ReceivedWithAnyArgs(1).ShowList(default!, default!);
    }

    [TestMethod]
    public void Opener_APick_RunsTheServiceAndShowsItsOutcome()
    {
        var (opener, _, _, host, battle) = Opener();
        Action<BattleCheatAction>? onPicked = null;
        host.When(h => h.ShowList(Arg.Any<IReadOnlyList<BattleCheatAction>>(), Arg.Any<Action<BattleCheatAction>>()))
            .Do(call => onPicked = call.ArgAt<Action<BattleCheatAction>>(1));
        battle.HealMainAgent().Returns(1);

        opener.TryOpen();
        onPicked!(BattleCheatAction.HealSelf);

        battle.Received(1).HealMainAgent();
        host.Received(1).ShowOutcome(BattleCheatAction.HealSelf, Arg.Is<CheatOutcome>(o => o.Kind == CheatOutcomeKind.Done && o.Number == 1));
    }
}
