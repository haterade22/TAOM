using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Infrastructure.Localization;

/// <summary>The string ids vanilla's own data declares, read from the installed game (Inconclusive without it).</summary>
internal static class VanillaLocalizationKeys
{
    private const int MinimumLanguageFileIds = 10000;
    private const int MinimumInlineKeys = 5000;

    private static readonly string[] Modules = { "Native", "SandBoxCore", "SandBox", "StoryMode", "CustomBattle" };

    private static readonly Regex InlineKey = new Regex(@"\{=([^}!*][^}]*)\}", RegexOptions.Compiled);
    private static readonly Regex RowId = new Regex("<string\\s+id=\"([^\"]+)\"", RegexOptions.Compiled);

    private static string GameDir()
    {
        string? env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        return string.IsNullOrWhiteSpace(env) ? @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord" : env;
    }

    private static readonly Regex VanillaShape = new Regex(@"\A[0-9A-Za-z]{8}\z", RegexOptions.Compiled);

    /// <summary>True when the key has the 8-character shape of a vanilla string id, the install-free rule for an English-only override.</summary>
    public static bool HasVanillaShape(string key) => VanillaShape.IsMatch(key);

    /// <summary>The row ids of each module's root-level English <c>Languages/std_*.xml</c>, which hold the keys vanilla writes in DLL code alone.</summary>
    private static HashSet<string> LanguageFileIds()
    {
        var game = GameDir();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var module in Modules)
        {
            var languages = Path.Combine(game, "Modules", module, "ModuleData", "Languages");
            if (!Directory.Exists(languages))
                continue;
            // Top level only: the language subfolders (DE, TR and the rest) hold translations, not English.
            foreach (var file in Directory.EnumerateFiles(languages, "std_*.xml", SearchOption.TopDirectoryOnly))
            {
                foreach (Match m in RowId.Matches(File.ReadAllText(file)))
                    ids.Add(m.Groups[1].Value);
            }
        }
        if (ids.Count < MinimumLanguageFileIds)
            Assert.Inconclusive($"Only {ids.Count} vanilla English language ids found under {game}; the install is missing or partial.");
        return ids;
    }

    /// <summary>The inline <c>{=KEY}</c> keys of vanilla's data XML plus <see cref="LanguageFileIds"/>.</summary>
    public static HashSet<string> All()
    {
        var game = GameDir();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var module in Modules)
        {
            var dir = Path.Combine(game, "Modules", module, "ModuleData");
            if (!Directory.Exists(dir))
                continue;
            foreach (var file in Directory.EnumerateFiles(dir, "*.xml", SearchOption.AllDirectories)
                         .Where(f => !f.Split(Path.DirectorySeparatorChar).Contains("Languages")))
            {
                foreach (Match m in InlineKey.Matches(File.ReadAllText(file)))
                    keys.Add(m.Groups[1].Value);
            }
        }
        if (keys.Count < MinimumInlineKeys)
            Assert.Inconclusive($"Only {keys.Count} vanilla keys found under {game}; the install is missing or partial.");
        keys.UnionWith(LanguageFileIds());
        return keys;
    }
}
