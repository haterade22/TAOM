using TAOM.Features.Execution;

namespace TAOM.Features.SwornAnew.Models;

/// <summary>
/// The raw facts one <c>DefectionModel.CanHeroDefectToFaction(hero, kingdom)</c> question rests on (docs/features/sworn-anew.md).
/// Every value is read, never decided, by the adapter; <c>SwornAnewService</c> decides. The nine facts after the prisoner
/// flag are the terms of vanilla's own check (<c>DefaultDefectionModel</c> line 9), so the service can repeat it for a prisoner.
/// </summary>
/// <param name="HeroId">The hero's StringId, the key the lore registry is asked about.</param>
/// <param name="RaceId">The hero's FaceGen race id, null when it cannot be read (0 is a real race, human).</param>
/// <param name="IsPrisoner">The hero is a prisoner now. A free hero is never this feature's business.</param>
/// <param name="InKingdom">The hero's map faction is a kingdom.</param>
/// <param name="InPlayerFaction">The hero's map faction is the player's own.</param>
/// <param name="IsFactionLeader">The hero leads their map faction.</param>
/// <param name="HasClan">The hero belongs to a clan.</param>
/// <param name="IsMinorFaction">The hero's clan is a minor faction.</param>
/// <param name="IsMercenary">The hero's clan is under mercenary service.</param>
/// <param name="ClanHasKingdom">The hero's clan belongs to a kingdom.</param>
/// <param name="TargetRuledByPlayerClan">The kingdom the hero would join is ruled by the player's clan.</param>
/// <param name="HasBloodFeudWithPlayer">The hero's clan has a blood feud with the player.</param>
/// <param name="HeldByMainParty">The hero is a prisoner of the player's own party.</param>
/// <param name="InPlayerClanSettlement">The hero is in a settlement the player's clan owns (vanilla's dungeon test: <c>CurrentSettlement.OwnerClan</c>).</param>
/// <param name="OnRansomScreen">The open party screen is a ransom screen. It lists a cloned prison roster, so a lord who just joined could still be ransomed from it.</param>
/// <param name="LordSide">The hero's side pair by their own clan, never their realm, built by <c>FactionSides.OfOwnClan</c> (#1114).</param>
/// <param name="PlayerSide">The player's side pair, built by <c>FactionSides.OfPlayer</c>.</param>
public sealed record DefectionFacts(
    string HeroId,
    int? RaceId,
    bool IsPrisoner,
    bool InKingdom,
    bool InPlayerFaction,
    bool IsFactionLeader,
    bool HasClan,
    bool IsMinorFaction,
    bool IsMercenary,
    bool ClanHasKingdom,
    bool TargetRuledByPlayerClan,
    bool HasBloodFeudWithPlayer,
    bool HeldByMainParty,
    bool InPlayerClanSettlement,
    bool OnRansomScreen,
    SideIds LordSide,
    SideIds PlayerSide);
