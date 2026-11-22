using System.Linq;

namespace TAOM.Features.Autorun.Scenario;

/// <summary>
/// The story-offer kind's phases (#1105 slices 3 to 5): the War Chronicle window the armed console command opened shows the offer popup, the
/// driver presses its accept button, picks the wanted option of the menu <c>taom_story_offer</c>, plays the mission with the bot, clicks
/// "Continue" and runs the arm file's <c>after=</c> commands. Every refusal ends the run with its own reason and one log line.
/// </summary>
internal sealed partial class ScenarioAutorunDriver
{
    private bool _continueClicked;

    /// <summary>
    /// The open command's answer starts with "[WarChronicle] Opened:" or says why not (a cheat mode off, an event that is not Pending). A
    /// by-date plan arms no open command: the window opens on its own day.
    /// </summary>
    private bool OfferWindowOpened()
    {
        if (_plan.OpenByDate || _commandOutputs.Any(output => ScenarioRules.OpenedTheWindow(output, _plan.EventId)))
            return true;

        Refused("offer_refused", "the open command did not open the window: " + OneLine(string.Join(" | ", _commandOutputs)));
        return false;
    }

    /// <summary>
    /// The popup comes from StoryOfferBehavior.OnTick, which runs only on a campaign tick with time moving (Campaign.Tick), so the clock runs
    /// here. Inquiries are hidden only while the behavior holds no offer for the armed event: a hidden popup is a snooze until tomorrow. The
    /// accept opens the menu in the same call (GameMenu.ActivateGameMenu, then MapState.EnterMenuMode creates the context), so a busy map,
    /// where the popup's button opens none, ends the run at once.
    /// </summary>
    private void OfferWait()
    {
        HideSceneNotification();
        CloseEventWindow();
        if (_host.KeepFastForward(_plan.Speed))
            _resets++;

        var limit = ScenarioRules.OfferPopupLimit(_plan.OpenByDate, _plan.WallLimitSeconds, _plan.MissionLimitSeconds);
        if (!ScenarioRules.Waiting(_wall - _phaseStart, limit))
        {
            Refused("offer_not_shown", "the offer popup never showed in " + ScenarioPlan.Num(limit) + " s pending=" + (_host.PendingOfferEvent ?? "none")
                + " day=" + ScenarioRules.WholeDay(_host.ElapsedDays) + " command output: " + OneLine(string.Join(" | ", _commandOutputs)));
            return;
        }

        if (_host.PendingOfferEvent != _plan.EventId)
        {
            HideInquiry();
            _held = 0.0;
            return;
        }

        if (!Held(_host.InquiryActive) || !ScenarioRules.PopupSettled(_held))
            return;

        if (!_host.AcceptOfferPopup(out var title))
        {
            Refused("offer_refused", "the popup's accept button could not be pressed title='" + title + "'");
            return;
        }

        _logger.LogInfo(Prefix + "accepted the offer popup '" + title + "'");
        if (_host.OfferMenuShown)
            Enter(Phase.OfferMenu);
        else
            Refused("offer_refused", "the accept found the map busy: no menu opened");
    }

    private void OfferMenu()
    {
        MapChores();
        if (!ScenarioRules.Waiting(_wall - _phaseStart, ScenarioRules.StoryStepWaitSeconds))
        {
            Refused("offer_refused", "the offer menu never settled in " + ScenarioRules.StoryStepWaitSeconds + " s");
            return;
        }

        if (!Held(_host.Screen == AutorunScreen.MapMenu && _host.OfferMenuShown) || !ScenarioRules.MapSettled(_held) || _host.EventWindowOpenOrWaiting)
            return;

        var fate = _plan.Choice == ScenarioPlan.ChoiceFate;
        var chosen = _host.ChooseOfferOption(ScenarioRules.OfferOptionIds(_plan.Choice), !fate);
        if (chosen == null)
        {
            Refused("offer_refused", "no '" + _plan.Choice + "' option is offered, choices=" + _host.PendingOfferChoices);
            return;
        }

        _logger.LogInfo(Prefix + "chose " + chosen);
        Enter(fate ? Phase.OfferReturn : Phase.InMission);
    }

    /// <summary>
    /// After a mission the engine returns to the offer menu, whose init applies the verdict; "Continue" then leaves it. "Let fate decide"
    /// leaves the menu in the click itself, so no Continue follows it.
    /// </summary>
    private void OfferReturn()
    {
        MapChores();
        if (!ScenarioRules.Waiting(_wall - _phaseStart, ScenarioRules.StoryStepWaitSeconds))
        {
            Refused("return_failed", "the map did not settle in " + ScenarioRules.StoryStepWaitSeconds + " s screen=" + _host.Screen);
            return;
        }

        var screen = _host.Screen;
        _freeMapHeld = screen == AutorunScreen.Map ? _freeMapHeld + _dt : 0.0;
        var waitingForContinue = !_continueClicked && _plan.Choice != ScenarioPlan.ChoiceFate && screen == AutorunScreen.MapMenu && _host.OfferMenuShown;
        if (Held(waitingForContinue))
        {
            if (!ScenarioRules.MapSettled(_held))
                return;

            if (_host.ChooseOfferOption(new[] { ScenarioRules.ContinueOptionId }, false) != null)
            {
                _continueClicked = true;
                _logger.LogInfo(Prefix + "chose " + ScenarioRules.ContinueOptionId);
            }
            else
                Refused("return_failed", "the Continue option was not offered");

            return;
        }

        if (ScenarioRules.MapSettled(_freeMapHeld) && !_host.EventWindowOpenOrWaiting)
        {
            _logger.LogInfo(Prefix + "back on the free map after the offer");
            RunCommands(_plan.AfterCommands);
            Finish(ScenarioAutorunDone.ReasonOk);
        }
    }

    private void Refused(string reason, string why)
    {
        _logger.LogWarning(Prefix + reason + ": " + why + " open_menu=" + (_host.OpenMenuId ?? "none"));
        Finish(reason);
    }

    private string DoneDetail(ScenarioRunFacts facts) => _plan.Kind switch
    {
        ScenarioPlan.KindStoryMission => " mission=" + _plan.MissionId + " verdict=" + facts.Verdict,
        ScenarioPlan.KindStoryOffer => " event=" + _plan.EventId + " choice=" + _plan.Choice + " verdict=" + facts.Verdict,
        _ => " days_run=" + ScenarioRules.DaysRun(_dayStart, _lastDay) + " days_wanted=" + _plan.Days,
    };

    private static string OneLine(string text) => text.Replace("\r", " ").Replace("\n", " ");
}
