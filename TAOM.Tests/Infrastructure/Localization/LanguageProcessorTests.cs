using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Infrastructure.Localization;

/// <summary>
/// <c>LanguageData.Deserialize</c> assigns <c>TextProcessor</c> from <c>text_processor</c> whenever <c>name</c> is non-empty,
/// so a module's <c>language_data.xml</c> with a <c>name</c> and no processor resets the language to a default that prints
/// no grammar tokens (#1026).
/// </summary>
[TestClass]
public class LanguageProcessorTests
{
    private const string Ns = "TaleWorlds.Localization.TextProcessor.LanguageProcessors.";

    /// <summary>The Languages folder's own language_data.xml, which declares English.</summary>
    private const string EnglishRootDir = "";

    private static readonly Dictionary<string, string> NativeProcessorByTaomDir = new()
    {
        [EnglishRootDir] = Ns + "EnglishTextProcessor",
        ["DE"] = Ns + "GermanTextProcessor",
        ["FR"] = Ns + "FrenchTextProcessor",
        ["IT"] = Ns + "ItalianTextProcessor",
        ["PL"] = Ns + "PolishTextProcessor",
        ["RU"] = Ns + "RussianTextProcessor",
        ["SP"] = Ns + "SpanishTextProcessor",
        ["TR"] = Ns + "TurkishTextProcessor",
    };

    private static readonly string[] AllTaomDirs =
        { EnglishRootDir, "BR", "CNs", "CNt", "DE", "FR", "IT", "JP", "KO", "PL", "RU", "SP", "TR" };

    private static string TaomLanguages => RepoPath("Main", "_Module", "ModuleData", "Languages");

    private static XElement Root(string dir) =>
        XDocument.Load(Path.Combine(TaomLanguages, dir, "language_data.xml")).Root!;

    private static string Label(string dir) => dir.Length == 0 ? "Languages" : "Languages/" + dir;

    private static string ModulesDir()
    {
        var env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        var game = string.IsNullOrWhiteSpace(env) ? @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord" : env;
        return Path.Combine(game, "Modules");
    }

    private static Dictionary<string, XElement> NativeByLanguageId()
    {
        var nativeDir = Path.Combine(ModulesDir(), "Native", "ModuleData", "Languages");
        if (!Directory.Exists(nativeDir))
            Assert.Inconclusive($"No Native language files at {nativeDir}.");
        var native = Directory.EnumerateFiles(nativeDir, "language_data.xml", SearchOption.AllDirectories)
            .Select(f => XDocument.Load(f).Root!)
            .Where(r => r.Attribute("id") != null)
            .ToDictionary(r => (string)r.Attribute("id")!, r => r);
        if (native.Count < 10)
            Assert.Inconclusive($"Only {native.Count} Native language_data.xml files found; the install is partial.");
        return native;
    }

    [TestMethod]
    public void TaomLanguageDataFiles_PinnedNativeTable_DeclareTheSameProcessor()
    {
        foreach (var dir in AllTaomDirs)
        {
            var root = Root(dir);
            var name = (string?)root.Attribute("name");
            var processor = (string?)root.Attribute("text_processor");
            if (NativeProcessorByTaomDir.TryGetValue(dir, out var expected))
            {
                Assert.AreEqual((string?)root.Attribute("id"), name,
                    $"{Label(dir)}/language_data.xml must carry name equal to its id: an empty name makes the engine skip text_processor");
                Assert.AreEqual(expected, processor,
                    $"{Label(dir)}/language_data.xml must declare Native's text_processor, or a name-bearing file resets it");
            }
            else
            {
                Assert.IsNull(processor, $"{Label(dir)}/language_data.xml: Native declares no processor for this language");
            }
        }
    }

    [TestMethod]
    public void TaomLanguageDataFiles_InstalledNative_DeclareTheSameNameAndProcessor()
    {
        var native = NativeByLanguageId();
        foreach (var dir in AllTaomDirs)
        {
            var taom = Root(dir);
            var id = (string)taom.Attribute("id")!;
            Assert.IsTrue(native.TryGetValue(id, out var nat), $"Native declares no language with id '{id}' ({Label(dir)})");
            var natProcessor = (string?)nat!.Attribute("text_processor");
            var taomName = (string?)taom.Attribute("name");
            var taomProcessor = (string?)taom.Attribute("text_processor");
            if (string.IsNullOrEmpty(natProcessor))
            {
                Assert.IsTrue(string.IsNullOrEmpty(taomProcessor),
                    $"{Label(dir)}: Native declares no processor, so TAOM must not either");
                continue;
            }
            Assert.AreEqual((string?)nat.Attribute("name"), taomName, $"{Label(dir)}: name differs from Native's");
            Assert.AreEqual(natProcessor, taomProcessor, $"{Label(dir)}: text_processor differs from Native's");
        }
    }
}
