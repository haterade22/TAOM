using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Features.CareerSystem;

/// <summary>
/// Cross-file reference guard for the career data set. A Career declares its ability template,
/// its root choice and its six choice groups by id, in three separate files — and nothing at
/// runtime complains when one of those ids resolves to nothing. CareerRegistry.GetGroup returns
/// null and GetChoicesForGroup returns EmptyChoices, both silently, so a career whose tree is
/// missing renders as an empty screen with no log line and no crash.
///
/// That is exactly how cave_troll_master shipped: its Career element stayed live while its
/// perk tree and ability template sat commented out behind a "DISABLED 2026-05-14 ... not ready
/// for live game yet" marker, so Gundabad players were offered a career that granted nothing.
///
/// CareerChoicesIntegrationTests guards the choices file against itself (does the parser support
/// what the content uses). This class guards the references BETWEEN the three files.
/// </summary>
[TestClass]
public class CareerChoiceIntegrityTests
{
    private static string ModuleDataPath => Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            @"..\..\..\..\Main\_Module\ModuleData"));

    private static XDocument LoadCareerFile(string fileName)
    {
        var path = Path.Combine(ModuleDataPath, "career_system", fileName);
        Assert.IsTrue(File.Exists(path), $"{fileName} not found at {path}");
        return XDocument.Load(path);
    }

    /// <summary>
    /// Commented-out XML is invisible to XDocument, which is the whole point — a disabled block
    /// is exactly as absent as a deleted one, and both must fail the same way.
    /// </summary>
    private static HashSet<string> IdsOf(XDocument doc, string elementName) =>
        new HashSet<string>(
            doc.Descendants(elementName)
                .Select(e => e.Attribute("id")?.Value)
                .Where(id => !string.IsNullOrEmpty(id))!,
            StringComparer.OrdinalIgnoreCase);

    [TestMethod]
    public void EveryCareer_DeclaredChoiceGroups_ResolveInChoicesXml()
    {
        // Arrange
        var careers = LoadCareerFile("taom_careers.xml");
        var groupIds = IdsOf(LoadCareerFile("taom_career_choices.xml"), "ChoiceGroup");

        // Act
        var unresolved = new List<string>();
        foreach (var career in careers.Descendants("Career"))
        {
            var careerId = career.Attribute("id")?.Value ?? "(unnamed)";
            foreach (var group in career.Descendants("Group"))
            {
                var groupId = group.Attribute("id")?.Value;
                if (string.IsNullOrEmpty(groupId) || !groupIds.Contains(groupId))
                    unresolved.Add($"{careerId} -> {groupId ?? "(no id)"}");
            }
        }

        // Assert
        Assert.AreEqual(0, unresolved.Count,
            "Careers declaring <Group id> values with no matching <ChoiceGroup> in " +
            $"taom_career_choices.xml: {string.Join(", ", unresolved)}. The career would render " +
            "with no perks and no error. If the tree is deliberately parked, comment out the " +
            "<Career> element too so the career is not offered at character creation.");
    }

    [TestMethod]
    public void EveryCareer_RootChoiceId_ResolvesInChoicesXml()
    {
        // Arrange
        var careers = LoadCareerFile("taom_careers.xml");
        var choices = LoadCareerFile("taom_career_choices.xml");

        // The root choice is a direct child of <CareerChoices>; the other 1500 live inside groups.
        var rootChoiceIds = new HashSet<string>(
            choices.Root!.Elements("Choice")
                .Select(e => e.Attribute("id")?.Value)
                .Where(id => !string.IsNullOrEmpty(id))!,
            StringComparer.OrdinalIgnoreCase);

        // Act
        var unresolved = careers.Descendants("Career")
            .Select(c => new
            {
                CareerId = c.Attribute("id")?.Value ?? "(unnamed)",
                RootId = c.Attribute("root_choice_id")?.Value,
            })
            .Where(x => string.IsNullOrEmpty(x.RootId) || !rootChoiceIds.Contains(x.RootId!))
            .Select(x => $"{x.CareerId} -> {x.RootId ?? "(none)"}")
            .ToList();

        // Assert
        Assert.AreEqual(0, unresolved.Count,
            "Careers whose root_choice_id has no matching top-level <Choice> in " +
            $"taom_career_choices.xml: {string.Join(", ", unresolved)}.");
    }

    [TestMethod]
    public void EveryCareer_AbilityTemplateId_ResolvesInAbilityTemplatesXml()
    {
        // Arrange
        var careers = LoadCareerFile("taom_careers.xml");
        var templateIds = IdsOf(LoadCareerFile("taom_ability_templates.xml"), "AbilityTemplate");

        // Act
        var unresolved = careers.Descendants("Career")
            .Select(c => new
            {
                CareerId = c.Attribute("id")?.Value ?? "(unnamed)",
                TemplateId = c.Attribute("ability_template_id")?.Value,
            })
            .Where(x => string.IsNullOrEmpty(x.TemplateId) || !templateIds.Contains(x.TemplateId!))
            .Select(x => $"{x.CareerId} -> {x.TemplateId ?? "(none)"}")
            .ToList();

        // Assert
        Assert.AreEqual(0, unresolved.Count,
            "Careers whose ability_template_id has no matching <AbilityTemplate> in " +
            $"taom_ability_templates.xml: {string.Join(", ", unresolved)}. The ability slot " +
            "renders the raw id instead of a display name.");
    }

    [TestMethod]
    public void EveryChoiceGroup_CareerIdBackReference_MatchesDeclaringCareer()
    {
        // Arrange
        var careers = LoadCareerFile("taom_careers.xml");
        var choices = LoadCareerFile("taom_career_choices.xml");

        var declaredBy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var career in careers.Descendants("Career"))
        {
            var careerId = career.Attribute("id")?.Value ?? "(unnamed)";
            foreach (var group in career.Descendants("Group"))
            {
                var groupId = group.Attribute("id")?.Value;
                if (!string.IsNullOrEmpty(groupId))
                    declaredBy[groupId!] = careerId;
            }
        }

        // Act — a group whose career_id points somewhere other than the career declaring it
        // would apply another career's perks; only groups that ARE declared are checked here,
        // since an undeclared group is the orphan case rather than the mismatch case.
        var mismatched = new List<string>();
        foreach (var group in choices.Descendants("ChoiceGroup"))
        {
            var groupId = group.Attribute("id")?.Value;
            var backRef = group.Attribute("career_id")?.Value;
            if (string.IsNullOrEmpty(groupId) || !declaredBy.TryGetValue(groupId!, out var owner))
                continue;

            if (!string.Equals(backRef, owner, StringComparison.OrdinalIgnoreCase))
                mismatched.Add($"{groupId} says career_id='{backRef}' but is declared by '{owner}'");
        }

        // Assert
        Assert.AreEqual(0, mismatched.Count,
            $"ChoiceGroup career_id back-references that disagree with the declaring Career: " +
            string.Join(", ", mismatched));
    }
}
