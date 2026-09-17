namespace TAOM.Features.CultureDoctrine;

/// <summary>Reads the culture's <c>CultureMorale</c> from the validated catalog behind the MCM
/// toggles; off, or for a culture without a row, it is vanilla. Singleton; the catalog is a
/// <c>Lazy</c> built once, so the worker-thread reads see an immutable object.</summary>
public sealed class CultureMoraleService : ICultureMoraleService
{
    private readonly ICultureDoctrineConfigProvider _config;
    private readonly ICultureDoctrineSettingsProvider _settings;

    public CultureMoraleService(ICultureDoctrineConfigProvider config, ICultureDoctrineSettingsProvider settings)
    {
        _config = config;
        _settings = settings;
        // Built on the main thread at game start (the morale models resolve it there): loading
        // the file and logging its result here means the worker-thread panic seam never sees the
        // first, file-reading call of the lazy catalog.
        _config.GetCatalog();
    }

    public bool CanPanic(string? cultureId) =>
        !_settings.IsMoraleEnabled || _config.GetCatalog().Resolve(cultureId).Morale.CanPanic;

    public float InitialMorale(string? cultureId, float baseMorale) =>
        _settings.IsMoraleEnabled ? _config.GetCatalog().Resolve(cultureId).Morale.InitialMorale(baseMorale) : baseMorale;
}
