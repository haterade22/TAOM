namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// The arrangements a faction's lane tactic can name. The engine layer gives each one as the engine's arrangement order of the
/// same kind; Skein is the wedge.
/// </summary>
internal enum LaneArrangement
{
    Line,
    Loose,
    ShieldWall,
    Square,
    Column,
    Skein,
}
