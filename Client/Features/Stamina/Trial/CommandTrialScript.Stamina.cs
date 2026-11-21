using System;
using System.Collections.Generic;
using TAOM.Features.Stamina.Domain;

namespace TAOM.Client.Features.SeatOfSeeing.Trial;

/// <summary>The stamina steps of the command trial (#1506): <see cref="StaminaTrialRows"/> names them and <see cref="StaminaTrialRules"/> judges them.</summary>
internal sealed partial class CommandTrialScript
{
    private static void StaminaSteps(Dictionary<string, Func<CommandTrialScript, IEnumerable<Verdict>>> into)
    {
        into[StaminaTrialRows.Gate] = s => s.StaminaGate();
        into[StaminaTrialRows.Walk] = s => s.StaminaWalk();
        into[StaminaTrialRows.Fight] = s => s.StaminaFight();
    }

    private static float ShareOf(StaminaGroupSample sample) => sample.Live > 0 ? (float)sample.Runners / sample.Live : 0f;

    private string Sides() => " allies=" + Whole(_host.CountStaminaSoldiers(false)) + " enemies=" + Whole(_host.CountStaminaSoldiers(true));

    private IEnumerable<Verdict> StaminaGate()
    {
        var startedAt = _now;
        while (!_host.InBattle)
        {
            if (_now - startedAt > StaminaTrialRules.BattleWaitSeconds)
                yield return Verdict.Failed("reason=no-battle");
            yield return Verdict.Waiting("battle");
        }

        var battleAt = _now;
        var pictures = 0;
        while (true)
        {
            if (pictures == 0 && _now - battleAt >= StaminaTrialRules.GatePictureSeconds)
            {
                pictures++;
                _host.Screenshot(StaminaTrialRules.GatePicture);
            }

            yield return Verdict.InMissionTick;
            var census = _host.ReadStamina();
            var passes = pictures > 0 && StaminaTrialRules.GatePasses(census);
            if (passes || _now - battleAt >= StaminaTrialRules.GateWaitSeconds)
            {
                var facts = StaminaTrialRules.GateFacts(census, pictures) + Sides();
                yield return passes ? Verdict.Passed(facts) : Verdict.Failed(facts);
            }

            yield return Verdict.Waiting("gate");
        }
    }

    private IEnumerable<Verdict> StaminaWalk()
    {
        yield return Verdict.InMissionTick;
        if (!_host.ReadStamina().Armed)
            yield return Verdict.Failed("reason=not-armed" + Sides());

        _host.ChargeAll();
        var smallest = _host.ChooseStaminaGroups(false);
        if (smallest < StaminaTrialRules.MinGroup)
            yield return Verdict.Failed("reason=too-few smallest=" + Whole(smallest) + Sides());

        var chosenAt = _now;
        StaminaGroupSample forced;
        StaminaGroupSample control;
        while (true)
        {
            yield return Verdict.Waiting("runners");
            yield return Verdict.InMissionTick;
            forced = _host.SampleStaminaGroup(0);
            control = _host.SampleStaminaGroup(1);
            if (ShareOf(forced) >= StaminaTrialRules.BaseRunnerShare && ShareOf(control) >= StaminaTrialRules.BaseRunnerShare)
                break;
            if (_now - chosenAt > StaminaTrialRules.RunnersWaitSeconds)
                yield return Verdict.Failed("reason=no-runners forced_share=" + Num(ShareOf(forced)) + " control_share=" + Num(ShareOf(control)) + Sides());
        }

        var baseForced = new StaminaWindow(forced, _now);
        var baseControl = new StaminaWindow(control, _now);
        var baseAt = _now;
        while (_now - baseAt < StaminaTrialRules.BaseWindowSeconds)
        {
            yield return Verdict.Waiting("base-window");
            yield return Verdict.InMissionTick;
            forced = _host.SampleStaminaGroup(0);
            control = _host.SampleStaminaGroup(1);
            baseForced.Add(forced, _now);
            baseControl.Add(control, _now);
        }

        _host.ForceStaminaGroup(1, StaminaBand.Fresh);
        _host.ForceStaminaGroup(0, StaminaBand.Exhausted);
        var forcedAt = _now;
        while (true)
        {
            yield return Verdict.Waiting("apply");
            yield return Verdict.InMissionTick;
            forced = _host.SampleStaminaGroup(0);
            control = _host.SampleStaminaGroup(1);
            if (StaminaTrialRules.AllLocked(forced) && _host.ReadStamina().RefreshesWaiting == 0)
                break;
            if (_now - forcedAt > StaminaTrialRules.ApplyLimitSeconds)
                yield return Verdict.Failed("reason=not-applied locked=" + Whole(forced.Locked) + " live=" + Whole(forced.Live) + Sides());
        }

        var forcedWindow = new StaminaWindow(forced, _now);
        var controlWindow = new StaminaWindow(control, _now);
        var windowAt = _now;
        while (_now - windowAt < StaminaTrialRules.WalkWindowSeconds)
        {
            yield return Verdict.Waiting("walk-window");
            yield return Verdict.InMissionTick;
            forced = _host.SampleStaminaGroup(0);
            control = _host.SampleStaminaGroup(1);
            forcedWindow.Add(forced, _now);
            controlWindow.Add(control, _now);
        }

        _host.StaminaReleaseForced();
        var facts = StaminaTrialRules.WalkFacts(forced.Live, control.Live, _host.ReadStamina().RunSpeed, baseForced, baseControl, forcedWindow, controlWindow) + Sides();
        if (StaminaTrialRules.WalkContact(controlWindow.Releases))
            yield return Verdict.Failed("reason=contact " + facts);
        var pass = StaminaTrialRules.WalkPasses(baseForced.RunnerShare, forcedWindow.RunnerShare, forcedWindow.MeanSpeed, controlWindow.MeanSpeed);
        yield return pass ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }

