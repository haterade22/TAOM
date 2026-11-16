using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.CompanionTactics.Roles;

/// <summary>
/// RoleTooltipDecorator strips an earlier "[label] " prefix only when "] " sits at index 5 or less, so a role
/// label longer than 4 characters stacks a second prefix when the role or the language changes (#1385 review).
/// Every row of every language must fit; a translator who needs more room changes the decorator first.
/// </summary>
[TestClass]
public class RoleLabelLengthTests
{
    private const int MaxLabelLength = 4;

    [TestMethod]
    public void EveryRoleLabelRow_InEveryLanguage_FitsThePrefixStrip()
    {
        var moduleData = RepoPaths.RepoPath("Main", "_Module", "ModuleData");
        var files = new[] { Path.Combine(moduleData, "taom_module_strings.xml") }
            .Concat(Directory.EnumerateFiles(Path.Combine(moduleData, "Languages"), "std_taom_module_strings_*.xml", SearchOption.AllDirectories));

        var rows = files
            .SelectMany(f => XDocument.Load(f).Descendants("string").Select(s => (File: Path.GetFileName(f), Id: (string?)s.Attribute("id"), Text: (string?)s.Attribute("text"))))
            .Where(r => r.Id != null && r.Id.StartsWith("taom_role_"))
            .Select(r => (r.File, r.Id, Label: r.Text!.StartsWith("{=") ? r.Text.Substring(r.Text.IndexOf('}') + 1) : r.Text))
            .ToList();

        Assert.IsTrue(rows.Count(r => r.File == "taom_module_strings.xml") >= 10, "The English role rows were not found; the scan broke.");
        var tooLong = rows.Where(r => r.Label.Length > MaxLabelLength).Select(r => $"{r.File}: {r.Id} = '{r.Label}'").ToList();
        Assert.AreEqual(0, tooLong.Count, "Role labels longer than " + MaxLabelLength + " characters:\n" + string.Join("\n", tooLong));
    }
}
