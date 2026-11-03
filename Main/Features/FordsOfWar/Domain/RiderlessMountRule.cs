namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// Which riderless mounts are removed, and when. Horses and wargs go, because they only run around the field; a beast in the other creature tracks stays. The engine's fade,
/// unlike a death, does not unlink a rider from its mount, so a mount that a rider is getting on or off must stay: a rider left holding a faded mount crashes the game on its
/// next speed read (native +0x6E5BCD, <c>IMBAgent.GetWalkSpeedLimitOfMountable</c>). A mount may go once it has stood empty for a while and no soldier holds or has chosen it.
/// </summary>
internal static class RiderlessMountRule
{
    internal const string Horse = "horse";

    /// <summary>The Monster ids of the wargs, which a test holds to WargConfig's list.</summary>
    internal static readonly string[] Wargs = { "warg", "fell_warg" };

    /// <summary>Longer than any mount or dismount takes.</summary>
    internal const double WaitSeconds = 20.0;

    internal static bool IsWarg(string? monsterId) => monsterId != null && System.Array.IndexOf(Wargs, monsterId) >= 0;

    internal static bool IsSwept(string? monsterId) => monsterId == Horse || IsWarg(monsterId);

    internal static bool MayFade(double riderlessSeconds, bool claimedBySoldier) => riderlessSeconds >= WaitSeconds && !claimedBySoldier;
}
