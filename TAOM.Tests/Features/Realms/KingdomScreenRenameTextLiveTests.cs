using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Realms;

namespace TAOM.Tests.Features.Realms;

/// <summary>
/// KingdomNames knows a player's kingdom screen rename by the raw text of Native's str_generic_kingdom_name (#1316). If
/// a game update changes that text, a player's name would quietly lose to title names again, so this reads the
/// installed file.
/// </summary>
[TestClass]
[TestCategory("LiveInstall")]
public class KingdomScreenRenameTextLiveTests
{
    private const string DefaultGameDir = @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord";

    [TestMethod]
    public void NativeGenericKingdomName_IsTheTextKingdomNamesLooksFor()
    {
        var path = new[] { Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR"), DefaultGameDir }
            .Where(root => !string.IsNullOrEmpty(root))
            .Select(root => Path.Combine(root!, "Modules", "Native", "ModuleData", "module_strings.xml"))
            .FirstOrDefault(File.Exists);
        if (path == null)
            Assert.Inconclusive("Native module_strings.xml is not installed here (set BANNERLORD_GAME_DIR).");

        var text = XDocument.Load(path).Descendants("string")
            .Where(s => (string?)s.Attribute("id") == "str_generic_kingdom_name")
            .Select(s => (string?)s.Attribute("text"))
            .SingleOrDefault();

        Assert.IsNotNull(text, "Native no longer defines str_generic_kingdom_name; KingdomManagementVM's rename changed.");
        Assert.IsTrue(KingdomNames.RenamedByPlayer(text, hasVariables: true),
            $"Native's str_generic_kingdom_name is now \"{text}\"; update KingdomNames.KingdomScreenText.");
    }
}
