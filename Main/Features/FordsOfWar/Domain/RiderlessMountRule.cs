namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// When a riderless mount is removed. Every kind goes, horses, wargs, spiders and the great beasts alike (Mike, 2026-11-07): a mount
/// with no rider only runs around the field, and soldiers never choose it as a target (the engine takes a mount's side from its
/// rider, so a riderless one is nobody's enemy), so nobody clears it. The engine's fade, unlike a death, does not unlink a rider from its mount, so a mount that a rider is getting on or off must
/// stay: a rider left holding a faded mount crashes the game on its next speed read (native +0x6E5BCD,
/// <c>IMBAgent.GetWalkSpeedLimitOfMountable</c>). A mount may go once it has stood empty for a while and no soldier holds or has
/// chosen it. A howdah or mumakil crew is not on the mount: its platform releases the seats when the beast is gone, as on a death.
/// </summary>
internal static class RiderlessMountRule
{
    /// <summary>Longer than any mount or dismount takes.</summary>
    internal const double WaitSeconds = 20.0;

    internal static bool MayFade(double riderlessSeconds, bool claimedBySoldier) => riderlessSeconds >= WaitSeconds && !claimedBySoldier;
}
