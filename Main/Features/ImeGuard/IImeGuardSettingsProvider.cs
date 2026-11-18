namespace TAOM.Features.ImeGuard;

/// <summary>Isolates the MCM read so the IME guard is tested without a live MCM page.</summary>
public interface IImeGuardSettingsProvider
{
    bool Enabled { get; }
}

/// <summary>
/// Reads <see cref="TaomSettings.ImeGuardEnabled"/> on each tick. The MCM instance is cached on its first non-null read
/// and read through, so a change in MCM applies at once. With no MCM instance, or a read that throws, the guard stays
/// on, its shipped default.
/// </summary>
public sealed class ImeGuardSettingsProvider : IImeGuardSettingsProvider
{
    private TaomSettings? _settings;

    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public bool Enabled
    {
        get
        {
            try
            {
                return Settings?.ImeGuardEnabled ?? true;
            }
            catch
            {
                // swallow-ok: an MCM read failure keeps the shipped default.
                return true;
            }
        }
    }
}
