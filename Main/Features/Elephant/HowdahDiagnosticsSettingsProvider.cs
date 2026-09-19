namespace TAOM.Features.Elephant;

/// <summary>
/// Wraps <see cref="TaomSettings"/> so the howdah code can read the toggle without MCM loaded. TaomSettings.Instance is
/// null whenever MCM is absent, so the fallback must equal the compiled default: on, while the platform is being tested
/// (Mike, 2026-09-19, #627). HowdahDiagnosticsSettingsProviderTests pins both.
/// </summary>
public class HowdahDiagnosticsSettingsProvider : IHowdahDiagnosticsSettingsProvider
{
    public bool IsEnabled => TaomSettings.Instance?.EnableHowdahDiagnostics ?? true;
}
