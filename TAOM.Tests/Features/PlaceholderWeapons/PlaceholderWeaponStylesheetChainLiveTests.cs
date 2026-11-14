using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Core;
using TAOM.Tests.Infrastructure;

// docs/features/placeholder-weapons.md: the engine builds a crafted weapon only from pieces its WeaponDescription
// (AvailablePieces) and CraftingTemplate (UsablePieces) list. LOTRLOME_Armory's weapon_descriptions.xslt and
// crafting_templates.xslt REPLACE those lists, so TAOM's placeholder sheets must run after the Armory's
// (SubModule.xml: DependedModuleMetadata LOTRLOME_Armory LoadBeforeThis). These tests run that chain on the
// installed files, Native, then the Armory, then TAOM, and check every taom_ph_ blade is listed and every
// piece the Armory listed is kept. Inconclusive where the Armory is not installed.

namespace TAOM.Tests.Features.PlaceholderWeapons;

[TestClass]
[TestCategory("LiveInstall")]
public class PlaceholderWeaponStylesheetChainLiveTests
{
    private const string Tag = "taom_ph_";

    private static string PlaceholderFolder => RepoPaths.RepoPath("Main", "_Module", "ModuleData", "placeholder_weapons");

    private static string GameDir()
    {
        string? env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        return string.IsNullOrWhiteSpace(env) ? @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord" : env;
    }

    private static XDocument Apply(string stylesheet, XDocument input)
    {
        var output = new XDocument();
        using (var reader = input.CreateReader())
        using (var writer = output.CreateWriter())
            CompiledXslt.Get(stylesheet, XsltLoadStyle.IgnoreDtdNoResolver).Transform(reader, writer);
        return output;
    }

    private static List<string> PlaceholderBlades() =>
        XDocument.Load(Path.Combine(PlaceholderFolder, "ph_crafting_pieces.xml")).Descendants("CraftingPiece")
            .Select(p => (string)p.Attribute("id")!).ToList();

    // (owner id, piece id) for every listed piece, so a piece moved to another description counts as lost.
    private static List<string> Listed(XDocument doc, string owner, string piece, string idAttribute) =>
        doc.Descendants(piece)
            .Select(p => $"{(string?)p.Ancestors(owner).First().Attribute("id")}/{(string?)p.Attribute(idAttribute)}")
            .ToList();

    private static void AssertChainKeepsArmoryAndAddsEveryBlade(string nativeFile, string sheet, string owner, string piece, string idAttribute)
    {
        string native = Path.Combine(GameDir(), "Modules", "Native", "ModuleData", nativeFile);
        string armory = Path.Combine(GameDir(), "Modules", "LOTRLOME_Armory", "ModuleData", sheet + ".xslt");
        if (!File.Exists(native) || !File.Exists(armory))
            Assert.Inconclusive($"Native {nativeFile} or LOTRLOME_Armory {sheet}.xslt is not installed here; the chain cannot be run.");

        // Native's XML, then the Armory's stylesheet, then TAOM's: the documented launcher order.
        XDocument afterArmory = Apply(armory, XDocument.Load(native));
        XDocument final = Apply(Path.Combine(PlaceholderFolder, "ph_" + sheet + ".xslt"), afterArmory);
        var armoryListed = Listed(afterArmory, owner, piece, idAttribute);
        var finalListed = Listed(final, owner, piece, idAttribute);

        var lost = armoryListed.Except(finalListed).ToList();
        Assert.AreEqual(0, lost.Count, $"TAOM's ph_{sheet}.xslt drops pieces the Armory lists: {string.Join(", ", lost.Take(10))}");

        var listedPieceIds = new HashSet<string>(finalListed.Select(e => e.Substring(e.IndexOf('/') + 1)));
        var missing = PlaceholderBlades().Where(b => !listedPieceIds.Contains(b)).ToList();
        Assert.AreEqual(0, missing.Count,
            $"After Native, LOTRLOME_Armory and TAOM, no {owner} lists these {Tag} blades, so their weapons get no usage: {string.Join(", ", missing)}");
        Assert.IsTrue(finalListed.Count > armoryListed.Count, $"ph_{sheet}.xslt added no {piece} entry.");
    }

    [TestMethod]
    public void WeaponDescriptions_NativeThenArmoryThenTaom_ListEveryPlaceholderBladeAndKeepArmoryPieces() =>
        AssertChainKeepsArmoryAndAddsEveryBlade("weapon_descriptions.xml", "weapon_descriptions", "WeaponDescription", "AvailablePiece", "id");

    [TestMethod]
    public void CraftingTemplates_NativeThenArmoryThenTaom_ListEveryPlaceholderBladeAndKeepArmoryPieces() =>
        AssertChainKeepsArmoryAndAddsEveryBlade("crafting_templates.xml", "crafting_templates", "CraftingTemplate", "UsablePiece", "piece_id");
}
