using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CultureDoctrine.Hooks.Tactics;

/// <summary>The formations a plan's roles resolve to after one <c>ManageFormationCounts</c>.
/// Null means the team has no formation for that role this apply.</summary>
public struct SlotAssignment
{
    public Formation? MainInfantry;
    public Formation? SecondInfantry;
    public Formation? LeftWing;
    public Formation? RightWing;
    public Formation? Archers;
    public Formation? LeftCavalry;
    public Formation? RightCavalry;
    public Formation? Cavalry;
    public Formation? RangedCavalry;
    public Formation? Vanguard;
}

/// <summary>
/// Picks formations for a plan's roles the way <c>TacticComponent.AssignTacticFormations1121</c>
/// does (`TacticComponent.cs:475-506`): per class, AI-controlled formations first, then by
/// formation power, the strongest taking the first slot. The 2/1/2/1 and 3/1/2/1 splits hand
/// the second and third infantry blocks to the second line or the wings; the vanguard split
/// takes the team's <c>HeavyCavalry</c> formation, which the tactic keeps out of the cavalry
/// consolidation so the routed troops stay a block of their own. Sides are written the way
/// vanilla writes them: the main infantry Middle, the cavalry Left and Right, and the wings
/// Left and Right so <c>BehaviorEnvelopWing</c> knows which flank it owns.
/// </summary>
public static class FormationSlots
{
    public static SlotAssignment Assign(MBList<Formation> formations, int infantrySlots, bool singleCavalry, Formation? vanguard)
    {
        var slots = default(SlotAssignment);
        var infantry = Sorted(formations, f => f.QuerySystem.IsInfantryFormation, vanguard);
        slots.MainInfantry = infantry.Count > 0 ? infantry[0] : null;
        if (slots.MainInfantry != null)
        {
            slots.MainInfantry.AI.IsMainFormation = true;
            slots.MainInfantry.AI.Side = FormationAI.BehaviorSide.Middle;
        }
        if (infantrySlots == 2)
        {
            slots.SecondInfantry = infantry.Count > 1 ? infantry[1] : null;
        }
        else if (infantrySlots == 3)
        {
            slots.LeftWing = infantry.Count > 1 ? infantry[1] : null;
            slots.RightWing = infantry.Count > 2 ? infantry[2] : null;
            if (slots.LeftWing != null)
                slots.LeftWing.AI.Side = FormationAI.BehaviorSide.Left;
            if (slots.RightWing != null)
                slots.RightWing.AI.Side = FormationAI.BehaviorSide.Right;
        }

        var archers = Sorted(formations, f => f.QuerySystem.IsRangedFormation, vanguard);
        slots.Archers = archers.Count > 0 ? archers[0] : null;

        var cavalry = Sorted(formations, f => f.QuerySystem.IsCavalryFormation, vanguard);
        if (singleCavalry)
        {
            slots.Cavalry = cavalry.Count > 0 ? cavalry[0] : null;
        }
        else if (cavalry.Count > 0)
        {
            slots.LeftCavalry = cavalry[0];
            slots.LeftCavalry.AI.Side = FormationAI.BehaviorSide.Left;
            if (cavalry.Count > 1)
            {
                slots.RightCavalry = cavalry[1];
                slots.RightCavalry.AI.Side = FormationAI.BehaviorSide.Right;
            }
        }

        var horseArchers = Sorted(formations, f => f.QuerySystem.IsRangedCavalryFormation, vanguard);
        slots.RangedCavalry = horseArchers.Count > 0 ? horseArchers[0] : null;
        slots.Vanguard = vanguard;
        return slots;
    }

    /// <summary>The team's HeavyCavalry formation when it holds troops, else null.</summary>
    public static Formation? VanguardOf(MBList<Formation> formations)
    {
        for (var i = 0; i < formations.Count; i++)
        {
            var f = formations[i];
            if (f.FormationIndex == FormationClass.HeavyCavalry && f.CountOfUnits > 0)
                return f;
        }
        return null;
    }

    /// <summary>Vanilla's slot test: the formation is gone, empty or no longer of its class.</summary>
    public static bool Holds(Formation? formation, Func<FormationQuerySystem, bool> isClass) =>
        formation == null || (formation.CountOfUnits != 0 && isClass(formation.QuerySystem));

    // ChooseAndSortByPriority without LINQ: AI-controlled first, then by power, stable.
    private static List<Formation> Sorted(MBList<Formation> formations, Func<Formation, bool> isClass, Formation? excluded)
    {
        var picked = new List<Formation>(4);
        for (var i = 0; i < formations.Count; i++)
        {
            var f = formations[i];
            if (f != excluded && f.CountOfUnits > 0 && isClass(f))
                picked.Add(f);
        }
        picked.Sort(ByPriority);
        return picked;
    }

    private static int ByPriority(Formation a, Formation b)
    {
        if (a.IsAIControlled != b.IsAIControlled)
            return a.IsAIControlled ? -1 : 1;
        return b.QuerySystem.FormationPower.CompareTo(a.QuerySystem.FormationPower);
    }
}
