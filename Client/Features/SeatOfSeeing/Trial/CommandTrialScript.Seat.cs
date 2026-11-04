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
    private const string RefuseStep = "refuse_in_deployment";

    private static readonly SeatInput NoInput = default;
    private static readonly SeatInput FlyForward = new(Right: 0f, Forward: 1f, Lift: 0f, ZoomNotches: 0f, LookYaw: 0f, LookPitch: 0f, Fast: false);

    private float _heightBeforeLeave = float.NaN;

    private bool Seated => _host.State == SeatState.Seated;

    private float HeightAboveGround(Vec3f at) => at.Z - _host.GroundHeight(at.X, at.Y);

    private Verdict NotSeated() => Verdict.Failed("reason=not-seated state=" + _host.State);

    private static void SeatSteps(Dictionary<string, Func<CommandTrialScript, IEnumerable<Verdict>>> into)
    {
        into[RefuseStep] = s => s.Refuse();
        into["enter"] = s => s.Enter();
        into["fly"] = s => s.Fly();
        into["hero_stands"] = s => s.HeroStands();
        into["leave_glide"] = s => s.LeaveGlide();
        into["reenter_keeps_height"] = s => s.Reenter();
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
        var controller = _host.HeroIsPlayerControlled;
        var facts = "rising=" + Bit(sawRising) + " seated=" + Bit(seated) + " frames=" + frames.ToString(CultureInfo.InvariantCulture)
            + " controller=" + (controller ? "Player" : "other");
        var pass = sawRising && seated && SeatTrialRules.OwnedEnoughFrames(frames) && controller && !_host.Faulted;
        yield return pass ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    private IEnumerable<Verdict> Fly()
    {
        var at = _host.ViewPosition();
        if (!Seated || at == null)
            yield return NotSeated();

        var startAt = at!.Value;
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
        var facts = "moved=" + Num(moved) + " expected=" + Num(expected) + " height=" + Num(height) + " seconds=" + Num(seconds);
        yield return Seated && SeatTrialRules.FlewExpected(expected, moved) ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    private IEnumerable<Verdict> HeroStands()
    {
        if (!Seated)
            yield return NotSeated();
        if (_heroAt == null)
            yield return Verdict.Failed("reason=no-hero");

        var heroStart = _heroAt!.Value;
        var fromTime = _heroTime;
        while (true)
        {
            yield return Verdict.Waiting("hero-window");
            if (_heroAt == null)
                yield return Verdict.Failed("reason=no-hero");
            else if (_heroTime - fromTime >= SeatTrialRules.HeroWindowSeconds)
                break;
        }

        var span = _heroTime - fromTime;
        var speed = heroStart.Distance2D(_heroAt!.Value) / span;
        var facts = "speed=" + Num(speed) + " seconds=" + Num(span);
        yield return Seated && SeatTrialRules.HeroStood(speed) ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    private IEnumerable<Verdict> LeaveGlide()
    {
        if (!Seated)
            yield return NotSeated();

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
        var facts = "before=" + Num(_heightBeforeLeave) + " after=" + Num(after);
        yield return SeatTrialRules.HeightKept(_heightBeforeLeave, after) && !_host.Faulted ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }
}
