namespace TAOM.Features.Elephant;

/// <summary>Whether the howdah diagnostics log is on (#627). Instrumentation only: it gates log lines, never behaviour.</summary>
public interface IHowdahDiagnosticsSettingsProvider
{
    bool IsEnabled { get; }
}
