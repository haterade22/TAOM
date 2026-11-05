using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LocalizationOverride;
using TAOM.Tests.Core;
using TAOM.Tests.Infrastructure.Localization;

namespace TAOM.Tests.Features.LocalizationOverride;

[TestClass]
public class LocalizationOverrideLoaderTests
{
    [TestMethod]
    public void ParseOverrides_ExtractsVanillaIdAndOverrideText()
    {
        // Arrange
        string xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<strings>
    <string id=""taom_the_fix_0cdXddA9"" text=""{=0cdXddA9}{KINGDOM1_LINK} has formed an alliance with {KINGDOM2_LINK}."" />
</strings>";

        // Act
        var overrides = LocalizationOverrideLoader.ParseOverrides(xml);

        // Assert
        Assert.AreEqual(1, overrides.Count);
        Assert.AreEqual("{KINGDOM1_LINK} has formed an alliance with {KINGDOM2_LINK}.", overrides["0cdXddA9"]);
    }

    [TestMethod]
    public void ParseOverrides_IgnoresEntriesWithTaomPrefixedIds()
    {
        // Arrange — entries whose {=ID} starts with "taom_" are TAOM's own strings, not overrides
        string xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<strings>
    <string id=""str_faction_official.empire"" text=""{=taom_str_faction_official.empire}a clansman of Dunland"" />
</strings>";

        // Act
        var overrides = LocalizationOverrideLoader.ParseOverrides(xml);

        // Assert
        Assert.AreEqual(0, overrides.Count);
    }

    [TestMethod]
    public void ParseOverrides_IgnoresEntriesWithoutLocalizationToken()
    {
        // Arrange
        string xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<strings>
    <string id=""some_string"" text=""Plain text without token"" />
</strings>";

        // Act
        var overrides = LocalizationOverrideLoader.ParseOverrides(xml);

        // Assert
        Assert.AreEqual(0, overrides.Count);
    }

    [TestMethod]
    public void ParseOverrides_IgnoresSpecialMarkers()
    {
        // Arrange
        string xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<strings>
    <string id=""dynamic_text"" text=""{=!}Some dynamic text"" />
    <string id=""wildcard_text"" text=""{=*}Some wildcard text"" />
</strings>";

        // Act
        var overrides = LocalizationOverrideLoader.ParseOverrides(xml);

        // Assert
        Assert.AreEqual(0, overrides.Count);
    }

    [TestMethod]
    public void ParseOverrides_MultipleEntries_AllParsed()
    {
        // Arrange
        string xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<strings>
    <string id=""taom_the_fix_0cdXddA9"" text=""{=0cdXddA9}{KINGDOM1_LINK} has formed an alliance with {KINGDOM2_LINK}."" />
    <string id=""taom_the_fix_1rbg3Hz2"" text=""{=1rbg3Hz2}{FACTION_1} and {FACTION_2} are no longer enemies."" />
    <string id=""str_faction_official.empire"" text=""{=taom_str_faction_official.empire}a clansman of Dunland"" />
</strings>";

        // Act
        var overrides = LocalizationOverrideLoader.ParseOverrides(xml);

        // Assert
        Assert.AreEqual(2, overrides.Count);
        Assert.AreEqual("{KINGDOM1_LINK} has formed an alliance with {KINGDOM2_LINK}.", overrides["0cdXddA9"]);
        Assert.AreEqual("{FACTION_1} and {FACTION_2} are no longer enemies.", overrides["1rbg3Hz2"]);
    }

    [TestMethod]
    public void ParseOverrides_EmptyXml_ReturnsEmptyDictionary()
    {
        // Arrange
        string xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<strings>
</strings>";

        // Act
        var overrides = LocalizationOverrideLoader.ParseOverrides(xml);

        // Assert
        Assert.AreEqual(0, overrides.Count);
    }

    [TestMethod]
    public void ParseOverridesFromFile_ShippedModuleStrings_YieldsTheVanillaKeyOverrides()
    {
        // The English rows of taom_module_strings.xml are the whole feature for the vanilla keys that carry no
        // language row (VanillaKeyOverrideTests): losing one silently restores vanilla's English. Losing a
        // convoy registration row is worse without NavalDLC: on the installed v1.4.8 only that module declares the GameText ids
        // str_convoy_party_name and str_armed_convoy_party_name (the two translation keys, LjUhEJxz and l4pRw7pO, are vanilla's).

        // Arrange + Act
        var overrides = LocalizationOverrideLoader.ParseOverridesFromFile(
            Path.Combine(CultureDataFixture.ModuleDataPath(), "taom_module_strings.xml"));
        var vanillaShaped = overrides.Keys.Where(VanillaLocalizationKeys.HasVanillaShape).ToList();

        // Assert
        Assert.IsTrue(vanillaShaped.Count >= 181,
            $"Only {vanillaShaped.Count} vanilla-shaped overrides load from the shipped file; 181 ship.");
        Assert.AreEqual("Call {CALLED_KINGDOM} to War Against {KINGDOM_TO_CALL_TO_WAR_AGAINST}", overrides["0CsbbwtN"]);
        CollectionAssert.IsSubsetOf(new[] { "LjUhEJxz", "l4pRw7pO" }, overrides.Keys.ToList());
    }
}
