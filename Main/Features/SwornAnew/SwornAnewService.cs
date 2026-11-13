using TAOM.Features.CoopInterop;
using TAOM.Features.Execution;
using TAOM.Features.SwornAnew.Models;
using TAOM.Features.UncapturableHeroes;

namespace TAOM.Features.SwornAnew;

/// <summary>
/// Sworn Anew (docs/features/sworn-anew.md, #869): whether a captured lord can be asked to join the player's kingdom. Pure over the
/// raw facts the adapter reads; every rule is decided here, so the three entry points (the <c>DefectionModel</c> override, the
/// postfix on vanilla's prisoner line and a campaign behavior) stay thin.
/// <para>
/// A free hero is answered with null, so the model falls through to vanilla (<c>DefaultDefectionModel</c>), which never lets a
/// prisoner defect. For a prisoner the verdict repeats vanilla's own faction checks (including the blood feud, which vanilla
/// applies only to a kingdom the player's clan rules), then requires custody (held by the player's party, or in a settlement the
/// player's clan owns, as vanilla's prisoner line does), then lore (<see cref="IUncapturableRegistry"/>: Sauron and the Nine by
/// identity, whatever that feature's own switch says), then alignment: a Free lord is never asked to join an Evil realm, nor an
/// Evil lord a Free one, and a Neutral side on either end is allowed. A lord's side is their own clan's, whatever realm they serve. That last rule is <see cref="FactionSideRules.AreOpposed"/>,
/// not <see cref="IAlignmentService.AreEnemyAlignments(FactionSide, FactionSide)"/>, which calls Neutral the enemy of everyone.
/// </para>
/// The service keeps no state, so it needs no per-campaign reset. The model override stands down on a dedicated server, on a co-op
/// peer deferring to the host and with the switch off (vanilla's behavior); the postfix on the prisoner line only hides a line
/// locally, so it asks <see cref="HidesPrisonerLine"/>, which stands down for the switch alone.
/// </summary>
public sealed class SwornAnewService
{
    private readonly ISwornAnewSettingsProvider _settings;
    private readonly IAlignmentService _alignment;
    private readonly IUncapturableRegistry _registry;
    private readonly ICoopSessionProvider _coop;
    private readonly IDedicatedServerProvider _server;

    public SwornAnewService(ISwornAnewSettingsProvider settings, IAlignmentService alignment, IUncapturableRegistry registry,
        ICoopSessionProvider coop, IDedicatedServerProvider server)
    {
        _settings = settings;
        _alignment = alignment;
        _registry = registry;
        _coop = coop;
        _server = server;
    }

    /// <summary>
    /// For the <c>DefectionModel</c> override: true or false for a prisoner (can the lord be asked to join the kingdom), null for
    /// everyone else and whenever this feature stands down (no facts, a free hero, a dedicated server, a co-op peer deferring to
    /// the host, the switch off), so vanilla's own check decides.
    /// </summary>
    public bool? CanPrisonerDefect(DefectionFacts? facts) =>
        facts == null || !facts.IsPrisoner || Suppressed || !_settings.Enabled ? (bool?)null : Allows(facts);

    /// <summary>
    /// For the postfix on vanilla's prisoner line: whether to take the line away from a prisoner this feature refuses.
    /// The full verdict, not the alignment alone, because the line also offers a place to a mercenary clan's lord, whose joining
    /// raises <c>JoinAsMercenary</c> and never arms the party-screen commit. It hides a line on this machine only, so a co-op peer
    /// and a dedicated server answer it as everyone else does; a free hero and the switch off leave vanilla's line alone.
    /// </summary>
    public bool HidesPrisonerLine(DefectionFacts? facts) =>
        facts != null && facts.IsPrisoner && _settings.Enabled && !Allows(facts);

    /// <summary>
    /// Whether the open party screen should be committed once the conversation ends: the conversation partner's clan has just
    /// defected into the player's kingdom. The release the engine makes for the new kingdom would otherwise be undone when the
    /// player leaves the party screen with Escape.
    /// </summary>
    public bool CommitsPartyScreenAfter(bool partnerDefectedToPlayersKingdom) =>
        partnerDefectedToPlayersKingdom && !Suppressed && _settings.Enabled;

    // A dedicated server has no player to serve, and a co-op peer that is not the authority leaves a shared-world change to the host
    // (CoopInterop: ShouldDeferToHost also covers co-op mods TAOM cannot probe).
    private bool Suppressed => _server.IsDedicatedServer || _coop.ShouldDeferToHost;

    // The ransom screen lists a cloned prison roster, so a lord who just joined the kingdom could still be ransomed from it.
    private bool Allows(DefectionFacts f) =>
        !f.OnRansomScreen && PassesVanillaChecks(f) && InCustody(f) && !_registry.IsUncapturable(f.HeroId, f.RaceId) && AlignmentAllows(f);

    // DefaultDefectionModel line 9, minus its closing "not a prisoner".
    private static bool PassesVanillaChecks(DefectionFacts f) =>
        f.InKingdom && !f.InPlayerFaction && !f.IsFactionLeader && f.HasClan && !f.IsMinorFaction && !f.IsMercenary && f.ClanHasKingdom
        && (!f.TargetRuledByPlayerClan || !f.HasBloodFeudWithPlayer);

    private static bool InCustody(DefectionFacts f) => f.HeldByMainParty || f.InPlayerClanSettlement;

    private bool AlignmentAllows(DefectionFacts f) =>
        !FactionSideRules.AreOpposed(_alignment.ResolveSide(f.LordSide), _alignment.ResolveSide(f.PlayerSide));
}
