using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.XPath;
using Bannerlord.UIExtenderEx.Attributes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Migration;

/// <summary>
/// Binding gate for every UIExtenderEx <c>[PrefabExtension]</c> XPath in TAOM.
///
/// <para>
/// UIExtenderEx resolves each extension with <c>SelectSingleNode</c> and, on null, calls
/// <c>DisplayUserError</c>: a <c>Trace.TraceError</c> plus one red in-game message at movie load.
/// That is loud in principle and invisible in practice, because it fires once during campaign load
/// amid the message flood, throws nothing, and leaves no persistent artifact. Offline it cannot be
/// seen at all. An <c>InsertType.Replace</c> extension whose XPath misses is a pure silent no-op.
/// </para>
///
/// <para>
/// v1.5.0 proved the gap was load-bearing: it rewrote <c>MapBar.xml</c> and moved
/// <c>HintWidget</c> from the outer wrapper to an inner sibling, so
/// <c>SpecialResourceIconPrefab</c>'s path matched nothing and special-resource map-bar icons
/// silently stopped installing. Build, full suite and <c>BindingVerification</c> were all green.
/// </para>
///
/// <para>
/// Resolution note that matters for correctness: Gauntlet resolves prefabs by BASENAME with
/// last-module-wins (<c>WidgetFactory.GetPrefabNamesAndPathsFromCurrentPath</c> keys on
/// <c>Path.GetFileNameWithoutExtension</c>), and UIExtenderEx patches the movie AFTER that
/// resolution. So where TAOM ships its own clone of a prefab, the XPath must be tested against
/// TAOM's copy, not vanilla's.
/// </para>
/// </summary>
[TestClass]
public class PrefabExtensionBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static Assembly TaomAssembly => typeof(TAOM.IoC).Assembly;

    // Vanilla modules that ship Gauntlet prefabs, plus TAOM's own GUI tree. TAOM is searched LAST
    // so its clones win, matching the engine's last-module-wins basename resolution.
    private static readonly string[] VanillaGuiModules =
        { "Native", "SandBoxCore", "SandBox", "StoryMode", "CustomBattle", "Multiplayer" };

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void EveryPrefabExtension_XPath_ResolvesAgainstTheWinningPrefab()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable: " + string.Join("; ", GameAssemblies.Diagnostics));

        var index = BuildPrefabIndex();
        Assert.IsTrue(index.Count > 0, "No prefabs discovered; the prefab index is empty.");

        var failures = new StringBuilder();
        var checkedCount = 0;

        foreach (var type in TaomAssembly.GetTypes())
        {
            foreach (var attr in type.GetCustomAttributes<PrefabExtensionAttribute>(inherit: false))
            {
                if (string.IsNullOrEmpty(attr.XPath)) continue; // whole-movie extension, nothing to resolve
                checkedCount++;

                if (!index.TryGetValue(attr.Movie, out var path))
                {
                    failures.AppendLine($"{type.Name}: movie '{attr.Movie}' not found in any module.");
                    continue;
                }

                int matches;
                try
                {
                    var doc = new XmlDocument();
                    doc.Load(path);
                    matches = doc.CreateNavigator().Select(attr.XPath!).Count;
                }
                catch (Exception ex)
                {
                    failures.AppendLine($"{type.Name}: XPath threw against {path}: {ex.Message}");
                    continue;
                }

                if (matches == 0)
                    failures.AppendLine(
                        $"{type.Name}: XPath matched 0 nodes in {path}{Environment.NewLine}"
                        + $"    xpath: {attr.XPath}{Environment.NewLine}"
                        + $"    first step that drops to zero: {FirstFailingStep(path, attr.XPath!)}");
            }
        }

        Assert.IsTrue(checkedCount > 0, "No [PrefabExtension] attributes with an XPath were discovered.");
        Assert.AreEqual(0, failures.Length,
            $"UIExtenderEx prefab extensions that no longer resolve (each is a SILENT no-op in game):"
            + Environment.NewLine + failures);
    }

    // Splits the XPath into progressive prefixes and reports the first step whose match count hits
    // zero. That is what turns "something moved" into "HintWidget is no longer the wrapper".
    private static string FirstFailingStep(string prefabPath, string xpath)
    {
        XPathNavigator nav;
        try
        {
            var doc = new XmlDocument();
            doc.Load(prefabPath);
            nav = doc.CreateNavigator();
        }
        catch { return "(could not reload prefab)"; }

        var steps = xpath.Split('/');
        var prefix = string.Empty;
        for (var i = 0; i < steps.Length; i++)
        {
            if (steps[i].Length == 0) continue;
            prefix = i == 0 ? steps[i] : prefix + "/" + steps[i];
            int n;
            try { n = nav.Select(prefix).Count; }
            catch { return $"'{prefix}' (invalid expression)"; }
            if (n == 0) return $"'{prefix}' matches 0";
        }
        return "(no single step failed; the full expression matched 0)";
    }

    // basename -> full path, vanilla modules first then TAOM, so TAOM's clones overwrite and win.
    private static Dictionary<string, string> BuildPrefabIndex()
    {
        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var modulesRoot = Path.Combine(GameAssemblies.GameDir, "Modules");

        // GUI/Prefabs ONLY. Brushes live under GUI/Brushes, are a different resource type, and are
        // NOT part of the prefab basename index. Including them makes TAOM's Brushes/MapBar.xml
        // shadow SandBox's Prefabs/Map/MapBar.xml and produces a wall of false positives.
        foreach (var module in VanillaGuiModules)
            AddPrefabsFrom(index, Path.Combine(modulesRoot, module, "GUI", "Prefabs"));

        // TAOM last, so its basename clones shadow vanilla's exactly as the engine does at runtime.
        //
        // Read the REPOSITORY copy, not the deployed module. The prescribed build for this repo is
        // non-deploying (-p:DisableModuleCopy=true -p:ModuleId=), so the installed
        // Modules/TAOM/GUI/Prefabs tree lags the checkout by however long it has been since someone
        // ran a deploying build. Testing against it is a FALSE GREEN: a broken XPath in the source
        // prefab passes because the stale deployed copy still has the old structure. Verified: the
        // deployed PartyNameplateItem.xml had zero BloodFeud references while the checkout had five.
        AddPrefabsFrom(index, RepoPrefabsDir());
        return index;
    }

    // Main/_Module/GUI/Prefabs, found by walking up from the test working directory the same way
    // CultureDataFixture locates ModuleData.
    private static string RepoPrefabsDir()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            var candidate = Path.Combine(dir, "Main", "_Module", "GUI", "Prefabs");
            if (Directory.Exists(candidate)) return candidate;
            dir = Directory.GetParent(dir)?.FullName;
        }
        Assert.Fail("Could not locate Main/_Module/GUI/Prefabs from the test working directory.");
        return null;
    }

    private static void AddPrefabsFrom(IDictionary<string, string> index, string guiRoot)
    {
        if (!Directory.Exists(guiRoot)) return;
        foreach (var file in Directory.GetFiles(guiRoot, "*.xml", SearchOption.AllDirectories))
            index[Path.GetFileNameWithoutExtension(file)] = file;
    }
}
