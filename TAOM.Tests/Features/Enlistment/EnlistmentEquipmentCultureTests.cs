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
/// <c>sk_gd_ano_gloves_a</c> in <c>enlist_vlandia_recruit</c> (a two-token id at the time;
/// the ids gained an assignment token in #525, and the <c>enlist_vlandia_</c> prefix this
/// guard matches on covers both shapes). Since #525 the kits carry WEAPONS too, and the
/// item-prefix filter picks those up for free: a Gondor sword in a Rohan kit reddens here
/// exactly as the gloves did.
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
        // 16 today: 4 assignments x 4 ranks. Asserted as a floor rather than an equality
        // because a culture may legitimately lose a cell when its troop tree has no donor of
        // that group within a band of the rank. Dropping below the four ranks means the file
        // layout changed and this guard needs re-aiming, not that data got thinner.
        Assert.IsTrue(vlandiaRosters.Count >= 4,
            $"Expected at least the four vlandia rank rosters, found {vlandiaRosters.Count}; "
            + "the file layout changed and this guard needs re-aiming.");

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
