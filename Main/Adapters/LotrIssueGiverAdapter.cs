using TaleWorlds.CampaignSystem;
using TAOM.Features.LotrIssues.Domain;

namespace TAOM.Adapters;

/// <summary>
/// Wraps a polled <see cref="Hero"/> for LOTR-issue eligibility. Maps the hero's vanilla notable role
/// to a <see cref="IssueGiverOccupation"/> (null when the hero gives no issues). All members are
/// computed from public 1.4.6 Hero APIs; guards keep it null-safe for the boundary (ADR-007).
/// </summary>
public class LotrIssueGiverAdapter : ILotrIssueGiverAdapter
{
    private readonly Hero _hero;

    public LotrIssueGiverAdapter(Hero hero)
    {
        _hero = hero;
    }

    public bool IsValid => _hero != null && _hero.IsAlive && _hero.CurrentSettlement != null;

    public IssueGiverOccupation? Occupation
    {
        get
        {
            if (_hero == null) return null;
            // Lord is NOT a notable occupation — Hero.IsNotable is false for lords (verified v1.4.6
            // Hero.IsNotable: Artisan/GangLeader/Preacher/Merchant/RuralNotable/Headman only). So the
            // notable roles gate behind IsNotable, but Lord is checked independently — otherwise every
            // lord-giver issue would be unreachable.
            if (_hero.IsNotable)
            {
                if (_hero.IsHeadman) return IssueGiverOccupation.Headman;
                if (_hero.IsGangLeader) return IssueGiverOccupation.GangLeader;
                if (_hero.IsMerchant) return IssueGiverOccupation.Merchant;
                if (_hero.IsArtisan) return IssueGiverOccupation.Artisan;
                if (_hero.IsRuralNotable) return IssueGiverOccupation.RuralNotable;
            }
            if (_hero.IsLord) return IssueGiverOccupation.Lord;
            return null;
        }
    }

    // Hero.Culture is a saveable field (safe once the hero is non-null), not a computed getter.
    public string CultureStringId => _hero?.Culture?.StringId ?? "";

    public int RelationWithPlayer => _hero == null ? 0 : (int)_hero.GetRelationWithPlayer();
}
