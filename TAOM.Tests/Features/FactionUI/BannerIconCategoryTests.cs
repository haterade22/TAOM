using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FactionUI.Campaign;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.FactionUI;

/// <summary>
/// The layered banner editor's icon pages (BannerIconCategory) and the names that make them: TAOM's banner icon groups
/// are named after the people they belong to, so several groups (one per sheet) make one page in the editor.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class BannerIconCategoryTests
{
    private const string IconsPath = "Main/_Module/ModuleData/banner_icons.xml";
    private const string StringsPath = "Main/_Module/ModuleData/taom_module_strings.xml";

    [TestMethod]
    public void Merge_JoinsGroupsOfOneName_InFirstSeenOrder()
    {
        var pages = BannerIconCategory.Merge(new (string, IEnumerable<int>)[]
        {
            ("Dúnedain", new[] { 1, 2 }),
            ("Orcish", new[] { 10 }),
            ("Empty", new int[0]),
            ("Dúnedain", new[] { 2, 3 }),
        });

        CollectionAssert.AreEqual(new[] { "Dúnedain", "Orcish" }, pages.Select(p => p.Name).ToArray(), "an empty page was kept, or the order moved");
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, pages[0].Ids.ToArray(), "the second Dúnedain sheet was not appended once");
        Assert.IsTrue(pages[0].Contains(3));
        Assert.IsFalse(pages[1].Contains(3));
    }

    [TestMethod]
    public void TaomIconGroups_AreNamedByPeople_WithAnEnglishString()
    {
        var strings = new HashSet<string>(
            Regex.Matches(ReadSource(StringsPath), "<string id=\"(taom_banner_category_\\w+)\"").Cast<Match>().Select(m => m.Groups[1].Value));
        var failures = new List<string>();
        var seen = new HashSet<string>();

        foreach (var group in XDocument.Parse(ReadSource(IconsPath)).Descendants("BannerIconGroup"))
        {
            var name = (string?)group.Attribute("name") ?? "";
            var match = Regex.Match(name, @"^\{=(taom_banner_category_\w+)\}\S");
            if (match.Success && !seen.Add(match.Groups[1].Value))
                failures.Add($"group {group.Attribute("id")?.Value}: {match.Groups[1].Value} already has a group; one group per people");
            if (!match.Success)
                failures.Add($"group {group.Attribute("id")?.Value} is named '{name}', not a taom_banner_category_* people");
            else if (!strings.Contains(match.Groups[1].Value))
                failures.Add($"group {group.Attribute("id")?.Value}: {match.Groups[1].Value} has no row in taom_module_strings.xml");
        }

        Assert.AreEqual(0, failures.Count, string.Join("\n", failures));
    }
}
