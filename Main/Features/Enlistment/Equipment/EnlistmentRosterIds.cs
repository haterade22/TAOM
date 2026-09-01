using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Equipment;

/// <summary>
/// Single owner of the enlistment roster id convention:
/// <c>enlist_{runtimeCultureId}_{assignment}_{rank}</c> (culture rosters) and
/// <c>enlist_default_{assignment}_{rank}</c> (culture-neutral fallbacks). Culture ids are
/// RUNTIME StringIds — vlandia (Rohan), empire (Dunland), aserai (Harad), khuzait (Rhûn),
/// sturgia (Dale), battania (Khand) — never lore names.
/// Mirrors CareerEquipmentRosterIds (CharacterCreation).
///
/// The assignment token entered the id in #525, when the kit stopped being armour-only: a
/// weapon is only right if it matches the role the player chose, so the role has to be part of
/// the key. <see cref="ServiceAssignment"/> is used directly rather than mirrored into this
/// namespace — PersistedEquipmentIssueLedger already imports Content, so the boundary is
/// crossed, and a second enum would need a parity test asserting the copy stays faithful, which
/// is the shape lessons/testing-qa.md warns against.
/// </summary>
public static class EnlistmentRosterIds
{
    public static string Build(string cultureId, ServiceAssignment assignment, EnlistmentRank rank)
        => $"enlist_{cultureId}_{AssignmentToken(assignment)}_{RankToken(rank)}";

    public static string BuildDefault(ServiceAssignment assignment, EnlistmentRank rank)
        => $"enlist_default_{AssignmentToken(assignment)}_{RankToken(rank)}";

    public static string RankToken(EnlistmentRank rank) => rank switch
    {
        EnlistmentRank.Recruit  => "recruit",
        EnlistmentRank.Soldier  => "soldier",
        EnlistmentRank.Veteran  => "veteran",
        EnlistmentRank.Sergeant => "sergeant",
        _                       => "recruit",
    };

    /// <summary>
    /// Assignment → id token. The default arm resolves to <c>infantry</c> deliberately: it is
    /// the one assignment every culture authors, so an ordinal outside the enum (a corrupt save,
    /// or a value added later) lands on a kit that exists rather than on nothing. That same
    /// defaulting is how a NEW assignment could silently wear infantry's kit and look covered,
    /// which is why EveryAssignment_MapsToItsOwnToken pins the mapping.
    /// </summary>
    public static string AssignmentToken(ServiceAssignment assignment) => assignment switch
    {
        ServiceAssignment.Infantry => "infantry",
        ServiceAssignment.Archer   => "archer",
        ServiceAssignment.Cavalry  => "cavalry",
        ServiceAssignment.Support  => "support",
        _                          => "infantry",
    };
}
