using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.StoryMissions;

/// <summary>Source pins for the seams the story host leans on: the campaign morale model and the client view list.</summary>
[TestClass]
public class StoryMissionWiringPinTests
{
    private static string Source(string path) => Regex.Replace(RepoPaths.ReadSource(path, stripComments: true), @"\s+", " ");

    [TestMethod]
    public void TheCampaignMoraleModel_HoldsTheArmedMissionsAttackers_LikeItsCustomBattleTwin()
    {
        // SandboxBattleMoraleModel.CanPanicDueToMorale answers true for any soldier without the Loyalty and Honor perk (SandboxBattleMoraleModel.cs:64-77), so a campaign-hosted wave would rout
        // and a running attacker is not counted as standing (LongDefeatBattlefieldAdapter.Diagnostics.cs:21): a wave could end with no kill. The hold is inert outside the mission the Long Defeat logic armed it for.
        var campaign = Source("Main/Features/CultureDoctrine/Models/TaomBattleMoraleModel.cs");
        var custom = Source("Main/Features/CultureDoctrine/Models/TaomCustomBattleMoraleModel.cs");

        StringAssert.Contains(campaign, "!LongDefeatMorale.HoldsAttacker(agent)");
        StringAssert.Contains(custom, "!LongDefeatMorale.HoldsAttacker(agent)");
        StringAssert.Contains(campaign, "CanPanicDueToMorale(Agent agent) => !SeatPresence.RefusesPanic(agent.IsMainAgent) && ");
    }

    [TestMethod]
    public void TheStoryViewList_IsKeyedByTheStoryMissionName_AndKeepsToViewMethodStatics()
    {
        var views = Source("Client/Features/StoryMissions/StoryMissionViews.cs");

        StringAssert.Contains(views, "[ViewCreatorModule]");
        StringAssert.Contains(views, "[ViewMethod(StoryMission.MissionName)]");
        Assert.AreEqual(1, Regex.Matches(views, @"\bstatic\b[^;{=]*\(").Count, "one static method: every static on a ViewCreatorModule class must carry ViewMethod (ViewCreatorManager.cs:63-66)");
    }

    [TestMethod]
    public void TheStoryViewList_IsACampaignListWithTheSiegeEngineMarkers_AndNoLongDefeatHud()
    {
        var views = Source("Client/Features/StoryMissions/StoryMissionViews.cs");

        StringAssert.Contains(views, "CampaignOptions.IsIronmanMode");
        StringAssert.Contains(views, "CreateMissionSiegeEngineMarkerView(mission)");
        StringAssert.Contains(views, "CreateMissionOrderUIHandler()");
        Assert.IsFalse(views.Contains("LongDefeatHudView"), "its HUD reads the Long Defeat session and would show a shop and a leave-to-main-menu button");
        Assert.IsFalse(views.Contains("MissionPreloadView"), "it reads MapEvent.PlayerMapEvent unguarded");
    }

    [TestMethod]
    public void TheStoryLogic_EndsTheMissionOnTheWatchsAnswer_AndTellsTheWatchTheSessionsValuesOnBothExits()
    {
        // Without Mission.EndMission a won or lost mission never ends: the launcher's list has no BattleEndLogic and the story view list has no leave button.
        var logic = Source("Main/Features/StoryMissions/Hooks/StoryMissionLogic.cs");

        StringAssert.Contains(logic, "if (_watch.Step(dt, _session.Result, _session.Fault != null)) Mission.EndMission();");
        StringAssert.Contains(logic, "OnEndMission() { base.OnEndMission(); _watch.Left(_session.Result, _session.Fault != null); ReportVerdict(); }");
        StringAssert.Contains(logic, "OnRemoveBehavior() { base.OnRemoveBehavior(); _watch.Left(_session.Result, _session.Fault != null); ReportVerdict(); }");
    }

    [TestMethod]
    public void TheLauncher_GivesTheHostedRunADecoratedLog_AndTheStoryLogicTheRawOne()
    {
        // The director writes taom.long-defeat.* lines that tools/ingame_checks/1079.toml reads; a story run's lines would read as that issue's evidence.
        var launcher = Source("Main/Features/StoryMissions/Hooks/StoryMissionLauncher.cs");

        StringAssert.Contains(launcher, "new LongDefeatMissionLogic(plan, session, _logger, new HostedRunOutcomeLog(_outcomes), mission.EndMission, key)");
        StringAssert.Contains(launcher, "new StoryMissionLogic(session, side, _logger, _outcomes, key, music, onVerdict)");
    }

