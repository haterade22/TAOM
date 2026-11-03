namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// When a riderless horse may be removed. The engine's fade, unlike a death, does not unlink a rider from its mount, so a horse that a rider
/// is getting on or off must stay: a rider left holding a faded horse crashes the game on its next speed read (native +0x6E5BCD,
/// <c>IMBAgent.GetWalkSpeedLimitOfMountable</c>). A horse may go once it has stood empty for a while and no soldier holds or has chosen it.
/// </summary>
internal static class RiderlessHorseRule
{
    /// <summary>Longer than any mount or dismount takes.</summary>
    internal const double WaitSeconds = 20.0;

    internal static bool MayFade(double riderlessSeconds, bool claimedBySoldier) => riderlessSeconds >= WaitSeconds && !claimedBySoldier;
}
