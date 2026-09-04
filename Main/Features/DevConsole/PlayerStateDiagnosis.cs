using System.Collections.Generic;

namespace TAOM.Features.DevConsole;

/// <summary>
/// The player's identity links at one instant, flattened to ids so the reading can be tested
/// without a campaign.
/// </summary>
internal sealed class PlayerStateSnapshot
{
    internal string MainHeroId { get; set; }
    internal string MainHeroName { get; set; }
    internal bool MainHeroIsAlive { get; set; }
    internal string MainHeroClanId { get; set; }

    /// <summary>The party the hero reports belonging to. Legitimately differs from the main party
    /// in several states, so this is reported rather than asserted. See the Build remarks.</summary>
    internal string MainHeroPartyId { get; set; }

    internal string PlayerClanId { get; set; }
    internal string PlayerClanLeaderId { get; set; }

    internal string MainPartyId { get; set; }
    internal string MainPartyLeaderHeroId { get; set; }
    internal bool MainPartyIsActive { get; set; }

    /// <summary>
    /// `Game.Current.PlayerTroop`. Printed for reference, never compared against the hero:
    /// `Hero.MainHero` is DERIVED from it (`CharacterObject.PlayerCharacter.HeroObject`), so the two
    /// cannot disagree and a check between them would be a tautology dressed up as a safety net.
    /// </summary>
    internal string PlayerTroopId { get; set; }

    /// <summary>Set when reading the engine threw, so the report can say so instead of inventing a verdict.</summary>
    internal string CaptureError { get; set; }
}

/// <summary>
/// Checks that the player's identity links agree with each other.
///
/// This exists because TAOM's Player Switcher repoints the player at an existing lord and deletes
/// the throwaway character-creation hero and clan. The engine holds the player's identity in several
/// places updated by different code, and a handover that misses one leaves a link pointing at a
/// deleted object: nothing throws where the TAOM log can see it, values still render, but anything
/// resolving the player silently fails.
///
/// Every mismatch is reported, never just the first, because a half-finished handover breaks several
/// at once and the first alone is misleading.
///
/// Two things are deliberately NOT asserted, because asserting them produced false alarms:
/// the hero-vs-PlayerTroop link (a tautology, see the snapshot field), and hero-party-vs-main-party,
/// which legitimately diverges when the player is enlisted (a TAOM feature), a prisoner, or
/// travelling. Those are reported as observations so a reader can judge them in context.
/// </summary>
internal static class PlayerStateDiagnosis
{
    internal static IReadOnlyList<string> Build(PlayerStateSnapshot snapshot)
    {
        var lines = new List<string>();
        if (snapshot == null)
        {
            lines.Add("MISMATCH: no snapshot at all.");
            return lines;
        }

        if (!string.IsNullOrEmpty(snapshot.CaptureError))
        {
            lines.Add($"MISMATCH (capture): reading the player's state threw: {snapshot.CaptureError}. "
                    + "That is itself a finding, and Hero.MainHero is the usual culprit: it is a computed "
                    + "getter (CharacterObject.PlayerCharacter.HeroObject) that dereferences unguarded.");
            return lines;
        }

        lines.Add($"MainHero    : {Show(snapshot.MainHeroId)} ({Show(snapshot.MainHeroName)}), alive={snapshot.MainHeroIsAlive}");
        lines.Add($"PlayerClan  : {Show(snapshot.PlayerClanId)}, leader={Show(snapshot.PlayerClanLeaderId)} "
                + $"(hero's clan: {Show(snapshot.MainHeroClanId)})");
        lines.Add($"MainParty   : {Show(snapshot.MainPartyId)}, leader={Show(snapshot.MainPartyLeaderHeroId)}, "
                + $"active={snapshot.MainPartyIsActive} (hero's party: {Show(snapshot.MainHeroPartyId)})");
        lines.Add($"PlayerTroop : {Show(snapshot.PlayerTroopId)} (reference only, derived from the same source as MainHero)");

        var mismatches = new List<string>();

        if (string.IsNullOrEmpty(snapshot.MainHeroId))
            mismatches.Add("MISMATCH: there is no Hero.MainHero. Nothing that resolves the player can work.");

        if (!snapshot.MainHeroIsAlive)
            mismatches.Add("MISMATCH: Hero.MainHero is not alive.");

        Compare(mismatches, snapshot.MainHeroClanId, snapshot.PlayerClanId,
            "clan", "Hero.MainHero's clan is not Clan.PlayerClan. The throwaway creation clan most "
            + "likely survived as the player clan.");

        Compare(mismatches, snapshot.MainPartyLeaderHeroId, snapshot.MainHeroId,
            "party leader", "MobileParty.MainParty is led by someone other than the player.");

        if (!snapshot.MainPartyIsActive && !string.IsNullOrEmpty(snapshot.MainPartyId))
            mismatches.Add("MISMATCH (party): MobileParty.MainParty is not active.");

        // Reported, not asserted. Being a non-leader member of your own clan is what a Player
        // Switcher takeover of a lord who is not his house's head produces, and it is EXPECTED there.
        // Do not "repair" it by promoting the player: on a ruling clan that also rewrites
        // Kingdom.Leader, because Kingdom.Leader is a projection of RulingClan.Leader, so the player
        // silently becomes monarch. Verified against Kingdom.cs:168 on v1.4.8.
        if (!string.IsNullOrEmpty(snapshot.PlayerClanLeaderId)
            && !string.IsNullOrEmpty(snapshot.MainHeroId)
            && snapshot.PlayerClanLeaderId != snapshot.MainHeroId)
        {
            lines.Add($"NOTE (clan leader): Clan.PlayerClan is led by '{snapshot.PlayerClanLeaderId}', not by "
                    + $"the player '{snapshot.MainHeroId}'. Expected after taking over a lord who is not his "
                    + "clan's head. Vanilla has no path to it, so engine code may assume otherwise. Promoting "
                    + "the player is NOT a safe repair: on a kingdom's ruling clan it also transfers "
                    + "rulership, since Kingdom.Leader is a projection of RulingClan.Leader.");
        }

        if (!string.IsNullOrEmpty(snapshot.MainHeroPartyId)
            && !string.IsNullOrEmpty(snapshot.MainPartyId)
            && snapshot.MainHeroPartyId != snapshot.MainPartyId)
        {
            lines.Add($"NOTE (party): the hero belongs to '{snapshot.MainHeroPartyId}' while "
                    + $"MobileParty.MainParty is '{snapshot.MainPartyId}'. Legitimate while enlisted, "
                    + "imprisoned or travelling; a finding only if none of those apply.");
        }

        if (mismatches.Count == 0)
        {
            lines.Add("PLAYER STATE OK: no contradiction found among the asserted links.");
            return lines;
        }

        lines.AddRange(mismatches);
        return lines;
    }

    private static void Compare(ICollection<string> mismatches, string actual, string expected, string label, string explanation)
    {
        // Both empty is already covered by the MainHero check; comparing them here would double-report.
        if (string.IsNullOrEmpty(actual) && string.IsNullOrEmpty(expected)) return;

        if (actual != expected)
            mismatches.Add($"MISMATCH ({label}): {Show(actual)} vs {Show(expected)}. {explanation}");
    }

    private static string Show(string value) => string.IsNullOrEmpty(value) ? "(null)" : value;
}
