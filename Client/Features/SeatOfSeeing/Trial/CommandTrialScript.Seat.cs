using System;
using System.Collections.Generic;
using System.Globalization;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Client.Features.SeatOfSeeing.Trial;

internal sealed partial class CommandTrialScript
{
    private const float PressFromSeconds = 1f;
    private const float PressTakenSeconds = 2f;
    private const float BattleSettleSeconds = 1f;
    private const float EnterShotSeconds = 2f;
    private const float FlyShotSeconds = 1.5f;
    private const float ReenterSettleSeconds = 1f;
    private const float AfterLeaveSampleSeconds = 0.1f;
    private const float PanelSettleSeconds = 0.3f;
    private const float HideSettleSeconds = 0.4f;
    private const float AimSeconds = 6f;
    private const int AimFramesOnTarget = 3;
    private const float FocusSettleSeconds = 0.5f;
    private const float CursorFractionX = 0.30f;
    private const float CursorFractionY = 0.40f;
    private const float CompanyShotSeconds = 1.5f;
    private const string RefuseStep = "refuse_in_deployment";

    private static readonly SeatInput NoInput = default;
    private static readonly SeatInput FlyForward = new(Right: 0f, Forward: 1f, Lift: 0f, ZoomNotches: 0f, LookYaw: 0f, LookPitch: 0f, Fast: false);
    private static readonly SeatInput RiseUp = new(Right: 0f, Forward: 0f, Lift: 1f, ZoomNotches: 0f, LookYaw: 0f, LookPitch: 0f, Fast: false);

    private float _heightBeforeLeave = float.NaN;

    private bool Seated => _host.State == SeatState.Seated;

    private float HeightAboveGround(Vec3f at) => at.Z - _host.GroundHeight(at.X, at.Y);

    private Verdict NotSeated() => Verdict.Failed("reason=not-seated state=" + _host.State);

    // The two conditions every S2 and S3 pass reads at its end, so a FAIL line never shows only good facts.
    private string Standing() => " seated=" + Bit(Seated) + " faulted=" + Bit(_host.Faulted);

    private static void SeatSteps(Dictionary<string, Func<CommandTrialScript, IEnumerable<Verdict>>> into)
    {
        into[RefuseStep] = s => s.Refuse();
        into["enter"] = s => s.Enter();
        into["fly"] = s => s.Fly();
        into["hero_stands"] = s => s.HeroStands();
        into["leave_glide"] = s => s.LeaveGlide();
        into["reenter_keeps_height"] = s => s.Reenter();
        into["cursor_with_panel"] = s => s.CursorWithPanel();
        into["focus_follows_cursor"] = s => s.FocusFollowsCursor();
        into["keep_panel_reopens"] = s => s.KeepPanelReopens();
        into["hide_bars_restored"] = s => s.HideBarsRestored();
        into["keep_to_soldier"] = s => s.KeepToSoldier();
        into["keep_to_company"] = s => s.KeepToCompany();
        into["let_go_on_death"] = s => s.LetGoOnDeath();
        into["fight_on"] = s => s.FightOn();
        into["morale_held"] = s => s.MoraleHeld();
        into["back_to_stand"] = s => s.BackToStand();
        into["hero_falls"] = s => s.HeroFalls();
        into["take_soldier"] = s => s.TakeSoldier();
    }

