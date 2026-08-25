using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.BattleBalance;

/// <summary>
/// Assembly-wide ban on calling <c>PartyBase.get_Owner</c>. The getter is a throwing computed
/// property: for a non-mobile (settlement) party it returns <c>Settlement.Owner</c>, which is
/// <c>OwnerClan.Leader</c> with no null guard — and <c>Settlement.OwnerClan</c> is null for any
/// settlement that is neither Village, Town, nor Hideout (TAOM_Map's <c>retirement_retreat</c>
/// CustomSettlementComponent settlement). A <c>?.</c> on the result guards nothing because the
/// throw happens inside the getter (.claude/rules/adapters.md, issue #281 family).
///
/// This shipped as a deterministic new-campaign CTD in v2.0.8.0 (crash 0b462fd8): the settlement
/// daily tick fed <c>settlement.Party</c> into <c>TaomPartyHealingModel.GetDailyHealingHpForHeroes</c>,
/// which resolved the career-passive hero via <c>party?.Owner</c>. Safe replacement:
/// <c>CareerPassiveHero.ResolveId</c> (<c>MobileParty?.Owner ?? LeaderHero</c>).
///
/// The scan walks raw IL bytes via <see cref="IlCallScanner"/> (not Harmony's
/// <c>PatchProcessor.ReadMethodBody</c>, which throws <c>NotSupportedException</c> on generic
/// method definitions — 37 of them in TAOM.dll) so every method body in the assembly is covered.
/// </summary>
[TestClass]
public class PartyOwnerGetterBanTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestMethod]
    public void TaomAssembly_AllMethodBodies_NeverCallPartyBaseOwnerGetter()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var bannedGetter = typeof(PartyBase).GetProperty(nameof(PartyBase.Owner))?.GetGetMethod();
        Assert.IsNotNull(bannedGetter, "PartyBase.Owner no longer exists on the installed engine — retire this ban test.");

        var violations = IlCallScanner.FindCallers(
            typeof(TAOM.IoC).Assembly,
            target => IlCallScanner.SameMethod(target, bannedGetter),
            out var unreadable,
            out var scanned);

        if (scanned < 1000)
            Assert.Inconclusive($"Only {scanned} method bodies scanned (expected thousands) — assembly enumeration problem, not a genuine pass.");

        if (violations.Count > 0 || unreadable.Count > 0)
        {
            var sb = new StringBuilder();
            if (violations.Count > 0)
            {
                sb.AppendLine($"{violations.Count} method(s) call PartyBase.get_Owner — a throwing computed getter (NREs for settlements with null OwnerClan, crash 0b462fd8).");
                sb.AppendLine("Resolve the hero via CareerPassiveHero.ResolveId / party.MobileParty?.Owner instead:");
                foreach (var v in violations.Distinct()) sb.AppendLine("  " + v);
            }
            if (unreadable.Count > 0)
            {
                sb.AppendLine($"{unreadable.Count} method bodies could not be read — the ban cannot vouch for them:");
                foreach (var u in unreadable.Take(20)) sb.AppendLine("  " + u);
            }
            Assert.Fail(sb.ToString());
        }
    }
}
