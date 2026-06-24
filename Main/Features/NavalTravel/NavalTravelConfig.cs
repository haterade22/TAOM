namespace TAOM.Features.NavalTravel;

/// <summary>
/// JSON DTO for <c>naval_travel/naval_travel_config.json</c>. Validated on load by
/// <see cref="NavalTravelConfigProvider"/>; the three booleans are overridden at runtime by MCM
/// (<c>TaomSettings</c>) via <see cref="NavalTravelSettingsProvider"/>.
/// </summary>
public class NavalTravelConfig
{
    /// <summary>Master toggle. When false the feature is inert (vanilla land-only movement).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>When false the player party never gains naval capability (AI still gated by <see cref="ApplyToAi"/>).</summary>
    public bool ApplyToPlayer { get; set; } = true;

    /// <summary>When false AI parties never gain naval capability (player still gated by <see cref="ApplyToPlayer"/>).</summary>
    public bool ApplyToAi { get; set; } = true;

    /// <summary>
    /// Embark/disembark proximity threshold. Matches NavalDLC's 0.5; the engine default of 0 blocks
    /// embarking. Validated finite + within [0, <see cref="MaxEmbarkThresholdDistance"/>].
    /// </summary>
    public float EmbarkThresholdDistance { get; set; } = DefaultEmbarkThresholdDistance;

    /// <summary>
    /// <c>TerrainType</c> integers a ship may navigate. Default mirrors NavalDLC's set:
    /// 8=Lake, 10=Water, 11=River, 18=CoastalSea, 19=OpenSea, 23=LandRestriction, 24=SeaRestriction,
    /// 25=UnderBridge. Validated against the known <c>TerrainType</c> enum on load.
    /// </summary>
    public int[] NavalTerrainTypeIds { get; set; } = DefaultNavalTerrainTypeIds();

    /// <summary>
    /// When true (default) an at-sea party's map icon is swapped to a boat mesh. False keeps the
    /// vanilla (figure-less) at-sea icon but still lets parties move over water.
    /// </summary>
    public bool RenderBoatVisual { get; set; } = true;

    /// <summary>
    /// Mesh used for the at-sea boat icon. Default is the base-game <c>boat_sail_on</c> (ships in
    /// Native, no DLC). Swap for any loadable map mesh. Empty/whitespace reverts to default on load.
    /// </summary>
    public string BoatMeshName { get; set; } = DefaultBoatMeshName;

    /// <summary>Uniform scale for the boat mesh. Validated finite + within (0, <see cref="MaxBoatScale"/>].</summary>
    public float BoatScale { get; set; } = DefaultBoatScale;

    /// <summary>
    /// Modifier key the player HOLDS to deliberately set sail: hold it + click water and the party
    /// heads to the coast and embarks (auto-pathing never picks a sea route over a land/bridge route,
    /// so sailing is player-initiated). Parsed to a TaleWorlds <c>InputKey</c>; unknown/empty names
    /// fall back to <see cref="DefaultSailModifierKey"/>. Disembark is automatic (click land while at sea).
    /// </summary>
    public string SailModifierKey { get; set; } = DefaultSailModifierKey;

    public const float DefaultEmbarkThresholdDistance = 0.5f;
    public const float MaxEmbarkThresholdDistance = 50f;
    public const string DefaultBoatMeshName = "boat_sail_on";
    public const float DefaultBoatScale = 0.4f;
    public const float MaxBoatScale = 100f;
    public const string DefaultSailModifierKey = "LeftAlt";

    public static int[] DefaultNavalTerrainTypeIds() => new[] { 8, 10, 11, 18, 19, 23, 24, 25 };
}
