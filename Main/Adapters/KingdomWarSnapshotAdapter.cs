using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Validation;

namespace TAOM.Adapters;

/// <summary>
/// Production <see cref="IKingdomWarSnapshotAdapter"/>. Every engine member read here was checked
/// against the installed v1.5.4 decompile: <c>Kingdom.Fiefs</c> (towns and castles, villages are not
/// in it), <c>Kingdom.InitialHomeSettlement</c>, <c>Settlement.OwnerClan</c>, <c>Clan.Kingdom</c>,
/// <c>Kingdom.CurrentTotalStrength</c>, <c>Kingdom.Clans</c>, <c>Clan.Heroes</c>, <c>Hero.IsPrisoner</c>,
/// <c>Campaign.UniqueGameId</c> and <c>CampaignTimeModel.CampaignStartTime</c>.
/// </summary>
public sealed class KingdomWarSnapshotAdapter : IKingdomWarSnapshotAdapter
{
    public IReadOnlyList<KingdomWarSnapshot> GetKingdoms()
    {
        var result = new List<KingdomWarSnapshot>();
        if (Campaign.Current == null)
            return result;

        var playerClan = Clan.PlayerClan;
        foreach (var kingdom in Kingdom.All)
        {
            if (kingdom == null || kingdom.IsEliminated)
                continue;
            result.Add(Read(kingdom, playerClan));
        }

        return result;
    }

    public string GetCampaignId() => Campaign.Current?.UniqueGameId ?? string.Empty;

    public int GetElapsedDay() =>
        FloorElapsedDays(Campaign.Current?.Models?.CampaignTimeModel?.CampaignStartTime.ElapsedDaysUntilNow);

    /// <summary>
    /// Whole days, floored; 0 for null, a non-finite or negative value, or one at or past 2^31. The check
    /// runs on a double, where int.MaxValue is exact: the float overload's bound rounds up to 2^31, which
    /// the cast would turn into int.MinValue.
    /// </summary>
    internal static int FloorElapsedDays(float? days)
    {
        if (!days.HasValue || !FiniteFloatValidator.IsFiniteInRange((double)days.Value, 0d, int.MaxValue))
            return 0;
        return (int)Math.Floor(days.Value);
    }

    public double GetNowHours() => Campaign.Current == null ? 0d : CampaignTime.Now.ToHours;

    private static KingdomWarSnapshot Read(Kingdom kingdom, Clan? playerClan)
    {
        var towns = 0;
        var castles = 0;
        var fiefs = kingdom.Fiefs;
        for (var i = 0; i < fiefs.Count; i++)
        {
            var fief = fiefs[i];
            if (fief == null)
                continue;
            if (fief.IsCastle)
                castles++;
            else
                towns++;
        }

        var strength = kingdom.CurrentTotalStrength;
        return new KingdomWarSnapshot
        {
            Id = kingdom.StringId ?? string.Empty,
            CultureId = kingdom.Culture?.StringId ?? string.Empty,
            IsPlayerRuled = playerClan != null && kingdom.RulingClan == playerClan,
            AtWar = IsAtWarWithAnotherKingdom(kingdom),
            Towns = towns,
            Castles = castles,
            HomeHeld = IsHomeHeld(kingdom),
            Strength = FiniteFloatValidator.IsFinite(strength) ? strength : -1f,
            PrisonerLords = CountPrisonerLords(kingdom),
        };
    }

    private static bool IsAtWarWithAnotherKingdom(Kingdom kingdom)
    {
        foreach (var other in Kingdom.All)
        {
            if (other != null && other != kingdom && !other.IsEliminated && kingdom.IsAtWarWith(other))
                return true;
        }

        return false;
    }

    private static bool? IsHomeHeld(Kingdom kingdom)
    {
        var home = kingdom.InitialHomeSettlement;
        if (home == null)
            return null;
        return home.OwnerClan?.Kingdom == kingdom;
    }

    private static int CountPrisonerLords(Kingdom kingdom)
    {
        var count = 0;
        var clans = kingdom.Clans;
        for (var i = 0; i < clans.Count; i++)
        {
            var heroes = clans[i]?.Heroes;
            if (heroes == null)
                continue;
            for (var j = 0; j < heroes.Count; j++)
            {
                if (heroes[j]?.IsPrisoner == true)
                    count++;
            }
        }

        return count;
    }
}
