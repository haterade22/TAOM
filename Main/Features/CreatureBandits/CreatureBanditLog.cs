using TAOM.Core.Logging;

namespace TAOM.Features.CreatureBandits;

/// <summary>
/// The feature's logger for static engine glue that cannot take one by injection (#692): the spawner and the
/// weapon-state hook report their failures through it. Set by <see cref="CreatureBanditsModule.InitializeStatics"/>,
/// cleared at unload. Kept outside the temporary Diagnostics folder, so stripping the diagnostics keeps the error log.
/// </summary>
internal static class CreatureBanditLog
{
    internal static IModLogger? Logger;

    public static void ResetForUnload() => Logger = null;
}
