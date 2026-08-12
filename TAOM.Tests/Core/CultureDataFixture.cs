using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Core;

/// <summary>
/// Shared helpers for the config-validation tests that read TAOM's culture data straight off disk
/// (<see cref="CultureLordTemplateTests"/>, <see cref="CulturePartyTemplateTests"/>).
///
/// Both need the same two things: the ModuleData root, and the list of cultures whose only
/// settlements are hideouts. The hideout list carries a long rationale that must not be duplicated,
/// so it lives here once.
/// </summary>
internal static class CultureDataFixture
{
    /// <summary>
    /// Minor cultures whose only settlements are hideouts, so no town or village reader can reach them.
    ///
    /// NOT "landless" — every one of these owns settlements (159 between them in
    /// <c>TAOM_Map/ModuleData/settlements.xml</c>), and all 159 are <c>&lt;Hideout&gt;</c>. Every
    /// exemption built on this list rests on that, and each needs to stay true:
    ///
    /// <list type="bullet">
    /// <item><c>RebellionsCampaignBehavior</c> reads <c>settlement.Culture.RebelliousHeroTemplates</c>
    /// for a town or castle that revolts. A hideout never revolts.</item>
    /// <item><c>CompanionRolesCampaignBehavior</c> reads <c>companionHero.Culture.LordTemplates</c>
    /// when a companion is granted a fief. TAOM ships 208 wanderers across 18 cultures and none of
    /// them is one of these eight, so no companion can carry one of these cultures.</item>
    /// <item><c>DefaultSettlementPatrolModel.CanSettlementHavePatrolParties</c> requires
    /// <c>settlement.IsTown</c>, <c>VillagerCampaignBehavior</c> requires a village, and
    /// <c>CaravansCampaignBehavior.SpawnCaravan</c> requires a town notable. None of the three can
    /// reach a hideout-only culture, so its party templates are never read.</item>
    /// </list>
    ///
    /// Give any of them a town and every exemption is void: the lists become empty
    /// <c>MBReadOnlyList</c>s, <c>GetRandomElement</c> indexes straight into them, and an unset
    /// party template dereferences as null. The cross-module guards that would notice are
    /// <c>tools/validate_moduledata.py</c>'s <c>LANDLESS_CULTURE</c> check and
    /// <c>build_settled_cultures</c> in <c>tools/taom_schema.py</c>.
    ///
    /// Distinct from <c>Patch65_LandlessCultureSpawnGuard</c>'s sense of "landless" (a culture
    /// owning NO settlement, which makes vanilla <c>SpawnLordParty</c>'s unguarded
    /// <c>Settlement.All.First(culture)</c> throw). That guard decides at runtime via
    /// <c>AnySettlementHasHeroCulture</c> and keeps no list. Two different questions, do not merge
    /// the vocabularies.
    /// </summary>
    public static readonly HashSet<string> HideoutOnlyCultures = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "dunland_raiders", "rhun_raiders", "harad_raiders", "gundabad_raiders",
        "umbar_corsairs", "gondor_soldiers", "erebor_warriors", "mirkwood_stalkers"
    };

    /// <summary>Walks up from the test working directory to <c>Main/_Module/ModuleData</c>.</summary>
    public static string ModuleDataPath()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            var candidate = Path.Combine(dir, "Main", "_Module", "ModuleData");
            if (Directory.Exists(candidate))
                return candidate;
            dir = Directory.GetParent(dir)?.FullName;
        }

        Assert.Fail("Could not locate Main/_Module/ModuleData from the test working directory.");
        return null;
    }

    /// <summary>Strips the <c>PartyTemplate.</c> / <c>NPCCharacter.</c> style prefix from a reference.</summary>
    public static string StripPrefix(string reference)
    {
        var dot = reference.IndexOf('.');
        return dot < 0 ? reference : reference.Substring(dot + 1);
    }
}
