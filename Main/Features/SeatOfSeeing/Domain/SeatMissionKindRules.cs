namespace TAOM.Features.SeatOfSeeing.Domain;

/// <summary>
/// The presence tests a live mission answers, read by the client's classifier and decided here. Never a mission name string,
/// because names rot. <paramref name="Combat"/> is <c>Mission.CombatType == Combat</c>, <paramref name="TeamBattle"/> a field
/// battle, siege or sally-out team AI, and <paramref name="BattleMode"/> a Battle or Deployment mode.
/// </summary>
internal readonly record struct SeatMissionProbe(
    bool Hideout, bool Tournament, bool Arena, bool CampaignLocation, bool Combat, bool TeamBattle, bool BattleMode);

/// <summary>Decides a mission's <see cref="SeatMissionKind"/> from its probe; the order matters, and <c>Mission.Mode</c> alone proves nothing because it reads StartUp in a town.</summary>
internal static class SeatMissionKindRules
{
    internal static SeatMissionKind Classify(in SeatMissionProbe probe)
    {
        if (probe.Hideout)
            return SeatMissionKind.Hideout;
        if (probe.Tournament)
            return SeatMissionKind.Tournament;
        if (probe.Arena)
            return SeatMissionKind.Arena;
        if (probe.CampaignLocation)
            return SeatMissionKind.Settlement;
        return probe.Combat && (probe.TeamBattle || probe.BattleMode) ? SeatMissionKind.Battle : SeatMissionKind.Other;
    }
}
