using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// A widget property whose setter raises a float change (the list below, Widget.cs v1.5.7) is written BACK to the bound view
/// model property: <c>GauntletView.OnViewPropertyChanged</c> calls <c>ViewModel.SetPropertyValue</c> with the boxed float, and
/// reflection refuses to put a Single into an int setter. The first push at bind time does not write back (the view listens
/// to the widget only after it, GauntletView.cs:154-187), so the throw comes with the first change after it, once per change,
/// inside the screen tick. #1497: the stamina bar bound <c>SuggestedHeight</c> to an int and the player got the crash dialog
/// the moment a soldier's bar moved. So: a bound property that can change after binding and takes a write is a float.
/// Limits: the scan reads declarations that carry [DataSourceProperty] (a plain ViewModel exposes a public property without
/// it), matches names across the whole code base, sees a notification only in the declaring file, and covers the float
/// setters of Widget itself, not the int, bool and string write-backs of its subclasses.
/// </summary>
[TestClass]
public class GauntletFloatWriteBackGateTests
{
    // Setters in TaleWorlds.GauntletUI.BaseTypes.Widget that call OnPropertyChanged(float value, name).
    private static readonly string[] FloatWriteBack =
    {
        "SuggestedWidth", "SuggestedHeight", "PositionXOffset", "PositionYOffset", "MarginTop", "MarginLeft", "MarginBottom",
        "MarginRight", "MaxWidth", "MaxHeight", "MinWidth", "MinHeight",
    };

    // Attr="@Name" in an XML file (also with spaces around "=" or single quotes), Attr=""@Name"" in a verbatim C# string,
    // Attr=\"@Name\" in a plain one.
    private static readonly Regex Binding = new(
        @"\b(?<attr>" + string.Join("|", FloatWriteBack) + @")\s*=\s*\\?(?:""{1,2}|')@(?<name>\w+)", RegexOptions.Compiled);

    private static readonly Regex Declaration = new(
        @"\[DataSourceProperty\]\s*public\s+(?:override\s+|virtual\s+)?(?<type>[\w<>\[\]?.]+)\s+(?<name>\w+)(?<body>[^\[]*)", RegexOptions.Compiled);

    internal static IEnumerable<string> BoundNames(string text) => Binding.Matches(text).Cast<Match>().Select(m => m.Groups["name"].Value);

