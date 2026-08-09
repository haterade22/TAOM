using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// Guards the Rohan enlistment kits against Gondor item leakage (#375, reported in play
/// 2026-08-08: "the quartermaster gives me gondor gloves and I'm enlisted under Theoden").
/// Culture resolution was correct — the roster CONTENT carried a stray
/// <c>sk_gd_ano_gloves_a</c> in <c>enlist_vlandia_recruit</c>.
///
/// Deliberately vlandia-only. Umbar's enlistment kits also carry <c>sk_gd_ano_*</c> items,
/// and that is NOT a defect to guard against: troops_umbar.xml dresses Umbar troops in the
/// same Anorien set as their primary kit (33 uses each of the four pieces — the most-used
/// armour items in the file), so the enlisted player matching them is consistent, and
/// plausibly deliberate Black-Numenorean styling. A blanket prefix-to-culture rule would
/// redden on that intended data. If Umbar's dress is ever re-judged, change the troop tree
/// first and this guard's scope second.
/// </summary>
[TestClass]
public class EnlistmentEquipmentCultureTests
{
    private const string RosterRelPath = "Main/_Module/ModuleData/equipmentsets/taom_enlistment_equipment.xml";

    [TestMethod]
    public void VlandiaEnlistmentKits_ContainNoGondorItems()
    {
        var doc = XDocument.Load(FromRepoRoot(RosterRelPath));

        var vlandiaRosters = doc.Descendants("EquipmentRoster")
            .Where(r => ((string)r.Attribute("id") ?? "").StartsWith("enlist_vlandia_", StringComparison.Ordinal))
            .ToList();
        Assert.IsTrue(vlandiaRosters.Count >= 4,
            "Expected the four vlandia rank rosters; the file layout changed and this guard needs re-aiming.");

        var offenders = vlandiaRosters
            .SelectMany(r => r.Descendants("Equipment")
                .Select(e => new
                {
                    Roster = (string)r.Attribute("id"),
                    Slot = (string)e.Attribute("slot"),
                    Item = (string)e.Attribute("id") ?? string.Empty,
                }))
            .Where(x => x.Item.StartsWith("Item.sk_gd_", StringComparison.Ordinal))
            .ToList();

        Assert.AreEqual(0, offenders.Count,
            "Gondor (sk_gd_*) items in Rohan enlistment kits: " +
            string.Join(", ", offenders.Select(o => $"{o.Roster}/{o.Slot}={o.Item}")));
    }

    private static string FromRepoRoot(string relPath)
    {
        var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (dir != null)
        {
            // File.Exists too, not just Directory.Exists: in a git WORKTREE `.git` is a FILE
            // holding a `gitdir:` pointer, not a directory (see f1bc6b39).
            var gitPath = Path.Combine(dir.FullName, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
                return Path.Combine(dir.FullName, relPath.Replace('/', Path.DirectorySeparatorChar));
            dir = dir.Parent;
        }

        throw new InvalidOperationException("repo root (.git) not found from " + AppDomain.CurrentDomain.BaseDirectory);
    }
}
