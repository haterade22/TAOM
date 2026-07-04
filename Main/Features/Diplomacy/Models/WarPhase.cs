namespace TAOM.Features.Diplomacy.Models;

public enum WarPhase
{
    Peace,
    IsengardWar,
    FullWar,
    // WotR Momentum #327 — terminal state set by EndWar when one side wins.
    // Appended (value 3) so persisted ints from older saves stay valid.
    WarEnded
}
