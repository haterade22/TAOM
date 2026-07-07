using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Core.Logging;
using TAOM.Features.CultureConversion.Domain;

namespace TAOM.Adapters;

/// <summary>
/// Boundary implementation of <see cref="ICultureConversionAdapter"/>. Resolves settlements by
/// <c>Settlement.Find</c> (same as <c>NamedCompanionAdapter</c>) and delegates culture-id resolution
/// to the existing <see cref="ICultureObjectAdapter"/>. Computed TaleWorlds properties use <c>?.</c>
/// per the adapter rules (they can throw before a plain null check).
/// </summary>
public class CultureConversionAdapter : ICultureConversionAdapter
{
    private readonly ICultureObjectAdapter _cultureObjectAdapter;
    private readonly IModLogger _logger;

    public CultureConversionAdapter(ICultureObjectAdapter cultureObjectAdapter, IModLogger logger)
    {
        _cultureObjectAdapter = cultureObjectAdapter;
        _logger = logger;
    }

    public bool IsFortification(string settlementId)
        => Settlement.Find(settlementId)?.IsFortification ?? false;

    public bool IsPlayerOwned(string settlementId)
    {
        var settlement = Settlement.Find(settlementId);
        return settlement?.OwnerClan != null && settlement.OwnerClan == Clan.PlayerClan;
    }

    public string GetCurrentCultureId(string settlementId)
        => Settlement.Find(settlementId)?.Culture?.StringId;

    public string GetOwnerCultureId(string settlementId)
        => Settlement.Find(settlementId)?.OwnerClan?.Culture?.StringId;

    public float? GetLoyalty(string settlementId)
    {
        var town = Settlement.Find(settlementId)?.Town;
        return town?.Loyalty;
    }

    public IReadOnlyList<string> GetBoundVillageSettlementIds(string settlementId)
    {
        var result = new List<string>();
        var settlement = Settlement.Find(settlementId);
        var villages = settlement?.BoundVillages;
        if (villages == null)
            return result;

        foreach (var village in villages)
        {
            var villageSettlementId = village?.Settlement?.StringId;
            if (!string.IsNullOrEmpty(villageSettlementId))
                result.Add(villageSettlementId);
        }
        return result;
    }

    public bool SetSettlementCulture(string settlementId, string cultureId)
    {
        var settlement = Settlement.Find(settlementId);
        if (settlement == null)
        {
            _logger.LogWarning($"CultureConversionAdapter: cannot set culture — settlement '{settlementId}' not found");
            return false;
        }

        if (!(_cultureObjectAdapter.ResolveCulture(cultureId) is CultureObject culture))
        {
            _logger.LogWarning($"CultureConversionAdapter: cannot resolve culture '{cultureId}' for settlement '{settlementId}'");
            return false;
        }

        settlement.Culture = culture;
        return true;
    }

    public void ResetVolunteers(string settlementId)
    {
        var settlement = Settlement.Find(settlementId);
        if (settlement == null)
            return;

        foreach (Hero notable in settlement.Notables)
        {
            if (notable == null || !notable.IsAlive || !notable.CanHaveRecruits)
                continue;
            var slots = notable.VolunteerTypes;
            if (slots == null)
                continue;
            for (int i = 0; i < slots.Length; i++)
                slots[i] = null;
        }
    }

    public IReadOnlyList<ConvertibleNotable> GetNotables(string settlementId)
    {
        var result = new List<ConvertibleNotable>();
        var notables = Settlement.Find(settlementId)?.Notables;
        if (notables == null)
            return result;

        foreach (Hero notable in notables)
        {
            if (notable == null)
                continue;
            result.Add(new ConvertibleNotable(notable.StringId, notable.Culture?.StringId, notable.IsAlive));
        }
        return result;
    }

    public bool ReplaceNotable(string heroId)
    {
        try
        {
            var hero = Campaign.Current?.CampaignObjectManager?.Find<Hero>(heroId);
            if (hero == null || !hero.IsAlive || !hero.IsNotable || hero.CurrentSettlement == null)
            {
                _logger.LogWarning($"CultureConversionAdapter: cannot replace notable '{heroId}' — unresolvable, dead, non-notable, or not in a settlement");
                return false;
            }

            var settlement = hero.CurrentSettlement;
            var occupation = hero.Occupation;

            // CreateNotable NREs when the culture has no template for the occupation
            // (GetRandomTemplateByOccupation returns null on an empty filtered list) — pre-check and keep the old notable.
            var templates = settlement.Culture?.NotableTemplates;
            if (templates == null || !templates.Any(t => t != null && t.Occupation == occupation))
            {
                _logger.LogWarning($"CultureConversionAdapter: culture '{settlement.Culture?.StringId}' has no {occupation} notable template — keeping '{heroId}'");
                return false;
            }

            // The engine's occupation filter does NOT null-check, so a single null entry (malformed
            // <notable_templates> ref) NREs CreateNotable even when a valid match exists — same gate
            // as CastleNotableMaintainer (#332 deep-review finding, 2026-07-07).
            if (templates.Any(t => t == null))
            {
                _logger.LogWarning($"CultureConversionAdapter: culture '{settlement.Culture?.StringId}' has a null NotableTemplates entry (malformed <notable_templates> ref) — keeping '{heroId}'");
                return false;
            }

            var replacement = HeroCreator.CreateNotable(occupation, settlement);
            // Unreachable on v1.4.6 — CreateNotable throws rather than returning null; cheap forward
            // guard should a future engine build convert the throw into a null return.
            if (replacement == null)
            {
                _logger.LogWarning($"CultureConversionAdapter: CreateNotable returned null for {occupation} in '{settlement.StringId}' — keeping '{heroId}'");
                return false;
            }

            // Transfers must precede removal: OnHeroKilled destroys any caravans the victim still owns,
            // and the engine's workshop/alley death listeners reassign/null whatever wasn't moved.
            foreach (var workshop in hero.OwnedWorkshops.ToList())
                ChangeOwnerOfWorkshopAction.ApplyByDeath(workshop, replacement);
            foreach (var alley in hero.OwnedAlleys.ToList())
                alley.SetOwner(replacement);
            foreach (var caravan in hero.OwnedCaravans.ToList())
                CaravanPartyComponent.TransferCaravanOwnership(caravan.MobileParty, replacement, settlement);

            // Cancel (not transfer) any issue/quest; leaves Issue == null so ApplyByRemove's
            // notable-has-quest assert and IssueManager's death handling both no-op.
            hero.Issue?.CompleteIssueWithCancel();

            // A notable dying at Power >= NotableDisappearPowerLimit spawns a RELATIVE that copies
            // the OLD culture (NotablesCampaignBehavior.OnHeroKilled) — zero power so no heir appears.
            hero.AddPower(-hero.Power);

            KillCharacterAction.ApplyByRemove(hero);
            return true;
        }
        catch (System.Exception ex)
        {
            _logger.LogError($"CultureConversionAdapter: replacing notable '{heroId}' failed: {ex.Message}");
            return false;
        }
    }
}
