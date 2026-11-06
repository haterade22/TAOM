using System;
using TAOM.Core.Validation;

namespace TAOM.Core.Combat;

/// <summary>
/// Whether a raised shield covers a scripted blow: only when its bearer faces the attacker, so a shield held toward one
/// enemy does not cover a blow from behind. One rule for every TAOM blow that cuts its damage on a shield block and
/// skips the knockdown (a half for the dwarf attacks and the troll's Brute Force ring, a quarter for the elephant-like
/// attacks and, by default, the signature strike rings). The boundary checks that a shield is raised and reads the
/// engine (<c>CustomAttacksUtils.ShieldBlocks</c>); this is the geometry. A degenerate or non-finite direction cannot
/// prove cover, so it is no block.
/// </summary>
public static class ShieldCover
{
    /// <summary>The cover is the bearer's front half: the cosine between his look and the attacker must exceed this.</summary>
    public const float FacingDot = 0f;

    /// <summary>A direction shorter than this, in metres or unit-vector length, proves nothing.</summary>
    public const float MinLength = 1e-4f;

    public static bool Faces(float victimLookX, float victimLookY, float toAttackerX, float toAttackerY)
    {
        float lookLength = (float)Math.Sqrt(victimLookX * victimLookX + victimLookY * victimLookY);
        float toLength = (float)Math.Sqrt(toAttackerX * toAttackerX + toAttackerY * toAttackerY);
        if (!(lookLength > MinLength) || !(toLength > MinLength)) return false;
        float dot = (victimLookX * toAttackerX + victimLookY * toAttackerY) / (lookLength * toLength);
        return FiniteFloatValidator.IsFinite(dot) && dot > FacingDot;
    }
}