    private IEnumerable<Verdict> StaminaFight()
    {
        yield return Verdict.InMissionTick;
        if (!_host.ReadStamina().Armed)
            yield return Verdict.Failed("reason=not-armed" + Sides());

        var smallest = _host.ChooseStaminaGroups(true);
        if (smallest < StaminaTrialRules.MinGroup)
            yield return Verdict.Failed("reason=too-few smallest=" + Whole(smallest) + Sides());

        var chosenAt = _now;
        var block = _host.SampleStaminaGroup(2);
        var blockAt = _now;
        StaminaGroupSample control;
        while (true)
        {
            yield return Verdict.Waiting("contact");
            yield return Verdict.InMissionTick;
            control = _host.SampleStaminaGroup(2);
            if (control.Releases - block.Releases >= StaminaTrialRules.ContactReleases)
                break;
            if (_now - blockAt >= StaminaTrialRules.ContactBlockSeconds)
            {
                block = control;
                blockAt = _now;
            }

            if (_now - chosenAt > StaminaTrialRules.ContactWaitSeconds)
                yield return Verdict.Failed("reason=no-contact waited=" + Num(_now - chosenAt) + Sides());
        }

        var contactAt = _now - chosenAt;
        var stripsBefore = _host.ReadStamina().AiStrips;
        _host.ForceStaminaGroup(2, StaminaBand.Fresh);
        _host.ForceStaminaGroup(1, StaminaBand.Tired);
        _host.ForceStaminaGroup(0, StaminaBand.Exhausted);
        var forcedAt = _now;
        StaminaGroupSample exhausted;
        while (true)
        {
            yield return Verdict.Waiting("apply");
            yield return Verdict.InMissionTick;
            exhausted = _host.SampleStaminaGroup(0);
            if (StaminaTrialRules.AllLocked(exhausted) && _host.ReadStamina().RefreshesWaiting == 0)
                break;
            if (_now - forcedAt > StaminaTrialRules.ApplyLimitSeconds)
                yield return Verdict.Failed("reason=not-applied locked=" + Whole(exhausted.Locked) + " live=" + Whole(exhausted.Live) + Sides());
        }

        var appliedAt = _now;
        while (_now - appliedAt < StaminaTrialRules.SettleSeconds)
            yield return Verdict.Waiting("settle");

        yield return Verdict.InMissionTick;
        exhausted = _host.SampleStaminaGroup(0);
        var tired = _host.SampleStaminaGroup(1);
        control = _host.SampleStaminaGroup(2);
        var eWindow = new StaminaWindow(exhausted, _now);
        var tWindow = new StaminaWindow(tired, _now);
        var cWindow = new StaminaWindow(control, _now);
        var windowAt = _now;
        while (true)
        {
            yield return Verdict.Waiting("fight");
            yield return Verdict.InMissionTick;
            exhausted = _host.SampleStaminaGroup(0);
            tired = _host.SampleStaminaGroup(1);
            control = _host.SampleStaminaGroup(2);
            eWindow.Add(exhausted, _now);
            tWindow.Add(tired, _now);
            cWindow.Add(control, _now);
            var elapsed = _now - windowAt;
            if (elapsed >= StaminaTrialRules.FightMaxSeconds || (elapsed >= StaminaTrialRules.FightMinSeconds && cWindow.Releases >= StaminaTrialRules.ControlEnoughReleases))
                break;
        }

        var strips = _host.ReadStamina().AiStrips - stripsBefore;
        var restoreStart = _host.SampleStaminaGroup(0).Releases;
        _host.StaminaReleaseForced();
        var restoredAt = _now;
        long restored = 0;
        while (restored < StaminaTrialRules.RestoreEnoughReleases && _now - restoredAt < StaminaTrialRules.RestoreLimitSeconds)
        {
            yield return Verdict.Waiting("restore");
            yield return Verdict.InMissionTick;
            restored = _host.SampleStaminaGroup(0).Releases - restoreStart;
        }

        var facts = StaminaTrialRules.FightFacts(exhausted.Live, tired.Live, control.Live, eWindow, tWindow, cWindow, strips, restored, contactAt) + Sides();
        var pass = StaminaTrialRules.FightPasses(eWindow.Releases, eWindow.Defends, tWindow.Releases, tWindow.LockedFrames, cWindow.Releases, restored);
        if (!pass && cWindow.Releases < StaminaTrialRules.ControlMinReleases)
            yield return Verdict.Failed("reason=contact-lost " + facts);
        yield return pass ? Verdict.Passed(facts) : Verdict.Failed(facts);
    }
}
