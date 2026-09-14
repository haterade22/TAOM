using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Migration;

/// <summary>
/// Every element name in a TAOM prefab must still be something the installed engine can build.
///
/// <para>
/// Gauntlet resolves an element by its bare name: first as a widget class from the loaded
/// assemblies, then as a prefab by basename from every module's <c>GUI/Prefabs</c> tree. A name that
/// resolves as neither fails the movie at load, and nothing offline says so: the file is well-formed
/// XML, the C# compiler never sees it, and the clone shadows the vanilla file that would have worked.
/// </para>
///
/// <para>
/// Bannerlord v1.5.2 renamed <c>BoolStateChangerWidget</c> to <c>BoolStateChangerBrushWidget</c>.
/// Four TAOM clones (the party screen, both party nameplates, the mission agent status HUD) still
/// named the old type, and <c>PrefabExtensionBindingTests</c> could not see it because it checks
/// XPaths, not element types. This gate is the sibling that does.
/// </para>
/// </summary>
[TestClass]
public class PrefabElementTypeBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    // Element names that are prefab grammar, not widgets.
    private static readonly HashSet<string> Structural = new HashSet<string>(StringComparer.Ordinal)
    {
        "Prefab", "Constants", "Constant", "Parameters", "Parameter", "Variables", "Variable",
        "VisualDefinitions", "VisualDefinition", "VisualState", "Window", "Children", "ItemTemplate",
        "Extensions", "Extension", "Templates", "Template", "Styles", "Style", "Layer", "Layers",
        "LayoutDefinitions",
    };

    private static readonly string[] VanillaGuiModules =
        { "Native", "SandBoxCore", "SandBox", "StoryMode", "CustomBattle", "Multiplayer" };

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void EveryPrefabElement_ResolvesToAWidgetTypeOrAPrefab()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable: " + string.Join("; ", GameAssemblies.Diagnostics));

        var widgetTypes = WidgetTypeNames();
        Assert.IsTrue(widgetTypes.Contains("Widget") && widgetTypes.Contains("ListPanel"),
            "The widget type index is missing the Gauntlet base types; the assembly scan is wrong.");

        var prefabs = PrefabNames();
        Assert.IsTrue(prefabs.Count > 0, "No prefabs indexed.");

        var offenders = new List<string>();
        var repoDir = RepoPrefabsDir();
        foreach (var file in Directory.GetFiles(repoDir, "*.xml", SearchOption.AllDirectories))
        {
            foreach (var name in ElementNames(file))
            {
                if (Structural.Contains(name) || widgetTypes.Contains(name) || prefabs.Contains(name)) continue;
                offenders.Add($"{file.Substring(repoDir.Length + 1)}: <{name}>");
            }
        }

        Assert.AreEqual(0, offenders.Count,
            "TAOM prefabs name elements the installed engine can neither build as a widget nor find as a "
            + "prefab; the movie fails at load and the clone hides the vanilla file that would have worked:"
            + Environment.NewLine + string.Join(Environment.NewLine, offenders.Distinct()));
    }

    private static IEnumerable<string> ElementNames(string file)
    {
        // XmlReader skips comment text, which is where stray widget names legitimately live.
        var names = new HashSet<string>(StringComparer.Ordinal);
        using (var reader = XmlReader.Create(file, new XmlReaderSettings { IgnoreComments = true, IgnoreWhitespace = true, DtdProcessing = DtdProcessing.Ignore }))
        {
            while (reader.Read())
                if (reader.NodeType == XmlNodeType.Element) names.Add(reader.LocalName);
        }
        return names;
    }

    private static HashSet<string> WidgetTypeNames()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var simple = assembly.GetName().Name ?? string.Empty;
            if (simple.IndexOf("GauntletUI", StringComparison.Ordinal) < 0 && simple != "TAOM") continue;
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }
            foreach (var type in types)
                if (type.IsClass && !type.IsAbstract) names.Add(type.Name);
        }
        return names;
    }

    private static HashSet<string> PrefabNames()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var modulesRoot = Path.Combine(GameAssemblies.GameDir, "Modules");
        foreach (var module in VanillaGuiModules)
            AddPrefabsFrom(names, Path.Combine(modulesRoot, module, "GUI", "Prefabs"));
        AddPrefabsFrom(names, RepoPrefabsDir());
        return names;
    }

    private static void AddPrefabsFrom(ISet<string> names, string guiRoot)
    {
        if (!Directory.Exists(guiRoot)) return;
        foreach (var file in Directory.GetFiles(guiRoot, "*.xml", SearchOption.AllDirectories))
            names.Add(Path.GetFileNameWithoutExtension(file));
    }

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
}
