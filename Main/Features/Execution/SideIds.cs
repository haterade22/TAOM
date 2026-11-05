namespace TAOM.Features.Execution;

/// <summary>
/// The id pair <see cref="IAlignmentService.ResolveSide(string, string)"/> needs: the id and the
/// culture of the SAME entity (a faction with its own culture, or a clanless hero with the hero's own).
/// Built from engine objects in exactly one place, <c>TAOM.Adapters.FactionSides</c>; services carry the
/// pair around whole so a kingdom can never be zipped with another entity's culture (#957).
/// </summary>
/// <param name="FactionId">StringId of the entity's map faction (a kingdom or a clan), empty when it has none.</param>
/// <param name="CultureId">StringId of the culture of that same entity, empty when it has none.</param>
public sealed record SideIds(string FactionId, string CultureId)
{
    public static readonly SideIds None = new(string.Empty, string.Empty);
}
