using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.ServerSettings;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Infrastructure.Localization;

/// <summary>
/// The setting names, hints, button texts and group parts on TAOM's MCM pages carry a key with a registered English
/// row (#1385 Phase 4). MCM passes a setting's name, hint and button text through <c>TextObject</c>, and splits a group
/// path on '/' before it localizes each part, so each part needs its own key. The Chinese team patched these texts at
/// runtime before. Not keyed: the 8 dropdown options (TAOM reads their selected value) and the page titles (MCM shows
/// the class DisplayName without localizing it).
/// </summary>
[TestClass]
public class McmSettingsTextIsKeyedTests
{
    private static readonly Regex Keyed = new Regex(@"^\{=([A-Za-z0-9_]+)\}(.*)$", RegexOptions.Singleline);

    private static IEnumerable<(string Where, string Text)> Walk()
    {
        foreach (var type in ServerSettingsSet.Classes)
        foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var where = type.Name + "." + p.Name;
            foreach (var group in p.GetCustomAttributes<SettingPropertyGroupAttribute>(inherit: true))
                foreach (var part in group.GroupName.Split('/'))
                    yield return (where + " group", part);
            foreach (var attr in p.GetCustomAttributes<BaseSettingPropertyAttribute>(inherit: true))
            {
                yield return (where + " name", attr.DisplayName);
                if (!string.IsNullOrEmpty(attr.HintText))
                    yield return (where + " hint", attr.HintText);
                if (attr is SettingPropertyButtonAttribute button && !string.IsNullOrEmpty(button.Content))
                    yield return (where + " button", button.Content);
            }
        }
    }

    /// <summary>Every MCM text, with a floor so that a broken reflection walk cannot pass a test on zero texts.</summary>
    private static List<(string Where, string Text)> McmTexts()
    {
        var texts = Walk().ToList();
        Assert.IsTrue(texts.Count > 1000, "Found only " + texts.Count + " MCM texts; the reflection walk broke.");
        return texts;
    }

    /// <summary>The group paths of every setting, each part as (key, English).</summary>
    private static List<(string Where, List<(string Key, string English)> Parts)> GroupPaths()
    {
        var paths = new List<(string, List<(string, string)>)>();
        foreach (var type in ServerSettingsSet.Classes)
        foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        foreach (var group in p.GetCustomAttributes<SettingPropertyGroupAttribute>(inherit: true))
            paths.Add((type.Name + "." + p.Name, group.GroupName.Split('/').Select(part => Keyed.Match(part))
                .Select(m => (m.Groups[1].Value, m.Groups[2].Value)).ToList()));
        Assert.IsTrue(paths.Count > 500, "Found only " + paths.Count + " group paths; the reflection walk broke.");
        return paths;
    }

    private static Dictionary<string, string> Rows(string file, bool keyedOnly)
    {
        var rows = new Dictionary<string, string>();
        foreach (var s in XDocument.Load(file).Descendants("string"))
        {
            var id = (string?)s.Attribute("id");
            var text = (string?)s.Attribute("text") ?? "";
            var m = Keyed.Match(text);
            if (id == null)
                continue;
            if (m.Success && m.Groups[1].Value == id)
                rows[id] = m.Groups[2].Value;
            else if (!keyedOnly)
                rows[id] = text;
        }
        return rows;
    }

    private static Dictionary<string, string> RegisteredEnglish()
    {
        var rows = new Dictionary<string, string>();
        var moduleData = RepoPaths.RepoPath("Main", "_Module", "ModuleData");
        foreach (var file in Directory.EnumerateFiles(moduleData, "*_strings.xml", SearchOption.AllDirectories)
                     .Where(f => !f.Contains(Path.DirectorySeparatorChar + "Languages" + Path.DirectorySeparatorChar)))
        foreach (var row in Rows(file, keyedOnly: true))
            rows[row.Key] = row.Value;
        return rows;
    }

    [TestMethod]
    public void ServerSettingsSet_ListsEveryMcmSettingsClassInTaomDll()
    {
        // The gates in this class walk ServerSettingsSet.Classes, a list kept by hand.
        Type[] types;
        try { types = typeof(TaomSettings).Assembly.GetTypes(); }
        catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray()!; }
        var mcm = types.Where(t => !t.IsAbstract && t.BaseType is { IsGenericType: true } b
                                   && b.GetGenericTypeDefinition() == typeof(AttributeGlobalSettings<>)).ToList();

        CollectionAssert.AreEquivalent(mcm, ServerSettingsSet.Classes, "List the new MCM settings class in ServerSettingsSet.Classes.");
    }

    [TestMethod]
    public void EveryMcmText_StartsWithAKey()
    {
        var bare = McmTexts().Where(t => !Keyed.IsMatch(t.Text)).Select(t => t.Where + ": " + t.Text).ToList();
        Assert.AreEqual(0, bare.Count, "MCM texts with no {=key}:\n" + string.Join("\n", bare.Take(40)));
    }

    [TestMethod]
    public void EveryMcmKey_HasARegisteredRowWithTheSameEnglish()
    {
        var rows = RegisteredEnglish();
        var problems = new List<string>();
        foreach (var (where, text) in McmTexts())
        {
            var m = Keyed.Match(text);
            if (!m.Success) continue;
            var key = m.Groups[1].Value;
            if (!rows.TryGetValue(key, out var english))
                problems.Add(where + ": " + key + " has no row");
            else if (english != m.Groups[2].Value)
                problems.Add(where + ": " + key + " row says '" + english + "', the code says '" + m.Groups[2].Value + "'");
        }
        Assert.AreEqual(0, problems.Count, string.Join("\n", problems.Take(40)));
    }

    [TestMethod]
    public void OneGroupPart_HasOneKey()
    {
        // MCM matches groups by their raw text, so one English part under two keys would show as two groups.
        var keysByEnglish = McmTexts().Where(t => t.Where.EndsWith(" group"))
            .Select(t => Keyed.Match(t.Text)).Where(m => m.Success)
            .GroupBy(m => m.Groups[2].Value)
            .Where(g => g.Select(m => m.Groups[1].Value).Distinct().Count() > 1)
            .Select(g => g.Key + ": " + string.Join(", ", g.Select(m => m.Groups[1].Value).Distinct()))
            .ToList();
        Assert.AreEqual(0, keysByEnglish.Count, string.Join("\n", keysByEnglish));
    }

    [TestMethod]
    public void NoChildGroupText_ContainsItsParentsText_InAnyLanguage()
    {
        // MCM shows a child group as Localize(child).Replace(Localize(parent), ""): a child that holds its parent's
        // text shows cut, and an empty parent text throws, so MCM hides the whole TAOM page.
        var languages = new List<(string Name, Dictionary<string, string> Rows)> { ("English", new Dictionary<string, string>()) };
        foreach (var file in Directory.EnumerateFiles(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "Languages"),
                     "std_taom_module_strings_*.xml", SearchOption.AllDirectories))
            languages.Add((Path.GetFileName(file), Rows(file, keyedOnly: false)));

        var problems = new List<string>();
        foreach (var (where, parts) in GroupPaths())
        for (var i = 1; i < parts.Count; i++)
        foreach (var (language, rows) in languages)
        {
            var parent = rows.TryGetValue(parts[i - 1].Key, out var p) ? p : parts[i - 1].English;
            var child = rows.TryGetValue(parts[i].Key, out var c) ? c : parts[i].English;
            if (string.IsNullOrEmpty(parent))
                problems.Add(language + " " + where + ": " + parts[i - 1].Key + " is empty");
            else if (child.Contains(parent))
                problems.Add(language + " " + where + ": '" + child + "' contains its parent '" + parent + "'");
        }
        Assert.AreEqual(0, problems.Count, string.Join("\n", problems.Distinct().Take(40)));
    }
}
