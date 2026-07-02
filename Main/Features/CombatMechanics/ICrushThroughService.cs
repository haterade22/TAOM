using TAOM.Features.CombatMechanics.Domain;

namespace TAOM.Features.CombatMechanics;

public interface ICrushThroughService
{
    // Decision order: monster auto-CTB (non-shield block) → orc shield-CTB → skill-based extra CTB.
    // true = crush through; null = no opinion, fall through to the engine's base 58f path.
    // (Never returns false — TAOM only ADDS crush-throughs, it never vetoes a vanilla one.)
    bool? DecideCrushThrough(in CrushThroughContext context);
}
