using System.Text;
using SandBox.ViewModelCollection.Tournament;

namespace TAOM.Features.Arena;

/// <summary>
/// Renders a <c>TournamentVM</c>'s whole bracket into one log line, for the forensic dump
/// <c>Patch69_TournamentEndGuard</c> writes when <c>OnTournamentEnd</c> throws.
///
/// Split out of the patch so the Harmony class stays a thin entry point (ADR-002) — the formatter
/// dominated it. Sealed engine view models are legitimate here because this is a boundary
/// formatter, the same role <see cref="TAOM.Features.BattleLoadDiagnostics.SpawnOriginFormatter"/>
/// plays for its patch.
///
/// What it is looking for: a participant slot with <c>IsValid == true</c> and a null
/// <c>Participant</c>. <c>TournamentParticipantVM.Refresh(null, …)</c> nulls <c>Participant</c>
/// without ever resetting <c>IsValid</c>, and <c>GetParticipants()</c> filters on <c>IsValid</c>, so
/// that combination is a live NRE source in <c>OnTournamentEnd</c> that we could not reproduce from
/// a full bracket. If it ever happens, this dump names the exact slot.
/// </summary>
public static class TournamentBracketFormatter
{
    public static string Dump(TournamentVM vm)
    {
        if (vm == null) return "bracket unavailable (__instance was null)";

        var sb = new StringBuilder();
        sb.Append("bracket dump:");
        AppendRound(sb, "Round1", vm.Round1);
        AppendRound(sb, "Round2", vm.Round2);
        AppendRound(sb, "Round3", vm.Round3);
        AppendRound(sb, "Round4", vm.Round4);
        return sb.ToString();
    }

    private static void AppendRound(StringBuilder sb, string label, TournamentRoundVM round)
    {
        if (round == null)
        {
            sb.Append($" | {label}=<null>");
            return;
        }

        sb.Append($" | {label} valid={round.IsValid} matches={round.Matches?.Count ?? 0}");
        if (round.Matches == null) return;

        for (var m = 0; m < round.Matches.Count; m++)
        {
            var match = round.Matches[m];
            if (match == null || !match.IsValid) continue;      // uninitialised slots are expected

            sb.Append($" [m{m}");
            var teams = match.Teams;
            if (teams != null)
            {
                for (var t = 0; t < teams.Count; t++)
                {
                    var team = teams[t];
                    if (team == null || !team.IsValid) continue;

                    var participants = team.Participants;
                    if (participants == null) continue;

                    // Walk Count, not GetParticipants() — the latter filters on IsValid, which is
                    // exactly the flag we suspect of lying. A raw index walk is the only way to see
                    // a stale-valid slot.
                    for (var p = 0; p < team.Count && p < participants.Count; p++)
                        AppendParticipant(sb, t, p, participants[p]);
                }
            }
            sb.Append(']');
        }
    }

    private static void AppendParticipant(StringBuilder sb, int teamIndex, int slotIndex, TournamentParticipantVM slot)
    {
        if (slot == null)
        {
            sb.Append($" t{teamIndex}p{slotIndex}=<null VM>");
            return;
        }

        var participant = slot.Participant;
        if (participant == null)
        {
            // IsValid true here IS the stale-valid defect; IsValid false is an ordinary empty slot.
            sb.Append($" t{teamIndex}p{slotIndex}=EMPTY(valid={slot.IsValid})");
            return;
        }

        var character = participant.Character;
        if (character == null)
        {
            sb.Append($" t{teamIndex}p{slotIndex}=<null character>(valid={slot.IsValid})");
            return;
        }

        var hero = character.IsHero ? character.HeroObject : null;
        var kind = character.IsHero ? "hero" : "troop";
        var sex = character.IsFemale ? "F" : "M";
        var clan = hero?.Clan?.StringId ?? "-";
        var mapFaction = character.IsHero ? (hero?.MapFaction != null ? "ok" : "NULL") : "n/a";
        var culture = character.Culture != null ? "ok" : "NULL";

        sb.Append($" t{teamIndex}p{slotIndex}={character.StringId}({kind},{sex},race={character.Race}," +
                  $"clan={clan},mapFaction={mapFaction},culture={culture})");
    }
}
