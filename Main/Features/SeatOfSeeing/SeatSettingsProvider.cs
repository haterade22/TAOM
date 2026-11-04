namespace TAOM.Features.SeatOfSeeing;

/// <summary>
/// <see cref="ISeatSettingsProvider"/> over the MCM <c>TaomSettings</c> singleton, never keeping a null, with the defaults here so the
/// fallback and the MCM property cannot drift.
/// </summary>
internal sealed class SeatSettingsProvider : ISeatSettingsProvider
{
    internal const bool DefaultEnabled = true;

    /// <summary>Off by default: a player who never asks sees vanilla behaviour (build-spec section 1).</summary>
    internal const bool DefaultBeginBattlesInSeat = false;

    private TaomSettings? _settings;

    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public SeatSettingsProvider() { }

    internal SeatSettingsProvider(TaomSettings settings) => _settings = settings;

    public bool Enabled => Settings?.SeatEnabled ?? DefaultEnabled;

    public bool BeginBattlesInSeat => Settings?.BeginBattlesInSeat ?? DefaultBeginBattlesInSeat;
}
