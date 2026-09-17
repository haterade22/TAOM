using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CultureDoctrine.Hooks.Behaviors;
using TAOM.Features.CultureDoctrine.Hooks.Tactics;

namespace TAOM.Tests.Features.CultureDoctrine;

/// <summary>
/// Every TAOM tactic and behaviour type name is a key into three engine string tables, and a
/// missing variation renders "ERROR: Text with id ... doesn't exist!" in the battle feed:
/// TeamAIComponent.MakeDecision (str_team_ai_tactic_text, the sergeant tactic popup),
/// BehaviorComponent.GetBehaviorString (str_formation_ai_sergeant_instruction_behavior_text, the
/// sergeant's order text) and MissionOrderVM.OnDelegateCommandToAI (str_formation_ai_behavior_text,
/// the "Infantry are ..." message after F6). The first A/B battle (2026-09-17) showed the third
/// table missing for every TAOM behaviour.
/// </summary>
[TestClass]
public class DoctrinePopupStringsTests
{
    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\.."));

    private static string StringsPath =>
        Path.Combine(RepoRoot, @"Main\_Module\ModuleData\taom_module_strings.xml");

    private static HashSet<string> ShippedIds() =>
        new(XDocument.Load(StringsPath).Descendants("string").Select(s => (string)s.Attribute("id")).Where(id => id != null));

    private static IEnumerable<Type> ConcreteSubclassesOf(Type baseType)
    {
        Type[] types;
        try
        {
            types = baseType.Assembly.GetTypes();
        }
        catch (System.Reflection.ReflectionTypeLoadException e)
        {
            // Types needing game DLLs the test host lacks come back null; the doctrine types load.
            types = e.Types.Where(t => t != null).ToArray();
        }
        var found = types.Where(t => !t.IsAbstract && baseType.IsAssignableFrom(t)).OrderBy(t => t.Name).ToList();
        Assert.IsTrue(found.Count >= 5, "expected the TAOM subclasses of " + baseType.Name + " to load; got " + found.Count);
        return found;
    }

    [TestMethod]
    public void EveryTaomTactic_HasASergeantPopupRow()
    {
        var ids = ShippedIds();
        var missing = ConcreteSubclassesOf(typeof(TaomTacticBase))
            .Select(t => "str_team_ai_tactic_text." + t.Name)
            .Where(id => !ids.Contains(id)).ToList();
        Assert.AreEqual(0, missing.Count, "taom_module_strings.xml lacks: " + string.Join(", ", missing));
    }

    [TestMethod]
    public void EveryTaomBehaviour_HasBothBehaviourTextRows()
    {
        var ids = ShippedIds();
        var missing = ConcreteSubclassesOf(typeof(TaomBehaviorBase))
            .SelectMany(t => new[]
            {
                "str_formation_ai_sergeant_instruction_behavior_text." + t.Name,
                "str_formation_ai_behavior_text." + t.Name,
            })
            .Where(id => !ids.Contains(id)).ToList();
        Assert.AreEqual(0, missing.Count, "taom_module_strings.xml lacks: " + string.Join(", ", missing));
    }

    [TestMethod]
    public void DelegateCommandRows_CarryTheVanillaTroopNameVariables()
    {
        // MissionOrderVM sets TROOP_NAMES_BEGIN / TROOP_NAMES_END / IS_PLURAL before the lookup;
        // a row without them reads as a bare sentence with no subject.
        var rows = XDocument.Load(StringsPath).Descendants("string")
            .Where(s => ((string)s.Attribute("id") ?? "").StartsWith("str_formation_ai_behavior_text.", StringComparison.Ordinal))
            .ToList();
        Assert.IsTrue(rows.Count >= 5, "expected the five TAOM behaviour rows");
        foreach (var row in rows)
        {
            var text = (string)row.Attribute("text") ?? "";
            StringAssert.Contains(text, "{TROOP_NAMES_BEGIN}", (string)row.Attribute("id"));
            StringAssert.Contains(text, "{TROOP_NAMES_END}", (string)row.Attribute("id"));
        }
    }

    [TestMethod]
    public void DelegateCommandRows_KeepTheVanillaVariablesInEveryLanguage()
    {
        // The translator rewrites the twelve language files; a translation that drops or
        // mangles {TROOP_NAMES_BEGIN}, {?IS_PLURAL} and {?}{\?} renders a broken F6 message.
        var keys = XDocument.Load(StringsPath).Descendants("string")
            .Where(r => ((string)r.Attribute("id") ?? "").StartsWith("str_formation_ai_behavior_text.", StringComparison.Ordinal))
            .Select(r => Regex.Match((string)r.Attribute("text") ?? "", @"^\{=([^}]+)\}").Groups[1].Value)
            .Where(k => k.Length > 0).ToList();
        Assert.IsTrue(keys.Count >= 6);
        var languages = Path.Combine(RepoRoot, @"Main\_Module\ModuleData\Languages");
        var failures = new List<string>();
        foreach (var dir in Directory.GetDirectories(languages))
        {
            var rows = Directory.GetFiles(dir, "std_taom_*.xml")
                .SelectMany(f => XDocument.Load(f).Descendants("string"))
                .GroupBy(r => (string)r.Attribute("id") ?? "", StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => (string)g.First().Attribute("text") ?? "", StringComparer.Ordinal);
            foreach (var key in keys)
            {
                if (!rows.TryGetValue(key, out var text))
                    continue; // coverage is LanguageFileCoverageTests' job
                foreach (var token in new[] { "{TROOP_NAMES_BEGIN}", "{TROOP_NAMES_END}", "{?IS_PLURAL}", "{?}", "{\\?}" })
                    if (!text.Contains(token))
                        failures.Add(Path.GetFileName(dir) + " " + key + " lacks " + token);
            }
        }
        Assert.AreEqual(0, failures.Count, string.Join("; ", failures));
    }
}
