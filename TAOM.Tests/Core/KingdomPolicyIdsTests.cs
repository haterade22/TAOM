using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Core;

/// <summary>
/// Every policy a kingdom or a culture lists must be one the engine defines. Kingdom.Deserialize looks a
/// kingdom's policy up by exact id and drops a miss without a message (Kingdom.cs:796-807, v1.5.4), so a
/// dead id ships silently: all 15 TAOM kingdoms listed two (#756), and vanilla's own misspelt
/// policy_land_grants_for_veterans was Gondor's only kingdom row, so Gondor started with only its
/// culture's Senate. A culture's dead default policy is worse: CultureObject.Deserialize adds the null
/// (CultureObject.cs:376-377) and the Policies screen reads its name.
/// </summary>
[TestClass]
public class KingdomPolicyIdsTests
{
    // DefaultPolicies.RegisterAll, v1.5.5, re-read 2026-10-09 (TaleWorlds.CampaignSystem.DefaultPolicies; `pwsh tools/taom-src.ps1
    // path TaleWorlds.CampaignSystem.DefaultPolicies`). The engine's own spelling, typo included:
    // policy_land_grands_for_veteran. Re-read on an engine bump (/engine-bump lists this set).
    private static readonly HashSet<string> EnginePolicyIds = new()
    {
        "policy_bailiffs", "policy_cantons", "policy_castle_charters", "policy_citizenship",
        "policy_council_of_the_commons", "policy_crown_duty", "policy_debasement_of_the_currency",
        "policy_feudal_inheritance", "policy_forgiveness_of_debts", "policy_grazing_rights",
        "policy_hunting_rights", "policy_imperial_towns", "policy_land_grands_for_veteran", "policy_land_tax",
        "policy_lawspeakers", "policy_lords_privy_council", "policy_magistrates", "policy_marshals",
        "policy_military_coronae", "policy_noble_retinues", "policy_precarial_land_tenure", "policy_road_tolls",
        "policy_royal_commissions", "policy_royal_guard", "policy_royal_privilege", "policy_sacred_majesty",
        "policy_senate", "policy_serfdom", "policy_state_monopolies", "policy_trial_by_jury",
        "policy_tribunes_of_the_people", "policy_war_tax",
    };

    [DataTestMethod]
    [DataRow("taom_spkingdoms.xml")]
    [DataRow("spkingdoms.xslt")]
    [DataRow("taom_spcultures.xml")]
    [DataRow("spcultures.xslt")]
    public void EveryKingdomPolicy_IsOneTheEngineDefines(string file)
    {
        var text = File.ReadAllText(Path.Combine(CultureDataFixture.ModuleDataPath(), file));
        var ids = Regex.Matches(text, @"<policy\s+id=""([^""]+)""").Cast<Match>().Select(m => m.Groups[1].Value).ToList();

        var dead = ids.Where(id => !EnginePolicyIds.Contains(id)).Distinct().ToList();

        Assert.IsTrue(ids.Count > 0, $"{file}: no <policy> rows read");
        Assert.AreEqual(0, dead.Count, $"{file}: policy ids the engine does not define: {string.Join(", ", dead)}");
    }
}
