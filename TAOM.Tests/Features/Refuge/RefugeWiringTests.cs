using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Features.Refuge;

/// <summary>
/// Wiring regression guard for the Refuge arc, in the HeroRaceWiringTests shape. Every seam here
/// fails SILENTLY when dropped: an unregistered service resolves nothing and the menus just never
/// appear, a dropped patch Initialize leaves a null-guarded no-op patch, a mistyped category
/// string makes Harmony apply nothing and report nothing, and a drifted menu index quietly lands
/// the refuge options on top of (or under) a FieldCamp option instead of in the reserved slot.
/// Each is pinned against the ACTUAL source blocks (the vacuous-batch-test lesson: bound the
/// assertion to the code region that carries the claim, so the test can fail by construction).
/// </summary>
[TestClass]
public class RefugeWiringTests
{
    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\.."));

    private static string ReadSource(params string[] parts)
    {
        var path = Path.Combine(new[] { RepoRoot }.Concat(parts).ToArray());
        Assert.IsTrue(File.Exists(path), $"Expected source file not found: {path}");
        return File.ReadAllText(path);
    }

    // ---- IoC registrations + patch handshakes (RefugeIoC.cs) ----

    [TestMethod]
    public void RefugeIoC_RegistersTheRefugeService()
    {
        var src = ReadSource("Main", "Features", "Refuge", "RefugeIoC.cs");

        StringAssert.Contains(src, "IRefugeService, RefugeService",
            "The refuge service is no longer registered; menus, ticks, SyncData and both patches "
            + "all go dead with no error.");
    }

    [TestMethod]
    public void RefugeIoC_InitializesBothPatch75Patches()
    {
        var src = ReadSource("Main", "Features", "Refuge", "RefugeIoC.cs");

        StringAssert.Contains(src, "RefugeClanScreenPatch.Initialize",
            "RefugeIoC no longer hands the service to the clan-screen patch; the patch null-guards "
            + "a missing service, so refuges silently vanish from clan management.");
        StringAssert.Contains(src, "RefugeEncounterPatch.Initialize",
            "RefugeIoC no longer hands its services to the encounter patch; the patch null-guards "
            + "and stands down, so meeting a refuge opens vanilla's stranger-conversation flow.");
    }

    // ---- SubModule wiring (single-owner file; these pin the two lines Refuge depends on) ----

    [TestMethod]
    public void SubModule_AppliesThePatch75Category()
    {
        var src = ReadSource("Main", "SubModule.cs");

        StringAssert.Contains(src, ".PatchCategory(\"Patch75_Refuge\")",
            "SubModule.cs no longer applies Patch75_Refuge; Harmony is never asked to apply the "
            + "clan-screen and encounter patches, and both features die silently.");
    }

    [TestMethod]
    public void SubModule_AddsTheRefugeBehavior()
    {
        var src = ReadSource("Main", "SubModule.cs");

        StringAssert.Contains(src, "new Features.Refuge.Hooks.RefugeCampaignBehavior(",
            "SubModule.cs no longer adds RefugeCampaignBehavior; menus, SyncData and every tick "
            + "fan-out are gone with no error.");
    }

    // ---- Behavior event registrations that fail SILENTLY when dropped ----

    [TestMethod]
    public void Behavior_ListensForMobilePartyDestroyed()
    {
        var src = ReadSource("Main", "Features", "Refuge", "Hooks", "RefugeCampaignBehavior.cs");

        StringAssert.Contains(src, "CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener",
            "Without this listener a refuge wiped in a lost defense leaves an immortal book row: "
            + "the engine destroys the party AFTER OnMapEventEnded (MapEventSide.HandleMapEventEnd "
            + "calls DestroyPartyAction directly), so the cap slot and visuals leak until reload.");
    }

    [TestMethod]
    public void Behavior_ListensForMakePeace()
    {
        var src = ReadSource("Main", "Features", "Refuge", "Hooks", "RefugeCampaignBehavior.cs");

        StringAssert.Contains(src, "CampaignEvents.MakePeace.AddNonSerializedListener",
            "Vanilla's peace-time prisoner release enumerates caravans, war parties, villages and "
            + "garrisons only; without this listener a hero stored in a refuge stays captive "
            + "through every peace.");
    }

