using TaleWorlds.MountAndBlade;
using TAOM.Features.MarshalsWord;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Adapters;

/// <summary>Reads the agents of the view's hit and shot callbacks and counts a player-team blow or shot for the company's table, from any thread, with no lock, no allocation and no log line.</summary>
internal static class MarshalCombatReader
{
    internal static void OnBlow(Agent? affector, Agent? affected, bool isMissile)
    {
        var mission = Mission.Current;
        if (!MarshalFireTable.IsLive || isMissile || affector == null || affected == null || mission == null || affector.Team == affected.Team)
            return;
        // Formation.Index repeats across teams, so the team is tested before the index is used.
        var formation = affector.Formation;
        if (formation == null || affector.Team != mission.PlayerTeam)
            return;
        var component = affector.GetComponent<MarshalWordComponent>();
        var inTheLine = component != null && component.HoldQualifies;
        var woundedAt = component?.WoundedAt ?? double.NegativeInfinity;
        if (MarshalHoldLineRules.BlowIsStrikeBack(inTheLine, woundedAt, mission.CurrentTime))
            MarshalFireTable.CountStrikeBlow(formation.Index);
        else if (MarshalHoldLineRules.BlowCountsForLine(inTheLine, MarshalFireTable.HoldOf(formation.Index)))
            MarshalFireTable.CountBlow(formation.Index);
    }

    internal static void OnShot(Agent shooter)
    {
        if (!MarshalFireTable.IsLive)
            return;
        // Formation.Index repeats across teams, so the team is tested before the index is used.
        var formation = shooter.Formation;
        if (formation == null || shooter.Team != Mission.Current?.PlayerTeam)
            return;
        var company = formation.Index;
        MarshalFireTable.NoteShooter(company, shooter);
        MarshalFireTable.CountAnyShot(company);
        if (MarshalFireTable.ModeOf(company) == FireMode.Free)
            return;
        var component = shooter.GetComponent<MarshalWordComponent>();
        MarshalFireTable.Count(company, component?.Classify(Mission.Current!.CurrentTime) ?? ShotKind.Free);
    }
}
