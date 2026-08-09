namespace TAOM.Features.AutoResolveDiagnostics;

/// <summary>
/// Seam over the MCM toggles so the behavior is testable without MCM loaded
/// (TaomSettings.Instance is a static and is null outside the game).
/// </summary>
public interface IAutoResolveDiagnosticsSettingsProvider
{
    /// <summary>Master switch. When false the feature does NOTHING — no start snapshot, no
    /// capture, no census, no log line. It is checked before the per-battle snapshot, not only
    /// before the write, so "off" costs nothing rather than merely producing nothing.</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// The once-per-session troop census, separately gated because its cost profile is different:
    /// one line per CharacterObject — 8,341 in a live campaign, roughly 2.5 MB of log — against
    /// one line per battle for everything else. Someone who wants battle records but not a
    /// multi-megabyte census dump should not have to choose between them.
    ///
    /// Subordinate to <see cref="IsEnabled"/>: master off means no census regardless.
    ///
    /// Defaults OFF while <see cref="IsEnabled"/> defaults ON, because the two answer different
    /// kinds of question. A battle record describes a session that already happened, so it has to
    /// have been running before anyone knew they wanted it. The census is static per build — the
    /// engine's tier, power and classification per troop type move only when troop data or the
    /// balance config moves — so one capture serves until then. Measured on a live session it was
    /// 8,341 of 17,622 log lines: 47% of the file, rewritten identically every launch.
    /// </summary>
    bool IsCensusEnabled { get; }
}
