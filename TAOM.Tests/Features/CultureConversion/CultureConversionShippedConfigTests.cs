using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CultureConversion;

namespace TAOM.Tests.Features.CultureConversion;

/// <summary>
/// Pins the SHIPPED <c>culture_conversion_config.json</c> against the compiled defaults.
///
/// Why this exists: two separate things looked like they already covered it, and neither did.
/// <c>lint_docs.py</c>'s config-drift check only compares fenced ```json blocks in a feature doc,
/// and this feature documents its config as a markdown table, so its <c>config_drift: 0</c> is a
/// vacuous pass. <c>CultureConversionConfigProviderTests</c> writes its own fixtures into a temp
/// directory and never opens the shipped file. So a key could be renamed, misspelled or given a
/// value that disagrees with the compiled default, and nothing would say so.
///
/// The failure that matters is silent: Newtonsoft ignores an unknown key and leaves the property at
/// its compiled default, so a typo in the shipped JSON reads in game as "the setting does nothing"
/// with no parse error anywhere. Same shape as the v1.4.7 <c>banner_color_config.json</c> finding.
/// </summary>
[TestClass]
public class CultureConversionShippedConfigTests
{
    private static string ShippedConfigPath()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TAOM.sln")))
            dir = dir.Parent;
        Assert.IsNotNull(dir, "TAOM.sln not found walking upward from cwd");

        var path = Path.Combine(dir!.FullName, "Main", "_Module", "ModuleData",
            "culture_conversion", "culture_conversion_config.json");
        Assert.IsTrue(File.Exists(path), $"Shipped config not found at {path}");
        return path;
    }

    [TestMethod]
    public void ShippedConfig_ParsesAndMatchesTheCompiledDefaults()
    {
        var parsed = JsonConvert.DeserializeObject<CultureConversionConfig>(File.ReadAllText(ShippedConfigPath()));
        Assert.IsNotNull(parsed, "Shipped config did not deserialize.");

        var defaults = new CultureConversionConfig();
        Assert.AreEqual(defaults.Enabled, parsed!.Enabled, nameof(defaults.Enabled));
        Assert.AreEqual(defaults.RequiredHoldDays, parsed.RequiredHoldDays, nameof(defaults.RequiredHoldDays));
        Assert.AreEqual(defaults.RequireStableLoyalty, parsed.RequireStableLoyalty, nameof(defaults.RequireStableLoyalty));
        Assert.AreEqual(defaults.MinLoyaltyToConvert, parsed.MinLoyaltyToConvert, 0.0001f, nameof(defaults.MinLoyaltyToConvert));
        Assert.AreEqual(defaults.ConvertPlayerOwnedSettlements, parsed.ConvertPlayerOwnedSettlements, nameof(defaults.ConvertPlayerOwnedSettlements));
        Assert.AreEqual(defaults.ReplaceNotablesOnConversion, parsed.ReplaceNotablesOnConversion, nameof(defaults.ReplaceNotablesOnConversion));
        Assert.AreEqual(defaults.ReplaceGarrisonOnConversion, parsed.ReplaceGarrisonOnConversion, nameof(defaults.ReplaceGarrisonOnConversion));
        Assert.AreEqual(defaults.ReplaceMilitiaOnConversion, parsed.ReplaceMilitiaOnConversion, nameof(defaults.ReplaceMilitiaOnConversion));
        Assert.AreEqual(defaults.ReplaceGarrisonInPlayerFiefs, parsed.ReplaceGarrisonInPlayerFiefs, nameof(defaults.ReplaceGarrisonInPlayerFiefs));
    }

    [TestMethod]
    public void ShippedConfig_NamesEveryConfigProperty_AndNothingElse()
    {
        // The half the value comparison above cannot see: a renamed or misspelled key still
        // deserializes to the compiled default, so the values agree while the setting is inert.
        var json = JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string, object>>(
            File.ReadAllText(ShippedConfigPath()));
        Assert.IsNotNull(json);

        var declared = typeof(CultureConversionConfig).GetProperties();
        foreach (var property in declared)
        {
            Assert.IsTrue(
                json!.Keys.Any(k => string.Equals(k, property.Name, StringComparison.OrdinalIgnoreCase)),
                $"Shipped config has no key for '{property.Name}'. Newtonsoft would leave it at its "
                + "compiled default and the player's edit would do nothing.");
        }

        foreach (var key in json!.Keys)
        {
            Assert.IsTrue(
                declared.Any(p => string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase)),
                $"Shipped config carries '{key}', which matches no property on CultureConversionConfig. "
                + "Newtonsoft ignores it silently, so this key does nothing.");
        }
    }
}
