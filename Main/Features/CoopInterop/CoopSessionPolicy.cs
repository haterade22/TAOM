namespace TAOM.Features.CoopInterop;

/// <summary>
/// Pure decision layer for "may this peer run authoritative campaign logic right now?".
///
/// Split out from <see cref="CoopSessionProvider"/> for the same reason
/// <c>PatchShieldPolicy</c> / <c>SaveShieldPolicy</c> are split from their installers: the provider
/// binds to BannerlordCoop's assemblies by reflection and cannot run in a test host, but the
/// decision itself is three booleans and must be pinned exactly.
///
/// WHY THIS EXISTS — BannerlordCoop does NOT stop a client ticking the campaign. Its
/// <c>PartyTickPatch</c> blocks only the per-entity tickers (<c>TickPeriodicEvents</c>,
/// <c>MobilePartyHourlyTick</c>, <c>MobileParty.HourlyTick</c>), so
/// <c>DailyTickSettlementEvent</c> and friends never reach a client — but the GLOBAL
/// <c>CampaignEvents.DailyTickEvent</c> / <c>HourlyTickEvent</c> fire on both peers, off a
/// locally-advancing server-slewed clock. Any TAOM behaviour on a global tick therefore runs twice
/// unless it consults this. Coop's own countermeasure is a hand-written allowlist of ~127
/// <c>RegisterEvents</c> prefixes over NAMED VANILLA types; there is no generic hook a third-party
/// mod can register into, so TAOM must author its own.
///
/// Evidence + the full tick split: <c>docs/research/bannerlordcoop-internals.md</c>.
/// </summary>
public static class CoopSessionPolicy
{
    /// <summary>
    /// True when this peer owns the simulation and may mutate shared campaign state.
    ///
    /// FAILS OPEN TO SINGLEPLAYER, deliberately. Every input this takes is best-effort reflection
    /// against another mod's internals; when any of it is unavailable the honest reading is "no
    /// co-op session", and the only safe behaviour then is exactly what TAOM does today. A false
    /// negative here silently disables TAOM features for a solo player, which is a far worse and
    /// far less diagnosable outcome than the divergence it would be guarding against.
    /// </summary>
    /// <param name="sessionActive">
    /// A co-op session container is live. From <c>ContainerProvider.TryGetContainer</c> — NEVER
    /// from <c>ContainerProvider.Alive</c>, which is an inline static initialiser evaluated while
    /// the backing field is still null and is therefore permanently false.
    /// </param>
    /// <param name="isServer">
    /// Coop's own host flag (<c>Common.ModInformation.IsServer</c>). Only meaningful when
    /// <paramref name="sessionActive"/> is true: it defaults to false and is STICKY across session
    /// teardown, so on its own it reports "client" in plain singleplayer whenever the Coop module is
    /// merely enabled, and "host" forever after hosting once.
    /// </param>
    public static bool IsAuthority(bool sessionActive, bool isServer) => !sessionActive || isServer;

    /// <summary>
    /// True only when a co-op session is live AND this peer is a client. Never true in
    /// singleplayer, which is the trap <c>ModInformation.IsClient</c> falls into on its own.
    /// </summary>
    public static bool IsCoopClient(bool sessionActive, bool isServer) => sessionActive && !isServer;

    /// <summary>
    /// Should this peer YIELD a shared-world decision (a diplomacy veto, a permanent-alliance
    /// enforcement, a global time change) to the host rather than applying its own answer?
    ///
    /// Distinct from <see cref="IsAuthority"/> because it must also cover co-op mods TAOM cannot
    /// probe. The truth table this exists to satisfy:
    ///
    /// <code>
    /// module | probe | session | server | defer? | why
    /// -------+-------+---------+--------+--------+---------------------------------------------
    ///  no    |   -   |    -    |   -    |  NO    | plain singleplayer
    ///  yes   |  no   |    -    |   -    |  YES   | unprobeable co-op mod (BannerlordTogether):
    ///        |       |         |        |        | cannot tell host from client, so the only safe
    ///        |       |         |        |        | answer is to yield on every peer
    ///  yes   |  yes  |   no    |   -    |  NO    | BannerlordCoop installed but playing SOLO
    ///  yes   |  yes  |  yes    |  yes   |  NO    | BannerlordCoop host — it IS the authority
    ///  yes   |  yes  |  yes    |  no    |  YES   | BannerlordCoop client
    /// </code>
    ///
    /// Gating these decisions on module PRESENCE alone (the first implementation) got rows 3 and 4
    /// wrong: a solo player who merely had the Coop module enabled, and the co-op host itself, both
    /// silently lost TAOM's War of the Ring diplomacy rules. Gating on <see cref="IsAuthority"/>
    /// alone gets row 2 wrong, because that fails open and would report every BannerlordTogether
    /// peer as authoritative — no gate at all. Only the pair of signals answers all five rows.
    /// </summary>
    /// <param name="coopModuleActive">A co-op module is in the launcher's active set.</param>
    /// <param name="roleProbeAvailable">
    /// TAOM can actually read host/client role for the active co-op mod — i.e. its runtime members
    /// bound. False for any co-op mod TAOM has no probe for.
    /// </param>
    public static bool ShouldDeferToHost(
        bool coopModuleActive, bool roleProbeAvailable, bool sessionActive, bool isServer)
    {
        if (!coopModuleActive) return false;
        if (!roleProbeAvailable) return true;
        if (!sessionActive) return false;
        return !isServer;
    }

    /// <summary>
    /// May this peer write state that round-trips through a TAOM <c>SyncData</c> key?
    ///
    /// A co-op client may not. Its save record is the HOST's — it was shipped over the wire at join
    /// — so any save-backed field a client writes is per-peer state landing in shared saved state.
    /// The client keeps such state in non-persisted process memory instead.
    ///
    /// Exists as a named predicate rather than an inline <c>!IsCoopClient</c> because the inline
    /// form is exactly what shipped wrong: <c>SiegeDefenseService.GrantReward</c> set the
    /// save-serialized <c>RewardClaimed</c> flag on every peer, so a client claiming its own siege
    /// reward mutated the host's event record (Codex review 2026-08-01, HIGH).
    /// </summary>
    public static bool MayWriteSaveBackedState(bool isCoopClient) => !isCoopClient;
}
