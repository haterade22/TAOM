namespace TAOM.Features.AutoResolveDiagnostics;

public class AutoResolveDiagnosticsSettingsProvider : IAutoResolveDiagnosticsSettingsProvider
{
    // Default ON, unlike the per-event diagnostics (BlowDiagnostics, SiegePropDiagnostics) which
    // default off because they fire on a hot path. This one emits a single line per completed
    // battle and answers a question that is only ever asked about a session that has already
    // happened — the EconomyDiagnostics rationale. A diagnostic the user has to switch on in
    // advance is one they never have data from when it matters.
    //
    // Each ?? fallback MUST equal the compiled default on the matching TaomSettings property;
    // AutoResolveDiagnosticsSettingsProviderTests pins both directions. A drift between them
    // behaves one way before the settings file is written and another way after — a bug that
    // reproduces only on a fresh install.
    public bool IsEnabled => TaomSettings.Instance?.LogAutoResolvedBattles ?? true;

    // The census defaults OFF while the battle log defaults ON, because they answer different
    // kinds of question. A battle record is about a session that already happened, so it has to
    // have been running before anyone knew they wanted it. The census is static per build — the
    // engine's tier, power and classification for each troop type only move when troop data or
    // the balance config moves — so one capture serves indefinitely, and measured on a live log
    // it was 8,341 of 17,622 lines: 47% of the file, re-written identically every session.
    public bool IsCensusEnabled => TaomSettings.Instance?.LogAutoResolveTroopCensus ?? false;
}
