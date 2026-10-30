using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Migration;

/// <summary>
/// A TAOM prefab that shadows a vanilla prefab must keep every widget reference the vanilla file
/// declares.
///
/// <para>
/// A Gauntlet widget class reaches its children through properties the prefab binds by attribute:
/// <c>ShipBannerContainerWidget="ShipBannerContainerWidget\..."</c> on the root element sets a
/// <c>Widget</c>-typed property, and the widget's update code then dereferences it. TAOM ships
/// clones of vanilla prefabs, and a clone replaces the vanilla file entirely, so an attribute the
/// clone never declares leaves the property null. The engine does not guard those: on v1.5.0
/// <c>PartyNameplateWidget.UpdateNameplatesVisibility</c> dereferenced <c>BloodFeudIconWidget</c>
/// every frame, and on v1.5.3 the same method plus <c>OnUpdate</c> dereference
/// <c>ShipBannerContainerWidget</c> and <c>ShipBannerWidget</c>. Each engine bump can add another,
/// and nothing offline notices: the XML is well-formed, the element types resolve, the C# compiler
/// never sees a prefab.
/// </para>
///
/// <para>
/// The rule is deliberately narrow. Only attributes whose name is a property of Widget-derived type
/// on that element's widget class count; a value attribute TAOM drops or changes (a size, a brush,
/// a visibility) is a design choice and is not checked. The one non-Widget attribute that counts is
/// <c>DataSource</c>: a clone that drops a binding the vanilla element declares loses its hover hint
/// or panel (#1051, <c>UsedHorsesHint</c> on the troop manager popup); only its presence is checked,
/// not its value. Elements are paired root to root and then by <c>Id</c>, and only when the clone
/// keeps the same element type. An element with no <c>Id</c> (the popup's <c>DataSource="{..}"</c>
/// text) and a prefab-instance tag are not checked, and a repeated <c>Id</c> pairs every vanilla
/// occurrence with the clone's first element of that <c>Id</c> (skipped when their types differ), so
/// the clone's later occurrences are never checked. The engine resolves a <c>DataSource</c> by the
/// ancestor chain, not by <c>Id</c>.
/// </para>
/// </summary>
[TestClass]
public class PrefabCloneWidgetReferenceTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static readonly string[] VanillaGuiModules =
        { "Native", "SandBoxCore", "SandBox", "StoryMode", "CustomBattle", "Multiplayer", "BirthAndDeath" };

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void EveryShadowedVanillaWidgetReference_SurvivesInTheTaomClone()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable: " + string.Join("; ", GameAssemblies.Diagnostics));

        var widgetTypes = WidgetTypesByName();
        Assert.IsTrue(widgetTypes.ContainsKey("Widget"), "The widget type index is missing the Gauntlet base type; the assembly scan is wrong.");

        var pairs = ShadowPairs();
        var offenders = pairs
            .SelectMany(p => MissingWidgetReferences(p.vanilla, p.clone, widgetTypes).Select(gap => $"{p.relative} (shadows {p.module}): {gap}"))
            .ToList();

        Assert.AreEqual(0, offenders.Count,
            "TAOM prefab clones drop widget references the vanilla prefab declares; the widget's own update "
            + "code dereferences those properties unguarded, so the clone throws where the vanilla file did not "
            + "(for a dropped DataSource, the hint or panel it feeds binds nothing). "
            + "Re-base the clone on the installed vanilla file and re-apply TAOM's edits (a clone with no TAOM edit is deleted instead):"
            + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    // A copy with no TAOM change still replaces the vanilla file by name, so it only hides what a later engine update
    // changes there (#1031, #1051): delete it and let vanilla load.
    [TestMethod]
    [TestCategory("LiveInstall")]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void NoTaomPrefab_IsAnUnchangedCopyOfTheVanillaFileItShadows()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game install unavailable: " + string.Join("; ", GameAssemblies.Diagnostics));

        var pairs = ShadowPairs();
        var copies = pairs
            .Where(p => XNode.DeepEquals(AsTheEngineReadsIt(p.clone), AsTheEngineReadsIt(p.vanilla)))
            .Select(p => $"{p.relative} (same as {p.module})")
            .ToList();

        Assert.AreEqual(0, copies.Count,
            "TAOM prefabs identical to the vanilla file they replace; delete them:" + Environment.NewLine + string.Join(Environment.NewLine, copies));
    }

    // Gauntlet resolves a prefab by basename across every module's GUI/Prefabs tree, so a clone shadows the vanilla file
    // of the same name wherever either of them sits.
    private static List<(string clone, string relative, string module, string vanilla)> ShadowPairs()
    {
        var repoDir = RepoPrefabsDir();
        var vanillaByBasename = VanillaPrefabsByBasename();
        var pairs = new List<(string, string, string, string)>();
        foreach (var clone in Directory.GetFiles(repoDir, "*.xml", SearchOption.AllDirectories))
        {
            if (!vanillaByBasename.TryGetValue(Path.GetFileName(clone), out var shadowed)) continue;
            foreach (var (module, vanilla) in shadowed)
                pairs.Add((clone, clone.Substring(repoDir.Length + 1), module, vanilla));
        }
        Assert.IsTrue(pairs.Count > 0, "No TAOM prefab shadows a vanilla prefab; the pairing walk found nothing.");
        return pairs;
    }

    // The engine's prefab reader ignores comments and layout whitespace and keeps attribute values exactly (WidgetPrefab.LoadFrom).
    private static XDocument AsTheEngineReadsIt(string path)
    {
        var doc = XDocument.Load(path, LoadOptions.None);
        doc.DescendantNodes().OfType<XComment>().Remove();
        return doc;
    }

    private static IEnumerable<string> MissingWidgetReferences(string vanillaPath, string clonePath, IReadOnlyDictionary<string, Type> widgetTypes)
    {
        var vanillaRoot = RootWidget(vanillaPath);
        var cloneRoot = RootWidget(clonePath);
        if (vanillaRoot == null || cloneRoot == null) yield break;

        var cloneById = cloneRoot.DescendantsAndSelf()
            .Where(e => e.Attribute("Id") != null)
            .GroupBy(e => e.Attribute("Id").Value, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        foreach (var vanillaElement in vanillaRoot.DescendantsAndSelf())
        {
            XElement twin;
            string where;
            if (vanillaElement == vanillaRoot)
            {
                twin = cloneRoot;
                where = "root <" + vanillaRoot.Name.LocalName + ">";
            }
            else
            {
                var id = vanillaElement.Attribute("Id")?.Value;
                if (id == null || !cloneById.TryGetValue(id, out twin)) continue;
                where = "<" + vanillaElement.Name.LocalName + " Id=\"" + id + "\">";
            }
            if (twin.Name.LocalName != vanillaElement.Name.LocalName) continue;
            if (!widgetTypes.TryGetValue(vanillaElement.Name.LocalName, out var widgetType)) continue;

            foreach (var attribute in vanillaElement.Attributes())
            {
                if (attribute.Name.LocalName != "DataSource" && !IsWidgetReferenceProperty(widgetType, attribute.Name.LocalName)) continue;
                if (twin.Attribute(attribute.Name.LocalName) != null) continue;
                yield return $"{where} lacks {attribute.Name.LocalName}=\"{attribute.Value}\"";
            }
        }
    }

    private static bool IsWidgetReferenceProperty(Type widgetType, string attributeName)
    {
        var property = widgetType.GetProperty(attributeName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
        if (property == null) return false;
        var widgetBase = widgetType;
        while (widgetBase != null && widgetBase.Name != "Widget") widgetBase = widgetBase.BaseType;
        return widgetBase != null && widgetBase.IsAssignableFrom(property.PropertyType);
    }

    // The first element under <Window>; a prefab with no <Window> (a template-only file) is skipped.
    private static XElement RootWidget(string path)
    {
        var doc = XDocument.Load(path, LoadOptions.None);
        var window = doc.Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "Window");
        return window?.Elements().FirstOrDefault();
    }

    private static Dictionary<string, List<(string module, string path)>> VanillaPrefabsByBasename()
    {
        var index = new Dictionary<string, List<(string, string)>>(StringComparer.OrdinalIgnoreCase);
        var modulesRoot = Path.Combine(GameAssemblies.GameDir, "Modules");
        foreach (var module in VanillaGuiModules)
        {
            var root = Path.Combine(modulesRoot, module, "GUI", "Prefabs");
            if (!Directory.Exists(root)) continue;
            foreach (var file in Directory.GetFiles(root, "*.xml", SearchOption.AllDirectories))
            {
                if (!index.TryGetValue(Path.GetFileName(file), out var list)) index[Path.GetFileName(file)] = list = new List<(string, string)>();
                list.Add((module, file));
            }
        }
        return index;
    }

    private static Dictionary<string, Type> WidgetTypesByName()
    {
        // TAOM.Client loads lazily: touch it so the front-end widgets that moved there (ADR-013) are in the AppDomain.
        _ = typeof(TAOM.Client.ClientSubModule).Assembly;
        var types = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var simple = assembly.GetName().Name ?? string.Empty;
            if (simple.IndexOf("GauntletUI", StringComparison.Ordinal) < 0 && simple != "TAOM" && simple != "TAOM.Client") continue;
            Type[] loaded;
            try { loaded = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { loaded = ex.Types.Where(t => t != null).ToArray(); }
            foreach (var type in loaded)
                if (type.IsClass && !type.IsAbstract && !types.ContainsKey(type.Name)) types[type.Name] = type;
        }
        return types;
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
