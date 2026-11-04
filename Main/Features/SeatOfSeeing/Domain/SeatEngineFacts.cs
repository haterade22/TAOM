namespace TAOM.Features.SeatOfSeeing.Domain;

/// <summary>
/// What the engine says about the mission right now, read through the screen adapter each time the state machine needs it.
/// <paramref name="Interlude"/> is a Conversation, Barter or CutScene mode; <paramref name="BattleMode"/> is Mission.Mode Battle
/// (the only mode the engine's return glide runs in); <paramref name="RageHoldsHero"/> is true while a warg rage's controller, not
/// vanilla's, holds the hero (build-spec 5.5); <paramref name="RadialMenuActive"/> is a radial menu open on the mission screen, which
/// refuses an entry (build-spec 6.0).
/// </summary>
internal readonly record struct SeatEngineFacts(
    bool Deployment, bool Interlude, bool BattleMode, bool PhotoMode, bool CustomCamera, bool MissionEnded, bool RageHoldsHero,
    bool RadialMenuActive);
