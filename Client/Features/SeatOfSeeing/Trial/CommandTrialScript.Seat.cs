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
}