    [TestMethod]
    public void TheMusicView_JoinsOnlyAMissionThatNamesATrack_AndIsBuiltPerMission()
    {
        // A silenced slot left set outranks the campaign handler (MBMusicManager.CheckActiveHandler), so the adapter that holds it is never a container singleton.
        var views = Source("Client/Features/StoryMissions/StoryMissionViews.cs");
        var view = Source("Client/Features/StoryMissions/StoryMusicMissionView.cs");
        var module = Source("Main/Features/StoryMissions/StoryMissionsModule.cs");

        StringAssert.Contains(views, "var music = StoryMusicMissionView.ForMission(mission); if (music != null) views.Add(music);");
        StringAssert.Contains(view, "if (logic?.Music == null) return null;");
        StringAssert.Contains(view, "new StoryMusicCue(new MissionMusicAdapter(),");
        Assert.IsFalse(module.Contains("MissionMusicAdapter"), "registered in the container, one adapter would hold every mission's slot and event");
    }

    [TestMethod]
    public void TheMusicView_ReleasesFromBothTeardownHooks()
    {
        // OnMissionScreenFinalize comes from IMissionListener.OnEndMission (Mission.EndMission). A teardown without it (shutdown, or a mod's mid-mission load)
        // reaches only OnRemoveBehavior, the path vanilla's silenced view misses.
        var view = Source("Client/Features/StoryMissions/StoryMusicMissionView.cs");

        StringAssert.Contains(view, "public override void OnMissionScreenFinalize() { _cue.Release(\"screen-finalize\");");
        StringAssert.Contains(view, "public override void OnRemoveBehavior() { _cue.Release(\"remove-behavior\");");
    }

    [TestMethod]
    public void TheMusicAdapter_PlaysOnTheEnginesMusicEvent_AndItsSilencedHandlerDoesNothing()
    {
        // event:/music is the event psai's own channels use (native), so it most likely follows the Music fader (UNVERIFIED); the module_sounds categories have no music.
        var adapter = Source("Main/Adapters/MissionMusicAdapter.cs");

        StringAssert.Contains(adapter, "SoundEvent.CreateEventFromExternalFile(MusicEvent, fullPath, null, is3d: false, isBlocking: false)");
        StringAssert.Contains(adapter, "private const string MusicEvent = \"event:/music\";");
        StringAssert.Contains(adapter, "void IMusicHandler.OnUpdated(float dt) { }");
    }

    [TestMethod]
    public void SiegeDismount_SitsOutAStoryMission_BecauseAStoryMissionWritesNothingToTheCampaign()
    {
        // SiegeDismountMissionBehavior runs for every mission (SubModule.AddTaomMissionBehaviors) and in a campaign writes the main hero's equipment and the party's mount roster.
        var dismount = Source("Main/Features/SiegeDismount/Hooks/SiegeDismountMissionBehavior.cs");

        StringAssert.Contains(dismount, "var isSiegeBattle = (mission?.IsSiegeBattle ?? false) && mission?.GetMissionBehavior<StoryMissionLogic>() == null;");
    }

    [TestMethod]
    public void TheStoryOffer_SnoozesOnEscape_DeclinesOnlyByChoice_AndAppliesTheVerdictBeforeASave()
    {
        // #1105 slice 4 review: Escape runs a popup's negative action and a menu hotkey runs its first leave option, so neither may be the
        // saved decline; the behavior saves nothing, so the verdict is applied in the init the engine runs when the mission returns.
        var offer = Source("Main/Features/StoryMissions/Hooks/StoryOfferBehavior.cs");

        StringAssert.Contains(offer, "() => { if (MapPopupGate.IsMapFreeUnderPopup()) _menus.EnsureMenuOpen(MenuId); }, Later), true, false);");
        StringAssert.Contains(offer, "OfferCondition, _ => { Decline(); _menus.ExitToLast(); }, isLeave: false, index: 1);");
        StringAssert.Contains(offer, "private void OnInit(MenuCallbackArgs args) { if (_offer != null && _verdict is { } verdict && _result == null) _result = _service.Apply(_offer, verdict);");
        StringAssert.Contains(offer, "private void Continue(MenuCallbackArgs args) { Clear(); _menus.ExitToLast(); }");
    }
}
