namespace TAOM.Features.Enlistment.Equipment;

/// <summary>
/// Single owner of the enlistment roster id convention:
/// <c>enlist_{runtimeCultureId}_{rank}</c> (culture rosters) and
/// <c>enlist_default_{rank}</c> (culture-neutral fallbacks). Culture ids are
/// RUNTIME StringIds — vlandia (Rohan), empire (Dunland), aserai (Harad),
/// khuzait (Rhûn), sturgia (Dale), battania (Khand) — never lore names.
/// Mirrors CareerEquipmentRosterIds (CharacterCreation).
/// </summary>
public static class EnlistmentRosterIds
{
    public static string Build(string cultureId, EnlistmentRank rank)
        => $"enlist_{cultureId}_{RankToken(rank)}";

    public static string BuildDefault(EnlistmentRank rank)
        => $"enlist_default_{RankToken(rank)}";

    public static string RankToken(EnlistmentRank rank) => rank switch
    {
        EnlistmentRank.Recruit  => "recruit",
        EnlistmentRank.Soldier  => "soldier",
        EnlistmentRank.Veteran  => "veteran",
        EnlistmentRank.Sergeant => "sergeant",
        _                       => "recruit",
    };
}
