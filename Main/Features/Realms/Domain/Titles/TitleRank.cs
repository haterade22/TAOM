namespace TAOM.Features.Realms.Domain.Titles;

/// <summary>
/// The tiers of the title hierarchy, lowest first. A title's de jure parent must have a strictly higher rank.
/// Lordship = village, Barony = castle, County = town, Duchy = a group of counties, Kingdom = a realm,
/// Empire = a high title over several kingdoms (for example Arnor over its successor realms),
/// Union = a title over several realms and high titles, formed by holding them all (the Reunited Kingdom).
/// </summary>
public enum TitleRank
{
    Lordship = 0,
    Barony = 1,
    County = 2,
    Duchy = 3,
    Kingdom = 4,
    Empire = 5,
    Union = 6,
}
