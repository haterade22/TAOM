using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TAOM.Core.Logging;

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
    private readonly IModLogger _logger;
    private readonly HashSet<string> _warnedMissingTemplates = new HashSet<string>();

    public CastleNotableMaintainer(ICastleRecruitmentService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
    }

    public void EnsureAllCastles()
    {
        foreach (Settlement settlement in Settlement.All)
        {
            if (!settlement.IsCastle)
                continue;
            // Runs inside OnNewGameCreated/OnGameLoaded: an escaped exception doesn't CTD — it stalls
            // the engine's loading state machine, which re-runs the handler every tick forever
            // (crash report 2026-07-07: 26k+ identical NREs on a tester's new-game loading screen).
            try
            {
                EnsureCastleNotables(settlement);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[CastleRecruitment] EnsureCastleNotables failed for '{settlement.StringId}' — skipping castle: {ex}");
            }
        }
    }

    /// <summary>Daily per-castle work: top up the notable population, then generate volunteers.</summary>
    public void TickCastle(Settlement castle)
    {
        try
        {
            EnsureCastleNotables(castle);
            FillCastleVolunteers(castle);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[CastleRecruitment] daily tick failed for '{castle.StringId}' — skipping castle: {ex}");
        }
    }

    /// <summary>Tops a castle up to its per-occupation targets. Only ever ADDS notables (never moves
    /// or mutates existing heroes), so it is safe in every entity state and idempotent across repeated
    /// loads/ticks — counts the live notables first and spawns only the deficit.</summary>
    private void EnsureCastleNotables(Settlement castle)
    {
        string cultureId = castle.Culture?.StringId ?? "(null-culture)";

        // CreateNotable NREs when the culture has no template for the occupation
        // (GetRandomTemplateByOccupation returns null on an empty filtered list) — pre-check and
        // skip, same guard as CultureConversionAdapter.ReplaceNotable (#325).
        var templates = castle.Culture?.NotableTemplates;
        if (templates == null)
        {
            WarnOnce($"{cultureId}:no-templates",
                $"[CastleRecruitment] culture '{cultureId}' has no NotableTemplates — skipping castle notables (first seen at '{castle.StringId}')");
            return;
        }

        // The engine's occupation filter (GetRandomTemplateByOccupation's Where) does NOT null-check,
        // so a single null entry (malformed <notable_templates> ref — ReadObjectReferenceFromXml
        // returns null on a missing name attribute) NREs EVERY CreateNotable for the culture,
        // regardless of occupation. Skip the culture entirely rather than spam the daily-tick catch.
        if (templates.Any(t => t == null))
        {
            WarnOnce($"{cultureId}:null-template-entry",
                $"[CastleRecruitment] culture '{cultureId}' has a null NotableTemplates entry (malformed <notable_templates> ref) — skipping castle notables (first seen at '{castle.StringId}')");
            return;
        }

        foreach (var target in _service.GetOccupationTargets())
        {
            Occupation occupation = ToOccupation(target.Key);
            if (!templates.Any(t => t.Occupation == occupation))
            {
                WarnOnce($"{cultureId}:{occupation}",
                    $"[CastleRecruitment] culture '{cultureId}' has no {occupation} notable template — skipping (first seen at '{castle.StringId}')");
                continue;
            }

            int existing = CountNotables(castle, occupation);
            for (int i = existing; i < target.Value; i++)
            {
                // Vanilla NotablesCampaignBehavior.OnHeroCreated places the new notable into its
                // HomeSettlement (the castle), gives 10000 gold, and assigns a supporter clan.
                // Unreachable on v1.4.6 — CreateNotable throws rather than returning null (the
                // pre-checks + the per-castle catch are the real guards); kept as a cheap forward
                // guard should a future engine build convert the throw into a null return.
                if (HeroCreator.CreateNotable(occupation, castle) == null)
                {
                    _logger.LogWarning($"[CastleRecruitment] CreateNotable returned null for {occupation} at '{castle.StringId}'");
                    break;
                }
            }
        }
    }

    private void WarnOnce(string key, string message)
    {
        if (_warnedMissingTemplates.Add(key))
            _logger.LogWarning(message);
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
