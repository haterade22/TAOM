using System;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TAOM.Features.SpecialResources;

namespace TAOM.Features.Realms;

/// <summary>
/// The player's special resource (Castar, Marks, Gems...) through TAOM's <see cref="ISpecialResourceSpender"/>, resolved
/// by the hero's kingdom, then culture. Every TAOM call is behind a catch, so a TAOM build without the spender turns
/// buying claims off instead of breaking the titles window.
/// </summary>
internal static class SpecialResources
{
    /// <summary>The resource's name and the hero's balance; null when their people have none or TAOM cannot say.</summary>
    public static (string Name, float Amount)? Balance(Hero hero)
    {
        try
        {
            var balance = Spender()?.GetBalance(hero.StringId, hero.Clan?.Kingdom?.StringId, hero.Culture?.StringId);
            return balance == null ? null : (balance.DisplayName, balance.Amount);
        }
        catch (Exception ex)
        {
            Debug.Print($"[TAOM.Realms] special resource balance unavailable: {ex.Message}");
            return null;
        }
    }

    /// <summary>Takes the amount when the balance covers it.</summary>
    public static bool TrySpend(Hero hero, float amount)
    {
        try
        {
            return Spender()?.TrySpend(hero.StringId, hero.Clan?.Kingdom?.StringId, hero.Culture?.StringId, amount) == true;
        }
        catch (Exception ex)
        {
            Debug.Print($"[TAOM.Realms] special resource spend failed: {ex.Message}");
            return false;
        }
    }

    // Kept out of line so a missing TAOM type fails inside the callers' try blocks.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ISpecialResourceSpender? Spender() => TAOM.IoC.Resolve<ISpecialResourceSpender>();
}
