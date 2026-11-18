using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem.ViewModelCollection;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// Patching a method can run its type's static initializer. <c>CampaignUIHelper</c>'s initializer calls
/// <c>GameTexts.FindText</c> (v1.5.7 CampaignUIHelper.cs:210), and <c>GameTexts</c> is null until <c>Game.Initialize</c>.
/// Applied from <c>OnSubModuleLoad</c>, the initializer throws and .NET keeps the type failed for the whole process
/// (docs/reviews/lessons/harmony-il.md, "A UIExtenderEx mixin compiles vanilla code at Register"). Patch163 and Patch165
/// did this on 2026-11-18. Such a category belongs in the once-per-process block of <c>OnGameInitializationFinished</c>.
/// </summary>
[TestClass]
public class EarlyPatchStaticInitializerGateTests
{
    // Types whose static initializer needs a running game. Add one when its decompile shows the same dependency.
    private static readonly Type[] NeedsAGame = { typeof(CampaignUIHelper) };

    private static bool _gameLoaded;

    // The patch attributes name game types (SandBox, the View assemblies), which load from the installed game.
    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = TAOM.Tests.Migration.GameAssemblies.EnsureLoaded();

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void OnSubModuleLoad_AppliesNoCategoryThatPatchesATypeWhoseInitializerNeedsAGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Bannerlord install not found: " + string.Join("; ", TAOM.Tests.Migration.GameAssemblies.Diagnostics));
        var body = MethodBody(File.ReadAllText(Path.Combine(FindRepoRoot(), "Main", "SubModule.cs")), "protected override void OnSubModuleLoad()");
        var categories = new HashSet<string>(Regex.Matches(body, "TryPatchCategory\\(\"([^\"]+)\"\\)").Cast<Match>().Select(m => m.Groups[1].Value));
        Assert.IsTrue(categories.Count > 20, $"found only {categories.Count} categories in OnSubModuleLoad: the body parse broke");

        var offenders = new List<string>();
        foreach (var type in TAOM.Tests.Migration.HarmonyPatchBindingTests.DiscoverPatchTypes(out _))
        {
            var category = type.GetCustomAttribute<HarmonyPatchCategory>()?.info.category
                           ?? type.DeclaringType?.GetCustomAttribute<HarmonyPatchCategory>()?.info.category;
            if (category == null || !categories.Contains(category))
                continue;
            var (declaringType, methodName, _, _) = TAOM.Tests.Migration.HarmonyPatchBindingTests.MergeSpec(type);
            if (NeedsAGame.Contains(declaringType))
                offenders.Add($"{category}: {type.FullName} patches {declaringType!.Name}.{methodName}");
        }

        Assert.AreEqual(0, offenders.Count,
            "Apply these categories in OnGameInitializationFinished's once-per-process block instead:\n" + string.Join("\n", offenders));
    }

    private static string MethodBody(string source, string signature)
    {
        int start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, signature + " not found in Main/SubModule.cs");
        int open = source.IndexOf('{', start);
        int depth = 0;
        for (int i = open; i < source.Length; i++)
        {
            if (source[i] == '{')
                depth++;
            else if (source[i] == '}' && --depth == 0)
                return source.Substring(open, i - open + 1);
        }
        throw new InvalidOperationException("unbalanced braces after " + signature);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TAOM.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new FileNotFoundException("TAOM.sln not found walking upward from cwd");
    }
}
