using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.Enlistment.Equipment;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// Cross-references TAOM's main cultures against the shipped enlistment kits, through the REAL
/// <see cref="EnlistmentRosterResolver"/> rather than by rebuilding ids by hand.
///
/// <para>The hazard is #431. A culture with no roster of its own does not fail, log or crash: the
/// resolver walks out to <c>enlist_default_*</c>, which is tagged <c>Culture.neutral_culture</c>
/// while being Rohan militia in Dunland boots. So <c>abanissa</c> and <c>shaghana</c>, 17 lord
/// clans between them, quietly dress their soldiers as Rohan. Since #525 that kit carries weapons
/// too, so the same gap now hands out the wrong faction's arms as well as its armour.</para>
///
/// <para>These assert on what the resolver RETURNS, not on which ids exist. Under
/// <c>enlist_{culture}_{assignment}_{rank}</c> a cell is legitimately absent whenever the
/// culture's troop tree has no donor of that group within one band of the rank (goblin fields no
/// cavalry; bluecraig and mistymountainorcs field neither cavalry nor archers). Demanding every
/// id would fail on data that is correct. What actually matters to the player is that every
/// request lands on HIS OWN culture's gear, and only the resolver can answer that.</para>
///
/// <para>The predicate for "main culture" is <c>is_main_culture="true"</c> in
/// <c>taom_spcultures.xml</c> — the same one <c>taom_schema.py</c>'s MISSING_EDUCATION_TEMPLATES
/// rule uses, for the same reason: derived from the data rather than hand-maintained. Four TAOM
/// systems have shipped a per-culture gap (careers, narrative options, narrative strings,
/// education templates), every one through a list written before the missing culture existed.</para>
///
/// <para>The vanilla six (aserai, battania, empire, khuzait, sturgia, vlandia) are not main
/// cultures but do have rosters, since TAOM re-skins them as Harad, Khand, Dunland, Rhûn, Dale
/// and Rohan. They are covered incidentally, not by this rule.</para>
/// </summary>
[TestClass]
public class EnlistmentRosterCultureCoverageTests
{
    private static readonly string ModuleDataPath = Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..",
            "Main", "_Module", "ModuleData"));

    /// <summary>
    /// Derived from production, not copied from it. A hard-coded list here would have carried the
    /// comment "mirrors <c>EnlistmentRosterIds.RankToken</c>" while being free to drift from it —
    /// the exact defect this repo has now shipped three times (see
    /// <c>lessons/testing-qa.md</c>, "a comment is a claim"). Adding a fifth
    /// <see cref="EnlistmentRank"/> extends the coverage requirement automatically.
    /// </summary>
    private static readonly EnlistmentRank[] Ranks =
        Enum.GetValues(typeof(EnlistmentRank)).Cast<EnlistmentRank>().ToArray();

    /// <summary>Same derivation, one axis over: a fifth assignment extends coverage by itself.</summary>
    private static readonly ServiceAssignment[] Assignments =
        Enum.GetValues(typeof(ServiceAssignment)).Cast<ServiceAssignment>().ToArray();

    /// <summary>
    /// Main cultures knowingly without rosters. Removing an entry is the last step of authoring
    /// that culture's kits; adding one requires an issue explaining why that culture's lords issue
    /// another faction's gear.
    /// </summary>
    private static readonly HashSet<string> DocumentedExceptions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // #431 — 8 abanissa and 9 shaghana lord clans whose soldiers are dressed as Rohan
            // militia by the fallback. Blocked on a decision: author the rosters, or make the
            // fallback genuinely culture-neutral so no future culture inherits the same answer.
            "abanissa",
            "shaghana",
        };

    private static readonly Regex CultureElement = new Regex(@"<Culture\b([^>]*?)/?>", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex IdAttribute = new Regex(@"\bid=""([A-Za-z0-9_.\-]+)""", RegexOptions.Compiled);

    private static List<string> LoadMainCultures()
    {
        var path = Path.Combine(ModuleDataPath, "taom_spcultures.xml");
        Assert.IsTrue(File.Exists(path), $"taom_spcultures.xml not found at {path}");

        var cultures = new List<string>();
        foreach (Match element in CultureElement.Matches(File.ReadAllText(path)))
        {
            var attributes = element.Groups[1].Value;
            if (!attributes.Contains("is_main_culture=\"true\""))
                continue;

            var id = IdAttribute.Match(attributes);
            if (id.Success)
                cultures.Add(id.Groups[1].Value);
        }

        Assert.IsTrue(cultures.Count > 0, "No main cultures parsed from taom_spcultures.xml — the "
            + "parse is broken, and a test that finds nothing to check passes for the wrong reason.");
        return cultures;
    }

    private static HashSet<string> LoadRosterIds()
    {
        var path = Path.Combine(ModuleDataPath, "equipmentsets", "taom_enlistment_equipment.xml");
        Assert.IsTrue(File.Exists(path), $"taom_enlistment_equipment.xml not found at {path}");

        var ids = new HashSet<string>(
            XDocument.Load(path).Descendants("EquipmentRoster")
                .Select(r => (string?)r.Attribute("id"))
                .Where(id => !string.IsNullOrEmpty(id))!,
            StringComparer.OrdinalIgnoreCase);

        Assert.IsTrue(ids.Count > 0, "No roster ids parsed — a test that finds nothing to check "
            + "passes for the wrong reason.");
        return ids;
    }

    /// <summary>Resolve through the shipped roster ids using the production chain.</summary>
    private static string? Resolve(HashSet<string> rosters, string culture,
        ServiceAssignment assignment, EnlistmentRank rank)
        => EnlistmentRosterResolver.Resolve(culture, assignment, rank, rosters.Contains);

    [TestMethod]
    public void EveryMainCulture_ResolvesToItsOwnKit_ForEveryAssignmentAndRank_OrIsDocumented()
    {
        var rosters = LoadRosterIds();

        var wrong = new List<string>();
        foreach (var culture in LoadMainCultures())
        {
            if (DocumentedExceptions.Contains(culture))
                continue;

            foreach (var assignment in Assignments)
            foreach (var rank in Ranks)
            {
                var resolved = Resolve(rosters, culture, assignment, rank);
                var expectedPrefix = $"enlist_{culture}_";
                if (resolved == null)
                    wrong.Add($"{culture}/{assignment}/{rank}: resolved to NOTHING");
                else if (!resolved.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
                    wrong.Add($"{culture}/{assignment}/{rank}: resolved to '{resolved}'");
            }
        }

        Assert.AreEqual(0, wrong.Count,
            "A main culture whose request leaves its own rosters does not fail — it silently "
            + "issues another faction's gear, now weapons included (#431/#525). Author the "
            + "culture's kits, or document the exception:\n  " + string.Join("\n  ", wrong));
    }

    /// <summary>
    /// Every runtime culture a player can actually enlist under, derived from the TROOP DATA:
    /// the culture of every non-hero Soldier in <c>troops_*.xml</c>, plus the two tree-borrowers
    /// that own no troop file but bind to another culture's tree.
    ///
    /// <para>Derived from the troop data and NOT from the roster file, which is the whole point.
    /// An earlier version of this test parsed its culture list out of the very ids it was
    /// auditing, so it could not fail on the two mutations that matter: deleting every
    /// <c>enlist_vlandia_*</c> row removed Rohan from the test's own input, and renaming them to
    /// <c>enlist_rohan_*</c> made the test happily accept a culture StringId that does not exist.
    /// Both left runtime Rohan falling through to the neutral default, and both stayed green.</para>
    /// </summary>
    private static List<string> EnlistableCultures()
    {
        var troopsDir = Path.Combine(ModuleDataPath, "troops");
        Assert.IsTrue(Directory.Exists(troopsDir), $"troops dir not found at {troopsDir}");

        var cultures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.GetFiles(troopsDir, "troops_*.xml"))
        {
            foreach (var npc in XDocument.Load(path).Descendants("NPCCharacter"))
            {
                if ((string?)npc.Attribute("is_hero") == "true")
                    continue;
                if (((string?)npc.Attribute("occupation") ?? "Soldier") != "Soldier")
                    continue;
                var raw = (string?)npc.Attribute("culture") ?? "";
                if (raw.StartsWith("Culture.", StringComparison.Ordinal))
                    cultures.Add(raw.Substring("Culture.".Length));
            }
        }

        // lothlorien and battania own no troops file but bind to another culture's tree
        // (taom_spcultures.xml / spcultures.xslt), so they ship rosters and are enlistable.
        cultures.Add("lothlorien");
        cultures.Add("battania");

        Assert.IsTrue(cultures.Count >= 20,
            $"Only {cultures.Count} enlistable cultures parsed from the troop data; the parse is "
            + "broken, and a test that checks almost nothing passes for the wrong reason.");
        return cultures.OrderBy(c => c, StringComparer.Ordinal).ToList();
    }

    [TestMethod]
    public void EveryEnlistableCulture_ResolvesToItsOwnKit_ForEveryAssignmentAndRank()
    {
        // Absent cells are legitimate: they fall through to the culture's own infantry kit. What is
        // NOT legitimate is a request that resolves to null (the quartermaster issues nothing) or
        // to enlist_default_* (the player is dressed as another faction, which is #427/#431).
        //
        // The culture list comes from the troop data, so deleting or renaming a culture's rosters
        // FAILS here instead of quietly shrinking the test's own input.
        var rosters = LoadRosterIds();

        var wrong = (from culture in EnlistableCultures()
                     from assignment in Assignments
                     from rank in Ranks
                     let resolved = Resolve(rosters, culture, assignment, rank)
                     where resolved == null
                        || !resolved.StartsWith($"enlist_{culture}_", StringComparison.OrdinalIgnoreCase)
                     select $"{culture}/{assignment}/{rank} -> {resolved ?? "NOTHING"}").ToList();

        Assert.AreEqual(0, wrong.Count,
            "Every culture a player can enlist under must resolve to its OWN kit for every "
            + "assignment and rank. These leave the culture or resolve to nothing:\n  "
            + string.Join("\n  ", wrong));
    }

    [TestMethod]
    public void EveryRank_MapsToItsOwnToken()
    {
        // RankToken ends in `_ => "recruit"`, so a rank added without a case silently ALIASES the
        // recruit roster: the new rank looks covered, issues starting gear forever, and no other
        // test can see it because the id it asks for exists. Deriving Ranks above from the enum is
        // not enough on its own — this is the half that makes the derivation meaningful.
        var collisions = Ranks
            .GroupBy(EnlistmentRosterIds.RankToken)
            .Where(g => g.Count() > 1)
            .Select(g => $"'{g.Key}' <- {string.Join(", ", g)}")
            .ToList();

        Assert.AreEqual(0, collisions.Count,
            "Two EnlistmentRank values produce the same roster token, so one silently wears the "
            + "other's kit. Add the missing case to EnlistmentRosterIds.RankToken:\n  "
            + string.Join("\n  ", collisions));
    }

    [TestMethod]
    public void EveryAssignment_MapsToItsOwnToken()
    {
        // The same trap one axis over, and it is live: AssignmentToken ends in
        // `_ => "infantry"`, deliberately, so that an ordinal outside the enum lands on a kit
        // that exists. The cost of that safety is that a NEW assignment added without a case
        // would silently draw the infantry kit and look covered.
        var collisions = Assignments
            .GroupBy(EnlistmentRosterIds.AssignmentToken)
            .Where(g => g.Count() > 1)
            .Select(g => $"'{g.Key}' <- {string.Join(", ", g)}")
            .ToList();

        Assert.AreEqual(0, collisions.Count,
            "Two ServiceAssignment values produce the same roster token, so one silently wears "
            + "the other's kit. Add the missing case to EnlistmentRosterIds.AssignmentToken:\n  "
            + string.Join("\n  ", collisions));
    }

    [TestMethod]
    public void TheDefaultFallback_CoversEveryAssignmentAndRank()
    {
        // The fallback is what every undocumented gap lands on. A missing cell here turns a
        // cosmetic problem into an unequipped soldier.
        var rosters = LoadRosterIds();

        var missing = (from assignment in Assignments
                       from rank in Ranks
                       let id = EnlistmentRosterIds.BuildDefault(assignment, rank)
                       where !rosters.Contains(id)
                       select id).ToList();

        Assert.AreEqual(0, missing.Count,
            "The culture-neutral fallback is missing a cell: " + string.Join(", ", missing));
    }

    [TestMethod]
    public void DocumentedExceptions_AreStillMainCulturesAndStillUnauthored()
    {
        // A resolved exception is a stale suppression: it silently widens the blind spot for
        // whoever inherits it, and nothing else would ever prompt its deletion.
        //
        // This asks the RESOLVER whether the culture is still falling out to the default. The
        // previous version counted ids of the form `enlist_{culture}_{rank}`, which no id has
        // taken since #525 — so it scored zero for every culture, forever, and would have gone on
        // passing after the rosters were authored. That is precisely the decay it exists to catch.
        var cultures = LoadMainCultures();
        var rosters = LoadRosterIds();
        var stale = new List<string>();

        foreach (var exception in DocumentedExceptions)
        {
            if (!cultures.Contains(exception, StringComparer.OrdinalIgnoreCase))
            {
                stale.Add($"{exception}: no longer a main culture — delete the exception");
                continue;
            }

            var cells = (from assignment in Assignments
                         from rank in Ranks
                         select Resolve(rosters, exception, assignment, rank)).ToList();
            var own = cells.Count(id =>
                id != null && id.StartsWith($"enlist_{exception}_", StringComparison.OrdinalIgnoreCase));

            if (own == cells.Count)
                stale.Add($"{exception}: every cell now resolves to its own kit — delete the "
                    + "exception and close #431");
            else if (own > 0)
                stale.Add($"{exception}: partially authored ({own}/{cells.Count} cells) — the rest "
                    + "still fall back to Rohan gear, and a half-finished culture is harder to "
                    + "spot than an untouched one; finish it or revert");
        }

        Assert.AreEqual(0, stale.Count,
            "Stale entries in DocumentedExceptions:\n  " + string.Join("\n  ", stale));
    }
}