    /// <summary>The declarations in <paramref name="source"/> that would throw on a float write-back, as "type Name".</summary>
    internal static IEnumerable<string> Offenders(string source, ISet<string> boundNames)
    {
        foreach (Match m in Declaration.Matches(source))
        {
            var name = m.Groups["name"].Value;
            var type = m.Groups["type"].Value;
            if (!boundNames.Contains(name) || type == "float" || type == "Single" || type == "double")
                continue;
            var body = m.Groups["body"].Value;
            // ViewModel.SetPropertyValue asks for the public setter only: get-only or a private setter takes no write.
            var publicSetter = Regex.IsMatch(body, @"(?<!private\s)(?<!protected\s)(?<!internal\s)\bset\b");
            // An auto-property that nothing notifies never changes the widget after the first push, so nothing comes back.
            var auto = Regex.IsMatch(body, @"^\s*\{\s*get;\s*(?:set;\s*)?\}");
            var notified = !auto || Regex.IsMatch(source, @"OnPropertyChanged\w*\([^;]*?(?:nameof\((?:\w+\.)*" + name + @"\)|""" + name + @""")");
            if (publicSetter && notified)
                yield return type + " " + name;
        }
    }

    [TestMethod]
    public void EveryPropertyBoundToAWidgetFloatThatWritesBack_IsAFloat_WhenItCanChangeAndTakeAWrite()
    {
        var sources = new[] { "Main", "Client" }
            .SelectMany(folder => Directory.EnumerateFiles(RepoPath(folder), "*.cs", SearchOption.AllDirectories))
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                           && !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .ToList();
        var prefabs = Directory.EnumerateFiles(RepoPath("Main", "_Module", "GUI"), "*.xml", SearchOption.AllDirectories);

        var texts = sources.ToDictionary(path => path, File.ReadAllText);
        var bound = new HashSet<string>(texts.Values.Concat(prefabs.Select(File.ReadAllText)).SelectMany(BoundNames), StringComparer.Ordinal);
        Assert.IsTrue(bound.Count > 20, "the scan found only " + bound.Count + " bindings: the pattern no longer matches the prefabs");

        var offenders = texts
            .SelectMany(pair => Offenders(pair.Value, bound).Select(o => o + " in " + pair.Key.Substring(RepoPath().Length).TrimStart('\\', '/')))
            .OrderBy(o => o, StringComparer.Ordinal)
            .ToList();

        Assert.AreEqual(0, offenders.Count,
            "Bound to a widget property that writes a float back (" + string.Join(", ", FloatWriteBack) + "), with a public setter and a change "
            + "notification, but not a float. The widget's write throws ArgumentException in the screen tick on the first change after "
            + "binding. Make the property a float:\n  " + string.Join("\n  ", offenders));
    }

    [TestMethod]
    public void BoundNames_ReadTheThreeQuoteForms()
    {
        CollectionAssert.AreEqual(new[] { "A", "B", "C" }, BoundNames(
            "<Widget SuggestedHeight=\"@A\" /> SuggestedWidth=\"\"@B\"\" MarginTop=\\\"@C\\\" Color=\"@NotAFloat\" SuggestedHeight=\"12\"").ToArray());
        CollectionAssert.AreEqual(new[] { "D", "E" }, BoundNames("<Widget MaxWidth = \"@D\" MinHeight='@E' />").ToArray());
    }

    [TestMethod]
    public void Offenders_AnIntWithANotifyingPublicSetter_IsNamed()
    {
        const string source = @"
    [DataSourceProperty]
    public int HudStaminaHeight
    {
        get => _h;
        set { if (_h != value) { _h = value; OnPropertyChanged(nameof(HudStaminaHeight)); } }
    }
    [DataSourceProperty] public bool Other { get; set; }";

        CollectionAssert.AreEqual(new[] { "int HudStaminaHeight" }, Offenders(source, new HashSet<string> { "HudStaminaHeight" }).ToArray());
    }

    [DataTestMethod]
    [DataRow("[DataSourceProperty] public float Fill { get => _f; set { _f = value; OnPropertyChanged(nameof(Fill)); } }", DisplayName = "a float")]
    [DataRow("[DataSourceProperty] public int Fill { get; }", DisplayName = "get-only: SetPropertyValue finds no setter")]
    [DataRow("[DataSourceProperty] public int Fill { get; private set; }", DisplayName = "a private setter is not found either")]
    [DataRow("[DataSourceProperty] public int Fill => _fill;", DisplayName = "expression-bodied: only the setter rule clears it")]
    [DataRow("[DataSourceProperty] public int Fill { get => _f; private set { _f = value; OnPropertyChanged(nameof(Fill)); } }",
        DisplayName = "a notifying private setter: only the setter rule clears it")]
    [DataRow("[DataSourceProperty] public double Fill { get => _f; set { _f = value; OnPropertyChanged(nameof(Fill)); } }",
        DisplayName = "a double takes a boxed float")]
    [DataRow("[DataSourceProperty] public int Fill { get; set; } = 100;", DisplayName = "an auto-property nothing notifies never changes the widget")]
    public void Offenders_ShapesThatCannotThrow_AreNotNamed(string source)
    {
        Assert.AreEqual(0, Offenders(source, new HashSet<string> { "Fill" }).Count());
    }

    [TestMethod]
    [DataTestMethod]
    [DataRow("OnPropertyChanged(nameof(Fill));")]
    [DataRow("OnPropertyChangedWithValue(Fill, nameof(Fill));")]
    [DataRow("OnPropertyChanged(nameof(BarVM.Fill));")]
    [DataRow("OnPropertyChanged(\"Fill\");")]
    public void Offenders_AnAutoPropertyThatIsNotifiedByName_IsNamed(string notification)
    {
        var source = "[DataSourceProperty] public int Fill { get; set; }\n void Grow() { Fill++; " + notification + " }";

        CollectionAssert.AreEqual(new[] { "int Fill" }, Offenders(source, new HashSet<string> { "Fill" }).ToArray());
    }
}
