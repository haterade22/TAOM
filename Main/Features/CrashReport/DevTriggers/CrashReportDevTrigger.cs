using System;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CrashReport.DevTriggers;

// Mission-side dev trigger. Reads the MCM toggle; if set, throws on the very next
// OnMissionTick and resets the toggle. Wired into Mission via AddMissionBehavior
// from SubModule on game start. CampaignBehavior surface is overkill here — the
// trigger is QA-only and we want it usable in any mission (custom battle, arena,
// regular field battle), not just campaign.
//
// Settings caching (Codex review #46 LOW-01 fix): `CrashReportSettings.Instance` is
// a provider-scan-plus-lookup in MCM, not a static-field read. Cache the resolved
// settings reference on first non-null result so per-tick cost drops to a single
// field read + null check. MCM returns the SAME singleton on every call once
// initialised, so caching is safe.
public sealed class CrashReportDevTriggerMissionBehavior : MissionLogic
{
    private CrashReportSettings? _cachedSettings;

    // #699 follow-up repro: a throw out of a TAOM behavior's AfterStart, which runs inside
    // Mission.AfterStart's behavior loop (v1.5.3 Mission.cs:3839-3842) before the mission is
    // Continuing (:3851). Reset before the throw so one toggle fires once, even if the load reruns.
    public override void AfterStart()
    {
        CrashReportSettings? settings;
        try { settings = CrashReportSettings.Instance; }
        catch { return; /* MCM read errors must not break mission start */ }
        if (settings == null || !settings.EnableCrashCapture || !settings.ThrowOnNextMissionAfterStart) return;

        settings.ThrowOnNextMissionAfterStart = false;
        throw new TaomDevTriggerException("TAOM CrashReport dev trigger: mission AfterStart throw fired by MCM toggle.");
    }

    public override void OnMissionTick(float dt)
    {
        try
        {
            var settings = _cachedSettings ??= CrashReportSettings.Instance;
            if (settings == null) return;
            if (!settings.EnableCrashCapture) return; // master toggle gate
            if (!settings.ThrowOnNextMissionTick) return;

            settings.ThrowOnNextMissionTick = false;
            throw new TaomDevTriggerException("TAOM CrashReport dev trigger: mission-tick throw fired by MCM toggle.");
        }
        catch (TaomDevTriggerException) { throw; }
        catch { /* swallow MCM read errors */ }
    }
}
