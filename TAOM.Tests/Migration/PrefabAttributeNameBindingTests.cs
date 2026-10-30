using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Migration;

/// <summary>
/// Every attribute on a TAOM prefab element must name a property the installed engine can set.
///
/// <para>
/// Gauntlet applies each attribute through <c>WidgetExtensions.GetObjectAndProperty</c>: an exact-case
/// public instance property, with a public setter. A misspelt name (<c>VertialAlignment</c>) or a
/// field is dropped without a log line, and a get-only property throws a caught NRE into
/// <c>FailedAssert</c>. Only property types the string setter converts (WidgetExtensions.cs:56-122) reach that
/// caught NRE; others, such as <c>LayoutImp</c> (<c>ILayout</c>, protected setter), are ignored silently.
/// Nothing offline says so, which is how <c>AcceptEvents</c> (no such property)
/// sat on three buttons. <c>PrefabElementTypeBindingTests</c> is the sibling that checks element names.
/// </para>
///
/// <para>
/// A tag resolves to a prefab before a widget type, and a prefab tag's attributes land on that
/// prefab's root widget: the first element under <c>&lt;Window&gt;</c>. The resolver here mirrors that.
/// Key attributes (<c>Id</c>, <c>DataSource</c>, <c>Parameter.*</c>, <c>Command.*</c>,
/// <c>CommandParameter.*</c>) never reach a property and are skipped.
/// </para>
/// </summary>
[TestClass]
public class PrefabAttributeNameBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    // Vanilla ships these (type, attribute) pairs in its own prefabs, and the engine tolerates them
    // (no setter, so a caught failure, or a silent drop). The rule applied: in a shipped prefab,
    // misspellings and dotted paths that name nothing are deleted even where vanilla carries them
    // (VertialAlignment, VerticalAlinment), and so is AlwaysRefreshAll: no engine type has it, and TAOM's
    // re-synced popup ListPanel drops it although vanilla still carries it at SandBox
    // PartyTroopManagerPopUp.xml:77 (#1051). Other names vanilla carries on the same widget type are
    // allowlisted below.
    // Paths are relative to the game's Modules folder.
    private static readonly HashSet<string> VanillaParityAllowlist = new HashSet<string>(StringComparer.Ordinal)
    {
        // SandBox/GUI/Prefabs/Encyclopedia/EncyclopediaSubPages/EncyclopediaUnitTreeNodeItem.xml:13
        "EncyclopediaUnitTreeNodeItemBrushWidget.ButtonContainer",
        // Native/GUI/Prefabs/GameMenu/GameMenuItem.xml:2
        "GameMenuItemWidget.ProgressBar",
        "GameMenuItemWidget.ProgressTextWidget",
        // SandBox/GUI/Prefabs/Encyclopedia/EncyclopediaSubPages/EncyclopediaHeroPage.xml:171
        "GridWidget.AutoScrollTopOffset",
        "GridWidget.AutoScrollBottomOffset",
        // Native/GUI/Prefabs/Conversation/SPConversation.xml:46
        "GridWidget.LayoutImp",
        // Native/GUI/Prefabs/BannerBuilder/BannerBuilderScreen.xml:278
        "NavigatableGridWidget.LayoutImp",
        // Native/GUI/Prefabs/CharacterCreation/CharacterCreationGainedProperties.xml:48
        "ListPanel.ForcePixelPerfectPlacement",
        // Native/GUI/Prefabs/BannerBuilder/BannerBuilderScreen.xml:55
        "MaskedTextureWidget.HideWhenNull",
        // Native/GUI/Prefabs/FaceGen/FaceGenBody.xml:90
        "NavigatableListPanel.ChildTarget",
        // Native/GUI/Prefabs/FaceGen/FaceGenBody.xml:67
        "NumericUpDownWidget.IsFlipUpButtonHorizontal",
        // SandBox/GUI/Prefabs/Nameplate/PartyNameplateItem.xml:13
        "PartyNameplateWidget.Brush",
        // SandBox/GUI/Prefabs/Nameplate/PartyPlayerNameplateItem.xml:15
        "PartyPlayerNameplateWidget.Brush",
        // SandBox/GUI/Prefabs/Party/PartyScreen.xml:232
        "PartyQuestProgressWidget.Brush",
        // SandBox/GUI/Prefabs/Party/PartyScreen.xml:104
        "PartyScreenWidget.FirstUpgradeButton",
        "PartyScreenWidget.SecondUpgradeButton",
        "PartyScreenWidget.NumOfUpgrades",
        // SandBox/GUI/Prefabs/Nameplate/SettlementNameplateItemLarge.xml:128
        "QuestMarkerBrushWidget.MarkerScale",
        // Native/GUI/Prefabs/GameMenu/GameMenuTournamentLeaderboard.xml:75
        "ScrollablePanel.MouseScrollAxis",
        // SandBox/GUI/Prefabs/Nameplate/SettlementNameplateItemLarge.xml:49
        "SettlementNameplateItemWidget.SettlementNameplateCapsuleWidget",
        // SandBox/GUI/Prefabs/Nameplate/SettlementNameplateItemSmall.xml:49
        "SettlementNameplateItemWidget.SettlementNameplateQuestWidget",
        // SandBox/GUI/Prefabs/Party/PartyTroopTuple.xml:162
        "Widget.HueFactor",
        // Native/GUI/Prefabs/BannerEditor/BannerEditor.xml:13
        "Widget.WidgetToAddIcons",
        // Native/GUI/Prefabs/Standard/Standard.DropdownWithHorizontalControl.xml:68
        "Widget.WidgetToCopyHeightFrom",
    };

    // Element names that are prefab grammar, not widgets.
    private static readonly HashSet<string> Structural = new HashSet<string>(StringComparer.Ordinal)
    {
        "Prefab", "Constants", "Constant", "Parameters", "Parameter", "Variables", "Variable",
        "VisualDefinitions", "VisualDefinition", "VisualState", "Window", "Children", "ItemTemplate",
        "Extensions", "Extension", "Templates", "Template", "Styles", "Style", "Layer", "Layers",
        "LayoutDefinitions", "CustomElements", "LogicalChildrenLocation",
    };

    // Load order: later modules shadow earlier ones, and TAOM's own prefabs shadow them all.
    private static readonly string[] VanillaGuiModules =
        { "Native", "SandBoxCore", "SandBox", "StoryMode", "CustomBattle", "Multiplayer", "BirthAndDeath", "NavalDLC" };

    // Resolver unit tests: pure, no game needed.

    [TestMethod]
    public void ResolveAttribute_ExactPublicSetter_IsSettable()
        => Assert.AreEqual(AttributeResolution.Settable, PrefabAttributeResolver.ResolveAttribute(typeof(FakeWidget), "Settable"));

    [TestMethod]
    public void ResolveAttribute_WrongCase_IsUnknown()
        => Assert.AreEqual(AttributeResolution.Unknown, PrefabAttributeResolver.ResolveAttribute(typeof(FakeWidget), "settable"));

    [TestMethod]
    public void ResolveAttribute_PublicField_IsUnknown()
        => Assert.AreEqual(AttributeResolution.Unknown, PrefabAttributeResolver.ResolveAttribute(typeof(FakeWidget), "AField"));

    [TestMethod]
    public void ResolveAttribute_PrivateSetter_IsNotSettable()
        => Assert.AreEqual(AttributeResolution.NotSettable, PrefabAttributeResolver.ResolveAttribute(typeof(FakeWidget), "PrivateSet"));

    [TestMethod]
    public void ResolveAttribute_DottedConcreteSegment_ChecksSecondSegment()
    {
        Assert.AreEqual(AttributeResolution.Settable, PrefabAttributeResolver.ResolveAttribute(typeof(FakeWidget), "Inner.Value"));
        Assert.AreEqual(AttributeResolution.Unknown, PrefabAttributeResolver.ResolveAttribute(typeof(FakeWidget), "Inner.Missing"));
        Assert.AreEqual(AttributeResolution.NotSettable, PrefabAttributeResolver.ResolveAttribute(typeof(FakeWidget), "Inner.ReadOnly"));
        Assert.AreEqual(AttributeResolution.Unknown, PrefabAttributeResolver.ResolveAttribute(typeof(FakeWidget), "Nope.Value"));
    }

    [TestMethod]
    public void ResolveAttribute_DottedInterfaceSegment_ChecksTheKnownRuntimeType()
    {
        Type Known(Type widget, string property) => property == "Layout" ? typeof(FakeInner) : null;
        Assert.AreEqual(AttributeResolution.Settable, PrefabAttributeResolver.ResolveAttribute(typeof(FakeWidget), "Layout.Value", Known));
        Assert.AreEqual(AttributeResolution.Unknown, PrefabAttributeResolver.ResolveAttribute(typeof(FakeWidget), "Layout.Missing", Known));
        Assert.AreEqual(AttributeResolution.NotSettable, PrefabAttributeResolver.ResolveAttribute(typeof(FakeWidget), "Layout.ReadOnly", Known));
    }

    [TestMethod]
    public void ResolveAttribute_UnknownInterfaceSegment_Fails()
    {
        Assert.AreEqual(AttributeResolution.RuntimeTypeUnknown, PrefabAttributeResolver.ResolveAttribute(typeof(FakeWidget), "Layout.Anything", (w, p) => null));
        Assert.AreEqual(AttributeResolution.RuntimeTypeUnknown, PrefabAttributeResolver.ResolveAttribute(typeof(FakeWidget), "AbstractPart.Anything", (w, p) => null));
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    public void ResolveAttribute_GridLayoutImpMisspelledSuffix_IsUnknown()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable: " + string.Join("; ", GameAssemblies.Diagnostics));
        var grid = typeof(TaleWorlds.GauntletUI.BaseTypes.GridWidget);
        Assert.AreEqual(AttributeResolution.Settable, PrefabAttributeResolver.ResolveAttribute(grid, "LayoutImp.HorizontalLayoutMethod"));
        Assert.AreEqual(AttributeResolution.Unknown, PrefabAttributeResolver.ResolveAttribute(grid, "LayoutImp.HorizontalLayoutMethd"));
        var list = typeof(TaleWorlds.GauntletUI.BaseTypes.ListPanel);
        Assert.AreEqual(AttributeResolution.Settable, PrefabAttributeResolver.ResolveAttribute(list, "LayoutImp.LayoutMethod"));
        Assert.AreEqual(AttributeResolution.Unknown, PrefabAttributeResolver.ResolveAttribute(list, "LayoutImp.LayoutMethd"));
        // StackLayout has LayoutMethod only; the grid-only name is not a ListPanel property.
        Assert.AreEqual(AttributeResolution.Unknown, PrefabAttributeResolver.ResolveAttribute(list, "LayoutImp.VerticalLayoutMethod"));
    }

    [TestMethod]
    public void ResolveAttribute_ThreeSegments_IsUnknown()
        => Assert.AreEqual(AttributeResolution.Unknown, PrefabAttributeResolver.ResolveAttribute(typeof(FakeWidget), "Inner.Value.More"));

    [TestMethod]
    public void IsKeyTypeAttribute_IdDataSourceParameterCommand_AreSkipped()
    {
        foreach (var key in new[] { "Id", "DataSource", "Parameter.Foo", "Command.Click", "CommandParameter.Click" })
            Assert.IsTrue(PrefabAttributeResolver.IsKeyTypeAttribute(key), key);
        foreach (var plain in new[] { "id", "Parameters", "Commands", "Brush", "CommandX.Click" })
            Assert.IsFalse(PrefabAttributeResolver.IsKeyTypeAttribute(plain), plain);
    }

    [TestMethod]
    public void RootWidgetType_PrefabShadowsBuiltinType_UsesPrefabRoot()
    {
        var types = new Dictionary<string, Type> { ["Foo"] = typeof(FakeWidget), ["Bar"] = typeof(OtherFakeWidget) };
        var prefabs = new Dictionary<string, string> { ["Foo"] = "<Prefab><Window><Bar Id=\"x\" /></Window></Prefab>" };
        Assert.AreEqual(typeof(OtherFakeWidget), PrefabAttributeResolver.RootWidgetType("Foo", prefabs, types));
        Assert.AreEqual(typeof(FakeWidget), PrefabAttributeResolver.RootWidgetType("Bar", new Dictionary<string, string>(), new Dictionary<string, Type> { ["Bar"] = typeof(FakeWidget) }));
    }

    [TestMethod]
    public void RootWidgetType_BareWindowRoot_Resolves()
    {
        var types = new Dictionary<string, Type> { ["Bar"] = typeof(OtherFakeWidget) };
        var prefabs = new Dictionary<string, string> { ["Foo"] = "<Window><Bar /></Window>" };
        Assert.AreEqual(typeof(OtherFakeWidget), PrefabAttributeResolver.RootWidgetType("Foo", prefabs, types));
    }

    [TestMethod]
    public void RootWidgetType_CommentBeforeRoot_SkipsIt()
    {
        var types = new Dictionary<string, Type> { ["Bar"] = typeof(OtherFakeWidget), ["Baz"] = typeof(FakeWidget) };
        var prefabs = new Dictionary<string, string>
        {
            ["Foo"] = "<Prefab><!-- <Baz /> --><Window><!-- <Baz /> --><Bar /></Window></Prefab>",
        };
        Assert.AreEqual(typeof(OtherFakeWidget), PrefabAttributeResolver.RootWidgetType("Foo", prefabs, types));
    }

    [TestMethod]
    public void RootWidgetType_SelfReferencingPrefab_IsUnresolved()
    {
        var types = new Dictionary<string, Type> { ["Foo"] = typeof(FakeWidget) };
        var prefabs = new Dictionary<string, string>
        {
            ["Foo"] = "<Window><Foo /></Window>",
            ["A"] = "<Window><B /></Window>",
            ["B"] = "<Window><A /></Window>",
        };
        Assert.IsNull(PrefabAttributeResolver.RootWidgetType("Foo", prefabs, types));
        Assert.IsNull(PrefabAttributeResolver.RootWidgetType("A", prefabs, types));
    }

    private static List<string> VisualProblems(string xml)
    {
        var result = new ScanResult();
        foreach (var element in XDocument.Parse(xml, LoadOptions.SetLineInfo).Descendants()
                     .Where(e => e.Name.LocalName == "VisualDefinition" || e.Name.LocalName == "VisualState"))
            CheckVisualAttributes(result, element, element.Name.LocalName, "synthetic.xml", ((IXmlLineInfo)element).LineNumber);
        return result.Problems;
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    public void CheckVisualAttributes_VisualDefinition_AcceptsOnlyTheFiveReadNames()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable: " + string.Join("; ", GameAssemblies.Diagnostics));

        var bad = VisualProblems("<VisualDefinition Name=\"A\" EaseIn=\"true\" TransitionDuration=\"0.4\" />");
        Assert.AreEqual(1, bad.Count, string.Join("; ", bad));
        StringAssert.Contains(bad[0], "EaseIn");

        Assert.AreEqual(0, VisualProblems(
            "<VisualDefinition Name=\"A\" TransitionDuration=\"0.4\" EaseType=\"EaseIn\" EaseFunction=\"Quad\" DelayOnBegin=\"0.1\" />").Count);
        Assert.AreEqual(0, VisualProblems(
            "<VisualDefinition Name=\"A\" EaseType=\"EaseOut\" EaseFunction=\"Quint\" />").Count);

        // The loader parses these with Enum.TryParse and no ignoreCase, and keeps Linear/Sine on a miss.
        var badType = VisualProblems("<VisualDefinition Name=\"A\" EaseType=\"Easeout\" />");
        Assert.AreEqual(1, badType.Count, string.Join("; ", badType));
        StringAssert.Contains(badType[0], "Easeout");
        var badFunction = VisualProblems("<VisualDefinition Name=\"A\" EaseFunction=\"Quintic\" />");
        Assert.AreEqual(1, badFunction.Count, string.Join("; ", badFunction));
        StringAssert.Contains(badFunction[0], "Quintic");
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    public void CheckVisualAttributes_VisualState_NeedsAPublicSettableProperty()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable: " + string.Join("; ", GameAssemblies.Diagnostics));

        var misspelt = VisualProblems("<VisualState State=\"Default\" PositionXOfset=\"3\" />");
        Assert.AreEqual(1, misspelt.Count, string.Join("; ", misspelt));
        StringAssert.Contains(misspelt[0], "PositionXOfset");

        // VisualState.GotPositionXOffset is { get; private set; }.
        var privateSetter = VisualProblems("<VisualState State=\"Default\" GotPositionXOffset=\"true\" />");
        Assert.AreEqual(1, privateSetter.Count, string.Join("; ", privateSetter));
        StringAssert.Contains(privateSetter[0], "GotPositionXOffset");

        Assert.AreEqual(0, VisualProblems("<VisualState State=\"Default\" PositionXOffset=\"3\" />").Count);
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void EveryPrefabAttribute_NamesASettableWidgetProperty()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable: " + string.Join("; ", GameAssemblies.Diagnostics));

        var scan = Scan();
        var offenders = scan.Problems
            .Concat(scan.BadUses.Where(u => !VanillaParityAllowlist.Contains(u.Key)).Select(u => u.Describe()))
            .ToList();

        Assert.AreEqual(0, offenders.Count,
            "TAOM prefabs carry attributes the engine cannot apply (no exact-case public property with a public "
            + "setter, a dotted path past two segments, or an element it cannot resolve). In a shipped prefab, "
            + "delete it: players have never seen it applied. In markup you are writing, fix the name and look in game. "
            + "If vanilla itself carries a correctly spelt pair, add it to VanillaParityAllowlist with the vanilla "
            + "file:line:" + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void VanillaParityAllowlist_EveryEntryIsStillUsed()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable: " + string.Join("; ", GameAssemblies.Diagnostics));

        var seen = new HashSet<string>(Scan().BadUses.Select(u => u.Key), StringComparer.Ordinal);
        var stale = VanillaParityAllowlist.Where(k => !seen.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();

        Assert.AreEqual(0, stale.Count,
            "These allowlist entries match no TAOM prefab use any more; remove them so the list stays exact:"
            + Environment.NewLine + string.Join(Environment.NewLine, stale));
    }

    private sealed class BadUse
    {
        public string TypeName;
        public string Attribute;
        public AttributeResolution Resolution;
        public string File;
        public int Line;
        public string Key => TypeName + "." + Attribute;
        public string Describe() => $"{File}:{Line} <{TypeName}> {Attribute} ({Resolution})";
    }

    private sealed class ScanResult
    {
        public readonly List<BadUse> BadUses = new List<BadUse>();
        public readonly List<string> Problems = new List<string>();
    }

    private static ScanResult Scan()
    {
        var widgetTypes = new Dictionary<string, Type>(StringComparer.Ordinal);
        var ambiguous = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in WidgetTypes())
        {
            if (widgetTypes.TryGetValue(type.Name, out var existing) && existing != type) ambiguous.Add(type.Name);
            widgetTypes[type.Name] = type;
        }
        Assert.IsTrue(widgetTypes.ContainsKey("Widget") && widgetTypes.ContainsKey("ListPanel"),
            "The widget type index is missing the Gauntlet base types; the assembly scan is wrong.");

        var repoDir = RepoPrefabsDir();
        var prefabXml = new Dictionary<string, string>(StringComparer.Ordinal);
        var modulesRoot = Path.Combine(GameAssemblies.GameDir, "Modules");
        foreach (var module in VanillaGuiModules)
            AddPrefabsFrom(prefabXml, Path.Combine(modulesRoot, module, "GUI", "Prefabs"));
        AddPrefabsFrom(prefabXml, repoDir);
        Assert.IsTrue(prefabXml.Count > 0, "No prefabs indexed.");

        var result = new ScanResult();
        foreach (var file in Directory.GetFiles(repoDir, "*.xml", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
        {
            var rel = file.Substring(repoDir.Length + 1).Replace('\\', '/');
            XDocument doc;
            try { doc = XDocument.Load(file, LoadOptions.SetLineInfo); }
            catch (XmlException ex) { result.Problems.Add($"{rel}: not well-formed ({ex.Message})"); continue; }

            foreach (var element in doc.Descendants())
            {
                var tag = element.Name.LocalName;
                var line = ((IXmlLineInfo)element).LineNumber;
                if (tag == "VisualDefinition" || tag == "VisualState")
                {
                    CheckVisualAttributes(result, element, tag, rel, line);
                    continue;
                }
                if (Structural.Contains(tag)) continue;

                var rootTag = PrefabAttributeResolver.ResolveRootTag(tag, prefabXml);
                if (rootTag == null || !widgetTypes.ContainsKey(rootTag))
                {
                    result.Problems.Add($"{rel}:{line} <{tag}> does not resolve to a widget type (unknown tag, empty prefab or prefab cycle)");
                    continue;
                }
                if (ambiguous.Contains(rootTag))
                {
                    result.Problems.Add($"{rel}:{line} <{tag}> resolves to '{rootTag}', a short name shared by two widget types");
                    continue;
                }

                var type = widgetTypes[rootTag];
                foreach (var attribute in element.Attributes())
                {
                    if (attribute.IsNamespaceDeclaration) continue;
                    var name = attribute.Name.LocalName;
                    if (PrefabAttributeResolver.IsKeyTypeAttribute(name)) continue;
                    var resolution = PrefabAttributeResolver.ResolveAttribute(type, name);
                    if (resolution == AttributeResolution.Settable) continue;
                    result.BadUses.Add(new BadUse { TypeName = type.Name, Attribute = name, Resolution = resolution, File = rel, Line = line });
                }
            }
        }
        return result;
    }

    // WidgetPrefab.LoadVisualDefinitions (WidgetPrefab.cs:36-56) reads only these five VisualDefinition
    // attributes and ignores any other. A VisualState attribute is "State" or goes through
    // WidgetExtensions.SetWidgetAttributeFromString on a TaleWorlds.GauntletUI.VisualState
    // (WidgetPrefab.cs:60-70, VisualStateTemplate.CreateVisualState), so it must name a public settable property.
    private static readonly HashSet<string> VisualDefinitionAttributes = new HashSet<string>(StringComparer.Ordinal)
    {
        "Name", "TransitionDuration", "EaseType", "EaseFunction", "DelayOnBegin",
    };

    private static void CheckVisualAttributes(ScanResult result, XElement element, string tag, string rel, int line)
    {
        foreach (var attribute in element.Attributes())
        {
            if (attribute.IsNamespaceDeclaration) continue;
            var name = attribute.Name.LocalName;
            bool ok = tag == "VisualDefinition"
                ? VisualDefinitionAttributes.Contains(name)
                : name == "State"
                  || PrefabAttributeResolver.ResolveAttribute(typeof(TaleWorlds.GauntletUI.VisualState), name) == AttributeResolution.Settable;
            if (!ok)
            {
                result.Problems.Add($"{rel}:{line} <{tag}> {name} is not an attribute the engine reads on a {tag}");
                continue;
            }

            // WidgetPrefab.cs:42-51 uses Enum.TryParse without ignoreCase and silently keeps Linear/Sine on a miss.
            var easeEnum = tag != "VisualDefinition" ? null
                : name == "EaseType" ? typeof(TaleWorlds.GauntletUI.AnimationInterpolation.Type)
                : name == "EaseFunction" ? typeof(TaleWorlds.GauntletUI.AnimationInterpolation.Function)
                : null;
            if (easeEnum != null && !Enum.IsDefined(easeEnum, attribute.Value))
                result.Problems.Add($"{rel}:{line} <{tag}> {name}=\"{attribute.Value}\" is not a {easeEnum.Name} name; the loader is case-sensitive and keeps the default");
        }
    }

    private static IEnumerable<Type> WidgetTypes()
    {
        // TAOM.Client loads lazily: touch it so the front-end widgets that moved there (ADR-013) are in the AppDomain.
        _ = typeof(TAOM.Client.ClientSubModule).Assembly;
        var widgetBase = typeof(TaleWorlds.GauntletUI.BaseTypes.Widget);
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic) continue;
            if (!ReferencesGauntlet(assembly)) continue;
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }
            foreach (var type in types)
                if (type.IsClass && !type.IsAbstract && widgetBase.IsAssignableFrom(type))
                    yield return type;
        }
    }

    private static bool ReferencesGauntlet(Assembly assembly)
    {
        if (assembly.GetName().Name == "TaleWorlds.GauntletUI") return true;
        try { return assembly.GetReferencedAssemblies().Any(r => r.Name == "TaleWorlds.GauntletUI"); }
        catch (Exception) { return false; }
    }

    private static void AddPrefabsFrom(IDictionary<string, string> prefabs, string guiRoot)
    {
        if (!Directory.Exists(guiRoot)) return;
        foreach (var file in Directory.GetFiles(guiRoot, "*.xml", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
            prefabs[Path.GetFileNameWithoutExtension(file)] = File.ReadAllText(file);
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

    internal enum AttributeResolution { Settable, Unknown, NotSettable, RuntimeTypeUnknown, Ambiguous }

    /// <summary>Pure mirror of Gauntlet's attribute and tag lookup, kept free of game types so it unit-tests on fakes.</summary>
    internal static class PrefabAttributeResolver
    {
        public static bool IsKeyTypeAttribute(string name)
            => name == "Id" || name == "DataSource"
               || name.StartsWith("Parameter.", StringComparison.Ordinal)
               || name.StartsWith("Command.", StringComparison.Ordinal)
               || name.StartsWith("CommandParameter.", StringComparison.Ordinal);

        /// <summary>
        /// <paramref name="runtimeTypeOf"/> names the runtime type of an interface or abstract property
        /// (the engine resolves a dotted suffix on the property's value, WidgetExtensions.GetObjectAndProperty);
        /// null means the default table. A property it cannot name is a failure, never a pass.
        /// </summary>
        public static AttributeResolution ResolveAttribute(Type widgetType, string attribute, Func<Type, string, Type> runtimeTypeOf = null)
        {
            var segments = attribute.Split('.');
            if (segments.Length > 2) return AttributeResolution.Unknown;

            var first = FindProperty(widgetType, segments[0], out var ambiguous);
            if (ambiguous) return AttributeResolution.Ambiguous;
            if (first == null) return AttributeResolution.Unknown;
            if (segments.Length == 1) return HasPublicSetter(first) ? AttributeResolution.Settable : AttributeResolution.NotSettable;

            // The second segment resolves on the first one's runtime value, which an interface or
            // abstract declared type does not pin down.
            var inner = first.PropertyType;
            if (inner.IsInterface || inner.IsAbstract)
            {
                inner = (runtimeTypeOf ?? KnownRuntimeType)(widgetType, segments[0]);
                if (inner == null) return AttributeResolution.RuntimeTypeUnknown;
            }
            var second = FindProperty(inner, segments[1], out ambiguous);
            if (ambiguous) return AttributeResolution.Ambiguous;
            if (second == null) return AttributeResolution.Unknown;
            return HasPublicSetter(second) ? AttributeResolution.Settable : AttributeResolution.NotSettable;
        }

        /// <summary>
        /// The one interface-typed path TAOM prefabs use is <c>LayoutImp</c>. Widget.LayoutImp is an ILayout
        /// (Widget.cs:1185) that each subclass constructor assigns: GridWidget.cs:140-141
        /// (<c>GridLayout = new GridLayout(); base.LayoutImp = GridLayout;</c>) and ListPanel.cs:39-40
        /// (<c>StackLayout = new StackLayout(); base.LayoutImp = StackLayout;</c>). Verified against the
        /// v1.5.5 decompile; no other widget reassigns it to either of those layouts.
        /// </summary>
        private static Type KnownRuntimeType(Type widgetType, string property)
        {
            if (property != "LayoutImp") return null;
            for (var t = widgetType; t != null; t = t.BaseType)
            {
                if (t.Name == "GridWidget") return t.GetProperty("GridLayout", BindingFlags.Instance | BindingFlags.Public)?.PropertyType;
                if (t.Name == "ListPanel") return t.GetProperty("StackLayout", BindingFlags.Instance | BindingFlags.Public)?.PropertyType;
            }
            return null;
        }

        public static Type RootWidgetType(string tag, IReadOnlyDictionary<string, string> prefabXml, IReadOnlyDictionary<string, Type> widgetTypes)
        {
            var name = ResolveRootTag(tag, prefabXml);
            return name != null && widgetTypes.TryGetValue(name, out var type) ? type : null;
        }

        /// <summary>
        /// The widget tag a tag finally builds: a prefab tag resolves to its prefab's first element under
        /// Window, recursively. Null for a prefab with no root, or a prefab cycle (including a prefab
        /// whose root names itself).
        /// </summary>
        public static string ResolveRootTag(string tag, IReadOnlyDictionary<string, string> prefabXml)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            while (prefabXml.TryGetValue(tag, out var xml))
            {
                if (!seen.Add(tag)) return null;
                var root = PrefabRootElement(xml);
                if (root == null) return null;
                tag = root;
            }
            return tag;
        }

        private static string PrefabRootElement(string xml)
        {
            XDocument doc;
            try { doc = XDocument.Parse(xml); }
            catch (XmlException) { return null; }
            var top = doc.Root;
            if (top == null) return null;
            var window = top.Name.LocalName == "Window" ? top : top.Elements().FirstOrDefault(e => e.Name.LocalName == "Window");
            return window?.Elements().FirstOrDefault()?.Name.LocalName;
        }

        private static PropertyInfo FindProperty(Type type, string name, out bool ambiguous)
        {
            ambiguous = false;
            try { return type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public); }
            catch (AmbiguousMatchException) { ambiguous = true; return null; }
        }

        private static bool HasPublicSetter(PropertyInfo property) => property.GetSetMethod(false) != null;
    }

    private abstract class AbstractPart { }

    private interface IFakeLayout { }

    private sealed class FakeInner
    {
        public string Value { get; set; }
        public string ReadOnly { get; } = "";
    }

    private class FakeWidget
    {
        public string AField = "";
        public string Settable { get; set; }
        public string PrivateSet { get; private set; }
        public FakeInner Inner { get; set; }
        public IFakeLayout Layout { get; set; }
        public AbstractPart AbstractPart { get; set; }
    }

    private class OtherFakeWidget { }
}
