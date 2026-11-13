using TAOM.Features.BattleLoadDiagnostics.Domain;

namespace TAOM.Features.BattleLoadDiagnostics;

// Robust collection mechanism for the intermittent battle-load HANG. A hang freezes the
// MAIN thread, so no in-the-moment dialog can render and the player force-quits — meaning
// the on-disk taom_debug log never reaches the dev by itself. This marker closes
// that gap by mirroring Dependencies/Foundation/IncompatibleModDetector's pattern:
//
//   - MarkInflight    written when a battle/scene load STARTS (phase 4, Mission.Initialize)
//   - ClearInflight   removed when the load reaches "playable" (phase 6) or the mission ends
//   - TryConsumeStaleMarker  checked at the NEXT session's main menu — a surviving marker
//                            means the previous load never finished, so surface a notice.
//
// The marker catches the player who force-quits a hung load. (A load watchdog that wrote a bundle
// for a player who WAITED past 300 s was removed 2026-11-12.)
public interface IBattleLoadStallMarker
{
    void MarkInflight(string sceneName);

    void ClearInflight();

    // Read + DELETE a stale marker (one notice per stalled load). Null when none exists.
    StallMarkerInfo? TryConsumeStaleMarker();
}
