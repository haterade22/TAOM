using System;

namespace TAOM.Features.BattleLoadDiagnostics.Domain;

// A stall marker recovered from a PREVIOUS session whose battle/scene load never reached
// "playable" (the marker survived because phase 6 / mission-end never ran — the load hung
// and the player force-quit). Drives the next-session "please send your log" notice.
public sealed class StallMarkerInfo
{
    public StallMarkerInfo(string sceneName, DateTime? writtenUtc, string logFilePath, string markerPath)
    {
        SceneName = sceneName;
        WrittenUtc = writtenUtc;
        LogFilePath = logFilePath;
        MarkerPath = markerPath;
    }

    public string SceneName { get; }

    // Informational provenance only (the marker's on-disk timestamp), surfaced for a future
    // staleness check if one is ever wanted — NOT currently read by any decision path. The
    // notice fires exactly once per stalled load (consume-and-delete) regardless of age.
    public DateTime? WrittenUtc { get; }

    // The PRIOR session's taom_debug log — the file the player should send. (Not the
    // current session's log; that's a different, newer file.)
    public string LogFilePath { get; }
    public string MarkerPath { get; }
}
