using TaleWorlds.Core;

namespace TAOM.Features.Mumakil;

/// <summary>
/// The origin a Mûmakil crew archer is built with (#627 phase 2), a clone of the howdah's
/// <see cref="TAOM.Features.Elephant.HowdahCrewAgentOrigin"/>.
///
/// The crew belong to the beast, not to the party. Sharing the rider's own origin would book the first crew casualty
/// as the RIDER's: <c>PartyGroupAgentOrigin.SetKilled</c> removes that troop from the party roster and moves the
/// supplier's <c>NumRemovedTroops</c>, which <c>MissionBattleSideSpawnContext</c> reads as a lost unit, so a side
/// could be declared beaten while its mumakil fought on. That defect shipped once on the elephant (#627, delta
/// review) and this type is what prevents it here.
///
/// So every casualty and score call does nothing, while the scoreboard combatant, banner, colours and command state
/// forward to the rider's origin, which the engine reads on every agent build. Each archer carries its own seed so
/// the crew do not share one face.
/// </summary>
public sealed class MumakilCrewAgentOrigin : IAgentOriginBase
{
    private readonly IAgentOriginBase _rider;
    private readonly bool _hasThrownWeapon;
    private readonly bool _hasSpear;
    private readonly bool _hasShield;
    private readonly bool _hasHeavyArmor;
    private Banner? _banner;

    public MumakilCrewAgentOrigin(IAgentOriginBase riderOrigin, BasicCharacterObject? troop, int seed)
    {
        _rider = riderOrigin;
        Troop = troop!;
        Seed = seed;
        if (troop != null)
            AgentOriginUtilities.GetDefaultTroopTraits(troop, out _hasThrownWeapon, out _hasSpear, out _hasShield, out _hasHeavyArmor);
    }

    public bool IsUnderPlayersCommand => _rider.IsUnderPlayersCommand;
    public bool IsInSameArmyAsPlayer => _rider.IsInSameArmyAsPlayer;
    public uint FactionColor => _rider.FactionColor;
    public uint FactionColor2 => _rider.FactionColor2;
    public IBattleCombatant BattleCombatant => _rider.BattleCombatant;
    public int UniqueSeed => Seed;
    public int Seed { get; }
    public Banner Banner => _banner ?? _rider.Banner;
    public BasicCharacterObject Troop { get; }
    public bool HasThrownWeapon => _hasThrownWeapon;
    public bool HasHeavyArmor => _hasHeavyArmor;
    public bool HasShield => _hasShield;
    public bool HasSpear => _hasSpear;

    public void SetWounded() { }
    public void SetKilled() { }
    public void SetRouted(bool isOrderRetreat) { }
    public void OnAgentRemoved(float agentHealth) { }

    public void OnScoreHit(BasicCharacterObject victim, BasicCharacterObject formationCaptain, int damage, bool isFatal,
        bool isTeamKill, WeaponComponentData attackerWeapon) { }

    public void SetBanner(Banner banner) => _banner = banner;

    // GetDefaultTraitsMask reads Troop.IsMounted; the troop is null only in unit tests, so a null troop reports no
    // traits instead of throwing.
    public TroopTraitsMask GetTraitsMask() =>
        Troop == null ? TroopTraitsMask.None : AgentOriginUtilities.GetDefaultTraitsMask(this);
}
