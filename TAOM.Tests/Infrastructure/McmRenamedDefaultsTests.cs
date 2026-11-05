using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// #1017: MCM loads the saved value by property name over the compiled default, so a default changed
/// under an unchanged name reached nobody who had saved settings. Each row is a setting renamed so the
/// intended default applies; the old name must stay gone, or a saved value would load again.
/// </summary>
[TestClass]
public class McmRenamedDefaultsTests
{
    [TestMethod]
    public void RenamedSettings_OldNameGone_NewNameHasTheIntendedDefault()
    {
        object[][] rows =
        {
            new object[] { "BanditMaxPartiesPerHideout", "BanditPartiesPerHideoutCap", 6 },
            new object[] { "ChargeAutoKnockdownWeightRatio", "ChargeAutoKnockdownRatio", 6 },
            new object[] { "EnableFormationPresets", "EnableOobFormationPresets", false },
            new object[] { "EnableSmartCavalryAI", "EnableSmartCavalryCharge", false },
            new object[] { "Phase1TriggerDay", "WarPhase1TriggerDay", 30 },
            new object[] { "Phase2TriggerDay", "WarPhase2TriggerDay", 44 },
            new object[] { "FieldCommissionMeritThreshold", "FieldCommissionMeritToPromote", 32 },
            new object[] { "FieldCommissionMaxOffersPerBattle", "FieldCommissionOffersPerBattle", 2 },
            new object[] { "BanditInitialHideoutsPerFaction", "BanditStartingHideoutsPerFaction", 7 },
            new object[] { "AiLordPartySizeFactor", "AiLordPartyScale", 1f },
            new object[] { "AiLordPartySizeFlatBonus", "AiLordPartyFlatBonus", 0f },
            new object[] { "AiGarrisonSizeFactor", "AiGarrisonScale", 1f },
            new object[] { "AiFoodConsumptionRelief", "AiPartyFoodRelief", 0f },
            new object[] { "AiWageRelief", "AiPartyWageRelief", 0f },
        };
        var settings = new TaomSettings();
        var problems = new System.Collections.Generic.List<string>();
        foreach (var row in rows)
        {
            string oldName = (string)row[0], newName = (string)row[1];
            if (typeof(TaomSettings).GetProperty(oldName) != null)
                problems.Add($"{oldName} still exists");
            PropertyInfo renamed = typeof(TaomSettings).GetProperty(newName);
            if (renamed == null)
                problems.Add($"{newName} missing");
            else if (!Equals(renamed.GetValue(settings), row[2]))
                problems.Add($"{newName} default is {renamed.GetValue(settings)}, expected {row[2]}");
        }
        Assert.AreEqual(0, problems.Count, string.Join("; ", problems));
    }
}
