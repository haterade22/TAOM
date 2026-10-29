using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.StagedSiege;

/// <summary>
/// The HUD's progress line is one whole localized sentence per capture status: a line joined in code from two translated
/// fragments cannot be reordered by a translator, and a joiner such as " - " is not localized at all.
/// </summary>
[TestClass]
public class StagedSiegeHudStringsTests
{
    private static readonly string[] ProgressKeys =
    {
        "taom_staged_siege_progress_rising", "taom_staged_siege_progress_falling",
        "taom_staged_siege_progress_contested", "taom_staged_siege_progress_idle",
    };

    private static readonly string[] RetiredKeys =
    {
        "taom_staged_siege_progress", "taom_staged_siege_status_rising", "taom_staged_siege_status_falling",
        "taom_staged_siege_status_contested", "taom_staged_siege_status_idle",
    };

    private static string[] ShippedIds() =>
        XDocument.Load(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "taom_module_strings.xml")).Descendants("string")
            .Select(s => (string?)s.Attribute("id")).Where(id => id != null).Select(id => id!).ToArray();

    [TestMethod]
    public void TheFourWholeLineProgressKeys_ShipWithAPercentVariable()
    {
        var rows = XDocument.Load(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "taom_module_strings.xml")).Descendants("string")
            .ToDictionary(s => (string)s.Attribute("id")!, s => (string)s.Attribute("text")!);

        foreach (var key in ProgressKeys)
        {
            Assert.IsTrue(rows.ContainsKey(key), key + " is missing from taom_module_strings.xml");
            StringAssert.Contains(rows[key], "{PERCENT}");
        }
    }

    [TestMethod]
    public void TheHallKeys_ShipAndTheViewModelUsesThem()
    {
        var rows = XDocument.Load(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "taom_module_strings.xml")).Descendants("string")
            .ToDictionary(s => (string)s.Attribute("id")!, s => (string)s.Attribute("text")!);
        var source = RepoPaths.ReadSource("Main/Features/StagedSiege/UI/StagedSiegeHudVM.cs", stripComments: true);
        var adapter = RepoPaths.ReadSource("Main/Adapters/StagedHallMissionAdapter.cs", stripComments: true);

        StringAssert.EndsWith(rows["taom_staged_siege_take_hall"], "}Take the lord's hall");
        StringAssert.EndsWith(rows["taom_staged_siege_hall_taken"], "}The lord's hall is taken.");
        StringAssert.Contains(source, "{=taom_staged_siege_take_hall}");
        StringAssert.Contains(source, "{=taom_staged_siege_hall_taken}");
        StringAssert.Contains(adapter, "{=taom_staged_siege_hall_taken}");
    }

    [TestMethod]
    public void TheFragmentKeys_AreRetiredFromTheEnglishFileAndEveryLanguage()
    {
        foreach (var key in RetiredKeys)
            CollectionAssert.DoesNotContain(ShippedIds(), key);

        var languages = Directory.GetFiles(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "Languages"), "std_taom_module_strings_*.xml", SearchOption.AllDirectories);
        Assert.AreEqual(12, languages.Length);
        foreach (var file in languages)
        {
            var ids = XDocument.Load(file).Descendants("string").Select(s => (string?)s.Attribute("id")).ToList();
            foreach (var key in RetiredKeys)
                CollectionAssert.DoesNotContain(ids, key, Path.GetFileName(file));
        }
    }

    [TestMethod]
    public void TheViewModel_JoinsNoLocalizedFragmentsInCode()
    {
        var source = RepoPaths.ReadSource("Main/Features/StagedSiege/UI/StagedSiegeHudVM.cs", stripComments: true);

        Assert.IsFalse(source.Contains("\" - \""), "the progress line is one localized sentence per status");
        foreach (var key in ProgressKeys)
            StringAssert.Contains(source, "{=" + key + "}");
        foreach (var key in RetiredKeys)
            Assert.IsFalse(source.Contains("{=" + key + "}"), key);
    }
}
