namespace TAOM.Features.LordRetinue;

/// <summary>
/// Reads MCM live values (<c>TaomSettings.Instance</c>). The instance is cached on its first non-null
/// read and read through, never snapshotted, as in <c>AlignmentDesertionSettingsProvider</c>.
/// </summary>
public sealed class LordRetinueSettingsProvider : ILordRetinueSettingsProvider
{
    /// <summary>The upper bound of the MCM slider and of the clamp below.</summary>
    public const int MaxExtraHeroesCap = 2;

    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public LordRetinueSettingsProvider()
    {
    }

    internal LordRetinueSettingsProvider(TaomSettings settings) => _settings = settings;

    public bool IsEnabled => Settings?.EnableLordRetinue ?? true;

    public int MaxExtraHeroes
    {
        get
        {
            var value = Settings?.LordRetinueMaxExtraHeroes ?? 1;
            return value < 0 ? 0 : value > MaxExtraHeroesCap ? MaxExtraHeroesCap : value;
        }
    }
}
