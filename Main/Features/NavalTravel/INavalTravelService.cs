namespace TAOM.Features.NavalTravel;

/// <summary>
/// Pure decisions for the naval-travel feature, free of TaleWorlds + MCM types so it is fully
/// unit-testable. Consumed by <c>TaomPartyNavigationModel</c> (the boundary that converts sealed
/// engine types into the primitives below). The whole feature reuses the base-engine naval system
/// (water pathing, embark/disembark, native ship rendering) by overriding the engine's
/// <c>PartyNavigationModel</c> — no NavalDLC dependency, works without the DLC.
/// </summary>
public interface INavalTravelService
{
    /// <summary>Master gate. When false the feature is inert and parties behave vanilla (land-only).</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Whether to suppress the vanilla <c>AIMoveToNearestLandBehavior</c> at-sea→nearest-land rescue
    /// tick. That behavior fires only for a party already <c>IsCurrentlyAtSea</c> and calls the native
    /// cross-region land-pathfind (<c>GetNearestFaceCenterForPositionWithPath</c>), which AVs on
    /// TAOM_Map because the naval region-map navigation infrastructure it expects is never built (#120).
    /// Nothing puts a party at sea except this feature, so the behavior is inert in vanilla TAOM and the
    /// only thing the rescue can do on TAOM_Map is crash — suppress it whenever the feature is enabled
    /// (the player disembarks manually; the patch covers AI parties too). When the feature is off the
    /// behavior is inert anyway, so we leave it running (vanilla).
    /// </summary>
    bool ShouldSuppressAtSeaLandRescue { get; }

    /// <summary>
    /// Embark/disembark proximity the engine uses to begin a land↔sea transition. NavalDLC uses
    /// 0.5; the engine default of 0 effectively blocks embarking.
    /// </summary>
    float EmbarkThresholdDistance { get; }

    /// <summary>
    /// Whether a party may START sailing (gain naval capability from land). Replaces NavalDLC's
    /// ship-ownership check so any party can sail without owning a ship (no DLC content). Honors the
    /// independent player / AI MCM gates. This is the per-party permission; the full capability gate
    /// (incl. at-sea grace + army inheritance) is <see cref="HasNavalCapability"/>.
    /// </summary>
    bool CanPartySail(bool isPlayerParty);

    /// <summary>
    /// The full naval-capability decision the engine's <c>PartyNavigationModel</c> gate routes to.
    /// A party <paramref name="isAtSea"/> keeps capability unconditionally so it can reach land — the
    /// gates govern NEW embarks (from land), not stranding a party mid-voyage when a toggle flips off
    /// (the engine propagates <c>IsCurrentlyAtSea</c> to attached parties but recomputes each party's
    /// navigation capability per party). Otherwise a party may sail if <see cref="CanPartySail"/>, or
    /// — for a non-leader — if its army leader can sail (<paramref name="attachedLeaderCanSail"/>),
    /// mirroring NavalDLC's attached-to inheritance so a player-led army doesn't strand its attached
    /// AI parties at sea when ApplyToAi is off.
    /// </summary>
    bool HasNavalCapability(bool isMainParty, bool isAtSea, bool attachedLeaderCanSail);

    /// <summary>
    /// Whether the given <c>TerrainType</c> integer is navigable by ship (Water/Lake/River/
    /// CoastalSea/OpenSea + the engine's restriction/under-bridge ids). Drives "all water travelable".
    /// </summary>
    bool IsNavalTerrain(int terrainTypeId);

    /// <summary>
    /// Whether an at-sea party's campaign map-icon should be swapped to a boat mesh. The base game
    /// omits the leader figure at sea but adds no ship (the ship visual is otherwise NavalDLC-only), so
    /// TAOM renders the boat itself. False during the embark/disembark transition (the base shows the
    /// figure then), when at sea is false, or when the feature / boat-visual toggle is off.
    /// </summary>
    bool ShouldRenderBoat(bool isAtSea, bool isInTransition);

    /// <summary>Mesh name for the at-sea boat icon (default base-game <c>boat_sail_on</c>; JSON-swappable).</summary>
    string BoatMeshName { get; }

    /// <summary>Uniform scale applied to the boat mesh (default 0.4, matching NavalDLC's map ship).</summary>
    float BoatScale { get; }

    /// <summary>
    /// Name of the modifier key the player holds to deliberately set sail (parsed to a TaleWorlds
    /// <c>InputKey</c> by the model). Default <c>LeftAlt</c>.
    /// </summary>
    string SailModifierKey { get; }
}
