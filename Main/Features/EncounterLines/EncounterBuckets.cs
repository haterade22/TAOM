using TAOM.Features.Execution;

namespace TAOM.Features.EncounterLines;

/// <summary>
/// The engine-free rules that place the player against a speaker (docs/features/encounter-lines.md "Buckets"). Every
/// input is a plain value read by the adapter; the strings returned are the bucket part of a game-text variation.
/// </summary>
internal static class EncounterBuckets
{
    internal const string Liege = "liege";
    internal const string Ally = "ally";
    internal const string Rival = "rival";
    internal const string Foe = "foe";
    internal const string Kin = "kin";
    internal const string Friend = "friend";
    internal const string Side = "side";

    /// <summary>
    /// A lord's bucket, checked in this order: liege (the player rules the speaker's kingdom); foe (at war while not on
    /// the same side, or Free against Evil at any time); kin (the same race family, and for Men the same speaker
    /// group); ally (at peace with a known player side); rival (anything else). A null <paramref name="playerSide"/> is
    /// a player whose side cannot be read.
    /// </summary>
    internal static string Lord(
        bool playerRulesRealm,
        bool atWar,
        FactionSide speakerSide,
        FactionSide? playerSide,
        string? speakerFamily,
        string? playerFamily,
        string? speakerGroup,
        string? playerGroup)
    {
        if (playerRulesRealm)
            return Liege;

        var sameSide = playerSide.HasValue && playerSide.Value == speakerSide;
        var opposed = playerSide.HasValue && FactionSideRules.AreOpposed(speakerSide, playerSide.Value);
        if ((atWar && !sameSide) || opposed)
            return Foe;

        if (IsKin(speakerFamily, playerFamily, speakerGroup, playerGroup))
            return Kin;

        return !atWar && playerSide.HasValue ? Ally : Rival;
    }

    /// <summary>A caravan's or villagers' bucket: foe when at war with the party's realm, kin when its home culture shares the player's speaker group, else friend.</summary>
    internal static string Trade(bool atWar, string? homeGroup, string? playerGroup)
    {
        if (atWar)
            return Foe;
        return homeGroup != null && homeGroup == playerGroup ? Kin : Friend;
    }

    /// <summary>
    /// A bandit clan's bucket, or null for a clan the data does not list. Kin: a clan with a home group when the
    /// player's culture is in it, otherwise the same race family (orc, dwarf and elf clans, and looters for any human).
    /// Side: the player's side equals the clan's, Neutral included. Foe: anything else.
    /// </summary>
    internal static string? Bandit(BanditClan? clan, string? playerGroup, string? playerFamily, FactionSide? playerSide)
    {
        if (clan == null)
            return null;

        var kin = clan.HomeGroup != null
            ? playerGroup != null && playerGroup == clan.HomeGroup
            : IsFamily(clan.Family) && clan.Family == playerFamily;
        if (kin)
            return Kin;

        return playerSide.HasValue && playerSide.Value == clan.Side ? Side : Foe;
    }

    /// <summary>
    /// FNV-1a over the UTF-16 code units: the same number for the same id in every process, which
    /// <see cref="string.GetHashCode()"/> does not promise.
    /// </summary>
    internal static uint StableHash(string? value)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var c in value ?? string.Empty)
            {
                hash ^= c;
                hash *= 16777619u;
            }
            return hash;
        }
    }

    /// <summary>
    /// Which of a lord's two surrender demands applies today: true picks the <c>_2</c> row. It alternates from one day
    /// to the next and stays the same for one lord through one day.
    /// </summary>
    internal static bool IsAltLine(string? heroId, int dayOfYear) =>
        unchecked(StableHash(heroId) + (uint)dayOfYear) % 2u == 1u;

    private static bool IsKin(string? speakerFamily, string? playerFamily, string? speakerGroup, string? playerGroup)
    {
        if (!IsFamily(speakerFamily) || speakerFamily != playerFamily)
            return false;
        if (speakerFamily != EncounterGroupMap.Man)
            return true;
        return speakerGroup != null && speakerGroup == playerGroup;
    }

    private static bool IsFamily(string? family) => family != null && family != EncounterGroupMap.NoFamily;
}