    private IEnumerable<Verdict> Refuse()
    {
        while (!_host.InDeployment || _now < PressFromSeconds)
            yield return !_host.InDeployment ? Verdict.Failed("reason=not-in-deployment") : Verdict.Waiting("press-time");

        _host.PressSeatKey();
        var pressedAt = _now;
        while (_host.PressPending)
            yield return _now - pressedAt > PressTakenSeconds ? Verdict.Failed("reason=press-not-taken") : Verdict.Waiting("press-taken");

        var inDeployment = _host.InDeployment;
        var facts = "taken=1 mode=" + (inDeployment ? "Deployment" : "other") + " state=" + _host.State;
        yield return inDeployment && _host.State == SeatState.Off && !_host.Faulted ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    private IEnumerable<Verdict> Enter()
    {
        var battleSince = float.NaN;
        while (true)
        {
            if (!_host.InBattle || !_host.HeroAlive)
                battleSince = float.NaN;
            else if (float.IsNaN(battleSince))
                battleSince = _now;

            if (!float.IsNaN(battleSince) && _now - battleSince >= BattleSettleSeconds)
                break;
            yield return Verdict.Waiting("battle");
        }

        _host.SetInput(NoInput);
        _host.PressSeatKey();
        var pressedAt = _now;
        var sawRising = false;
        var shot = false;
        while (true)
        {
            sawRising |= _host.State == SeatState.Rising;
            var since = _now - pressedAt;
            if (!shot && since >= EnterShotSeconds)
            {
                shot = true;
                _host.Screenshot("hud_enter.bmp");
            }

            if (since >= SeatTrialRules.EnterWindowSeconds)
                break;
            yield return Verdict.Waiting("window");
        }

        var seated = Seated;
        var frames = _host.Frames;
        var seconds = _now - pressedAt;
        var controller = _host.HeroIsPlayerControlled;
        var facts = "rising=" + Bit(sawRising) + " seated=" + Bit(seated) + " frames=" + frames.ToString(CultureInfo.InvariantCulture)
            + " seconds=" + Num(seconds) + " controller=" + (controller ? "Player" : "other");
        var pass = sawRising && seated && SeatTrialRules.OwnedEnoughFrames(frames, seconds) && controller && !_host.Faulted;
        yield return pass ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    private IEnumerable<Verdict> Fly()
    {
        var at = _host.ViewPosition();
        if (!Seated || at == null)
            yield return NotSeated();

        var startAt = at!.Value;
        var forward = _host.ViewForward();
        var height = HeightAboveGround(startAt);
        _host.SetInput(FlyForward);
        var startedAt = _now;
        var shot = false;
        while (_now - startedAt < SeatTrialRules.FlySeconds)
        {
            if (!shot && _now - startedAt >= FlyShotSeconds)
            {
                shot = true;
                _host.Screenshot("hud_fly.bmp");
            }

            yield return Verdict.Waiting("flight");
        }

        _host.SetInput(NoInput);
        var end = _host.ViewPosition();
        var seconds = _now - startedAt;
        var moved = end == null ? float.NaN : startAt.Distance2D(end.Value);
        var expected = SeatCameraMath.PanSpeed(height) * seconds;
        var dot = end == null || forward == null ? float.NaN
            : SeatTrialRules.ForwardDot(end.Value.X - startAt.X, end.Value.Y - startAt.Y, forward.Value.X, forward.Value.Y);
        var facts = "moved=" + Num(moved) + " expected=" + Num(expected) + " height=" + Num(height) + " seconds=" + Num(seconds) + " dot=" + Num(dot);
        yield return Seated && SeatTrialRules.FlewExpected(expected, moved) && SeatTrialRules.FlewForward(dot) ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    private IEnumerable<Verdict> HeroStands()
    {
        if (!Seated)
            yield return NotSeated();
        if (_heroAt == null)
            yield return Verdict.Failed("reason=no-hero");

        var heroStart = _heroAt!.Value;
        var fromTime = _heroTime;
        var startedAt = _now;
        while (true)
        {
            yield return Verdict.Waiting("hero-window");
            if (_heroAt == null)
                yield return Verdict.Failed("reason=no-hero");
            else if (_now - startedAt >= SeatTrialRules.HeroWindowSeconds)
                break;
        }

        // The window is real time and the distance is divided by the mission seconds that passed in it, the measured fact.
        var span = _heroTime - fromTime;
        var speed = heroStart.Distance2D(_heroAt!.Value) / span;
        var facts = "speed=" + Num(speed) + " mission_seconds=" + Num(span) + " seconds=" + Num(_now - startedAt);
        yield return Seated && SeatTrialRules.HeroStood(speed) ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    private IEnumerable<Verdict> LeaveGlide()
    {
        if (!Seated)
            yield return NotSeated();

        _host.SetInput(RiseUp);
        var risingSince = _now;
        while (_now - risingSince < SeatTrialRules.LiftSeconds)
            yield return Verdict.Waiting("lift");
        _host.SetInput(NoInput);

        var view = _host.ViewPosition();
        if (view == null || _heroAt == null)
            yield return Verdict.Failed("reason=no-position");

        var beforeDistance = view!.Value.Distance(_heroAt!.Value);
        _heightBeforeLeave = HeightAboveGround(view.Value);
        _host.PressSeatKey();
        var pressedAt = _now;
        while (_host.PressPending)
            yield return _now - pressedAt > PressTakenSeconds ? Verdict.Failed("reason=press-not-taken") : Verdict.Waiting("press-taken");

        var leftAt = _now;
        var afterDistance = float.NaN;
        float distance;
        while (true)
        {
            if (_host.State != SeatState.Off)
                yield return Verdict.Failed("reason=still-seated state=" + _host.State);

            var at = _host.ViewPosition();
            distance = at == null || _heroAt == null ? float.NaN : at.Value.Distance(_heroAt.Value);
            if (float.IsNaN(afterDistance) && _now - leftAt >= AfterLeaveSampleSeconds)
                afterDistance = distance;
            if (SeatTrialRules.NearHero(distance) || _now - pressedAt > SeatTrialRules.GlideWindowSeconds)
                break;
            yield return Verdict.Waiting("glide");
        }

        var glide = _host.GlideBegan;
        var after = float.IsNaN(afterDistance) ? distance : afterDistance;
        var facts = "glide=" + Bit(glide) + " before=" + Num(beforeDistance) + " after=" + Num(after) + " near=" + Num(distance)
            + " seconds=" + Num(_now - pressedAt);
        if (!SeatTrialRules.NearHero(distance))
            yield return Verdict.Failed("reason=never-near " + facts);

        yield return glide && !_host.Faulted ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    private IEnumerable<Verdict> Reenter()
    {
        if (_host.State != SeatState.Off)
            yield return Verdict.Failed("reason=not-off state=" + _host.State);

        _host.SetInput(NoInput);
        _host.PressSeatKey();
        var pressedAt = _now;
        while (_host.PressPending)
            yield return _now - pressedAt > PressTakenSeconds ? Verdict.Failed("reason=press-not-taken") : Verdict.Waiting("press-taken");

        while (_host.State != SeatState.Seated)
            yield return _host.State == SeatState.Off ? Verdict.Failed("reason=not-entered state=Off") : Verdict.Waiting("seated");

        var seatedAt = _now;
        while (_now - seatedAt < ReenterSettleSeconds)
            yield return Verdict.Waiting("settle");

        var view = _host.ViewPosition();
        var after = view == null ? float.NaN : HeightAboveGround(view.Value);
        var facts = "before=" + Num(_heightBeforeLeave) + " after=" + Num(after) + " entry=" + Num(SeatCameraMath.EntryHeight);
        yield return SeatTrialRules.HeightKept(_heightBeforeLeave, after, SeatCameraMath.EntryHeight) && !_host.Faulted ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    private IEnumerable<Verdict> CursorWithPanel()
    {
        if (!Seated)
            yield return NotSeated();

        var closedBefore = _host.MouseVisible;
        yield return Verdict.InMissionTick;
        var opened = _host.OpenPanel();
        var openedAt = _now;
        while (opened && !_host.MouseVisible && _now - openedAt < SeatTrialRules.ShortWaitSeconds)
            yield return Verdict.Waiting("cursor-shown");

        var panelOpen = _host.PanelOpen;
        var cursorOpen = _host.MouseVisible;
        yield return Verdict.InMissionTick;
        _host.ClosePanel(asPlayer: true);
        var closedAt = _now;
        while (_host.MouseVisible && _now - closedAt < SeatTrialRules.ShortWaitSeconds)
            yield return Verdict.Waiting("cursor-hidden");

        var panelClosed = !_host.PanelOpen;
        var cursorClosed = !_host.MouseVisible;
        var facts = "opened=" + Bit(opened) + " before=" + Bit(closedBefore) + " panel_open=" + Bit(panelOpen) + " cursor_open=" + Bit(cursorOpen)
            + " panel_closed=" + Bit(panelClosed) + " cursor_closed=" + Bit(cursorClosed) + Standing();
        var pass = opened && !closedBefore && panelOpen && cursorOpen && panelClosed && cursorClosed && Seated && !_host.Faulted;
        yield return pass ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    private IEnumerable<Verdict> FocusFollowsCursor()
    {
        if (!Seated)
            yield return NotSeated();
        if (!_host.WindowFocused)
            yield return Verdict.Failed("reason=no-window-focus");

        yield return Verdict.InMissionTick;
        var opened = _host.OpenPanel();
        var openedAt = _now;
        while (opened && !_host.MouseVisible && _now - openedAt < SeatTrialRules.ShortWaitSeconds)
            yield return Verdict.Waiting("cursor-shown");
        if (!_host.MouseVisible)
            yield return Verdict.Failed("reason=no-cursor opened=" + Bit(opened));

        _host.MoveCursorTo(CursorFractionX, CursorFractionY);
        var movedAt = _now;
        while (_now - movedAt < FocusSettleSeconds)
            yield return Verdict.Waiting("handler-tick");

        var reading = _host.ProbeFocus();
        var focused = _host.WindowFocused;
        yield return Verdict.InMissionTick;
        _host.ClosePanel(asPlayer: true);

        var facts = "pixel=" + Num(reading.PixelX) + "," + Num(reading.PixelY) + " wanted=" + Num(reading.WantedX) + "," + Num(reading.WantedY) + " expected=" + Num(reading.ExpectedX) + "," + Num(reading.ExpectedY)
            + " field=" + Num(reading.FieldX) + "," + Num(reading.FieldY) + " focused=" + reading.Focused.Replace(' ', '_') + " window=" + Bit(focused) + Standing();
        var placed = SeatTrialRules.CursorPlaced(reading.WantedX, reading.WantedY, reading.PixelX, reading.PixelY);
        yield return focused && placed && SeatTrialRules.FocusFollowed(reading) && Seated && !_host.Faulted ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    private IEnumerable<Verdict> KeepPanelReopens()
    {
        if (!Seated)
            yield return NotSeated();

        yield return Verdict.InMissionTick;
        var opened = _host.OpenPanel();
        var openedAt = _now;
        while (_now - openedAt < PanelSettleSeconds)
            yield return Verdict.Waiting("panel-seen");

        yield return Verdict.InMissionTick;
        _host.ClosePanel(asPlayer: false);
        var closed = !_host.PanelOpen;
        var frames = 0;
        while (closed && !_host.PanelOpen && frames <= SeatTrialRules.ReopenFrameLimit)
        {
            yield return Verdict.Waiting("reopen");
            frames++;
        }

        var reopened = _host.PanelOpen;
        if (!closed || !reopened || !SeatTrialRules.ReopenedPromptly(frames))
            yield return Verdict.Failed("opened=" + Bit(opened) + " closed=" + Bit(closed) + " reopened=" + Bit(reopened) + " frames="
                + frames.ToString(CultureInfo.InvariantCulture) + " limit=" + SeatTrialRules.ReopenFrameLimit.ToString(CultureInfo.InvariantCulture) + Standing());

        var settledAt = _now;
        while (_now - settledAt < PanelSettleSeconds)
            yield return Verdict.Waiting("panel-seen-again");

        yield return Verdict.InMissionTick;
        _host.ClosePanel(asPlayer: true);
        var playerClosedAt = _now;
        var reopenedAfterPlayer = false;
        while (_now - playerClosedAt < SeatTrialRules.StayClosedSeconds)
        {
            reopenedAfterPlayer |= _host.PanelOpen;
            yield return Verdict.Waiting("stay-closed");
        }

        var facts = "opened=" + Bit(opened) + " closed=" + Bit(closed) + " frames=" + frames.ToString(CultureInfo.InvariantCulture)
            + " stays_closed=" + Bit(!reopenedAfterPlayer) + Standing();
        var pass = opened && !reopenedAfterPlayer && !_host.PanelOpen && Seated && !_host.Faulted;
        yield return pass ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    private IEnumerable<Verdict> HideBarsRestored()
    {
        if (!Seated)
            yield return NotSeated();

        var before = _host.UiHidden;
        _host.PressHide();
        var pressedAt = _now;
        while (_now - pressedAt < HideSettleSeconds)
            yield return Verdict.Waiting("hidden");

        var hidden = _host.UiHidden;
        _host.Screenshot("hud_hide_bars_restored_hidden.bmp");
        var shotAt = _now;
        while (_now - shotAt < PanelSettleSeconds)
            yield return Verdict.Waiting("picture");

        yield return Verdict.InMissionTick;
        var opened = _host.OpenPanel();
        var openedAt = _now;
        while (_host.UiHidden && _now - openedAt < SeatTrialRules.ShortWaitSeconds)
            yield return Verdict.Waiting("restored");

        var restored = !_host.UiHidden;
        var backAt = _now;
        while (_now - backAt < HideSettleSeconds)
            yield return Verdict.Waiting("settled");

        _host.Screenshot("hud_hide_bars_restored_back.bmp");
        var lastShotAt = _now;
        while (_now - lastShotAt < PanelSettleSeconds)
            yield return Verdict.Waiting("picture");

        yield return Verdict.InMissionTick;
        _host.ClosePanel(asPlayer: true);

        var facts = "before=" + Bit(before) + " hidden=" + Bit(hidden) + " opened=" + Bit(opened) + " restored_on_panel=" + Bit(restored) + Standing();
        var pass = !before && hidden && opened && restored && Seated && !_host.Faulted;
        yield return pass ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    private IEnumerable<Verdict> Aim()
    {
        var startedAt = _now;
        var onTarget = 0;
        while (onTarget < AimFramesOnTarget)
        {
            var step = _host.AimAtChosen();
            if (step.Gone)
                yield return Verdict.Failed("reason=soldier-gone");
            _host.SetInput(step.Input);
            onTarget = step.OnTarget ? onTarget + 1 : 0;
            if (_now - startedAt > AimSeconds)
                yield return Verdict.Failed("reason=never-aimed");
            if (onTarget < AimFramesOnTarget)
                yield return Verdict.Waiting("aim");
        }

        _host.SetInput(NoInput);
    }

    private IEnumerable<Verdict> KeepToSoldier()
    {
        if (!Seated)
            yield return NotSeated();
        if (_host.PanelOpen)
            yield return Verdict.Failed("reason=panel-open");

        yield return Verdict.InMissionTick;
        if (!_host.ChooseSoldier())
            yield return Verdict.Failed("reason=no-soldier");

        foreach (var verdict in Aim())
            yield return verdict;

        _host.PressKeep();
        var pressedAt = _now;
        while (_host.Keeping == SeatKeepKind.None && _now - pressedAt < SeatTrialRules.ShortWaitSeconds)
            yield return Verdict.Waiting("keep-taken");
        if (_host.Keeping == SeatKeepKind.None)
            yield return Verdict.Failed("reason=not-kept said=" + _host.Said);

        var kind = _host.Keeping;
        var same = _host.KeepsChosen;
        var keptAt = _now;
        while (_now - keptAt < SeatTrialRules.OrbitSettleSeconds)
            yield return Verdict.Waiting("orbit");

        var distance = _host.DistanceToKept();
        var stillKept = _host.Keeping == SeatKeepKind.Soldier;
        _host.PressKeep();
        var letGoAt = _now;
        while (_host.Keeping != SeatKeepKind.None && _now - letGoAt < SeatTrialRules.ShortWaitSeconds)
            yield return Verdict.Waiting("let-go");

        var letGo = _host.Keeping == SeatKeepKind.None;
        var facts = "kind=" + kind + " chosen=" + Bit(same) + " still_kept=" + Bit(stillKept) + " distance=" + Num(distance) + " orbit=" + Num(SeatOrbit.StartDistance)
            + " letgo=" + Bit(letGo) + Standing();
        var pass = kind == SeatKeepKind.Soldier && same && stillKept && SeatTrialRules.KeptNearTarget(distance, SeatOrbit.StartDistance) && letGo && Seated && !_host.Faulted;
        yield return pass ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    private IEnumerable<Verdict> KeepToCompany()
    {
        if (!Seated)
            yield return NotSeated();
        if (_host.PanelOpen)
            yield return Verdict.Failed("reason=panel-open");

        yield return Verdict.InMissionTick;
        var units = _host.ChooseCompany(largest: true);
        if (units == 0)
            yield return Verdict.Failed("reason=no-company");

        _host.SelectChosenCompany();
        var opened = _host.OpenPanel();
        var openedAt = _now;
        while (_now - openedAt < PanelSettleSeconds)
            yield return Verdict.Waiting("panel-seen");

        _host.PressKeep();
        var pressedAt = _now;
        while (_host.Keeping == SeatKeepKind.None && _now - pressedAt < SeatTrialRules.ShortWaitSeconds)
            yield return Verdict.Waiting("keep-taken");
        if (_host.Keeping == SeatKeepKind.None)
            yield return Verdict.Failed("reason=not-kept opened=" + Bit(opened) + " said=" + _host.Said);

        var kind = _host.Keeping;
        var same = _host.KeepsChosen;
        var keptAt = _now;
        var shot = false;
        while (_now - keptAt < SeatTrialRules.OrbitSettleSeconds)
        {
            if (!shot && _now - keptAt >= CompanyShotSeconds)
            {
                shot = true;
                _host.Screenshot("hud_keep_to_company.bmp");
            }

            yield return Verdict.Waiting("orbit");
        }

        var distance = _host.DistanceToKept();
        _host.PressKeep();
        var letGoAt = _now;
        while (_host.Keeping != SeatKeepKind.None && _now - letGoAt < SeatTrialRules.ShortWaitSeconds)
            yield return Verdict.Waiting("let-go");

        var letGo = _host.Keeping == SeatKeepKind.None;
        yield return Verdict.InMissionTick;
        _host.ClosePanel(asPlayer: true);

        var facts = "opened=" + Bit(opened) + " kind=" + kind + " chosen=" + Bit(same) + " units=" + units.ToString(CultureInfo.InvariantCulture) + " distance=" + Num(distance)
            + " orbit=" + Num(SeatOrbit.StartDistance) + " letgo=" + Bit(letGo) + Standing();
        var pass = opened && kind == SeatKeepKind.Company && same && SeatTrialRules.KeptNearTarget(distance, SeatOrbit.StartDistance) && letGo && Seated && !_host.Faulted;
        yield return pass ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    private IEnumerable<Verdict> LetGoOnDeath()
    {
        if (!Seated)
            yield return NotSeated();
        if (_host.PanelOpen)
            yield return Verdict.Failed("reason=panel-open");

        yield return Verdict.InMissionTick;
        if (!_host.ChooseSoldier())
            yield return Verdict.Failed("reason=no-soldier");

        foreach (var verdict in Aim())
            yield return verdict;

        _host.PressKeep();
        var pressedAt = _now;
        while (_host.Keeping == SeatKeepKind.None && _now - pressedAt < SeatTrialRules.ShortWaitSeconds)
            yield return Verdict.Waiting("keep-taken");
        if (_host.Keeping != SeatKeepKind.Soldier)
            yield return Verdict.Failed("reason=soldier-not-kept");
        if (!_host.KeepsChosen)
            yield return Verdict.Failed("reason=another-soldier-kept");

        var saidBefore = _host.Said;
        yield return Verdict.InMissionTick;
        _host.KillChosenSoldier();
        var killedAt = _now;
        while (_host.Keeping != SeatKeepKind.None && _now - killedAt < SeatTrialRules.ShortWaitSeconds)
            yield return Verdict.Waiting("soldier-let-go");

        var soldierGone = _host.Keeping == SeatKeepKind.None;
        var soldierSaid = _host.Said - saidBefore;
        var soldierMessage = _host.LastSaid == SeatMessage.KeepGone;

        yield return Verdict.InMissionTick;
        var units = _host.ChooseCompany(largest: false);
        if (units == 0)
            yield return Verdict.Failed("reason=no-company-to-kill soldier_gone=" + Bit(soldierGone));

        _host.SelectChosenCompany();
        var opened = _host.OpenPanel();
        var openedAt = _now;
        while (_now - openedAt < PanelSettleSeconds)
            yield return Verdict.Waiting("panel-seen");

        _host.PressKeep();
        var companyPressedAt = _now;
        while (_host.Keeping == SeatKeepKind.None && _now - companyPressedAt < SeatTrialRules.ShortWaitSeconds)
            yield return Verdict.Waiting("company-kept");
        var companyKept = _host.Keeping == SeatKeepKind.Company;

        var saidBeforeCompany = _host.Said;
        yield return Verdict.InMissionTick;
        _host.KillChosenCompany();
        var companyKilledAt = _now;
        while (_host.Keeping != SeatKeepKind.None && _now - companyKilledAt < SeatTrialRules.ShortWaitSeconds * 2f)
            yield return Verdict.Waiting("company-let-go");

        var companyGone = _host.Keeping == SeatKeepKind.None;
        var companySaid = _host.Said - saidBeforeCompany;
        var companyMessage = _host.LastSaid == SeatMessage.KeepGone;
        yield return Verdict.InMissionTick;
        _host.ClosePanel(asPlayer: true);

        var facts = "soldier_gone=" + Bit(soldierGone) + " soldier_messages=" + soldierSaid.ToString(CultureInfo.InvariantCulture)
            + " soldier_message=" + Bit(soldierMessage) + " opened=" + Bit(opened)
            + " company_units=" + units.ToString(CultureInfo.InvariantCulture) + " company_kept=" + Bit(companyKept)
            + " company_gone=" + Bit(companyGone) + " company_messages=" + companySaid.ToString(CultureInfo.InvariantCulture)
            + " company_message=" + Bit(companyMessage) + Standing();
        var pass = soldierGone && soldierSaid == 1 && soldierMessage && opened && companyKept && companyGone && companySaid == 1 && companyMessage && Seated && !_host.Faulted;
        yield return pass ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    // ---- Stage S4: the hero fights on ----

    private const float LeaveSettleSeconds = 0.3f;
    private const float StandSettleSeconds = 1f;
    private const float CompanyStopSeconds = 2f;

    private bool _playerAfterLeave;
    private float _standSpeed = float.NaN;
    private int _ownedBeforeFall;

    // Leaves the Seat if it is up, sets the trial's fight value, waits for the mission tick to hand the hero back, and enters again, so the
    // Seat reads the setting at a new entry. A failed verdict ends the step, and nothing after it runs.
    private IEnumerable<Verdict> ReEnter(bool fightOn)
    {
        _host.SetFightOn(fightOn);
        _host.SetInput(NoInput);
        if (_host.State != SeatState.Off)
        {
            _host.PressSeatKey();
            var pressedAt = _now;
            while (_host.PressPending)
                yield return _now - pressedAt > PressTakenSeconds ? Verdict.Failed("reason=press-not-taken") : Verdict.Waiting("press-taken");
            if (_host.State != SeatState.Off)
                yield return Verdict.Failed("reason=still-seated state=" + _host.State);
        }

        var leftAt = _now;
        while (_now - leftAt < LeaveSettleSeconds)
            yield return Verdict.Waiting("hand-back");
        _playerAfterLeave = _host.HeroIsPlayerControlled && !_host.FightGateUp;

        _host.PressSeatKey();
        var enteredAt = _now;
        while (_host.PressPending)
            yield return _now - enteredAt > PressTakenSeconds ? Verdict.Failed("reason=press-not-taken") : Verdict.Waiting("press-taken");
        while (_host.State != SeatState.Seated)
            yield return _host.State == SeatState.Off ? Verdict.Failed("reason=not-entered state=Off") : Verdict.Waiting("seated");
    }

    private IEnumerable<Verdict> WaitForTheHandOff()
    {
        var waitedAt = _now;
        while (!_host.HeroOnAi && _now - waitedAt < SeatTrialRules.HandOffWaitSeconds)
            yield return Verdict.Waiting("hand-off");
        if (!_host.HeroOnAi)
            yield return Verdict.Failed("reason=not-handed controller=" + (_host.HeroIsPlayerControlled ? "Player" : "other") + " gate=" + Bit(_host.FightGateUp) + Standing());
    }

    private IEnumerable<Verdict> FightOn()
    {
        if (!Seated)
            yield return NotSeated();

        foreach (var verdict in ReEnter(fightOn: true))
            yield return verdict;
        foreach (var verdict in WaitForTheHandOff())
            yield return verdict;

        var start = _host.HeroPosition();
        yield return Verdict.InMissionTick;
        _host.ChargeHeroFormation();
        var chargedAt = _now;
        while (_now - chargedAt < SeatTrialRules.FightWindowSeconds)
            yield return Verdict.Waiting("fight");

        var end = _host.HeroPosition();
        var moved = start == null || end == null ? float.NaN : start.Value.Distance2D(end.Value);
        var onAi = _host.HeroOnAi;
        var retreating = _host.HeroRetreating;
        var panicked = _host.HeroPanicked;

        yield return Verdict.InMissionTick;
        var opened = _host.OpenPanel();
        var openedAt = _now;
        while (_now - openedAt < PanelSettleSeconds)
            yield return Verdict.Waiting("panel-seen");

        var panelOpen = opened && _host.PanelOpen;
        yield return Verdict.InMissionTick;
        _host.ClosePanel(asPlayer: true);

        var facts = "controller=" + (onAi ? "AI" : "other") + " moved=" + Num(moved) + " retreating=" + Bit(retreating) + " panicked=" + Bit(panicked)
            + " gate=" + Bit(_host.FightGateUp) + " panel=" + Bit(panelOpen) + Standing();
        var pass = onAi && SeatTrialRules.FoughtOn(moved) && !retreating && !panicked && _host.FightGateUp && panelOpen && Seated && !_host.Faulted;
        yield return pass ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    private IEnumerable<Verdict> MoraleHeld()
    {
        if (!Seated)
            yield return NotSeated();
        if (!_host.HeroOnAi)
            yield return Verdict.Failed("reason=not-handed gate=" + Bit(_host.FightGateUp));

        yield return Verdict.InMissionTick;
        _host.WriteHeroMorale(0f);
        var written = _host.HeroMorale;
        if (!SeatTrialRules.AtMoraleFloor(written))
            yield return Verdict.Failed("reason=morale-not-written written=" + Num(written) + Standing());

        var wroteAt = _now;
        while (_now - wroteAt < SeatTrialRules.MoraleWindowSeconds)
            yield return Verdict.Waiting("morale");

        var onAi = _host.HeroOnAi;
        var panicked = _host.HeroPanicked;
        var retreating = _host.HeroRetreating;
        var facts = "controller=" + (onAi ? "AI" : "other") + " panicked=" + Bit(panicked) + " retreating=" + Bit(retreating) + " written=" + Num(written) + " morale=" + Num(_host.HeroMorale)
            + " seconds=" + Num(_now - wroteAt) + Standing();
        yield return onAi && !panicked && !retreating && Seated && !_host.Faulted ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    private IEnumerable<Verdict> MeasureHeroSpeed()
    {
        if (_heroAt == null)
            yield return Verdict.Failed("reason=no-hero");

        var heroStart = _heroAt!.Value;
        var fromTime = _heroTime;
        var startedAt = _now;
        while (_now - startedAt < SeatTrialRules.HeroWindowSeconds)
        {
            yield return Verdict.Waiting("hero-window");
            if (_heroAt == null)
                yield return Verdict.Failed("reason=no-hero");
        }

        _standSpeed = heroStart.Distance2D(_heroAt!.Value) / (_heroTime - fromTime);
    }

    private IEnumerable<Verdict> BackToStand()
    {
        if (!Seated)
            yield return NotSeated();

        yield return Verdict.InMissionTick;
        _host.StopHeroFormation();
        var stoppedAt = _now;
        while (_now - stoppedAt < CompanyStopSeconds)
            yield return Verdict.Waiting("company-stops");

        foreach (var verdict in MeasureHeroSpeed())
            yield return verdict;
        var before = _standSpeed;
        if (!SeatTrialRules.HeroStood(before))
            yield return Verdict.Failed("reason=ground-not-quiet before=" + Num(before) + Standing());

        foreach (var verdict in ReEnter(fightOn: false))
            yield return verdict;
        var gateDown = !_host.FightGateUp;

        var settledAt = _now;
        while (_now - settledAt < StandSettleSeconds)
            yield return Verdict.Waiting("settle");

        foreach (var verdict in MeasureHeroSpeed())
            yield return verdict;
        var speed = _standSpeed;
        var player = _host.HeroIsPlayerControlled;
        var facts = "after_leave=" + (_playerAfterLeave ? "Player" : "other") + " controller=" + (player ? "Player" : "other") + " gate=" + Bit(!gateDown)
            + " before=" + Num(before) + " speed=" + Num(speed) + Standing();
        yield return _playerAfterLeave && player && gateDown && SeatTrialRules.HeroStood(speed) && Seated && !_host.Faulted ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    // ---- Stage S5: take a soldier after the fall ----

    private IEnumerable<Verdict> KeepToALargeCompany()
    {
        yield return Verdict.InMissionTick;
        if (_host.ChooseCompany(largest: true) == 0)
            yield return Verdict.Failed("reason=no-company");

        _host.SelectChosenCompany();
        var opened = _host.OpenPanel();
        var openedAt = _now;
        while (_now - openedAt < PanelSettleSeconds)
            yield return Verdict.Waiting("panel-seen");

        _host.PressKeep();
        var pressedAt = _now;
        while (_host.Keeping == SeatKeepKind.None && _now - pressedAt < SeatTrialRules.ShortWaitSeconds)
            yield return Verdict.Waiting("keep-taken");
        if (_host.Keeping != SeatKeepKind.Company)
            yield return Verdict.Failed("reason=not-kept opened=" + Bit(opened) + " said=" + _host.Said.ToString(CultureInfo.InvariantCulture) + Standing());

        yield return Verdict.InMissionTick;
        _host.ClosePanel(asPlayer: true);
    }

    private IEnumerable<Verdict> HeroFalls()
    {
        if (!Seated)
            yield return NotSeated();

        foreach (var verdict in ReEnter(fightOn: true))
            yield return verdict;
        foreach (var verdict in WaitForTheHandOff())
            yield return verdict;
        if (!_host.HeroMortal)
            yield return Verdict.Failed("reason=hero-immortal");

        foreach (var verdict in KeepToALargeCompany())
            yield return verdict;
        var kept = _host.Keeping;

        _ownedBeforeFall = _host.FormationsOwnedByHero();
        var saidBefore = _host.Said;
        yield return Verdict.InMissionTick;
        _host.KillHero();
        var killedAt = _now;
        // The engine clears the main agent inside the kill, and the Seat lowers the gate and says the fall at its next mission tick, so a read
        // that follows the kill sees the gate still up. The step waits for the Seat's reaction, within a named limit, and judges what it finds then.
        while (!FallSeen(saidBefore) && _now - killedAt < SeatTrialRules.FallWaitSeconds)
            yield return Verdict.Waiting("fall");

        var waited = _now - killedAt;
        var said = _host.Said - saidBefore;
        var fell = _host.LastSaid == SeatMessage.HeroFell;
        var facts = "main_null=" + Bit(!_host.HeroAlive) + " gate=" + Bit(_host.FightGateUp) + " owned=" + _ownedBeforeFall.ToString(CultureInfo.InvariantCulture)
            + " kept=" + kept + " messages=" + said.ToString(CultureInfo.InvariantCulture) + " message_fell=" + Bit(fell) + " waited=" + Num(waited) + Standing();
        var pass = !_host.HeroAlive && !_host.FightGateUp && _ownedBeforeFall > 0 && said == 1 && fell && Seated && !_host.Faulted;
        yield return pass ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    // The hero is gone, the Seat lowered the gate, and it has said something since the kill.
    private bool FallSeen(int saidBefore) => !_host.HeroAlive && !_host.FightGateUp && _host.Said > saidBefore;

    private IEnumerable<Verdict> TakeSoldier()
    {
        if (!Seated)
            yield return NotSeated();
        if (_host.HeroAlive)
            yield return Verdict.Failed("reason=hero-alive");
        if (_host.Keeping != SeatKeepKind.Company)
            yield return Verdict.Failed("reason=no-kept-company kept=" + _host.Keeping);

        var saidBefore = _host.Said;
        _host.PressTake();
        var pressedAt = _now;
        while (!_host.HeroAlive && _now - pressedAt < SeatTrialRules.ShortWaitSeconds)
            yield return Verdict.Waiting("take");

        var took = _host.HeroAlive && _host.TookFromKeptCompany;
        var reading = _host.ReadHandOver();
        var said = _host.Said - saidBefore;

        yield return Verdict.InMissionTick;
        var opened = _host.OpenPanel();
        var openedAt = _now;
        while (_now - openedAt < PanelSettleSeconds)
            yield return Verdict.Waiting("panel-seen");

        var panelOpen = opened && _host.PanelOpen;
        yield return Verdict.InMissionTick;
        _host.ClosePanel(asPlayer: true);

        var facts = "took=" + Bit(took) + " controller_owner=" + Bit(reading.ControllerOwnerIsMain) + " owned_before=" + _ownedBeforeFall.ToString(CultureInfo.InvariantCulture)
            + " owned_after=" + reading.OwnedByMain.ToString(CultureInfo.InvariantCulture) + " still_ai=" + reading.StillAi.ToString(CultureInfo.InvariantCulture)
            + " messages=" + said.ToString(CultureInfo.InvariantCulture) + " panel=" + Bit(panelOpen) + Standing();
        var pass = took && SeatTrialRules.CommandHandedOver(_ownedBeforeFall, reading) && panelOpen && Seated && !_host.Faulted;
        yield return pass ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }
}
