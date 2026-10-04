namespace TAOM.Features.TimeAcceleration;

public class TimeAccelerationSettingsProvider : ITimeAccelerationSettingsProvider
{
    // Read on a campaign hot path (per party, per score, per day or every map frame). Resolving
    // TaomSettings.Instance walks MCM's settings containers, so the reference is cached on its first
    // non-null read and read THROUGH, never snapshotted: MCM edits its one registered instance in place
    // (reset and presets copy values into it), so live MCM edits still apply. Lazy, not in the
    // constructor, so a resolve before MCM is up cannot pin the fallbacks. Same contract as
    // BattleBalanceSettingsProvider.
    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public TimeAccelerationSettingsProvider() { }
    internal TimeAccelerationSettingsProvider(TaomSettings settings) => _settings = settings;

    public int FastForwardMultiplier => Settings?.FastForwardMultiplier ?? 4;

    // Both sliders range 1 to 128 on their own, so without this a player can make "extra" slower
    // than "fast", and the MapBar button's lit state (a fast-forward mode running ABOVE the normal
    // multiplier) becomes unreachable. Floor, never swap: the fast-forward value stays exactly what
    // the player set.
    public int ExtraFastForwardMultiplier =>
        ClampExtra(FastForwardMultiplier, Settings?.ExtraFastForwardMultiplier ?? 8);

    public int CtrlSpaceMultiplier => Settings?.CtrlSpaceMultiplier ?? 16;

    internal static int ClampExtra(int fast, int extra) => extra < fast ? fast : extra;
}
