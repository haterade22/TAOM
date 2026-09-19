using TaleWorlds.Core;

namespace TAOM.Features.Elephant;

/// <summary>
/// The origin of one howdah crew archer (#627). The crew are the elephant's crew, spawned beside the party's troop
/// supplier, not from it, so they are not roster troops. The June code gave them the mahout's origin, and the first crew
/// casualty then went through PartyGroupAgentOrigin.SetKilled: the Harad elephant rider was removed from the party
/// roster while still riding, and the supplier's NumRemovedTroops moved, which MissionBattleSideSpawnContext reads as a
/// lost unit (a side could be declared beaten while its elephant fought on). So every casualty and score call is a no-op
/// here, and what the scoreboard (BattleCombatant, read unguarded by BattleObserverMissionLogic), the banner colours and
/// the command checks need comes from the mahout's origin. One instance per crew agent, with a per-seat seed so the four
/// archers do not share one face and one equipment roll.
/// </summary>
public sealed class HowdahCrewAgentOrigin : IAgentOriginBase
{
    private readonly IAgentOriginBase _mahout;
    private readonly bool _hasThrownWeapon;
    private readonly bool _hasSpear;
    private readonly bool _hasShield;
    private readonly bool _hasHeavyArmor;
    private Banner? _banner;

    public HowdahCrewAgentOrigin(IAgentOriginBase mahoutOrigin, BasicCharacterObject? troop, int seed)
    {
        _mahout = mahoutOrigin;
        Troop = troop!;
        Seed = seed;
        if (troop != null)
            AgentOriginUtilities.GetDefaultTroopTraits(troop, out _hasThrownWeapon, out _hasSpear, out _hasShield, out _hasHeavyArmor);
    }

    public bool IsUnderPlayersCommand => _mahout.IsUnderPlayersCommand;
    public bool IsInSameArmyAsPlayer => _mahout.IsInSameArmyAsPlayer;
    public uint FactionColor => _mahout.FactionColor;
    public uint FactionColor2 => _mahout.FactionColor2;
    public IBattleCombatant BattleCombatant => _mahout.BattleCombatant;
    public int UniqueSeed => Seed;
    public int Seed { get; }
    public Banner Banner => _banner ?? _mahout.Banner;
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

    // GetDefaultTraitsMask reads Troop.IsMounted; the troop is null only in unit tests (production always passes the
    // crew character), so a null troop reports no traits instead of throwing.
    public TroopTraitsMask GetTraitsMask() =>
        Troop == null ? TroopTraitsMask.None : AgentOriginUtilities.GetDefaultTraitsMask(this);
}
