using TAOM.Core.Logging;

namespace TAOM.Features.SpecialResources;

/// <summary>
/// The gold-only option (#1108) from MCM, with the rate range-checked.
/// </summary>
public sealed class SpecialResourceSettingsProvider : ISpecialResourceSettingsProvider
{
    public const int MinGoldPerResource = 50;
    public const int MaxGoldPerResource = 1000;
    public const int DefaultGoldPerResource = 200;

    private readonly IModLogger _logger;
    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;
    private bool _badRateLogged;

    public SpecialResourceSettingsProvider(IModLogger logger) => _logger = logger;

    internal SpecialResourceSettingsProvider(TaomSettings settings, IModLogger logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public bool EliteTroopsCostGoldOnly => Settings?.EliteTroopsCostGoldOnly ?? false;

    public int EmissaryGoldPerResource
    {
        get
        {
            var rate = Settings?.EmissaryGoldPerResource ?? DefaultGoldPerResource;
            if (rate >= MinGoldPerResource && rate <= MaxGoldPerResource)
                return rate;

            if (!_badRateLogged)
            {
                _badRateLogged = true;
                _logger.LogWarning($"[SpecRes] EmissaryGoldPerResource {rate} is outside {MinGoldPerResource} to {MaxGoldPerResource}; using {DefaultGoldPerResource}");
            }
            return DefaultGoldPerResource;
        }
    }
}
