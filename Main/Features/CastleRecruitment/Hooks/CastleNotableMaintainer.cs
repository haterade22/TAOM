using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace TAOM.Features.CastleRecruitment.Hooks;

/// <summary>
/// Engine-glue boundary for castle notable population + volunteer generation, factored out of
/// <see cref="CastleRecruitmentBehavior"/> (ADR-002 — keep the behavior a thin event router). All
/// decisions (target counts, occupations, slot probabilities) come from
/// <see cref="ICastleRecruitmentService"/>; this class only orchestrates the TaleWorlds calls.
/// Callers gate on <c>IService.IsEnabled</c> before invoking.
/// </summary>
internal sealed class CastleNotableMaintainer
{
    private readonly ICastleRecruitmentService _service;

    public CastleNotableMaintainer(ICastleRecruitmentService service)
    {
        _service = service;
    }

    public void EnsureAllCastles()
    {
        foreach (Settlement settlement in Settlement.All)
        {
            if (settlement.IsCastle)
                EnsureCastleNotables(settlement);
        }
    }

    /// <summary>Daily per-castle work: top up the notable population, then generate volunteers.</summary>
    public void TickCastle(Settlement castle)
    {
        EnsureCastleNotables(castle);
        FillCastleVolunteers(castle);
    }

    /// <summary>Tops a castle up to its per-occupation targets. Only ever ADDS notables (never moves
    /// or mutates existing heroes), so it is safe in every entity state and idempotent across repeated
    /// loads/ticks — counts the live notables first and spawns only the deficit.</summary>
    private void EnsureCastleNotables(Settlement castle)
    {
        foreach (var target in _service.GetOccupationTargets())
        {
            Occupation occupation = ToOccupation(target.Key);
            int existing = CountNotables(castle, occupation);
            for (int i = existing; i < target.Value; i++)
            {
                // Vanilla NotablesCampaignBehavior.OnHeroCreated places the new notable into its
                // HomeSettlement (the castle), gives 10000 gold, and assigns a supporter clan.
                HeroCreator.CreateNotable(occupation, castle);
            }
        }
    }

    private static int CountNotables(Settlement castle, Occupation occupation)
    {
        int count = 0;
        foreach (Hero notable in castle.Notables)
        {
            if (notable.IsAlive && notable.CharacterObject?.Occupation == occupation)
                count++;
        }
        return count;
    }

    /// <summary>Castle-safe mirror of vanilla
    /// <c>RecruitmentCampaignBehavior.UpdateVolunteersOfNotablesInSettlement</c> (which skips castles,
    /// and whose production model NREs for them). Uses the service's pure slot probability instead of
    /// <c>VolunteerModel.GetDailyVolunteerProductionProbability</c>.</summary>
    private void FillCastleVolunteers(Settlement castle)
    {
        int maxTier = Campaign.Current.Models.VolunteerModel.MaxVolunteerTier;
        foreach (Hero notable in castle.Notables)
        {
            if (!notable.CanHaveRecruits || !notable.IsAlive)
                continue;
            // GetBasicVolunteer routes through TaomVolunteerModel → the LOTR recruitment pool (keyed
            // on the castle id). Castle-safe because our occupations are never RuralNotable.
            CharacterObject basicVolunteer = Campaign.Current.Models.VolunteerModel.GetBasicVolunteer(notable);
            if (basicVolunteer == null)
                continue;

            for (int i = 0; i < 6; i++)
            {
                if (MBRandom.RandomFloat >= _service.GetSlotProductionProbability(i))
                    continue;
                CharacterObject current = notable.VolunteerTypes[i];
                if (current == null)
                {
                    notable.VolunteerTypes[i] = basicVolunteer;
                }
                else if (current.UpgradeTargets.Length != 0 && current.Tier < maxTier)
                {
                    float upgradeChance = MathF.Log(notable.Power / (float)current.Tier, 2f) * 0.01f;
                    if (MBRandom.RandomFloat < upgradeChance)
                        notable.VolunteerTypes[i] = current.UpgradeTargets[MBRandom.RandomInt(current.UpgradeTargets.Length)];
                }
            }
        }
    }

    private static Occupation ToOccupation(CastleNotableOccupation occupation)
    {
        switch (occupation)
        {
            case CastleNotableOccupation.GangLeader: return Occupation.GangLeader;
            case CastleNotableOccupation.Headman: return Occupation.Headman;
            case CastleNotableOccupation.Merchant: return Occupation.Merchant;
            case CastleNotableOccupation.Artisan: return Occupation.Artisan;
            // Force a future CastleNotableOccupation addition to update this switch rather than
            // silently mapping to GangLeader (data-flow review finding).
            default: throw new ArgumentOutOfRangeException(nameof(occupation), occupation, "Unmapped castle occupation");
        }
    }
}
