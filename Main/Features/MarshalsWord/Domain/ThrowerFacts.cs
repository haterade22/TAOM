namespace TAOM.Features.MarshalsWord.Domain;

/// <summary>A copy of one run's facts, for the lines and the field trial.</summary>
internal readonly record struct ThrowerFacts(
    int Company, int Serial, ThrowerState State, ThrowerReleaseKind Kind, string Why, int Units, int Armed0, int Ammo0, float Range, float StandGap,
    float Gap0, float GapAtStand, float MinGap, float MaxOvershoot, double StoodSeconds, int Shots, int RunShots, int ArmedAtRelease, double ReleasedAt,
    bool EverStood, bool ClosedBeforeStand, string Melee, int Blows, double MeleeSeconds, float Advanced, int AmmoSpent);
