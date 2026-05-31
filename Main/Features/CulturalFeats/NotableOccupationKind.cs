namespace TAOM.Features.CulturalFeats;

/// <summary>
/// TAOM-owned classification of notable occupations used by the per-occupation
/// notable-count feats. The <see cref="Models.TaomNotableSpawnModel"/> maps the
/// sealed TaleWorlds <c>Occupation</c> enum to this kind at the boundary so the
/// service stays free of engine types (ADR-007). Only the occupations vanilla's
/// notable spawn pool uses are represented — every other <c>Occupation</c>
/// value maps to <see cref="Other"/> and the service returns the base count
/// unchanged.
/// </summary>
public enum NotableOccupationKind
{
    Other = 0,

    // Town occupations (vanilla DefaultNotableSpawnModel target counts: 2 / 2 / 1)
    Merchant,
    GangLeader,
    Artisan,

    // Village occupations (vanilla target: 2 / 1)
    RuralNotable,
    Headman,
}
