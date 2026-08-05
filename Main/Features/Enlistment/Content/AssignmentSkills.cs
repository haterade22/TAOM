using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Content;

/// <summary>Signature skill per assignment (engine StringIds) — the daily-XP routing target.</summary>
public static class AssignmentSkills
{
    public static string SignatureSkill(ServiceAssignment assignment)
    {
        switch (assignment)
        {
            case ServiceAssignment.Archer: return "Bow";
            case ServiceAssignment.Cavalry: return "Riding";
            case ServiceAssignment.Support: return "Steward";
            default: return "Athletics";
        }
    }

    /// <summary>Context XP skill: siege trains Engineering, naval Tactics, blockade Scouting, army Leadership.</summary>
    public static string ContextSkill(ArmyRhythmSnapshot rhythm)
    {
        if (rhythm == null)
            return null;
        if (rhythm.SiegePressure) return "Engineering";
        if (rhythm.Naval) return "Tactics";
        if (rhythm.Blockade) return "Scouting";
        if (rhythm.InArmy) return "Leadership";
        return null;
    }

    public static int ContextXp(ArmyRhythmSnapshot rhythm, ProgressionTables tables)
    {
        if (rhythm == null || tables == null)
            return 0;
        if (rhythm.SiegePressure) return tables.SiegeContextXp;
        if (rhythm.Naval) return tables.NavalContextXp;
        if (rhythm.Blockade) return tables.BlockadeContextXp;
        if (rhythm.InArmy) return tables.ArmyContextXp;
        return 0;
    }
}
