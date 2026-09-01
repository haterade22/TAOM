using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// Shipped-data invariants for the enlistment kits, on the slots the engine will actually issue.
///
/// <para><b>Why a C# test and not just the Python auditor.</b>
/// <c>tools/audit_enlistment_roster_coverage.py</c> checks all of this and more, but nothing runs
/// it automatically: the ModuleData commit hook invokes <c>validate_moduledata.py</c> only.
/// This suite runs in the <c>dotnet test</c> that <c>/verify</c> and the completion workflow
/// perform on every change, so it is the half that fires without anybody remembering to.
/// It is NOT "the CI-reachable half": the workflow's build job is gated on
/// <c>vars.BANNERLORD_GAME_DIR != ''</c>, which is unset, so CI compiles no C# at all. It
/// deliberately covers only what needs no game install, so it stays green on a machine with no
/// Bannerlord; the per-assignment content rules (an archer carries a bow, a bow carries arrows)
/// need the item registry and stay in the Python gate.</para>
///
/// <para><b>Why these slots.</b> <c>EquipmentRosterCatalogAdapter.GetBattleSetItemIds</c>
/// reads <c>WeaponItemBeginSlot</c> (0) through <c>NumEquipmentSetSlots</c> (12), so anything in
/// any of the twelve is issued into the player's baggage. <c>Horse</c> (10) and
/// <c>HorseHarness</c> (11) are excluded at every assignment, cavalry included: the cavalry donor
/// pools mount mûmakil, war elephants and chariots, and the roster is keyed on the COMMANDER's
/// culture, so it cannot know that the player is a dwarf who would spawn inside a horse.
/// <c>Item4</c> is <c>ExtraWeaponSlot</c> in the installed v1.4.8 enum; a banner is one eligible
/// occupant of it rather than the slot's name. None is reachable from the generator today, which
/// is exactly why a hand edit is the way one would arrive.</para>
/// </summary>
[TestClass]
public class EnlistmentRosterSlotInvariantsTests
{
    private static readonly string RosterPath = Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..",
            "Main", "_Module", "ModuleData", "equipmentsets", "taom_enlistment_equipment.xml"));

    private static readonly HashSet<string> AllowedSlots =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "Item0", "Item1", "Item2", "Item3",
            "Head", "Body", "Leg", "Gloves", "Cape",
        };

    private static readonly HashSet<string> WeaponSlots =
        new HashSet<string>(StringComparer.Ordinal) { "Item0", "Item1", "Item2", "Item3" };

    private static readonly string[] Assignments = { "infantry", "archer", "cavalry", "support" };
    private static readonly string[] RankTokens = { "recruit", "soldier", "veteran", "sergeant" };

    /// <summary>
    /// enlist_{culture}_{assignment}_{rank} -> its parts, or null. Splits from the RIGHT, because
    /// a culture token may contain an underscore (mistymountainorcs does not, but nothing stops
    /// the next one) while the assignment and rank tokens never do.
    /// </summary>
    private static (string Culture, string Assignment, string Rank)? SplitId(string id)
    {
        if (!id.StartsWith("enlist_", StringComparison.Ordinal))
            return null;
        var body = id.Substring("enlist_".Length);
        var rank = RankTokens.FirstOrDefault(r => body.EndsWith("_" + r, StringComparison.Ordinal));
        if (rank == null)
            return null;
        body = body.Substring(0, body.Length - rank.Length - 1);
        var assignment = Assignments.FirstOrDefault(a => body.EndsWith("_" + a, StringComparison.Ordinal));
        if (assignment == null)
            return null;
        var culture = body.Substring(0, body.Length - assignment.Length - 1);
        return culture.Length == 0 ? null : (culture, assignment, rank);
    }

    private static List<XElement> Rosters()
    {
        Assert.IsTrue(File.Exists(RosterPath), $"taom_enlistment_equipment.xml not found at {RosterPath}");
        var rosters = XDocument.Load(RosterPath).Descendants("EquipmentRoster").ToList();
        Assert.IsTrue(rosters.Count > 0, "No rosters parsed — a test that finds nothing to check "
            + "passes for the wrong reason.");
        return rosters;
    }

    [TestMethod]
    public void NoRoster_CarriesAMountOrTheBannerSlot()
    {
        var offenders = (from roster in Rosters()
                         from equipment in roster.Descendants("Equipment")
                         let slot = (string?)equipment.Attribute("slot")
                         where slot == null || !AllowedSlots.Contains(slot)
                         select $"{(string?)roster.Attribute("id")}: slot='{slot}' "
                             + $"item='{(string?)equipment.Attribute("id")}'").ToList();

        Assert.AreEqual(0, offenders.Count,
            "GetBattleSetItemIds reads all 12 slots, so anything here is issued into the player's "
            + "baggage. Horse/HorseHarness are excluded at every assignment (the roster is keyed "
            + "on the commander's culture and cannot know the player's race; MOUNTED_DWARF cannot "
            + "see these rosters at all), and Item4 is the engine's ExtraWeaponSlot. Allowed: "
            + string.Join(", ", AllowedSlots.OrderBy(s => s, StringComparer.Ordinal)) + "\n  "
            + string.Join("\n  ", offenders));
    }

    [TestMethod]
    public void EveryRoster_CarriesAtLeastOneWeapon()
    {
        // The lower bound, and the one this whole change exists to enforce. #525 was "the service
        // kit has no weapons"; the first fix for it shipped 15 rosters that still had none,
        // because the generator emitted an armour-only cell whenever the donor carried no
        // OneHanded item. Every gate passed, because every gate asked what a kit must NOT contain
        // and none asked what it MUST.
        //
        // A weaponless roster is worse than a missing one: EnlistmentRosterResolver probes
        // EXISTENCE, so a present-but-empty cell ENDS the fallback walk and shadows the armed kit
        // the player would otherwise have descended to. The generator now suppresses such a cell;
        // this is the assertion that keeps it suppressed.
        var weaponless = (from roster in Rosters()
                          let slots = roster.Descendants("Equipment")
                              .Select(e => (string?)e.Attribute("slot")).ToList()
                          where !slots.Any(s => s != null && WeaponSlots.Contains(s))
                          select (string?)roster.Attribute("id")).ToList();

        Assert.AreEqual(0, weaponless.Count,
            "These rosters carry armour and no weapon, which is the reported #525 defect verbatim. "
            + "The cell must be ABSENT (the resolver then falls back inside the culture to an armed "
            + "kit), never emitted armour-only:\n  " + string.Join("\n  ", weaponless));
    }

    [TestMethod]
    public void NoRankChain_IssuesTheSameKitTwice()
    {
        // The ledger spends one draw per rank, so a promotion that hands back a kit the player has
        // already drawn is a wasted draw and a pile of duplicate items in his baggage. The resolver
        // descends ranks, so the generator suppresses a repeat and the player keeps the lower cell:
        // same outcome, one roster instead of several, and the file stops claiming a progression
        // the donor tree does not have.
        var chains = new Dictionary<string, List<(string Id, string Kit)>>(StringComparer.Ordinal);
        foreach (var roster in Rosters())
        {
            var id = (string?)roster.Attribute("id") ?? "";
            if (id.StartsWith("enlist_default_", StringComparison.Ordinal))
                continue;
            var parsed = SplitId(id);
            if (parsed == null)
                continue;
            var kit = string.Join("|", roster.Descendants("Equipment")
                .Select(e => $"{(string?)e.Attribute("slot")}={(string?)e.Attribute("id")}")
                .OrderBy(s => s, StringComparer.Ordinal));
            var key = $"{parsed.Value.Culture}/{parsed.Value.Assignment}";
            if (!chains.TryGetValue(key, out var list))
                chains[key] = list = new List<(string, string)>();
            list.Add((id, kit));
        }

        var repeats = (from chain in chains
                       from g in chain.Value.GroupBy(x => x.Kit)
                       where g.Count() > 1
                       select $"{chain.Key}: {string.Join(", ", g.Select(x => x.Id))}").ToList();

        Assert.AreEqual(0, repeats.Count,
            "These rank chains issue a byte-identical kit at more than one rank:\n  "
            + string.Join("\n  ", repeats));
    }

    [TestMethod]
    public void EveryRoster_HasExactlyOneEquipmentSet_AndIsNotEmpty()
    {
        // A second <EquipmentSet> is not a harmless duplicate: GetBattleSetItemIds takes the FIRST
        // battle set, so the rest is authored data that silently never reaches a player.
        var problems = new List<string>();
        foreach (var roster in Rosters())
        {
            var id = (string?)roster.Attribute("id");
            var sets = roster.Elements("EquipmentSet").ToList();
            if (sets.Count != 1)
                problems.Add($"{id}: {sets.Count} <EquipmentSet> elements, expected exactly 1");
            if (!roster.Descendants("Equipment").Any())
                problems.Add($"{id}: no <Equipment> at all — issues nothing");
        }

        Assert.AreEqual(0, problems.Count, string.Join("\n  ", problems));
    }

    [TestMethod]
    public void EveryRoster_HasAUniqueId_AndDeclaresACulture()
    {
        // A duplicate id is silently shadowed by the engine, so one of the two kits is dead data.
        var problems = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var roster in Rosters())
        {
            var id = (string?)roster.Attribute("id");
            if (string.IsNullOrEmpty(id))
            {
                problems.Add("a roster carries no id");
                continue;
            }
            if (!seen.Add(id!))
                problems.Add($"{id}: duplicate id — the engine keeps one and drops the other");
            if (string.IsNullOrEmpty((string?)roster.Attribute("culture")))
                problems.Add($"{id}: no culture attribute (the engine logs this once per roster "
                    + "at load)");
        }

        Assert.AreEqual(0, problems.Count, string.Join("\n  ", problems));
    }
}
