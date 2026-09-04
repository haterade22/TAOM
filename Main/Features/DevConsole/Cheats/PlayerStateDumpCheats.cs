using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace TAOM.Features.DevConsole.Cheats;

/// <summary>
/// Checks that the player's identity is internally consistent, for the class of report that follows
/// a Player Switcher takeover: the campaign plays normally, values render, nothing reaches the log,
/// but anything that resolves the player misbehaves.
///
/// The engine keeps "who is the player" in four places updated by different code. This asks all four
/// and compares them, which is the difference between "something is wrong" and a named broken link.
/// </summary>
public static class PlayerStateDumpCheats
{
    private const string Usage =
        "Format is \"taom.print_player_state\".\n"
        + "Prints Hero.MainHero, Clan.PlayerClan, MobileParty.MainParty and the engine's\n"
        + "PlayerTroop, then reports every link between them that disagrees. Run it after a\n"
        + "Player Switcher takeover, or any time the player is on screen but behaving oddly.";

    [CommandLineFunctionality.CommandLineArgumentFunction("print_player_state", "taom")]
    public static string PrintPlayerState(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, Usage, args =>
        {
            var lines = PlayerStateDiagnosis.Build(Capture());
            return string.Join("\n", lines);
        });

    /// <summary>
    /// Wrapped, not merely null-conditional. `Hero.MainHero` is a computed getter
    /// (`CharacterObject.PlayerCharacter.HeroObject`, Hero.cs:891) that dereferences unguarded, so it
    /// THROWS before any `?.` of ours can apply, and it throws precisely in the damaged state this
    /// command exists to describe. Guarding the result instead of the read is the trap named in
    /// `.claude/rules/adapters.md`. A capture failure is recorded and reported as a finding rather
    /// than being allowed to replace the whole report with a stack trace.
    /// </summary>
    private static PlayerStateSnapshot Capture()
    {
        var snapshot = new PlayerStateSnapshot();

        try
        {
            var hero = Hero.MainHero;
            snapshot.MainHeroId = hero?.StringId;
            snapshot.MainHeroName = hero?.Name?.ToString();
            snapshot.MainHeroIsAlive = hero?.IsAlive ?? false;
            snapshot.MainHeroClanId = hero?.Clan?.StringId;
            snapshot.MainHeroPartyId = hero?.PartyBelongedTo?.StringId;
        }
        catch (Exception ex)
        {
            snapshot.CaptureError = $"{ex.GetType().Name} reading Hero.MainHero: {ex.Message}";
            return snapshot;
        }

        // Each of the remaining groups is independent: one throwing must not cost us the others.
        try
        {
            var playerClan = Clan.PlayerClan;
            snapshot.PlayerClanId = playerClan?.StringId;
            snapshot.PlayerClanLeaderId = playerClan?.Leader?.StringId;
        }
        catch (Exception ex)
        {
            snapshot.CaptureError = $"{ex.GetType().Name} reading Clan.PlayerClan: {ex.Message}";
            return snapshot;
        }

        try
        {
            var mainParty = MobileParty.MainParty;
            snapshot.MainPartyId = mainParty?.StringId;
            snapshot.MainPartyLeaderHeroId = mainParty?.LeaderHero?.StringId;
            snapshot.MainPartyIsActive = mainParty?.IsActive ?? false;
        }
        catch (Exception ex)
        {
            snapshot.CaptureError = $"{ex.GetType().Name} reading MobileParty.MainParty: {ex.Message}";
            return snapshot;
        }

        try
        {
            snapshot.PlayerTroopId = Game.Current?.PlayerTroop?.StringId;
        }
        catch (Exception ex)
        {
            snapshot.CaptureError = $"{ex.GetType().Name} reading Game.Current.PlayerTroop: {ex.Message}";
        }

        return snapshot;
    }
}