    // ---- Menu insertion indexes (FieldCamp reserves INDEX 4 on both menus for Refuge) ----

    [TestMethod]
    public void MenuController_InsertsFoundOnCampSubMenuAtReservedIndexFour()
    {
        AssertInsertionAtIndexFour(
            menuConstant: "FieldCampCampaignBehavior.CampSubMenuId",
            optionId: "taom_rf_found");
    }

    [TestMethod]
    public void MenuController_InsertsEnterOnBaseMenuAtReservedIndexFour()
    {
        AssertInsertionAtIndexFour(
            menuConstant: "FieldCampCampaignBehavior.BaseMenuId",
            optionId: "taom_rf_enter");
    }

    /// <summary>Bounds the assertion to the actual AddGameMenuOption call: locate the call by its
    /// menu-constant + option-id pair, slice from there to the NEXT AddGameMenuOption call (or end
    /// of file), and require the reserved index INSIDE that slice. A stray "index: 4" belonging to
    /// another option cannot satisfy this (it sits past the slice boundary), and neither can the
    /// option moving to a different menu (the anchor pairs menu and option id).</summary>
    private static void AssertInsertionAtIndexFour(string menuConstant, string optionId)
    {
        var src = ReadSource("Main", "Features", "Refuge", "Hooks", "RefugeMenuController.cs");

        var callAnchor = menuConstant + ", \"" + optionId + "\"";
        int start = src.IndexOf(callAnchor, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0,
            $"RefugeMenuController no longer inserts '{optionId}' on {menuConstant}; the reserved "
            + "slot goes unused and the option is gone (or moved to the wrong menu).");

        int end = src.IndexOf("AddGameMenuOption", start + callAnchor.Length, StringComparison.Ordinal);
        if (end < 0)
            end = src.Length;

        var call = src.Substring(start, end - start);
        StringAssert.Contains(call, "index: 4",
            $"The '{optionId}' insertion drifted off the reserved index 4; FieldCamp deliberately "
            + "leaves index 4 unassigned on both menus and any other index collides with its options.");
    }

    // ---- Founding flow: leave the camp wait menu BEFORE the garrison deposit screen ----

    [TestMethod]
    public void MenuController_FoundingExitsTheCampMenuBeforeTheDepositScreen()
    {
        // Found breaks the camp. The option that starts it lives on FieldCamp's WAIT sub-menu and
        // is not isLeave, so without an explicit exit the deposit screen returns the player to a
        // menu whose camp is gone. A wait menu's condition delegate gates every option's
        // visibility (GameMenu.GetMenuOptionConditionsHold, 1.4.8), so that panel had no options
        // and no exit, and MapState persisted it into the save (player reports, v2.0.24 to
        // v2.0.28). Establish and Break camp land on the map for the same reason.
        var src = ReadSource("Main", "Features", "Refuge", "Hooks", "RefugeMenuController.cs");

        int start = src.IndexOf("private void OnWardenChosen(", StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "RefugeMenuController lost OnWardenChosen");
        int end = src.IndexOf("private ", start + 1, StringComparison.Ordinal);
        if (end < 0)
            end = src.Length;
        var body = src.Substring(start, end - start);

        int deposit = body.IndexOf("PartyScreenHelper.OpenScreenAsManageTroopsAndPrisoners", StringComparison.Ordinal);
        Assert.IsTrue(deposit >= 0,
            "OnWardenChosen no longer opens the garrison deposit screen after founding");
        int exit = body.IndexOf("_menus.ExitToLast()", StringComparison.Ordinal);
        Assert.IsTrue(exit >= 0 && exit < deposit,
            "OnWardenChosen must exit the camp menu BEFORE opening the deposit screen; otherwise the "
            + "player returns to a wait menu whose camp is gone and cannot leave it");
    }
}
